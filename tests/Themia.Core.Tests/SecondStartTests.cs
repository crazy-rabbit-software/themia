using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-66] Themia started again while it runs: what the copy that stands down says, and what the running copy does.</summary>
public sealed class SecondStartTests
{
    [Fact]
    public void S66_a_start_by_hand_opens_the_settings_menu()
        => Assert.Equal(SecondStartAction.OpenSettings, SecondStart.Decide(SecondStart.Encode(LaunchSource.Manual)));

    [Fact]
    public void S66_a_start_at_sign_in_does_nothing()
        => Assert.Equal(SecondStartAction.Ignore, SecondStart.Decide(SecondStart.Encode(LaunchSource.Autostart)));

    [Fact]
    public void S66_the_two_starts_send_different_values_and_neither_is_zero()
    {
        // Zero is what any program posting the bare registered message sends: it must not open anything.
        Assert.NotEqual(SecondStart.Encode(LaunchSource.Manual), SecondStart.Encode(LaunchSource.Autostart));
        Assert.NotEqual(0, SecondStart.Encode(LaunchSource.Manual));
        Assert.NotEqual(0, SecondStart.Encode(LaunchSource.Autostart));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void S66_a_value_no_copy_sends_is_unknown(int wParam)
        => Assert.Equal(SecondStartAction.Unknown, SecondStart.Decide(wParam));
}
