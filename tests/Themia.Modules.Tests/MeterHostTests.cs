using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
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
    public void S1120_each_meter_has_its_default_span()
    {
        Assert.Equal(60, new CpuConfig().GraphSpanSeconds);
        Assert.Equal(60, new MemoryConfig().GraphSpanSeconds);
        Assert.Equal(60, new NetworkConfig().GraphSpanSeconds);
        Assert.Equal(60, new GpuConfig().GraphSpanSeconds);
        Assert.Equal(300, new DiskConfig().GraphSpanSeconds);
        Assert.Equal(900, new BatteryConfig().GraphSpanSeconds);
        Assert.Equal(900, new PingConfig().GraphSpanSeconds);

        foreach (var config in new GaugeConfig[] { new CpuConfig(), new MemoryConfig(), new NetworkConfig(), new GpuConfig(), new DiskConfig(), new BatteryConfig(), new PingConfig() })
        {
            Assert.Contains(config.GraphSpanSeconds, MeterTiming.SpanChoices);
        }
    }

    [Fact]
    public void S1120_repair_snaps_the_span()
    {
        var cpu = new CpuConfig { GraphSpanSeconds = 7 };
        var notes = cpu.Repair();

        Assert.Single(notes);
        Assert.Equal(60, cpu.GraphSpanSeconds);
        Assert.Empty(cpu.Repair());
    }

    [Fact]
    public void S2503_a_stored_cadence_is_not_a_setting()
    {
        var cpu = (CpuConfig)ConfigJson.Read("Cpu", """{ "kind": "cpu", "intervalSeconds": 300 }""");

        Assert.Empty(cpu.Repair());
        Assert.Null(typeof(GaugeConfig).GetProperty("IntervalSeconds"));
    }

    [Fact]
    public void S6310_only_the_core_styles_ask_the_hub_for_every_processor()
    {
        static bool ReadsCores(CpuStyle style)
            => SystemInterests.For(new CpuConfig { Style = style }, @"C:\").Feeds.Contains(FeedKey.Create(SystemSource.CpuCores));

        Assert.True(ReadsCores(CpuStyle.Cores));
        Assert.True(ReadsCores(CpuStyle.Grid));
        Assert.True(ReadsCores(CpuStyle.CoreGraphs));
        Assert.False(ReadsCores(CpuStyle.Graph));
        Assert.False(ReadsCores(CpuStyle.Gauge));

        // Every style keeps the total's history, so switching back to the graph finds it already there.
        foreach (var style in Enum.GetValues<CpuStyle>())
        {
            Assert.Contains(new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty), SystemInterests.For(new CpuConfig { Style = style }, @"C:\").Series);
        }
    }

    [Fact]
    public void S6318_only_core_graphs_ask_the_hub_for_each_cores_history_and_for_their_span()
    {
        var graphs = SystemInterests.For(new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 300 }, @"C:\");
        Assert.Contains(FeedKey.Create(SystemSource.CpuCores), graphs.Feeds);
        Assert.Equal(300, graphs.CoreHistorySeconds);

        Assert.Equal(0, SystemInterests.For(new CpuConfig { Style = CpuStyle.Grid, GraphSpanSeconds = 300 }, @"C:\").CoreHistorySeconds);
        Assert.Equal(0, SystemInterests.For(new CpuConfig { Style = CpuStyle.Cores }, @"C:\").CoreHistorySeconds);
        Assert.Equal(0, SystemInterests.For(new CpuConfig(), @"C:\").CoreHistorySeconds);
    }

    [Fact]
    public void S6318_the_hub_keeps_the_longest_span_any_view_shows_and_hears_of_a_change()
    {
        var minute = SystemInterests.From(State(new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 60 }, new CpuConfig { Style = CpuStyle.Grid, GraphSpanSeconds = 3600 }), @"C:\");
        Assert.Equal(60, minute.CoreHistorySeconds);

        var both = SystemInterests.From(State(new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 60 }, new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 900 }), @"C:\");
        Assert.Equal(900, both.CoreHistorySeconds);

        // The same feeds and series but a longer span is a change the hub must hear of.
        Assert.False(minute.SameAs(both));
        Assert.True(both.SameAs(SystemInterests.From(State(new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 900 }), @"C:\")));
        Assert.Equal(0, SystemInterests.From(State(new CpuConfig { Style = CpuStyle.Grid }), @"C:\").CoreHistorySeconds);
    }
}
