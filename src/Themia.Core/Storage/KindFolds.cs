using System.Text.Json.Nodes;
using Themia.Sdk;

namespace Themia.Core.Storage;

/// <summary>
/// [S-16230] The fold of a state load: placed widgets of kinds that are gone, which were parts of one widget that is back
/// (owner, 2026-11-22: "basically reset them to what they were a few days ago"; "before 19th"), folded into that widget
/// in place (<see cref="MergeFrom.Fold"/>). Touching parts (<see cref="MergeFold.Touching"/>: the now-playing texts and
/// buttons into the track, the times into the progress) become one widget of the kind at the union of their boxes, once
/// per group of touching parts and cards of the kind; a card of the kind in the group keeps its id, settings and look,
/// else the group's first part becomes it. A part over its widget (<see cref="MergeFold.Under"/>: a calendar's header)
/// is taken into each card of the kind standing right under it, which grows up over it; with none under it, it becomes
/// the kind in its own box. It runs on the raw document before <see cref="KindMerges"/>' replacement, a logged line a fold.
/// </summary>
public static class KindFolds
{
    /// <summary>How near two edges are to touch, as a fraction of the display (a rounding's worth).</summary>
    private const double Touch = 1e-4;

    /// <summary>[S-16230] Fold every placed part of a removed kind in <paramref name="document"/>; one line a fold.</summary>
    internal static List<string> Fold(JsonNode? document, IReadOnlyList<WidgetDescriptor> widgets)
    {
        var notes = new List<string>();
        // [S-16200] A kind that joins stays in the catalog; every other part's kind is gone.
        var folds = widgets.SelectMany(w => w.Merges.Where(m => m.Fold is not null).Select(m => (Into: w.Id.Value, From: m)))
            .Where(m => m.From.Fold == MergeFold.Joins || !widgets.Any(w => string.Equals(w.Id.Value, m.From.Kind, StringComparison.Ordinal)))
            .GroupBy(m => m.From.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        if (folds.Count == 0 || document is not JsonObject root || root["views"] is not JsonArray views)
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
            foreach (var into in folds.Values.Select(f => f.Into).Distinct(StringComparer.Ordinal).ToList())
            {
                Alone(placed, into, folds, viewName, notes);
                Under(placed, into, folds, viewName, notes);
                Touching(placed, into, folds, viewName, notes);
            }
        }

        return notes;
    }

    /// <summary>[S-16690] Each placed one of a kind merged into <paramref name="into"/> alone: it becomes the kind where it stands, its preset set.</summary>
    private static void Alone(JsonArray placed, string into, Dictionary<string, (string Into, MergeFrom From)> folds, string viewName, List<string> notes)
    {
        foreach (var widget in placed.OfType<JsonObject>())
        {
            if (!IsFold(widget, into, MergeFold.Alone, folds, out var kind) || widget["config"] is not JsonObject config)
            {
                continue;
            }

            Become(widget, into);
            var set = string.Empty;
            if (folds[kind].From.Sets is var (key, value))
            {
                config[key] = value;
                set = $" with {key} {value}";
            }

            notes.Add($"folded {kind} '{Id(widget)}' in view '{viewName}' into the widget {into}{set} in its own box");
        }
    }

    /// <summary>[S-16230] The parts over a card of <paramref name="into"/>: each card right under one grows up over it.</summary>
    private static void Under(JsonArray placed, string into, Dictionary<string, (string Into, MergeFrom From)> folds, string viewName, List<string> notes)
    {
        for (var at = 0; at < placed.Count; at++)
        {
            if (placed[at] is not JsonObject part || !IsFold(part, into, MergeFold.Under, folds, out var kind) || Rect(part) is not { } over)
            {
                continue;
            }

            var id = Id(part);
            var under = placed.OfType<JsonObject>()
                .Where(w => Kind(w) == into && Rect(w) is { } r && Math.Abs(r.Y - (over.Y + over.H)) <= Touch && Overlap(r.X, r.W, over.X, over.W))
                .ToList();
            if (under.Count == 0)
            {
                Become(part, into);
                notes.Add($"folded {kind} '{id}' in view '{viewName}' into the widget {into} in its own box");
                continue;
            }

            foreach (var card in under)
            {
                var r = Rect(card)!.Value;
                Place(card, (r.X, over.Y, r.W, r.H + (r.Y - over.Y)));
            }

            placed.RemoveAt(at);
            at--;
            notes.Add($"folded {kind} '{id}' in view '{viewName}' into the {into} under it: {string.Join(", ", under.Select(c => $"'{Id(c)}'"))}");
        }
    }

