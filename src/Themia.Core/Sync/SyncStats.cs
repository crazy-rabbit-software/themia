using Themia.Core.Accounts;
using Themia.Core.Localization;

namespace Themia.Core.Sync;

/// <summary>
/// [S-3820] What one shelf holds, in numbers: for the Accounts page, never for a sync decision.
/// </summary>
/// <param name="Items">Messages, events, people or tasks stored on this PC.</param>
/// <param name="Containers">Folders, calendars or task lists those are in; 0 where the kind has none (contacts).</param>
/// <param name="LastSync">When the newest successful read of any part landed; null when none has yet.</param>
/// <param name="Pending">Changes made here and still waiting to reach the provider.</param>
public sealed record ShelfStats(SyncKind Kind, int Items, int Containers, DateTimeOffset? LastSync, int Pending);

/// <summary>[S-3821] One kind on an account's group: its numbers, or null when nothing of it is stored yet.</summary>
public sealed record KindSummary(SyncKind Kind, ShelfStats? Stats);

/// <summary>
/// [S-3820]..[S-3822] Counting the shelves. Pure: the services hand in the shelf they just loaded or
/// saved, so the counts come from the very document on disk, and nothing here opens a file.
/// </summary>
public static class SyncStats
{
    /// <summary>Messages over every stored folder; the newest folder read; the queued changes.</summary>
    public static ShelfStats Of(MailShelf shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        return new ShelfStats(
            SyncKind.Mail,
            shelf.Folders.Sum(f => f.Messages.Count),
            shelf.Folders.Count,
            Latest(shelf.Folders.Select(f => (DateTimeOffset?)f.At)),
            shelf.Pending.Count);
    }

    /// <summary>
    /// Events over every stored calendar window (a birthday is a person's, not an event); the
    /// calendars the account lists, or the windows held when the list was never read.
    /// </summary>
    public static ShelfStats Of(CalendarShelf shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        return new ShelfStats(
            SyncKind.Calendar,
            shelf.Spans.Sum(s => s.Events.Count),
            shelf.Calendars?.Count ?? shelf.Spans.Count,
            Latest(shelf.Spans.Select(s => (DateTimeOffset?)s.NearAt).Append(shelf.CalendarsAt).Append(shelf.BirthdaysAt)),
            0);
    }

    /// <summary>The people in the address book; contacts have no containers and no queue.</summary>
    public static ShelfStats Of(ContactsShelf shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        return new ShelfStats(SyncKind.Contacts, shelf.Contacts.Count, 0, shelf.At, 0);
    }

    /// <summary>Tasks over every stored list; the lists the account has, or those held; the queued changes.</summary>
    public static ShelfStats Of(TasksShelf shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        return new ShelfStats(
            SyncKind.Tasks,
            shelf.Held.Sum(l => l.Tasks.Count),
            shelf.Lists?.Count ?? shelf.Held.Count,
            Latest(shelf.Held.Select(l => (DateTimeOffset?)l.At).Append(shelf.ListsAt)),
            shelf.Pending.Count);
    }

    /// <summary>
    /// [S-3821] The kinds an account's group lists: every kind its provider feeds, in one fixed order
    /// (mail, calendar, contacts, tasks), each with its numbers or none yet. A kind the provider does
    /// not feed is never listed, even if a stray shelf of it were reported.
    /// </summary>
    public static IReadOnlyList<KindSummary> Summarize(AccountFeatures features, IEnumerable<ShelfStats> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var byKind = new Dictionary<SyncKind, ShelfStats>();
        foreach (var s in stats)
        {
            byKind[s.Kind] = s;
        }

        var rows = new List<KindSummary>(4);
        foreach (var (kind, feature) in Order)
        {
            if ((features & feature) != 0)
            {
                rows.Add(new KindSummary(kind, byKind.GetValueOrDefault(kind)));
            }
        }

        return rows;
    }

    /// <summary>[S-3822] A kind's name on its row.</summary>
    public static string Label(SyncKind kind) => kind switch
    {
        SyncKind.Mail => Str.Shell_Accounts_Stats_Mail.Text(),
        SyncKind.Calendar => Str.Shell_Accounts_Stats_Calendar.Text(),
        SyncKind.Contacts => Str.Shell_Accounts_Stats_Contacts.Text(),
        SyncKind.Tasks => Str.Shell_Accounts_Stats_Tasks.Text(),
        _ => kind.ToString(),
    };

