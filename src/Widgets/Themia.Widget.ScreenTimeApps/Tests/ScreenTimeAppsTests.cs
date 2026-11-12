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
        Assert.Equal(new SizeDip(300, FamilySpace.ContentTop + (5 * FamilyShape.Row1) + FamilySpace.Inset), sizing.Natural);
        Assert.Equal((220, 520), (sizing.Min.Width, sizing.Max.Width));
        Assert.True(sizing.HeightSteps!.FollowsConfig);
        Assert.Empty(sizing.Problems());

        var twelve = Kind.Sizing(new ScreenTimeAppsConfig { Rows = 12 });
        Assert.Equal(FamilySpace.ContentTop + (12 * FamilyShape.Row1) + FamilySpace.Inset, twelve.Natural.Height);
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
        Assert.Equal(FamilySpace.ContentTop + (5 * FamilyShape.Row2) + FamilySpace.Inset, sizing.Natural.Height);
    }

    [Fact]
    public void S11510_dragging_the_bottom_edge_sets_rows_on_a_copy()
    {
        var config = new ScreenTimeAppsConfig();
        var set = Assert.IsType<ScreenTimeAppsConfig>(Kind.SizeSets!(config, new SizeDip(300, FamilySpace.ContentTop + (8 * FamilyShape.Row1) + FamilySpace.Inset)));
        Assert.Equal(8, set.Rows);
        Assert.Equal(5, config.Rows);
        Assert.Same(config, Kind.SizeSets!(config, Kind.Sizing(config).Natural));
    }

    [Fact]
    public void S11510_rows_and_lines_out_of_range_are_repaired()
    {
        var config = new ScreenTimeAppsConfig { Rows = 40, Lines = 5 };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal((20, 2), (config.Rows, config.Lines));
        Assert.Equal((7, 2), (((ScreenTimeAppsConfig)new ScreenTimeAppsConfig { Rows = 7, Lines = 2 }.Clone()).Rows, ((ScreenTimeAppsConfig)new ScreenTimeAppsConfig { Rows = 7, Lines = 2 }.Clone()).Lines));
    }
}
