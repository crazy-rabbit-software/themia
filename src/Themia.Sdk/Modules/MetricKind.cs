using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-14604] One kind of a single-metric family, as data: its id, the metric's key in its family (a system sheet's fact
/// key, a reading's name), its glyph, what history its metric has (its trends), the row's name (translated), its name
/// and description on the add page, its search words, its config (a sealed type a kind: a config's type is how it knows
/// its id, [S-6902]).
/// </summary>
public sealed record MetricKind(
    string Id,
    string Key,
    string Glyph,
    MetricHistory History,
    LocalText Name,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    Func<WidgetConfig> Create)
{
    /// <summary>The config's type.</summary>
    public Type ConfigType { get; } = Create().GetType();

    /// <summary>
    /// [S-14706] What a click on the card opens and its card menu's line: its family's source, the page its own widgets
    /// open (<see cref="MetricOpens"/>); null for a kind with no source to open (it lights nothing under the pointer).
    /// </summary>
    public MetricOpen? Opens { get; init; }

    /// <summary>[S-14706] <paramref name="kinds"/>, each opening <paramref name="open"/>: a family's one source.</summary>
    public static IReadOnlyList<MetricKind> Opening(IReadOnlyList<MetricKind> kinds, MetricOpen open)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(open);
        return [.. kinds.Select(kind => kind with { Opens = open })];
    }
}
