using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>The parts the curve's columns share. [S-726] [S-727]</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>
    /// [S-14810] [S-14815] A column of a day's parts: from the card's top edge to its foot (the columns stand on
    /// <see cref="ListEdge"/>, the inset over and under the parts their padding inside the column, WR-3), from the card's
    /// left edge.
    /// </summary>
    private static Thickness ColumnPadding => new(0, FamilySpace.Inset, 0, FamilySpace.Inset);

    /// <summary>[S-14815] The columns' widths, each its one size on whole device pixels, from the card's left edge.</summary>
    private void AddColumns(Grid grid, double width, int count)
    {
        var reach = new RowReach(width, count, _laid.Grid().Raster);
        for (var i = 0; i < count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(reach.Height(i)) });
        }
    }

    /// <summary>[S-727] "Today" or the weekday, in Body; today strong, never in the accent.</summary>
    private static TextBlock DayText(ForecastDay day, DateOnly today)
        => Family.Text(day.Day == today ? FamilyType.BodyStrong : FamilyType.Body, ColourRole.TextPrimary, WeatherReport.DayLabel(day.Day, today));

    private static string Describe(ForecastDay day) => ForecastItems.Describe(day, Strings.Current);
}
