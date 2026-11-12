using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-2475] A ready-made group as a scene, for <c>--recipe-png</c>: its widgets where the recipe puts
/// them, at <see cref="Origin"/> on a full-HD stand-in with a sample wallpaper and the taskbar, in edit
/// mode with the new group selected, as a placement leaves it. Built from the recipe, never written by
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
    /// of a kind by its kind and place), its preview state from <paramref name="states"/> by kind.
    /// </summary>
    public static SceneFile Of(ReadyMadeGroup recipe, IReadOnlyDictionary<string, string> states)
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

        var scene = new Scene(recipe.Id.ToLowerInvariant(), AppTheme.Dark, string.Empty, Wallpaper, widgets, new SceneEdit(Bar: false, Group: name), Scale: 1, Taskbar: true);
        return new SceneFile(SceneScreen.Default, [scene]);
    }
}
