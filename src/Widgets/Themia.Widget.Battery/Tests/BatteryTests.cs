using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Battery.Tests;

/// <summary>[S-17182] This PC's battery as a device item: its charge, its line, its bar, and none.</summary>
public sealed class BatteryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("battery"));

    [Fact]
    public void S17182_a_charging_battery_is_its_charge_its_state_and_time_to_full_and_its_bar()
    {
        // Owner, 2026-11-23: "battery widget should get the same base container as teh bluetooth device one and should also
        // have the percentage progress as well".
        var pack = new BatteryPack(false, 57_000, 52_000, 33_280, 18_000, 12_400, 210, 31, "Li-ion", "Sample", 1);
        var item = BatteryItems.Item(Locale.En, true, new BatteryPower(true, 64, true, true, null), pack);

        Assert.Equal(("battery", "Battery", "64", "%"), (item.Id, item.Title, item.End!.Value, item.End.Unit));
        Assert.Equal("Charging · Full in 1h 2m", item.Subtitle);
        Assert.Equal((0.64, ColourRole.Accent), (item.Bar!.To, item.Bar.Role));
        Assert.Equal(BatteryItems.Glyph(64, charging: true), item.Lead!.Glyph);
        Assert.NotEqual(BatteryItems.Glyph(64, charging: false), item.Lead.Glyph);
    }

    [Fact]
    public void S17182_on_battery_it_says_the_time_left_and_low_its_bar_is_critical()
    {
        var item = BatteryItems.Item(Locale.En, true, new BatteryPower(true, 82, false, false, TimeSpan.FromMinutes(190)), null);
        Assert.Equal("On battery · 3h 10m left", item.Subtitle);
        Assert.Equal(ColourRole.Accent, item.Bar!.Role);

        Assert.Equal(ColourRole.Critical, BatteryItems.Item(Locale.En, true, new BatteryPower(true, BatteryItems.Low, false, false, null), null).Bar!.Role);
        Assert.Equal("Fully charged", BatteryItems.Item(Locale.En, true, new BatteryPower(true, 100, true, false, null), null).Subtitle);
    }

    [Fact]
    public void S17182_no_battery_has_no_percentage_and_no_bar_and_says_why()
    {
        var none = BatteryItems.Item(Locale.En, true, new BatteryPower(false, null, true, false, null), null);
        Assert.Equal(((ItemEnd?)null, (ItemBar?)null, "No battery"), (none.End, none.Bar, none.Subtitle));
        Assert.True(none.Absent);

        Assert.Equal("Loading…", BatteryItems.Item(Locale.En, false, null, null).Subtitle);
        Assert.Null(BatteryItems.Item(Locale.En, true, new BatteryPower(true, null, false, false, null), null).Bar);
    }

    [Fact]
    public void S17182_its_card_is_one_device_item_its_bar_at_its_side()
    {
        var sizing = Kind.Sizing(new BatteryConfig());
        Assert.Equal((60.0, 60.0), (sizing.Min.Height, sizing.Natural.Height));
        Assert.Equal(300, sizing.Natural.Width);
        Assert.Empty(sizing.Problems());
        Assert.False(WidgetStyles.Offers(new WidgetId("battery")));

        var copy = (BatteryConfig)new BatteryConfig { HiddenParts = [ItemPart.Bar] }.Clone();
        Assert.Equal([ItemPart.Bar], copy.HiddenParts);
    }
}
