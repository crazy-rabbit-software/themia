using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Office;

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
/// [S-588] Where the calendar and contacts widgets read from: Themia's own accounts. One instance is shared by every widget, so four calendars on
/// a desktop cost one request per calendar, not four.
/// </summary>
internal interface IOfficeSource
{
    /// <summary>Raised on the UI thread when an account is added, removed, renamed or changes health.</summary>
    event Action? Changed;

    /// <summary>The accounts that can feed <paramref name="feature"/>, in the order they were added.</summary>
    IReadOnlyList<AccountInfo> Accounts(AccountFeatures feature);

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
    private static AccountOfficeSource? _live;

    /// <summary>
    /// The source for this moment, or null when there is none: the host has not started (or is
    /// shutting down), or this is a debug render. A widget treats that as "no account".
    /// </summary>
    public static IOfficeSource? Current
    {
        get
        {
            if (AppServices.Accounts is not { } accounts)
            {
                return null;
            }

            // One live source per account service, so the caches survive as long as the accounts do.
            if (_live is null || !ReferenceEquals(_live.Service, accounts))
            {
                _live = new AccountOfficeSource(accounts);
            }

            return _live;
        }
    }
}
