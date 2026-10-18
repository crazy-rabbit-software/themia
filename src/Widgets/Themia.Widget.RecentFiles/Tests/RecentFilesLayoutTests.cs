using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.RecentFiles.Tests;

/// <summary>[S-4842] The Recent files ladder. Moved with the widget from Core's DeskInfoWidgetsTests, unchanged.</summary>
public sealed class RecentFilesLayoutTests
{
    [Fact]
    public void S4842_rows_grow_a_folder_line_then_a_time_column()
    {
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(120, 76)).Compact);
        Assert.False(RecentFilesLayout.Plan(new WidgetArea(120, 76)).TwoLine);
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(320, 196)).TwoLine);
        Assert.False(RecentFilesLayout.Plan(new WidgetArea(320, 196)).TimeColumn);
        Assert.True(RecentFilesLayout.Plan(new WidgetArea(560, 376)).TimeColumn);
    }
}
