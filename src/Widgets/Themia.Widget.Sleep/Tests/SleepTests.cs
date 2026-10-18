using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Sleep.Tests;

/// <summary>[S-7600] The Sleep power button stands for its one action, from its own config.</summary>
public sealed class SleepTests
{
    [Fact]
    public void S7600_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("sleep");
        var config = Assert.IsType<SleepConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Sleep, config.Action);
        Assert.Equal(PowerAction.Sleep, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Sleep));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.Sleep));
    }
}
