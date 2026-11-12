using System.Globalization;
using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Scenes;

/// <summary>
/// [S-1075] Reads a store scenes file (<c>store/scenes.json</c>), a trust boundary: the whole
/// document is checked and either every scene comes back, its <c>like</c> followed, or the first
/// problem does, naming its JSON path. Strict on purpose, so a typo never vanishes: a property this
/// reader does not know, or one written twice, is refused, not ignored; numbers are finite; names are
/// unique and of <c>[a-z0-9-]</c>; a rectangle lies inside the display; a closed set (theme, header,
/// alignment) is one of its names. What only the installed widgets know (a kind, a style, a config
/// fragment) is <see cref="SceneWidgets"/>' to check. Pure.
/// </summary>
public static class SceneParser
{
    /// <summary>The largest display side a scene may name, in DIPs.</summary>
    public const double MaxScreenSide = 8192;

    /// <summary>The smallest display side a scene may name, in DIPs.</summary>
    public const double MinScreenSide = 320;

    /// <summary>The file's scenes, or null and <paramref name="refusal"/> saying where and why.</summary>
    public static SceneFile? Parse(string json, out SceneRefusal? refusal)
    {
        refusal = null;
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            return File(document.RootElement);
        }
        catch (JsonException ex)
        {
            refusal = new SceneRefusal("$", $"not JSON ({ex.Message})");
            return null;
        }
        catch (Refused ex)
        {
            refusal = ex.Refusal;
            return null;
        }
    }

    private static SceneFile File(JsonElement root)
    {
        var props = Properties(root, "$", "screen", "scenes");
        var screen = props.TryGetValue("screen", out var s) ? Screen(s, "$.screen") : SceneScreen.Default;
        if (!props.TryGetValue("scenes", out var list))
        {
            throw Refuse("$", "no \"scenes\"");
        }

        var items = Array(list, "$.scenes");
        if (items.Count == 0)
        {
            throw Refuse("$.scenes", "no scene");
        }

        var drafts = new List<Draft>();
        for (var i = 0; i < items.Count; i++)
        {
            var draft = ReadDraft(items[i], $"$.scenes[{i}]", screen);
            if (drafts.Any(d => d.Name == draft.Name))
            {
                throw Refuse(draft.Path + ".name", $"the name \"{draft.Name}\" is taken by another scene");
            }

            drafts.Add(draft);
        }

        var resolved = new Dictionary<string, Scene>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            _ = Resolve(draft, drafts, resolved, []);
        }

        var scenes = drafts.Select(d => resolved[d.Name]).ToList();
        foreach (var (scene, draft) in scenes.Zip(drafts))
        {
            Check(scene, draft.Path);
        }

        return new SceneFile(screen, scenes);
    }

    /// <summary>[S-1075] A scene with its <c>like</c> followed: what it does not set itself comes from the scene it is like.</summary>
    private static Scene Resolve(Draft draft, List<Draft> drafts, Dictionary<string, Scene> resolved, List<string> visiting)
    {
        if (resolved.TryGetValue(draft.Name, out var done))
        {
            return done;
        }

        if (visiting.Contains(draft.Name))
        {
            throw Refuse(draft.Path + ".like", $"\"like\" goes round in a circle ({string.Join(" > ", visiting.Append(draft.Name))})");
        }

        Scene? basis = null;
        if (draft.Like is { } like)
        {
            var other = drafts.Find(d => d.Name == like)
                ?? throw Refuse(draft.Path + ".like", $"no scene is named \"{like}\"");
            visiting.Add(draft.Name);
            basis = Resolve(other, drafts, resolved, visiting);
            visiting.Remove(draft.Name);
        }
        else if (draft.Widgets is null)
        {
            throw Refuse(draft.Path, "no \"widgets\" (and no \"like\" to take them from)");
        }

        var scene = new Scene(
            draft.Name,
            draft.Theme ?? basis?.Theme ?? AppTheme.Dark,
            draft.Design ?? basis?.Design ?? string.Empty,
            draft.HasWallpaper ? draft.Wallpaper : basis?.Wallpaper,
            draft.Widgets ?? basis!.Widgets,
            draft.HasEdit ? draft.Edit : basis?.Edit);
        resolved[draft.Name] = scene;
        return scene;
    }

    /// <summary>What only the whole scene can tell: a selection needs edit mode, an open submenu a selection.</summary>
    private static void Check(Scene scene, string path)
    {
        if (scene.Edit is null && scene.Selected.FirstOrDefault() is { } selected)
        {
            throw Refuse(path, $"the widget at {selected.Path} is selected, but the scene has no \"edit\"");
        }

        if (scene.Edit is { Open: not null } && !scene.Selected.Any())
        {
            throw Refuse(path, "\"edit.open\" opens a submenu of the properties menu, but no widget is selected");
        }
    }

    /// <summary>One scene as written, before its <c>like</c> is followed: null is "not set here".</summary>
    private sealed class Draft
    {
        public required string Name { get; init; }

        public required string Path { get; init; }

        public string? Like { get; init; }

        public AppTheme? Theme { get; init; }

        public string? Design { get; init; }

        public bool HasWallpaper { get; init; }

        public string? Wallpaper { get; init; }

        public List<SceneWidget>? Widgets { get; init; }

        public bool HasEdit { get; init; }

        public SceneEdit? Edit { get; init; }
    }

    /// <summary>
    /// One scene as written. A <c>like</c> scene may set <c>wallpaper</c> or <c>edit</c> to null to
    /// draw without the one it would take over.
    /// </summary>
    private static Draft ReadDraft(JsonElement element, string path, SceneScreen screen)
    {
        var props = Properties(element, path, "name", "like", "theme", "design", "wallpaper", "widgets", "edit");
        if (!props.TryGetValue("name", out var nameElement))
        {
            throw Refuse(path, "no \"name\"");
        }

        var name = Text(nameElement, path + ".name");
        if (name.Length == 0 || !name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw Refuse(path + ".name", $"\"{name}\" is not a name of a-z, 0-9 and '-'");
        }

        List<SceneWidget>? widgets = null;
        if (props.TryGetValue("widgets", out var list))
        {
            var items = Array(list, path + ".widgets");
            widgets = [.. items.Select((w, i) => Widget(w, $"{path}.widgets[{i}]", screen))];
        }

        var hasWallpaper = props.TryGetValue("wallpaper", out var wallpaper);
        var hasEdit = props.TryGetValue("edit", out var edit);
        return new Draft
        {
            Name = name,
            Path = path,
            Like = props.TryGetValue("like", out var like) ? Text(like, path + ".like") : null,
            Theme = props.TryGetValue("theme", out var theme) ? Theme(theme, path + ".theme") : null,
            Design = props.TryGetValue("design", out var design) ? DesignOf(design, path + ".design") : null,
            HasWallpaper = hasWallpaper,
            Wallpaper = hasWallpaper ? Wallpaper(wallpaper, path + ".wallpaper") : null,
            Widgets = widgets,
            HasEdit = hasEdit,
            Edit = hasEdit && edit.ValueKind != JsonValueKind.Null ? Edit(edit, path + ".edit") : null,
        };
    }

    private static SceneScreen Screen(JsonElement element, string path)
    {
        var props = Properties(element, path, "width", "height");
        var width = props.TryGetValue("width", out var w) ? Number(w, path + ".width") : SceneScreen.Default.Width;
        var height = props.TryGetValue("height", out var h) ? Number(h, path + ".height") : SceneScreen.Default.Height;
        foreach (var (side, name) in new[] { (width, "width"), (height, "height") })
        {
            if (side < MinScreenSide || side > MaxScreenSide || side != Math.Round(side))
            {
                throw Refuse($"{path}.{name}", $"{Show(side)} is not a whole number of DIPs from {MinScreenSide} to {MaxScreenSide}");
            }
        }

        return new SceneScreen(width, height);
    }

    private static AppTheme Theme(JsonElement element, string path) => Text(element, path) switch
    {
        "dark" => AppTheme.Dark,
        "light" => AppTheme.Light,
        var other => throw Refuse(path, $"\"{other}\" is not a theme (dark or light)"),
    };

    /// <summary>The design text, checked by applying it to a design no one keeps: every pair must read.</summary>
    private static string DesignOf(JsonElement element, string path)
    {
        var text = Text(element, path);
        var refused = DesignText.Apply(new DesignState(), text);
        if (refused.Count > 0)
        {
            throw Refuse(path, $"cannot read {string.Join(", ", refused.Select(r => $"'{r}'"))} (keys and values as --design takes them)");
        }

        return text;
    }

    private static string? Wallpaper(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var text = Text(element, path);
        if (text.Trim().Length == 0 || System.IO.Path.IsPathRooted(text))
        {
            throw Refuse(path, "a wallpaper is a picture's path relative to the scenes file");
        }

        return text;
    }

    private static SceneEdit Edit(JsonElement element, string path)
    {
        var props = Properties(element, path, "bar", "open", "add", "grid");
        string? open = null;
        if (props.TryGetValue("open", out var o))
        {
            open = Text(o, path + ".open");
            if (open.Trim().Length == 0)
            {
                throw Refuse(path + ".open", "an empty submenu path");
            }
        }

        return new SceneEdit(
            props.TryGetValue("bar", out var bar) ? Bool(bar, path + ".bar") : true,
            open,
            props.TryGetValue("add", out var add) && Bool(add, path + ".add"),
            props.TryGetValue("grid", out var grid) && Bool(grid, path + ".grid"));
    }

    private static SceneWidget Widget(JsonElement element, string path, SceneScreen screen)
    {
        var props = Properties(element, path,
            "kind", "at", "state", "style", "header", "background", "items", "itemText", "scale", "align", "label", "bare", "config", "selected");

        if (!props.TryGetValue("kind", out var kindElement))
        {
            throw Refuse(path, "no \"kind\"");
        }

        var kindText = Text(kindElement, path + ".kind");
        if (!WidgetId.TryParse(kindText, out var kind))
        {
            throw Refuse(path + ".kind", $"\"{kindText}\" is not a widget id (camelCase, as the add menu's ids)");
        }

        if (!props.TryGetValue("at", out var atElement))
        {
            throw Refuse(path, "no \"at\" ([x, y, width, height] in DIPs)");
        }

        var at = Rect(atElement, path + ".at", screen);

        return new SceneWidget(
            path,
            kind,
            at,
            State: props.TryGetValue("state", out var state) ? Word(state, path + ".state") : null,
            Style: props.TryGetValue("style", out var style) ? Word(style, path + ".style") : null,
            Header: props.TryGetValue("header", out var header) ? Named<HeaderPlacement>(header, path + ".header") : null,
            Background: props.TryGetValue("background", out var background) ? Fill(background, path + ".background") : null,
            Items: props.TryGetValue("items", out var items) ? Fill(items, path + ".items") : null,
            ItemText: props.TryGetValue("itemText", out var itemText) ? Colour(itemText, path + ".itemText") : null,
            Scale: props.TryGetValue("scale", out var scale) ? Scale(scale, path + ".scale") : null,
            Align: props.TryGetValue("align", out var align) ? Named<ContentAlign>(align, path + ".align") : null,
            Label: props.TryGetValue("label", out var label) ? Text(label, path + ".label") : null,
            Bare: props.TryGetValue("bare", out var bare) && Bool(bare, path + ".bare"),
            Config: props.TryGetValue("config", out var config) ? Config(config, path + ".config") : null,
            Selected: props.TryGetValue("selected", out var selected) && Bool(selected, path + ".selected"));
    }

    /// <summary>[S-1075] <c>[x, y, width, height]</c>: finite, a positive size, wholly on the display.</summary>
    private static SceneRect Rect(JsonElement element, string path, SceneScreen screen)
    {
        var items = Array(element, path);
        if (items.Count != 4)
        {
            throw Refuse(path, $"{items.Count} numbers; a rectangle is [x, y, width, height]");
        }

        var n = items.Select((e, i) => Number(e, $"{path}[{i}]")).ToArray();
        var rect = new SceneRect(n[0], n[1], n[2], n[3]);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw Refuse(path, "a width and a height above 0");
        }

        if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > screen.Width || rect.Y + rect.Height > screen.Height)
        {
            throw Refuse(path, $"[{Show(rect.X)}, {Show(rect.Y)}, {Show(rect.Width)}, {Show(rect.Height)}] is not inside the {Show(screen.Width)} × {Show(screen.Height)} display");
        }

        return rect;
    }

    private static FillChoice Fill(JsonElement element, string path)
    {
        var text = Text(element, path);
        return DesignText.Fill(text)
            ?? throw Refuse(path, $"\"{text}\" is not a fill (default, transparent or #RRGGBB[@percent])");
    }

    private static string Colour(JsonElement element, string path)
    {
        var text = Text(element, path);
        return HexColor.TryParse(text, out _, out _, out _)
            ? HexColor.Normalise(text)
            : throw Refuse(path, $"\"{text}\" is not a colour (#RRGGBB)");
    }

    private static double Scale(JsonElement element, string path)
    {
        var scale = Number(element, path);
        return scale is >= WidgetScale.Min and <= WidgetScale.Max
            ? WidgetScale.Normalise(scale)
            : throw Refuse(path, $"{Show(scale)} is not a widget scale ({WidgetScale.Min} to {WidgetScale.Max})");
    }

    /// <summary>A config fragment: an object, kept as written for <see cref="SceneWidgets"/> to merge.</summary>
    private static string Config(JsonElement element, string path)
        => element.ValueKind == JsonValueKind.Object
            ? element.GetRawText()
            : throw Refuse(path, $"a config fragment is an object, not {Kind(element)}");

    /// <summary>A closed set by its name, in any case (<c>hidden</c>, <c>center</c>).</summary>
    private static T Named<T>(JsonElement element, string path)
        where T : struct, Enum
    {
        var text = Text(element, path);
        return Enum.TryParse<T>(text, ignoreCase: true, out var value) && Enum.IsDefined(value) && !text.Any(char.IsDigit)
            ? value
            : throw Refuse(path, $"\"{text}\" is not one of {string.Join(", ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))}");
    }

    /// <summary>A non-empty name (a state, a style), as written.</summary>
    private static string Word(JsonElement element, string path)
    {
        var text = Text(element, path);
        return text.Trim().Length > 0 ? text : throw Refuse(path, "an empty name");
    }

    /// <summary>The object's properties by name; one this reader does not know, or one written twice, is refused.</summary>
    private static Dictionary<string, JsonElement> Properties(JsonElement element, string path, params string[] known)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Refuse(path, $"an object was expected, not {Kind(element)}");
        }

        var props = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
            {
                throw Refuse($"{path}.{property.Name}", $"unknown property (known here: {string.Join(", ", known)})");
            }

            if (!props.TryAdd(property.Name, property.Value))
            {
                throw Refuse($"{path}.{property.Name}", "written twice");
            }
        }

        return props;
    }

    private static List<JsonElement> Array(JsonElement element, string path)
        => element.ValueKind == JsonValueKind.Array
            ? [.. element.EnumerateArray()]
            : throw Refuse(path, $"an array was expected, not {Kind(element)}");

    private static string Text(JsonElement element, string path)
        => element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw Refuse(path, $"a string was expected, not {Kind(element)}");

    private static bool Bool(JsonElement element, string path)
        => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Refuse(path, $"true or false was expected, not {Kind(element)}"),
        };

    private static double Number(JsonElement element, string path)
        => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value) && double.IsFinite(value)
            ? value
            : throw Refuse(path, element.ValueKind == JsonValueKind.Number ? "not a finite number" : $"a number was expected, not {Kind(element)}");

    private static string Kind(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "true or false",
        JsonValueKind.Null => "null",
        _ => "nothing",
    };

    private static string Show(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static Refused Refuse(string path, string problem) => new(new SceneRefusal(path, problem));

    /// <summary>The first problem, carried out of the walk to <see cref="Parse"/>, which turns it into the refusal.</summary>
    private sealed class Refused(SceneRefusal refusal) : Exception(refusal.ToString())
    {
        public SceneRefusal Refusal { get; } = refusal;
    }
}
