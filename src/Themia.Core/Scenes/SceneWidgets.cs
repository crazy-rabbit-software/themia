using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-1076] A scene's widgets against the installed ones: the kind must be installed, the style one of
/// its styles, and the config fragment must name only properties of the kind's config and merge over
/// its default (<see cref="StateJson.Merged"/>), so a misspelt setting is refused rather than
/// dropped by the reader. What the views alone know (a preview state's name) is the render's to check.
/// </summary>
public static class SceneWidgets
{
    /// <summary>
    /// The widget a card of the scene shows: the kind's default, the config fragment over it, the
    /// style, then <c>bare</c> (a transparent background and no header, as the first-run desktop's
    /// <see cref="FirstRunDesktop.Seed.Bare"/>) and over that what the scene names itself. Null and
    /// <paramref name="refusal"/> when the installed widgets cannot give it.
    /// </summary>
    public static WidgetInstance? Build(SceneWidget widget, SceneScreen screen, out SceneRefusal? refusal)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(screen);
        refusal = null;

        if (!ModuleCatalog.TryGet(widget.Kind, out var descriptor))
        {
            refusal = new SceneRefusal(widget.Path + ".kind", $"this build has no widget \"{widget.Kind}\"");
            return null;
        }

        var at = widget.At;
        var model = WidgetInstance.Create(widget.Kind, new FracRect(at.X / screen.Width, at.Y / screen.Height, at.Width / screen.Width, at.Height / screen.Height));

        if (widget.Config is { } fragment)
        {
            if (Config(descriptor, model.Config, fragment, widget.Path + ".config", out refusal) is not { } config)
            {
                return null;
            }

            model.Config = config;
        }

        if (widget.Style is { } styleName)
        {
            if (WidgetStyles.Find(widget.Kind, styleName) is not { } style)
            {
                var styles = WidgetStyles.For(widget.Kind);
                refusal = new SceneRefusal(widget.Path + ".style", styles.Count == 0
                    ? $"{widget.Kind} has no styles"
                    : $"{widget.Kind} has no style \"{styleName}\" (has: {string.Join(", ", styles.Select(s => s.Name))})");
                return null;
            }

            WidgetStyles.Set(model.Config, style.Value);
        }

        if (widget.Bare)
        {
            model.Background = FillChoice.Transparent();
            model.Header = HeaderPlacement.Hidden;
        }

        if (widget.Header is { } header)
        {
            model.Header = header;
        }

        if (widget.Background is { } background)
        {
            model.Background = background.Clone();
        }

        if (widget.Items is { } items)
        {
            model.ItemBackground = items.Clone();
        }

        model.ItemTextColor = widget.ItemText ?? model.ItemTextColor;
        model.Scale = widget.Scale ?? model.Scale;
        model.Align = widget.Align ?? model.Align;
        model.Label = widget.Label ?? model.Label;
        return model;
    }

    /// <summary>Every widget of <paramref name="scene"/> that the installed widgets cannot give, each with why.</summary>
    public static IReadOnlyList<SceneRefusal> Check(Scene scene, SceneScreen screen)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var refused = new List<SceneRefusal>();
        foreach (var widget in scene.Widgets)
        {
            if (Build(widget, screen, out var refusal) is null && refusal is not null)
            {
                refused.Add(refusal);
            }
        }

        return refused;
    }

    /// <summary>The fragment over the kind's default: every property one the kind's config has.</summary>
    private static WidgetConfig? Config(WidgetDescriptor descriptor, WidgetConfig defaults, string fragment, string path, out SceneRefusal? refusal)
    {
        refusal = null;
        var known = StateJson.Options.GetTypeInfo(descriptor.ConfigType).Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        try
        {
            using (var document = JsonDocument.Parse(fragment))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!known.Contains(property.Name))
                    {
                        refusal = new SceneRefusal($"{path}.{property.Name}", $"{descriptor.Id}'s config has no \"{property.Name}\" (has: {string.Join(", ", known.Order(StringComparer.Ordinal))})");
                        return null;
                    }
                }
            }

            return StateJson.Merged(defaults, fragment);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            refusal = new SceneRefusal(path, $"does not merge over {descriptor.Id}'s config ({ex.Message})");
            return null;
        }
    }
}
