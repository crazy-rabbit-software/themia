using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RecentFiles.Tests;

/// <summary>[S-11200] Recent files states its rows as the kit's lists do ([S-2440]): its height follows them.</summary>
public sealed class RecentFilesSizingTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("recentFiles"));

    [Fact]
    public void S11200_a_new_one_shows_six_files_of_two_lines_and_its_height_follows_the_rows()
    {
        var config = new RecentFilesConfig();
        Assert.Equal((6, 2), (config.Rows, config.Lines));

        var sizing = Kind.Sizing(config);
        Assert.Equal(new SizeDip(300, 6 * 40), sizing.Natural);
        Assert.Equal((220.0, Themia.Sdk.SizeStretch.Ceiling), (sizing.Min.Width, sizing.Max.Width)); // relaxed, [S-12411]: no practical Max

        Assert.Equal(3 * 30, Kind.Sizing(new RecentFilesConfig { Rows = 3, Lines = 1 }).Natural.Height);
    }

    [Fact]
    public void S11200_S14500_its_rows_are_a_new_cards_height_every_cell_from_one_row_up()
    {
        var sizing = Kind.Sizing(new RecentFilesConfig { Rows = 4 });

        Assert.Equal(4 * 40, sizing.Natural.Height);
        Assert.Equal(40, sizing.Min.Height);
        Assert.Equal(6, new RecentFilesConfig().Rows);
    }

    [Fact]
    public void S11200_its_menu_is_rows_and_lines_per_row()
    {
        var lines = new Themia.Sdk.WinUI.WidgetMenuLines(() => { });
        new RecentFilesModule().MenuLines(lines, new RecentFilesConfig());

        Assert.Equal([Str.Kit_Rows.Text(), Str.Kit_Lines.Text()], lines.Lines.Select(l => l.Text));
    }

    [Fact]
    public void S11200_rows_and_lines_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new RecentFilesConfig { Rows = 40, Lines = 0 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((KitSizing.MaxRows, 1), (config.Rows, config.Lines));

        var copy = (RecentFilesConfig)config.Clone();
        Assert.Equal((config.Rows, config.Lines), (copy.Rows, copy.Lines));
        Assert.Empty(new RecentFilesConfig().Repair());
    }
}
