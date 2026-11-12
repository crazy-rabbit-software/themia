using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-2475] A ready-made group as a scene, for <c>--recipe-png</c>: its widgets where the recipe puts
/// them, at <see cref="Origin"/> on a full-HD stand-in with a sample wallpaper and the taskbar, as one
/// group on the desktop: no edit chrome, so the picture is the panel the recipe makes; asked for edit mode,
/// the new group selected under the bar, as a placement leaves it. Built from the recipe, never written by
/// hand, so the picture is the recipe.
/// </summary>
public static class ReadyMadeScene
{
    /// <summary>Where the group's corner stands on the stand-in display, in DIPs.</summary>
    public const double Origin = 160;

    /// <summary>The wallpaper the pictures share.</summary>
    public const string Wallpaper = Scene.SamplePrefix + "lake-at-dusk";

    /// <summary>
    /// The scene of <paramref name="recipe"/>: one widget per recipe widget, named by its kind (a second
    /// of a kind by its kind and place), its preview state from <paramref name="states"/> by kind, in
    /// <paramref name="theme"/> (dark when not given); in edit mode with the bar when <paramref name="edit"/>.
    /// </summary>
    public static SceneFile Of(ReadyMadeGroup recipe, IReadOnlyDictionary<string, string> states, bool edit = false, AppTheme theme = AppTheme.Dark)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(states);

        var name = recipe.Name.Text();
        var widgets = recipe.Widgets.Select((w, i) => new SceneWidget(
            $"$.readyMade.{recipe.Id}.widgets[{i}]",
            recipe.Widgets.Take(i).Any(o => o.Kind == w.Kind) ? $"{w.Kind}-{i}" : w.Kind,
            new WidgetId(w.Kind),
            new SceneRect(Origin + w.Box.X, Origin + w.Box.Y, w.Box.Width, w.Box.Height),
            State: states.TryGetValue(w.Kind, out var state) ? state : null,
            Header: w.Header,
            Label: w.Label,
            Config: w.Config,
            Group: name)).ToList();

        // A selection's page hangs from the bar's entry ([S-2343]): edit mode is the bar, the group selected.
        var scene = new Scene(recipe.Id.ToLowerInvariant(), theme, string.Empty, Wallpaper, widgets, edit ? new SceneEdit(Bar: true, Group: name) : null, Scale: 1, Taskbar: true);
        return new SceneFile(SceneScreen.Default, [scene]);
    }
}
