using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.AirplaneModeToggle.Tests;

/// <summary>[S-8400] The Airplane mode toggle stands for its one quick setting, from its own config.</summary>
public sealed class AirplaneModeToggleTests
{
    [Fact]
    public void S8400_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("airplaneModeToggle");
        var config = Assert.IsType<AirplaneModeToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.AirplaneMode, config.Toggle);
        Assert.Equal(QuickToggle.AirplaneMode, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.AirplaneMode));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<AirplaneModeToggleConfig>(config.Clone()).Style);
    }
}
