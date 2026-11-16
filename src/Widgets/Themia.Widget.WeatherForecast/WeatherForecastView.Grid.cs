using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>[S-6171] The forecast's grid style.</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>
    /// The days that have a cell's floor, as cells in the plan's columns × rows (filled left to
    /// right, then down; time runs left to right in every language, [S-1113]), 12 apart. Each cell is
    /// the day, the sky's glyph (32, 48 on an expanded width) and the high beside the low, centred in it.
    /// [S-12403] Each line of cells fills the content's width: a last line short of days shares the
    /// width among the days it has (<see cref="ForecastGrid.Lines"/>), so no empty cell is left at its end.
    /// </summary>
    private Grid BuildGrid(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastGridPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid
        {
            Margin = ContentInset,
            RowSpacing = ForecastGrid.Gap,
            FlowDirection = FlowDirection.LeftToRight,
        };

        var first = 0;
        foreach (var cells in ForecastGrid.Lines(count, plan.Columns))
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var line = new Grid { ColumnSpacing = ForecastGrid.Gap };
            for (var c = 0; c < cells; c++)
            {
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var cell = BuildCell(days[first + c], today, plan);
                Grid.SetColumn(cell, c);
                line.Children.Add(cell);
            }

            Grid.SetRow(line, grid.RowDefinitions.Count - 1);
            grid.Children.Add(line);
            first += cells;
        }

        return grid;
    }

    private Grid BuildCell(ForecastDay day, DateOnly today, ForecastGridPlan plan)
    {
        var label = DayText(day, today);
        label.HorizontalAlignment = HorizontalAlignment.Center;

        var icon = Family.Glyph(LookGlyph(WeatherReport.Look(day.Code)), Math.Max(plan.Icon, FamilyShape.Icon));
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.Visibility = plan.Icon > 0 ? Visibility.Visible : Visibility.Collapsed;

        var figures = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ForecastGrid.FigureGap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Family.Text(FamilyType.Body, ColourRole.TextPrimary, WeatherReport.Degrees(day.High)),
                Family.Text(FamilyType.Body, ColourRole.TextSecondary, WeatherReport.Degrees(day.Low)),
            },
        };

        var host = new Grid
        {
            Background = Family.Brush(ColourRole.Clear),
            Children =
            {
                new StackPanel
                {
                    Spacing = ForecastGrid.PartGap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { label, icon, figures },
                },
            },
        };

        ToolTipService.SetToolTip(host, Describe(day));
        LinkDay(host, day.Day);
        return host;
    }
}
