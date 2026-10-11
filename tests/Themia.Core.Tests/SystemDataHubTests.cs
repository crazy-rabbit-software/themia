using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The system data hub's rules (S-2500..S-2511): keys, cadence, history, holding and linger.</summary>
public sealed class SystemDataHubTests
{
    private static readonly long LingerMs = (long)FeedTable<object>.Linger.TotalMilliseconds;

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
        table.Join(FeedKey.Create(SystemSource.Processor, TimeSpan.FromSeconds(2)), out _);
        table.Join(FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "b.example"), out _);
        table.Join(FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(2)), out _);
        table.Join(FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "a.example"), out _);

        var order = table.Ordered().Select(e => e.Key.ToString()).ToArray();

        Assert.Equal(["Cpu@2s", "Processor@2s", "Ping(a.example)@10s", "Ping(b.example)@10s"], order);
    }

    // ---- S-2502: feed keys ---------------------------------------------------------------------

    [Fact]
    public void S2502_one_host_however_it_is_typed_is_one_prober()
    {
        var a = FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), " Example.COM ");
        var b = FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "example.com");

        Assert.Equal(a, b);
        Assert.Equal("example.com", a.Target);
    }

    [Fact]
    public void S2502_another_cadence_or_host_is_another_feed()
    {
        var ten = FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "example.com");

        Assert.NotEqual(ten, FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(5), "example.com"));
        Assert.NotEqual(ten, FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "example.org"));
        Assert.NotEqual(ten, FeedKey.Create(SystemSource.Network, TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(1.4, 1)]
    [InlineData(2.5, 3)]
    [InlineData(120, 120)]
    [InlineData(300, 300)]
    public void S2502_a_cadence_is_whole_seconds_at_least_one_and_never_capped_at_a_minute(double seconds, int expected)
        => Assert.Equal(expected, HubCadence.Seconds(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void S2502_a_machine_source_has_no_target()
    {
        Assert.Equal(string.Empty, FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1), "  ").Target);
        Assert.Equal(string.Empty, FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1)).Target);
    }

    // ---- S-2503: cadence arbitration -----------------------------------------------------------

    [Theory]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 2, 3 }, 1)]
    [InlineData(new[] { 2, 4, 6 }, 2)]
    [InlineData(new[] { 5, 15, 10 }, 5)]
    [InlineData(new[] { 10, 10 }, 10)]
    [InlineData(new[] { 60 }, 60)]
    public void S2503_the_hub_wakes_at_the_slowest_rate_that_lands_on_every_boundary(int[] intervals, int expected)
        => Assert.Equal(TimeSpan.FromSeconds(expected), HubCadence.Arbitrate(intervals));

    [Theory]
    [InlineData(new[] { 90 }, 30)]
    [InlineData(new[] { 120 }, 60)]
    [InlineData(new[] { 300, 10 }, 10)]
    [InlineData(new[] { 75 }, 15)]
    public void S2503_a_cadence_over_a_minute_still_lands_on_its_boundaries_with_a_ticker_capped_at_one(int[] intervals, int expected)
        => Assert.Equal(TimeSpan.FromSeconds(expected), HubCadence.Arbitrate(intervals));

    [Fact]
    public void S2503_nothing_to_read_is_no_cadence()
        => Assert.Null(HubCadence.Arbitrate([]));

    [Fact]
    public void S2503_a_feed_is_due_once_per_period_of_its_own_cadence()
    {
        var start = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var last = long.MinValue;
        var reads = new List<int>();

        // The hub wakes every second (a 1 s feed elsewhere); a 3 s feed reads at 0, 3, 6, 9.
        for (var s = 0; s < 10; s++)
        {
            if (HubCadence.IsDue(last, start.AddSeconds(s).AddMilliseconds(15), 3, out var period))
            {
                last = period;
                reads.Add(s);
            }
        }

        Assert.Equal([0, 3, 6, 9], reads);
    }

    [Fact]
    public void S2503_a_feed_read_this_period_is_not_due_again_in_it()
    {
        var now = new DateTimeOffset(2026, 10, 11, 12, 0, 5, TimeSpan.Zero);
        Assert.True(HubCadence.IsDue(long.MinValue, now, 5, out var period));
        Assert.False(HubCadence.IsDue(period, now.AddMilliseconds(400), 5, out _));
        Assert.True(HubCadence.IsDue(period, now.AddSeconds(5), 5, out _));
    }

    [Fact]
    public void S2503_the_feed_table_arbitrates_over_held_and_lingering_feeds()
    {
        var table = new FeedTable<object>(_ => new object());
        var two = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(2));
        var three = FeedKey.Create(SystemSource.Memory, TimeSpan.FromSeconds(3));

        Assert.Null(table.Cadence);

        table.Join(two, out _);
        Assert.Equal(TimeSpan.FromSeconds(2), table.Cadence);

        table.Join(three, out _);
        Assert.Equal(TimeSpan.FromSeconds(1), table.Cadence);

        // Lingering is still read, so it still counts.
        table.Leave(three, 0);
        Assert.Equal(TimeSpan.FromSeconds(1), table.Cadence);

        table.Expire(LingerMs);
        Assert.Equal(TimeSpan.FromSeconds(2), table.Cadence);
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
        Assert.Null(ring.Latest);
        Assert.Empty(ring.Newest(2));

        ring.Add(9);
        Assert.Equal([9], ring.Newest(2));
    }

    [Fact]
    public void S2504_the_history_holds_the_longest_chart_and_the_widest_ping_window()
    {
        Assert.Equal(MeterTiming.MaxCapacity, SeriesHistory.Capacity);
        Assert.True(SeriesHistory.Capacity >= Model.PingDetailsConfig.MaxWindow);
        Assert.True(SeriesHistory.Capacity >= MeterTiming.Capacity(3600, 1));
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

    // ---- S-2505: holding and linger ------------------------------------------------------------

    [Fact]
    public void S2505_every_widget_on_one_key_shares_one_feed()
    {
        var made = 0;
        var table = new FeedTable<object>(_ => { made++; return new object(); });
        var key = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1));

        var first = table.Join(key, out var created);
        Assert.True(created);

        var second = table.Join(key, out created);
        Assert.False(created);
        Assert.Same(first, second);
        Assert.Equal(1, made);
        Assert.Equal(2, table.Holders(key));
    }

    [Fact]
    public void S2505_a_feed_nobody_holds_lingers_and_a_returning_widget_gets_the_same_one()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1));
        var feed = table.Join(key, out _);

        Assert.True(table.Leave(key, 1_000));
        Assert.Equal(1, table.Lingering);
        Assert.Empty(table.Expire(1_000 + LingerMs - 1));

        // A view switch and back within the linger: the history is the same feed's.
        Assert.Same(feed, table.Join(key, out var created));
        Assert.False(created);
        Assert.Equal(0, table.Lingering);

        // Held again, the old linger no longer counts.
        Assert.Empty(table.Expire(1_000 + (10 * LingerMs)));
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void S2505_a_feed_whose_linger_ran_out_is_dropped_and_comes_back_fresh()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Gpu, TimeSpan.FromSeconds(2));
        var feed = table.Join(key, out _);
        table.Leave(key, 5_000);

        var expired = table.Expire(5_000 + LingerMs);

        Assert.Single(expired);
        Assert.Same(feed, expired[0].Value);
        Assert.Equal(0, table.Count);
        Assert.False(table.Reads(SystemSource.Gpu, string.Empty));

        Assert.NotSame(feed, table.Join(key, out var created));
        Assert.True(created);
    }

    [Fact]
    public void S2505_a_feed_still_held_by_one_widget_never_expires()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1));
        table.Join(key, out _);
        table.Join(key, out _);
        table.Leave(key, 0);

        Assert.Empty(table.Expire(100 * LingerMs));
        Assert.Equal(1, table.Holders(key));
    }

    [Fact]
    public void S2505_letting_go_twice_or_of_an_unknown_feed_changes_nothing()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1));

        Assert.False(table.Leave(key, 0));

        table.Join(key, out _);
        Assert.True(table.Leave(key, 0));
        Assert.False(table.Leave(key, LingerMs / 2));

        // The second release did not restart the linger.
        Assert.Single(table.Expire(LingerMs));
    }

    [Fact]
    public void S2505_a_clock_that_reads_before_the_release_does_not_end_the_linger()
    {
        var table = new FeedTable<object>(_ => new object());
        var key = FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1));
        table.Join(key, out _);
        table.Leave(key, 10_000);

        Assert.Empty(table.Expire(0));
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void S2505_a_source_is_read_while_any_feed_of_it_lives()
    {
        var table = new FeedTable<object>(_ => new object());
        var a = FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(10), "example.com");
        var b = FeedKey.Create(SystemSource.Ping, TimeSpan.FromSeconds(30), "example.com");
        table.Join(a, out _);
        table.Join(b, out _);
        table.Leave(a, 0);
        table.Expire(LingerMs);

        Assert.True(table.Reads(SystemSource.Ping, "example.com"));
        Assert.False(table.Reads(SystemSource.Ping, "example.org"));

        table.Leave(b, 0);
        table.Expire(LingerMs);
        Assert.False(table.Reads(SystemSource.Ping, "example.com"));
    }

    [Fact]
    public void S2505_clearing_hands_back_every_feed()
    {
        var table = new FeedTable<object>(_ => new object());
        table.Join(FeedKey.Create(SystemSource.Cpu, TimeSpan.FromSeconds(1)), out _);
        table.Join(FeedKey.Create(SystemSource.Memory, TimeSpan.FromSeconds(1)), out _);

        Assert.Equal(2, table.Clear().Count);
        Assert.Equal(0, table.Count);
        Assert.Null(table.Cadence);
    }
}
