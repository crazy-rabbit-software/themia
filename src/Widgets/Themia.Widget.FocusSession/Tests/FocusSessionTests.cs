using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.FocusSession.Tests;

/// <summary>[S-4426] The focus widget's ladder. Moved with the widget from Core's WindowsPanelsTests, unchanged in what it asserts (the ladder is FocusSessionLayout's now).</summary>
public sealed class FocusSessionTests
{
    [Fact]
    public void S4426_the_ladder_time_then_button_then_words_steppers_status_ring()
    {
        var tiny = FocusSessionLayout.Plan(new WidgetArea(140, 100));
        Assert.False(tiny.Button);

        var small = FocusSessionLayout.Plan(new WidgetArea(240, 110));
        Assert.True(small.Button);
        Assert.True(small.Words);
        Assert.False(small.Status);

        var square = FocusSessionLayout.Plan(new WidgetArea(240, 200));
        Assert.True(square.Steppers);
        Assert.True(square.Status);
        Assert.False(square.Ring);

        var big = FocusSessionLayout.Plan(new WidgetArea(420, 320));
        Assert.True(big.Ring);
    }
}
