using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-14604] One kind of a single-metric family, as data: its id, the metric's key in its family (a system sheet's fact
/// key, a reading's name), its glyph, what history its metric has (its trends), the row's name (translated), its name
/// and description on the add page, its search words, its config (a sealed type a kind: a config's type is how it knows
/// its id, [S-6902]) and the row of a removed kind of many rows it stands in for ([S-14606]).
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

    /// <summary>[S-14606] The removed kind's row it stands in for; null for none.</summary>
    public SplitFrom? Split { get; init; }

    /// <summary>
    /// [S-14606] <paramref name="kinds"/> as the rows of the removed kind <paramref name="removed"/>, in its order: those
    /// whose keys are in <paramref name="defaults"/> were a new one's.
    /// </summary>
    public static IReadOnlyList<MetricKind> SplitOf(string removed, IReadOnlyList<MetricKind> kinds, IReadOnlyCollection<string> defaults)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(defaults);
        return [.. kinds.Select((kind, i) => kind with { Split = new SplitFrom(removed, kind.Key, i, defaults.Contains(kind.Key)) })];
    }
}
