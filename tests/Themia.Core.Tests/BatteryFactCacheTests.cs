using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2514] A battery's own facts are asked once per battery tag, and again after a power event.</summary>
public sealed class BatteryFactCacheTests
{
    private const string Bay = @"\?\acpi#pnp0c0a#1#{72631e54-78a4-11d0-bcf7-00aa00b7b32a}";
    private const string Second = @"\?\acpi#pnp0c0a#2#{72631e54-78a4-11d0-bcf7-00aa00b7b32a}";

    [Fact]
    public void S2514_the_same_battery_is_read_once()
    {
        var cache = new BatteryFactCache<int>();
        var reads = 0;

        Assert.Equal(1, cache.Get(Bay, 7, () => ++reads));
        Assert.Equal(1, cache.Get(Bay, 7, () => ++reads));
        Assert.Equal(1, cache.Get(Bay, 7, () => ++reads));
        Assert.Equal(1, reads);
    }

    [Fact]
    public void S2514_a_new_tag_is_another_battery_and_read_again()
    {
        var cache = new BatteryFactCache<string>();

        Assert.Equal("first", cache.Get(Bay, 7, () => "first"));
        Assert.Equal("swapped", cache.Get(Bay, 8, () => "swapped"));
        Assert.Equal("swapped", cache.Get(Bay, 8, () => "unread"));
    }

    [Fact]
    public void S2514_each_bay_keeps_its_own_battery()
    {
        var cache = new BatteryFactCache<string>();

        Assert.Equal("one", cache.Get(Bay, 7, () => "one"));
        Assert.Equal("two", cache.Get(Second, 7, () => "two"));
        Assert.Equal("one", cache.Get(Bay, 7, () => "unread"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void S2514_a_power_event_reads_every_battery_again()
    {
        var cache = new BatteryFactCache<int>();
        var reads = 0;
        cache.Get(Bay, 7, () => ++reads);
        cache.Get(Second, 3, () => ++reads);

        cache.Forget();

        Assert.Equal(0, cache.Count);
        Assert.Equal(3, cache.Get(Bay, 7, () => ++reads));
        Assert.Equal(3, reads);
    }
}
