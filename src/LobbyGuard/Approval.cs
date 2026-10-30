using System.Reflection;
using System.Text;
using MonoMod.RuntimeDetour;
using Netcode.Transports.Facepunch;
using Unity.Netcode;

namespace LobbyGuard;

// The game checks its kick list against the SteamID a joining client writes in its request. Steam has already proven
// who's on the other end of the connection, so a request has to name that same ID.
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

    // The request reads "<game version>,<SteamID>".
    public static bool Names(byte[] payload, ulong steamId)
    {
        string[] parts = Encoding.ASCII.GetString(payload).Split(',');
        return parts.Length >= 2 && ulong.TryParse(parts[1], out ulong claimed) && claimed == steamId;
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
        string? why = !Names(request.Payload, steamId) ? "SteamID in the request isn't the connection's"
            : StartOfRound.Instance != null && StartOfRound.Instance.KickedClientIds.Contains(steamId) ? "kicked earlier"
            : null;
        if (why == null)
        {
            orig(self, request, response);
            return;
        }
        Plugin.Log.LogWarning($"Turned away Steam user {steamId}: {why}");
        response.Reason = why == "kicked earlier" ? "You cannot rejoin after being kicked." : "Your SteamID doesn't match your connection.";
        response.CreatePlayerObject = false;
        response.Approved = false;
        response.Pending = false;
    }
}
