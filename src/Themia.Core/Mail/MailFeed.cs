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

/// <summary>What the widget's body shows.</summary>
public enum MailScreenKind
{
    /// <summary>No mail-capable account is signed in to Themia at all.</summary>
    NoAccounts,

    /// <summary>The widget names accounts and none of them is signed in any more.</summary>
    AccountsGone,

    /// <summary>Every account it would read needs the user to sign in again.</summary>
    SignInRequired,

    /// <summary>Asked, nothing back yet, nothing to show meanwhile.</summary>
    Loading,

    /// <summary>Every account failed and there is nothing to show.</summary>
    Failed,

    /// <summary>Fetched, and the folder is empty.</summary>
    Empty,

    /// <summary>A list. <see cref="MailScreen.Warning"/> may still name an account that failed.</summary>
    Messages,
}

/// <param name="OffersAccounts">Whether the screen carries a way to the Accounts settings page.</param>
/// <param name="Warning">For a list: one account that could not be read, in a sentence; else null.</param>
public sealed record MailScreen(MailScreenKind Kind, string Text, bool OffersAccounts, string? Warning);

/// <summary>[S-549] Which accounts a widget reads, and which screen it shows.</summary>
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

    public static MailScreen Decide(
        IReadOnlyList<MailAccount> mailAccounts,
        IReadOnlyList<string> chosen,
        IReadOnlyList<MailAccountOutcome> outcomes,
        int messageCount,
        MailFolder folder)
        => Decide(Strings.Current, mailAccounts, chosen, outcomes, messageCount, folder);

    /// <summary><see cref="Decide(IReadOnlyList{MailAccount}, IReadOnlyList{string}, IReadOnlyList{MailAccountOutcome}, int, MailFolder)"/> in <paramref name="locale"/>.</summary>
    public static MailScreen Decide(
        Locale locale,
        IReadOnlyList<MailAccount> mailAccounts,
        IReadOnlyList<string> chosen,
        IReadOnlyList<MailAccountOutcome> outcomes,
        int messageCount,
        MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(mailAccounts);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(outcomes);

        if (mailAccounts.Count == 0)
        {
            // [S-1862] The card's Set up button stands here; the screen itself says nothing.
            return new MailScreen(MailScreenKind.NoAccounts, string.Empty, false, null);
        }

        var targets = Targets(mailAccounts, chosen);
        if (targets.Count == 0)
        {
            return new MailScreen(MailScreenKind.AccountsGone, Strings.Get(locale, Str.WidgetsB_Email_AccountsGone), true, null);
        }

        var usable = targets.Where(a => a.Problem is null).ToList();
        var broken = targets.Where(a => a.Problem is not null).ToList();

        if (usable.Count == 0)
        {
            var text = broken.Count == 1
                ? SignInAgain(locale, broken[0].Label)
                : Strings.Get(locale, Str.WidgetsB_Email_AllSignInAgain);
            return new MailScreen(MailScreenKind.SignInRequired, text, true, null);
        }

        var byId = outcomes.GroupBy(o => o.AccountId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var states = usable
            .Select(a => (Account: a, Outcome: byId.TryGetValue(a.Id, out var o) ? o : null))
            .ToList();
        var failed = states.Where(s => s.Outcome is { State: MailAccountState.Failed }).ToList();

        var warning = broken.Count > 0
            ? SignInAgain(locale, broken[0].Label)
            : failed.Count > 0
                ? Stamped(locale, Strings.Format(locale, Str.WidgetsB_Email_CouldNotRefresh, ("account", failed[0].Account.Label), ("reason", Reason(locale, failed[0].Outcome!.Status))), failed[0].Outcome!.At)
                : null;

        if (messageCount > 0)
        {
            return new MailScreen(MailScreenKind.Messages, string.Empty, broken.Count > 0, warning);
        }

        if (states.Any(s => s.Outcome is null or { State: MailAccountState.Pending }))
        {
            return new MailScreen(MailScreenKind.Loading, Strings.Get(locale, Str.WidgetsB_Email_Loading), false, null);
        }

        if (failed.Count == states.Count)
        {
            var status = failed[0].Outcome!.Status;
            var signIn = status == FetchStatus.NotConfigured;
            var text = signIn
                ? SignInAgain(locale, failed[0].Account.Label)
                : status == FetchStatus.Offline
                    ? Strings.Get(locale, Str.WidgetsB_Email_Offline)
                    : Describe(locale, status);
            return new MailScreen(signIn ? MailScreenKind.SignInRequired : MailScreenKind.Failed, text, signIn, null);
        }

        return new MailScreen(MailScreenKind.Empty, Strings.Format(locale, Str.WidgetsB_Email_NoMessagesIn, ("folder", folder.Label(locale))), false, warning);
    }

    /// <summary>
    /// [S-559] How long ago the list on screen was fetched, for the Refresh command: the oldest
    /// account's stamp (the worst of what is shown, as [S-2664] stamps), in the language's short
    /// relative time ("now", "5 min. ago"). A provider clock ahead of this one reads "now". Null
    /// while nothing is stored yet.
    /// </summary>
    public static string? UpdatedAgo(Locale locale, IReadOnlyList<MailAccountOutcome> outcomes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        DateTimeOffset? oldest = null;
        foreach (var outcome in outcomes)
        {
            if (outcome.At is { } at && (oldest is not { } held || at < held))
            {
                oldest = at;
            }
        }

        if (oldest is not { } stamp)
        {
            return null;
        }

        var age = now - stamp;
        return LocaleFormat.Relative(locale, age > TimeSpan.Zero ? -age : TimeSpan.Zero, RelativeStyle.Short);
    }

    /// <summary>[S-2612] A failure over a stored list also says when that list is from.</summary>
    private static string Stamped(Locale locale, string sentence, DateTimeOffset? at)
        => at is { } stamp
            ? Strings.Format(locale, Str.Sync_WithUpdated, ("sentence", sentence), ("time", Sync.SyncFreshness.When(locale, stamp, DateTimeOffset.Now)))
            : sentence;

    private static string SignInAgain(Locale locale, string account)
        => Strings.Format(locale, Str.WidgetsB_Email_SignInAgain, ("account", account));

    /// <summary>A failure in mail's own words, as a sentence; the generic one where mail has nothing better to say.</summary>
    public static string Describe(Locale locale, FetchStatus status) => Strings.Get(locale, status switch
    {
        FetchStatus.Offline => Str.WidgetsB_Email_Status_Offline,
        FetchStatus.Unreachable => Str.WidgetsB_Email_Status_Unreachable,
        FetchStatus.TimedOut => Str.WidgetsB_Email_Status_TimedOut,
        FetchStatus.RateLimited => Str.WidgetsB_Email_Status_RateLimited,
        FetchStatus.NotConfigured => Str.WidgetsB_Email_Status_NotConfigured,
        FetchStatus.Refused => Str.WidgetsB_Email_Status_Refused,
        _ => Str.WidgetsB_Email_Status_Unexpected,
    });

    /// <summary>The same failure as the reason inside "Could not refresh Home: …" (lower case where the language wants it).</summary>
    private static string Reason(Locale locale, FetchStatus status) => Strings.Get(locale, status switch
    {
        FetchStatus.Offline => Str.WidgetsB_Email_Reason_Offline,
        FetchStatus.Unreachable => Str.WidgetsB_Email_Reason_Unreachable,
        FetchStatus.TimedOut => Str.WidgetsB_Email_Reason_TimedOut,
        FetchStatus.RateLimited => Str.WidgetsB_Email_Reason_RateLimited,
        FetchStatus.NotConfigured => Str.WidgetsB_Email_Reason_NotConfigured,
        FetchStatus.Refused => Str.WidgetsB_Email_Reason_Refused,
        _ => Str.WidgetsB_Email_Reason_Unexpected,
    });
}

