using System.Globalization;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Sync;

namespace Themia.App.Services.Office;

/// <summary>
/// [S-560] The provider half: what to ask Microsoft Graph, Google Calendar and People, CalDAV and
/// CardDAV — the classic app's <c>get_calendar_events</c>, <c>list_calendars</c>,
/// <c>get_contact_birthdays</c> and <c>get_contacts</c> — and how to read the answer (the reading is
/// in Core, where it is tested). Every request goes through <see cref="AccountService"/>, which adds
/// the proof, refreshes a token once on a 401, follows pages and redirects, and fails with a typed
/// error. Only the sync half calls these.
/// </summary>
internal sealed partial class AccountOfficeSource
{

    private const string Graph = "https://graph.microsoft.com/v1.0/";
    private const string GoogleCalendar = "https://www.googleapis.com/calendar/v3/";
    private const string People = "https://people.googleapis.com/v1/people/me/connections";

    /// <summary>A ceiling on events from one calendar over one window, whatever the provider keeps paging ([S-2630]).</summary>
    private const int MaxEvents = OfficeShelves.MaxEvents;

    /// <summary>The event fields every Graph read asks for, so a calendar view and a re-read by id ([S-3610]) agree.</summary>
    private const string GraphEventFields = "id,subject,start,end,isAllDay,isCancelled,location,organizer,webLink,onlineMeeting,onlineMeetingUrl,bodyPreview,responseStatus";

    /// <summary>A ceiling on contacts read for birthdays: the whole address book, within reason.</summary>

    private const int MaxBirthdayContacts = 5000;

    // -----------------------------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------------------------

    private async Task<IReadOnlyList<CalendarEvent>> LoadEventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var account = Require(target.AccountId);

