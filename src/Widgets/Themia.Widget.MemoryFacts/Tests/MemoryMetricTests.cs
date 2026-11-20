using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.MemoryFacts.Tests;

/// <summary>[S-2408] [S-14630] The memory's facts, a single metric each: the feeds, the fact a kind reads, the in-use line.</summary>
public sealed class MemoryMetricTests
{
    [Fact]
    public void S14630_each_reads_the_performance_figures_and_in_use_s_line_the_memory_s_share_series()
    {
        Assert.Equal([FeedKey.Create(SystemSource.Performance)], SystemInterests.For(new MemoryAvailableConfig(), @"C:\").Feeds);
        Assert.Empty(SystemInterests.For(new MemoryInUseConfig { Trend = MetricTrend.Arrow }, @"C:\").Series);

        var line = SystemInterests.For(new MemoryInUseConfig { Trend = MetricTrend.Line }, @"C:\");
        Assert.Equal(SystemInterests.Chart(SystemSource.Memory).Series, line.Series);
        Assert.Contains(FeedKey.Create(SystemSource.Performance), line.Feeds);
        Assert.Equal(MetricHistory.Series, new MemoryInUseConfig().History);
        Assert.Equal(MetricHistory.Readings, new MemoryPageFileConfig().History);
    }

    [Fact]
    public void S14630_in_use_is_used_of_total_with_the_bytes_used_as_its_number()
    {
        const ulong Gb = 1024UL * 1024 * 1024;
        var figures = new PerformanceFigures(10 * Gb, 40 * Gb, 32 * Gb, 20 * Gb, 4 * Gb, Gb, Gb / 2, 1000, 200, 3000);
        var sheet = MemoryDetailRules.Sheet(Locale.En, figures, new PagefileUsage(0, 0));

        var inUse = MetricRules.Pick(sheet, "inUse").Value;
        Assert.Equal(12d * Gb, inUse.Number);
        Assert.Contains("/", inUse.Text, StringComparison.Ordinal);
        Assert.Null(MetricRules.Pick(sheet, "pageFile").Value.Number); // no page file: "None", no figure to trend
    }

    [Fact]
    public void S2013_a_click_opens_task_manager()
        => Assert.Equal(System.IO.Path.Combine(System.Environment.SystemDirectory, "Taskmgr.exe"), MemoryKinds.All.Select(kind => kind.Opens!.Target(kind.Create())!.Value).Distinct().Single());
}
