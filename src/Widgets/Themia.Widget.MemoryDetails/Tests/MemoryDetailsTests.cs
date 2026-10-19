using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.MemoryDetails.Tests;

/// <summary>[S-9600] The memory details widget as a module: the feed it reads and no options of its own.</summary>
public sealed class MemoryDetailsTests
{
    [Fact]
    public void S9600_it_reads_the_performance_figures_and_charts_nothing()
    {
        var wants = SystemInterests.For(new MemoryDetailsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Performance)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9600_it_has_no_options_of_its_own()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new MemoryDetailsModule()).MenuLines(menu, new MemoryDetailsConfig());
        Assert.Empty(menu.Lines);
        Assert.Empty(menu.StyleLines);
    }
}
