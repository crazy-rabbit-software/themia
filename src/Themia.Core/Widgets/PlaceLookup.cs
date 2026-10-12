using System.Globalization;
using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One hit as the geocoder sent it: nothing in it is trusted yet.</summary>
public sealed record PlaceCandidate(
    string? Name,
    string? Admin1,
    string? Admin2,
    string? Country,
    double? Latitude,
    double? Longitude);

/// <summary>
/// A place the search may offer: checked, and labelled so that it can be told apart from every
/// other place offered beside it.
/// </summary>
/// <param name="Name">The city, the suggestion's first line ("Zürich").</param>
/// <param name="Detail">What sets it apart, the second line ("Zurich, Switzerland"); may be empty.</param>
/// <param name="Label">What is stored and shown once picked: the name and the detail ("Zürich, Zurich, Switzerland").</param>
public sealed record PlaceMatch(string Name, string Detail, string Label, double Latitude, double Longitude);

/// <summary>What a query in the box calls for. [S-2841]</summary>
public enum PlaceQueryStep
{
    /// <summary>The box is empty: close the list and ask nothing.</summary>
    Idle,

    /// <summary>One character while typing: not yet a question, and not an error either.</summary>
    TooShort,

    /// <summary>Text with no letter in it ("12345"): said, not sent.</summary>
    NotAName,

    /// <summary>Worth asking the geocoder about.</summary>
    Search,
}

/// <summary>How a search ended, as the suggestion list says it. [S-2843]</summary>
public enum PlaceOutcome
{
    Matches,

    /// <summary>The geocoder answered and matched nothing: an answer, not a failure.</summary>
    NoMatch,

    /// <summary>This machine has no network: a state of the laptop, not of the service.</summary>
    Offline,

    /// <summary>There is a network and the service did not answer usefully. Logged.</summary>
    Failed,

    /// <summary>The query was refused before any request.</summary>
    NotAName,
}

/// <summary>
/// [S-2841]..[S-2845] The rules of the city search behind the shared location row: what is worth
/// asking, how the geocoder's answer is checked and ranked, and how places of the same name are
/// labelled apart. Pure, so it is tested without a network; the request itself stays behind the
/// guarded web fetch in the app.
/// </summary>
public static class PlaceQuery
{
    /// <summary>
    /// How long typing has to pause before a search goes out. Long enough that "Zurich" is one
    /// request rather than six, short enough that the list keeps up with somebody who stops.
    /// </summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    /// <summary>What typing has to reach before it searches by itself. Enter searches from one.</summary>
    public const int MinTypedLength = 2;

    /// <summary>How many places the list offers: more than six is a list to read, not to pick from.</summary>
    public const int MaxResults = 6;

    /// <summary>How many the geocoder is asked for, so that merging duplicates still leaves six.</summary>
    public const int Requested = 10;

    /// <summary>A name longer than this is not a city's name; the hit is dropped.</summary>
    private const int MaxNameLength = 120;

    /// <summary>Two hits closer than this (degrees, about 10 km) with the same labels are one place.</summary>
    private const double SamePlaceDegrees = 0.1;

    /// <summary>Trimmed, runs of whitespace made one space, cut at the place length limit.</summary>
    public static string Normalise(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return string.Empty;
        }

        var text = new StringBuilder(Math.Min(typed.Length, WebInput.MaxPlaceLength));
        var space = false;

        foreach (var c in typed.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space && text.Length > 0)
            {
                text.Append(' ');
            }

            space = false;
            text.Append(c);

