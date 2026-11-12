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
    public void S1120_each_meter_has_its_default_span()
    {
        Assert.Equal(60, new Themia.Widget.CpuHistory.CpuHistoryConfig().GraphSpanSeconds);
        Assert.Equal(60, new Themia.Widget.MemoryHistory.MemoryHistoryConfig().GraphSpanSeconds);
        Assert.Equal(60, new Themia.Widget.NetworkHistory.NetworkHistoryConfig().GraphSpanSeconds);
        Assert.Equal(60, new Themia.Widget.GpuHistory.GpuHistoryConfig().GraphSpanSeconds);
        Assert.Equal(300, new Themia.Widget.DiskHistory.DiskHistoryConfig().GraphSpanSeconds);
        Assert.Equal(900, new Themia.Widget.BatteryHistory.BatteryHistoryConfig().GraphSpanSeconds);
        Assert.Equal(900, new Themia.Widget.PingHistory.PingHistoryConfig().GraphSpanSeconds);

        foreach (var config in new ISpansHistory[] { new Themia.Widget.MemoryHistory.MemoryHistoryConfig(), new Themia.Widget.NetworkHistory.NetworkHistoryConfig(), new Themia.Widget.GpuHistory.GpuHistoryConfig(), new Themia.Widget.DiskHistory.DiskHistoryConfig(), new Themia.Widget.BatteryHistory.BatteryHistoryConfig(), new Themia.Widget.PingHistory.PingHistoryConfig() })
        {
            Assert.Contains(config.GraphSpanSeconds, MeterTiming.SpanChoices);
        }
    }

    [Fact]
    public void S1120_repair_snaps_the_span()
    {
        var cpu = new Themia.Widget.CpuHistory.CpuHistoryConfig { GraphSpanSeconds = 7 };
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
        Assert.DoesNotContain(FeedKey.Create(SystemSource.CpuCores), SystemInterests.For(new Themia.Widget.CpuHistory.CpuHistoryConfig(), @"C:\").Feeds);

        // [S-11000] The reading, the history and the cores read the one series, so they agree to the point.
        var series = new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty);
        Assert.Contains(series, SystemInterests.For(new CpuConfig(), @"C:\").Series);
        Assert.Contains(series, SystemInterests.For(new Themia.Widget.CpuHistory.CpuHistoryConfig(), @"C:\").Series);
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
        var ping = new Themia.Widget.PingHistory.PingHistoryConfig { GraphSpanSeconds = 1800, Host = "example.com" };
        var copy = (Themia.Widget.PingHistory.PingHistoryConfig)ping.Clone();

        Assert.Equal((1800, "example.com"), (copy.GraphSpanSeconds, copy.Host));
    }

    [Fact]
    public void S700_the_disk_meter_starts_with_its_header_like_the_other_meters()
    {
        Assert.Equal(HeaderPlacement.Top, WidgetInstance.DefaultHeader(new WidgetId("disk")));
        Assert.Equal(WidgetInstance.DefaultHeader(new WidgetId("memory")), WidgetInstance.DefaultHeader(new WidgetId("disk")));
    }
}
