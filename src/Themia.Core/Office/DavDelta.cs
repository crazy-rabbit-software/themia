using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Themia.Core.Accounts;

namespace Themia.Core.Office;

/// <summary>One DAV resource as read: where, which version, and its iCalendar or vCard text (null when only the etag was asked).</summary>
public sealed record DavItem(string Href, string Etag, string? Data);

/// <summary>An RFC 6578 sync-collection answer: resources changed (with their etags), resources gone, and the next token.</summary>
public sealed record DavChanges(IReadOnlyList<(string Href, string Etag)> Changed, IReadOnlyList<string> Gone, string? Token);

/// <summary>
/// [S-2688] The CalDAV / CardDAV requests of an incremental sync and how their answers are read:
/// the collection's tag (<c>getctag</c>, RFC 6578 <c>sync-token</c>), an etag-only listing, a
/// sync-collection REPORT and a multiget of the resources that changed. Pure; the account service
/// sends them.
/// </summary>
public static class DavDelta
{
    /// <summary>The most resources one multiget asks for.</summary>
    public const int MultigetBatch = 100;

    /// <summary>A Depth-0 PROPFIND for what changes whenever anything in the collection does.</summary>
    public const string CollectionTagBody = """
        <?xml version="1.0" encoding="UTF-8"?>
        <D:propfind xmlns:D="DAV:" xmlns:CS="http://calendarserver.org/ns/">
          <D:prop><CS:getctag/><D:sync-token/></D:prop>
        </D:propfind>
        """;

