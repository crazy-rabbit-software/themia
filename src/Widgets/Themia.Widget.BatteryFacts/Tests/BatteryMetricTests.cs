using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.BatteryFacts.Tests;

/// <summary>[S-2423] [S-14660] What the battery reports, a single metric each: the feeds, a desktop's dimmed default, the trends.</summary>
public sealed class BatteryMetricTests
{
    [Fact]
    public void S14660_each_reads_the_charge_and_the_battery_pack_and_charts_nothing()
    {
        var wants = SystemInterests.For(new BatteryTimeConfig { Trend = MetricTrend.Arrow }, @"C:\");
        Assert.Equal([FeedKey.Create(SystemSource.Battery), FeedKey.Create(SystemSource.BatteryPack)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S14660_a_machine_without_a_battery_keeps_the_row_its_default_dimmed_and_says_why()
    {
        var shown = MetricRules.Pick(BatteryDetailRules.Sheet(Locale.En, new BatteryPower(false, null, true, false, null), null), "health");
        Assert.Equal(("--", false), (shown.Value.Text, shown.Value.Available));
        Assert.False(string.IsNullOrEmpty(shown.Why));
    }

    [Fact]
    public void S14660_the_time_names_itself_full_in_while_charging_and_carries_its_minutes()
    {
        var charging = new BatteryPower(true, 40, true, true, null);
        var pack = new BatteryPack(false, 50000, 48000, 20000, 15000, 11400, 120, 31, "LION", "Maker", 1);
        var shown = MetricRules.Pick(BatteryDetailRules.Sheet(Locale.En, charging, pack), "time");

        Assert.Equal(Strings.Get(Locale.En, Str.Details_Battery_FullIn), shown.Name);
        Assert.NotNull(shown.Value.Number);
        Assert.Equal(MetricHistory.Readings, new BatteryTimeConfig().History);
        Assert.Equal(MetricHistory.None, new BatteryChemistryConfig().History);
    }

    [Fact]
    public void S2023_a_click_opens_its_settings_page()
        => Assert.All(BatteryKinds.All, kind => Assert.Equal("ms-settings:batterysaver", kind.Opens!.Target(kind.Create())!.Value));
}
