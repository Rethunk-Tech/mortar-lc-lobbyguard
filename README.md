# LobbyGuard

Stops a bad client from crashing or freezing your lobby. It checks what each client sends before Netcode for
GameObjects reads it, and disconnects a client that sends something broken or floods you with packets.

- Who needs it: the host. Clients don't.
- Config: none.

Each kick goes in the BepInEx log with the player's SteamID and why, so you can block them on Steam. Kicked players stay kicked: the game's kick list is checked against the
SteamID Steam vouches for.

It can't stop a flood that fills your internet connection, since that happens before the game sees anything.

## Patched methods

- `Unity.Netcode.NetworkConnectionManager.DataEventHandler` (a MonoMod `Hook`)
- `GameNetworkManager.ConnectionApproval` (a MonoMod `Hook`)
- `Dissonance.Networking.Server.BroadcastingClientCollection<NfgoConn>.ProcessHandshakeRequest` and
  `ProcessClientState`, for voice chat (MonoMod `Hook`s)
- Every RPC handler that reads a list or text from the network, in the game and in other mods (an `ILHook` each, as
  their assemblies load)
