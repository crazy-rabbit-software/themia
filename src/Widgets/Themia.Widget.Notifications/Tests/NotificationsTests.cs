using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Notifications.Tests;

/// <summary>[S-4404] The notification widget's ladder. Moved with the widget from Core's WindowsPanelsTests, unchanged in what it asserts (the ladder is NotificationsLayout's now).</summary>
public sealed class NotificationsTests
{
    [Fact]
    public void S4404_the_ladder_face_then_flat_then_grouped_with_more_body()
    {
        var face = NotificationsLayout.Plan(new WidgetArea(150, 150));
        Assert.True(face.Face);

        var low = NotificationsLayout.Plan(new WidgetArea(320, 50));
        Assert.True(low.Face);

        var strip = NotificationsLayout.Plan(new WidgetArea(440, 110));
        Assert.False(strip.Face);
        Assert.Equal(0, strip.BodyLines);

        var small = NotificationsLayout.Plan(new WidgetArea(320, 200));
        Assert.False(small.Face);
        Assert.False(small.Grouped);
        Assert.Equal(1, small.BodyLines);

        var medium = NotificationsLayout.Plan(new WidgetArea(320, 320));
        Assert.True(medium.Grouped);
        Assert.Equal(2, medium.BodyLines);

        var large = NotificationsLayout.Plan(new WidgetArea(480, 560));
        Assert.True(large.Grouped);
        Assert.Equal(3, large.BodyLines);
    }
}
