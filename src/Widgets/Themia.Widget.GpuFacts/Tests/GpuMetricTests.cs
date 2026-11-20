using System.Text.Json;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.GpuFacts.Tests;

/// <summary>[S-2431] [S-14620] An adapter's facts, a single metric each: the adapter, the feed, the load's line.</summary>
public sealed class GpuMetricTests
{
    [Fact]
    public void S2431_a_new_one_shows_the_first_adapter_and_a_position_that_is_none_is_repaired_to_it()
    {
        Assert.Equal(0, new GpuTemperatureConfig().Adapter);
        var bad = new GpuTemperatureConfig { Adapter = -2 };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(0, bad.Adapter);

        var json = JsonSerializer.Serialize<WidgetConfig>(new GpuFanConfig { Adapter = 1, Trend = MetricTrend.Arrow }, StateJson.WidgetConfig);
        var back = Assert.IsType<GpuFanConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal((1, MetricTrend.Arrow), (back.Adapter, back.Trend));
    }

    [Fact]
    public void S14620_it_reads_the_gpu_feed_and_the_load_s_line_its_adapter_s_series()
    {
        Assert.Equal(SystemInterests.Read(SystemSource.Gpu).Feeds, SystemInterests.For(new GpuLoadConfig { Adapter = 1 }, @"C:\").Feeds);
        Assert.Empty(SystemInterests.For(new GpuLoadConfig { Adapter = 1, Trend = MetricTrend.Arrow }, @"C:\").Series);
        Assert.Equal(GpuInterests.Of(1).Series, SystemInterests.For(new GpuLoadConfig { Adapter = 1, Trend = MetricTrend.Line }, @"C:\").Series);
        Assert.Equal(MetricHistory.Series, new GpuLoadConfig().History);
        Assert.Equal(MetricHistory.Readings, new GpuClockConfig().History);
    }

    [Fact]
    public void S2431_the_adapter_is_offered_with_two_or_more_only_then_the_trend()
    {
        GpuAdapter[] two = [new(0, "Radeon RX 7800", "a", 0, 0), new(1, "Intel Arc", "b", 0, 0)];
        var one = new WidgetMenuLines(() => { });
        GpuMetricModule.Lines(one, new GpuLoadConfig(), [two[0]]);
        Assert.Empty(one.Lines);

        var config = new GpuLoadConfig();
        var menu = new WidgetMenuLines(() => { });
        GpuMetricModule.Lines(menu, config, two);
        Assert.Single(menu.Lines);
        menu.Lines[0].Children![1].Action!();
        Assert.Equal(1, config.Adapter);
    }

    [Fact]
    public void S2015_a_click_opens_task_manager()
        => Assert.Equal(System.IO.Path.Combine(System.Environment.SystemDirectory, "Taskmgr.exe"), GpuKinds.All.Select(kind => kind.Opens!.Target(kind.Create())!.Value).Distinct().Single());

    [Fact]
    public void S14620_no_adapter_keeps_the_row_its_default_dimmed()
    {
        var shown = MetricRules.Pick(GpuDetailRules.Absent(Locale.En, "No graphics adapter"), "temperature");
        Assert.False(shown.Value.Available);
        Assert.Equal("--", shown.Value.Text);
        Assert.Equal("No graphics adapter", shown.Why);
    }
}
