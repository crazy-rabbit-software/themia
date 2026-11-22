using System.Text.Json.Nodes;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Storage;

/// <summary>
/// [S-14713] The one kind-replacement pass of a state load: a placed widget of a kind that is gone, replaced in place by the
/// kind its descriptor says stands in for it (<see cref="WidgetDescriptor.Merges"/>; owner, 2026-11-20: "Replace in place").
/// It runs on the raw document, before the unknown kinds are dropped and the typed read, which no longer knows the old
/// kind (<see cref="StateDocument"/>); each replaced widget is one logged line, the rest of the document is not touched.
/// ([S-14606]'s facts-card splits went with the last build that converted them.)
/// </summary>
public static class KindMerges
{
    /// <summary>[S-14713] Replace every placed widget of a merged kind in <paramref name="document"/>; one line each.</summary>
    public static List<string> Replace(JsonNode? document) => Replace(document, ModuleCatalog.Widgets);

    /// <summary>The span keys of the removed graph kinds (a weather graph's window, a history's span), the metric's <c>span</c> now.</summary>
    private static readonly string[] Spans = ["hours", "graphSpanSeconds"];

    /// <summary>
    /// [S-14713] Replace every placed widget of a removed graph kind in <paramref name="document"/> by the single metric that
    /// stands in for it (owner, 2026-11-21: "i think it would be good if we merge all the different graph types into the
    /// general metric widget"; placed ones "Replace in place", as the facts cards were): the metric whose
    /// <see cref="MergeFrom.When"/> its config matches (an air graph's measure), else the one that takes the rest, in the same
    /// spot with the same id, look and settings (the place, the drive, the adapter, the host, its chart look), its trend
    /// Line so its graph shows, its window or span the metric's <c>span</c>. One logged line a replaced widget; a key the
    /// metric does not have is then the load's to drop, loudly (<see cref="StateDocument"/>). Which kind stands in for which
    /// the kinds declare (<see cref="WidgetDescriptor.Merges"/>).
    /// </summary>
    internal static List<string> Replace(JsonNode? document, IReadOnlyList<WidgetDescriptor> widgets)
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
            foreach (var item in placed)
            {
                if (item is not JsonObject widget
                    || widget["config"] is not JsonObject config
                    || config["kind"] is not JsonValue kindValue
                    || !kindValue.TryGetValue<string>(out var kind)
                    || !merges.TryGetValue(kind, out var into)
                    || Into(config, into) is not { } target)
                {
                    continue;
                }

                var id = widget["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text : "?";
                config["kind"] = target.Into;
                config["trend"] = nameof(MetricTrend.Line);
                if (target.From.When is { } when)
                {
                    config.Remove(when.Key);
                }

                foreach (var key in Spans)
                {
                    if (config[key] is { } span)
                    {
                        config.Remove(key);
                        config["span"] = span.DeepClone();
                    }
                }

                notes.Add($"replaced {kind} '{id}' in view '{viewName}' in place by the single metric {target.Into} with its graph shown");
            }
        }

        return notes;
    }

    /// <summary>[S-14713] The metric a placed graph becomes: the one its config's value names, else the one that takes the rest.</summary>
    private static (string Into, MergeFrom From)? Into(JsonObject config, List<(string Into, MergeFrom From)> into)
    {
        foreach (var candidate in into)
        {
            if (candidate.From.When is { } when && config[when.Key] is JsonValue value && value.TryGetValue<string>(out var held)
                && string.Equals(held, when.Value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return into.FirstOrDefault(c => c.From.When is null) is { Into: not null } rest ? rest : null;
    }
}
