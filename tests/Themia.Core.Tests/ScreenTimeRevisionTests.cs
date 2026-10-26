using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3324] The sampling tick tells the widgets only when it changed something they read: idle,
/// locked or with Themia in front the history stands still, and so do the charts and the ring.
/// </summary>
public sealed class ScreenTimeRevisionTests
{
    private const long Day1Midnight = 1_000_000;
    private const string Day1 = "2026-07-01";
    private const string Day2 = "2026-07-02";

    private static LocalStamp D1(int seconds) => new(Day1Midnight + seconds, Day1, seconds);

    /// <summary>One tick as the service runs it: did it tell the widgets?</summary>
    private static bool Tick(ScreenTimeTracker tracker, LocalStamp stamp, int idle, ref long told, ref string? day)
    {
        tracker.OnTick(stamp, idle);

        if (!ScreenTimeTracker.Tells(told, day, tracker.Revision, stamp.Date))
        {
            return false;
        }

        told = tracker.Revision;
        day = stamp.Date;
        return true;
    }

    [Fact]
    public void S3324_a_tick_that_credits_use_tells_the_widgets()
    {
        var t = new ScreenTimeTracker();
        long told = -1;
        string? day = null;
        t.OnForeground(D1(0), "code");

        Assert.True(Tick(t, D1(30), 0, ref told, ref day));
        Assert.True(Tick(t, D1(60), 0, ref told, ref day));
    }

    [Fact]
    public void S3324_an_idle_user_is_not_redrawn_every_tick()
    {
        // Was a Changed every 30 s for as long as Themia ran: idle, locked or not.
        var t = new ScreenTimeTracker();
        long told = -1;
        string? day = null;
        t.OnForeground(D1(0), "code");
        Tick(t, D1(30), 0, ref told, ref day);

        // Idle past the threshold: this tick credits and pauses (a change), the next ones nothing.
        Assert.True(Tick(t, D1(60), ScreenTimeTracker.IdleThresholdSeconds, ref told, ref day));
        Assert.False(Tick(t, D1(90), ScreenTimeTracker.IdleThresholdSeconds + 30, ref told, ref day));
        Assert.False(Tick(t, D1(120), ScreenTimeTracker.IdleThresholdSeconds + 60, ref told, ref day));

        // Back: the pause lifts (a change), and use is credited again from the next tick.
        Assert.True(Tick(t, D1(150), 0, ref told, ref day));
        Assert.True(Tick(t, D1(180), 0, ref told, ref day));
    }

    [Fact]
    public void S3324_a_locked_session_is_not_redrawn_every_tick()
    {
        var t = new ScreenTimeTracker();
        long told = -1;
        string? day = null;
        t.OnForeground(D1(0), "code");
        Tick(t, D1(30), 0, ref told, ref day);
        t.OnLock(D1(40));
        told = t.Revision;

        Assert.False(Tick(t, D1(60), 0, ref told, ref day));
        Assert.False(Tick(t, D1(90), 0, ref told, ref day));
    }

    [Fact]
    public void S3324_nothing_in_front_but_themia_is_not_redrawn_every_tick()
    {
        var t = new ScreenTimeTracker();
        long told = -1;
        string? day = null;
        t.OnForeground(D1(0), "code");
        t.OnOwnForeground(D1(10));
        Tick(t, D1(30), 0, ref told, ref day);

        Assert.False(Tick(t, D1(60), 0, ref told, ref day));
    }

    [Fact]
    public void S3324_a_new_day_is_told_even_when_nothing_accrued()
    {
        // A day period's "today" turns over at midnight while the user sleeps.
        Assert.True(ScreenTimeTracker.Tells(5, Day1, 5, Day2));
        Assert.False(ScreenTimeTracker.Tells(5, Day1, 5, Day1));
        Assert.True(ScreenTimeTracker.Tells(5, Day1, 6, Day1));
    }

    [Fact]
    public void S3324_every_change_a_read_sees_moves_the_revision()
    {
        var t = new ScreenTimeTracker();
        var seen = t.Revision;

        void Moved(Action change)
        {
            change();
            Assert.NotEqual(seen, t.Revision);
            seen = t.Revision;
        }

        Moved(() => t.OnForeground(D1(0), "code", "Visual Studio Code"));
        Moved(() => t.OnForeground(D1(20), "chrome"));
        Moved(() => t.NoteExePath("chrome", @"C:\chrome.exe"));
        Moved(() => t.NoteName("chrome", "Google Chrome"));
        Moved(() => t.Preferences = new ScreenTimePreferences { GoalSeconds = 3600 });
        Moved(() => t.OnLock(D1(40)));
        Moved(() => t.OnUnlock(D1(50)));

        var loaded = new ScreenTimeTracker();
        loaded.Add(Day1, "notepad", 60);
        Moved(() => t.Merge(loaded));

        // A flush resets Dirty, never the revision.
        t.Dirty = false;
        Assert.Equal(seen, t.Revision);
    }
}