        switch (account.Provider)
        {
            case var pid when pid == HostProviders.Microsoft:
            {
                // [S-567] A chosen calendar is asked by id (opaque base64url, so escaped); none is
                // the account's default calendar view.
                var path = target.CalendarId is { } id ? $"me/calendars/{Uri.EscapeDataString(id)}/calendarView" : "me/calendarView";
                var url = new Uri($"{Graph}{path}?startDateTime={Uri.EscapeDataString(Iso(from))}&endDateTime={Uri.EscapeDataString(Iso(to))}"
                    + $"&$select={GraphEventFields}&$orderby=start/dateTime&$top=100");

                var items = await _accounts.GetGraphPagesAsync(account.Id, url, OfficeJsonContext.Default.GraphEvent, MaxEvents, ct,
                    ("Prefer", "outlook.timezone=\"UTC\"")).ConfigureAwait(false);

                return [.. items.Select(OfficeWire.FromGraph).OfType<CalendarEvent>()];
            }

            case var pid when pid == HostProviders.Google:
            {
                var calendar = Uri.EscapeDataString(target.CalendarId ?? "primary");
                var url = new Uri($"{GoogleCalendar}calendars/{calendar}/events?timeMin={Uri.EscapeDataString(Iso(from))}&timeMax={Uri.EscapeDataString(Iso(to))}"
                    + "&singleEvents=true&orderBy=startTime&maxResults=250");

                var items = await _accounts.GetGooglePagesAsync(account.Id, url, "items", OfficeJsonContext.Default.GoogleEvent, MaxEvents, ct)
                    .ConfigureAwait(false);

                return [.. items.Select(OfficeWire.FromGoogle).OfType<CalendarEvent>()];
            }

            case var pid when pid == HostProviders.CalDav:
            {
                var reply = await _accounts.DavAsync(account.Id, "REPORT", DavTarget(account, target.CalendarId), DavOffice.CalendarQuery(from, to), 1, ct)
                    .ConfigureAwait(false);

                var events = new List<CalendarEvent>();
                foreach (var data in DavOffice.CalendarData(reply.Body))
                {
                    events.AddRange(IcsEvents.Read(data, from, to, TimeZoneInfo.Local));
                }

                return events;
            }

            default:
                throw new AccountException(AccountError.Refused, $"{account.Label} has no calendar.");
        }
    }

    // -----------------------------------------------------------------------------------------
    // Calendars
    // -----------------------------------------------------------------------------------------

    private async Task<IReadOnlyList<CalendarInfo>> LoadCalendarsAsync(string accountId, CancellationToken ct)
    {
        var account = Require(accountId);

        switch (account.Provider)
        {
            case var pid when pid == HostProviders.Microsoft:
            {
                var url = new Uri($"{Graph}me/calendars?$select=id,name,isDefaultCalendar,hexColor&$top=100");
                var items = await _accounts.GetGraphPagesAsync(account.Id, url, OfficeJsonContext.Default.GraphCalendar, 500, ct).ConfigureAwait(false);
                return [.. items.Select(OfficeWire.FromGraph).OfType<CalendarInfo>()];
            }

            case var pid when pid == HostProviders.Google:
            {
                try
                {
                    var url = new Uri($"{GoogleCalendar}users/me/calendarList?maxResults=250");
                    var items = await _accounts.GetGooglePagesAsync(account.Id, url, "items", OfficeJsonContext.Default.GoogleCalendar, 500, ct)
                        .ConfigureAwait(false);
                    return [.. items.Select(OfficeWire.FromGoogle).OfType<CalendarInfo>()];
                }
                catch (AccountException ex) when (ex.Error == AccountError.Forbidden)
                {
                    // A Google sign-in that was not granted calendar.calendarlist.readonly (the user
                    // left it out on the consent screen) may read events but not list calendars. The
                    // primary calendar still works, so the picker offers that rather than calling the
                    // account broken.
                    Log.Info(Scope, $"{account.Id}: the calendar list is not readable with this sign-in; offering the primary calendar");
                    return [new CalendarInfo("primary", account.Address, true, null)];
                }
            }

            case var pid when pid == HostProviders.CalDav:
                return await LoadDavCalendarsAsync(account, ct).ConfigureAwait(false);

            default:
                return [];
        }
    }

    /// <summary>
    /// [S-567] The collection the account was set up with → its calendar home set (directly, or via
    /// the principal) → a Depth-1 listing. Degrades to the one stored collection when there is no
    /// home set to list, as the original did. Never follows a home set or principal to another
    /// origin: the account's password goes only where the account lives ([S-578]).
    /// </summary>
    private async Task<IReadOnlyList<CalendarInfo>> LoadDavCalendarsAsync(AccountInfo account, CancellationToken ct)
    {
        var collection = Collection(account);
        var single = (IReadOnlyList<CalendarInfo>)[DavOffice.SingleCalendar(collection)];

        var probe = await _accounts.DavAsync(account.Id, "PROPFIND", null, DavDiscovery.DiscoveryBody(DavKind.Calendar), 0, ct).ConfigureAwait(false);

        Uri? home = null;
        if (DavUrls.Resolve(probe.FinalUrl, DavXml.HomeSet(probe.Body, DavKind.Calendar)) is { } direct)
        {
            home = direct;
        }
        else if (DavUrls.Resolve(probe.FinalUrl, DavXml.Principal(probe.Body)) is { } principal && DavOffice.SameOrigin(principal, collection))
        {
            var answer = await _accounts.DavAsync(account.Id, "PROPFIND", principal, DavDiscovery.HomeBody(DavKind.Calendar), 0, ct).ConfigureAwait(false);
            home = DavUrls.Resolve(answer.FinalUrl, DavXml.HomeSet(answer.Body, DavKind.Calendar));
        }

        if (home is null || !DavOffice.SameOrigin(home, collection))
        {
            return single;
        }

        var listing = await _accounts.DavAsync(account.Id, "PROPFIND", home, DavDiscovery.ListBody(DavKind.Calendar), 1, ct).ConfigureAwait(false);
        var calendars = DavOffice.Calendars(listing.Body, listing.FinalUrl, collection);
        return calendars.Count > 0 ? calendars : single;
    }

    // -----------------------------------------------------------------------------------------
    // Birthdays
    // -----------------------------------------------------------------------------------------

    private async Task<IReadOnlyList<Birthday>> LoadBirthdaysAsync(string accountId, CancellationToken ct)
    {
        var account = Require(accountId);

        switch (account.Provider)
        {
            case var pid when pid == HostProviders.Microsoft:
            {
                var url = new Uri($"{Graph}me/contacts?$select=displayName,birthday&$top=500");
                var items = await _accounts.GetGraphPagesAsync(account.Id, url, OfficeJsonContext.Default.GraphContact, MaxBirthdayContacts, ct)
                    .ConfigureAwait(false);
                return [.. items.Select(OfficeWire.BirthdayFromGraph).OfType<Birthday>()];
            }

            case var pid when pid == HostProviders.Google:
            {
                var url = new Uri($"{People}?personFields=names,birthdays&pageSize=500");
                var items = await _accounts.GetGooglePagesAsync(account.Id, url, "connections", OfficeJsonContext.Default.GooglePersonRecord, MaxBirthdayContacts, ct)
                    .ConfigureAwait(false);
                return [.. items.Select(OfficeWire.BirthdayFromGoogle).OfType<Birthday>()];
            }

            default:
                // A CalDAV account has no address book behind it; the original showed none either.
                return [];
        }
    }

    // -----------------------------------------------------------------------------------------
    // Plumbing
    // -----------------------------------------------------------------------------------------

    private AccountInfo Require(string accountId)
        => _accounts.Find(accountId) ?? throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());

    private static Uri Collection(AccountInfo account)
        => Uri.TryCreate(account.ServerUrl, UriKind.Absolute, out var url)
            ? url
            : throw new AccountException(AccountError.Refused, $"{account.Label} has no DAV address.");

    /// <summary>
    /// [S-578] The collection to REPORT: the stored one, or a chosen calendar URL from the widget
    /// config — refused unless it is on the account's own origin, so a hand-edited config cannot
    /// send the account's password to another host.
    /// </summary>
    private static Uri? DavTarget(AccountInfo account, string? calendarId)
    {
        if (calendarId is null)
        {
            return null;
        }

        var collection = Collection(account);
        return Uri.TryCreate(calendarId, UriKind.Absolute, out var url) && DavOffice.SameOrigin(url, collection)
            ? url
            : throw new AccountException(AccountError.Refused, Str.Shell_Accounts_CalendarElsewhere.Text());
    }

    private static string Iso(DateTimeOffset moment)
        => moment.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
