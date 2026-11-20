using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeApps.Tests;

/// <summary>[S-11510] The app list states its rows: its height follows them, its plan is its config's at every size.</summary>
public sealed class ScreenTimeAppsTests
{
    private static WidgetDescriptor Kind => new ScreenTimeAppsModule().Descriptor;

    [Fact]
    public void S11510_five_one_line_rows_300_wide_the_height_following_the_rows()
    {
        var sizing = Kind.Sizing(new ScreenTimeAppsConfig());
        Assert.Equal(new SizeDip(300, 40 + (5 * 30)), sizing.Natural);
        Assert.Equal((220d, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Width, sizing.Max.Width)); // relaxed, [S-12411]: no practical Max
        Assert.Equal(40 + 30, sizing.Min.Height);
        Assert.Empty(sizing.Problems());

        var twelve = Kind.Sizing(new ScreenTimeAppsConfig { Rows = 12 });
        Assert.Equal(40 + (12 * 30), twelve.Natural.Height);
    }

    [Fact]
    public void S11510_a_second_line_only_where_a_row_has_figures_for_it()
    {
        // A day with no share shown: one line whatever Lines says.
        Assert.Equal(1, ScreenTimeAppsLayout.Apps(new ScreenTimeAppsConfig { Lines = 2 }).Lines);

        // A window's average, or the share: two lines, the icon at 32.
        var week = ScreenTimeAppsLayout.Apps(new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Week, Lines = 2 });
        Assert.Equal((2, FamilyShape.IconLarge), (week.Lines, week.Lead));
        Assert.Equal(2, ScreenTimeAppsLayout.Apps(new ScreenTimeAppsConfig { Lines = 2, ShowPercent = true }).Lines);
        Assert.Equal(FamilyShape.Icon, ScreenTimeAppsLayout.Apps(new ScreenTimeAppsConfig()).Lead);

        var sizing = Kind.Sizing(new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Week, Lines = 2 });
        Assert.Equal(5 * 40, sizing.Natural.Height);
    }

    [Fact]
    public void S11510_S14500_a_height_takes_every_cell_from_one_row_up()
    {
        var sizing = Kind.Sizing(new ScreenTimeAppsConfig());
        foreach (var height in new[] { 70.0, 80, 110, 230, 237 })
        {
            Assert.Equal(height, sizing.FitHeight(height));
        }
    }

    [Fact]
    public void S11510_rows_and_lines_out_of_range_are_repaired()
    {
        var config = new ScreenTimeAppsConfig { Rows = 40, Lines = 5 };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal((20, 2), (config.Rows, config.Lines));
        Assert.Equal((7, 2), (((ScreenTimeAppsConfig)new ScreenTimeAppsConfig { Rows = 7, Lines = 2 }.Clone()).Rows, ((ScreenTimeAppsConfig)new ScreenTimeAppsConfig { Rows = 7, Lines = 2 }.Clone()).Lines));
    }

    [Fact]
    public void S11510_S14500_the_list_shows_the_rows_its_room_holds_at_their_optimal()
    {
        // The render of 2026-11-20 stood the config's five rows in any card: under the day line of a card 150 tall the
        // fifth was cut to 20, at 180 twenty were left under the last. The rows its room holds stand, stretched to fill it.
        var grid = ItemGrid.Default();
        Assert.Equal(3, ScreenTimeAppsLayout.RowsIn(110, 1, grid));
        Assert.Equal(4, ScreenTimeAppsLayout.RowsIn(140, 1, grid));
        Assert.Equal(2, ScreenTimeAppsLayout.RowsIn(110, 2, grid));
        Assert.Equal(1, ScreenTimeAppsLayout.RowsIn(20, 1, grid));
        Assert.Equal(0, ScreenTimeAppsLayout.RowsIn(0, 1, grid));

        var config = new ScreenTimeAppsConfig { Rows = 5 };
        Assert.Equal(5, ScreenTimeAppsLayout.Apps(config).Rows);
        Assert.Equal(3, ScreenTimeAppsLayout.Apps(config, held: 3).Rows);
    }
}
