using Themia.Core.Accounts;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2610] [S-2692] One owner's write-back queue sent to its provider, the drain mail and tasks share.</summary>
public sealed class WriteDrainTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private readonly DateTimeOffset _now = Start;
    private readonly List<string> _sent = [];
    private readonly List<(string Id, WriteResult Result)> _settled = [];
    private readonly Dictionary<string, Func<CancellationToken, Task<WriteSent<string>>>> _answers = new(StringComparer.Ordinal);
    private bool _usable = true;

    private WriteDrain<string> Drain(CancellationToken cancellation = default)
    {
        var drain = new WriteDrain<string>("test", "acct", Send, () => _now, () => _usable, cancellation);
        drain.Settled += (write, sent) => _settled.Add((write.Id, sent.Result));
        return drain;
    }

    private Task<WriteSent<string>> Send(PendingWrite write, CancellationToken cancellation)
    {
        _sent.Add(write.Id);
        return _answers.TryGetValue(write.Id, out var answer) ? answer(cancellation) : Task.FromResult(WriteSent<string>.Done("ok-" + write.Id));
    }

    private static PendingWrite Write(string id, string target, PendingKind kind = PendingKind.TaskComplete)
        => new(id, kind, target, "list", null, null, Start);

    [Fact]
    public async Task S2692_sends_head_first_in_order_until_the_queue_is_empty()
    {
        var drain = Drain();
        drain.Add(Write("1", "t1"));
        drain.Add(Write("2", "t2"));

        await drain.DrainAsync();

        Assert.Equal(["1", "2"], _sent);
        Assert.Equal([("1", WriteResult.Done), ("2", WriteResult.Done)], _settled);
        Assert.Empty(drain.Pending);
    }

    [Fact]
    public async Task S2692_a_failure_time_can_mend_keeps_the_write_and_stops_the_drain()
    {
        var drain = Drain();
        _answers["1"] = _ => throw new AccountException(AccountError.Offline, "no network");
        drain.Add(Write("1", "t1"));
        drain.Add(Write("2", "t2"));

        await drain.DrainAsync();

        Assert.Equal(["1"], _sent);
        Assert.Equal(2, drain.Pending.Count);
        Assert.Equal(1, drain.Pending[0].Attempts);
        Assert.Equal(2, drain.Waiting);
        Assert.Equal([("1", WriteResult.Retry)], _settled);
    }

    [Fact]
    public async Task S2692_a_refusal_is_taken_back_and_the_rest_goes_on()
    {
        var drain = Drain();
        _answers["1"] = _ => throw new AccountException(AccountError.NotFound, "gone");
        drain.Add(Write("1", "t1"));
        drain.Add(Write("2", "t2"));

        await drain.DrainAsync();

        Assert.Equal([("1", WriteResult.Refused), ("2", WriteResult.Done)], _settled);
        Assert.Empty(drain.Pending);
    }

    [Fact]
    public async Task S2692_an_unexpected_failure_is_kept_not_dropped()
    {
        var drain = Drain();
        _answers["1"] = _ => throw new InvalidOperationException("a provider oddity");
        drain.Add(Write("1", "t1"));

        await drain.DrainAsync();

        Assert.Single(drain.Pending);
        Assert.Equal([("1", WriteResult.Retry)], _settled);
    }

    [Fact]
    public async Task S2624_the_heads_first_attempt_answers_whoever_queued_it()
    {
        var drain = Drain();
        drain.Add(Write("1", "t1"));

        var sent = await drain.SendNowAsync("1");

        Assert.Equal(WriteResult.Done, sent?.Result);
        Assert.Equal("ok-1", sent?.Value);
    }

    [Fact]
    public async Task S2624_a_change_queued_behind_another_answers_at_once()
    {
        var drain = Drain();
        var slow = new TaskCompletionSource<WriteSent<string>>();
        _answers["1"] = _ => slow.Task;
        drain.Add(Write("1", "t1"));
        var head = drain.SendNowAsync("1");
        drain.Add(Write("2", "t2"));

        Assert.Null(await drain.SendNowAsync("2"));

        slow.SetResult(WriteSent<string>.Done());
        Assert.Equal(WriteResult.Done, (await head)?.Result);
        Assert.Equal(["1", "2"], _sent);
    }

    [Fact]
    public async Task S2692_an_owner_that_cannot_send_sends_nothing_and_answers_at_once()
    {
        var drain = Drain();
        _usable = false;
        drain.Add(Write("1", "t1"));

        Assert.Null(await drain.SendNowAsync("1"));
        Assert.Empty(_sent);
        Assert.Single(drain.Pending);
    }

    [Fact]
    public async Task S2692_a_cancelled_owner_stops_and_keeps_its_queue()
    {
        using var cancel = new CancellationTokenSource();
        var drain = Drain(cancel.Token);
        _answers["1"] = async cancellation =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return WriteSent<string>.Done();
        };
        drain.Add(Write("1", "t1"));
        drain.Add(Write("2", "t2"));

        var running = drain.DrainAsync();
        await cancel.CancelAsync();
        await running;

        Assert.Equal(["1"], _sent);
        Assert.Equal(2, drain.Pending.Count);
        Assert.Empty(_settled);
    }

    [Fact]
    public void S2611_a_change_folds_into_what_waits_for_the_same_thing()
    {
        var drain = Drain();
        drain.Add(Write("1", "t1", PendingKind.TaskComplete));

        var added = drain.Add(Write("2", "t1", PendingKind.TaskReopen));

        Assert.Equal(["1"], added!.Value.Folded.Select(w => w.Id));
        Assert.True(added.Value.Kept);
        Assert.Equal(["2"], drain.Pending.Select(w => w.Id));
    }

    [Fact]
    public async Task S2611_a_confirmed_create_readdresses_the_writes_after_it()
    {
        var drain = Drain();
        drain.Settled += (write, sent) =>
        {
            if (write.Kind == PendingKind.TaskCreate && sent.Value is { } real)
            {
                drain.Retarget(write.Target, real);
            }
        };
        _answers["2"] = _ => Task.FromException<WriteSent<string>>(new AccountException(AccountError.Offline, "no network"));
        drain.Add(Write("1", "temp", PendingKind.TaskCreate));
        drain.Add(Write("2", "temp", PendingKind.TaskRename));

        await drain.DrainAsync();

        Assert.Equal("ok-1", Assert.Single(drain.Pending).Target);
    }
}
