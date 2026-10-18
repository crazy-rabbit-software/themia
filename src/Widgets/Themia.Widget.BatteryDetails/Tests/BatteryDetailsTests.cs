using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.BatteryDetails.Tests;

/// <summary>[S-9800] The battery details widget as a module: the feeds it reads and its page of nothing.</summary>
public sealed class BatteryDetailsTests
{
    [Fact]
    public void S9800_it_reads_the_charge_and_the_battery_pack_and_charts_nothing()
    {
        var wants = SystemInterests.For(new BatteryDetailsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Battery), FeedKey.Create(SystemSource.BatteryPack)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9800_it_has_no_settings_page()
    {
        var rows = new List<FrameworkElement>();
        ((IWidgetModule)new BatteryDetailsModule()).SettingsRows(new WidgetSettingsRows(rows, () => { }, () => { }), new BatteryDetailsConfig());
        Assert.Empty(rows);
    }
}
