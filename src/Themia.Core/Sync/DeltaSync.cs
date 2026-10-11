using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;

namespace Themia.Core.Sync;

/// <summary>[S-2680] What a provider's change cursor is, so a stored one is only ever used the way it was issued.</summary>
public enum CursorKind
{
    /// <summary>A Microsoft Graph <c>@odata.deltaLink</c> (calendar view, contacts, messages, To Do tasks).</summary>
    GraphDelta = 1,

    /// <summary>A Google <c>nextSyncToken</c> (Calendar events, People connections).</summary>
    GoogleSyncToken = 2,

    /// <summary>A Google Tasks <c>updatedMin</c> instant (Tasks has no sync token).</summary>
    GoogleUpdatedMin = 3,

    /// <summary>A CalDAV / CardDAV collection tag (<c>getctag</c>, else the <c>sync-token</c>) with the etag of every resource held.</summary>
    Dav = 4,

    /// <summary>An IMAP folder's UIDVALIDITY, UIDNEXT, EXISTS and (CONDSTORE) HIGHESTMODSEQ.</summary>
    Imap = 5,
}

/// <summary>
/// [S-2680] Where a provider said "ask me again from here": stored in the owner's shelf next to what
/// it describes (a calendar's span, a contacts book, a mail folder, a task list), sealed with it.
/// </summary>
public sealed class SyncCursor
{
    /// <summary>The cursor's own format; a cursor of another format is dropped on load (a full fetch follows).</summary>
    public const int Version = 1;

    public int Format { get; set; } = Version;

    public CursorKind Kind { get; set; }

    /// <summary>The token, delta link, instant or collection tag; empty for IMAP.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// What it was issued for (a calendar window, a mail folder and its date filter, a task list and
    /// whether finished tasks are held). A cursor for another scope does not describe what is held.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>When the full fetch this chain of deltas grew from ran ([S-2682]: the daily resync counts from it).</summary>
    public DateTimeOffset Since { get; set; }

    /// <summary>When it last moved on.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>DAV: the RFC 6578 sync-token, when the collection offers one.</summary>
    public string? DavToken { get; set; }

    /// <summary>DAV: every resource held, with its etag and the items read from it.</summary>
    public List<DavTag>? Resources { get; set; }

    /// <summary>IMAP: the folder's state when it was last read.</summary>
    public ImapMark? Imap { get; set; }
}

/// <summary>One DAV resource as last read: where, which version, and which events or people came out of it.</summary>
public sealed class DavTag
{
    public string Href { get; set; } = string.Empty;

    public string Etag { get; set; } = string.Empty;

    public List<string> Ids { get; set; } = [];
}

/// <summary>An IMAP folder as last read ([S-2689]).</summary>
public sealed class ImapMark
{
    public uint UidValidity { get; set; }

    public uint UidNext { get; set; }

    public long Exists { get; set; }

    /// <summary>Null when the server does not offer CONDSTORE (or said NOMODSEQ).</summary>
    public ulong? HighestModSeq { get; set; }
}

/// <summary>Whether a stored cursor may be used for the next sync.</summary>
public enum CursorCheck
{
    Usable,

    /// <summary>None stored: the first sync, or the provider never offered one.</summary>
    Missing,

    /// <summary>Of another kind or issued for another scope (the window moved on, the filter changed).</summary>
    OtherScope,

    /// <summary>The daily full resync is due ([S-2682]).</summary>
    Stale,
}

/// <summary>[S-2680]..[S-2682] Validating, checking and moving cursors on.</summary>
public static class SyncCursors
{
    /// <summary>
    /// [S-2682] However well deltas go, the whole of what is held is fetched again this often: a
    /// safety net for a change a provider never reported (a server-side restore, a bug on either
    /// side). A day: the calendar window moves on daily anyway, and every provider's token outlives it.
    /// </summary>
    public static readonly TimeSpan ResyncAfter = TimeSpan.FromHours(24);

