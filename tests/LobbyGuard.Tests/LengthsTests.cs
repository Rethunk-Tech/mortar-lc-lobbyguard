using Xunit;

namespace LobbyGuard.Tests;

public sealed class LengthsTests
{
    [Fact]
    public void LengthMustFitWhatsLeft()
    {
        Assert.True(Lengths.Fits(3, 4, 12));
        Assert.True(Lengths.Fits(0, 4, 0));
        Assert.False(Lengths.Fits(4, 4, 12));
        Assert.False(Lengths.Fits(-1, 1, 100));
        Assert.False(Lengths.Fits(int.MaxValue, 8, int.MaxValue));
    }
}
