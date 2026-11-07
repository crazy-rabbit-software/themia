using Themia.Core.Accounts;
using Themia.Core.Office;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-567] Where a CalDAV account's collections are listed: the collection it was set up with → its
/// calendar home set (directly, or via the principal) → a Depth-1 listing of that home set. The
/// calendars and the task lists ([S-618]) read the same listing. Never follows a home set or
/// principal to another origin: the account's password goes only where the account lives ([S-578]).
/// </summary>
internal static class DavHomes
{
    /// <summary>
    /// The Depth-1 listing of the account's calendar home set, or null when it has none to list (the
    /// caller then offers the one stored collection, as the original did).
    /// </summary>
    public static async Task<AccountReply?> CalendarListingAsync(AccountService accounts, string accountId, Uri collection, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(collection);

        var probe = await accounts.DavAsync(accountId, "PROPFIND", null, DavDiscovery.DiscoveryBody(DavKind.Calendar), 0, ct).ConfigureAwait(false);

        Uri? home = null;
        if (DavUrls.Resolve(probe.FinalUrl, DavXml.HomeSet(probe.Body, DavKind.Calendar)) is { } direct)
        {
            home = direct;
        }
        else if (DavUrls.Resolve(probe.FinalUrl, DavXml.Principal(probe.Body)) is { } principal && DavOffice.SameOrigin(principal, collection))
        {
            var answer = await accounts.DavAsync(accountId, "PROPFIND", principal, DavDiscovery.HomeBody(DavKind.Calendar), 0, ct).ConfigureAwait(false);
            home = DavUrls.Resolve(answer.FinalUrl, DavXml.HomeSet(answer.Body, DavKind.Calendar));
        }

        if (home is null || !DavOffice.SameOrigin(home, collection))
        {
            return null;
        }

        return await accounts.DavAsync(accountId, "PROPFIND", home, DavDiscovery.ListBody(DavKind.Calendar), 1, ct).ConfigureAwait(false);
    }
}
