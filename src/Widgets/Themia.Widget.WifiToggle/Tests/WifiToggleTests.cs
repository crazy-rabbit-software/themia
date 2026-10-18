using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.WifiToggle.Tests;

/// <summary>[S-8200] The Wifi toggle stands for its one quick setting, from its own config.</summary>
public sealed class WifiToggleTests
{
    [Fact]
    public void S8200_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("wifiToggle");
        var config = Assert.IsType<WifiToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Wifi, config.Toggle);
        Assert.Equal(QuickToggle.Wifi, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Wifi));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<WifiToggleConfig>(config.Clone()).Style);
    }
}
