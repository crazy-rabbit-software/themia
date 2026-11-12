using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkAdapters.Tests;

/// <summary>[S-11021] The network adapters list: its rows of two lines, its feeds, its one line.</summary>
public sealed class NetworkAdaptersTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("networkAdapters"));

    [Fact]
    public void S11021_four_rows_of_two_lines_and_a_dragged_edge_sets_them()
    {
        var sizing = Kind.SizingOf();
        Assert.Equal(36 + (4 * 40) + 12, sizing.Natural.Height);
        var three = Assert.IsType<NetworkAdaptersConfig>(Kind.SizeSets!(new NetworkAdaptersConfig(), new SizeDip(300, 36 + (3 * 40) + 12)));
        Assert.Equal(3, three.Rows);
        Assert.Equal(7, ((NetworkAdaptersConfig)new NetworkAdaptersConfig { Rows = 7 }.Clone()).Rows);
    }

    [Fact]
    public void S11021_it_reads_the_addresses_and_the_throughput_and_its_one_line_is_rows()
    {
        Assert.Equal([FeedKey.Create(SystemSource.LocalAddresses), FeedKey.Create(SystemSource.Network)], SystemInterests.For(new NetworkAdaptersConfig(), @"C:\").Feeds);

        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new NetworkAdaptersModule()).MenuLines(menu, new NetworkAdaptersConfig());
        Assert.Single(menu.Lines);

        var bad = new NetworkAdaptersConfig { Rows = 0 };
        Assert.Single(bad.Repair());
        Assert.Equal(1, bad.Rows);
    }
}
