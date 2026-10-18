using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Countdown.Tests;

/// <summary>
/// [S-1447] [S-1448] [S-6031] [S-6032] The countdown's ladder, progress, ring and flip cards. Moved
/// with the widget from Core's TrackersLayoutTests and TimeStylesTests, unchanged in what they
/// assert (the tiers' slots are the card less a 44px header).
/// </summary>
public sealed class CountdownLayoutTests
{
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S6031_the_ring_is_the_share_still_to_go_and_unknown_without_a_creation_time()
    {
        var created = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var trip = new CountdownConfig { Label = "Trip" };
        trip.Retarget(created.AddDays(10), created);

        Assert.Equal(1, CountdownLayout.Left(trip, created));
        Assert.Equal(0.75, CountdownLayout.Left(trip, created.AddDays(2.5))!.Value, 6);
        Assert.Equal(0, CountdownLayout.Left(trip, created.AddDays(12)));
        Assert.Null(CountdownLayout.Left(new CountdownConfig { Target = created.AddDays(10) }, created));
        Assert.Null(CountdownLayout.Left(new CountdownConfig(), created));
    }

    [Fact]
    public void S6032_flip_cards_are_days_while_there_are_any_then_two_figure_hours_and_minutes()
    {
        var now = new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

        var far = CountdownLayout.Cards(now.AddDays(12).AddHours(3).AddMinutes(7), now, seconds: false);
        Assert.Equal(["12", "03", "07"], far.Select(c => c.Digits));
        Assert.Equal([CountdownUnit.Days, CountdownUnit.Hours, CountdownUnit.Minutes], far.Select(c => c.Unit));

        var near = CountdownLayout.Cards(now.AddHours(5).AddMinutes(4).AddSeconds(9), now, seconds: true);
        Assert.Equal(["05", "04", "09"], near.Select(c => c.Digits));
        Assert.Equal(CountdownUnit.Seconds, near[^1].Unit);

        // Gone by: it counts up, as the text style's "… ago" does.
        var past = CountdownLayout.Cards(now.AddMinutes(-90), now, seconds: false);
        Assert.Equal(["01", "30"], past.Select(c => c.Digits));

        Assert.Equal("365", CountdownLayout.Cards(now.AddDays(365), now, false)[0].Digits);
    }

    [Fact]
    public void S1447_the_progress_bar_is_a_rung_of_the_large_tiles_alone()
    {
        Assert.False(CountdownLayout.ShowsProgress(XS));
        Assert.False(CountdownLayout.ShowsProgress(S));
        Assert.False(CountdownLayout.ShowsProgress(M));
        Assert.False(CountdownLayout.ShowsProgress(Wide));
        Assert.False(CountdownLayout.ShowsProgress(Tall));
        Assert.True(CountdownLayout.ShowsProgress(L));
        Assert.True(CountdownLayout.ShowsProgress(XL));
    }

    [Fact]
    public void S1448_progress_runs_from_when_the_target_was_set_to_the_target()
    {
        var config = new CountdownConfig();
        config.Retarget(Now.AddHours(10), Now.AddHours(-30));
        Assert.Equal(0.75, CountdownLayout.Progress(config, Now)!.Value, 6);
        Assert.Equal(1, CountdownLayout.Progress(config, Now.AddDays(5)));
        Assert.Equal(0, CountdownLayout.Progress(config, Now.AddDays(-5)));

        // Unknown when it was set, set after the target, or no target at all: no bar.
        Assert.Null(CountdownLayout.Progress(new CountdownConfig { Target = Now }, Now));
        Assert.Null(CountdownLayout.Progress(new CountdownConfig { Target = Now, Created = Now.AddHours(1) }, Now));
        Assert.Null(CountdownLayout.Progress(new CountdownConfig(), Now));
    }

    [Fact]
    public void S1448_clone_keeps_the_creation_time()
    {
        var config = new CountdownConfig();
        config.Retarget(Now.AddHours(10), Now);
        Assert.Equal(Now, ((CountdownConfig)config.Clone()).Created);
    }

    [Fact]
    public void S1447_hero_width_is_the_formats_widest()
    {
        Assert.True(CountdownLayout.HeroCharacters(CountdownFormat.DaysHoursMinutesSeconds, false)
            > CountdownLayout.HeroCharacters(CountdownFormat.Days, false));
        Assert.Equal(CountdownLayout.HeroCharacters(CountdownFormat.Auto, false) + 4, CountdownLayout.HeroCharacters(CountdownFormat.Auto, true));
    }
}