/// <summary>[S-548] When a message arrived, in a list's few characters.</summary>
public static class MailTime
{
    /// <summary>List and compact rows: now, 5m, 3h, 2d, then a date.</summary>
    public static string Short(DateTimeOffset? when, DateTimeOffset now, Locale locale)
    {
        if (when is not { } at)
        {
            return string.Empty;
        }

        var age = now - at;

        if (age < TimeSpan.FromMinutes(1))
        {
            return LocaleFormat.Relative(locale, TimeSpan.Zero); // "now", also for a provider clock running ahead of this one
        }

        if (age < TimeSpan.FromHours(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_MinutesCompact, ("count", (long)age.TotalMinutes));
        }

        if (age < TimeSpan.FromDays(1))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_HoursCompact, ("count", (long)age.TotalHours));
        }

        if (age < TimeSpan.FromDays(7))
        {
            return Strings.Format(locale, Str.WidgetsB_Time_DaysCompact, ("count", (long)age.TotalDays));
        }

        return MonthDay(at.ToOffset(now.Offset), locale);
    }

    /// <summary>The row view's date column: a time today, a weekday this week, then a date.</summary>
    public static string Full(DateTimeOffset? when, DateTimeOffset now, Locale locale)
    {
        if (when is not { } at)
        {
            return string.Empty;
        }

        var local = at.ToOffset(now.Offset);
        var age = now - at;

        if (age < TimeSpan.FromDays(1))
        {
            return LocaleFormat.Time(locale, local.DateTime);
        }

        return age < TimeSpan.FromDays(7) ? LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short) : MonthDay(local, locale);
    }

    /// <summary>"Sep 28" · "28. Sept.": the language's short month-day.</summary>
    private static string MonthDay(DateTimeOffset at, Locale locale) => LocaleFormat.Date(locale, DateOnly.FromDateTime(at.DateTime), DateStyle.MonthDayShort);
}