    public const int MaxValue = 16 * 1024;
    public const int MaxScope = 2048;
    public const int MaxResources = OfficeShelves.MaxEvents;
    public const int MaxHref = 2048;
    public const int MaxEtag = 512;
    public const int MaxIdsPerResource = 1000;

    public static SyncCursor Start(CursorKind kind, string value, string scope, DateTimeOffset now) => new()
    {
        Kind = kind,
        Value = value,
        Scope = scope,
        Since = now,
        At = now,
    };

    /// <summary>The same chain, moved on: the full fetch it grew from stays its <see cref="SyncCursor.Since"/>.</summary>
    public static SyncCursor Advance(SyncCursor cursor, string value, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        return new SyncCursor
        {
            Kind = cursor.Kind,
            Value = value,
            Scope = cursor.Scope,
            Since = cursor.Since,
            At = now,
            DavToken = cursor.DavToken,
            Resources = cursor.Resources,
            Imap = cursor.Imap,
        };
    }

    /// <summary>[S-2681] Whether a cursor is usable for <paramref name="kind"/> over <paramref name="scope"/> now.</summary>
    public static CursorCheck Check(SyncCursor? cursor, CursorKind kind, string scope, DateTimeOffset now)
    {
        if (cursor is null)
        {
            return CursorCheck.Missing;
        }

        if (cursor.Kind != kind || !string.Equals(cursor.Scope, scope, StringComparison.Ordinal))
        {
            return CursorCheck.OtherScope;
        }

        // A chain started in the future (the clock moved back) is as unknown as an old one.
        return cursor.Since > now || now - cursor.Since >= ResyncAfter ? CursorCheck.Stale : CursorCheck.Usable;
    }

    /// <summary>
    /// [S-2681] A cursor read from a shelf: of this format and a known kind, bounded, with the parts
    /// its kind needs. A Graph delta link must be an https address on Graph itself, since the
    /// account's token is sent to it. Anything else is dropped (counted) and the owner fetches in full.
    /// </summary>
    public static SyncCursor? Validate(SyncCursor? cursor, ref int dropped)
    {
        if (cursor is null)
        {
            return null;
        }

        if (!Valid(cursor))
        {
            dropped++;
            return null;
        }

        return cursor;
    }

