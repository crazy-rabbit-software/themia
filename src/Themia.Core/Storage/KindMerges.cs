using System.Text.Json.Nodes;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Storage;

/// <summary>
/// [S-14713] [S-15022] The one kind-replacement pass of a state load: a placed widget of a kind that is gone, replaced in place
/// by the kind or kinds whose descriptors say they stand in for it (<see cref="WidgetDescriptor.Merges"/>; owner, 2026-11-20:
/// "Replace in place"). It runs on the raw document, before the unknown kinds are dropped and the typed read, which no longer
/// knows the old kind (<see cref="StateDocument"/>); each replaced widget is one logged line, the rest of the document is not
/// touched. ([S-14606]'s facts-card splits went with the last build that converted them.)
/// </summary>
public static class KindMerges
{
    /// <summary>
    /// [S-14713] Replace every placed widget of a merged kind in <paramref name="document"/>; one line each. [S-15026] Each
    /// card shared out by several kinds is added to <paramref name="sharedOut"/> (when given), for its display to lay once
    /// its metrics are known (<see cref="ShareOuts"/>).
    /// </summary>
    public static List<string> Replace(JsonNode? document, List<ShareOut>? sharedOut = null) => Replace(document, ModuleCatalog.Widgets, sharedOut);

    /// <summary>The span keys of the removed graph kinds (a weather graph's window, a history's span), the metric's <c>span</c> now.</summary>
    private static readonly string[] Spans = ["hours", "graphSpanSeconds"];

