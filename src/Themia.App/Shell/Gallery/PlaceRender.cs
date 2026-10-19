#if DEBUG
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2847] The place search's Debug renders, without a network: <see cref="UseFixture"/> answers the
/// properties menu's location searches from a fixed list of fixture places (<c>--editbar-png</c>
/// types into them); [S-2848] <c>--place-cleared</c> on <c>--widget-png</c> clears the widget's place
/// (a Sun or Moon starts at Zurich).
/// </summary>
internal static class PlaceRender
{
    private static readonly PlaceCandidate[] Fixture =
    [
        new("Zürich", "Zurich", "Bezirk Zürich", "Switzerland", 47.3667, 8.55),
        new("Zurich", "Ontario", "Huron County", "Canada", 43.4211, -81.6248),
        new("Zurich", "Kansas", "Rooks County", "United States", 39.2322, -99.4337),
        new("Zürichsee", "Zurich", null, "Switzerland", 47.25, 8.7),
        new("Springfield", "Illinois", "Sangamon County", "United States", 39.8017, -89.6437),
        new("Springfield", "Missouri", "Greene County", "United States", 37.2153, -93.2982),
        new("Springfield", "Ohio", "Clark County", "United States", 39.9242, -83.8088),
        new("Springfield", "Ohio", "Summit County", "United States", 41.0287, -81.4932),
        new("Springfield", "Massachusetts", "Hampden County", "United States", 42.1015, -72.5898),
    ];

    /// <summary>The geocoder's answers from <see cref="Fixture"/>: the places whose name holds the query.</summary>
    public static void UseFixture()
        => PlaceSearch.DebugLookup = (query, _) =>
        {
            var places = PlaceQuery.Results(Fixture.Where(c => c.Name!.Contains(query, StringComparison.OrdinalIgnoreCase)), query);
            return Task.FromResult<(PlaceOutcome, IReadOnlyList<PlaceMatch>, string?)>((places.Count == 0 ? PlaceOutcome.NoMatch : PlaceOutcome.Matches, places, null));
        };

    /// <summary>[S-2848] <c>--place-cleared</c> on one widget's config.</summary>
    public static void Apply(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--place-cleared") >= 0 && config is IPlaced placed)
        {
            placed.ClearLocation();
        }
    }
}
#endif
