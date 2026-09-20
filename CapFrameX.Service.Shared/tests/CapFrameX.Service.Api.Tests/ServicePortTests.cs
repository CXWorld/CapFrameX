namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// The port the service takes.
/// </summary>
public sealed class ServicePortTests
{
    [Fact]
    public void The_test_host_uses_the_service_port()
    {
        // GuardedApiFactory.OwnHost has to be a compile-time constant, so it repeats the number.
        // This is what keeps the copy honest.
        Assert.Equal($"127.0.0.1:{CapFrameXApiOptions.DefaultPort}", GuardedApiFactory.OwnHost);
    }

    [Fact]
    public void The_default_port_is_not_the_one_CapFrameX_1_x_serves_on()
    {
        // 1.x's own web service defaults to 1337, and the two applications run side by side while
        // records are moved over. Sharing the number means whichever starts second does not.
        Assert.NotEqual(1337, CapFrameXApiOptions.DefaultPort);
    }

    [Theory]
    [InlineData("18000", 18000)]
    [InlineData("", CapFrameXApiOptions.DefaultPort)]
    [InlineData("not a port", CapFrameXApiOptions.DefaultPort)]
    [InlineData("0", CapFrameXApiOptions.DefaultPort)]
    [InlineData("70000", CapFrameXApiOptions.DefaultPort)]
    public void A_host_can_move_the_service_and_nonsense_falls_back(string value, int expected)
    {
        Assert.Equal(expected, CapFrameXApiOptions.ResolvePort(_ => value));
    }
}
