using System.Text;
using Xunit;

namespace LobbyGuard.Tests;

public sealed class ApprovalTests
{
    [Fact]
    public void RequestMustNameTheConnectionsSteamId()
    {
        Assert.True(Approval.Names(Encoding.ASCII.GetBytes("81,76561198000000001"), 76561198000000001));
        Assert.False(Approval.Names(Encoding.ASCII.GetBytes("81,76561198000000002"), 76561198000000001));
        Assert.False(Approval.Names(Encoding.ASCII.GetBytes("81"), 76561198000000001));
        Assert.False(Approval.Names(Encoding.ASCII.GetBytes("81,-1"), 76561198000000001));
    }
}
