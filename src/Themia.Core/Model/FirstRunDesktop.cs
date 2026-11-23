using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-893] The first-run desktop as data: which widgets (by id), where on the 2560×1440 grid it was
/// laid out on, whether each sits bare on the wallpaper, and what of its default config differs, as a
/// JSON fragment in the state file's own shape. Built through each widget's descriptor and the state's
/// JSON, so the seed names no config type; a widget this build does not have is left out, logged.
/// The product's choice of widgets, not a per-widget table: a widget's own rules stay its own.
/// </summary>
internal static class FirstRunDesktop
{
    private const string Scope = "first-run";

    /// <summary>
    /// One seeded widget. <paramref name="Id"/> is a pick ([S-16682]: a kind's id, or <c>kind/preset</c>);
    /// <paramref name="Config"/> is merged over its new config, property by property.
    /// </summary>
    internal sealed record Seed(string Id, FracRect Rect, bool Bare = false, string? Config = null);

    /// <summary>
    /// [S-893] Home: the weather top left, a clock top right, the quick settings along the top centre,
    /// and the four meters as graphs down the right edge. The clock first, so a reset or a recovery
    /// shows it on top. No shortcuts: Windows' own desktop icons are the desktop items ([S-5500]).
    /// </summary>
    internal static IReadOnlyList<Seed> Home { get; } =
    [
        new("clock", Grid(2250, 0, 310, 180), Bare: true),

        // No place: the widget asks for one ([S-2848]) rather than showing someone else's town.
        new("weatherNow", Grid(0, 0, 310, 180), Bare: true, Config: """{ "style": "largeIcon" }"""),

        // The meters stack on a 120-DIP stride; each is 1/1080 of the screen shorter than it, so they never touch.
        new("cpu", Meter(0), Bare: true),
        new("memory", Meter(1), Bare: true),
        new("gpu", Meter(2), Bare: true),
        new("network", Meter(3), Bare: true),

        // Square tiles side by side along the top, about the screen's centre (1090..1450 of 2560).
        new("doNotDisturbToggle", Tile(0)),
        new("colorFiltersToggle", Tile(1)),
        new("nightLightToggle", Tile(2)),
        new("airplaneModeToggle", Tile(3)),
        new("bluetoothToggle", Tile(4)),
        new("wifiToggle", Tile(5)),
    ];

    /// <summary>
    /// [S-893] Work: the month calendar, tasks and a note side by side across the top left, a clock
    /// with the date top right. Calendar and tasks show their Set up button until an account exists
    /// ([S-1860]); notes work at once.
    /// </summary>
    internal static IReadOnlyList<Seed> Work { get; } =
    [
        new("clock", Grid(2330, 0, 230, 120), Bare: true),
        new("calendar", Grid(0, 0, 800, 710)),
        new("tasks", Grid(800, 0, 510, 710)),
        new("notes", Grid(1310, 0, 510, 710)),
    ];

    /// <summary>A view named <paramref name="name"/> holding <paramref name="seeds"/>, every id new.</summary>
    internal static ViewState View(string name, IReadOnlyList<Seed> seeds)
    {
        var view = ViewState.Create(name);
        foreach (var seed in seeds)
        {
            if (Build(seed) is { } widget)
            {
                view.Widgets.Add(widget);
            }
        }

        return view;
    }

    /// <summary>
    /// The widget a seed stands for: its descriptor's default config with the seed's fragment over
    /// it, read back through the state's JSON and copied, so every id inside it (a shortcut's own) is new
    /// on each call as a pasted copy's are. Null for a widget this build does not have.
    /// </summary>
    internal static WidgetInstance? Build(Seed seed)
    {
        if (!WidgetPick.TryParse(seed.Id, out var id))
        {
            Log.Warn(Scope, $"the first-run desktop names a widget this build does not have ('{seed.Id}'); it is left out");
            return null;
        }

        var widget = WidgetInstance.Create(id, seed.Rect);
        if (seed.Config is { } fragment)
        {
            widget.Config = StateJson.Merged(widget.Config, fragment);
        }

        if (seed.Bare)
        {
            widget.Background = FillChoice.Transparent();
        }

        return widget;
    }

    /// <summary>A rect on the 2560×1440 grid the first-run views were laid out on, as fractions.</summary>
    private static FracRect Grid(double x, double y, double w, double h) => new(x / 2560, y / 1440, w / 2560, h / 1440);

    private static FracRect Meter(int i) => new(2250.0 / 2560, (920.0 + (120 * i)) / 1440, 310.0 / 2560, 89.0 / 1080);

    private static FracRect Tile(int i) => Grid(1090 + (60 * i), 0, 60, 60);

}
