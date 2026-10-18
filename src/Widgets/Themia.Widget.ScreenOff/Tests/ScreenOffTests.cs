using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.ScreenOff.Tests;

/// <summary>[S-8000] The ScreenOff power button stands for its one action, from its own config.</summary>
public sealed class ScreenOffTests
{
    [Fact]
    public void S8000_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("screenOff");
        var config = Assert.IsType<ScreenOffConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.ScreenOff, config.Action);
        Assert.Equal(PowerAction.ScreenOff, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.ScreenOff));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.ScreenOff));
    }
}
