using System.Text.Json.Serialization;
using Themia.Core.Accounts;

namespace Themia.Core.Sync;

/// <summary>[S-2610] A change the user made that is waiting to reach the provider. A closed set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PendingKind>))]
public enum PendingKind
{
    MailMarkRead,
    MailMarkUnread,
    MailArchive,
    MailJunk,
    MailDelete,
    TaskComplete,
    TaskReopen,
    TaskRename,
    TaskDelete,
    TaskCreate,
}

/// <summary>
/// [S-2610] One queued write, as the shelf keeps it so it survives a restart.
/// </summary>
/// <param name="Id">Unique within the owner's queue.</param>
/// <param name="Target">What it acts on: a message's raw id, a task's id (a temporary one for a create).</param>
/// <param name="Container">Where that lives: the mail folder's wire name, the task's list id.</param>
/// <param name="Text">A rename's or a create's title.</param>
/// <param name="Due">A create's due day, or null.</param>
/// <param name="Attempts">Tries that failed in a way time can mend.</param>
/// <param name="NextAttempt">Not before; null means now.</param>
/// <param name="LastError">The last transient failure, for the log and the header; never a secret.</param>
public sealed record PendingWrite(
    string Id,
    PendingKind Kind,
    string Target,
    string Container,
    string? Text,
    DateOnly? Due,
    DateTimeOffset Queued,
    int Attempts = 0,
    DateTimeOffset? NextAttempt = null,
    string? LastError = null);

/// <summary>What became of one attempt.</summary>
public enum WriteResult
{
    Done,

    /// <summary>Offline, unreachable, throttled, the server's trouble, or a sign-in to mend: kept and retried.</summary>
    Retry,

    /// <summary>The provider said no (gone, forbidden, malformed): taken back and said so.</summary>
    Refused,
}

/// <summary>
/// [S-2610] The write-back queue's rules, one owner at a time. Strictly in order: only the head goes
/// out, so "complete, then reopen" can never land reopened-first, and a head that waits holds the
/// rest behind it. Pure; the service owns the queue and persists it in the owner's shelf.
/// </summary>
public static class WriteQueue
{
    /// <summary>A queue longer than this refuses new changes rather than grow without bound.</summary>
    public const int MaxPending = 500;

    /// <summary>The longest a waiting write waits between tries.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(30);

    /// <summary>Which provider failures time (or the user signing in again) can mend.</summary>
    public static WriteResult Classify(AccountError error) => error switch
    {
        AccountError.Offline or AccountError.Unreachable or AccountError.TimedOut
            or AccountError.RateLimited or AccountError.ServerError or AccountError.SignInRequired => WriteResult.Retry,
        _ => WriteResult.Refused,
    };

    /// <summary>The write to send now, or null: the head, once its wait is over.</summary>
    public static PendingWrite? Ready(IReadOnlyList<PendingWrite> queue, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (queue.Count == 0)
        {
            return null;
        }

        var head = queue[0];
        return head.NextAttempt is { } at && at > now ? null : head;
    }

    /// <summary>
    /// [S-2611] Add a change. Folds it into an earlier one still waiting for the same thing (a second
    /// tick replaces the first, a rename replaces a rename, a delete swallows what was queued for the
    /// task) so a queue built offline sends what the user ended up with, not every step. The head is
    /// never folded away while <paramref name="headInFlight"/>: it may already be on the wire.
    /// Returns null when the queue is full.
    /// </summary>
    public static List<PendingWrite>? Add(IReadOnlyList<PendingWrite> queue, PendingWrite write, bool headInFlight)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(write);

        var next = new List<PendingWrite>(queue.Count + 1);

        // A task created and deleted while offline never existed as far as the provider is concerned:
        // both go, and so does anything queued for it in between.
        var createAt = queue.ToList().FindIndex(w => w.Kind == PendingKind.TaskCreate && w.Target == write.Target);
        if (write.Kind == PendingKind.TaskDelete && createAt >= 0 && !(createAt == 0 && headInFlight))
        {
            next.AddRange(queue.Where(w => w.Target != write.Target));
            return next;
        }

        for (var i = 0; i < queue.Count; i++)
        {
            var queued = queue[i];
            var protectedHead = i == 0 && headInFlight;

            if (!protectedHead && queued.Target == write.Target && Supersedes(write.Kind, queued.Kind))
            {
                continue;
            }

            next.Add(queued);
        }

        if (next.Count >= MaxPending)
        {
            return null;
        }

        next.Add(write);
        return next;
    }

    /// <summary>The head's attempt ended. Returns the queue that follows.</summary>
    public static List<PendingWrite> Settle(IReadOnlyList<PendingWrite> queue, string id, WriteResult result, DateTimeOffset now, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var next = queue.ToList();
        var index = next.FindIndex(w => w.Id == id);
        if (index < 0)
        {
            return next;
        }

        if (result == WriteResult.Retry)
        {
            var attempts = next[index].Attempts + 1;
            next[index] = next[index] with { Attempts = attempts, NextAttempt = now + Delay(attempts), LastError = error };
        }
        else
        {
            next.RemoveAt(index);
        }

        return next;
    }

    /// <summary>
    /// The network came back, or the user asked: every wait ends now. The attempt count stays, so a
    /// provider that is still down backs off again from where it was.
    /// </summary>
    public static List<PendingWrite> Wake(IReadOnlyList<PendingWrite> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return [.. queue.Select(w => w with { NextAttempt = null })];
    }

    /// <summary>30 s, 1, 2, 4 … minutes, never more than <see cref="MaxDelay"/>.</summary>
    public static TimeSpan Delay(int attempts)
    {
        var seconds = 30.0 * Math.Pow(2, Math.Clamp(attempts - 1, 0, 16));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxDelay.TotalSeconds));
    }

    /// <summary>A create whose temporary id was replaced by the provider's: later writes now address the real one.</summary>
    public static List<PendingWrite> Retarget(IReadOnlyList<PendingWrite> queue, string temporaryId, string realId)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return [.. queue.Select(w => w.Target == temporaryId ? w with { Target = realId } : w)];
    }

    /// <summary>
    /// Validation at load: entries without an id or a target are dropped, repeated ids keep the
    /// first, an attempt count is never negative, the queue is cut to <see cref="MaxPending"/>.
    /// </summary>
    public static List<PendingWrite> Validate(IEnumerable<PendingWrite?>? stored, ref int dropped)
    {
        var result = new List<PendingWrite>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var write in stored ?? [])
        {
            if (write is null || string.IsNullOrEmpty(write.Id) || string.IsNullOrEmpty(write.Target) || write.Container is null
                || !Enum.IsDefined(write.Kind) || !seen.Add(write.Id) || result.Count >= MaxPending)
            {
                dropped++;
                continue;
            }

            result.Add(write with { Attempts = Math.Max(0, write.Attempts) });
        }

        return result;
    }

    private static bool Supersedes(PendingKind incoming, PendingKind queued) => incoming switch
    {
        PendingKind.MailMarkRead or PendingKind.MailMarkUnread => queued is PendingKind.MailMarkRead or PendingKind.MailMarkUnread,
        PendingKind.TaskComplete or PendingKind.TaskReopen => queued is PendingKind.TaskComplete or PendingKind.TaskReopen,
        PendingKind.TaskRename => queued is PendingKind.TaskRename,
        PendingKind.TaskDelete => queued is PendingKind.TaskComplete or PendingKind.TaskReopen or PendingKind.TaskRename,
        _ => false,
    };
}