            if (text.Length >= WebInput.MaxPlaceLength)
            {
                break;
            }
        }

        return text.ToString(0, Math.Min(text.Length, WebInput.MaxPlaceLength)).TrimEnd();
    }

    /// <param name="query">A <see cref="Normalise"/>d query.</param>
    /// <param name="submitted">Enter was pressed: one character is then a question (津 is a city).</param>
    public static PlaceQueryStep Classify(string query, bool submitted)
    {
        if (string.IsNullOrEmpty(query))
        {
            return PlaceQueryStep.Idle;
        }

        if (!submitted && query.Length < MinTypedLength)
        {
            return PlaceQueryStep.TooShort;
        }

        return WebInput.IsPlausiblePlace(query) ? PlaceQueryStep.Search : PlaceQueryStep.NotAName;
    }

    /// <summary>The geocoder's language parameter for the app's language.</summary>
    public static string LanguageCode(Locale locale) => LanguageResolver.Code(locale);

    /// <summary>One cache entry per question: the same words in any case, in one language.</summary>
    public static string CacheKey(string query, Locale locale)
        => $"{LanguageCode(locale)}|{query.ToUpperInvariant()}";

    /// <summary>Answers are cached; a failure is not, so the next keystroke can try again.</summary>
    public static bool IsCacheable(PlaceOutcome outcome) => outcome is PlaceOutcome.Matches or PlaceOutcome.NoMatch;

    /// <summary>How a fetch ended, as one of the four things the list can say.</summary>
    public static PlaceOutcome Outcome(FetchStatus status) => status switch
    {
        FetchStatus.Ok => PlaceOutcome.Matches,
        FetchStatus.Empty => PlaceOutcome.NoMatch,
        FetchStatus.Offline => PlaceOutcome.Offline,
        FetchStatus.Refused => PlaceOutcome.NotAName,
        _ => PlaceOutcome.Failed,
    };

    /// <summary>
    /// [S-2842] The geocoder's hits as the list offers them: malformed ones dropped, the same place
    /// twice merged, exact names first, then names that start with the query, then the service's
    /// own order (it ranks by population), at most <see cref="MaxResults"/>; and labelled so that
    /// no two read the same.
    /// </summary>
    public static IReadOnlyList<PlaceMatch> Results(IEnumerable<PlaceCandidate> hits, string query)
    {
        ArgumentNullException.ThrowIfNull(hits);
        query ??= string.Empty;

        var kept = new List<Checked>();

        foreach (var hit in hits)
        {
            if (Check(hit) is not { } place)
            {
                continue;
            }

            if (kept.Any(other => other.SameAs(place)))
            {
                continue;
            }

            kept.Add(place);
        }

        var ranked = kept
            .Select((place, order) => (place, order))
            .OrderBy(p => Rank(p.place.Name, query))
            .ThenBy(p => p.order)
            .Select(p => p.place)
            .Take(MaxResults)
            .ToList();

        return [.. ranked.Select(place => Label(place, ranked))];
    }

    private static int Rank(string name, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    private sealed record Checked(string Name, string? Admin1, string? Admin2, string? Country, double Latitude, double Longitude)
    {
        public bool SameLabels(Checked other)
            => Same(Name, other.Name) && Same(Admin1, other.Admin1) && Same(Country, other.Country);

        public bool SameAs(Checked other)
            => SameLabels(other)
                && Same(Admin2, other.Admin2)
                && Math.Abs(Latitude - other.Latitude) < SamePlaceDegrees
                && Math.Abs(Longitude - other.Longitude) < SamePlaceDegrees;

        private static bool Same(string? a, string? b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static Checked? Check(PlaceCandidate hit)
    {
        if (hit is null || Field(hit.Name) is not { } name || !WebInput.AreCoordinates(hit.Latitude, hit.Longitude))
        {
            return null;
        }

        // A region the city shares its name with ("Berlin, Berlin") says nothing twice.
        var admin1 = Field(hit.Admin1) is { } a1 && !a1.Equals(name, StringComparison.OrdinalIgnoreCase) ? a1 : null;
        var admin2 = Field(hit.Admin2) is { } a2 && !a2.Equals(name, StringComparison.OrdinalIgnoreCase) ? a2 : null;

        return new Checked(name, admin1, admin2, Field(hit.Country), hit.Latitude!.Value, hit.Longitude!.Value);
    }

    /// <summary>A text field, trimmed; null when absent, blank, overlong or carrying a control character.</summary>
    private static string? Field(string? text)
    {
        var value = text?.Trim();

        if (string.IsNullOrEmpty(value) || value.Length > MaxNameLength || value.Any(char.IsControl))
        {
            return null;
        }

        return value;
    }

    /// <summary>
    /// The second line: region and country; plus the county when another place offered has the same
    /// name, region and country; plus the coordinates when even that does not tell them apart.
    /// </summary>
    private static PlaceMatch Label(Checked place, List<Checked> offered)
    {
        var twins = offered.Where(other => !ReferenceEquals(other, place) && other.SameLabels(place)).ToList();

        var parts = new List<string>(3);

        if (twins.Count > 0 && place.Admin2 is { } county)
        {
            parts.Add(county);
        }

        if (place.Admin1 is { } region)
        {
            parts.Add(region);
        }

        if (place.Country is { } country)
        {
            parts.Add(country);
        }

        var detail = string.Join(", ", parts);

        if (twins.Any(other => string.Equals(other.Admin2 ?? string.Empty, place.Admin2 ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"{place.Latitude:0.##}, {place.Longitude:0.##}");
            detail = detail.Length == 0 ? at : $"{detail} · {at}";
        }

        var label = detail.Length == 0 ? place.Name : $"{place.Name}, {detail}";
        return new PlaceMatch(place.Name, detail, label, place.Latitude, place.Longitude);
    }
}

/// <summary>
/// [S-2845] The answers already given this session, so that deleting a letter and typing it again
/// asks nothing. Bounded, least recently used out first; thread-safe, one per app.
/// </summary>
public sealed class PlaceLookupCache
{
    private readonly int _capacity;
    private readonly Lock _gate = new();
    private readonly LinkedList<(string Key, IReadOnlyList<PlaceMatch> Places)> _order = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, IReadOnlyList<PlaceMatch> Places)>> _index = new(StringComparer.Ordinal);

    public PlaceLookupCache(int capacity = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public bool TryGet(string key, out IReadOnlyList<PlaceMatch> places)
    {
        lock (_gate)
        {
            if (_index.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                places = node.Value.Places;
                return true;
            }
        }

        places = [];
        return false;
    }

    public void Put(string key, IReadOnlyList<PlaceMatch> places)
    {
        ArgumentNullException.ThrowIfNull(places);

        lock (_gate)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
            }

            _index[key] = _order.AddFirst((key, places));

            while (_order.Count > _capacity && _order.Last is { } last)
            {
                _order.RemoveLast();
                _index.Remove(last.Value.Key);
            }
        }
    }
}
