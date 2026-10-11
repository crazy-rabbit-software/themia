using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Themia.Core.Localization;

namespace Themia.Core.Accounts;

/// <summary>Which half of DAV: calendars or address books.</summary>
public enum DavKind
{
    Calendar,
    Contacts,
}

/// <summary>One <c>response</c> of a multistatus: its href and the properties that came back 2xx.</summary>
public sealed record DavResponse(string Href, IReadOnlyList<XElement> Props)
{
    /// <summary>The first property with this local name, whatever its namespace prefix.</summary>
    public XElement? Prop(string localName)
        => Props.FirstOrDefault(p => string.Equals(p.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether <c>resourcetype</c> carries a child with this local name.</summary>
    public bool HasResourceType(string localName)
        => Prop("resourcetype")?.Elements().Any(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>The <c>href</c> inside a property such as <c>current-user-principal</c>.</summary>
    public string? HrefIn(string localName)
        => Prop(localName)?.Elements().FirstOrDefault(e => e.Name.LocalName == "href")?.Value.Trim() is { Length: > 0 } href
            ? href
            : null;
}

/// <summary>A calendar or address book found under a home set.</summary>
/// <param name="Href">The collection's href as the server wrote it; resolve against the reply's URL.</param>
/// <param name="Name">Its display name, or the last path segment when it has none.</param>
/// <param name="Color">The Apple <c>calendar-color</c> as given, or null.</param>
public sealed record DavCollection(string Href, string Name, string? Color);

/// <summary>
/// [S-515] Reading a WebDAV multistatus.
/// <para>
/// By local name, not by prefix: servers bind <c>DAV:</c> to <c>d:</c>, <c>D:</c> or no prefix at
/// all, and the classic app's prefix-insensitive scanners existed for exactly that. A body that is
/// not XML — a proxy's error page, an HTML login — reads as no responses rather than throwing, since
/// the status has already said what it could. DTDs are refused, so a hostile server cannot make the
/// parser expand entities.
/// </para>
/// </summary>
public static class DavXml
{
    public static IReadOnlyList<DavResponse> Responses(string? xml)
    {
        if (Parse(xml) is not { } document)
        {
            return [];
        }

        var responses = new List<DavResponse>();

        foreach (var response in document.Descendants().Where(e => e.Name.LocalName == "response"))
        {
            var href = response.Elements().FirstOrDefault(e => e.Name.LocalName == "href")?.Value.Trim();
            if (string.IsNullOrEmpty(href))
            {
                continue;
            }

            var props = new List<XElement>();
            foreach (var propstat in response.Elements().Where(e => e.Name.LocalName == "propstat"))
            {
                // A property the server could not give comes back under a 404 propstat; it is absent,
                // not empty, and must not satisfy a "has calendar-home-set" test.
                var status = StatusCode(propstat.Elements().FirstOrDefault(e => e.Name.LocalName == "status")?.Value) ?? 200;
                if (status is < 200 or >= 300)
                {
                    continue;
                }

                if (propstat.Elements().FirstOrDefault(e => e.Name.LocalName == "prop") is { } prop)
                {
                    props.AddRange(prop.Elements());
                }
            }

            responses.Add(new DavResponse(href, props));
        }

        return responses;
    }

    /// <summary>The <c>current-user-principal</c> href, if any response carries one.</summary>
    public static string? Principal(string? xml)
        => Responses(xml).Select(r => r.HrefIn("current-user-principal")).FirstOrDefault(h => h is not null);

    /// <summary>The calendar or address-book home-set href, if any response carries one.</summary>
    public static string? HomeSet(string? xml, DavKind kind)
        => Responses(xml).Select(r => r.HrefIn(HomeSetName(kind))).FirstOrDefault(h => h is not null);

    /// <summary>Whether a Depth-0 answer says the resource asked about is itself a usable collection.</summary>
    public static bool IsCollection(string? xml, DavKind kind)
        => Responses(xml).Any(r => r.HasResourceType(CollectionType(kind)));

    /// <summary>
    /// The usable collections in a Depth-1 home-set listing, in document order. For calendars, one
    /// that advertises its components must include VEVENT (a tasks-only or birthday collection is not
    /// a calendar of events); one that advertises nothing is accepted.
    /// </summary>
    public static IReadOnlyList<DavCollection> Collections(string? xml, DavKind kind)
    {
        var found = new List<DavCollection>();

        foreach (var response in Responses(xml))
        {
            if (!response.HasResourceType(CollectionType(kind)))
            {
                continue;
            }

            if (kind == DavKind.Calendar
                && response.Prop("supported-calendar-component-set") is { } components
                && !components.Elements().Any(c => string.Equals((string?)c.Attribute("name"), "VEVENT", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var name = response.Prop("displayname")?.Value.Trim();
            var color = response.Prop("calendar-color")?.Value.Trim();

            found.Add(new DavCollection(
                response.Href,
                string.IsNullOrEmpty(name) ? LastSegment(response.Href) : name,
                string.IsNullOrEmpty(color) ? null : color));
        }

        return found;
    }

    /// <summary>The number in a status line such as <c>HTTP/1.1 404 Not Found</c>.</summary>
    public static int? StatusCode(string? line)
    {
        foreach (var part in (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var status) && status is >= 100 and < 600)
            {
                return status;
            }
        }

        return null;
    }

    public static string CollectionType(DavKind kind) => kind == DavKind.Calendar ? "calendar" : "addressbook";

    public static string HomeSetName(DavKind kind) => kind == DavKind.Calendar ? "calendar-home-set" : "addressbook-home-set";

    public static string Namespace(DavKind kind)
        => kind == DavKind.Calendar ? "urn:ietf:params:xml:ns:caldav" : "urn:ietf:params:xml:ns:carddav";

    public static string WellKnownName(DavKind kind) => kind == DavKind.Calendar ? "caldav" : "carddav";

    private static string LastSegment(string href)
        => href.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() is { } last
            ? Uri.UnescapeDataString(last)
            : href;

    /// <summary>A multistatus as a document, through the same DTD-refusing reader; null when it is not XML.</summary>
    public static XDocument? Document(string? xml) => Parse(xml);

    private static XDocument? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

/// <summary>[S-516] DAV addresses: hrefs made absolute, and the RFC 6764 well-known address.</summary>
public static class DavUrls
{
    /// <summary>
    /// An href as an absolute URL. Usually an absolute path, sometimes a full URL, rarely relative —
    /// all three resolve against the URL the answer actually came from (after redirects).
    /// </summary>
    public static Uri? Resolve(Uri replyUrl, string? href)
    {
        ArgumentNullException.ThrowIfNull(replyUrl);

        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var trimmed = href.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            return absolute;
        }

        // A rooted path on Windows parses as an absolute file: URI; only http(s) counts as absolute.
        return Uri.TryCreate(replyUrl, trimmed, out var resolved) ? resolved : null;
    }

    /// <summary><c>scheme://authority/.well-known/caldav</c> (or carddav) for a URL.</summary>
    public static Uri? WellKnown(Uri entered, DavKind kind)
    {
        ArgumentNullException.ThrowIfNull(entered);
        return Uri.TryCreate($"{entered.Scheme}://{entered.Authority}/.well-known/{DavXml.WellKnownName(kind)}", UriKind.Absolute, out var url)
            ? url
            : null;
    }

    /// <summary>
    /// What the user typed, as a URL: taken at its word when it has a scheme, given https when it is
    /// a bare host. Null when it cannot be a URL at all, or names a scheme other than http(s).
    /// </summary>
    public static Uri? FromTyped(string? typed)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            && url.Host.Length > 0
                ? url
                : null;
    }
}

/// <summary>One PROPFIND's answer: the status, the body, and where it ended up after redirects.</summary>
public readonly record struct DavReply(int Status, string Body, Uri FinalUrl);

/// <summary>How discovery ended.</summary>
public enum DavDiscoveryOutcome
{
    /// <summary><see cref="DavDiscoveryResult.Collection"/> is the collection to use.</summary>
    Found,

    /// <summary>Every candidate refused the credentials.</summary>
    AuthFailed,

    /// <summary>The server answered with an error that is not about the credentials.</summary>
    ServerError,

    /// <summary>Connected and signed in, but there is no collection of the wanted kind.</summary>
    NothingFound,
}

public readonly record struct DavDiscoveryResult(DavDiscoveryOutcome Outcome, Uri? Collection, int Status, string Message);

/// <summary>
/// [S-517] Resolve what the user typed to a concrete calendar or address-book collection, proving the
/// credentials on the way (RFC 6764).
/// <para>
/// The typed URL is tried first, then the <c>.well-known</c> address on its origin — a bare server
/// root often answers a PROPFIND with 401 (Nextcloud's web root does) even though its DAV context
/// signs in fine. A URL that already is a collection is used verbatim, in one round trip; otherwise
/// the chain <c>current-user-principal</c> → home set → collections is walked and the first usable
/// one wins. A real credential failure is never masked: when every candidate refused, the result
/// says so with the status.
/// </para>
/// <para>
/// The requests are made by the caller (<paramref name="propfind"/>), which follows redirects itself
/// so a PROPFIND is never downgraded to a GET and the reply carries the URL it finally came from.
/// That is also what makes this testable without a server.
/// </para>
/// </summary>
public static class DavDiscovery
{
    public delegate Task<DavReply> Propfind(Uri url, string body, int depth, CancellationToken cancellation);

    public static async Task<DavDiscoveryResult> DiscoverAsync(Uri entered, DavKind kind, Propfind propfind, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(propfind);

        var candidates = new List<Uri> { entered };
        if (DavUrls.WellKnown(entered, kind) is { } wellKnown && wellKnown.AbsoluteUri != entered.AbsoluteUri)
        {
            candidates.Add(wellKnown);
        }

        var authFailed = false;
        var connected = false;
        var lastStatus = 0;

        foreach (var candidate in candidates)
        {
            var probe = await propfind(candidate, DiscoveryBody(kind), 0, cancellation).ConfigureAwait(false);

            if (probe.Status is 401 or 403)
            {
                authFailed = true;
                lastStatus = probe.Status;
                continue;
            }

            if (!IsOk(probe.Status))
            {
                lastStatus = probe.Status;
                continue;
            }

            connected = true;

            if (DavXml.IsCollection(probe.Body, kind))
            {
                return new DavDiscoveryResult(DavDiscoveryOutcome.Found, probe.FinalUrl, probe.Status, string.Empty);
            }

            if (await WalkAsync(probe, kind, propfind, cancellation).ConfigureAwait(false) is { } found)
            {
                return new DavDiscoveryResult(DavDiscoveryOutcome.Found, found, 207, string.Empty);
            }
        }

        // Signed in somewhere and found nothing is the useful thing to say, ahead of a 404 from the
        // .well-known fallback that was only ever a guess.
        if (authFailed && !connected)
        {
            return new DavDiscoveryResult(DavDiscoveryOutcome.AuthFailed, null, lastStatus,
                Str.Shell_Accounts_Dav_AuthFailed.Format(("status", lastStatus)));
        }

        if (lastStatus != 0 && !connected)
        {
            return new DavDiscoveryResult(DavDiscoveryOutcome.ServerError, null, lastStatus,
                Str.Shell_Accounts_Dav_ServerError.Format(("status", lastStatus)));
        }

        return new DavDiscoveryResult(DavDiscoveryOutcome.NothingFound, null, 0, kind == DavKind.Calendar
            ? Str.Shell_Accounts_Dav_NoCalendar.Text()
            : Str.Shell_Accounts_Dav_NoAddressBook.Text());
    }

    private static async Task<Uri?> WalkAsync(DavReply probe, DavKind kind, Propfind propfind, CancellationToken cancellation)
    {
        // The probe may already carry the home set (it hit the DAV root or the principal itself);
        // otherwise the principal is asked for it.
        Uri? home;
        if (DavXml.HomeSet(probe.Body, kind) is { } direct)
        {
            home = DavUrls.Resolve(probe.FinalUrl, direct);
        }
        else if (DavUrls.Resolve(probe.FinalUrl, DavXml.Principal(probe.Body)) is { } principal)
        {
            var answer = await propfind(principal, HomeBody(kind), 0, cancellation).ConfigureAwait(false);
            home = IsOk(answer.Status) ? DavUrls.Resolve(answer.FinalUrl, DavXml.HomeSet(answer.Body, kind)) : null;
        }
        else
        {
            return null;
        }

        if (home is null)
        {
            return null;
        }

        var listing = await propfind(home, ListBody(kind), 1, cancellation).ConfigureAwait(false);
        if (!IsOk(listing.Status))
        {
            return null;
        }

        return DavXml.Collections(listing.Body, kind)
            .Select(c => DavUrls.Resolve(listing.FinalUrl, c.Href))
            .FirstOrDefault(u => u is not null);
    }

    public static bool IsOk(int status) => status is >= 200 and < 300;

    /// <summary>Depth 0 on the typed URL: what it is, who is signed in, and the home set if it knows.</summary>
    public static string DiscoveryBody(DavKind kind) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><D:propfind xmlns:D="DAV:" xmlns:A="{DavXml.Namespace(kind)}"><D:prop><D:resourcetype/><D:current-user-principal/><A:{DavXml.HomeSetName(kind)}/></D:prop></D:propfind>""";

    /// <summary>Depth 0 on the principal: its home set.</summary>
    public static string HomeBody(DavKind kind) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><D:propfind xmlns:D="DAV:" xmlns:A="{DavXml.Namespace(kind)}"><D:prop><A:{DavXml.HomeSetName(kind)}/></D:prop></D:propfind>""";

    /// <summary>Depth 1 on the home set: each collection's type, name, components and colour.</summary>
    public static string ListBody(DavKind kind) => kind == DavKind.Calendar
        ? """<?xml version="1.0" encoding="UTF-8"?><D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:I="http://apple.com/ns/ical/"><D:prop><D:resourcetype/><D:displayname/><C:supported-calendar-component-set/><I:calendar-color/></D:prop></D:propfind>"""
        : """<?xml version="1.0" encoding="UTF-8"?><D:propfind xmlns:D="DAV:"><D:prop><D:resourcetype/><D:displayname/></D:prop></D:propfind>""";
}
