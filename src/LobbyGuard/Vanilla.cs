using System.Reflection;
using MonoMod.RuntimeDetour;
using Steamworks;
using Steamworks.Data;

namespace LobbyGuard;

// The game tags a lobby as modded whenever BepInEx is installed. LobbyGuard doesn't change how the game plays, so the
// lobby keeps showing as vanilla and players who only look for vanilla lobbies still see it.
internal static class Vanilla
{
    private delegate void Orig(GameNetworkManager self, Result result, Lobby lobby);

    private static Hook? hook;

    public static void Hook()
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        hook = new Hook(typeof(GameNetworkManager).GetMethod(nameof(GameNetworkManager.SteamMatchmaking_OnLobbyCreated), any),
            typeof(Vanilla).GetMethod(nameof(Created), any));
    }

    private static void Created(Orig orig, GameNetworkManager self, Result result, Lobby lobby)
    {
        orig(self, result, lobby);
        if (result == Result.OK)
        {
            lobby.SetData("dmods", "f");
        }
    }
}
