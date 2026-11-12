using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuDetails.Tests;

/// <summary>[S-9500] The CPU details widget as a module: the feeds it reads and no options of its own.</summary>
public sealed class CpuDetailsTests
{
    [Fact]
    public void S9500_it_reads_the_load_the_counts_and_the_processor_and_charts_nothing()
    {
        var wants = SystemInterests.For(new CpuDetailsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.Performance), FeedKey.Create(SystemSource.Processor)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9500_it_has_no_options_of_its_own()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuDetailsModule()).MenuLines(menu, new CpuDetailsConfig());
        Assert.Empty(menu.Lines);
        Assert.Empty(menu.StyleLines);
    }
}
