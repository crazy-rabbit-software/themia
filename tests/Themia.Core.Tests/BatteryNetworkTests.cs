using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The battery and network meters' rules (S-1180..S-1192).</summary>
public sealed class BatteryNetworkTests
{
    // ---- S-1180: the power status -----------------------------------------------------------------

    [Fact]
    public void S1180_charging_is_the_flag_bit_not_the_plug()
    {
        // Plugged in, flag "high" (1) without the charging bit: plugged in, not charging.
        var full = BatteryRules.Read(acLineStatus: 1, batteryFlag: 1, batteryLifePercent: 100, batteryLifeTime: uint.MaxValue);
        Assert.True(full.OnMains);
        Assert.False(full.Charging);

        var charging = BatteryRules.Read(1, 8 | 2, 40, uint.MaxValue);
        Assert.True(charging.Charging);

        // 255 is "unknown", not "every bit set".
        Assert.False(BatteryRules.Read(1, 255, 40, uint.MaxValue).Charging);
    }

    [Fact]
    public void S1180_a_desktop_has_no_battery()
    {
        var desktop = BatteryRules.Read(1, 128, 255, uint.MaxValue);
        Assert.False(desktop.HasBattery);
        Assert.Null(desktop.Percent);
    }

    [Fact]
    public void S1180_a_machine_windows_will_not_answer_about_keeps_its_battery()
    {
        // 255 in the flag is "I do not know", a different claim from "there is none".
        var unknown = BatteryRules.Read(255, 255, 255, uint.MaxValue);
        Assert.True(unknown.HasBattery);
        Assert.Null(unknown.Percent);
        Assert.False(unknown.OnMains);
    }

    [Fact]
    public void S1180_a_real_percentage_outranks_a_flag_that_says_there_is_no_battery()
    {
        var detachable = BatteryRules.Read(0, 128, 80, uint.MaxValue);
        Assert.True(detachable.HasBattery);
        Assert.Equal(80, detachable.Percent);
    }

    [Fact]
    public void S1180_time_left_is_only_a_real_estimate_while_discharging()
    {
        // 0xFFFFFFFF seconds is 136 years; plugged in, the field holds a stale estimate.
        Assert.Null(BatteryRules.Read(0, 4, 50, uint.MaxValue).Remaining);
        Assert.Null(BatteryRules.Read(1, 4, 50, 7200).Remaining);
        Assert.Equal(TimeSpan.FromHours(2), BatteryRules.Read(0, 4, 64, 7200).Remaining);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(73, 7)]
    [InlineData(100, 10)]
    public void S1187_icon_level_is_the_nearest_tenth(int? percent, int expected) => Assert.Equal(expected, BatteryRules.Level(percent));

    // ---- S-1190: throughput ---------------------------------------------------------------------

    [Fact]
    public void S1190_rates_are_per_adapter_deltas_over_elapsed_time()
    {
        var tracker = new ThroughputTracker();
        var first = tracker.Update([new("a", "Wi-Fi", 1_000, 500), new("b", "Ethernet", 0, 0)], 0);
        Assert.Equal(0, first.Down);

        var second = tracker.Update([new("a", "Wi-Fi", 3_000, 1_500), new("b", "Ethernet", 4_000, 0)], 2_000);
        Assert.Equal(3_000, second.Down);
        Assert.Equal(500, second.Up);
        Assert.Equal("Ethernet", second.Interfaces[0].Name);
        Assert.Equal(6_000, second.SessionDown);
        Assert.Equal(1_000, second.SessionUp);
    }

    [Fact]
    public void S1190_one_adapter_resetting_does_not_zero_the_others()
    {
        var tracker = new ThroughputTracker();
        tracker.Update([new("a", "Wi-Fi", 10_000, 0), new("b", "Ethernet", 10_000, 0)], 0);
        var reset = tracker.Update([new("a", "Wi-Fi", 100, 0), new("b", "Ethernet", 12_000, 0)], 1_000);

        Assert.Equal(2_000, reset.Down);
        Assert.Equal(2_000, reset.SessionDown);

        // The reset adapter counts again from its new baseline, and the session never goes back.
        var after = tracker.Update([new("a", "Wi-Fi", 1_100, 0), new("b", "Ethernet", 12_000, 0)], 2_000);
        Assert.Equal(1_000, after.Down);
        Assert.Equal(3_000, after.SessionDown);
    }

    [Fact]
    public void S1190_a_new_adapter_starts_from_a_baseline_not_its_lifetime()
    {
        var tracker = new ThroughputTracker();
        tracker.Update([new("a", "Wi-Fi", 0, 0)], 0);
        var joined = tracker.Update([new("a", "Wi-Fi", 0, 0), new("b", "Ethernet", 9_000_000_000, 0)], 1_000);
        Assert.Equal(0, joined.Down);
    }

    [Fact]
    public void S1190_no_elapsed_time_is_no_rate()
    {
        var tracker = new ThroughputTracker();
        tracker.Update([new("a", "Wi-Fi", 0, 0)], 1_000);
        var same = tracker.Update([new("a", "Wi-Fi", 5_000, 0)], 1_000);
        Assert.Equal(0, same.Down);
        Assert.Equal(5_000, same.SessionDown);
    }

    // ---- S-1191: bar scale ----------------------------------------------------------------------

    // ---- S-1192: what counts --------------------------------------------------------------------

    [Theory]
    [InlineData("Ethernet-Npcap Packet Driver (NPCAP)-0000", "Intel(R) Ethernet", true)]
    [InlineData("Ethernet", "Realtek PCIe GbE-WinPcap Packet Driver", true)]
    [InlineData("Npcap Loopback Adapter", "Npcap Loopback Adapter", false)]
    [InlineData("Wi-Fi", "Intel(R) Wi-Fi 6E AX211", false)]
    public void S1192_capture_mirrors_are_not_counted(string name, string description, bool mirror)
    {
        Assert.Equal(mirror, NetworkRules.IsCaptureMirror(name, description));
        Assert.Equal(!mirror, NetworkRules.Counts(true, false, name, description));
    }

    [Theory]
    [InlineData("WiFi-QoS Packet Scheduler-0000", true)]
    [InlineData("WiFi-WFP Native MAC Layer LightWeight Filter-0000", true)]
    [InlineData("wifi-Baumer Packet Filter Driver-0001", true)]
    [InlineData("WiFi", false)]
    [InlineData("Lab-0001", false)]
    [InlineData("WiFi-0000", false)]
    [InlineData("Ethernet-Npcap Packet Driver (NPCAP)", false)]
    [InlineData("Local Area Connection* 10", false)]
    public void S1192_filter_bindings_of_another_adapter_are_not_counted(string name, bool binding)
        => Assert.Equal(binding, NetworkRules.IsFilterBinding(name, ["WiFi", "Ethernet", "Lab-0001", "Local Area Connection* 10", name]));

    [Fact]
    public void S1192_down_loopback_and_tunnels_are_not_counted()
    {
        Assert.False(NetworkRules.Counts(false, false, "Ethernet", "x"));
        Assert.False(NetworkRules.Counts(true, true, "Loopback", "x"));
    }
}
