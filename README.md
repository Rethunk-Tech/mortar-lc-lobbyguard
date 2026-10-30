# LobbyGuard

Keeps bad players from crashing or freezing your lobby.

- Who needs it: the host. Players don't.
- Config: none.

It checks what each player sends, voice chat included, before the game reads it. Anyone who sends something broken,
or far more than the game ever would, gets disconnected. Players you kick stay kicked.

Each kick goes in the BepInEx log with the player's SteamID and the reason, so you can block them on Steam.

It can't do anything about a flood big enough to fill your internet connection.