    /// <summary>[S-16230] Each group of touching parts and cards of <paramref name="into"/> that holds a part: one card at their union.</summary>
    private static void Touching(JsonArray placed, string into, Dictionary<string, (string Into, MergeFrom From)> folds, string viewName, List<string> notes)
    {
        var members = placed.OfType<JsonObject>()
            .Where(w => Rect(w) is not null && (Kind(w) == into || IsFold(w, into, MergeFold.Touching, folds, out _) || Joins(w)))
            .ToList();

        // [S-16200] A kind that stays joins the parts it touches; it never starts a group or links two.
        bool Joins(JsonObject w) => IsFold(w, into, MergeFold.Joins, folds, out _);
        var seen = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        foreach (var start in members)
        {
            if (Joins(start) || !seen.Add(start))
            {
                continue;
            }

            // The group: every member that touches one already in it.
            var group = new List<JsonObject> { start };
            for (var i = 0; i < group.Count; i++)
            {
                if (Joins(group[i]))
                {
                    continue;
                }

                var r = Rect(group[i])!.Value;
                foreach (var other in members.Where(m => !seen.Contains(m) && Touches(r, Rect(m)!.Value)))
                {
                    seen.Add(other);
                    group.Add(other);
                }
            }

            var parts = group.Where(m => Kind(m) != into && !Joins(m)).ToList();
            if (parts.Count == 0)
            {
                continue;
            }

            // The card that stays: the group's first card of the kind in the drawing order, else its first part.
            var ordered = group.OrderBy(m => placed.IndexOf(m)).ToList();
            var keep = ordered.FirstOrDefault(m => Kind(m) == into) ?? ordered.First(parts.Contains);
            var rects = group.Select(m => Rect(m)!.Value).ToList();
            var left = rects.Min(r => r.X);
            var top = rects.Min(r => r.Y);
            Place(keep, (left, top, rects.Max(r => r.X + r.W) - left, rects.Max(r => r.Y + r.H) - top));
            var folded = string.Join(", ", ordered.Where(m => m != keep).Select(m => $"{Kind(m)} '{Id(m)}'"));
            if (Kind(keep) != into)
            {
                folded = $"{Kind(keep)} '{Id(keep)}', {folded}".TrimEnd(',', ' ');
                Become(keep, into);
            }

            foreach (var gone in group.Where(m => m != keep))
            {
                placed.Remove(gone);
            }

            notes.Add($"folded {folded} in view '{viewName}' into the widget {into} '{Id(keep)}' over their boxes");
        }
    }

    /// <summary>Whether <paramref name="widget"/> is a part of a removed kind that folds into <paramref name="into"/> that way.</summary>
    private static bool IsFold(JsonObject widget, string into, MergeFold how, Dictionary<string, (string Into, MergeFrom From)> folds, out string kind)
    {
        kind = Kind(widget) ?? string.Empty;
        return folds.TryGetValue(kind, out var fold) && fold.Into == into && fold.From.Fold == how;
    }

    /// <summary>The part becomes <paramref name="into"/>, its settings kept: a key the kind does not have is the load's to drop, loudly.</summary>
    private static void Become(JsonObject widget, string into) => ((JsonObject)widget["config"]!)["kind"] = into;

    private static string? Kind(JsonObject widget)
        => widget["config"] is JsonObject config && config["kind"] is JsonValue value && value.TryGetValue<string>(out var kind) ? kind : null;

    private static string Id(JsonObject widget) => widget["id"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : "?";

    /// <summary>The widget's rect as fractions of its display; null without a whole one.</summary>
    private static (double X, double Y, double W, double H)? Rect(JsonObject widget)
    {
        static double? Of(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var d) && double.IsFinite(d) ? d : null;
        return (Of(widget["x"]), Of(widget["y"]), Of(widget["w"]), Of(widget["h"])) is ({ } x, { } y, { } w, { } h) && w > 0 && h > 0
            ? (x, y, w, h)
            : null;
    }

    private static void Place(JsonObject widget, (double X, double Y, double W, double H) rect)
    {
        widget["x"] = rect.X;
        widget["y"] = rect.Y;
        widget["w"] = rect.W;
        widget["h"] = rect.H;
    }

    /// <summary>Whether two spans along one axis share more than a point.</summary>
    private static bool Overlap(double a, double aLength, double b, double bLength)
        => Math.Min(a + aLength, b + bLength) - Math.Max(a, b) > Touch;

    /// <summary>Whether two rects overlap or share a stretch of an edge (a corner alone is no touch).</summary>
    private static bool Touches((double X, double Y, double W, double H) a, (double X, double Y, double W, double H) b)
    {
        var across = Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X);
        var down = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y);
        return (across > Touch && down >= -Touch) || (down > Touch && across >= -Touch);
    }
}
