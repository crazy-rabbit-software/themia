using Themia.Core.Accounts;
using Themia.Core.Diagnostics;

namespace Themia.Core.Sync;

/// <summary>What one send of a queued write came to, and what the provider gave back (a created task).</summary>
public readonly record struct WriteSent<TResult>(WriteResult Result, string? Problem = null, TResult? Value = default)
{
    public static WriteSent<TResult> Done(TResult? value = default) => new(WriteResult.Done, null, value);
}

/// <summary>
/// [S-2610] [S-2692] One owner's write-back queue sent to its provider, written once for mail and tasks: the queue
/// (<see cref="WriteQueue"/>'s rules, persisted by the owner), one send at a time, head first, until it is empty or the
/// head has to wait; a typed failure kept (<see cref="WriteResult.Retry"/>) or taken back
/// (<see cref="WriteResult.Refused"/>) by <see cref="WriteQueue.Classify"/>, anything else kept and logged; the first
/// attempt of a change at the head answers whoever queued it. The owner hears every settled write
/// (<see cref="Settled"/>: it shows a confirmed change, takes back a refused one, saves its shelf and tells its
/// widgets) and holds the cancellation that stops it.
/// <para>One thread: the owner's; each send resumes there.</para>
/// </summary>
public sealed class WriteDrain<TResult>
{
    /// <summary>Send one write to the provider.</summary>
    public delegate Task<WriteSent<TResult>> Sender(PendingWrite write, CancellationToken cancellation);

    private readonly string _scope;
    private readonly string _subject;
    private readonly Sender _send;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<bool> _usable;
    private readonly CancellationToken _cancellation;
    private readonly Dictionary<string, TaskCompletionSource<WriteSent<TResult>?>> _waiting = new(StringComparer.Ordinal);
    private List<PendingWrite> _pending = [];

