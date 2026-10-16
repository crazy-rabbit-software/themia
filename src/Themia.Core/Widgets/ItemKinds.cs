using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6570] Whether a kind's view draws items at all: rows, tiles, chips or tracks painted through
/// the item layer (<c>ItemFill</c>, [S-6513]). A kind without items has nothing an item background
/// could reach, so the properties panel greys that row out for it ([S-6571]).
/// <para>
/// Declared here, next to the kinds, because the panel's rules are Core's; it cannot drift from
/// the views: <c>ItemKindsTests</c> scans every view's source (and the shared parts it uses) and
/// fails when a kind listed here paints an item, or a kind not listed does not.
/// </para>
/// </summary>
public static class ItemKinds
{
    /// <summary>[S-6570] The kinds whose views paint no item.</summary>
    private static readonly HashSet<WidgetKind> Without =
    [
        WidgetKind.Alarm,
        WidgetKind.AudioVisualizer,
        WidgetKind.Brightness,
        WidgetKind.ExchangeRates,
        WidgetKind.Gallery,
        WidgetKind.NextMeeting,
        WidgetKind.Notes,
        WidgetKind.RainNextHour,
        WidgetKind.RecycleBin,
        WidgetKind.ScreenTimeChart,
        WidgetKind.ScreenTimeDonut,
        WidgetKind.Stocks,
        WidgetKind.Stopwatch,
        WidgetKind.VideoStream,
        WidgetKind.Volume,
        WidgetKind.WeatherNow,
    ];

    /// <summary>[S-6570] Whether a widget of <paramref name="kind"/> has items an item background reaches.</summary>
    public static bool Has(WidgetKind kind) => !Without.Contains(kind);
}
