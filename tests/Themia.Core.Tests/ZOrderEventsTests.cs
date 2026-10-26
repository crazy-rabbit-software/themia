using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-34a] Which window-manager events wake the z-order controller, and [S-34d] the bound on its
/// repairs. There is no timer behind these: an event the filter drops is a change nothing else
/// will ever report, and a repair loop the bound does not end runs forever.
/// </summary>
public sealed class ZOrderEventsTests
{
    /// <summary>The foreground changing is the user's own doing: it checks and gives the repairs a fresh allowance.</summary>
    [Theory]
    [InlineData(EventSource.TopLevelWindow)]
    [InlineData(EventSource.Other)]
    public void S34a_a_foreground_change_checks_afresh(EventSource source)
        => Assert.Equal(ZTrigger.CheckAfresh, ZOrderEvents.Classify(ShellEvent.Foreground, true, source));

    /// <summary>
    /// A top-level window's z-order change is announced by the desktop window. This is the event
    /// that hears Show Desktop raising the desktop, a late <c>WorkerW</c> raise ([S-34b]) and an
    /// application sent to the bottom ([S-34c]).
    /// </summary>
    [Fact]
    public void S34a_a_reorder_of_the_top_level_windows_checks()
        => Assert.Equal(ZTrigger.Check, ZOrderEvents.Classify(ShellEvent.Reorder, false, EventSource.DesktopWindow));

    /// <summary>A list view or toolbar reordering its items says nothing about windows.</summary>
    [Theory]
    [InlineData(EventSource.TopLevelWindow)]
    [InlineData(EventSource.Other)]
    public void S34a_a_reorder_inside_a_window_is_ignored(EventSource source)
        => Assert.Equal(ZTrigger.Ignore, ZOrderEvents.Classify(ShellEvent.Reorder, false, source));

    /// <summary>[S-33a] [S-33e] A window minimised, restored, shown, cloaked or uncloaked changes what is on screen.</summary>
    [Theory]
    [InlineData(ShellEvent.Minimize)]
    [InlineData(ShellEvent.Show)]
    [InlineData(ShellEvent.Cloak)]
    public void S34a_a_top_level_window_going_or_coming_checks(ShellEvent kind)
        => Assert.Equal(ZTrigger.Check, ZOrderEvents.Classify(kind, true, EventSource.TopLevelWindow));

    /// <summary>A caret or cursor shown, or a child window cloaked, is not a window coming back.</summary>
    [Theory]
    [InlineData(ShellEvent.Show, false, EventSource.TopLevelWindow)]
    [InlineData(ShellEvent.Show, true, EventSource.Other)]
    [InlineData(ShellEvent.Cloak, false, EventSource.TopLevelWindow)]
    [InlineData(ShellEvent.Cloak, true, EventSource.Other)]
    [InlineData(ShellEvent.Minimize, true, EventSource.Other)]
    public void S34a_parts_and_child_windows_are_ignored(ShellEvent kind, bool wholeWindow, EventSource source)
        => Assert.Equal(ZTrigger.Ignore, ZOrderEvents.Classify(kind, wholeWindow, source));

    [Fact]
    public void S34d_the_allowance_grants_its_repairs_then_refuses_once_out_loud()
    {
        var repairs = new ZOrderRepairs();

        for (var i = 0; i < ZOrderRepairs.Allowance; i++)
        {
            Assert.Equal(RepairGrant.Granted, repairs.TrySpend());
        }

        Assert.Equal(RepairGrant.Refused, repairs.TrySpend());
        Assert.Equal(RepairGrant.RefusedAgain, repairs.TrySpend());
        Assert.Equal(0, repairs.Left);
    }

    [Fact]
    public void S34d_a_user_action_refills_the_allowance_and_the_warning()
    {
        var repairs = new ZOrderRepairs();
        while (repairs.TrySpend() == RepairGrant.Granted)
        {
        }

        repairs.Refill();

        Assert.Equal(ZOrderRepairs.Allowance, repairs.Left);
        Assert.Equal(RepairGrant.Granted, repairs.TrySpend());
    }

    /// <summary>
    /// [S-34d] Another program that answers every one of our writes with its own: each write is heard
    /// back as an event and checked again. Without the bound this never ends; with it, it ends after
    /// the allowance and stays quiet until the user does something.
    /// </summary>
    [Fact]
    public void S34d_a_fight_with_another_program_ends_without_a_clock()
    {
        var repairs = new ZOrderRepairs();
        var writes = 0;
        var pending = 1;

        for (var events = 0; pending > 0 && events < 10_000; events++)
        {
            pending--;
            if (ZOrderPolicy.AnchorHolds(false, [StackWindow.Other]))
            {
                continue;
            }

            if (repairs.TrySpend() == RepairGrant.Granted)
            {
                writes++;

                // The other program puts its window back under ours: another reorder event.
                pending++;
            }
        }

        Assert.Equal(0, pending);
        Assert.Equal(ZOrderRepairs.Allowance, writes);
    }
}
