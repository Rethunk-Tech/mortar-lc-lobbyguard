using Xunit;

namespace LobbyGuard.Tests;

public sealed class VoiceTests
{
    [Fact]
    public void OnlyTheGamesOwnCodecSettings()
    {
        Assert.True(Voice.Allowed(1, 960, 48000));
        Assert.True(Voice.Allowed(1, 2880, 48000));
        Assert.True(Voice.Allowed(0, 441, 44100));
        Assert.False(Voice.Allowed(1, 961, 48000));
        Assert.False(Voice.Allowed(1, uint.MaxValue, 48000));
        Assert.False(Voice.Allowed(1, 960, 1));
        Assert.False(Voice.Allowed(0, 100000, 44100));
        Assert.False(Voice.Allowed(2, 960, 48000));
    }
}
