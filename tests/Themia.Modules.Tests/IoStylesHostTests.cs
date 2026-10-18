using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6267] [S-6270]..[S-6274] The I/O meters' and the drive's styles side by side: which each kind
/// offers, that only a graph draws the history, that a clone keeps the style. They name several
/// widgets' configs, which are their modules', so they run here. Moved from Core's IoStylesTests,
/// unchanged in what they assert.
/// </summary>
public sealed class IoStylesHostTests
{
    private sealed class PlainGauge() : GaugeConfig(60)
    {
        public override WidgetConfig Clone() => new PlainGauge();
    }

    [Theory]
    [InlineData("network", "graph,numbers")]
    [InlineData("disk", "graph,bars")]
    [InlineData("battery", "graph,battery,ring")]
    [InlineData("ping", "graph,signal")]
    [InlineData("drive", "bar,ring")]
    public void S6270_S6274_each_kind_offers_its_styles_in_menu_order(string kindId, string names)
    {
        var kind = new WidgetId(kindId);
        Assert.Equal(names, string.Join(",", WidgetStyles.For(kind).Select(s => s.Name)));
    }

    [Fact]
    public void S6267_only_a_graph_style_draws_the_history_whatever_the_old_switch_says()
    {
        Assert.True(new NetworkConfig().DrawsHistory);
        Assert.False(new NetworkConfig { Style = NetworkStyle.Numbers }.DrawsHistory);
        Assert.False(new DiskConfig { Style = DiskStyle.Bars }.DrawsHistory);
        Assert.False(new BatteryConfig { Style = BatteryStyle.Battery }.DrawsHistory);
        Assert.False(new BatteryConfig { Style = BatteryStyle.Ring }.DrawsHistory);
        Assert.False(new PingConfig { Style = PingStyle.Signal }.DrawsHistory);

        // The switch is not offered for a styled kind ([S-6000]): a stored off is not a second way to hide the graph.
        Assert.True(new PingConfig { ShowGraph = false }.DrawsHistory);

        // A meter without styles keeps its switch.
        Assert.False(new PlainGauge { ShowGraph = false }.DrawsHistory);
    }

    [Fact]
    public void S6270_S6274_a_clone_keeps_the_style()
    {
        Assert.Equal(NetworkStyle.Numbers, ((NetworkConfig)new NetworkConfig { Style = NetworkStyle.Numbers }.Clone()).Style);
        Assert.Equal(DiskStyle.Bars, ((DiskConfig)new DiskConfig { Style = DiskStyle.Bars }.Clone()).Style);
        Assert.Equal(BatteryStyle.Ring, ((BatteryConfig)new BatteryConfig { Style = BatteryStyle.Ring }.Clone()).Style);
        Assert.Equal(PingStyle.Signal, ((PingConfig)new PingConfig { Style = PingStyle.Signal }.Clone()).Style);
        Assert.Equal(DriveStyle.Ring, ((DriveConfig)new DriveConfig { Style = DriveStyle.Ring, Drive = "D:\\" }.Clone()).Style);
    }
}
