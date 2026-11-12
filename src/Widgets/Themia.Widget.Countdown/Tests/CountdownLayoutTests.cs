using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Countdown.Tests;

/// <summary>
/// [S-1447] [S-1448] [S-6031] [S-6032] [S-11303] The countdown's face at its Size, progress, ring and flip cards.
/// </summary>
public sealed class CountdownLayoutTests
{

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
    public void S11303_the_readout_is_on_the_sizes_step_sized_on_the_widest_reading_of_its_format()
    {
        Assert.Equal("88 days ago", CountdownLayout.Widest(CountdownFormat.Days, Locale.En));
        Assert.Equal("888 days ago", CountdownLayout.Widest(CountdownFormat.Days, Locale.En, dayFigures: 3));
        Assert.True(HeroFit.Ems(CountdownLayout.Widest(CountdownFormat.DaysHoursMinutesSeconds, Locale.En)) > HeroFit.Ems(CountdownLayout.Widest(CountdownFormat.DaysHoursMinutes, Locale.En)));

        var medium = CountdownLayout.Plan(new CountdownConfig(), Locale.En);
        Assert.Equal(CountdownLook.Text, medium.Look);
        Assert.Equal(FamilyType.Hero68, medium.Step);
        Assert.Equal(FamilyType.Big40, CountdownLayout.Plan(new CountdownConfig { Size = ReadoutSize.Small }, Locale.En).Step);
    }

    [Fact]
    public void S11303_the_ring_and_the_cards_are_one_step_under_and_the_cards_as_wide_as_the_day_count()
    {
        var ring = CountdownLayout.Plan(new CountdownConfig { Style = CountdownStyle.Ring }, Locale.En);
        Assert.Equal(CountdownLook.Ring, ring.Look);
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.True(ring.Ring > 0 && ring.Width == ring.Ring && ring.Height == ring.Ring);

        var flip = CountdownLayout.Plan(new CountdownConfig { Style = CountdownStyle.Flip, Format = CountdownFormat.DaysHoursMinutesSeconds }, Locale.En);
        Assert.Equal(CountdownLook.Flip, flip.Look);
        Assert.Equal(FamilyType.Big40, flip.Step);
        Assert.Equal(FlipRules.RowWidth(FamilyType.Big40, 4, "88"), flip.Width);
    }

    [Fact]
    public void S11803_the_card_is_sized_for_its_own_targets_day_count_and_changes_with_the_target_not_the_clock()
    {
        var now = new DateTimeOffset(2026, 11, 12, 10, 42, 0, TimeSpan.Zero);
        CountdownConfig At(int days)
        {
            var config = new CountdownConfig();
            config.Retarget(now.AddDays(days), now);
            return config;
        }

        Assert.Equal(2, CountdownLayout.DayFigures(new CountdownConfig()));
        Assert.Equal(2, CountdownLayout.DayFigures(At(0)));
        Assert.Equal(2, CountdownLayout.DayFigures(At(40)));
        Assert.Equal(3, CountdownLayout.DayFigures(At(400)));
        Assert.Equal(4, CountdownLayout.DayFigures(At(4000)));

        // Days until a date need the figures that date needs: a near target is narrower than a far one,
        // and both narrower than sizing every countdown for 999 days.
        var near = CountdownLayout.Sizing(At(40), Locale.En).Natural.Width;
        var far = CountdownLayout.Sizing(At(400), Locale.En).Natural.Width;
        Assert.True(near <= far);
        Assert.True(HeroFit.Ems(CountdownLayout.Widest(CountdownFormat.Days, Locale.En, 2)) < HeroFit.Ems(CountdownLayout.Widest(CountdownFormat.Days, Locale.En, 3)));

        // A moment has no "ago": the Auto card is sized on a reading it can draw, not "in a moment ago" (in English its widest is "in a moment" itself).
        Assert.Equal("in a moment", CountdownLayout.Widest(CountdownFormat.Auto, Locale.En));

        // Within a day the reading is hours and minutes: the "88h 88m ago" a two-figure card holds.
        Assert.True(HeroFit.Ems(CountdownLayout.Widest(CountdownFormat.Auto, Locale.En)) >= HeroFit.Ems(CountdownLayout.Shape("23h 59m ago")));
    }

    [Fact]
    public void S11303_the_card_is_its_face_under_its_header_with_the_bar_where_it_is_on_but_never_on_a_ring()
    {
        var plain = CountdownLayout.Sizing(new CountdownConfig(), Locale.En);
        var barred = CountdownLayout.Sizing(new CountdownConfig { ShowProgress = true }, Locale.En);
        var ring = new CountdownConfig { Style = CountdownStyle.Ring, ShowProgress = true };

        Assert.True(plain.FixedHeight && !plain.FixedWidth);
        Assert.Equal(36 + 92 + 20 + 12 + 2, plain.Natural.Height);
        Assert.Equal(plain.Natural.Height + 12, barred.Natural.Height);
        Assert.False(ring.DrawsProgress);
        Assert.Equal(CountdownLayout.Sizing(ring, Locale.En), CountdownLayout.Sizing(new CountdownConfig { Style = CountdownStyle.Ring }, Locale.En));
        Assert.False(new CountdownConfig().ShowProgress);
    }
}
