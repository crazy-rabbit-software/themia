using System.Globalization;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Office;
using Themia.Core.Sync;

namespace Themia.App.Services.Office;

/// <summary>
/// [S-2680]..[S-2688] The incremental half: a calendar's window and an address book read as the
/// changes since the stored cursor, where the provider offers one, and in full (starting a new
/// cursor) where it does not, where the cursor is stale ([S-2682]) or where the provider refused it.
/// <list type="bullet">
/// <item>Microsoft: the default calendar through <c>calendarView/delta</c> (Graph offers it for the
/// primary calendar only, so a chosen calendar is read in full); contacts through the default
/// folder's <c>contacts/delta</c>.</item>
/// <item>Google: events with <c>syncToken</c> (410 Gone = in full); People connections with
/// <c>syncToken</c> (an expired token = in full).</item>
/// <item>CalDAV / CardDAV: the collection's <c>getctag</c> (or <c>sync-token</c>) says whether
/// anything changed; when it did, an RFC 6578 <c>sync-collection</c> (where offered) or an etag
/// listing says which resources, and only those are read.</item>
/// </list>
/// The merging is <see cref="DeltaMerge"/>'s (Core, tested).
/// </summary>
internal sealed partial class AccountOfficeSource
{
    /// <summary>Whether a calendar is read incrementally, and through which cursor.</summary>
    private CursorKind? WindowCursorKind(string accountId, string? calendarId) => _accounts.Find(accountId)?.Provider switch
    {
        AccountProvider.Microsoft when calendarId is null => CursorKind.GraphDelta,
        AccountProvider.Google => CursorKind.GoogleSyncToken,
        AccountProvider.CalDav => CursorKind.Dav,
        _ => null,
    };

    private static string WindowScope(DateTimeOffset from, DateTimeOffset to) => $"{Iso(from)}|{Iso(to)}";

    private const string BookScope = "book";

    /// <summary>[S-2690] Calendars and books whose change requests were refused this session: read the plain way.</summary>
    private readonly DeltaFallback _fallback = new(Scope);

