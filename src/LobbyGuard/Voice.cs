using System;
using System.Reflection;
using Dissonance;
using Dissonance.Audio.Codecs;
using Dissonance.Integrations.Unity_NFGO;
using Dissonance.Networking;
using Dissonance.Networking.Server;
using MonoMod.RuntimeDetour;
using Unity.Netcode;

namespace LobbyGuard;

// Voice chat sizes its decoder buffers from the codec settings each player sends, and the host passes them on to
// everyone. The game only ever sends Opus at 48 kHz with one of Opus's frame sizes, or Dissonance's fixed Identity
// settings, so the host drops anything else, or anything cut short, and disconnects whoever sent it.
//
// The hooks sit on the host's void handlers. The readers themselves return structs, and a detour on a struct method
// that returns a struct gets its arguments swapped on Windows.
internal static class Voice
{
    private static readonly int[] opusFrameSizes = { 120, 240, 480, 960, 1920, 2880 };

    private delegate void Orig(BroadcastingClientCollection<NfgoConn> self, NfgoConn source, ref PacketReader reader);

    private static Hook? handshake;
    private static Hook? state;

    public static void Hook()
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type clients = typeof(BroadcastingClientCollection<NfgoConn>);
        handshake = new Hook(clients.GetMethod(nameof(BroadcastingClientCollection<NfgoConn>.ProcessHandshakeRequest), any),
            typeof(Voice).GetMethod(nameof(Handshake), any));
        state = new Hook(clients.GetMethod(nameof(BroadcastingClientCollection<NfgoConn>.ProcessClientState), any),
            typeof(Voice).GetMethod(nameof(State), any));
    }

    public static bool Allowed(byte codec, uint frameSize, int sampleRate) => codec switch
    {
        (byte)Codec.Opus => sampleRate == 48000 && Array.IndexOf(opusFrameSizes, (int)frameSize) >= 0,
        (byte)Codec.Identity => sampleRate == 44100 && frameSize == 441,
        _ => false,
    };

    private static void Handshake(Orig orig, BroadcastingClientCollection<NfgoConn> self, NfgoConn source, ref PacketReader reader)
    {
        PacketReader copy = reader;
        if (Check(source, () =>
        {
            copy.ReadHandshakeRequest(out _, out CodecSettings settings);
            return settings;
        }))
        {
            orig(self, source, ref reader);
        }
    }

    private static void State(Orig orig, BroadcastingClientCollection<NfgoConn> self, NfgoConn source, ref PacketReader reader)
    {
        PacketReader copy = reader;
        if (Check(source, () => copy.ReadClientStateHeader().CodecSettings))
        {
            orig(self, source, ref reader);
        }
    }

    // Reads the settings from a copy of the packet, so the game still reads it from the start.
    private static bool Check(NfgoConn source, Func<CodecSettings> read)
    {
        string why;
        try
        {
            CodecSettings settings = read();
            if (Allowed((byte)settings.Codec, settings.FrameSize, settings.SampleRate))
            {
                return true;
            }
            why = "bad voice codec settings";
        }
        catch (Exception)
        {
            why = "broken voice packet";
        }
        NetworkManager? manager = NetworkManager.Singleton;
        if (manager != null && manager.IsServer && source.ClientId != manager.LocalClientId)
        {
            Plugin.Kick(manager, source.ClientId, why);
        }
        else
        {
            Plugin.Log.LogWarning($"Dropped a voice packet from this game: {why}");
        }
        return false;
    }
}
