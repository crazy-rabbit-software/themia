using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Stopwatch.Tests;

/// <summary>[S-4603] [S-11302] The stopwatch on the timers' sized face: the widest reading it draws, its Size, its lap rows.</summary>
public sealed class StopwatchFaceTests
{
    private static WidgetSizing Sizing(StopwatchConfig config) => StopwatchFace.Sizing(config, Locale.En);

    [Fact]
    public void S4603_the_time_is_sized_for_the_widest_reading_it_draws()
    {
        Assert.Equal("88:88.8", StopwatchFace.Widest(hours: false));
        Assert.Equal("88:88:88.8", StopwatchFace.Widest(hours: true));

        // Under an hour with its tenth, from an hour its hours without it: the face holds the wider.
        Assert.True(StopwatchFace.Tenths(hours: false));
        Assert.False(StopwatchFace.Tenths(hours: true));
        Assert.Equal("88:88:88", StopwatchFace.Worst().Value);
    }

    [Fact]
    public void S11302_S2449_retired_the_natural_card_is_the_one_readout_face_without_laps_a_small_width_range()
    {
        var medium = Sizing(new StopwatchConfig());

        Assert.Equal(FamilyType.Hero68, StopwatchFace.Sized(new StopwatchConfig()).Step);
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, medium.Natural.Height);
        Assert.False(medium.FixedWidth);
        Assert.Null(typeof(StopwatchConfig).GetProperty("Size"));
    }

    [Fact]
    public void S11302_the_height_follows_the_lap_rows_and_dragging_it_sets_them()
    {
        var none = Sizing(new StopwatchConfig());
        var four = Sizing(new StopwatchConfig { LapRows = 4 });

        Assert.Equal(none.Natural.Height + (4 * 28), four.Natural.Height);
        Assert.Equal(none.Natural.Height + (10 * 28), four.Max.Height);
        Assert.Equal(4, four.HeightSteps!.Count);

        var config = new StopwatchConfig { LapRows = 4 };
        var three = Assert.IsType<StopwatchConfig>(StopwatchFace.SizeSets(config, four.Natural with { Height = none.Natural.Height + (3 * 28) }));
        Assert.Equal(3, three.LapRows);
        Assert.Equal(4, config.LapRows);
        Assert.Same(config, StopwatchFace.SizeSets(config, four.Natural));
    }

    [Fact]
    public void S11302_lap_rows_are_repaired_into_range_and_copied()
    {
        var config = new StopwatchConfig { LapRows = 40 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(10, config.LapRows);

        var copy = (StopwatchConfig)new StopwatchConfig { LapRows = 3 }.Clone();
        Assert.Equal(3, copy.LapRows);
    }

    [Fact]
    public void S11302_its_menu_is_the_laps_and_no_size()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new StopwatchModule().MenuLines(menu, new StopwatchConfig());

        Assert.Equal([StopwatchStr.Stopwatch_Laps.Text()], menu.Lines.Select(l => l.Text));
    }
}