    /// <summary>
    /// [S-14713] [S-15022] Replace every placed widget of a removed kind in <paramref name="document"/> by the kinds that stand
    /// in for it: those whose <see cref="MergeFrom.When"/> its config matches (an air graph's measure), else every one that
    /// takes the rest and is not left out by <see cref="MergeFrom.Unless"/> (the weather summary's sky and condition, which its
    /// Minimal look never drew). One kind takes the widget's spot, id, look and settings (the place, the drive, the adapter,
    /// the host, its chart look); several each take a copy of it, its id the old one and the kind's (<c>w07-weatherSky</c>), in
    /// their share of its rect (<see cref="MergeFrom.Share"/>), top to bottom and left to right, in its place in the drawing
    /// order. A graph's (<see cref="MergeFrom.Graph"/>) has its trend Line so its graph shows and its window or span the
    /// metric's <c>span</c>; the keys the old kind was shared out by go; a key a new kind does not have is then the load's to
    /// drop, loudly (<see cref="StateDocument"/>). One logged line a replaced widget.
    /// </summary>
    internal static List<string> Replace(JsonNode? document, IReadOnlyList<WidgetDescriptor> widgets, List<ShareOut>? sharedOut = null)
    {
        // [S-16230] The parts of a widget that is back fold into it first.
        var notes = KindFolds.Fold(document, widgets);
        var merges = widgets.SelectMany(w => w.Merges.Where(m => m.Fold is null).Select(m => (Into: w.Id.Value, From: m)))
            .Where(m => !widgets.Any(w => string.Equals(w.Id.Value, m.From.Kind, StringComparison.Ordinal)))
            .GroupBy(m => m.From.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        if (merges.Count == 0 || document is not JsonObject root || root["views"] is not JsonArray views)
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
            var viewId = entry["id"] is JsonValue viewIdValue && viewIdValue.TryGetValue<string>(out var viewIdText) ? viewIdText : null;
            for (var at = 0; at < placed.Count; at++)
            {
                if (placed[at] is not JsonObject widget
                    || widget["config"] is not JsonObject config
                    || config["kind"] is not JsonValue kindValue
                    || !kindValue.TryGetValue<string>(out var kind)
                    || !merges.TryGetValue(kind, out var candidates)
                    || Into(config, candidates) is not { Count: > 0 } targets)
                {
                    continue;
                }

                var id = widget["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";
                var keys = candidates.SelectMany(c => new[] { c.From.When?.Key, c.From.Unless?.Key }).OfType<string>().Distinct().ToList();
                if (targets.Count == 1)
                {
                    Become(widget, targets[0], keys);
                    notes.Add($"replaced {kind} '{id}' in view '{viewName}' in place by {Name(targets[0])}");
                    continue;
                }

                // [S-15026] The card's rect and each part's share: its display lays them once its metrics are known.
                if (sharedOut is not null && viewId is not null && Rect(widget) is var (cx, cy, cw, ch)
                    && targets.All(t => t.From.Share is not null))
                {
                    sharedOut.Add(new ShareOut(viewId, kind, new FracRect(cx, cy, cw, ch),
                        [.. targets.Select(t => new SharedPart($"{id}-{t.Into}", t.From.Share!.Value))]));
                }

                placed.RemoveAt(at);
                var made = new List<string>(targets.Count);
                for (var i = 0; i < targets.Count; i++)
                {
                    var copy = (JsonObject)widget.DeepClone();
                    var newId = $"{id}-{targets[i].Into}";
                    copy["id"] = newId;
                    Become(copy, targets[i], keys);
                    placed.Insert(at + i, copy);
                    made.Add($"{targets[i].Into} '{newId}'");
                }

                at += targets.Count - 1;
                notes.Add($"replaced {kind} '{id}' in view '{viewName}' in place by {targets.Count} widgets in its spot: {string.Join(", ", made)}");
            }
        }

        return notes;
    }

    /// <summary>The words of a replacement by one kind: a graph's metric with its graph shown, or the kind.</summary>
    private static string Name((string Into, MergeFrom From) target)
        => target.From.Graph ? $"the single metric {target.Into} with its graph shown" : $"the widget {target.Into}";

    /// <summary>[S-14713] [S-15022] Make <paramref name="widget"/> the kind <paramref name="target"/> names, in its share of its rect.</summary>
    private static void Become(JsonObject widget, (string Into, MergeFrom From) target, List<string> keys)
    {
        var config = (JsonObject)widget["config"]!;
        config["kind"] = target.Into;
        foreach (var key in keys)
        {
            config.Remove(key);
        }

        if (target.From.Graph)
        {
            config["trend"] = nameof(MetricTrend.Line);
            foreach (var key in Spans)
            {
                if (config[key] is { } span)
                {
                    config.Remove(key);
                    config["span"] = span.DeepClone();
                }
            }
        }

        if (target.From.Share is { } share && Rect(widget) is var (x, y, w, h))
        {
            widget["x"] = x + (share.X * w);
            widget["y"] = y + (share.Y * h);
            widget["w"] = share.Width * w;
            widget["h"] = share.Height * h;
        }
    }

    /// <summary>The widget's rect as fractions of its monitor; null without a whole one.</summary>
    private static (double X, double Y, double W, double H)? Rect(JsonObject widget)
    {
        static double? Of(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;
        return (Of(widget["x"]), Of(widget["y"]), Of(widget["w"]), Of(widget["h"])) is ({ } x, { } y, { } w, { } h) && w > 0 && h > 0
            ? (x, y, w, h)
            : null;
    }

    /// <summary>
    /// [S-14713] [S-15022] The kinds a placed widget of a removed kind becomes: those its config's value names, else every one
    /// that takes the rest and is not left out by its value, in their shares' order (top to bottom, left to right).
    /// </summary>
    private static List<(string Into, MergeFrom From)> Into(JsonObject config, List<(string Into, MergeFrom From)> into)
    {
        var named = into.Where(c => c.From.When is { } when && Holds(config, when)).ToList();
        var targets = named.Count > 0 ? named : [.. into.Where(c => c.From.When is null && !(c.From.Unless is { } unless && Holds(config, unless)))];
        return [.. targets.OrderBy(t => t.From.Share?.Y ?? 0).ThenBy(t => t.From.Share?.X ?? 0)];
    }

    /// <summary>Whether the config holds <paramref name="pair"/>'s value at its key, ignoring case.</summary>
    private static bool Holds(JsonObject config, (string Key, string Value) pair)
        => config[pair.Key] is JsonValue value && value.TryGetValue<string>(out var held) && string.Equals(held, pair.Value, StringComparison.OrdinalIgnoreCase);
}

/// <summary>[S-15026] A part of a card shared out: its widget's id and its share of the card.</summary>
public readonly record struct SharedPart(string Id, MergeShare Share);

/// <summary>
/// [S-15026] A placed card the load shared out among several kinds (<see cref="KindMerges"/>): the view it stands on, its
/// removed kind, its rect and its parts in their order (top to bottom, left to right), each in its share of the rect.
/// </summary>
public sealed record ShareOut(string ViewId, string Kind, FracRect Card, IReadOnlyList<SharedPart> Parts);
