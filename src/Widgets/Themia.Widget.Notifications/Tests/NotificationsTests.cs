using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Notifications.Tests;

/// <summary>[S-4404] The notification widget's ladder. Moved with the widget from Core's WindowsPanelsTests, the ladder on the family's rows.</summary>
public sealed class NotificationsTests
{
    [Theory]
    [InlineData(120, 76, 1, false, false)]   // XS: the titles alone
    [InlineData(200, 106, 1, true, false)]   // S: icon, title, when
    [InlineData(480, 116, 1, true, false)]   // Wide
    [InlineData(320, 196, 2, true, false)]   // M: the body under the title
    [InlineData(200, 356, 2, true, true)]    // Tall: under app headings
    [InlineData(560, 376, 2, true, true)]    // L
    public void S4404_the_ladder_rows_then_the_body_then_app_headings(double width, double height, int lines, bool wide, bool grouped)
    {
        var plan = NotificationsLayout.Plan(new WidgetArea(width, height));

        Assert.Equal(lines, plan.Lines);
        Assert.Equal(wide, plan.Lead);
        Assert.Equal(wide, plan.Time);
        Assert.Equal(grouped, plan.Grouped);
        Assert.Equal(FamilyShape.RowHeight(lines), plan.RowHeight);
    }
}
