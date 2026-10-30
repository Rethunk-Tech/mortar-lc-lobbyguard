using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEngine;

namespace LobbyGuard;

[BepInPlugin(MyPluginInfo.Id, MyPluginInfo.Name, MyPluginInfo.Version)]
[BepInProcess("Lethal Company.exe")]
public class Plugin : BaseUnityPlugin
{
    // Netcode sends a client's packets once per channel per frame, so this leaves room for a very high frame rate.
    private const int MaxPacketsPerSecond = 2000;

    // A packet can hold thousands of tiny messages, and NGO logs an error for each one it can't use.
    private const int MaxMessagesPerSecond = 20000;

    internal static ManualLogSource Log { get; private set; } = null!;

    private static readonly Dictionary<ulong, (float Start, int Packets, int Messages)> rates = new();

    private Hook? hook;

    private delegate void DataEvent(NetworkConnectionManager self, ulong transportId, ref ArraySegment<byte> payload, float receiveTime);

    private void Awake()
    {
        Log = Logger;
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        hook = new Hook(typeof(NetworkConnectionManager).GetMethod(nameof(NetworkConnectionManager.DataEventHandler), any),
            typeof(Plugin).GetMethod(nameof(OnData), any));
        Lengths.HookAll();
        Approval.Hook();
        Voice.Hook();
        Vanilla.Hook();
        Log.LogInfo($"{MyPluginInfo.Name} {MyPluginInfo.Version} loaded");
    }

    private static void OnData(DataEvent orig, NetworkConnectionManager self, ulong transportId, ref ArraySegment<byte> payload, float receiveTime)
    {
        if (!self.LocalClient.IsServer)
        {
            orig(self, transportId, ref payload, receiveTime);
            return;
        }
        // NGO takes data from a transport id it doesn't know as the host's own. A kicked client's leftover packets
        // arrive that way.
        if (!self.TransportIdToClientIdMap.TryGetValue(transportId, out ulong clientId))
        {
            return;
        }
        uint? connectRequest = self.NetworkManager.NetworkConfig.ConnectionApproval
            ? self.MessageManager.GetMessageType(typeof(ConnectionRequestMessage))
            : null;
        string? why = Batch.Check(payload, Hash, connectRequest) ??
            (Flooding(transportId, BitConverter.ToUInt16(payload.Array!, payload.Offset + 2)) ? "too many packets or messages" : null);
        if (why == null)
        {
            orig(self, transportId, ref payload, receiveTime);
            return;
        }
        rates.Remove(transportId);
        Kick(self.NetworkManager, clientId, why);
    }

    // Names the player by SteamID in online games, so the host can block them. LAN games only have Netcode's number.
    internal static void Kick(NetworkManager manager, ulong clientId, string why)
    {
        ulong transportId = manager.ConnectionManager.ClientIdToTransportId(clientId);
        string who = manager.NetworkConfig.NetworkTransport is FacepunchTransport steam &&
            steam.connectedClients.TryGetValue(transportId, out FacepunchTransport.Client player)
            ? $"Steam user {player.steamId.Value}"
            : $"client {clientId}";
        Log.LogWarning($"Disconnected {who}: {why}");
        manager.DisconnectClient(clientId);
    }

    private static unsafe ulong Hash(ArraySegment<byte> body)
    {
        fixed (byte* p = body.Array)
        {
            return XXHash.Hash64(p + body.Offset, body.Count);
        }
    }

    private static bool Flooding(ulong transportId, int messages)
    {
        float now = Time.realtimeSinceStartup;
        (float start, int packets, int total) = rates.TryGetValue(transportId, out (float Start, int Packets, int Messages) rate) &&
            now - rate.Start < 1 ? rate : (now, 0, 0);
        rates[transportId] = (start, ++packets, total += messages);
        return packets > MaxPacketsPerSecond || total > MaxMessagesPerSecond;
    }
}
