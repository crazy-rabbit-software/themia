using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.App.Services.Office;

/// <summary>
/// What a sync answered: the data (the last good copy when this attempt failed), why it is stale
/// or missing, and when it was fetched.
/// </summary>
/// <param name="Value">The data, or null when there has never been any for this request.</param>
/// <param name="Problem">Why the last attempt failed, or <see cref="SyncProblem.None"/>.</param>
/// <param name="Detail">One line for the log; never shown verbatim.</param>
/// <param name="At">When <paramref name="Value"/> was fetched.</param>
internal sealed record Synced<T>(T? Value, SyncProblem Problem, string? Detail, DateTimeOffset? At)
    where T : class;

/// <summary>
/// [S-588] [S-2630] Where the calendar and contacts widgets read from: Themia's own accounts, through
/// the offline store. One instance is shared by every widget, so four calendars on a desktop cost one
/// sync per calendar, not four, and a view switch costs none.
/// </summary>
internal interface IOfficeSource
{
    /// <summary>Raised on the UI thread when an account is added, removed, renamed or changes health, and when a sync brought new data.</summary>
    event Action? Changed;

    /// <summary>The accounts that can feed <paramref name="feature"/>, in the order they were added.</summary>
    IReadOnlyList<AccountInfo> Accounts(FeatureId feature);

    /// <summary>Events overlapping [<paramref name="from"/>, <paramref name="to"/>) of one calendar.</summary>
    Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation);

    /// <summary>[S-567] The calendars an account exposes, its default flagged.</summary>
    Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation);

    /// <summary>[S-575] Its contacts' birthdays, held for a day.</summary>
    Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation);

    /// <summary>[S-580] Up to <paramref name="max"/> of its contacts.</summary>
    Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation);
}

/// <summary>Which <see cref="IOfficeSource"/> widgets use right now.</summary>
internal static class OfficeSources
{
    /// <summary>
    /// The host's source, or null when there is none: the host has not started (or is shutting
    /// down). A widget treats that as "no account". Set by the host, which owns its lifetime.
    /// </summary>
    public static IOfficeSource? Current { get; internal set; }
}
