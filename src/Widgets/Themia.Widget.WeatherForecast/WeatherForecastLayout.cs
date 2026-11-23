using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11145] [S-11146] [S-14410] [S-14502] [S-16974] The forecast's sizes: its days (1..14, a new one 5) are the card's, every
/// one its box reaches at their one fixed size, the last cut ([S-14810]), never stored. The item base's arrangements are laid
/// by the base (<see cref="ItemLayout"/>, the days' own measures <see cref="ForecastItems.Cells"/>); the curve is its own:
/// a 60 column a day, the chart the height the days' parts leave.
/// </summary>
public static class WeatherForecastLayout
{
    /// <summary>
    /// [S-14502] The days the curve shows in <paramref name="area"/>: as many columns as it reaches at their one width from the
    /// card's left edge, the last cut by its right edge, 1..14; before the box is measured, a new one's.
    /// </summary>
    public static int DaysIn(WidgetArea area, WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return area.IsMeasured
            ? ItemSize.Count(area.Width, ForecastCurve.ColumnStart, area.Grid(), 1, WeatherForecastConfig.MostDays)
            : WeatherForecastConfig.DefaultDays;
    }

    /// <summary>
    /// [S-11146] [S-14500] [S-14810] [S-16974] Each style's sizing from the config: a new one five days, one day at least, every
    /// cell between: the item base's in its arrangement (the table's under no titles but where on; the row list's 60 columns,
    /// the grid's 80 × 100 cells three across), the curve's own.
    /// </summary>
    public static WidgetSizing Sizing(WeatherForecastConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        const int days = WeatherForecastConfig.DefaultDays, most = WeatherForecastConfig.MostDays;
        var grid = ItemGrid.Default();
        if (ForecastItems.Arrangement(config.Style) is not { } style)
        {
            var chrome = FamilySpace.ContentTop + FamilySpace.Inset;
            var parts = ForecastCurve.Parts(config.ShowPrecipitation);
            double Across(int n) => n * ItemSize.Optimal(ForecastCurve.ColumnStart, grid);
            return WidgetSizing.Range(
                new(Across(1), chrome + parts + ForecastCurve.ChartMin),
                new(Across(days), chrome + parts + ForecastCurve.ChartStart),
                new(Across(most), chrome + parts + ForecastCurve.ChartMax));
        }

        var shape = ForecastItems.Shape(config.Style, config.HiddenParts, config.ShowPrecipitation);
        return ItemLayout.Sizing(style, days, config.Gap, shape, ForecastItems.Cells, config.ColumnTitles);
    }

    /// <summary>The content's width: the view less the inset either side.</summary>
    public static double ContentWidth(WidgetArea area) => Math.Max(0, area.Width - (2 * FamilySpace.Inset));

    /// <summary>The content's height: the view (its top is the content's) less the bottom inset.</summary>
    public static double ContentHeight(WidgetArea area) => Math.Max(0, area.Height - FamilySpace.Inset);
}