    private static bool Valid(SyncCursor cursor)
    {
        if (cursor.Format != SyncCursor.Version || !Enum.IsDefined(cursor.Kind)
            || cursor.Value is null || cursor.Value.Length > MaxValue
            || cursor.Scope is null || cursor.Scope.Length > MaxScope
            || cursor.Since == default || cursor.At < cursor.Since)
        {
            return false;
        }

        switch (cursor.Kind)
        {
            case CursorKind.GraphDelta:
                return IsGraphLink(cursor.Value);

            case CursorKind.GoogleSyncToken:
                return cursor.Value.Length > 0;

            case CursorKind.GoogleUpdatedMin:
                return DateTimeOffset.TryParse(cursor.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

            case CursorKind.Dav:
                return cursor.Resources is { } resources && ValidResources(resources)
                    && cursor.DavToken is not { Length: > MaxValue };

            case CursorKind.Imap:
                return cursor.Imap is { UidValidity: > 0, UidNext: > 0, Exists: >= 0 };

            default:
                return false;
        }
    }

    private static bool ValidResources(List<DavTag> resources)
    {
        if (resources.Count > MaxResources)
        {
            return false;
        }

        var hrefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in resources)
        {
            if (tag is null || string.IsNullOrEmpty(tag.Href) || tag.Href.Length > MaxHref || !hrefs.Add(tag.Href)
                || tag.Etag is null || tag.Etag.Length > MaxEtag
                || tag.Ids is null || tag.Ids.Count > MaxIdsPerResource || tag.Ids.Any(string.IsNullOrEmpty))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A delta link the account's bearer token may be sent to: https on graph.microsoft.com, no user info.</summary>
    public static bool IsGraphLink(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var url)
            && url.Scheme == Uri.UriSchemeHttps
            && string.Equals(url.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase)
            && url.IsDefaultPort
            && url.UserInfo.Length == 0;
}

/// <summary>How a provider's change list is written.</summary>
public enum DeltaDialect
{
    /// <summary>Graph: <c>value</c>, <c>@removed</c>, <c>@odata.nextLink</c>, <c>@odata.deltaLink</c>.</summary>
    Graph,

    /// <summary>Google Calendar: <c>items</c>, <c>status: cancelled</c>, <c>nextPageToken</c>, <c>nextSyncToken</c>.</summary>
    GoogleEvents,

    /// <summary>Google People: <c>connections</c>, <c>metadata.deleted</c>, <c>nextPageToken</c>, <c>nextSyncToken</c>.</summary>
    GooglePeople,

    /// <summary>Google Tasks: <c>items</c>, <c>deleted</c> / <c>hidden</c>, <c>nextPageToken</c>; no token.</summary>
    GoogleTasks,
}

/// <summary>One page of a change list: what changed, what went, and where to go next.</summary>
/// <param name="Cursor">The delta link or sync token, on the last page only.</param>
public sealed record DeltaPage<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Removed, string? NextLink, string? NextPageToken, string? Cursor);

/// <summary>
/// A whole round of pages. <paramref name="Complete"/> is false when the round did not reach its last
/// page (the bound or the page ceiling was hit): what came is used, but nothing can follow from it.
/// <paramref name="Cursor"/> is the last page's cursor, null when incomplete or none was offered.
/// </summary>
public sealed record DeltaRound<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Removed, string? Cursor, bool Complete);

/// <summary>[S-2683] Reading the providers' change pages: the trust boundary for every delta answer.</summary>
public static class DeltaPages
{
    /// <summary>
    /// Read one page. Items that are not objects are skipped; a removed entry without an id is
    /// skipped; a Graph delta link that is not on Graph is not a cursor. A body that is not a JSON
    /// object throws <see cref="JsonException"/>.
    /// </summary>
    public static DeltaPage<T> Read<T>(string body, DeltaDialect dialect, JsonTypeInfo<T> item)
    {
        ArgumentNullException.ThrowIfNull(item);

        using var document = JsonDocument.Parse(body ?? string.Empty);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("a change page that is not an object");
        }

        var items = new List<T>();
        var removed = new List<string>();

        if (root.TryGetProperty(ItemsProperty(dialect), out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (IsRemoved(element, dialect))
                {
                    if (Text(element, dialect == DeltaDialect.GooglePeople ? "resourceName" : "id") is { Length: > 0 } id)
                    {
                        removed.Add(id);
                    }

                    continue;
                }

                if (element.Deserialize(item) is { } value)
                {
                    items.Add(value);
                }
            }
        }

        string? nextLink = null, pageToken = null, cursor = null;

        if (dialect == DeltaDialect.Graph)
        {
            nextLink = Text(root, "@odata.nextLink");
            cursor = Text(root, "@odata.deltaLink") is { } link && link.Length <= SyncCursors.MaxValue && SyncCursors.IsGraphLink(link) ? link : null;
        }
        else
        {
            pageToken = Text(root, "nextPageToken") is { Length: > 0 } token ? token : null;
            cursor = dialect != DeltaDialect.GoogleTasks && Text(root, "nextSyncToken") is { Length: > 0 and <= SyncCursors.MaxValue } sync ? sync : null;
        }

        // A cursor rides only on the last page; one beside a next page is not the end of the round.
        if (nextLink is not null || pageToken is not null)
        {
            cursor = null;
        }

