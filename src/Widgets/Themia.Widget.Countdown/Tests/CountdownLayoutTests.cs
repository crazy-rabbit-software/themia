using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Countdown.Tests;

/// <summary>
/// [S-1447] [S-1448] [S-6031] [S-6032] The countdown's ladder, progress, ring and flip cards. Moved
/// with the widget from Core's TrackersLayoutTests and TimeStylesTests, unchanged in what they
/// assert (the tiers' slots are the card less a 36 header).
/// </summary>
public sealed class CountdownLayoutTests
{
    private static readonly WidgetArea XS = new(120, 84);
    private static readonly WidgetArea S = new(200, 114);
    private static readonly WidgetArea M = new(320, 204);
    private static readonly WidgetArea Wide = new(480, 124);
    private static readonly WidgetArea Tall = new(200, 364);
    private static readonly WidgetArea L = new(560, 384);
    private static readonly WidgetArea XL = new(800, 524);

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
    public void S1447_the_readout_is_sized_on_the_readings_shape_so_a_tick_does_not_move_it()
    {
        Assert.Equal("8d 8h", CountdownLayout.Shape("3d 5h"));
        Assert.Equal(CountdownLayout.Shape("3d 5h"), CountdownLayout.Shape("2d 9h"));
        Assert.NotEqual(CountdownLayout.Shape("3d 5h"), CountdownLayout.Shape("23h 59m"));
    }

    [Fact]
    public void S1447_the_text_climbs_the_hero_steps_and_the_bar_stands_under_it_on_l_and_xl_only()
    {
        var m = CountdownLayout.Plan(CountdownStyle.Text, M, "3d 5h", [], progress: true);
        Assert.Equal(CountdownLook.Text, m.Look);
        Assert.Equal(FamilyType.Hero96, m.Step);
        Assert.True(m.Under);
        Assert.False(m.Bar);

        var l = CountdownLayout.Plan(CountdownStyle.Text, L, "3d 5h", [], progress: true);
        Assert.True(l.Bar);
        Assert.False(CountdownLayout.Plan(CountdownStyle.Text, L, "3d 5h", [], progress: false).Bar);

        var xs = CountdownLayout.Plan(CountdownStyle.Text, XS, "3d 5h", [], progress: true);
        Assert.Equal(FamilyType.Big28, xs.Step);
    }

    [Fact]
    public void S6031_the_ring_holds_the_text_where_its_hole_can_and_never_has_a_bar()
    {
        var m = CountdownLayout.Plan(CountdownStyle.Ring, M, "3d 5h", [], progress: true);
        Assert.Equal(CountdownLook.Ring, m.Look);
        Assert.Equal(192, m.Ring);
        Assert.Equal(FamilyType.Big40, m.Step);

        var xl = CountdownLayout.Plan(CountdownStyle.Ring, XL, "3d 5h", [], progress: true);
        Assert.False(xl.Bar);

        // XS: a 60 ring's hole cannot hold "8d 8h" on 28; the text instead, still without a bar.
        var xs = CountdownLayout.Plan(CountdownStyle.Ring, XS, "3d 5h", [], progress: true);
        Assert.Equal(CountdownLook.Text, xs.Look);
        Assert.False(xs.Bar);
    }

    [Fact]
    public void S6032_the_cards_take_the_largest_step_their_row_fits_else_the_text()
    {
        CountdownCard[] cards = [new("3", CountdownUnit.Days), new("05", CountdownUnit.Hours), new("19", CountdownUnit.Minutes)];

        var xl = CountdownLayout.Plan(CountdownStyle.Flip, XL, "3d 5h", cards, progress: true);
        Assert.Equal(CountdownLook.Flip, xl.Look);
        Assert.Equal(FamilyType.Hero192, xl.Step);
        Assert.True(xl.Under);
        Assert.False(xl.Bar);

        // XS: three cards on 28 are 3 × (33 + 24) + 16 = 187 wide in an 88 box: the text.
        Assert.Equal(CountdownLook.Text, CountdownLayout.Plan(CountdownStyle.Flip, XS, "3d 5h", cards, progress: true).Look);
    }
}
