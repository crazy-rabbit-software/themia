using Themia.Core.Accounts;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2691] The one sync engine the host's syncs run on: its table, schedule, runs, tick and cancellation.</summary>
public sealed class SyncEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Start;
    private readonly List<(string Key, bool Asked)> _runs = [];
    private readonly Dictionary<string, Queue<Func<CancellationToken, Task<SyncRun>>>> _answers = new(StringComparer.Ordinal);
    private int _subscribed;
    private int _unsubscribed;

    private sealed class Item(string key, TimeSpan interval) : SyncEntry(key, interval)
    {
        public bool Signed { get; set; } = true;
    }

    private SyncEngine<Item> Engine(Func<Item, bool>? alsoDue = null)
        => new(new SyncEntries<Item>("test"), Fetch, () => _now, Subscribe, i => i.Signed) { AlsoDue = alsoDue };

    private Task<SyncRun> Fetch(Item entry, bool asked, CancellationToken cancellation)
    {
        _runs.Add((entry.Key, asked));
        return _answers.TryGetValue(entry.Key, out var queue) && queue.Count > 0 ? queue.Dequeue()(cancellation) : Task.FromResult(SyncRun.Ok);
    }

    private void Answer(string key, Func<CancellationToken, Task<SyncRun>> answer)
    {
        if (!_answers.TryGetValue(key, out var queue))
        {
            queue = new Queue<Func<CancellationToken, Task<SyncRun>>>();
            _answers[key] = queue;
        }

        queue.Enqueue(answer);
    }

    private IDisposable Subscribe(TimeSpan every, Action tick)
    {
        Assert.Equal(TimeSpan.FromMinutes(1), every);
        _subscribed++;
        return new Unsubscribe(() => _unsubscribed++);
    }

    private sealed class Unsubscribe(Action done) : IDisposable
    {
        public void Dispose() => done();
    }

    private static Item Make(string key) => new(key, TimeSpan.FromMinutes(5));

    [Fact]
    public void S2691_a_wanted_entry_runs_when_due_and_then_waits_its_interval()
    {
        using var engine = Engine();
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));

        Assert.Equal([("a", false)], _runs);
        var entry = engine.Entries.Find("a")!;
        Assert.Equal(FetchStatus.Ok, entry.Schedule.Status);
        Assert.Equal(Start + TimeSpan.FromMinutes(5), entry.Schedule.NextAttempt);

        _now = Start + TimeSpan.FromMinutes(4);
        engine.Poll();
        Assert.Single(_runs);

        _now = Start + TimeSpan.FromMinutes(5);
        engine.Poll();
        Assert.Equal(2, _runs.Count);
    }

    [Fact]
    public void S2691_a_failure_backs_off_never_before_the_providers_own_time()
    {
        using var engine = Engine();
        var until = Start + TimeSpan.FromHours(2);
        Answer("a", _ => Task.FromResult(new SyncRun(FetchStatus.RateLimited, "429", until)));
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));

        var entry = engine.Entries.Find("a")!;
        Assert.Equal(FetchStatus.RateLimited, entry.Schedule.Status);
        Assert.Equal(until, entry.Schedule.NextAttempt);
    }

    [Fact]
    public void S2691_a_success_that_spent_the_budget_waits_for_its_end()
    {
        using var engine = Engine();
        var until = Start + TimeSpan.FromMinutes(40);
        Answer("a", _ => Task.FromResult(new SyncRun(FetchStatus.Ok, RetryAt: until)));
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));

        Assert.Equal(until, engine.Entries.Find("a")!.Schedule.NextAttempt);
    }

    [Fact]
    public void S2691_an_account_failure_is_the_runs_outcome_and_a_bug_backs_off()
    {
        using var engine = Engine();
        Answer("a", _ => throw new AccountException(AccountError.Forbidden, "no grant"));
        Answer("b", _ => throw new InvalidOperationException("a mapper bug"));
        engine.Reconcile(() =>
        {
            engine.Entries.Want("a", () => Make("a"));
            engine.Entries.Want("b", () => Make("b"));
        });

        Assert.Equal(FetchStatus.Forbidden, engine.Entries.Find("a")!.Schedule.Status);
        Assert.Equal(FetchStatus.BadResponse, engine.Entries.Find("b")!.Schedule.Status);
        Assert.True(engine.Entries.Find("b")!.Schedule.NextAttempt > Start);
    }

    [Fact]
    public async Task S2691_a_reconcile_lets_go_of_what_no_widget_reads_but_a_run_out_lands_first()
    {
        using var engine = Engine();
        var slow = new TaskCompletionSource<SyncRun>();
        Answer("b", _ => slow.Task);
        engine.Reconcile(() =>
        {
            engine.Entries.Want("a", () => Make("a"));
            engine.Entries.Want("b", () => Make("b"));
        });

        var flight = engine.Entries.Find("b")!.Flight!;
        var gone = engine.Reconcile(() => { });

        Assert.Equal(["a"], gone.Select(e => e.Key));
        Assert.NotNull(engine.Entries.Find("b"));

        slow.SetResult(SyncRun.Ok);
        await flight;
        Assert.Equal(FetchStatus.Ok, engine.Entries.Find("b")!.Schedule.Status);
        Assert.Single(engine.Reconcile(() => { }));
    }

    [Fact]
    public void S2691_a_reconcile_that_keeps_its_entries_runs_only_the_wanted()
    {
        using var engine = Engine();
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")), drop: false);
        engine.Entries.Hold("held", () => Make("held"));

        _now = Start + TimeSpan.FromHours(1);
        engine.Reconcile(() => { }, drop: false);

        Assert.Equal(["a"], _runs.Select(r => r.Key));
        Assert.Equal(2, engine.Entries.Count);
    }

    [Fact]
    public void S2691_an_entry_its_account_cannot_use_is_not_asked()
    {
        using var engine = Engine();
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")).Signed = false);

        Assert.Empty(_runs);
    }

    [Fact]
    public void S2691_the_users_refresh_goes_now_and_is_marked_asked()
    {
        using var engine = Engine();
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));

        engine.Force([engine.Entries.Find("a")!]);

        Assert.Equal([("a", false), ("a", true)], _runs);
        Assert.False(engine.Entries.Find("a")!.Asked);
    }

    [Fact]
    public async Task S2691_a_refresh_while_a_run_is_out_runs_once_more_after_it()
    {
        using var engine = Engine();
        var slow = new TaskCompletionSource<SyncRun>();
        Answer("a", _ => slow.Task);
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));
        var entry = engine.Entries.Find("a")!;
        var flight = entry.Flight!;

        engine.Force([entry]);
        engine.Force([entry]);
        Assert.Single(_runs);

        slow.SetResult(SyncRun.Ok);
        await flight;

        Assert.Equal([("a", false), ("a", true)], _runs);
    }

    [Fact]
    public void S2691_a_run_is_single_flight()
    {
        using var engine = Engine();
        var slow = new TaskCompletionSource<SyncRun>();
        Answer("a", _ => slow.Task);
        var entry = engine.Entries.Hold("a", () => Make("a"));

        var first = engine.RunAsync(entry);
        var second = engine.RunAsync(entry);

        Assert.Same(first, second);
        Assert.Single(_runs);
        slow.SetResult(SyncRun.Ok);
    }

    [Fact]
    public async Task S2691_dispose_cancels_the_run_in_flight_and_drops_its_outcome()
    {
        var engine = Engine();
        var heard = new TaskCompletionSource<bool>();
        Answer("a", async cancellation =>
        {
            using var registration = cancellation.Register(() => heard.TrySetResult(true));
            await Task.Delay(Timeout.Infinite, cancellation);
            return SyncRun.Ok;
        });
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));
        var entry = engine.Entries.Find("a")!;
        var flight = entry.Flight!;
        var raised = 0;
        engine.Changed += _ => raised++;

        engine.Dispose();
        await flight;

        Assert.True(await heard.Task);
        Assert.True(engine.Cancellation.IsCancellationRequested);
        Assert.Equal(FetchStatus.Loading, entry.Schedule.Status);
        Assert.False(entry.Schedule.Busy);
        Assert.Equal(0, raised);
        Assert.Equal(1, _unsubscribed);
    }

    [Fact]
    public void S2691_the_tick_runs_while_the_table_holds_anything()
    {
        using var engine = Engine();
        engine.Reconcile(() => { });
        Assert.Equal(0, _subscribed);

        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));
        Assert.Equal(1, _subscribed);

        engine.Reconcile(() => { });
        Assert.Equal(1, _unsubscribed);
    }

    [Fact]
    public void S2609_the_network_back_ends_only_the_waits_about_the_network()
    {
        using var engine = Engine();
        Answer("net", _ => Task.FromResult(new SyncRun(FetchStatus.Offline)));
        Answer("slow", _ => Task.FromResult(new SyncRun(FetchStatus.RateLimited)));
        engine.Reconcile(() =>
        {
            engine.Entries.Want("net", () => Make("net"));
            engine.Entries.Want("slow", () => Make("slow"));
        });

        engine.Wake();

        Assert.Equal(["net", "slow", "net"], _runs.Select(r => r.Key));
    }

    [Fact]
    public void S2691_more_wanted_than_held_runs_whatever_the_schedule_says()
    {
        var more = false;
        using var engine = Engine(alsoDue: e => more);
        var polled = 0;
        engine.Polled += () => polled++;
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));

        more = true;
        engine.Poll();

        Assert.Equal(2, _runs.Count);
        Assert.Equal(2, polled);
    }

    [Fact]
    public async Task S2691_an_entry_let_go_mid_run_is_abandoned_without_an_outcome()
    {
        using var engine = Engine();
        using var gone = new CancellationTokenSource();
        Answer("a", async _ =>
        {
            await Task.Delay(Timeout.Infinite, gone.Token);
            return SyncRun.Ok;
        });
        engine.Reconcile(() => engine.Entries.Want("a", () => Make("a")));
        var entry = engine.Entries.Find("a")!;
        var flight = entry.Flight!;

        engine.Entries.Remove("a");
        await gone.CancelAsync();
        await flight;

        Assert.Equal(FetchStatus.Loading, entry.Schedule.Status);
        Assert.False(entry.Schedule.Busy);
    }
}
