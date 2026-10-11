using Themia.Core.Mail;
using Themia.Core.Office;
using Themia.Core.Tasks;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2684]..[S-2688] Folding a provider's changes (what changed, by id, and what went) into a bounded
/// shelf. Every provider is reduced to that one pair — Graph's <c>@removed</c>, Google's cancelled or
/// deleted entries, a DAV resource gone or changed, an IMAP UID expunged — so the rules live once.
/// <para>
/// The merges hold what the provider says. Queued changes are not folded in here: they are laid on
/// top when a widget reads ([S-2622], <see cref="TaskEdits"/>), so a delta that left before a change
/// reached the provider cannot undo it on screen.
/// </para>
/// </summary>
public static class DeltaMerge
{
    /// <summary>
    /// [S-2684] A calendar window's changes. A changed event replaces the one held under its id when it
    /// still overlaps the window, and leaves it when it moved out; a removed id leaves. The window is
    /// unchanged, and a delta answers for all of it, so both refresh stamps move on.
    /// </summary>
    public static CalendarSpan Events(CalendarSpan held, IReadOnlyList<CalendarEvent> changed, IReadOnlyCollection<string> removed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);

        var incoming = Latest(changed.Where(e => !string.IsNullOrEmpty(e.Id)), e => e.Id).Select(OfficeShelves.Store).ToList();
        var removedIds = new HashSet<string>(removed, StringComparer.Ordinal);
        var gone = new HashSet<string>(removedIds, StringComparer.Ordinal);
        gone.UnionWith(incoming.Select(e => e.Id));

        var events = held.Events.Where(e => !gone.Contains(e.Id)).ToList();
        events.AddRange(incoming.Where(e => !removedIds.Contains(e.Id) && CalendarSync.Overlaps(e, held.From, held.To)));

