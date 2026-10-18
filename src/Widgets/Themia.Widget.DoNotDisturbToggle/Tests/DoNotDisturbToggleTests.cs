using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.DoNotDisturbToggle.Tests;

/// <summary>[S-8800] The Do not disturb toggle stands for its one quick setting, from its own config.</summary>
public sealed class DoNotDisturbToggleTests
{
    [Fact]
    public void S8800_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("doNotDisturbToggle");
        var config = Assert.IsType<DoNotDisturbToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.DoNotDisturb, config.Toggle);
        Assert.Equal(QuickToggle.DoNotDisturb, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.DoNotDisturb));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<DoNotDisturbToggleConfig>(config.Clone()).Style);
    }
}