    // -----------------------------------------------------------------------------------------
    // Calendar window
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-2684] The whole window of one calendar: the changes since its cursor when usable, else in
    /// full. Only for a calendar <see cref="WindowCursorKind"/> names.
    /// </summary>
    private Task<CalendarSpan> SyncWindowAsync(CalendarTarget target, CalendarSpan? held, CursorKind kind, CancellationToken ct)
    {
        var (from, to) = CalendarSync.Window(DateOnly.FromDateTime(DateTime.Now), TimeZoneInfo.Local);

        // DAV's own refusals fall back inside its rounds ([S-2688]); its full read is the plain one.
        if (kind == CursorKind.Dav)
        {
            return WindowChangesAsync(target, held, kind, from, to, ct);
        }

        // [S-2690] A calendar whose provider refused the change request: the plain calendar view, no cursor.
        return _fallback.ReadAsync($"{target.AccountId}|calendar|{target.CalendarId}", $"{target.AccountId}: calendar {target.CalendarId ?? "default"}",
            () => WindowChangesAsync(target, held, kind, from, to, ct),
            async () =>
            {
                var events = await LoadEventsAsync(target, from, to, ct).ConfigureAwait(true);
                return CalendarSync.Merge(held, target.CalendarId, events, from, to, DateTimeOffset.UtcNow, full: true);
            });
    }

    private async Task<CalendarSpan> WindowChangesAsync(CalendarTarget target, CalendarSpan? held, CursorKind kind, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var account = Require(target.AccountId);
        var scope = WindowScope(from, to);
        var now = DateTimeOffset.UtcNow;
        var check = held is null ? CursorCheck.Missing : SyncCursors.Check(held.Cursor, kind, scope, now);

        if (check == CursorCheck.Usable && held?.Cursor is { } cursor)
        {
            try
            {
                return kind switch
                {
                    CursorKind.GraphDelta => await GraphWindowAsync(account, target, held, cursor, from, to, scope, now, ct).ConfigureAwait(true),
                    CursorKind.GoogleSyncToken => await GoogleWindowAsync(account, target, held, cursor, from, to, scope, now, ct).ConfigureAwait(true),
                    _ => await DavWindowChangesAsync(account, target, held, cursor, from, to, now, ct).ConfigureAwait(true),
                };
            }
            catch (AccountException ex) when (DeltaRules.Rejected(ex.HttpStatus, ex.ProviderMessage))
            {
                // [S-2681] Google's 410 "sync token is no longer valid", Graph's expired delta link.
                Log.Info(Scope, $"{account.Id}: the calendar refused its change cursor (HTTP {ex.HttpStatus}); reading the window in full");
            }
        }
        else if (check is CursorCheck.Stale or CursorCheck.OtherScope)
        {
            Log.Debug(Scope, $"{account.Id}: calendar window read in full ({check})");
        }

        return kind switch
        {
            CursorKind.GraphDelta => await GraphWindowAsync(account, target, held, null, from, to, scope, now, ct).ConfigureAwait(true),
            CursorKind.GoogleSyncToken => await GoogleWindowAsync(account, target, held, null, from, to, scope, now, ct).ConfigureAwait(true),
            _ => await DavWindowFullAsync(account, target, held, from, to, scope, now, ct).ConfigureAwait(true),
        };
    }

    /// <summary>A Graph calendar-view round: from the start of the window (in full) or from the delta link.</summary>
    private async Task<CalendarSpan> GraphWindowAsync(
        AccountInfo account, CalendarTarget target, CalendarSpan? held, SyncCursor? cursor, DateTimeOffset from, DateTimeOffset to, string scope, DateTimeOffset now, CancellationToken ct)
    {
        var first = cursor is not null
            ? new Uri(cursor.Value)
            : new Uri($"{Graph}me/calendarView/delta?startDateTime={Uri.EscapeDataString(Iso(from))}&endDateTime={Uri.EscapeDataString(Iso(to))}");

        var round = await _accounts.GetDeltaAsync(account.Id, first, DeltaDialect.Graph, OfficeJsonContext.Default.GraphEvent, MaxEvents, ct,
            ("Prefer", "outlook.timezone=\"UTC\""), ("Prefer", "odata.maxpagesize=200")).ConfigureAwait(true);

        // A cancelled occurrence is not an event ([S-185]): it leaves like a deleted one. [S-3610] A
        // bare change notice is read again by id, or, when there are many, the window is read whole.
        var changes = OfficeWire.SortGraphChanges(round.Items, round.Removed);

        if (changes.ReadWindowInstead)
        {
            Log.Debug(Scope, $"{account.Id}: {changes.Reread.Count} calendar change notices; reading the window in full");
            var events = await LoadEventsAsync(target, from, to, ct).ConfigureAwait(true);
            var whole = CalendarSync.Merge(held, target.CalendarId, events, from, to, now, full: true);
            whole.Cursor = round.Cursor is not { } next || whole.Events.Count >= MaxEvents
                ? null
                : cursor is not null ? SyncCursors.Advance(cursor, next, now) : SyncCursors.Start(CursorKind.GraphDelta, next, scope, now);
            return whole;
        }

        var changed = changes.Changed.ToList();
        var removed = changes.Removed.ToList();

        foreach (var id in changes.Reread)
        {
            if (await RereadGraphEventAsync(account, id, ct).ConfigureAwait(true) is { } fresh)
            {
                changed.Add(fresh);
            }
            else
            {
                removed.Add(id);
            }
        }

        return Fold(held, cursor, target.CalendarId, changed, removed, round.Cursor, CursorKind.GraphDelta, from, to, scope, now);
    }

    /// <summary>
    /// [S-3610] One event a change round only named, read whole by id with the calendar view's fields.
    /// Null when it is gone (404) or is no event now (cancelled). Any other failure fails the round,
    /// so its cursor does not move past a change that was never read.
    /// </summary>
    private async Task<CalendarEvent?> RereadGraphEventAsync(AccountInfo account, string id, CancellationToken ct)
    {
        var url = new Uri($"{Graph}me/events/{Uri.EscapeDataString(id)}?$select={GraphEventFields}");

        try
        {
            var item = await _accounts.GetJsonAsync(account.Id, url, OfficeJsonContext.Default.GraphEvent, ct,
                ("Prefer", "outlook.timezone=\"UTC\"")).ConfigureAwait(true);
            return OfficeWire.FromGraph(item);
        }
        catch (AccountException ex) when (ex.Error == AccountError.NotFound)
        {
            // Gone since the round named it: it leaves like a removed one.
            return null;
        }
    }


    /// <summary>A Google events round: the window in full (no order, so Google can hand out a sync token), or from the token.</summary>
    private async Task<CalendarSpan> GoogleWindowAsync(
        AccountInfo account, CalendarTarget target, CalendarSpan? held, SyncCursor? cursor, DateTimeOffset from, DateTimeOffset to, string scope, DateTimeOffset now, CancellationToken ct)
    {
        var calendar = Uri.EscapeDataString(target.CalendarId ?? "primary");
        var query = cursor is not null
            ? $"syncToken={Uri.EscapeDataString(cursor.Value)}"
            : $"timeMin={Uri.EscapeDataString(Iso(from))}&timeMax={Uri.EscapeDataString(Iso(to))}";
        var first = new Uri($"{GoogleCalendar}calendars/{calendar}/events?{query}&singleEvents=true&maxResults=250");

        var round = await _accounts.GetDeltaAsync(account.Id, first, DeltaDialect.GoogleEvents, OfficeJsonContext.Default.GoogleEvent, MaxEvents, ct)
            .ConfigureAwait(true);

        var changed = round.Items.Select(OfficeWire.FromGoogle).OfType<CalendarEvent>().ToList();
        return Fold(held, cursor, target.CalendarId, changed, round.Removed, round.Cursor, CursorKind.GoogleSyncToken, from, to, scope, now);
    }

    /// <summary>A round's events into the span: replacing the window when it was read in full, folded in when it was a delta.</summary>
    private static CalendarSpan Fold(
        CalendarSpan? held, SyncCursor? cursor, string? calendarId, IReadOnlyList<CalendarEvent> changed, IReadOnlyList<string> removed,
        string? next, CursorKind kind, DateTimeOffset from, DateTimeOffset to, string scope, DateTimeOffset now)
    {
        if (cursor is not null && held is not null)
        {
            var span = DeltaMerge.Events(held, changed, removed, now);
            span.Cursor = next is null || span.Events.Count >= MaxEvents ? null : SyncCursors.Advance(cursor, next, now);
            return span;
        }

        var whole = CalendarSync.Merge(held, calendarId, changed, from, to, now, full: true);
        whole.Cursor = next is null || whole.Events.Count >= MaxEvents ? null : SyncCursors.Start(kind, next, scope, now);
        return whole;
    }

    /// <summary>[S-2688] A CalDAV window in full: the collection's tag first, then every resource with its etag.</summary>
    private async Task<CalendarSpan> DavWindowFullAsync(
        AccountInfo account, CalendarTarget target, CalendarSpan? held, DateTimeOffset from, DateTimeOffset to, string scope, DateTimeOffset now, CancellationToken ct)
    {
        var collection = DavTarget(account, target.CalendarId);
        var (ctag, token) = await CollectionTagAsync(account, collection, ct).ConfigureAwait(true);

        var reply = await _accounts.DavAsync(account.Id, "REPORT", collection, DavOffice.CalendarQuery(from, to), 1, ct).ConfigureAwait(true);
        var (tags, events) = ReadEvents(DavDelta.Items(reply.Body, DavKind.Calendar), from, to);

        var span = CalendarSync.Merge(held, target.CalendarId, events, from, to, now, full: true);
        span.Cursor = tags.Count <= SyncCursors.MaxResources && span.Events.Count < MaxEvents
            ? DavCursor(SyncCursors.Start(CursorKind.Dav, DavDelta.Tag(ctag, token) ?? string.Empty, scope, now), token, tags)
            : null;
        return span;
    }

    /// <summary>[S-2688] A CalDAV window's changes: nothing when the tag stands, else only the resources that changed.</summary>
    private async Task<CalendarSpan> DavWindowChangesAsync(
        AccountInfo account, CalendarTarget target, CalendarSpan held, SyncCursor cursor, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken ct)
    {
        var collection = DavTarget(account, target.CalendarId);
        var changes = await DavChangesAsync(account, collection, cursor, DavDelta.CalendarEtagQuery(from, to),
            hrefs => DavDelta.CalendarMultiget(hrefs, from, to), DavKind.Calendar, ct).ConfigureAwait(true);

        if (changes.Unchanged)
        {
            var same = DeltaMerge.Events(held, [], [], now);
            same.Cursor = SyncCursors.Advance(cursor, cursor.Value, now);
            return same;
        }

        var (fetched, events) = ReadEvents(changes.Fetched, from, to);
        var (tags, removed) = DeltaMerge.Resources(cursor.Resources ?? [], fetched, changes.Gone);

        var span = DeltaMerge.Events(held, events, removed, now);
        span.Cursor = tags.Count <= SyncCursors.MaxResources && span.Events.Count < MaxEvents
            ? DavCursor(SyncCursors.Advance(cursor, changes.Tag ?? string.Empty, now), changes.Token, tags)
            : null;
        return span;
    }

    /// <summary>
    /// Each resource's events over the window, and its tag. A resource with none in the window keeps
    /// a tag without ids, so the etag listing does not read it again until it changes.
    /// </summary>
    private static (List<DavTag> Tags, List<CalendarEvent> Events) ReadEvents(IReadOnlyList<DavItem> items, DateTimeOffset from, DateTimeOffset to)
    {
        var tags = new List<DavTag>();
        var events = new List<CalendarEvent>();

        foreach (var item in items)
        {
            var read = item.Data is { } data ? IcsEvents.Read(data, from, to, TimeZoneInfo.Local) : [];
            var ids = read.Select(e => e.Id).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Take(SyncCursors.MaxIdsPerResource).ToList();
            events.AddRange(read);

            // A resource read with no etag cannot be compared later; it is listed (and read) again
            // on the next change, which is correct, only slower.
            tags.Add(new DavTag { Href = item.Href, Etag = item.Etag, Ids = ids });
        }

        return (tags, events);
    }

    // -----------------------------------------------------------------------------------------
    // Address book
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-2685] An address book: the changes since its cursor when usable, else in full.</summary>
    private Task<(IReadOnlyList<Contact> People, SyncCursor? Cursor)> SyncBookAsync(string accountId, IReadOnlyList<Contact>? held, SyncCursor? cursor, CancellationToken ct)
    {
        var account = Require(accountId);
        if (account.Provider is not (AccountProvider.Microsoft or AccountProvider.Google))
        {
            return BookChangesAsync(account, held, cursor, ct);
        }

        // [S-2690] A book whose provider refused the change request: the plain listing, no cursor.
        return _fallback.ReadAsync($"{account.Id}|book", $"{account.Id}: address book",
            () => BookChangesAsync(account, held, cursor, ct),
            () => account.Provider == AccountProvider.Microsoft ? GraphBookPlainAsync(account, ct) : GoogleBookPlainAsync(account, ct));
    }

    private async Task<(IReadOnlyList<Contact> People, SyncCursor? Cursor)> BookChangesAsync(AccountInfo account, IReadOnlyList<Contact>? held, SyncCursor? cursor, CancellationToken ct)
    {
        CursorKind? kind = account.Provider switch
        {
            AccountProvider.Microsoft => CursorKind.GraphDelta,
            AccountProvider.Google => CursorKind.GoogleSyncToken,
            AccountProvider.CardDav => CursorKind.Dav,
            _ => null,
        };

        if (kind is not { } k)
        {
            throw new AccountException(AccountError.Refused, $"{account.Label} has no contacts.");
        }

        var now = DateTimeOffset.UtcNow;
        var check = held is null ? CursorCheck.Missing : SyncCursors.Check(cursor, k, BookScope, now);

        if (check == CursorCheck.Usable && held is not null && cursor is not null)
        {
            try
            {
                return k switch
                {
                    CursorKind.GraphDelta => await GraphBookRoundAsync(account, held, cursor, new Uri(cursor.Value), now, ct).ConfigureAwait(true),
                    CursorKind.GoogleSyncToken => await GoogleBookAsync(account, held, cursor, now, ct).ConfigureAwait(true),
                    _ => await DavBookChangesAsync(account, held, cursor, now, ct).ConfigureAwait(true),
                };
            }
            catch (AccountException ex) when (DeltaRules.Rejected(ex.HttpStatus, ex.ProviderMessage))
            {
                // [S-2681] Google People: "Sync token is expired" (tokens last seven days).
                Log.Info(Scope, $"{account.Id}: the address book refused its change cursor (HTTP {ex.HttpStatus}); reading it in full");
            }
        }
        else if (check is CursorCheck.Stale or CursorCheck.OtherScope)
        {
            Log.Debug(Scope, $"{account.Id}: address book read in full ({check})");
        }

        return k switch
        {
            CursorKind.GraphDelta => await GraphBookFullAsync(account, now, ct).ConfigureAwait(true),
            CursorKind.GoogleSyncToken => await GoogleBookAsync(account, null, null, now, ct).ConfigureAwait(true),
            _ => await DavBookFullAsync(account, now, ct).ConfigureAwait(true),
        };
    }

    private const string GraphContactFields = "id,displayName,emailAddresses,businessPhones,mobilePhone,homePhones,jobTitle,companyName";

    /// <summary>
    /// Graph in full: the first <see cref="OfficeShelves.MaxContacts"/> by name (as the widget lists
    /// them). When that is the whole book, its folder's delta round starts the cursor, and that
    /// round's answer — the book as it is now — is what is held.
    /// </summary>
    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> GraphBookFullAsync(AccountInfo account, DateTimeOffset now, CancellationToken ct)
    {
        var (people, folder) = await GraphBookListAsync(account, ct).ConfigureAwait(true);
        if (people.Count >= OfficeShelves.MaxContacts || folder is null)
        {
            return (people, null);
        }

        var first = new Uri($"{Graph}me/contactFolders/{Uri.EscapeDataString(folder)}/contacts/delta?$select={GraphContactFields}");
        return await GraphBookRoundAsync(account, [], null, first, now, ct).ConfigureAwait(true);
    }

    /// <summary>The first <see cref="OfficeShelves.MaxContacts"/> by name, and the folder they are in.</summary>
    private async Task<(IReadOnlyList<Contact> People, string? Folder)> GraphBookListAsync(AccountInfo account, CancellationToken ct)
    {
        var max = OfficeShelves.MaxContacts;
        var url = new Uri($"{Graph}me/contacts?$select={GraphContactFields},parentFolderId&$orderby=displayName&$top={max.ToString(CultureInfo.InvariantCulture)}");
        var items = await _accounts.GetGraphPagesAsync(account.Id, url, OfficeJsonContext.Default.GraphContact, max, ct).ConfigureAwait(true);
        return (Own(account, items.Select(OfficeWire.FromGraph).OfType<Contact>()), items.Select(i => i.ParentFolderId).FirstOrDefault(f => !string.IsNullOrEmpty(f)));
    }

    /// <summary>[S-2690] Graph's book the plain way: the listing alone.</summary>
    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> GraphBookPlainAsync(AccountInfo account, CancellationToken ct)
        => ((await GraphBookListAsync(account, ct).ConfigureAwait(true)).People, null);

    /// <summary>[S-2690] Google's book the plain way: the connections without asking for a sync token.</summary>
    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> GoogleBookPlainAsync(AccountInfo account, CancellationToken ct)
    {
        var url = new Uri($"{People}?personFields=names,emailAddresses,phoneNumbers,organizations&pageSize=1000&sortOrder=FIRST_NAME_ASCENDING");
        var items = await _accounts.GetGooglePagesAsync(account.Id, url, "connections", OfficeJsonContext.Default.GooglePersonRecord, OfficeShelves.MaxContacts, ct)
            .ConfigureAwait(true);
        var (people, _) = DeltaMerge.Contacts([], Own(account, items.Select(OfficeWire.FromGoogle).OfType<Contact>()), [], OfficeShelves.MaxContacts);
        return (people, null);
    }

    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> GraphBookRoundAsync(AccountInfo account, IReadOnlyList<Contact> held, SyncCursor? cursor, Uri first, DateTimeOffset now, CancellationToken ct)
    {
        var round = await _accounts.GetDeltaAsync(account.Id, first, DeltaDialect.Graph, OfficeJsonContext.Default.GraphContact, OfficeShelves.MaxContacts, ct,
            ("Prefer", "odata.maxpagesize=500")).ConfigureAwait(true);

        var changed = Own(account, round.Items.Select(OfficeWire.FromGraph).OfType<Contact>());
        var (people, whole) = DeltaMerge.Contacts(held, changed, round.Removed, OfficeShelves.MaxContacts);
        return (people, NextCursor(cursor, round.Cursor, whole, CursorKind.GraphDelta, now));
    }

    /// <summary>Google People: in full (asking for a sync token) or from the token; the same request either way.</summary>
    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> GoogleBookAsync(AccountInfo account, IReadOnlyList<Contact>? held, SyncCursor? cursor, DateTimeOffset now, CancellationToken ct)
    {
        var token = cursor is null ? string.Empty : $"&syncToken={Uri.EscapeDataString(cursor.Value)}";
        var url = new Uri($"{People}?personFields=names,emailAddresses,phoneNumbers,organizations&pageSize=1000&sortOrder=FIRST_NAME_ASCENDING&requestSyncToken=true{token}");

        var round = await _accounts.GetDeltaAsync(account.Id, url, DeltaDialect.GooglePeople, OfficeJsonContext.Default.GooglePersonRecord, OfficeShelves.MaxContacts, ct)
            .ConfigureAwait(true);

        var changed = Own(account, round.Items.Select(OfficeWire.FromGoogle).OfType<Contact>());
        var (people, whole) = DeltaMerge.Contacts(held ?? [], changed, round.Removed, OfficeShelves.MaxContacts);
        return (people, NextCursor(cursor, round.Cursor, whole, CursorKind.GoogleSyncToken, now));
    }

    private static SyncCursor? NextCursor(SyncCursor? cursor, string? next, bool whole, CursorKind kind, DateTimeOffset now)
        => next is null || !whole ? null
            : cursor is null ? SyncCursors.Start(kind, next, BookScope, now)
            : SyncCursors.Advance(cursor, next, now);

    /// <summary>[S-2688] A CardDAV book in full: the tag first, then every card with its etag.</summary>
    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> DavBookFullAsync(AccountInfo account, DateTimeOffset now, CancellationToken ct)
    {
        var (ctag, token) = await CollectionTagAsync(account, null, ct).ConfigureAwait(true);
        var reply = await _accounts.DavAsync(account.Id, "REPORT", null, DavOffice.AddressBookQuery, 1, ct).ConfigureAwait(true);
        var (tags, cards) = ReadCards(account, DavDelta.Items(reply.Body, DavKind.Contacts));

        var (people, whole) = DeltaMerge.Contacts([], cards, [], OfficeShelves.MaxContacts);
        var cursor = whole && tags.Count <= SyncCursors.MaxResources
            ? DavCursor(SyncCursors.Start(CursorKind.Dav, DavDelta.Tag(ctag, token) ?? string.Empty, BookScope, now), token, tags)
            : null;
        return (people, cursor);
    }

    private async Task<(IReadOnlyList<Contact>, SyncCursor?)> DavBookChangesAsync(AccountInfo account, IReadOnlyList<Contact> held, SyncCursor cursor, DateTimeOffset now, CancellationToken ct)
    {
        var changes = await DavChangesAsync(account, null, cursor, DavDelta.AddressEtagQuery, DavDelta.AddressMultiget, DavKind.Contacts, ct).ConfigureAwait(true);
        if (changes.Unchanged)
        {
            return (held, SyncCursors.Advance(cursor, cursor.Value, now));
        }

        var (fetched, cards) = ReadCards(account, changes.Fetched);
        var (tags, removed) = DeltaMerge.Resources(cursor.Resources ?? [], fetched, changes.Gone);
        var (people, whole) = DeltaMerge.Contacts(held, cards, removed, OfficeShelves.MaxContacts);

        var next = whole && tags.Count <= SyncCursors.MaxResources
            ? DavCursor(SyncCursors.Advance(cursor, changes.Tag ?? string.Empty, now), changes.Token, tags)
            : null;
        return (people, next);
    }

    /// <summary>
    /// The people on each card, and its tag. A card without a UID gets an id from where it lives, so
    /// the same card is the same person on every read (an index would shift).
    /// </summary>
    private static (List<DavTag> Tags, List<Contact> People) ReadCards(AccountInfo account, IReadOnlyList<DavItem> items)
    {
        var tags = new List<DavTag>();
        var people = new List<Contact>();

        foreach (var item in items)
        {
            var cards = item.Data is { } data ? VCards.Read(data) : [];
            var read = cards.Select((c, i) => c.Contact.Id.StartsWith("carddav:", StringComparison.Ordinal)
                    ? c.Contact with { Id = $"carddav:{item.Href}#{i.ToString(CultureInfo.InvariantCulture)}" }
                    : c.Contact)
                .Select(c => c with { AccountId = account.Id })
                .ToList();

            people.AddRange(read);
            tags.Add(new DavTag { Href = item.Href, Etag = item.Etag, Ids = [.. read.Select(c => c.Id).Distinct(StringComparer.Ordinal).Take(SyncCursors.MaxIdsPerResource)] });
        }

        return (tags, people);
    }

    private static List<Contact> Own(AccountInfo account, IEnumerable<Contact> contacts) => [.. contacts.Select(c => c with { AccountId = account.Id })];

    // -----------------------------------------------------------------------------------------
    // DAV plumbing
    // -----------------------------------------------------------------------------------------

    /// <summary>What changed in a DAV collection since its cursor.</summary>
    /// <param name="Unchanged">The collection's tag stands: nothing was read.</param>
    private sealed record DavChangeSet(bool Unchanged, string? Tag, string? Token, IReadOnlyList<DavItem> Fetched, IReadOnlyList<string> Gone);

    /// <summary>
    /// [S-2688] The collection's tag; when it moved, which resources changed — through the
    /// sync-collection REPORT when both sides have a sync-token (falling back to the etag listing
    /// when the server refuses the token), else the etag listing — and those resources read through
    /// a multiget, a hundred at a time. A resource the multiget does not return is gone.
    /// </summary>
    private async Task<DavChangeSet> DavChangesAsync(
        AccountInfo account, Uri? collection, SyncCursor cursor, string etagQuery, Func<IEnumerable<string>, string> multiget, DavKind kind, CancellationToken ct)
    {
        var (ctag, token) = await CollectionTagAsync(account, collection, ct).ConfigureAwait(true);
        var tag = DavDelta.Tag(ctag, token);

        if (tag is not null && cursor.Value.Length > 0 && string.Equals(tag, cursor.Value, StringComparison.Ordinal))
        {
            return new DavChangeSet(true, tag, token, [], []);
        }

        var held = cursor.Resources ?? [];
        IReadOnlyList<string>? changed = null;
        IReadOnlyList<string> gone = [];
        var nextToken = token;

        if (cursor.DavToken is { } since && token is not null)
        {
            try
            {
                var reply = await _accounts.DavAsync(account.Id, "REPORT", collection, DavDelta.SyncCollection(since), 0, ct).ConfigureAwait(true);
                var sync = DavDelta.SyncChanges(reply.Body);
                var etags = held.ToDictionary(t => t.Href, t => t.Etag, StringComparer.Ordinal);
                changed = [.. sync.Changed.Where(c => c.Etag.Length == 0 || !etags.TryGetValue(c.Href, out var e) || e != c.Etag).Select(c => c.Href)];
                gone = sync.Gone;
                nextToken = sync.Token ?? token;
            }
            catch (AccountException ex) when (DavDelta.TokenRefused(ex.HttpStatus))
            {
                Log.Info(Scope, $"{account.Id}: the server refused its sync-token (HTTP {ex.HttpStatus}); comparing etags instead");
            }
        }

        if (changed is null)
        {
            var listing = await _accounts.DavAsync(account.Id, "REPORT", collection, etagQuery, 1, ct).ConfigureAwait(true);
            var diff = DeltaMerge.Diff(held, [.. DavDelta.Items(listing.Body, kind).Select(i => (i.Href, i.Etag))]);
            changed = diff.Changed;
            gone = diff.Gone;
        }

        var fetched = new List<DavItem>();
        var missing = new List<string>(gone);

        foreach (var batch in changed.Chunk(DavDelta.MultigetBatch))
        {
            var reply = await _accounts.DavAsync(account.Id, "REPORT", collection, multiget(batch), 1, ct).ConfigureAwait(true);
            var items = DavDelta.Items(reply.Body, kind).Where(i => i.Data is not null).ToList();
            var returned = new HashSet<string>(items.Select(i => i.Href), StringComparer.Ordinal);

            fetched.AddRange(items);
            missing.AddRange(batch.Where(h => !returned.Contains(h)));
        }

        return new DavChangeSet(false, tag, nextToken, fetched, missing);
    }

    /// <summary>The collection's getctag and sync-token (Depth 0); both null when the server offers neither.</summary>
    private async Task<(string? Ctag, string? Token)> CollectionTagAsync(AccountInfo account, Uri? collection, CancellationToken ct)
    {
        var reply = await _accounts.DavAsync(account.Id, "PROPFIND", collection, DavDelta.CollectionTagBody, 0, ct).ConfigureAwait(true);
        return DavDelta.CollectionTag(reply.Body);
    }

    private static SyncCursor DavCursor(SyncCursor cursor, string? token, List<DavTag> tags)
    {
        cursor.DavToken = token;
        cursor.Resources = tags;
        return cursor;
    }
}