        return new CalendarSpan
        {
            CalendarId = held.CalendarId,
            From = held.From,
            To = held.To,
            NearAt = now,
            FullAt = now,
            Events = CalendarSync.Bound(events, now),
        };
    }

    /// <summary>
    /// [S-2685] An address book's changes, up to <paramref name="max"/> people in name order. Whole is
    /// false once the bound is reached: the book may hold people this copy never saw, so the next
    /// sync must read it in full rather than trust the cursor.
    /// </summary>
    public static (IReadOnlyList<Contact> People, bool Whole) Contacts(IReadOnlyList<Contact> held, IReadOnlyList<Contact> changed, IReadOnlyCollection<string> removed, int max)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);

        var people = Upsert(held, changed, c => c.Id, removed)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        return (people.Take(Math.Max(0, max)).ToList(), people.Count < max);
    }

    /// <summary>
    /// [S-2686] A task list's changes. A changed task replaces the held one in its place, a new one
    /// joins at the end; a removed, deleted or hidden one leaves, and so does one finished when the
    /// list is held without finished tasks ([S-2652]) — reopened, it comes back. Whole is false at
    /// the bound, as for contacts.
    /// </summary>
    public static (IReadOnlyList<TaskItem> Tasks, bool Whole) Tasks(IReadOnlyList<TaskItem> held, IReadOnlyList<TaskItem> changed, IReadOnlyCollection<string> removed, bool includeCompleted, int max)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);

        var tasks = Upsert(held, changed, t => t.Id, removed)
            .Where(t => includeCompleted || !t.Completed)
            .ToList();

        return (tasks.Take(Math.Max(0, max)).ToList(), tasks.Count < max);
    }

    /// <summary>
    /// [S-2687] A mail folder's changes into its newest <paramref name="count"/>. New and changed
    /// messages are upserted, removed ones (deleted, moved away, expunged) leave, and the list is cut
    /// back to the newest <paramref name="count"/> in <paramref name="newestFirst"/> order. A list
    /// left shorter than the folder could fill needs a refill: older messages it never held may
    /// belong in it now, and only a full fetch knows them. "Could fill" is the folder's
    /// <paramref name="total"/> when the counts probe gave one, else whether the list was full before.
    /// </summary>
    public static (IReadOnlyList<MailMessage> Messages, bool Refill) Mail(
        IReadOnlyList<MailMessage> held,
        IReadOnlyList<MailMessage> changed,
        IReadOnlyCollection<string> removed,
        int count,
        Comparison<MailMessage> newestFirst,
        long? total = null)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(newestFirst);

        var merged = Upsert(held, changed, m => m.RawId, removed).ToList();
        merged.Sort(newestFirst);

        var refill = merged.Count < count && (total is { } known ? merged.Count < known : held.Count >= count);
        return (merged.Take(Math.Max(0, count)).ToList(), refill);
    }

    /// <summary>Newest received first; undated last; ties by id so the order is stable.</summary>
    public static int ByReceived(MailMessage a, MailMessage b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var byDate = (b.Received ?? DateTimeOffset.MinValue).CompareTo(a.Received ?? DateTimeOffset.MinValue);
        return byDate != 0 ? byDate : string.CompareOrdinal(b.RawId, a.RawId);
    }

    /// <summary>IMAP: the highest UID is the newest arrival (the order the full fetch lists in).</summary>
    public static int ByImapUid(MailMessage a, MailMessage b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var ua = ImapMessageId.Parse(a.RawId)?.Uid ?? 0;
        var ub = ImapMessageId.Parse(b.RawId)?.Uid ?? 0;
        return ub.CompareTo(ua);
    }

    /// <summary>
    /// [S-2687] The date a Graph folder's delta is bounded by: the oldest message held when the folder
    /// is full, so the delta follows only the messages the shelf can keep; none (the whole folder)
    /// when it holds fewer than asked, since then every message belongs.
    /// </summary>
    public static DateTimeOffset? MailFilter(IReadOnlyList<MailMessage> held, int count)
    {
        ArgumentNullException.ThrowIfNull(held);

        if (held.Count < count || held.Any(m => m.Received is null))
        {
            return null;
        }

        return held.Min(m => m.Received!.Value);
    }

    /// <summary>
    /// [S-2688] DAV resources: fold the resources read again (with the items each now yields) and the
    /// ones gone into the held tags. Returns the new tags and every item id no resource yields any
    /// more — the ids to remove — so a DAV delta becomes the same (changed, removed) pair as the rest.
    /// </summary>
    public static (List<DavTag> Tags, List<string> Removed) Resources(IReadOnlyList<DavTag> held, IReadOnlyList<DavTag> fetched, IReadOnlyCollection<string> goneHrefs)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(fetched);
        ArgumentNullException.ThrowIfNull(goneHrefs);

        var incoming = Latest(fetched.Where(t => !string.IsNullOrEmpty(t.Href)), t => t.Href).ToList();
        var replaced = new HashSet<string>(incoming.Select(t => t.Href), StringComparer.Ordinal);
        var gone = new HashSet<string>(goneHrefs, StringComparer.Ordinal);

        var tags = new List<DavTag>();
        var dropped = new List<string>();

        foreach (var tag in held)
        {
            if (gone.Contains(tag.Href) || replaced.Contains(tag.Href))
            {
                dropped.AddRange(tag.Ids);
                continue;
            }

            tags.Add(tag);
        }

        tags.AddRange(incoming.Where(t => !gone.Contains(t.Href)));

        var still = new HashSet<string>(tags.SelectMany(t => t.Ids), StringComparer.Ordinal);
        return (tags, [.. dropped.Where(id => !still.Contains(id)).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// [S-2688] Which held resources a listing of (href, etag) says to read again (new or another
    /// etag) and which are gone (held, not listed).
    /// </summary>
    public static (IReadOnlyList<string> Changed, IReadOnlyList<string> Gone) Diff(IReadOnlyList<DavTag> held, IReadOnlyList<(string Href, string Etag)> listing)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(listing);

        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in held)
        {
            known.TryAdd(tag.Href, tag.Etag);
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        var changed = new List<string>();

        foreach (var (href, etag) in listing)
        {
            if (string.IsNullOrEmpty(href) || !listed.Add(href))
            {
                continue;
            }

            // A resource listed without an etag cannot be compared: read it again.
            if (!known.TryGetValue(href, out var heldEtag) || etag.Length == 0 || !string.Equals(heldEtag, etag, StringComparison.Ordinal))
            {
                changed.Add(href);
            }
        }

        return (changed, [.. known.Keys.Where(h => !listed.Contains(h))]);
    }

    /// <summary>Held items with the changed ones in their place, the new ones after, the removed ones out.</summary>
    private static IEnumerable<T> Upsert<T>(IReadOnlyList<T> held, IReadOnlyList<T> changed, Func<T, string> id, IReadOnlyCollection<string> removed)
    {
        var gone = new HashSet<string>(removed, StringComparer.Ordinal);
        var incoming = Latest(changed.Where(c => !string.IsNullOrEmpty(id(c))), id).ToDictionary(id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in held)
        {
            var key = id(item);
            if (gone.Contains(key) || !seen.Add(key))
            {
                continue;
            }

            yield return incoming.TryGetValue(key, out var fresh) ? fresh : item;
        }

        foreach (var (key, item) in incoming)
        {
            if (!gone.Contains(key) && seen.Add(key))
            {
                yield return item;
            }
        }
    }

    /// <summary>The last entry for each key, in first-seen order: a page that names an item twice means its later state.</summary>
    private static IEnumerable<T> Latest<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var order = new List<string>();
        var last = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var k = key(item);
            if (!last.ContainsKey(k))
            {
                order.Add(k);
            }

            last[k] = item;
        }

        return order.Select(k => last[k]);
    }
}
