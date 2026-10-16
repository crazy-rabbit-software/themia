#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2847] The location row's states for <c>--settings-png</c>, without a network:
/// <c>--place-chosen</c> gives the demo widget a place; [S-2848] <c>--place-cleared</c> clears it (a
/// Sun or Moon starts at Zurich), also for <c>--widget-png</c>; <c>--place-query text</c> types into
/// the location search and submits; <c>--place-reply results|none|offline|error|searching</c> is what
/// the stand-in geocoder answers (default results, from a fixed list of fixture places). The open
/// suggestion list is written beside the page as <c>&lt;out&gt;-place.png</c>.
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

    private static string? Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>The query to type, or null when this render does not open the location search.</summary>
    public static string? Query => Arg("--place-query");

    /// <summary>Before the page is built: the chosen place, and the stand-in geocoder.</summary>
    public static void Prepare(AppHost host, WidgetKind? kind)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (kind is { } wanted)
        {
            foreach (var widget in host.State.Views.SelectMany(v => v.Widgets).Where(w => w.Kind == wanted))
            {
                Apply(widget.Config);
            }
        }

        if (Query is null)
        {
            return;
        }

        var reply = Arg("--place-reply") ?? "results";

        PlaceSearch.DebugLookup = async (query, token) =>
        {
            switch (reply)
            {
                case "searching":
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(true);
                    return (PlaceOutcome.Failed, [], null);
                case "none":
                    return (PlaceOutcome.NoMatch, [], null);
                case "offline":
                    return (PlaceOutcome.Offline, [], null);
                case "error":
                    return (PlaceOutcome.Failed, [], "fixture: 503");
                default:
                    var places = PlaceQuery.Results(
                        Fixture.Where(c => c.Name!.Contains(query, StringComparison.OrdinalIgnoreCase)),
                        query);
                    return (places.Count == 0 ? PlaceOutcome.NoMatch : PlaceOutcome.Matches, places, null);
            }
        };
    }

    /// <summary>[S-2848] <c>--place-chosen</c> or <c>--place-cleared</c> on one widget's config.</summary>
    public static void Apply(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var args = Environment.GetCommandLineArgs();

        if (Array.IndexOf(args, "--place-cleared") >= 0 && config is IPlaced placed)
        {
            placed.ClearLocation();
            return;
        }

        if (Array.IndexOf(args, "--place-chosen") < 0)
        {
            return;
        }

        switch (config)
        {
            case WeatherConfig weather:
                weather.Latitude = 47.3667;
                weather.Longitude = 8.55;
                weather.LocationName = "Zürich, Zurich, Switzerland";
                break;
            case SkyPlaceConfig sky:
                sky.Latitude = 47.3667;
                sky.Longitude = 8.55;
                sky.Place = "Zürich, Zurich, Switzerland";
                break;
        }
    }

    /// <summary>Once the page is laid out: type the query and submit it.</summary>
    public static void Type()
    {
        if (Query is { } query)
        {
            PlaceSearch.DebugType?.Invoke(query);
        }
    }
}
#endif
