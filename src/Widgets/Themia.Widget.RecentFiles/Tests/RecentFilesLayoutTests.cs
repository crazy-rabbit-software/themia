using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RecentFiles.Tests;

/// <summary>[S-4842] The Recent files ladder on the family's rows ([S-6711], [S-6712]).</summary>
public sealed class RecentFilesLayoutTests
{
    [Theory]
    [InlineData(120, 76, 1, false)]   // XS: the name alone
    [InlineData(200, 106, 1, true)]   // S: icon, name, when
    [InlineData(480, 116, 1, true)]   // Wide: one line
    [InlineData(320, 196, 2, true)]   // M: the folder under the name
    [InlineData(200, 356, 2, true)]   // Tall
    [InlineData(560, 376, 2, true)]   // L: never more than the name and its folder
    public void S4842_rows_grow_the_folder_line_with_the_tile(double width, double height, int lines, bool wide)
    {
        var plan = RecentFilesLayout.Plan(new WidgetArea(width, height));

        Assert.Equal(lines, plan.Lines);
        Assert.Equal(wide, plan.Lead);
        Assert.Equal(wide, plan.Time);
        Assert.Equal(FamilyShape.RowHeight(lines), plan.RowHeight);
    }

    [Fact]
    public void S4842_the_list_shows_the_rows_that_fit_whole_above_the_bottom_inset()
    {
        var m = new WidgetArea(320, 196);
        Assert.Equal(3, RecentFilesLayout.Rows(m, RecentFilesLayout.Plan(m)));   // 180 / 48
        var xs = new WidgetArea(120, 76);
        Assert.Equal(1, RecentFilesLayout.Rows(xs, RecentFilesLayout.Plan(xs)));  // 60 / 32
    }
}
