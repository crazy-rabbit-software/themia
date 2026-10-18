using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Mail;

/// <summary>Where one account stands in a feed.</summary>
public enum MailAccountState
{
    /// <summary>Asked, no answer yet.</summary>
    Pending,

    /// <summary>The last fetch worked.</summary>
    Ok,

    /// <summary>The last attempt failed; <see cref="MailAccountOutcome.Status"/> says how.</summary>
    Failed,
}

/// <summary>How one account's part of a feed last went.</summary>
/// <param name="Folder">[S-1285] The folder's total and unread counts as the account last reported them, or null.</param>
/// <param name="At">[S-2612] [S-559] When the stored list was last known current (fetched, or a probe found it unchanged), or null when nothing is stored.</param>
public sealed record MailAccountOutcome(string AccountId, MailAccountState State, FetchStatus Status, string? Detail, MailFolderStatus? Folder = null, DateTimeOffset? At = null);

/// <summary>
/// [S-546] What a widget asks the mail service for. Two widgets asking the same thing share one feed
/// and one set of requests, so the key is order-insensitive in its accounts.
/// </summary>
public sealed record MailQuery(IReadOnlyList<string> AccountIds, MailFolder Folder, int Count)
{
    public string Key => string.Join(",", AccountIds.Order(StringComparer.Ordinal)) + "|" + Folder.Wire() + "|" + Count.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A feed's state at one moment: the merged list (newest first) and how each account last went.</summary>
/// <param name="WriteNote">[S-2624] Changes waiting to reach a provider, or the last one refused, in a sentence; else null.</param>
public sealed record MailFeedSnapshot(IReadOnlyList<MailMessage> Messages, IReadOnlyList<MailAccountOutcome> Outcomes, bool Busy, string? WriteNote = null)
{
    public static MailFeedSnapshot Empty { get; } = new([], [], false);
}

/// <summary>[S-548] Merging per-account lists into the one list a widget shows.</summary>
public static class MailList
{
    /// <summary>
    /// The newest <paramref name="count"/> messages across every account, then in the order asked.
    /// "Oldest first" is the same newest N turned round — not the N oldest messages in the mailbox,
    /// which nobody asking for a mail widget means. A message with no date sorts after every dated
    /// one; ties keep the accounts' order.
    /// </summary>
    public static IReadOnlyList<MailMessage> Merge(IEnumerable<IReadOnlyList<MailMessage>> perAccount, int count, MailSortOrder order)
    {
        ArgumentNullException.ThrowIfNull(perAccount);

        var newest = perAccount
            .SelectMany(list => list)
            .Select((message, index) => (message, index))
            .OrderByDescending(x => x.message.Received.HasValue)
            .ThenByDescending(x => x.message.Received ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.index)
            .Take(Math.Max(0, count))
            .Select(x => x.message)
            .ToList();

        if (order == MailSortOrder.OldestFirst)
        {
            newest.Reverse();
        }

        return newest;
    }

    /// <summary>The header badge: unread messages among those shown.</summary>
    public static int Unread(IReadOnlyList<MailMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages.Count(m => !m.IsRead);
    }
}

/// <summary>
/// [S-546] When to ask again. Each account's folder counts are probed on a cadence — one cheap
/// request — and the list is fetched only when they moved, or when it is old enough that a change
/// the counts cannot see (a message read and another arriving between two probes) might be hiding.
/// </summary>
public static class MailRefresh
{
    /// <summary>How often the counts are probed. Mail a minute or two late is fine; a request a second is not.</summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(2);

    /// <summary>The list is fetched again at least this often, whatever the counts say.</summary>
    public static readonly TimeSpan FullRefreshAfter = TimeSpan.FromMinutes(15);

    public static bool NeedsFetch(MailFolderStatus? known, MailFolderStatus? fresh, DateTimeOffset? lastFetch, DateTimeOffset now)
    {
        if (lastFetch is not { } fetched || known is null || fresh is null)
        {
            return true;
        }

        return known != fresh || now - fetched >= FullRefreshAfter || now < fetched;
    }
}

/// <summary>[S-549] Which accounts a widget reads (its screens are the kit's, [S-7040]).</summary>
public static class MailScreens
{
    /// <summary>
    /// The accounts a widget reads: every mail account when it names none, else the ones it names
    /// that are still signed in, in the accounts' own order.
    /// </summary>
    public static IReadOnlyList<MailAccount> Targets(IReadOnlyList<MailAccount> mailAccounts, IReadOnlyList<string> chosen)
    {
        ArgumentNullException.ThrowIfNull(mailAccounts);
        ArgumentNullException.ThrowIfNull(chosen);

        if (chosen.Count == 0)
        {
            return mailAccounts;
        }

        var wanted = new HashSet<string>(chosen, StringComparer.Ordinal);
        return [.. mailAccounts.Where(a => wanted.Contains(a.Id))];
    }
}

/// <summary>[S-548] When a message arrived, in a list's few characters.</summary>
public static class MailTime
{
    /// <summary>List and compact rows: now, 5m, 3h, 2d, then a date (the kit's <see cref="Themia.Sdk.Kit.KitTime.Compact"/>).</summary>
    public static string Short(DateTimeOffset? when, DateTimeOffset now, Locale locale)
        => when is { } at ? Themia.Sdk.Kit.KitText.Compact(locale, at, now) : string.Empty;

    /// <summary>The row view's date column: a time today, a weekday this week, then a date (the kit's <see cref="Themia.Sdk.Kit.KitTime.Stamp"/>).</summary>
    public static string Full(DateTimeOffset? when, DateTimeOffset now, Locale locale)
        => when is { } at ? Themia.Sdk.Kit.KitText.Stamp(locale, at, now) : string.Empty;
}

/// <summary>[S-1286] How many messages of a folder are kept and listed: the shelf's bound and the widget's whole list.</summary>
public static class MailLimits
{
    public const int PerFolder = 50;
}
