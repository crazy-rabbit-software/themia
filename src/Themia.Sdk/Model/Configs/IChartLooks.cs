using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-6752] A graph widget's chart look, the same two settings on every graph kind (owner, 2026-11-19: "the widgets have
/// to much variance in themselves. they should reuse more of the same components"): its <see cref="Chart"/> type, one of
/// the <see cref="ChartTypes"/> its data has (the first the kind's own look until now, so a new widget looks as it did),
/// and its <see cref="Detail"/>, Simple by default. Shared settings ([S-11750]): several graphs selected set them once.
/// </summary>
public interface IChartLooks
{
    /// <summary>How the series is drawn; repaired to <see cref="DefaultChart"/> when it is not one of <see cref="ChartTypes"/>.</summary>
    ChartType Chart { get; set; }

    /// <summary>Simple, or Detailed: the grid and values, the times, the low, the high and the latest marked.</summary>
    ChartDetail Detail { get; set; }

    /// <summary>The types this kind offers, in the page's order.</summary>
    IReadOnlyList<ChartType> ChartTypes { get; }

    /// <summary>The kind's own type: what it drew before there was a choice.</summary>
    ChartType DefaultChart { get; }

    /// <summary>Whether the kind offers Detailed (a grid of small charts, the cores', does not: no room to write in).</summary>
    bool OffersDetail { get; }
}

/// <summary>[S-6752] The rules over <see cref="IChartLooks"/>: the type drawn and the repair of one a kind does not offer.</summary>
public static class ChartLooks
{
    /// <summary>The histories' types: the line with its wash (their look until now) or alone.</summary>
    public static IReadOnlyList<ChartType> Lines { get; } = [ChartType.Area, ChartType.Line];

    /// <summary>A price's types: the histories' and the candles.</summary>
    public static IReadOnlyList<ChartType> Prices { get; } = [ChartType.Area, ChartType.Line, ChartType.Candles];

    /// <summary>The weather graphs' types: the line with its wash, the line alone, or a column per hour.</summary>
    public static IReadOnlyList<ChartType> Hours { get; } = [ChartType.Area, ChartType.Line, ChartType.Bars];

    /// <summary>The type drawn: the chosen one when the kind offers it, else the kind's own.</summary>
    public static ChartType Effective(IChartLooks looks)
    {
        ArgumentNullException.ThrowIfNull(looks);
        return looks.ChartTypes.Contains(looks.Chart) ? looks.Chart : looks.DefaultChart;
    }

    /// <summary>The detail drawn: Simple where the kind offers no other.</summary>
    public static ChartDetail EffectiveDetail(IChartLooks looks)
    {
        ArgumentNullException.ThrowIfNull(looks);
        return looks.OffersDetail && Enum.IsDefined(looks.Detail) ? looks.Detail : ChartDetail.Simple;
    }

    /// <summary>[S-6752] A type the kind does not offer (a hand-edited file) is its own type again, and a detail it has not Simple: said, never silent.</summary>
    public static IReadOnlyList<string> Repair(IChartLooks looks, string kind)
    {
        ArgumentNullException.ThrowIfNull(looks);
        var notes = new List<string>();
        if (Effective(looks) != looks.Chart)
        {
            notes.Add($"{kind}: chart type {looks.Chart} is not offered; {looks.DefaultChart}");
            looks.Chart = looks.DefaultChart;
        }

        if (EffectiveDetail(looks) != looks.Detail)
        {
            notes.Add($"{kind}: chart detail {looks.Detail} is not offered; {ChartDetail.Simple}");
            looks.Detail = ChartDetail.Simple;
        }

        return notes;
    }

    /// <summary>Copy the two settings onto <paramref name="to"/> (a Clone).</summary>
    public static void Copy(IChartLooks from, IChartLooks to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        to.Chart = from.Chart;
        to.Detail = from.Detail;
    }
}
