using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The system data hub's rules (S-2500..S-2513): keys, the one cadence, history, interests and holding.</summary>
public sealed class SystemDataHubTests
{
    private const string Root = "C:\\";

    // ---- S-2501: the read order ----------------------------------------------------------------

    [Fact]
    public void S2501_what_a_source_is_combined_with_is_read_before_it()
    {
        // CPU details combine the load and the counts with the processor's figures; network details
        // the addresses with the throughput; battery details the charge with the pack.
        Assert.True(SystemSource.Cpu < SystemSource.Processor);
        Assert.True(SystemSource.Performance < SystemSource.Processor);
        Assert.True(SystemSource.LocalAddresses < SystemSource.Network);
        Assert.True(SystemSource.PublicAddress < SystemSource.Network);
        Assert.True(SystemSource.Battery < SystemSource.BatteryPack);
    }

    [Fact]
    public void S2501_feeds_are_listed_in_read_order()
    {
        var table = new FeedTable<object>(_ => new object());
        table.Join(FeedKey.Create(SystemSource.Processor), out _);
        table.Join(FeedKey.Create(SystemSource.Ping, "b.example"), out _);
        table.Join(FeedKey.Create(SystemSource.Cpu), out _);
        table.Want([FeedKey.Create(SystemSource.Ping, "a.example")], out _);

        var order = table.Ordered().Select(e => e.Key.ToString()).ToArray();

        Assert.Equal(["Cpu", "Processor", "Ping(a.example)", "Ping(b.example)"], order);
    }

    // ---- S-2502: feed keys ---------------------------------------------------------------------

    [Fact]
    public void S2502_one_host_however_it_is_typed_is_one_prober()
    {
        var a = FeedKey.Create(SystemSource.Ping, " Example.COM ");
        var b = FeedKey.Create(SystemSource.Ping, "example.com");

        Assert.Equal(a, b);
        Assert.Equal("example.com", a.Target);
    }

    [Fact]
    public void S2502_another_host_or_source_is_another_feed()
    {
        var ping = FeedKey.Create(SystemSource.Ping, "example.com");

        Assert.NotEqual(ping, FeedKey.Create(SystemSource.Ping, "example.org"));
        Assert.NotEqual(ping, FeedKey.Create(SystemSource.Network));
    }

    [Fact]
    public void S2502_a_machine_source_has_no_target()
    {
        Assert.Equal(string.Empty, FeedKey.Create(SystemSource.Cpu, "  ").Target);
        Assert.Equal(string.Empty, FeedKey.Create(SystemSource.Cpu).Target);
    }

    // ---- S-2503 / S-2512: one cadence ----------------------------------------------------------

    [Fact]
    public void S2503_every_machine_source_is_read_every_second()
    {
        foreach (var source in Enum.GetValues<SystemSource>().Where(s => s != SystemSource.Ping))
        {
            Assert.Equal(SystemCadence.TickSeconds, SystemCadence.PeriodSeconds(source));
        }

        Assert.Equal(TimeSpan.FromSeconds(1), SystemCadence.Tick);
    }

    [Fact]
    public void S2512_a_ping_probes_every_fifth_tick_on_the_same_boundaries()
    {
        Assert.Equal(5, SystemCadence.PeriodSeconds(SystemSource.Ping));
        Assert.Equal(0, SystemCadence.PingSeconds % SystemCadence.TickSeconds);

        var start = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        long cpu = long.MinValue, ping = long.MinValue;
        var cpuReads = new List<int>();
        var pingReads = new List<int>();

        for (var s = 0; s < 11; s++)
        {
            var now = start.AddSeconds(s).AddMilliseconds(15);

            if (SystemCadence.IsDue(cpu, now, SystemSource.Cpu, out var c))
            {
                cpu = c;
                cpuReads.Add(s);
            }

            if (SystemCadence.IsDue(ping, now, SystemSource.Ping, out var p))
            {
                ping = p;
                pingReads.Add(s);
            }
        }

        Assert.Equal(Enumerable.Range(0, 11), cpuReads);
        Assert.Equal([0, 5, 10], pingReads);

        // Every probe lands on a tick the other sources are read on.
        Assert.All(pingReads, s => Assert.Contains(s, cpuReads));
    }

