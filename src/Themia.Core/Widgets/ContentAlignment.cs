using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6580]..[S-6585] Which widgets can align their content Left, Center or Right
/// (<see cref="WidgetInstance.Align"/>), what each does when nothing was chosen, and what a card
/// draws. Pure: the panel, load validation and the card all ask here.
/// <para>
/// Only a widget whose content is one block — a readout, a sentence, a face — has something to
/// align: a list, a grid, a chart or a mixer fills its card edge to edge, and a button tile is the
/// whole tile. Per style, too: an exchange rate's or a stock's chart, the moon's month of discs, fill
/// the card, so the same kind aligns in one style and not in another. <c>ContentAlignmentTests</c>
/// checks the views that take it against this list, so the two cannot drift.
/// </para>
/// </summary>
public static class ContentAlignment
{
    /// <summary>[S-6581] The kinds with a style (or every style) whose content can be aligned.</summary>
    private static readonly HashSet<WidgetKind> Kinds =
    [
        WidgetKind.Clock,
        WidgetKind.WeatherNow,
        WidgetKind.AirQuality,
        WidgetKind.ExchangeRates,
        WidgetKind.Stocks,
        WidgetKind.Countdown,
        WidgetKind.Quote,
        WidgetKind.Moon,
    ];

    /// <summary>[S-6581] The kinds with a style whose content can be aligned, in the enum's order.</summary>
    public static IEnumerable<WidgetKind> All => Enum.GetValues<WidgetKind>().Where(Kinds.Contains);

    /// <summary>[S-6581] Whether any style of <paramref name="kind"/> can be aligned.</summary>
    public static bool Offers(WidgetKind kind) => Kinds.Contains(kind);

    /// <summary>
    /// [S-6581] Whether a widget of <paramref name="kind"/> in the style <paramref name="config"/>
    /// holds can be aligned now: every style of the clock, the weather, the air quality, the
    /// countdown and the quote; the number styles of the exchange rate and the stock; the moon's disc.
    /// </summary>
    public static bool Applies(WidgetKind kind, WidgetConfig? config) => kind switch
    {
        WidgetKind.ExchangeRates => config is not ExchangeRatesConfig rates || rates.Style == ExchangeRatesStyle.Number,
        WidgetKind.Stocks => config is not StocksConfig stocks || stocks.Style == StocksStyle.Number,
        WidgetKind.Moon => config is not MoonConfig moon || moon.Style == MoonStyle.Disc,
        _ => Kinds.Contains(kind),
    };

    /// <summary>Whether <paramref name="widget"/> can be aligned in the style it has now.</summary>
    public static bool Applies(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Applies(widget.Kind, widget.Config);
    }

    /// <summary>
    /// [S-6582] Where the content sits when nothing was chosen: where each widget drew it before it
    /// had a choice, so nothing moves. Centred: the clock, the moon's disc, the weather's large-icon
    /// and minimal styles, the air quality's gauge. Everything else reads from the start edge.
    /// </summary>
    public static ContentAlign Default(WidgetKind kind, WidgetConfig? config) => kind switch
    {
        WidgetKind.Clock or WidgetKind.Moon => ContentAlign.Center,
        WidgetKind.WeatherNow when config is WeatherNowConfig { Style: not WeatherNowStyle.Standard } => ContentAlign.Center,
        WidgetKind.AirQuality when config is AirQualityConfig { Style: AirQualityStyle.Gauge } => ContentAlign.Center,
        _ => ContentAlign.Left,
    };

    /// <summary>
    /// [S-6583] Where the card draws <paramref name="widget"/>'s content: its own choice, or the
    /// kind's default when it has none, holds one outside the set, or cannot be aligned in its style.
    /// </summary>
    public static ContentAlign Shown(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return widget.Align is { } own && Enum.IsDefined(own) && Applies(widget)
            ? own
            : Default(widget.Kind, widget.Config);
    }

    /// <summary>
    /// [S-6585] Repair a widget's stored alignment in place: a value outside the set, or one on a kind
    /// that never aligns, is dropped (the kind's default). Returns what was wrong, or null. A choice
    /// on a style that does not align is kept: switching back to an aligned style finds it.
    /// </summary>
    public static string? Repair(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (widget.Align is not { } own)
        {
            return null;
        }

        if (!Enum.IsDefined(own))
        {
            widget.Align = null;
            return $"alignment {(int)own} unknown; the kind's default";
        }

        if (!Offers(widget.Kind))
        {
            widget.Align = null;
            return $"{widget.Kind} has nothing to align; alignment dropped";
        }

        return null;
    }
}