    /// <param name="scope">The log's scope.</param>
    /// <param name="subject">What the log calls the owner (an account id); never a token.</param>
    /// <param name="send">One send; a typed account failure it throws is classified, anything else is kept and logged.</param>
    /// <param name="clock">Now, in UTC.</param>
    /// <param name="usable">Whether the owner may send now (its account signed in).</param>
    /// <param name="cancellation">The owner's: a send in flight hears it, and the drain stops.</param>
    public WriteDrain(string scope, string subject, Sender send, Func<DateTimeOffset> clock, Func<bool> usable, CancellationToken cancellation)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _subject = subject ?? throw new ArgumentNullException(nameof(subject));
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _usable = usable ?? throw new ArgumentNullException(nameof(usable));
        _cancellation = cancellation;
    }

    /// <summary>A write was settled: the attempt's outcome, the queue already moved on.</summary>
    public event Action<PendingWrite, WriteSent<TResult>>? Settled;

    /// <summary>The queue, head first, as the owner persists it.</summary>
    public IReadOnlyList<PendingWrite> Pending => _pending;

    /// <summary>A send is out.</summary>
    public bool Writing { get; private set; }

    /// <summary>[S-2612] How many changes wait, once the head has had to wait; a change on its way out counts nothing.</summary>
    public int Waiting => _pending.Any(w => w.Attempts > 0) ? _pending.Count : 0;

    /// <summary>The queue the owner's shelf held at start ([S-2653]: shown again and resent).</summary>
    public void Restore(IEnumerable<PendingWrite> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        _pending = [.. pending];
    }

    /// <summary>
    /// [S-2611] Queue a change, folded into what waits for the same thing (never the head while it is out). Null when the
    /// queue is full; else the writes it folded away (their optimistic edits go) and whether the new one stayed.
    /// </summary>
    public (IReadOnlyList<PendingWrite> Folded, bool Kept)? Add(PendingWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (WriteQueue.Add(_pending, write, headInFlight: Writing) is not { } next)
        {
            return null;
        }

        var kept = new HashSet<string>(next.Select(w => w.Id), StringComparer.Ordinal);
        var folded = _pending.Where(w => !kept.Contains(w.Id)).ToList();
        _pending = next;
        return (folded, kept.Contains(write.Id));
    }

    /// <summary>A create's temporary id was replaced by the provider's: the writes after it address the real one.</summary>
    public void Retarget(string temporaryId, string realId) => _pending = WriteQueue.Retarget(_pending, temporaryId, realId);

    /// <summary>The network came back, or the user asked: every wait ends now.</summary>
    public void Wake() => _pending = WriteQueue.Wake(_pending);

    /// <summary>
    /// [S-2624] Send <paramref name="id"/> now when it is the head and nothing is out, and answer with its first
    /// attempt; else (queued behind another, or one is out) start the drain and answer null at once: it shows already,
    /// and its outcome reaches the header.
    /// </summary>
    public async Task<WriteSent<TResult>?> SendNowAsync(string id)
    {
        if (_pending.Count == 0 || _pending[0].Id != id || Writing)
        {
            _ = DrainAsync();
            return null;
        }

        var answer = new TaskCompletionSource<WriteSent<TResult>?>();
        _waiting[id] = answer;
        _ = DrainAsync();
        return await answer.Task.ConfigureAwait(true);
    }

    /// <summary>Send what is ready, head first, until the queue is empty or the head has to wait. Never throws.</summary>
    public async Task DrainAsync()
    {
        if (Writing)
        {
            return;
        }

        if (!_usable() || _cancellation.IsCancellationRequested)
        {
            // Nothing goes out now: whoever waits hears that their change is queued.
            AnswerWaiting();
            return;
        }

        Writing = true;
        try
        {
            while (!_cancellation.IsCancellationRequested && WriteQueue.Ready(_pending, _clock()) is { } write)
            {
                var sent = await SendAsync(write).ConfigureAwait(true);
                if (_cancellation.IsCancellationRequested)
                {
                    break;
                }

                _pending = WriteQueue.Settle(_pending, write.Id, sent.Result, _clock(), sent.Problem);
                Raise(write, sent);

                if (_waiting.Remove(write.Id, out var answer))
                {
                    answer.TrySetResult(sent);
                }

                if (sent.Result == WriteResult.Retry)
                {
                    break;
                }
            }
        }
        finally
        {
            Writing = false;

            // Anyone still waiting is behind a write that waits: their change is queued.
            AnswerWaiting();
        }
    }

    private async Task<WriteSent<TResult>> SendAsync(PendingWrite write)
    {
        try
        {
            var sent = await _send(write, _cancellation).ConfigureAwait(true);
            if (sent.Result == WriteResult.Done)
            {
                Log.Info(_scope, $"{write.Kind} done for {_subject}");
            }

            return sent;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            return new WriteSent<TResult>(WriteResult.Retry);
        }
        catch (AccountException ex)
        {
            var result = WriteQueue.Classify(ex.Error);
            Log.Warn(_scope, $"{write.Kind} for {_subject} {(result == WriteResult.Retry ? "is kept and will retry" : "was refused")}: {ex.Message}");
            return new WriteSent<TResult>(result, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Not typed by the account layer: a bug or a provider oddity. Kept, not dropped: the user's change is
            // retried on the queue's back-off and the log says what happened.
            Log.Error(_scope, $"{write.Kind} for {_subject} failed unexpectedly", ex);
            return new WriteSent<TResult>(WriteResult.Retry, ex.Message);
        }
    }

    private void Raise(PendingWrite write, WriteSent<TResult> sent)
    {
        try
        {
            Settled?.Invoke(write, sent);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The owner's bookkeeping must not stop the queue.
            Log.Error(_scope, "a settled write's handler threw", ex);
        }
    }

    private void AnswerWaiting()
    {
        foreach (var answer in _waiting.Values)
        {
            answer.TrySetResult(null);
        }

        _waiting.Clear();
    }
}
