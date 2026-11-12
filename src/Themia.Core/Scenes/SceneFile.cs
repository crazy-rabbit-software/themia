using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-1075] A store scenes file, read and checked (<see cref="SceneParser"/>): the stand-in display
/// every scene is drawn on and the scenes, each with its <c>like</c> already followed. Pure data: the
/// kinds, styles and config fragments are checked against the installed widgets by
/// <see cref="SceneWidgets"/>, where they are known.
/// </summary>
public sealed record SceneFile(SceneScreen Screen, IReadOnlyList<Scene> Scenes)
{
    /// <summary>The scene named <paramref name="name"/> (names are exact), or null.</summary>
    public Scene? Find(string name) => Scenes.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
}

/// <summary>[S-1075] The stand-in display, in DIPs.</summary>
public sealed record SceneScreen(double Width, double Height)
{
    /// <summary>A full-HD display, when the file names none.</summary>
    public static SceneScreen Default { get; } = new(1920, 1080);
}

/// <summary>
/// [S-1075] One picture: its application theme, the design text (<see cref="DesignText"/>), an
/// optional wallpaper (a path relative to the scenes file, or a sample picture, [S-2210]), its
/// widgets, when present the edit mode it is drawn in, and whether Windows' taskbar stands in along
/// the bottom ([S-2200]).
/// </summary>
public sealed record Scene(string Name, AppTheme Theme, string Design, string? Wallpaper, IReadOnlyList<SceneWidget> Widgets, SceneEdit? Edit, double Scale = 1, bool Taskbar = false)
{
    /// <summary>[S-2210] The prefix of a wallpaper that is one of the sample pictures, drawn in code: <c>sample:lake-at-dusk</c>.</summary>
    public const string SamplePrefix = "sample:";

    /// <summary>
    /// The widgets drawn selected (in edit mode only, [S-1077]): those marked <c>selected</c>, [S-2458] the
    /// members of the group <c>edit.group</c> selects, and the member <c>edit.enter</c> enters.
    /// </summary>
    public IEnumerable<SceneWidget> Selected => Widgets.Where(w => w.Selected
        || (Edit?.Group is { } group && string.Equals(w.Group, group, StringComparison.Ordinal))
        || (Edit?.Enter is { } entered && string.Equals(w.Id, entered, StringComparison.Ordinal)));

    /// <summary>[S-2458] The scene's groups by name, in the order their first member stands; each holds two or more widgets.</summary>
    public IReadOnlyList<string> Groups => [.. Widgets.Select(w => w.Group).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>[S-2210] The sample picture's name when the wallpaper is one (<c>lake-at-dusk</c>), else null.</summary>
    public string? SampleWallpaper => Wallpaper is { } w && w.StartsWith(SamplePrefix, StringComparison.Ordinal) ? w[SamplePrefix.Length..] : null;

    /// <summary>[S-1075] The wallpaper's path relative to the scenes file when it is a file, else null.</summary>
    public string? WallpaperFile => Wallpaper is not null && SampleWallpaper is null ? Wallpaper : null;

    /// <summary>
    /// [S-2201] The stand-in taskbar's height in DIPs: Windows' 48 at the scene's display scale
    /// ([S-1080]: 60 at 1.25), or 0 for a scene without one.
    /// </summary>
    public double TaskbarHeight => Taskbar ? SceneTaskbar.Height * Scale : 0;

    /// <summary>[S-2202] How far down a widget may reach on <paramref name="screen"/>: the display less the taskbar.</summary>
    public double WorkBottom(SceneScreen screen) => screen.Height - TaskbarHeight;
}

/// <summary>[S-2201] Windows 11's taskbar as the scenes stand it in: its height at 100 %, in DIPs.</summary>
public static class SceneTaskbar
{
    public const double Height = 48;
}

/// <summary>[S-1075] A rectangle on the stand-in display, in DIPs.</summary>
public readonly record struct SceneRect(double X, double Y, double Width, double Height);

/// <summary>
/// [S-1076] One widget of a scene: its kind (checked for its form here, against the installed widgets
/// by <see cref="SceneWidgets"/>), where its card is, and what differs from its kind's defaults. A
/// null property is the kind's own. <see cref="Path"/> is where it stands in the file, for a refusal
/// that names it.
/// </summary>
public sealed record SceneWidget(
    string Path,
    string Id,
    WidgetId Kind,
    SceneRect At,
    string? State = null,
    string? Style = null,
    HeaderPlacement? Header = null,
    FillChoice? Background = null,
    FillChoice? Items = null,
    string? ItemText = null,
    double? Scale = null,
    ContentAlign? Align = null,
    string? Label = null,
    bool Bare = false,
    string? Config = null,
    bool Selected = false,
    string? Group = null);

/// <summary>
/// [S-1077] Edit mode as a scene draws it: the edit bar (<see cref="Bar"/>), a submenu path opened
/// in the selection's properties menu (<see cref="Open"/>), the add menu open under the bar
/// (<see cref="Add"/>) and the snap grid (<see cref="Grid"/>). [S-2458] <see cref="Group"/> selects a
/// group of the scene whole; <see cref="Enter"/> enters its group with one member (a widget's id).
/// </summary>
public sealed record SceneEdit(bool Bar = true, string? Open = null, bool Add = false, bool Grid = false, string? Group = null, string? Enter = null);

/// <summary>[S-1075] Why a scenes file was refused: where (a JSON path such as <c>$.scenes[1].widgets[0].at</c>) and what.</summary>
public sealed record SceneRefusal(string Path, string Problem)
{
    public override string ToString() => $"{Path}: {Problem}";
}
