using System.Reflection;
using MonoMod.RuntimeDetour;
using Netcode.Transports.Facepunch;
using Unity.Netcode;

namespace LobbyGuard;

// The game checks its kick list against the SteamID a joining client writes in its request, which the client can
// change. Steam has already proven who's on the other end of the connection, so the kick list is checked against that.
internal static class Approval
{
    private delegate void Orig(GameNetworkManager self, NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response);

    private static Hook? hook;

    public static void Hook()
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        hook = new Hook(typeof(GameNetworkManager).GetMethod(nameof(GameNetworkManager.ConnectionApproval), any),
            typeof(Approval).GetMethod(nameof(Approve), any));
    }

    private static void Approve(Orig orig, GameNetworkManager self, NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        NetworkManager manager = NetworkManager.Singleton;
        ulong transportId = manager.ConnectionManager.ClientIdToTransportId(request.ClientNetworkId);
        if (request.ClientNetworkId == manager.LocalClientId || manager.NetworkConfig.NetworkTransport is not FacepunchTransport steam ||
            !steam.connectedClients.TryGetValue(transportId, out FacepunchTransport.Client player))
        {
            orig(self, request, response);
            return;
        }
        ulong steamId = player.steamId.Value;
        if (StartOfRound.Instance == null || !StartOfRound.Instance.KickedClientIds.Contains(steamId))
        {
            orig(self, request, response);
            return;
        }
        Plugin.Log.LogWarning($"Turned away Steam user {steamId}: kicked earlier");
        response.Reason = "You cannot rejoin after being kicked.";
        response.CreatePlayerObject = false;
        response.Approved = false;
        response.Pending = false;
    }
}
