using System.Globalization;
using Themia.Core.Accounts;
using Themia.Core.Tasks;

namespace Themia.Core.Office;

/// <summary>
/// [S-578] The CalDAV and CardDAV requests the calendar and contacts widgets make, and how their
/// answers are read. The requests are sent by the account service ([S-533]); this is what they say.
/// </summary>
public static class DavOffice
{
    /// <summary>
    /// A <c>calendar-query</c> REPORT for the VEVENTs overlapping a window, asking the server to
    /// <c>expand</c> recurrences into instances (RFC 4791 §9.6.5) — which also puts every instance
    /// in UTC. A server that ignores <c>expand</c> sends masters, which <see cref="IcsEvents"/>
    /// expands itself.
    /// </summary>
    public static string CalendarQuery(DateTimeOffset from, DateTimeOffset to)
    {
        var start = Utc(from);
        var end = Utc(to);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <C:calendar-query xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:D="DAV:">
              <D:prop><D:getetag/><C:calendar-data><C:expand start="{start}" end="{end}"/></C:calendar-data></D:prop>
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

    /// <summary>
    /// [S-619] A <c>calendar-query</c> REPORT for every VTODO of a task list, finished ones included:
    /// the list's filters are applied here ([S-595]), not by the server, because iCloud and older
    /// servers answer prop-filters inconsistently and a finished task without STATUS would slip through.
    /// No <c>expand</c>: a recurring task's current instance is picked by <see cref="IcsTasks"/>.
    /// </summary>
    public const string TodoQuery = """
        <?xml version="1.0" encoding="UTF-8"?>
        <C:calendar-query xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:D="DAV:">
          <D:prop><D:getetag/><C:calendar-data/></D:prop>
          <C:filter>
            <C:comp-filter name="VCALENDAR">
              <C:comp-filter name="VTODO"/>
            </C:comp-filter>
          </C:filter>
        </C:calendar-query>
        """;

    /// <summary>An <c>addressbook-query</c> REPORT for every card's data, as the classic app sent it.</summary>
    public const string AddressBookQuery = """
        <?xml version="1.0" encoding="UTF-8"?>
        <C:addressbook-query xmlns:C="urn:ietf:params:xml:ns:carddav" xmlns:D="DAV:">
          <D:prop><D:getetag/><C:address-data/></D:prop>
          <C:filter/>
        </C:addressbook-query>
        """;

    /// <summary>The iCalendar objects in a calendar-query answer, one per resource.</summary>
    public static IReadOnlyList<string> CalendarData(string? multistatus) => DataOf(multistatus, "calendar-data");

    /// <summary>The vCards in an addressbook-query answer, one per resource.</summary>
    public static IReadOnlyList<string> AddressData(string? multistatus) => DataOf(multistatus, "address-data");

    /// <summary>
    /// [S-567] The calendars in a Depth-1 listing of a calendar home set: ids resolved to absolute
    /// URLs against where the listing came from, colours normalised ([S-566]), and the collection
    /// the account was set up with flagged as the default (else the first). Collections on another
    /// origin than the account's are dropped ([S-578]).
    /// </summary>
    public static IReadOnlyList<CalendarInfo> Calendars(string? listing, Uri listedAt, Uri accountCollection)
    {
        var found = new List<CalendarInfo>();

        foreach (var c in DavXml.Collections(listing, DavKind.Calendar))
        {
            if (DavUrls.Resolve(listedAt, c.Href) is not { } url || !SameOrigin(url, accountCollection))
            {
                continue;
            }

            found.Add(new CalendarInfo(url.AbsoluteUri, c.Name, false, CalendarRules.NormalizeColor(c.Color)));
        }

        var stored = Trimmed(accountCollection);
        var index = found.FindIndex(c => Trimmed(new Uri(c.Id)) == stored);
        if (index < 0)
        {
            index = 0;
        }

        if (found.Count > 0)
        {
            found[index] = found[index] with { IsDefault = true };
        }

        return found;
    }

    /// <summary>
    /// [S-618] The task lists in a Depth-1 listing of a calendar home set: the collections whose
    /// <c>supported-calendar-component-set</c> includes VTODO (jtx Board's and Nextcloud's task
    /// lists, iCloud's Reminders lists), ids resolved to absolute URLs, on the account's origin only
    /// ([S-578]). The collection the account was set up with is the default when it holds tasks, else
    /// the first. Every one is read-only ([S-621]).
    /// </summary>
    public static IReadOnlyList<TaskListInfo> TaskLists(string? listing, Uri listedAt, Uri accountCollection)
    {
        var found = new List<TaskListInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var c in DavXml.Collections(listing, DavKind.Calendar, "VTODO"))
        {
            if (DavUrls.Resolve(listedAt, c.Href) is not { } url || !SameOrigin(url, accountCollection) || !seen.Add(Trimmed(url)))
            {
                continue;
            }

            found.Add(new TaskListInfo(url.AbsoluteUri, c.Name, false));
        }

        if (found.Count == 0)
        {
            return found;
        }

        var stored = Trimmed(accountCollection);
        var index = Math.Max(0, found.FindIndex(l => Trimmed(new Uri(l.Id)) == stored));
        found[index] = found[index] with { IsDefault = true };
        return found;
    }

    /// <summary>
    /// The calendar the account was set up with, when its home set cannot be listed: the picker
    /// then offers that one alone, as the original did.
    /// </summary>
    public static CalendarInfo SingleCalendar(Uri accountCollection)
    {
        var segment = accountCollection.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault(s => s.Length > 0);
        return new CalendarInfo(accountCollection.AbsoluteUri, string.IsNullOrEmpty(segment) ? accountCollection.Host : Uri.UnescapeDataString(segment), true, null);
    }

    /// <summary>
    /// [S-578] Whether a calendar URL may be asked with this account's password: same scheme, host
    /// and port as the account's collection. A calendar id is read from the widget config, which is
    /// a file; without this a hand-edited id would send the account's basic-auth credentials to any
    /// host it named.
    /// </summary>
    public static bool SameOrigin(Uri candidate, Uri account)
        => candidate.IsAbsoluteUri
            && string.Equals(candidate.Scheme, account.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Host, account.Host, StringComparison.OrdinalIgnoreCase)
            && candidate.Port == account.Port;

    private static IReadOnlyList<string> DataOf(string? multistatus, string property)
        => DavXml.Responses(multistatus)
            .Select(r => r.Prop(property)?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();

    private static string Utc(DateTimeOffset moment)
        => moment.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Trimmed(Uri url) => url.AbsoluteUri.TrimEnd('/');
}
