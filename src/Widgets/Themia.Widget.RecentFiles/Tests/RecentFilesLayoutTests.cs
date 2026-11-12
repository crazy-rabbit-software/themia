using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RecentFiles.Tests;

/// <summary>[S-4842] The Recent files ladder on the family's rows ([S-6711], [S-6712]).</summary>
public sealed class RecentFilesLayoutTests
{
    [Theory]
    [InlineData(120, 84, 1, false)]   // XS: the name alone
    [InlineData(200, 114, 1, true)]   // S: icon, name, when
    [InlineData(480, 124, 1, true)]   // Wide: one line
    [InlineData(320, 204, 2, true)]   // M: the folder under the name
    [InlineData(200, 364, 2, true)]   // Tall
    [InlineData(560, 384, 2, true)]   // L: never more than the name and its folder
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
        var m = new WidgetArea(320, 204);
        Assert.Equal(4, RecentFilesLayout.Rows(m, RecentFilesLayout.Plan(m)));   // 192 / 40
        var xs = new WidgetArea(120, 84);
        Assert.Equal(2, RecentFilesLayout.Rows(xs, RecentFilesLayout.Plan(xs)));  // 72 / 28
    }
}
