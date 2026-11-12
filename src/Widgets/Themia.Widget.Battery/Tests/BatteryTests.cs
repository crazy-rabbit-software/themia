using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Battery.Tests;

/// <summary>[S-6272] The battery reading: its drawings and their sizes.</summary>
public sealed class BatteryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("battery"));

    [Fact]
    public void S6272_the_number_the_battery_and_the_ring_each_hold_their_sizes()
    {
        Assert.Equal(["number", "battery", "ring"], WidgetStyles.For(new WidgetId("battery")).Select(s => s.Name));
        Assert.Equal(84, Kind.Sizing(new BatteryConfig()).Natural.Height);
        Assert.Equal(36 + 44 + 40 + 12, Kind.Sizing(new BatteryConfig { Style = BatteryStyle.Battery }).Natural.Height);
        foreach (var style in Enum.GetValues<BatteryStyle>())
        {
            Assert.Empty(Kind.Sizing(new BatteryConfig { Style = style }).Problems());
        }
    }
}
