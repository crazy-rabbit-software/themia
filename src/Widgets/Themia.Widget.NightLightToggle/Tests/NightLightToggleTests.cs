using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.NightLightToggle.Tests;

/// <summary>[S-8600] The Night light toggle stands for its one quick setting, from its own config.</summary>
public sealed class NightLightToggleTests
{
    [Fact]
    public void S8600_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("nightLightToggle");
        var config = Assert.IsType<NightLightToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.NightLight, config.Toggle);
        Assert.Equal(QuickToggle.NightLight, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.NightLight));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<NightLightToggleConfig>(config.Clone()).Style);
    }
}