    /// <summary>[S-3822] The count on the right of a kind's row ("1,240 messages"), or empty when nothing is stored.</summary>
    public static string Count(KindSummary row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Stats is not { } stats)
        {
            return string.Empty;
        }

        return (row.Kind switch
        {
            SyncKind.Mail => Str.Shell_Accounts_Stats_Messages,
            SyncKind.Calendar => Str.Shell_Accounts_Stats_Events,
            SyncKind.Contacts => Str.Shell_Accounts_Stats_People,
            _ => Str.Shell_Accounts_Stats_TaskCount,
        }).Plural(stats.Items, ("count", LocaleFormat.Integer(stats.Items)));
    }

    /// <summary>
    /// [S-3822] The line under a kind: where the items are, when it last synced, and what is waiting
    /// ("in 3 folders · synced 5 min. ago · 2 changes waiting to sync"); "Nothing stored on this PC yet"
    /// for a kind with no shelf this run.
    /// </summary>
    public static string Detail(KindSummary row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Stats is not { } stats)
        {
            return Str.Shell_Accounts_Stats_Nothing.Text();
        }

        var parts = new List<string>(3);

        if (stats.Containers > 0 && row.Kind != SyncKind.Contacts)
        {
            var where = row.Kind switch
            {
                SyncKind.Mail => Str.Shell_Accounts_Stats_Folders,
                SyncKind.Calendar => Str.Shell_Accounts_Stats_Calendars,
                _ => Str.Shell_Accounts_Stats_Lists,
            };

            parts.Add(where.Plural(stats.Containers, ("count", LocaleFormat.Integer(stats.Containers))));
        }

        parts.Add(stats.LastSync is { } at
            ? Str.Shell_Accounts_Stats_Synced.Format(("time", Widgets.FetchState.Freshness(at, now)))
            : Str.Shell_Accounts_Stats_NeverSynced.Text());

        if (stats.Pending > 0)
        {
            parts.Add(Str.Sync_Pending.Plural(stats.Pending, ("count", LocaleFormat.Integer(stats.Pending))));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>[S-3822] Changes still waiting across the account's kinds.</summary>
    public static int PendingTotal(IEnumerable<KindSummary> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Sum(r => r.Stats?.Pending ?? 0);
    }

    private static readonly (SyncKind Kind, AccountFeatures Feature)[] Order =
    [
        (SyncKind.Mail, AccountFeatures.Mail),
        (SyncKind.Calendar, AccountFeatures.Calendar),
        (SyncKind.Contacts, AccountFeatures.Contacts),
        (SyncKind.Tasks, AccountFeatures.Tasks),
    ];

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> times)
    {
        DateTimeOffset? latest = null;
        foreach (var time in times)
        {
            // A default timestamp is "never", as a span whose full window was never read stores it.
            if (time is { } t && t != default && (latest is null || t > latest))
            {
                latest = t;
            }
        }

        return latest;
    }
}

/// <summary>
/// [S-3823] The latest numbers of every shelf in this run, per owner and kind, as the sync services
/// report them when they load or save a shelf. In memory only; safe from any thread. Forgetting an
/// owner (a sign-out) drops all of its kinds.
/// </summary>
public sealed class SyncLedger
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Owner, SyncKind Kind), ShelfStats> _stats = [];

    /// <summary>Record a shelf's numbers. False when they are what was recorded already (nothing to tell).</summary>
    public bool Record(string owner, ShelfStats stats)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentNullException.ThrowIfNull(stats);

        lock (_gate)
        {
            var key = (owner, stats.Kind);
            if (_stats.TryGetValue(key, out var known) && known == stats)
            {
                return false;
            }

            _stats[key] = stats;
            return true;
        }
    }

    /// <summary>Drop everything recorded for <paramref name="owner"/>. False when there was nothing.</summary>
    public bool Forget(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (_gate)
        {
            var keys = _stats.Keys.Where(k => k.Owner == owner).ToList();
            foreach (var key in keys)
            {
                _stats.Remove(key);
            }

            return keys.Count > 0;
        }
    }

    /// <summary>What is recorded for <paramref name="owner"/>, any order.</summary>
    public IReadOnlyList<ShelfStats> For(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        lock (_gate)
        {
            return [.. _stats.Where(p => p.Key.Owner == owner).Select(p => p.Value)];
        }
    }
}
