using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.CpuCores;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// The host's meter rules a meter widget is the example of ([S-1120] the spans, [S-2503] the one
/// cadence, [S-6310] [S-6318] what the hub reads for a style): they need the meters' configs, which are
/// their modules', so they run here, beside every module. Moved from Core's MeterFrameworkTests,
/// MeterStylesTests and CoreGraphsTests, unchanged in what they assert.
/// </summary>
public sealed class MeterHostTests
{
    private static AppState State(params WidgetConfig[] configs) => new()
    {
        Views =
        [
            new ViewState
            {
                Id = "v0",
                Name = "View",
                Widgets = [.. configs.Select((c, i) => new WidgetInstance { Id = $"w{i}", Config = c })],
            },
        ],
    };

    [Fact]
    public void S1120_S14711_each_metric_graph_has_the_span_its_history_had()
    {
        // [S-14713] The histories' spans are their metrics' graphs' defaults now, the same choices ([S-1121]).
        Assert.Equal(60, MetricGraphs.EffectiveSpan(new Themia.Widget.CpuFacts.CpuLoadConfig()));
        Assert.Equal(60, MetricGraphs.EffectiveSpan(new Themia.Widget.MemoryFacts.MemoryInUseConfig()));
        Assert.Equal(60, MetricGraphs.EffectiveSpan(new Themia.Widget.NetworkFacts.NetworkDownloadConfig()));
        Assert.Equal(60, MetricGraphs.EffectiveSpan(new Themia.Widget.GpuFacts.GpuLoadConfig()));
        Assert.Equal(300, MetricGraphs.EffectiveSpan(new Themia.Widget.DriveFacts.DriveActivityConfig()));
        Assert.Equal(900, MetricGraphs.EffectiveSpan(new Themia.Widget.BatteryFacts.BatteryChargeConfig()));
        Assert.Equal(900, MetricGraphs.EffectiveSpan(new Themia.Widget.PingFacts.PingAverageConfig()));

        foreach (var config in new IMetricGraph[] { new Themia.Widget.CpuFacts.CpuLoadConfig(), new Themia.Widget.DriveFacts.DriveActivityConfig(), new Themia.Widget.PingFacts.PingAverageConfig() })
        {
            Assert.Equal(MeterTiming.SpanChoices, config.Graph!.Spans);
        }
    }

    [Fact]
    public void S1120_S14711_repair_snaps_the_span()
    {
        var cpu = new Themia.Widget.CpuFacts.CpuLoadConfig { Span = 7 };
        var notes = cpu.Repair();

        Assert.Single(notes);
        Assert.Equal(60, MetricGraphs.EffectiveSpan(cpu));
        Assert.Empty(cpu.Repair());
    }

    [Fact]
    public void S2503_a_stored_cadence_is_not_a_setting()
    {
        var cpu = (CpuConfig)ConfigJson.Read("Cpu", """{ "kind": "cpu", "intervalSeconds": 300 }""");

        Assert.Empty(cpu.Repair());
        Assert.Null(typeof(ReadingConfig).GetProperty("IntervalSeconds"));
        Assert.Null(typeof(HistoryConfig).GetProperty("IntervalSeconds"));
    }

    [Fact]
    public void S6310_only_the_cores_ask_the_hub_for_every_processor()
    {
        foreach (var style in Enum.GetValues<CpuCoresStyle>())
        {
            Assert.Contains(FeedKey.Create(SystemSource.CpuCores), SystemInterests.For(new CpuCoresConfig { Style = style }, @"C:\").Feeds);
        }

        Assert.DoesNotContain(FeedKey.Create(SystemSource.CpuCores), SystemInterests.For(new CpuConfig(), @"C:\").Feeds);
        Assert.DoesNotContain(FeedKey.Create(SystemSource.CpuCores), SystemInterests.For(new Themia.Widget.CpuFacts.CpuLoadConfig(), @"C:\").Feeds);

        // [S-11000] [S-14714] The reading, the load's metric and the cores read the one series, so they agree to the point.
        var series = new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty);
        Assert.Contains(series, SystemInterests.For(new CpuConfig(), @"C:\").Series);
        Assert.Contains(series, SystemInterests.For(new Themia.Widget.CpuFacts.CpuLoadConfig(), @"C:\").Series);
        Assert.Contains(series, SystemInterests.For(new CpuCoresConfig(), @"C:\").Series);
    }

    [Fact]
    public void S6318_only_core_graphs_ask_the_hub_for_each_cores_history_and_for_their_span()
    {
        var graphs = SystemInterests.For(new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 300 }, @"C:\");
        Assert.Contains(FeedKey.Create(SystemSource.CpuCores), graphs.Feeds);
        Assert.Equal(300, graphs.CoreHistorySeconds);

        Assert.Equal(0, SystemInterests.For(new CpuCoresConfig { Style = CpuCoresStyle.Grid, GraphSpanSeconds = 300 }, @"C:\").CoreHistorySeconds);
        Assert.Equal(0, SystemInterests.For(new CpuCoresConfig { Style = CpuCoresStyle.Bars }, @"C:\").CoreHistorySeconds);
        Assert.Equal(0, SystemInterests.For(new CpuConfig(), @"C:\").CoreHistorySeconds);
    }

    [Fact]
    public void S6318_the_hub_keeps_the_longest_span_any_view_shows_and_hears_of_a_change()
    {
        var minute = SystemInterests.From(State(new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 60 }, new CpuCoresConfig { Style = CpuCoresStyle.Grid, GraphSpanSeconds = 3600 }), @"C:\");
        Assert.Equal(60, minute.CoreHistorySeconds);

        var both = SystemInterests.From(State(new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 60 }, new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 900 }), @"C:\");
        Assert.Equal(900, both.CoreHistorySeconds);

        // The same feeds and series but a longer span is a change the hub must hear of.
        Assert.False(minute.SameAs(both));
        Assert.True(both.SameAs(SystemInterests.From(State(new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 900 }), @"C:\")));
        Assert.Equal(0, SystemInterests.From(State(new CpuCoresConfig { Style = CpuCoresStyle.Grid }), @"C:\").CoreHistorySeconds);
    }

    [Fact]
    public void S1120_clone_carries_the_shared_settings()
    {
        var ping = new Themia.Widget.PingFacts.PingAverageConfig { Span = 1800, Host = "example.com" };
        var copy = (Themia.Widget.PingFacts.PingAverageConfig)ping.Clone();

        Assert.Equal((1800, "example.com"), (copy.Span, copy.Host));
    }
}
