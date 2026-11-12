using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.MemoryFacts.Tests;

/// <summary>[S-11017] The memory facts atom: the feed it reads, its pick and its one menu line.</summary>
public sealed class MemoryFactsTests
{
    [Fact]
    public void S11017_it_reads_the_performance_figures_and_charts_nothing()
    {
        var wants = SystemInterests.For(new MemoryFactsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Performance)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S11017_a_new_one_shows_in_use_available_committed_and_cached()
        => Assert.Equal(["inUse", "available", "committed", "cached"], new MemoryFactsConfig().Facts);

    [Fact]
    public void S11017_in_use_is_used_of_total_and_every_fact_has_its_key()
    {
        var sheet = MemoryDetailRules.Sheet(Locale.En, new PerformanceFigures(1, 2, 32UL << 30, 16UL << 30, 1, 1, 1, 1, 1, 1), null);
        var rows = FactsRules.Pick(Locale.En, sheet, ["inUse", "pageFile"], MemoryDetailRules.Facts);
        Assert.Equal("16 GB / 32 GB", rows[0].Value);
        Assert.False(rows[1].Available);
        Assert.All(sheet.Facts, f => Assert.Contains(MemoryDetailRules.Facts, o => o.Key == f.Key));
    }

    [Fact]
    public void S11017_its_one_line_is_the_pick()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new MemoryFactsModule()).MenuLines(menu, new MemoryFactsConfig());
        Assert.Equal(Str.Details_Facts.Text(), Assert.Single(menu.Lines).Text);
        Assert.Empty(menu.StyleLines);
    }
}
