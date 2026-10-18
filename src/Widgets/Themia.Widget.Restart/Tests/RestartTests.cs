using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Restart.Tests;

/// <summary>[S-7500] The Restart power button stands for its one action, from its own config.</summary>
public sealed class RestartTests
{
    [Fact]
    public void S7500_its_kind_stands_for_its_action_and_arms_first()
    {
        var kind = new WidgetId("restart");
        var config = Assert.IsType<RestartConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Restart, config.Action);
        Assert.Equal(PowerAction.Restart, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Restart));
        Assert.True(PowerActionRules.NeedsArming(PowerAction.Restart));
    }
}
