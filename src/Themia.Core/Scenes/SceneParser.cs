using System.Globalization;
using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Links;
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
            Check(scene, draft.Path, screen);
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
            draft.HasEdit ? draft.Edit : basis?.Edit,
            draft.Scale ?? basis?.Scale ?? 1,
            draft.Taskbar ?? basis?.Taskbar ?? false,
            draft.HasLinks ? draft.Links : basis?.Links);
        resolved[draft.Name] = scene;
        return scene;
    }

    /// <summary>What only the whole scene can tell: a selection needs edit mode, an open submenu a selection, a widget the work area.</summary>
    private static void Check(Scene scene, string path, SceneScreen screen)
    {
        // [S-2202] With the taskbar, the desktop ends above it, as Windows' work area does.
        var bottom = scene.WorkBottom(screen);
        foreach (var widget in scene.Widgets.Where(w => w.At.Y + w.At.Height > bottom))
        {
            throw Refuse(widget.Path + ".at", $"reaches under the taskbar: with \"taskbar\" at scale {Show(scene.Scale)} a widget stays above y = {Show(bottom)} (the display's {Show(screen.Height)} less the taskbar's {Show(scene.TaskbarHeight)})");
        }

        // [S-1079] A widget is named by its id, its kind where it gives none: two of a kind need ids to be told apart.
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var widget in scene.Widgets)
        {
            if (!named.Add(widget.Id))
            {
                throw Refuse(widget.Path, $"the scene has another widget named \"{widget.Id}\": give one an \"id\"");
            }
        }

        if (scene.Edit is null && scene.Selected.FirstOrDefault() is { } selected)
        {
            throw Refuse(path, $"the widget at {selected.Path} is selected, but the scene has no \"edit\"");
        }

        // [S-2458] A group holds two or more widgets; the edit block selects one, or enters one with a member.
        foreach (var name in scene.Groups)
        {
            if (scene.Widgets.Count(w => string.Equals(w.Group, name, StringComparison.Ordinal)) < 2)
            {
                throw Refuse(path, $"the group \"{name}\" holds one widget: a group holds two or more");
            }
        }

        if (scene.Edit is { Group: { } selectsGroup } && !scene.Groups.Contains(selectsGroup, StringComparer.Ordinal))
        {
            throw Refuse(path + ".edit.group", $"no widget is in a group \"{selectsGroup}\"");
        }

        if (scene.Edit is { Enter: { } enters })
        {
            if (scene.Edit.Group is not null)
            {
                throw Refuse(path + ".edit", "\"group\" selects a group whole and \"enter\" one member inside it: one of the two");
            }

            if (scene.Widgets.FirstOrDefault(w => string.Equals(w.Id, enters, StringComparison.Ordinal)) is not { Group: not null })
            {
                throw Refuse(path + ".edit.enter", $"\"{enters}\" is no widget of a group in the scene");
            }
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

        public double? Scale { get; init; }

        public bool? Taskbar { get; init; }

        public bool HasWallpaper { get; init; }

        public string? Wallpaper { get; init; }

        public List<SceneWidget>? Widgets { get; init; }

        public bool HasEdit { get; init; }

        public SceneEdit? Edit { get; init; }

        public bool HasLinks { get; init; }

        public SceneLinks? Links { get; init; }
    }

    /// <summary>
    /// One scene as written. A <c>like</c> scene may set <c>wallpaper</c> or <c>edit</c> to null to
    /// draw without the one it would take over.
    /// </summary>
    private static Draft ReadDraft(JsonElement element, string path, SceneScreen screen)
    {
        var props = Properties(element, path, "name", "like", "theme", "design", "scale", "taskbar", "wallpaper", "widgets", "edit", "links");
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
            widgets = [.. items.SelectMany((w, i) => Entry(w, $"{path}.widgets[{i}]", screen))];
        }

        var hasWallpaper = props.TryGetValue("wallpaper", out var wallpaper);
        var hasEdit = props.TryGetValue("edit", out var edit);
        var hasLinks = props.TryGetValue("links", out var links);
        return new Draft
        {
            Name = name,
            Path = path,
            Like = props.TryGetValue("like", out var like) ? Text(like, path + ".like") : null,
            Theme = props.TryGetValue("theme", out var theme) ? Theme(theme, path + ".theme") : null,
            Design = props.TryGetValue("design", out var design) ? DesignOf(design, path + ".design") : null,
            Scale = props.TryGetValue("scale", out var scale) ? Scale(scale, path + ".scale") : null,
            Taskbar = props.TryGetValue("taskbar", out var taskbar) ? Bool(taskbar, path + ".taskbar") : null,
            HasWallpaper = hasWallpaper,
            Wallpaper = hasWallpaper ? Wallpaper(wallpaper, path + ".wallpaper") : null,
            Widgets = widgets,
            HasEdit = hasEdit,
            Edit = hasEdit && edit.ValueKind != JsonValueKind.Null ? Edit(edit, path + ".edit") : null,
            HasLinks = hasLinks,
            Links = hasLinks && links.ValueKind != JsonValueKind.Null ? Links(links, path + ".links") : null,
        };
    }

    /// <summary>
    /// [S-11909] What the picture points at and has picked: days (<c>2026-11-13</c>, <c>today+1</c>), apps
    /// (an executable's stem), a moment (<c>-00:00:30</c>, or <c>{ "at": .., "subject": "cpu" }</c>), a
    /// person, a file, a drive, a device. Each is checked as its widgets key it; an empty block is refused.
    /// </summary>
    private static SceneLinks Links(JsonElement element, string path)
    {
        var props = Properties(element, path, [.. SceneLinks.Names]);
        if (props.Count == 0)
        {
            throw Refuse(path, "no link is set (known: " + string.Join(", ", SceneLinks.Names) + ")");
        }

        SceneDay? Day(string name)
            => props.TryGetValue(name, out var value)
                ? SceneDay.Parse(Text(value, $"{path}.{name}")) ?? throw Refuse($"{path}.{name}", "not a day (2026-11-13, today, today+1)")
                : null;

        string? Keyed(string name, Func<string, string?> key, string what)
            => props.TryGetValue(name, out var value)
                ? key(Text(value, $"{path}.{name}")) is not null ? Text(value, $"{path}.{name}") : throw Refuse($"{path}.{name}", $"not {what}")
                : null;

        SceneInstant? instant = null;
        if (props.TryGetValue("hoveredInstant", out var moment))
        {
            var at = $"{path}.hoveredInstant";
            string text, subject = string.Empty;
            if (moment.ValueKind == JsonValueKind.Object)
            {
                var parts = Properties(moment, at, "at", "subject");
                text = parts.TryGetValue("at", out var when) ? Text(when, at + ".at") : throw Refuse(at, "no \"at\"");
                subject = parts.TryGetValue("subject", out var of) ? Text(of, at + ".subject").Trim() : string.Empty;
            }
            else
            {
                text = Text(moment, at);
            }

            instant = SceneInstant.Parse(text, subject) ?? throw Refuse(at, "not a moment (-00:00:30, or 2026-11-12T14:05:00+01:00)");
        }

        return new SceneLinks(
            HoveredDate: Day("hoveredDate"),
            SelectedDate: Day("selectedDate"),
            HoveredApp: Keyed("hoveredApp", LinkKeys.AppOfProcess, "an app"),
            SelectedApp: Keyed("selectedApp", LinkKeys.AppOfProcess, "an app"),
            HoveredInstant: instant,
            HoveredPerson: Keyed("hoveredPerson", text => LinkKeys.Person(text, text), "a person"),
            HoveredFile: Keyed("hoveredFile", text => Path.IsPathFullyQualified(text) ? LinkKeys.File(text) : null, "a full path"),
            HoveredDrive: Keyed("hoveredDrive", LinkKeys.Drive, "a drive (C:)"),
            HoveredDevice: Keyed("hoveredDevice", LinkKeys.Device, "a device address (A4:C1:38:0B:22:9F)"));
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
        if (text.StartsWith(Scene.SamplePrefix, StringComparison.Ordinal))
        {
            // [S-2210] The form only: which sample pictures there are is the renderer's to know.
            var name = text[Scene.SamplePrefix.Length..];
            return name.Length > 0 && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                ? text
                : throw Refuse(path, $"\"{text}\" is not a sample picture's name (sample: and its name of a-z, 0-9 and '-', as sample:lake-at-dusk)");
        }

        if (text.Trim().Length == 0 || System.IO.Path.IsPathRooted(text))
        {
            throw Refuse(path, "a wallpaper is a picture's path relative to the scenes file, or sample:<name>");
        }

        return text;
    }

    private static SceneEdit Edit(JsonElement element, string path)
    {
        var props = Properties(element, path, "bar", "open", "add", "grid", "group", "enter");
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
            props.TryGetValue("grid", out var grid) && Bool(grid, path + ".grid"),
            props.TryGetValue("group", out var group) ? Text(group, path + ".group") : null,
            props.TryGetValue("enter", out var enter) ? Text(enter, path + ".enter") : null);
    }

    /// <summary>
    /// [S-11860] One entry of a scene's widgets: a widget, or a ready-made group by its id
    /// (<c>{ "readyMade": "thisPc", "at": [x, y] }</c>), which stands for the group's widgets where the
    /// recipe puts them, as one group.
    /// </summary>
    private static IEnumerable<SceneWidget> Entry(JsonElement element, string path, SceneScreen screen)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("readyMade", out _)
            ? ReadyMade(element, path, screen)
            : [Widget(element, path, screen)];

    /// <summary>
    /// [S-11860] A ready-made group in a scene: every widget of the recipe at its box from the entry's
    /// point, with the recipe's config, header and label, named <c>id-kind</c> (a second of a kind
    /// <c>id-kind-n</c>; the id is the entry's, else the recipe's), in one group named as the entry says,
    /// else as the recipe is called. <c>states</c> gives a kind its preview state. So a picture of a group
    /// is the group the add page places, never a copy of its boxes.
    /// </summary>
    private static IEnumerable<SceneWidget> ReadyMade(JsonElement element, string path, SceneScreen screen)
    {
        var props = Properties(element, path, "readyMade", "at", "id", "group", "states");
        var recipeId = Text(props["readyMade"], path + ".readyMade");
        var recipe = ReadyMadeGroups.Find(recipeId)
            ?? throw Refuse(path + ".readyMade", $"no ready-made group \"{recipeId}\" (has: {string.Join(", ", ReadyMadeGroups.All.Select(r => r.Id))})");

        if (!props.TryGetValue("at", out var atElement))
        {
            throw Refuse(path, "no \"at\" ([x, y] in DIPs: the group's top-left corner)");
        }

        var point = Array(atElement, path + ".at");
        if (point.Count != 2)
        {
            throw Refuse(path + ".at", $"{point.Count} numbers; a ready-made group stands at [x, y]");
        }

        var x = Number(point[0], path + ".at[0]");
        var y = Number(point[1], path + ".at[1]");
        var prefix = props.TryGetValue("id", out var id) ? Id(id, path + ".id") : recipe.Id;
        var group = props.TryGetValue("group", out var named) ? GroupName(named, path + ".group") : recipe.Name.Text();

        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        if (props.TryGetValue("states", out var statesElement))
        {
            if (statesElement.ValueKind != JsonValueKind.Object)
            {
                throw Refuse(path + ".states", "an object: a kind and its preview state");
            }

            foreach (var state in statesElement.EnumerateObject())
            {
                if (!recipe.Widgets.Any(w => string.Equals(w.Kind, state.Name, StringComparison.Ordinal)))
                {
                    throw Refuse($"{path}.states.{state.Name}", $"\"{recipe.Id}\" has no \"{state.Name}\" (has: {string.Join(", ", recipe.Widgets.Select(w => w.Kind).Distinct())})");
                }

                states[state.Name] = Word(state.Value, $"{path}.states.{state.Name}");
            }
        }

        var widgets = new List<SceneWidget>(recipe.Widgets.Count);
        for (var i = 0; i < recipe.Widgets.Count; i++)
        {
            var part = recipe.Widgets[i];
            var rect = new SceneRect(x + part.Box.X, y + part.Box.Y, part.Box.Width, part.Box.Height);
            if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > screen.Width || rect.Y + rect.Height > screen.Height)
            {
                throw Refuse(path + ".at", $"\"{recipe.Id}\" at [{Show(x)}, {Show(y)}] does not stand inside the {Show(screen.Width)} × {Show(screen.Height)} display (its {part.Kind} would be [{Show(rect.X)}, {Show(rect.Y)}, {Show(rect.Width)}, {Show(rect.Height)}])");
            }

            var second = recipe.Widgets.Take(i).Any(o => string.Equals(o.Kind, part.Kind, StringComparison.Ordinal));
            widgets.Add(new SceneWidget(
                $"{path}.{part.Kind}",
                second ? $"{prefix}-{part.Kind}-{i}" : $"{prefix}-{part.Kind}",
                new WidgetId(part.Kind),
                rect,
                State: states.TryGetValue(part.Kind, out var chosen) ? chosen : null,
                Header: part.Header,
                Label: part.Label,
                Config: part.Config,
                Group: group));
        }

        return widgets;
    }

    private static SceneWidget Widget(JsonElement element, string path, SceneScreen screen)
    {
        var props = Properties(element, path,
            "id", "kind", "at", "state", "style", "header", "background", "items", "itemText", "scale", "align", "label", "bare", "config", "selected", "group");

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
            props.TryGetValue("id", out var id) ? Id(id, path + ".id") : kind.Value,
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
            Selected: props.TryGetValue("selected", out var selected) && Bool(selected, path + ".selected"),
            Group: props.TryGetValue("group", out var group) ? GroupName(group, path + ".group") : null);
    }

    /// <summary>[S-2458] A group's name in a scene: any text that is not blank.</summary>
    private static string GroupName(JsonElement element, string path)
    {
        var text = Text(element, path);
        return text.Trim().Length > 0 ? text : throw Refuse(path, "a group's name is not blank");
    }

    /// <summary>[S-1079] A widget's name in its scene, for what points at it (the store's pages): letters, digits and '-'.</summary>
    private static string Id(JsonElement element, string path)
    {
        var text = Text(element, path);
        return text.Length > 0 && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            ? text
            : throw Refuse(path, $"\"{text}\" is not an id of letters, digits and '-'");
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