        return new DeltaPage<T>(items, removed, nextLink, pageToken, cursor);
    }

    private static string ItemsProperty(DeltaDialect dialect) => dialect switch
    {
        DeltaDialect.Graph => "value",
        DeltaDialect.GooglePeople => "connections",
        _ => "items",
    };

    private static bool IsRemoved(JsonElement element, DeltaDialect dialect) => dialect switch
    {
        DeltaDialect.Graph => element.TryGetProperty("@removed", out _),
        DeltaDialect.GoogleEvents => string.Equals(Text(element, "status"), "cancelled", StringComparison.OrdinalIgnoreCase),
        DeltaDialect.GooglePeople => element.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True,
        _ => Flag(element, "deleted") || Flag(element, "hidden"),
    };

    private static bool Flag(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>[S-2681] What a provider's refusal of a cursor looks like.</summary>
public static class DeltaRules
{
    /// <summary>
    /// How far before a request an instant cursor (Google Tasks <c>updatedMin</c>) is set: a change
    /// the provider stamped while the request was out, or by a clock a little behind this one, is
    /// asked for again rather than missed. Asking for a change twice is harmless; merges upsert.
    /// </summary>
    public static readonly TimeSpan ClockOverlap = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether a failed request that carried a cursor failed because of the cursor: 410 Gone (Google
    /// Calendar's "sync token invalidated", Graph's expired delta token), or a 400 that names the
    /// token (Google People's "Sync token is expired", Graph's "badly formed token"). Such a failure
    /// is not the account's: the cursor is dropped and the owner fetches in full at once.
    /// </summary>
    public static bool Rejected(int status, string? providerMessage)
    {
        if (status == 410)
        {
            return true;
        }

        return status == 400 && providerMessage is { } said
            && (said.Contains("token", StringComparison.OrdinalIgnoreCase) || said.Contains("sync", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// [S-2690] Whether a change request (a delta, a sync token, an <c>updatedMin</c>) was refused as a
    /// request: 400 (Graph's bare "Invalid request" for a To Do list it will not track), 405 or 501.
    /// Not the account's failure and not the data's: the same thing is read the plain way instead.
    /// A cursor refusal ([S-2681], <see cref="Rejected"/>) is tried first, by a fresh round.
    /// </summary>
    public static bool Unsupported(int status) => status is 400 or 405 or 501;
}

/// <summary>
/// [S-2690] Change requests a provider refused for one thing it holds (a To Do list, a calendar, a
/// book, a folder), remembered for the session, so that thing is read the plain way — as before
/// incremental sync — from then on instead of being skipped, or asked again every cycle. One per
/// service; keys name the account and the thing.
/// </summary>
public sealed class DeltaFallback(string logScope)
{
    private readonly ConcurrentDictionary<string, byte> _refused = new(StringComparer.Ordinal);

    /// <summary>Whether a change request for <paramref name="key"/> was refused this session.</summary>
    public bool Refused(string key) => _refused.ContainsKey(key);

    /// <summary>
    /// <paramref name="delta"/> unless <paramref name="key"/> refused one before; on a refusal
    /// (<see cref="DeltaRules.Unsupported"/>), logged once, <paramref name="plain"/>. A failure of
    /// the plain read is the caller's, as any other failure of the delta is.
    /// </summary>
    /// <param name="what">The thing, for the log (an account id and a list id; ids, never names).</param>
    public async Task<T> ReadAsync<T>(string key, string what, Func<Task<T>> delta, Func<Task<T>> plain)
    {
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(plain);

        if (!_refused.ContainsKey(key))
        {
            try
            {
                // ConfigureAwait(true): callers on the UI thread run the plain read on it too.
                return await delta().ConfigureAwait(true);
            }
            catch (AccountException ex) when (DeltaRules.Unsupported(ex.HttpStatus))
            {
                if (_refused.TryAdd(key, 0))
                {
                    Log.Info(logScope, $"{what} refused its change request (HTTP {ex.HttpStatus}); read in full from now on");
                }
            }
        }

        return await plain().ConfigureAwait(true);
    }
}
