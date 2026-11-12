using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6270]..[S-6274] The I/O readings' and the drive's styles side by side: which each kind offers and
/// that a clone keeps the style. They name several widgets' configs, which are their modules', so they
/// run here. The history is no style any more: it is each subject's history atom ([S-11002]).
/// </summary>
public sealed class IoStylesHostTests
{
    [Theory]
    [InlineData("disk", "number,bars")]
    [InlineData("battery", "number,battery,ring")]
    [InlineData("ping", "number,signal")]
    [InlineData("drive", "bar,ring")]
    public void S6270_S6274_each_kind_offers_its_styles_in_menu_order(string kindId, string names)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
        Assert.False(WidgetStyles.Offers(new WidgetId("network")));
    }

    [Fact]
    public void S6270_S6274_a_clone_keeps_the_style()
    {
        Assert.Equal(DiskStyle.Bars, ((DiskConfig)new DiskConfig { Style = DiskStyle.Bars }.Clone()).Style);
        Assert.Equal(BatteryStyle.Ring, ((BatteryConfig)new BatteryConfig { Style = BatteryStyle.Ring }.Clone()).Style);
        Assert.Equal(PingStyle.Signal, ((PingConfig)new PingConfig { Style = PingStyle.Signal }.Clone()).Style);
        Assert.Equal(DriveStyle.Ring, ((DriveConfig)new DriveConfig { Style = DriveStyle.Ring, Drive = "D:\\" }.Clone()).Style);
    }
}
