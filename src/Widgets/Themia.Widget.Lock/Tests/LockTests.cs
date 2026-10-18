using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Lock.Tests;

/// <summary>[S-7800] The Lock power button stands for its one action, from its own config.</summary>
public sealed class LockTests
{
    [Fact]
    public void S7800_its_kind_stands_for_its_action_and_runs_at_once()
    {
        var kind = new WidgetId("lock");
        var config = Assert.IsType<LockConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(PowerAction.Lock, config.Action);
        Assert.Equal(PowerAction.Lock, PowerActionRules.ForKind(kind));
        Assert.Equal(kind, PowerActionRules.KindOf(PowerAction.Lock));
        Assert.False(PowerActionRules.NeedsArming(PowerAction.Lock));
    }
}
