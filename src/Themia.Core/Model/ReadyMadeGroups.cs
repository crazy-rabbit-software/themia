using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2470] Where a widget of a ready-made group stands, in DIPs from the group's top-left corner.</summary>
public readonly record struct ReadyMadeBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-2470] One widget of a ready-made group: its kind (a widget id, as the first-run seed names them),
/// its box, and what differs from its kind's defaults: a config fragment in the state file's own shape
/// (merged over the default, property by property), a header placement and a title.
/// </summary>
public sealed record ReadyMadeWidget(string Kind, ReadyMadeBox Box, string? Config = null, HeaderPlacement? Header = null, string? Label = null);

/// <summary>
/// [S-2470] A ready-made group: a recipe that places several widgets, touching (so they draw as one
/// panel, [S-2240]), already grouped ([S-2450]) under the recipe's name. Pure data; its words go through
/// <see cref="Str"/>. <paramref name="Home"/> is the subject of the add menu it is about: a search result
/// shows it as the recipe's place ([S-2473]).
/// </summary>
public sealed record ReadyMadeGroup(string Id, Str Name, Str About, string Glyph, AddFolderId Home, IReadOnlyList<ReadyMadeWidget> Widgets)
{
    /// <summary>The box round every widget, in DIPs from the group's corner.</summary>
    public ReadyMadeBox Bounds
    {
        get
        {
            var left = Widgets.Min(w => w.Box.X);
            var top = Widgets.Min(w => w.Box.Y);
            return new ReadyMadeBox(left, top, Widgets.Max(w => w.Box.Right) - left, Widgets.Max(w => w.Box.Bottom) - top);
        }
    }
}

/// <summary>
/// [S-2470]..[S-2475] The ready-made groups the add menu offers above the widgets, so a first desktop
/// is a few clicks. Adding one is adding an entry to <see cref="All"/> (and its two strings to the
/// <c>groups</c> locale area): nothing else names a recipe. <c>ReadyMadeGroupsTests</c> hold every entry
/// to the rules: each kind installed and its config read, no two boxes overlapping, every widget touching
/// another, and the whole fitting a 1280 × 720 work area.
/// </summary>
public static class ReadyMadeGroups
{
    private const string Scope = "ready-made";

    /// <summary>[S-2474] This PC: the four meters as a 2 × 2 grid.</summary>
    public static ReadyMadeGroup ThisPc { get; } = new("thisPc", Str.Groups_Recipe_ThisPc, Str.Groups_Recipe_ThisPcAbout, Glyphs.Display, AddFolderId.System,
    [
        new("cpu", new(0, 0, 200, 150)),
        new("memory", new(200, 0, 200, 150)),
        new("network", new(0, 150, 200, 150)),
        new("disk", new(200, 150, 200, 150)),
    ]);

    /// <summary>[S-2474] My day: the clock on top, the next meeting and the tasks under it.</summary>
    public static ReadyMadeGroup MyDay { get; } = new("myDay", Str.Groups_Recipe_MyDay, Str.Groups_Recipe_MyDayAbout, Glyphs.Calendar, AddFolderId.CalendarMail,
    [
        new("clock", new(0, 0, 320, 140)),
        new("nextMeeting", new(0, 140, 320, 200)),
        new("tasks", new(0, 340, 320, 320)),
    ]);

    /// <summary>[S-2474] Weather: now, the days ahead and the rain in the next hour, stacked.</summary>
    public static ReadyMadeGroup Weather { get; } = new("weather", Str.Groups_Recipe_Weather, Str.Groups_Recipe_WeatherAbout, Glyphs.Weather, AddFolderId.Weather,
    [
        new("weatherNow", new(0, 0, 380, 200)),
        new("weatherForecast", new(0, 200, 380, 220)),
        new("rainNextHour", new(0, 420, 380, 180)),
    ]);

    /// <summary>[S-2470] Every ready-made group, in the add menu's order.</summary>
    public static IReadOnlyList<ReadyMadeGroup> All { get; } = [ThisPc, MyDay, Weather];

    /// <summary>The ready-made group named <paramref name="id"/>, or null.</summary>
    public static ReadyMadeGroup? Find(string? id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// [S-2472] The groups this build can place: every kind of each one installed. One whose widget a
    /// build lacks is left out of the menu whole, never placed in part.
    /// </summary>
    public static IReadOnlyList<ReadyMadeGroup> Offered()
        => [.. All.Where(r => r.Widgets.All(w => WidgetId.TryParse(w.Kind, out var id) && ModuleCatalog.TryGet(id, out _)))];

    /// <summary>[S-2471] A recipe box on <paramref name="screen"/>, its group's corner at (<paramref name="xPx"/>, <paramref name="yPx"/>) DIPs.</summary>
    public static FracRect Rect(ReadyMadeBox box, ScreenMetrics screen, double xPx = 0, double yPx = 0)
        => FracRect.FromPixels((xPx + box.X) / screen.Width, (yPx + box.Y) / screen.Height, box.Width, box.Height, screen);

    /// <summary>
    /// [S-2471] The widgets of <paramref name="recipe"/> on <paramref name="screen"/>, its corner at
    /// (<paramref name="xPx"/>, <paramref name="yPx"/>) DIPs, every id new, each the kind's default config
    /// with the fragment over it (as the first-run seed builds them), in one new group named after the
    /// recipe. Null, logged, when this build lacks one of its kinds.
    /// </summary>
    public static (List<WidgetInstance> Widgets, WidgetGroup Group)? Build(ReadyMadeGroup recipe, ScreenMetrics screen, double xPx = 0, double yPx = 0)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var group = WidgetGroup.Create(recipe.Name.Text());
        var widgets = new List<WidgetInstance>(recipe.Widgets.Count);
        foreach (var part in recipe.Widgets)
        {
            var rect = Rect(part.Box, screen, xPx, yPx);
            if (FirstRunDesktop.Build(new FirstRunDesktop.Seed(part.Kind, rect, Config: part.Config)) is not { } widget)
            {
                Log.Warn(Scope, $"'{recipe.Id}' names a widget this build does not have ('{part.Kind}'); it is not placed");
                return null;
            }

            widget.Header = part.Header is { } header && WidgetInstance.CanHaveHeader(widget.Kind) ? header : widget.Header;
            widget.Label = part.Label ?? widget.Label;
            widget.Group = group.Id;
            widgets.Add(widget);
        }

        return (widgets, group);
    }
}
