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
/// optional wallpaper (a path relative to the scenes file), its widgets and, when present, the edit
/// mode it is drawn in.
/// </summary>
public sealed record Scene(string Name, AppTheme Theme, string Design, string? Wallpaper, IReadOnlyList<SceneWidget> Widgets, SceneEdit? Edit)
{
    /// <summary>The widgets drawn selected (in edit mode only, [S-1077]).</summary>
    public IEnumerable<SceneWidget> Selected => Widgets.Where(w => w.Selected);
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
    bool Selected = false);

/// <summary>
/// [S-1077] Edit mode as a scene draws it: the edit bar (<see cref="Bar"/>), a submenu path opened
/// in the selection's properties menu (<see cref="Open"/>), the add menu open under the bar
/// (<see cref="Add"/>) and the snap grid (<see cref="Grid"/>).
/// </summary>
public sealed record SceneEdit(bool Bar = true, string? Open = null, bool Add = false, bool Grid = false);

/// <summary>[S-1075] Why a scenes file was refused: where (a JSON path such as <c>$.scenes[1].widgets[0].at</c>) and what.</summary>
public sealed record SceneRefusal(string Path, string Problem)
{
    public override string ToString() => $"{Path}: {Problem}";
}
