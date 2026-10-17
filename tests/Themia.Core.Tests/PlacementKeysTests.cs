using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1594] Escape cancels a placement wherever the keyboard is.</summary>
public sealed class PlacementKeysTests
{
    private const int Esc = PlacementKeyFilter.Escape;
    private const int A = 0x41;

    [Fact]
    public void S1594_escape_cancels_the_placement()
        => Assert.Equal(PlacementKeyVerdict.Cancel, new PlacementKeyFilter().Decide(Esc, down: true, systemModifier: false));

    [Fact]
    public void S1594_the_cancelling_press_repeats_and_release_go_nowhere_else()
    {
        var filter = new PlacementKeyFilter();

        Assert.Equal(PlacementKeyVerdict.Cancel, filter.Decide(Esc, true, false));
        Assert.True(filter.Holding);
        Assert.Equal(PlacementKeyVerdict.Swallow, filter.Decide(Esc, true, false));
        Assert.Equal(PlacementKeyVerdict.Swallow, filter.Decide(Esc, true, false));
        Assert.Equal(PlacementKeyVerdict.Swallow, filter.Decide(Esc, false, false));
        Assert.False(filter.Holding);

        // The next press is a new one.
        Assert.Equal(PlacementKeyVerdict.Cancel, filter.Decide(Esc, true, false));
    }

    [Fact]
    public void S1594_a_release_of_an_escape_pressed_before_the_watch_passes_on()
    {
        var filter = new PlacementKeyFilter();
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(Esc, false, false));
    }

    [Fact]
    public void S1594_escape_with_ctrl_alt_or_windows_is_the_systems()
    {
        var filter = new PlacementKeyFilter();
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(Esc, true, true));
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(Esc, false, true));
    }

    [Fact]
    public void S1594_other_keys_pass_on()
    {
        var filter = new PlacementKeyFilter();
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(A, true, false));
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(A, false, false));

        // Even while the cancelling Escape is held.
        Assert.Equal(PlacementKeyVerdict.Cancel, filter.Decide(Esc, true, false));
        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(A, true, false));
    }

    [Fact]
    public void S1594_a_new_watch_forgets_a_press_the_last_one_was_holding()
    {
        var filter = new PlacementKeyFilter();
        Assert.Equal(PlacementKeyVerdict.Cancel, filter.Decide(Esc, true, false));

        filter.Reset();

        Assert.Equal(PlacementKeyVerdict.PassOn, filter.Decide(Esc, false, false));
    }
}
