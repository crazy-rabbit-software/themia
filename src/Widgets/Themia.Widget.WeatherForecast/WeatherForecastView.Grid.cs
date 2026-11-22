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
    /// [S-14815] The days as cells of one fixed size (the plan's), filled left to right, then down (time runs left to right
    /// in every language, [S-1113]), from the card's left and top edges, touching: as many across as the width holds
    /// whole, the last line the card's foot reaches cut by it, a last line short of days leaving the room after it empty.
    /// Each cell is the day, the sky's glyph and the high beside the low, centred in it: the inset is padding inside the
    /// cell (WR-3), never a margin round the cells. Every edge on a whole device pixel.
    /// </summary>
    private Grid BuildGrid(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastGridPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var scale = _area.Grid().Raster;
        var across = new RowReach(plan.CellWidth, plan.Columns, scale);
        var down = new RowReach(plan.CellHeight, plan.Rows, scale);
        var grid = new Grid
        {
            Margin = ListEdge.Margin,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            FlowDirection = FlowDirection.LeftToRight,
        };

        for (var c = 0; c < plan.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(across.Height(c)) });
        }

        var first = 0;
        foreach (var cells in ForecastGrid.Lines(count, plan.Columns))
        {
            var line = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(down.Height(line)) });
            for (var c = 0; c < cells; c++)
            {
                var cell = BuildCell(days[first + c], today, plan);
                Grid.SetRow(cell, line);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }

            first += cells;
        }

        return grid;
    }

    private Grid BuildCell(ForecastDay day, DateOnly today, ForecastGridPlan plan)
    {
        var label = DayText(day, today);
        label.HorizontalAlignment = HorizontalAlignment.Center;

        var icon = Family.Glyph(LookGlyph(WeatherReport.Look(day.Code)), plan.Icon);
        icon.HorizontalAlignment = HorizontalAlignment.Center;

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
        ItemMark.Mark(host);
        return host;
    }
}
