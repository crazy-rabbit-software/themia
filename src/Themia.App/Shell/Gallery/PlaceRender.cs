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

        // [S-3725] More than five answers: the search's Show all.
        new("San Francisco", "California", null, "United States", 37.7749, -122.4194),
        new("San Diego", "California", null, "United States", 32.7157, -117.1611),
        new("San Jose", "California", null, "United States", 37.3382, -121.8863),
        new("San Antonio", "Texas", null, "United States", 29.4241, -98.4936),
        new("San Juan", "San Juan", null, "Puerto Rico", 18.4655, -66.1057),
        new("San Sebastián", "Basque Country", null, "Spain", 43.3183, -1.9812),
    ];

    /// <summary>[S-3725] What the fixture never answers: the search stays out ("Searching…").</summary>
    public const string Unanswered = "wait";

    /// <summary>The geocoder's answers from <see cref="Fixture"/>: the places whose name holds the query.</summary>
    public static void UseFixture()
        => PlaceSearch.DebugLookup = (query, cancel) =>
        {
            if (string.Equals(query, Unanswered, StringComparison.OrdinalIgnoreCase))
            {
                return new TaskCompletionSource<(PlaceOutcome, IReadOnlyList<PlaceMatch>, string?)>().Task.WaitAsync(cancel);
            }

            var places = PlaceQuery.Results(Fixture.Where(c => c.Name!.Contains(query, StringComparison.OrdinalIgnoreCase)), query);
            return Task.FromResult<(PlaceOutcome, IReadOnlyList<PlaceMatch>, string?)>((places.Count == 0 ? PlaceOutcome.NoMatch : PlaceOutcome.Matches, places, null));
        };

    /// <summary>
    /// [S-2848] <c>--place-cleared</c> on one widget's config; [S-2855] <c>--device
    /// locating|denied|unavailable|located</c> turns its Use my location on over that stand-in state
    /// (located: Zurich), never Windows' own.
    /// </summary>
    public static void Apply(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--place-cleared") >= 0 && config is IPlaced placed)
        {
            placed.ClearLocation();
        }

        var at = Array.IndexOf(args, "--device");
        if (at >= 0 && config is IPlaced device)
        {
            if (at + 1 >= args.Length || Device(args[at + 1]) is not { } state)
            {
                Console.WriteLine("widget: --device takes locating, denied, unavailable or located");
                return;
            }

            device.UseDeviceLocation = true;
            Services.AppServices.DebugDeviceLocation = state;
        }
    }

    /// <summary>[S-2855] A stand-in device location by its name; located is Zurich.</summary>
    public static DeviceLocation? Device(string name) => name switch
    {
        "locating" => DeviceLocation.Locating,
        "denied" => DeviceLocation.Denied,
        "unavailable" => DeviceLocation.Unavailable,
        "located" => DeviceLocation.At(47.3769, 8.5417),
        _ => null,
    };
}
#endif
