using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.BluetoothToggle.Tests;

/// <summary>[S-8300] The Bluetooth toggle stands for its one quick setting, from its own config.</summary>
public sealed class BluetoothToggleTests
{
    [Fact]
    public void S8300_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("bluetoothToggle");
        var config = Assert.IsType<BluetoothToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Bluetooth, config.Toggle);
        Assert.Equal(QuickToggle.Bluetooth, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Bluetooth));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<BluetoothToggleConfig>(config.Clone()).Style);
    }
}
