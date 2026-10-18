using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Shutdown.Tests;

/// <summary>[S-7400] The Shutdown power button stands for its one action, from its own config.</summary>
public sealed class ShutdownTests
{
    [Fact]
    public void S7400_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("shutdown");
        var config = Assert.IsType<ShutdownConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Shutdown, config.Action);
        Assert.Equal(PowerAction.Shutdown, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Shutdown));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.Shutdown));
    }
}