    /// <summary>The collection's <c>getctag</c> and <c>sync-token</c>, each null when the server offers none.</summary>
    public static (string? Ctag, string? SyncToken) CollectionTag(string? multistatus)
    {
        foreach (var response in DavXml.Responses(multistatus))
        {
            var ctag = Bounded(response.Prop("getctag")?.Value);
            var token = Bounded(response.Prop("sync-token")?.Value);
            if (ctag is not null || token is not null)
            {
                return (ctag, token);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// What says "the collection changed": the ctag, else the sync-token (which also changes with
    /// every change). Null when the server offers neither, so every sync lists the etags.
    /// </summary>
    public static string? Tag(string? ctag, string? syncToken) => ctag ?? syncToken;

    /// <summary>A calendar-query for the etags of the VEVENTs overlapping a window, without their data.</summary>
    public static string CalendarEtagQuery(DateTimeOffset from, DateTimeOffset to)
    {
        var start = Utc(from);
        var end = Utc(to);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <C:calendar-query xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:D="DAV:">
              <D:prop><D:getetag/></D:prop>
              <C:filter>
                <C:comp-filter name="VCALENDAR">
                  <C:comp-filter name="VEVENT">
                    <C:time-range start="{start}" end="{end}"/>
                  </C:comp-filter>
                </C:comp-filter>
              </C:filter>
            </C:calendar-query>
            """;
    }

    /// <summary>An addressbook-query for every card's etag, without the cards.</summary>
    public const string AddressEtagQuery = """
        <?xml version="1.0" encoding="UTF-8"?>
        <C:addressbook-query xmlns:C="urn:ietf:params:xml:ns:carddav" xmlns:D="DAV:">
          <D:prop><D:getetag/></D:prop>
          <C:filter/>
        </C:addressbook-query>
        """;

    /// <summary>An RFC 6578 sync-collection REPORT from <paramref name="token"/>, members only, etags only.</summary>
    public static string SyncCollection(string token) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <D:sync-collection xmlns:D="DAV:">
          <D:sync-token>{Escape(token)}</D:sync-token>
          <D:sync-level>1</D:sync-level>
          <D:prop><D:getetag/></D:prop>
        </D:sync-collection>
        """;

    /// <summary>A calendar-multiget for some resources, their instances expanded over the window like the calendar-query.</summary>
    public static string CalendarMultiget(IEnumerable<string> hrefs, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(hrefs);
        var body = new StringBuilder();
        body.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n')
            .Append("""<C:calendar-multiget xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:D="DAV:">""")
            .Append(CultureInfo.InvariantCulture, $"""<D:prop><D:getetag/><C:calendar-data><C:expand start="{Utc(from)}" end="{Utc(to)}"/></C:calendar-data></D:prop>""");
        foreach (var href in hrefs)
        {
            body.Append("<D:href>").Append(Escape(href)).Append("</D:href>");
        }

        return body.Append("</C:calendar-multiget>").ToString();
    }

    /// <summary>An addressbook-multiget for some cards.</summary>
    public static string AddressMultiget(IEnumerable<string> hrefs)
    {
        ArgumentNullException.ThrowIfNull(hrefs);
        var body = new StringBuilder();
        body.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n')
            .Append("""<C:addressbook-multiget xmlns:C="urn:ietf:params:xml:ns:carddav" xmlns:D="DAV:">""")
            .Append("<D:prop><D:getetag/><C:address-data/></D:prop>");
        foreach (var href in hrefs)
        {
            body.Append("<D:href>").Append(Escape(href)).Append("</D:href>");
        }

        return body.Append("</C:addressbook-multiget>").ToString();
    }

    /// <summary>
    /// The member resources of a query or multiget answer: href, etag (empty when not given) and the
    /// <c>calendar-data</c> / <c>address-data</c>. The collection itself (an href ending in a slash)
    /// and repeats are skipped; hrefs are kept exactly as the server wrote them, since they are sent back.
    /// </summary>
    public static IReadOnlyList<DavItem> Items(string? multistatus, DavKind kind)
    {
        var data = kind == DavKind.Calendar ? "calendar-data" : "address-data";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<DavItem>();

        foreach (var response in DavXml.Responses(multistatus))
        {
            if (response.Href.EndsWith('/') || response.Href.Length > Sync.SyncCursors.MaxHref || !seen.Add(response.Href))
            {
                continue;
            }

            var etag = Bounded(response.Prop("getetag")?.Value) ?? string.Empty;
            var text = response.Prop(data)?.Value;
            items.Add(new DavItem(response.Href, etag.Length > Sync.SyncCursors.MaxEtag ? string.Empty : etag, string.IsNullOrWhiteSpace(text) ? null : text));
        }

        return items;
    }

    /// <summary>
    /// A sync-collection answer: a member with a 2xx propstat changed (its etag, empty when none
    /// was given); a member whose response status is 404 is gone; the multistatus' own
    /// <c>sync-token</c> is the next token.
    /// </summary>
    public static DavChanges SyncChanges(string? multistatus)
    {
        if (DavXml.Document(multistatus) is not { } document || document.Root is not { } root)
        {
            return new DavChanges([], [], null);
        }

        var changed = new List<(string, string)>();
        var gone = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var response in root.Elements().Where(e => e.Name.LocalName == "response"))
        {
            var href = response.Elements().FirstOrDefault(e => e.Name.LocalName == "href")?.Value.Trim();
            if (string.IsNullOrEmpty(href) || href.EndsWith('/') || href.Length > Sync.SyncCursors.MaxHref || !seen.Add(href))
            {
                continue;
            }

            if (DavXml.StatusCode(response.Elements().FirstOrDefault(e => e.Name.LocalName == "status")?.Value) is 404 or 410)
            {
                gone.Add(href);
                continue;
            }

            var etag = response.Elements().Where(e => e.Name.LocalName == "propstat")
                .Where(p => (DavXml.StatusCode(p.Elements().FirstOrDefault(e => e.Name.LocalName == "status")?.Value) ?? 200) is >= 200 and < 300)
                .SelectMany(p => p.Elements().Where(e => e.Name.LocalName == "prop").Elements())
                .FirstOrDefault(e => e.Name.LocalName == "getetag")?.Value.Trim() ?? string.Empty;

            changed.Add((href, etag.Length > Sync.SyncCursors.MaxEtag ? string.Empty : etag));
        }

        var token = Bounded(root.Elements().FirstOrDefault(e => e.Name.LocalName == "sync-token")?.Value);
        return new DavChanges(changed, gone, token);
    }

    /// <summary>
    /// Whether a sync-collection was refused because of its token (RFC 6578 §3.2
    /// <c>valid-sync-token</c> is a 403 or 409; some servers answer 400, 410 or 412) or because the
    /// change set was too big to send (507). Either way the etag listing takes over.
    /// </summary>
    public static bool TokenRefused(int status) => status is 400 or 403 or 409 or 410 or 412 or 507;

    private static string? Bounded(string? value)
        => value?.Trim() is { Length: > 0 and <= Sync.SyncCursors.MaxValue } trimmed ? trimmed : null;

    private static string Utc(DateTimeOffset moment)
        => moment.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
