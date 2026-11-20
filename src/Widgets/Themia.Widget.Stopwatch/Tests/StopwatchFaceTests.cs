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
        // [S-14500] The inset and the face (92 + 20 + 8 + 28 + 2) as one band of whole cells: 162 grown to 170.
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, StopwatchFace.FaceHeight(new StopwatchConfig(), Locale.En) + 24);
        Assert.Equal(170, medium.Natural.Height);
        Assert.Equal(medium.Min.Height, medium.Natural.Height);
        Assert.False(medium.FixedWidth);
        Assert.Null(typeof(StopwatchConfig).GetProperty("Size"));
    }

    [Fact]
    public void S11302_S14500_a_new_one_is_its_face_and_every_cell_taller_lists_the_laps_it_holds()
    {
        var sizing = Sizing(new StopwatchConfig());

        // [S-14500] A new one is its face's band (whole cells) and lists no laps; taller, a lap row each at its optimal (30),
        // up to ten, stretched to the card's foot.
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(Themia.Core.Widgets.ItemGrid.Default().Whole(12 + StopwatchFace.FaceHeight(new StopwatchConfig(), Themia.Core.Localization.Strings.Current)), sizing.Natural.Height);
        Assert.Equal(sizing.Natural.Height + 37, sizing.FitHeight(sizing.Natural.Height + 37));
    }

    [Fact]
    public void S11302_S14502_a_stored_lap_count_is_dropped_loudly()
    {
        // How many laps it lists is the card's: no field holds it, so a file that still stores one loads without it.
        Assert.DoesNotContain(typeof(StopwatchConfig).GetProperties(), p => p.Name == "LapRows");
    }

    [Fact]
    public void S11302_S14410_it_has_no_menu_lines_its_laps_and_its_size_are_the_cards()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows
        // the card's size": the laps a stopwatch lists are what its height holds; dragging the bottom edge sets them.
        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new StopwatchModule()).MenuLines(menu, new StopwatchConfig());

        Assert.Empty(menu.Lines);
        Assert.Empty(menu.StyleLines);
    }

    [Fact]
    public void S11302_S14500_the_laps_take_the_room_under_the_face_to_the_cards_foot()
    {
        // A view 208 tall (a card of 220) under a face band of 158 has 50 under it: one lap row, stretched to fill it. The
        // render of 2026-11-20 counted the card's inset twice (62), asked for two rows, showed one of 30 and left 20 empty
        // at the foot.
        var grid = ItemGrid.Default();
        Assert.Equal(1, StopwatchFace.LapRowsIn(208, 158, grid));
        Assert.Equal(2, StopwatchFace.LapRowsIn(218, 158, grid));
        Assert.Equal(5, StopwatchFace.LapRowsIn(328, 158, grid));
        Assert.Equal(0, StopwatchFace.LapRowsIn(180, 158, grid));
        Assert.Equal(0, StopwatchFace.LapRowsIn(double.NaN, 158, grid));
        Assert.Equal(StopwatchFace.MostLapRows, StopwatchFace.LapRowsIn(2000, 158, grid));
    }
}
