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
    public void S11021_S14500_four_rows_of_two_lines_a_new_one_and_every_cell_from_one_row_up()
    {
        var sizing = Kind.SizingOf();
        Assert.Equal(4 * 40, sizing.Natural.Height);
        Assert.Equal(40, sizing.Min.Height);
        Assert.Equal(3 * 40, Kind.SizingOf(new NetworkAdaptersConfig { Rows = 3 }).Natural.Height);
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
