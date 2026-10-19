using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2840] [S-3722] The place search every widget about a place shares on its properties menu (the
/// weather family's and the Sun and Moon's location lines): what is typed, asked of the host's
/// geocoder, answered as lines to pick from or one sentence in their place.
/// <para>
/// The place resolves to coordinates <b>once</b>, at the pick, never on a refresh: re-geocoding
/// would double the requests and let the widget move on its own the day the geocoder changes its
/// mind about which Springfield is the popular one.
/// </para>
/// </summary>
public static class PlaceSearch
{
    private const string Scope = "weather";

    /// <summary>The host's geocoder ([S-2840]): the request is the host's, the search is the SDK's.</summary>
    private static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>>? _lookup;

    private static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>> Geocoder
        => Volatile.Read(ref _lookup) ?? throw new InvalidOperationException("no place lookup installed: the host installs it before any place search is shown");

    /// <summary>The host's geocoder. Once per process.</summary>
    internal static void InstallLookup(Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (Interlocked.CompareExchange(ref _lookup, lookup, null) is not null)
        {
            throw new InvalidOperationException("the place lookup is installed once per process");
        }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: answers in place of the geocoder, so every state of the list can be drawn
    /// without a network. Null in every real run.
    /// </summary>
    internal static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>>? DebugLookup { get; set; }
#endif

    /// <summary>
    /// [S-3722] The geocoder as a search on the properties menu: what the box says calls for nothing
    /// (empty, one letter), a word (no letter in it), or the lookup ([S-2841]); its places as lines,
    /// a click on one <paramref name="picked"/>; no match, offline and failed said in their place ([S-2843]).
    /// </summary>
    public static async Task<MenuSearchAnswer> MenuAnswerAsync(string typed, Action<PlaceMatch> picked, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(picked);
        var query = PlaceQuery.Normalise(typed);
        switch (PlaceQuery.Classify(query, submitted: false))
        {
            case PlaceQueryStep.Idle:
            case PlaceQueryStep.TooShort:
                return new MenuSearchAnswer([]);
            case PlaceQueryStep.NotAName:
                return new MenuSearchAnswer([], Str.Shell_Place_TypeName.Text());
        }

#if DEBUG
        var find = DebugLookup ?? Geocoder;
#else
        var find = Geocoder;
#endif
        var (outcome, places, detail) = await find(query, cancel).ConfigureAwait(true);
        switch (outcome)
        {
            case PlaceOutcome.Matches:
                return new MenuSearchAnswer([.. places.Select(place => new ItemMenuEntry(Glyphs.Place,
                    place.Detail.Length > 0 ? place.Name + " · " + place.Detail : place.Name, () => picked(place)))]);
            case PlaceOutcome.NoMatch:
                return new MenuSearchAnswer([], Str.Shell_Place_NoMatch.Format(("query", query)));
            case PlaceOutcome.Offline:
                return new MenuSearchAnswer([], Str.Shell_Place_Offline.Text());
            case PlaceOutcome.NotAName:
                return new MenuSearchAnswer([], Str.Shell_Place_TypeName.Text());
            default:
                Log.Warn(Scope, $"place search failed: {detail}".TrimEnd());
                return new MenuSearchAnswer([], Str.Shell_Place_Failed.Text());
        }
    }
}
