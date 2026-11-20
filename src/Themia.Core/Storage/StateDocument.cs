using System.Text.Json;
using System.Text.Json.Nodes;
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

        // [S-14606] A placed widget of a kind split into single metrics becomes them first, in its place.
        var dropped = KindSplits.Replace(document);
        dropped.AddRange(DropUnknownKinds(document));
        dropped.AddRange(DropUnknownKeys(document));

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
            if (info.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object || info.Properties.Any(p => p.IsExtensionData))
            {
                return null;
            }

            var keys = Keys(info);
            keys.Add("kind");
            return keys;
        }

        static HashSet<string> Keys(System.Text.Json.Serialization.Metadata.JsonTypeInfo info)
            => new(info.Properties.Select(p => p.Name), StringComparer.Ordinal);
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
