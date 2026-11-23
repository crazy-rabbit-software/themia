using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Storage;


/// <summary>
/// A document read through <see cref="StateDocument.Parse"/>: a validated state with the repairs it
/// needed, or no state and the reason it was refused. Never a half-built state.
/// </summary>
public readonly record struct ParseResult(
    AppState? State,
    ParseRefusal Refusal,
    string? Problem,
    Exception? Error,
    IReadOnlyList<string> Repairs)
{
    public static ParseResult Refused(ParseRefusal refusal, string problem, Exception? error = null)
        => new(null, refusal, problem, error, []);
}

/// <summary>Why a document was refused. A closed set, so a caller can say it in its own words.</summary>
public enum ParseRefusal
{
    /// <summary>Not refused.</summary>
    None = 0,

    /// <summary>Empty or whitespace — corrupt, not absent.</summary>
    Empty = 1,

    /// <summary>Not JSON at all.</summary>
    NotJson = 2,

    /// <summary>JSON, but not the shape of a state document.</summary>
    NotState = 3,

    /// <summary>Written by a newer build than this one.</summary>
    NewerSchema = 4,

    /// <summary>The file itself could not be read.</summary>
    Unreadable = 5,
}

/// <summary>
/// [S-1050] A state document read: the one trust boundary for one, whether it is the app's own file at startup
/// (<see cref="AppStateStore.Load"/>) or a file the user picked to import (<see cref="SettingsTransfer"/>). The raw
/// JSON is cleaned first (what this build cannot name taken out, loudly), then read typed, then repaired
/// (<see cref="StateRepair"/>).
/// </summary>
public static class StateDocument
{
    /// <summary>
    /// Turn a document into a validated state, or say why it cannot be one. The one trust boundary
    /// for a state document, whether it is the app's own file at startup or a file the user picked
    /// to import: both are read the same way, so an import can never accept something a restart
    /// would then refuse.
    /// <para>
    /// Empty, unparseable, null and newer-schema documents are refused with a reason. Anything else
    /// is repaired back inside its invariants (<see cref="StateRepair.Repair"/>) and the repairs are listed.
    /// </para>
    /// </summary>
    public static ParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            // A zero-byte file is the classic result of a crash during a non-atomic write. It is
            // corrupt, not absent.
            return ParseResult.Refused(ParseRefusal.Empty, "state file is empty");
        }

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotJson, "state file is not valid JSON", ex);
        }

        // [S-14713] A placed widget of a kind merged into another becomes it first, in its place.
        var dropped = KindMerges.Replace(document);
        dropped.AddRange(DropUnknownKinds(document));
        dropped.AddRange(DropUnknownKeys(document));
        dropped.AddRange(ReplaceUnknownNames(document));

        AppState? parsed;
        try
        {
            parsed = document is null ? null : document.Deserialize(StateJson.AppState);
        }
        catch (JsonException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotJson, "state file is not valid JSON", ex);
        }
        catch (NotSupportedException ex)
        {
            return ParseResult.Refused(ParseRefusal.NotState, "state file holds an unsupported shape", ex);
        }

        if (parsed is null)
        {
            return ParseResult.Refused(ParseRefusal.NotState, "state file deserialised to null");
        }

        if (parsed.SchemaVersion > AppState.CurrentSchemaVersion)
        {
            return ParseResult.Refused(
                ParseRefusal.NewerSchema,
                $"state schema v{parsed.SchemaVersion} is newer than this build (v{AppState.CurrentSchemaVersion})");
        }

        // Only the current schema is read: there is no upgrade path, so an older number is a file
        // this build cannot vouch for, refused like any other that is not a state document.
        if (parsed.SchemaVersion < AppState.CurrentSchemaVersion)
        {
            return ParseResult.Refused(
                ParseRefusal.NotState,
                $"state schema v{parsed.SchemaVersion} is not supported (this build reads v{AppState.CurrentSchemaVersion})");
        }

        dropped.AddRange(StateRepair.Repair(parsed));
        return new ParseResult(parsed, ParseRefusal.None, null, null, dropped);
    }

    /// <summary>
    /// [S-1050] The schema changes in place: a key the design, the preferences, a view, a widget or a widget's config does
    /// not have in this build (a hand edit, or a key the app no longer writes, such as the groups that are gone, [S-2450],
    /// Over the edge, [S-2330], the assistants' server switch, [S-9960], or a readout's size, [S-2449]) is taken out
    /// before the typed read, loudly, one line each, never kept beside the state or read into anything. Runs after
    /// <see cref="DropUnknownKinds"/>: every config left names a kind of this build.
    /// </summary>
    private static List<string> DropUnknownKeys(JsonNode? document)
    {
        var dropped = new List<string>();
        if (document is not JsonObject root)
        {
            return dropped;
        }

        if (root["design"] is JsonObject design)
        {
            var designKeys = Keys(StateJson.Options.GetTypeInfo(typeof(DesignState)));
            foreach (var key in design.Select(p => p.Key).Where(k => !designKeys.Contains(k)).ToList())
            {
                design.Remove(key);
                dropped.Add($"design: dropped the unknown key '{key}'");
            }
        }

        if (root["preferences"] is JsonObject preferences)
        {
            var preferenceKeys = Keys(StateJson.Options.GetTypeInfo(typeof(AppPreferences)));
            foreach (var key in preferences.Select(p => p.Key).Where(k => !preferenceKeys.Contains(k)).ToList())
            {
                preferences.Remove(key);
                dropped.Add($"preferences: dropped the unknown key '{key}'");
            }
        }

        if (root["views"] is not JsonArray views)
        {
            return dropped;
        }

        var viewKeys = Keys(StateJson.Options.GetTypeInfo(typeof(ViewState)));
        var widgetKeys = Keys(StateJson.WidgetInstance);
        foreach (var view in views.OfType<JsonObject>())
        {
            var viewName = view["name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var viewText) ? viewText : "?";
            foreach (var key in view.Select(p => p.Key).Where(k => !viewKeys.Contains(k)).ToList())
            {
                view.Remove(key);
                dropped.Add($"view '{viewName}': dropped the unknown key '{key}'");
            }

            if (view["widgets"] is not JsonArray widgets)
            {
                continue;
            }

            foreach (var widget in widgets.OfType<JsonObject>())
            {
                var id = widget["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";
                foreach (var key in widget.Select(p => p.Key).Where(k => !widgetKeys.Contains(k)).ToList())
                {
                    widget.Remove(key);
                    dropped.Add($"widget '{id}' in view '{viewName}': dropped the unknown key '{key}'");
                }

                if (widget["config"] is JsonObject config && ConfigKeys(config) is { } configKeys)
                {
                    foreach (var key in config.Select(p => p.Key).Where(k => !configKeys.Contains(k)).ToList())
                    {
                        config.Remove(key);
                        dropped.Add($"widget '{id}' in view '{viewName}': dropped the unknown config key '{key}'");
                    }
                }
            }
        }

        return dropped;

        // The keys a config of its kind has: its discriminator and its type's properties; null where the type is not
        // read property by property (a converter of its own, extension data), which then keeps what it is given.
        static HashSet<string>? ConfigKeys(JsonObject config)
        {
            if (!(config["kind"] is JsonValue discriminator
                && discriminator.TryGetValue<string>(out var kind)
                && WidgetId.TryParse(kind, out var id)
                && ModuleCatalog.TryGet(id, out var descriptor)))
            {
                return null;
            }

            var info = StateJson.Options.GetTypeInfo(descriptor.ConfigType);
            if (info.Kind != JsonTypeInfoKind.Object || info.Properties.Any(p => p.IsExtensionData))
            {
                return null;
            }

            var keys = Keys(info);
            keys.Add("kind");
            return keys;
        }

        static HashSet<string> Keys(JsonTypeInfo info)
            => new(info.Properties.Select(p => p.Name), StringComparer.Ordinal);
    }

    /// <summary>
    /// [S-1055] Put back, before the typed read, every value of a closed set (an enum) that names none of its members
    /// (a hand edit, a member renamed or removed), and every other plain value its type cannot read (a setting whose type
    /// changed in place, such as a switch become a number), with what a fresh object of its type holds there, loudly, one
    /// line each. The typed read would otherwise refuse the whole document over one word, and the user would be handed a
    /// default desktop. Walked by the serializer's own metadata, so every enum in the state (the design, the
    /// preferences, a view bar, a widget, a widget's config) is covered without a list; a property read by a converter
    /// of its own (a widget's style, a lenient list) is left to it. A number that is no member reads and is the repair's
    /// (<see cref="StateRepair"/>). Runs after <see cref="DropUnknownKinds"/>: every config left names a kind of this build.
    /// </summary>
    private static List<string> ReplaceUnknownNames(JsonNode? document)
    {
        var replaced = new List<string>();
        if (document is JsonObject root)
        {
            Names(root, StateJson.AppState, string.Empty, replaced);
        }

        return replaced;
    }

    private static void Names(JsonNode node, JsonTypeInfo info, string path, List<string> replaced)
    {
        switch (info.Kind)
        {
            case JsonTypeInfoKind.Object when node is JsonObject fields:
                if (Derived(info, fields) is { } derived)
                {
                    Names(fields, derived, path, replaced);
                    return;
                }

                object? fresh = null;
                foreach (var property in info.Properties)
                {
                    if (property.CustomConverter is not null
                        || !fields.TryGetPropertyValue(property.Name, out var value)
                        || value is null
                        || Of(property.PropertyType) is not { } type)
                    {
                        continue;
                    }

                    var at = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    if (!Leaf(type))
                    {
                        Names(value, type, at, replaced);
                        continue;
                    }

                    if (Reads(value, type))
                    {
                        continue;
                    }

                    fresh ??= info.CreateObject?.Invoke();
                    var fallback = fresh is null ? null : property.Get?.Invoke(fresh);
                    if (fallback is null)
                    {
                        fields.Remove(property.Name);
                        replaced.Add($"{at}: {value.ToJsonString()} {Misread(type)}; left out");
                    }
                    else
                    {
                        var written = JsonSerializer.SerializeToNode(fallback, type);
                        fields[property.Name] = written;
                        replaced.Add($"{at}: {value.ToJsonString()} {Misread(type)}; using {written?.ToJsonString()}");
                    }
                }

                return;

            case JsonTypeInfoKind.Enumerable when node is JsonArray items && Of(info.ElementType) is { } element:
                for (var i = items.Count - 1; i >= 0; i--)
                {
                    if (items[i] is not { } item)
                    {
                        continue;
                    }

                    var at = $"{path}[{Name(item, i)}]";
                    if (!Leaf(element))
                    {
                        Names(item, element, at, replaced);
                    }
                    else if (!Reads(item, element))
                    {
                        items.RemoveAt(i);
                        replaced.Add($"{at}: {item.ToJsonString()} {Misread(element)}; dropped");
                    }
                }

                return;

            case JsonTypeInfoKind.Dictionary when node is JsonObject entries && Of(info.ElementType) is { } element:
                foreach (var (key, item) in entries.ToList())
                {
                    if (item is null)
                    {
                        continue;
                    }

                    var at = $"{path}[{key}]";
                    if (!Leaf(element))
                    {
                        Names(item, element, at, replaced);
                    }
                    else if (!Reads(item, element))
                    {
                        entries.Remove(key);
                        replaced.Add($"{at}: {item.ToJsonString()} {Misread(element)}; dropped");
                    }
                }

                return;
        }

        // A value read whole by its type's converter (a number, a switch, a text, an enum): it reads or it does not. Anything
        // else (an object, a list, a map) is walked into.
        static bool Leaf(JsonTypeInfo type) => type.Kind == JsonTypeInfoKind.None;

        // Why a leaf did not read, in the repair's line: an enum's word is no member; any other value is not of its type.
        static string Misread(JsonTypeInfo type) => type.Type.IsEnum ? "names no value" : $"is no {type.Type.Name}";

        // The metadata of a type as the state's options hold it; a nullable enum is its enum. Null where the options have
        // none, which the typed read would refuse on its own terms.
        static JsonTypeInfo? Of(Type? type)
            => type is not null && StateJson.Options.TryGetTypeInfo(Nullable.GetUnderlyingType(type) ?? type, out var found) ? found : null;

        // The derived type a union's discriminator names, or null where the type is no union or names none.
        static JsonTypeInfo? Derived(JsonTypeInfo info, JsonObject fields)
        {
            if (info.PolymorphismOptions is not { } union
                || fields[union.TypeDiscriminatorPropertyName] is not JsonValue discriminator
                || !discriminator.TryGetValue<string>(out var name))
            {
                return null;
            }

            var derived = union.DerivedTypes.FirstOrDefault(d => d.TypeDiscriminator is string id && string.Equals(id, name, StringComparison.Ordinal));
            return derived.DerivedType is null || derived.DerivedType == info.Type ? null : Of(derived.DerivedType);
        }

        // An element's name in a line: its id where it has one (a view, a widget), else its place.
        static string Name(JsonNode item, int index)
            => item is JsonObject o && o["id"] is JsonValue id && id.TryGetValue<string>(out var text) ? text : index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Whether the value reads as its type: the converter the typed read will use decides.
        static bool Reads(JsonNode value, JsonTypeInfo type)
        {
            try
            {
                JsonSerializer.Deserialize(value, type);
                return true;
            }
            catch (JsonException)
            {
                // ignore: a value its type cannot read is the answer asked for, not an error
                return false;
            }
            catch (NotSupportedException)
            {
                // ignore: a type the options read only through its owner (an object slot) is not judged here; the typed
                // read decides it
                return true;
            }
        }
    }

    /// <summary>
    /// [S-1900] [S-6908] Take out, before the typed read, every widget whose kind this build does not have —
    /// a widget that was removed from the app, or a hand edit — and say which. The typed read would
    /// otherwise refuse the whole document over one widget it cannot name, and the user would lose
    /// every other widget with it. What such a widget held goes with it; nothing else is touched. Each kind gone is one
    /// line with its count and the widgets it took (a kind removed from the app, such as the weather facts split into a
    /// widget a fact, [S-13900], goes from every view at once).
    /// </summary>
    private static List<string> DropUnknownKinds(JsonNode? document)
    {
        var dropped = new List<string>();
        if (document is not JsonObject root || root["views"] is not JsonArray views)
        {
            return dropped;
        }

        var gone = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var view in views)
        {
            if (view is not JsonObject { } entry || entry["widgets"] is not JsonArray widgets)
            {
                continue;
            }

            var viewName = entry["name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var viewText) ? viewText : "?";
            var unknown = new List<JsonNode>();
            foreach (var widget in widgets)
            {
                if (widget is not JsonObject fields)
                {
                    continue;
                }

                var id = fields["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";

                // [S-6908] The config's discriminator is the widget's id: one no module in this build
                // declares (or one that is not an id at all) cannot be read, and would refuse the
                // whole document if it reached the typed read. So would a widget without a config.
                // The widget's own "kind", which files from before ids still carry, is not read.
                if (fields["config"] is not JsonObject config)
                {
                    unknown.Add(widget);
                    dropped.Add($"dropped widget '{id}' without a config from view '{viewName}'");
                }
                else if (!(config["kind"] is JsonValue discriminator
                    && discriminator.TryGetValue<string>(out var widgetId)
                    && WidgetId.TryParse(widgetId, out var parsed)
                    && ModuleCatalog.TryGet(parsed, out _)))
                {
                    unknown.Add(widget);
                    var named = config["kind"] is JsonValue raw && raw.TryGetValue<string>(out var given) ? given : "(none)";
                    if (!gone.TryGetValue(named, out var which))
                    {
                        gone[named] = which = [];
                    }

                    which.Add($"'{id}' from view '{viewName}'");
                }
            }

            foreach (var widget in unknown)
            {
                widgets.Remove(widget);
            }
        }

        foreach (var (kind, which) in gone)
        {
            dropped.Add($"dropped {which.Count} {(which.Count == 1 ? "widget" : "widgets")} of unknown kind '{kind}': {string.Join(", ", which)}");
        }

        return dropped;
    }

}
