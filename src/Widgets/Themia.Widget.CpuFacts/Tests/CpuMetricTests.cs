using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuFacts.Tests;

/// <summary>[S-14610] The processor's facts, a single metric each: the feeds, the fact a kind reads, its trend and its click.</summary>
public sealed class CpuMetricTests
{
    [Fact]
    public void S14610_each_reads_the_load_the_counts_and_the_processor_and_charts_nothing()
    {
        var wants = SystemInterests.For(new CpuSpeedConfig { Trend = MetricTrend.Arrow }, @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.Performance), FeedKey.Create(SystemSource.Processor)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S14610_a_kind_reads_its_fact_of_the_sheet_with_its_number_and_one_not_read_is_a_dimmed_dash()
    {
        var sheet = CpuDetailRules.Sheet(Locale.En, new CpuFigures(Mhz: 3400, Cores: 8));

        Assert.Equal(new MetricValue("3.40 GHz", 3400), MetricRules.Pick(sheet, "speed").Value);
        Assert.Equal(new MetricValue("8", 8), MetricRules.Pick(sheet, "cores").Value);
        Assert.Equal(MetricValue.Unread, MetricRules.Pick(sheet, "threads").Value);
    }

    [Fact]
    public void S14610_the_figures_that_move_draw_an_arrow_and_the_constants_no_trend()
    {
        Assert.Equal(MetricHistory.Readings, new CpuSpeedConfig().History);
        Assert.Equal(MetricHistory.Readings, new CpuTemperatureConfig().History);
        Assert.Equal(MetricHistory.None, new CpuCoreCountConfig().History);
        Assert.Equal(MetricHistory.None, new CpuUptimeConfig().History);

        var line = new CpuThreadsConfig { Trend = MetricTrend.Line };
        Assert.NotEmpty(line.Repair());
        Assert.Equal(MetricTrend.Arrow, line.Trend);
    }

    [Fact]
    public void S2011_a_click_opens_task_manager()
        => Assert.Equal(System.IO.Path.Combine(System.Environment.SystemDirectory, "Taskmgr.exe"), CpuMetricModule.Opens!.Value);

    [Fact]
    public void S14610_its_menu_is_the_trend_alone_where_it_has_one()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuSpeedModule()).MenuLines(menu, new CpuSpeedConfig());
        Assert.Equal([Str.Metric_Trend.Text()], menu.Lines.Select(l => l.Text));

        var none = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuCoreCountModule()).MenuLines(none, new CpuCoreCountConfig());
        Assert.Empty(none.Lines);
    }
}