    [Fact]
    public void S2503_a_source_read_this_second_is_not_due_again_in_it()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 5, TimeSpan.Zero);
        Assert.True(SystemCadence.IsDue(long.MinValue, now, SystemSource.Memory, out var period));
        Assert.False(SystemCadence.IsDue(period, now.AddMilliseconds(400), SystemSource.Memory, out _));
        Assert.True(SystemCadence.IsDue(period, now.AddSeconds(1), SystemSource.Memory, out _));
    }

    [Fact]
    public void S2503_a_clock_that_went_back_is_due_at_once()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 5, TimeSpan.Zero);
        SystemCadence.IsDue(long.MinValue, now, SystemSource.Cpu, out var period);

        Assert.True(SystemCadence.IsDue(period, now.AddSeconds(-30), SystemSource.Cpu, out _));
    }

    // ---- S-2504: the history ring --------------------------------------------------------------

    [Fact]
    public void S2504_the_ring_keeps_the_newest_and_forgets_the_oldest()
    {
        var ring = new SampleRing<int>(3);

        foreach (var i in new[] { 1, 2, 3, 4, 5 })
        {
            ring.Add(i);
        }

        Assert.Equal(3, ring.Count);
        Assert.Equal(5, ring.Added);
        Assert.Equal(5, ring.Latest);
        Assert.Equal([3, 4, 5], ring.Newest(10));
        Assert.Equal([4, 5], ring.Newest(2));
        Assert.Empty(ring.Newest(0));
    }

    [Fact]
    public void S2504_a_ring_not_yet_full_hands_back_what_it_has_oldest_first()
    {
        var ring = new SampleRing<int>(5);
        ring.Add(7);
        ring.Add(8);

        Assert.Equal([7, 8], ring.Newest(5));
        Assert.Equal(8, ring.Latest);
    }

    [Fact]
    public void S2504_a_cleared_ring_is_empty()
    {
        var ring = new SampleRing<int>(2);
        ring.Add(1);
        ring.Add(2);
        ring.Add(3);
        ring.Clear();

        Assert.Equal(0, ring.Count);
        Assert.Equal(0, ring.Added);
        Assert.Null(ring.Latest);
        Assert.Empty(ring.Newest(2));

        ring.Add(9);
        Assert.Equal([9], ring.Newest(2));
    }

    [Fact]
    public void S2504_the_history_holds_the_longest_span_and_the_widest_ping_window()
    {
        Assert.Equal(MeterTiming.MaxSpanSeconds, SeriesHistory.Capacity(SystemSource.Cpu));
        Assert.Equal(MeterTiming.Points(MeterTiming.SpanChoices.Max(), SystemCadence.TickSeconds), SeriesHistory.Capacity(SystemSource.Gpu));
        Assert.Equal(MeterTiming.Points(MeterTiming.SpanChoices.Max(), SystemCadence.PingSeconds), SeriesHistory.Capacity(SystemSource.Ping));
        Assert.True(SeriesHistory.Capacity(SystemSource.Ping) >= PingDetailsConfig.MaxWindow);
    }

    [Fact]
    public void S2504_a_missed_probe_plots_but_is_no_answer()
    {
        var answered = new SeriesPoint(12);
        var missed = new SeriesPoint(0, Missed: true);

        Assert.Equal(12, answered.Answer);
        Assert.Null(missed.Answer);
        Assert.True(double.IsNaN(answered.Second));
    }

    // ---- S-2505: wanted and held ---------------------------------------------------------------

    [Fact]
    public void S2505_every_widget_on_one_key_shares_one_feed()
    {
        var made = 0;
        var table = new FeedTable<object>(_ => { made++; return new object(); });
        var key = FeedKey.Create(SystemSource.Cpu);

        var first = table.Join(key, out var created);
        Assert.True(created);

        var second = table.Join(key, out created);
        Assert.False(created);
        Assert.Same(first, second);
        Assert.Equal(1, made);
        Assert.Equal(2, table.Holders(key));
    }

    [Fact]
    public void S2505_a_feed_nobody_holds_or_wants_is_dropped_at_once()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Gpu);
        var feed = table.Join(key, out _);
        table.Join(key, out _);

        Assert.Null(table.Leave(key, out var known));
        Assert.True(known);
        Assert.Same(feed, table.Leave(key, out _));
        Assert.Equal(0, table.Count);

        Assert.NotSame(feed, table.Join(key, out var created));
        Assert.True(created);
    }

    [Fact]
    public void S2505_a_wanted_feed_outlives_its_last_watch_with_its_history()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu);
        table.Want([key], out var created);
        var feed = Assert.Single(created);

        // A view switch: the watch goes, the feed (and so its history) stays.
        Assert.Same(feed, table.Join(key, out var made));
        Assert.False(made);
        Assert.Null(table.Leave(key, out _));
        Assert.True(table.TryGet(key, out var kept));
        Assert.Same(feed, kept);
    }

    [Fact]
    public void S2505_a_feed_no_longer_wanted_goes_unless_a_watch_holds_it()
    {
        var table = new FeedTable<object>(_ => new object());
        var cpu = FeedKey.Create(SystemSource.Cpu);
        var gpu = FeedKey.Create(SystemSource.Gpu);
        table.Want([cpu, gpu], out _);
        table.Join(gpu, out _);

        var dropped = table.Want([], out var created);

        Assert.Empty(created);
        Assert.Single(dropped);
        Assert.False(table.TryGet(cpu, out _));
        Assert.True(table.TryGet(gpu, out _));
        Assert.False(table.IsWanted(gpu));

        Assert.NotNull(table.Leave(gpu, out _));
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void S2505_wanting_the_same_again_makes_and_drops_nothing()
    {
        var table = new FeedTable<object>(_ => new object());
        var keys = new[] { FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.Memory) };
        table.Want(keys, out var first);

        Assert.Equal(2, first.Count);
        Assert.Empty(table.Want(keys, out var again));
        Assert.Empty(again);
        Assert.Equal(2, table.Wanted);
    }

    [Fact]
    public void S2505_letting_go_twice_or_of_an_unknown_feed_changes_nothing()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu);

        Assert.Null(table.Leave(key, out var known));
        Assert.False(known);

        table.Want([key], out _);
        Assert.Null(table.Leave(key, out known));
        Assert.False(known);
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void S2505_clearing_hands_back_every_feed()
    {
        var table = new FeedTable<object>(_ => new object());
        table.Join(FeedKey.Create(SystemSource.Cpu), out _);
        table.Want([FeedKey.Create(SystemSource.Memory)], out _);

        Assert.Equal(2, table.Clear().Count);
        Assert.Equal(0, table.Count);
    }

    // ---- S-2513: interests from the whole state ------------------------------------------------

    private static AppState State(params WidgetConfig[][] views) => new()
    {
        Views =
        [
            .. views.Select((configs, v) => new ViewState
            {
                Id = $"v{v}",
                Name = $"View {v}",
                Widgets = [.. configs.Select((c, i) => new WidgetInstance { Id = $"w{v}-{i}", Kind = c.Kind, Config = c })],
            }),
        ],
    };

    [Fact]
    public void S2513_every_views_system_widgets_are_read_from_the_start()
    {
        var state = State(
            [new CpuConfig(), new ClockConfig()],
            [new MemoryConfig(), new TopProcessesConfig(), new PingDetailsConfig { Host = "Example.com" }]);

        var interests = SystemInterests.From(state, Root);

        Assert.Equal(
            [SystemSource.Cpu, SystemSource.Memory, SystemSource.Processes, SystemSource.Ping],
            interests.Feeds.Select(k => k.Source));
        Assert.Equal("example.com", interests.Feeds[^1].Target);

        // The charts' histories start too, the statistics' window with them.
        Assert.Equal(
            [SystemSource.Cpu, SystemSource.Memory, SystemSource.Ping],
            interests.Series.Select(s => s.Key.Source));
    }

    [Fact]
    public void S2513_two_widgets_on_one_feed_want_it_once_and_a_ping_meter_shares_the_statistics_prober()
    {
        var state = State(
            [new CpuConfig(), new CpuDetailsConfig(), new PingConfig { Host = "1.1.1.1" }],
            [new CpuConfig(), new PingDetailsConfig { Host = " 1.1.1.1 " }]);

        var interests = SystemInterests.From(state, Root);

        Assert.Equal(
            [SystemSource.Cpu, SystemSource.Performance, SystemSource.Processor, SystemSource.Ping],
            interests.Feeds.Select(k => k.Source));
        Assert.Equal(2, interests.Series.Count);
    }

    [Fact]
    public void S2513_a_disk_meter_charts_its_drive_the_system_drive_or_every_drive()
    {
        var state = State([
            new DiskConfig { Drive = "d" },
            new DiskConfig(),
            new DiskConfig { Scope = DiskScope.AllDrives },
        ]);

        var series = SystemInterests.From(state, Root).Series.Select(s => s.Instance).ToArray();

        Assert.Equal([SystemInterests.AllDrives, "C:\\", "D:\\"], series);
        Assert.Single(SystemInterests.From(state, Root).Feeds);
    }

    [Fact]
    public void S2513_the_gpu_tile_charts_its_one_adapter()
    {
        Assert.Equal(["2"], SystemInterests.For(new GpuConfig { Adapter = 2 }, Root).Series.Select(s => s.Instance));
        Assert.Equal(["0"], SystemInterests.For(new GpuConfig(), Root).Series.Select(s => s.Instance));
    }

    [Fact]
    public void S2513_the_public_address_is_asked_only_while_a_widget_wants_it()
    {
        Assert.DoesNotContain(FeedKey.Create(SystemSource.PublicAddress), SystemInterests.For(new NetworkDetailsConfig(), Root).Feeds);
        Assert.Contains(FeedKey.Create(SystemSource.PublicAddress), SystemInterests.For(new NetworkDetailsConfig { ShowPublicAddress = true }, Root).Feeds);
    }

    [Fact]
    public void S2513_every_system_kind_reads_something_and_no_other_kind_does()
    {
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var config = WidgetConfig.CreateDefault(kind);
            var wants = SystemInterests.For(config, Root);
            var system = config is GaugeConfig or DetailConfig;

            Assert.Equal(system, wants.Feeds.Count > 0);

            // A charted series' feed is read too.
            Assert.All(wants.Series, s => Assert.Contains(s.Key, wants.Feeds));
        }
    }

    [Fact]
    public void S2513_a_change_that_wants_the_same_is_no_change()
    {
        var state = State([new CpuConfig(), new MemoryDetailsConfig()]);
        var before = SystemInterests.From(state, Root);

        // Moving a widget or changing a span reads nothing new.
        ((CpuConfig)state.Views[0].Widgets[0].Config).GraphSpanSeconds = 3600;
        Assert.True(before.SameAs(SystemInterests.From(state, Root)));

        state.Views[0].Widgets.Add(new WidgetInstance { Id = "n", Kind = WidgetKind.NetworkDetails, Config = new NetworkDetailsConfig() });
        Assert.False(before.SameAs(SystemInterests.From(state, Root)));
    }

    [Fact]
    public void S2513_a_state_without_system_widgets_reads_nothing()
    {
        var interests = SystemInterests.From(State([new ClockConfig()]), Root);

        Assert.True(interests.SameAs(SystemInterests.None));
    }
}
