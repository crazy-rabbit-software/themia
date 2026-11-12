using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.BatteryFacts.Tests;

/// <summary>[S-9800] The battery details widget as a module: the feeds it reads and no options of its own.</summary>
public sealed class BatteryFactsTests
{
    [Fact]
    public void S11025_it_reads_the_charge_and_the_battery_pack_and_charts_nothing()
    {
        var wants = SystemInterests.For(new BatteryFactsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Battery), FeedKey.Create(SystemSource.BatteryPack)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S11025_its_one_line_is_the_pick()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new BatteryFactsModule()).MenuLines(menu, new BatteryFactsConfig());
        Assert.Single(menu.Lines);
        Assert.Empty(menu.StyleLines);
    }

    [Fact]
    public void S11025_a_new_one_shows_the_status_the_time_the_health_and_the_full_capacity()
        => Assert.Equal(["status", "time", "health", "fullCapacity"], new BatteryFactsConfig().Facts);
}
