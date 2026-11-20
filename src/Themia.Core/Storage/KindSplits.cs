using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Storage;

/// <summary>
/// [S-14606] A placed widget of a kind that was split into single metrics, replaced in place on load (owner, 2026-11-20,
/// asked what becomes of a placed facts widget: "Each placed facts widget becomes its shown rows as single-metric widgets
/// stacked in the same spot, with the same settings (drive, adapter, place)"): one widget a row it showed, in its order
/// (none stored: its kind's defaults), stacked top to bottom from its top edge, each one row of the old card tall (the old
/// card's height was the content top, a 28 row a fact and the inset, so its rows' share of it is a single metric's one
/// row of whole cells, <see cref="MetricRules.RowHeight"/>), each a copy of the old widget with its look and its config's
/// settings (the place, the drive, the adapter, the host, the window), its pick left out. Which kind stands in for which
/// row the kinds say themselves (<see cref="WidgetDescriptor.Split"/>); the rest of the document is not touched. Each
/// replaced widget is one logged line. It runs on the raw document, before the typed read, which no longer knows the old kind.
/// </summary>
public static class KindSplits
{
    /// <summary>The stored picks a removed kind's rows were in: the facts atoms' and the sky's.</summary>
    private static readonly string[] Picks = ["facts", "times"];

    /// <summary>[S-14606] Replace every placed widget of a split kind in <paramref name="document"/>; one line each.</summary>
    public static List<string> Replace(JsonNode? document) => Replace(document, ModuleCatalog.Widgets);

    /// <summary>[S-14606] <see cref="Replace(JsonNode?)"/> over <paramref name="widgets"/>: the catalogue's, or a part of it in a test.</summary>
    internal static List<string> Replace(JsonNode? document, IReadOnlyList<WidgetDescriptor> widgets)
    {
        var notes = new List<string>();
        var splits = widgets.Where(w => w.Split is not null)
            .GroupBy(w => w.Split!.Kind, StringComparer.Ordinal)
            .Where(g => !widgets.Any(w => string.Equals(w.Id.Value, g.Key, StringComparison.Ordinal)))
            .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Split!.Order).ToList(), StringComparer.Ordinal);
        if (splits.Count == 0 || document is not JsonObject root || root["views"] is not JsonArray views)
        {
            return notes;
        }

        foreach (var view in views)
        {
            if (view is not JsonObject { } entry || entry["widgets"] is not JsonArray placed)
            {
                continue;
            }

            var viewName = entry["name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var viewText) ? viewText : "?";
            for (var at = 0; at < placed.Count; at++)
            {
                if (placed[at] is not JsonObject widget
                    || widget["config"] is not JsonObject config
                    || config["kind"] is not JsonValue kindValue
                    || !kindValue.TryGetValue<string>(out var kind)
                    || !splits.TryGetValue(kind, out var rows))
                {
                    continue;
                }

                var id = widget["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";
                var shown = Shown(config, rows);
                var parts = Laid(widget, shown.Count);
                placed.RemoveAt(at);
                var made = new List<string>(shown.Count);
                for (var i = 0; i < shown.Count; i++)
                {
                    var copy = (JsonObject)widget.DeepClone();
                    var newId = $"{id}-{shown[i].Id.Value}";
                    copy["id"] = newId;
                    if (parts is { } rect)
                    {
                        copy["y"] = rect.Top + (i * rect.Row);
                        copy["h"] = rect.Row;
                    }

                    var settings = (JsonObject)copy["config"]!;
                    foreach (var pick in Picks)
                    {
                        settings.Remove(pick);
                    }

                    settings["kind"] = shown[i].Id.Value;
                    placed.Insert(at + i, copy);
                    made.Add($"{shown[i].Id.Value} '{newId}'");
                }

                at += shown.Count - 1;
                notes.Add($"replaced {kind} '{id}' in view '{viewName}' in place by {shown.Count} single {(shown.Count == 1 ? "metric" : "metrics")} stacked in its spot: {string.Join(", ", made)}");
            }
        }

        return notes;
    }

    /// <summary>The kinds of the rows a removed card showed, in its order: its stored pick (unknown and repeated rows left out), else its defaults.</summary>
    private static List<WidgetDescriptor> Shown(JsonObject config, IReadOnlyList<WidgetDescriptor> rows)
    {
        var shown = new List<WidgetDescriptor>();
        foreach (var pick in Picks)
        {
            if (config[pick] is not JsonArray stored)
            {
                continue;
            }

            foreach (var item in stored)
            {
                if (item is JsonValue value && value.TryGetValue<string>(out var part)
                    && rows.FirstOrDefault(r => string.Equals(r.Split!.Part, part, StringComparison.OrdinalIgnoreCase)) is { } row
                    && !shown.Contains(row))
                {
                    shown.Add(row);
                }
            }
        }

        return shown.Count > 0 ? shown : [.. rows.Where(r => r.Split!.Default)];
    }

    /// <summary>
    /// [S-14606] Where the rows stand in the old card's rect: its top and a row's height as a share of the monitor, the
    /// old card having been the content top, <paramref name="count"/> rows of 28 and the inset tall; null without a rect.
    /// </summary>
    private static (double Top, double Row)? Laid(JsonObject widget, int count)
    {
        if (widget["y"] is not JsonValue y || !y.TryGetValue<double>(out var top)
            || widget["h"] is not JsonValue h || !h.TryGetValue<double>(out var height)
            || !double.IsFinite(top) || !double.IsFinite(height) || height <= 0 || count <= 0)
        {
            return null;
        }

        var old = FamilySpace.ContentTop + (count * FamilyShape.Row1) + FamilySpace.Inset;
        return (top, height * MetricRules.RowHeight / old);
    }
}
