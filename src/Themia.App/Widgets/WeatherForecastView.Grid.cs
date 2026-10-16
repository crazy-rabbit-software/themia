using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-6171] The forecast's grid style.</summary>
internal sealed partial class WeatherForecastView
{
    /// <summary>
    /// The days that have a cell's floor, as cells in the plan's columns × rows (filled left to
    /// right, then down; time runs left to right in every language, [S-1113]). Each cell is the
    /// weekday, the sky's glyph and the high beside the low, centred, at the plan's scale.
    /// </summary>
    private Grid BuildGrid(IReadOnlyList<ForecastDay> days, DateOnly today, ForecastGridPlan plan)
    {
        var count = Math.Min(plan.Days, days.Count);
        var grid = new Grid
        {
            Padding = new Thickness(ForecastGrid.Padding),
            RowSpacing = ForecastGrid.Gap,
            ColumnSpacing = ForecastGrid.Gap,
            FlowDirection = FlowDirection.LeftToRight,
        };

        for (var c = 0; c < plan.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < plan.Rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < count; i++)
        {
            var cell = BuildCell(days[i], today, plan);
            SetColumn(cell, i % plan.Columns);
            SetRow(cell, i / plan.Columns);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private Border BuildCell(ForecastDay day, DateOnly today, ForecastGridPlan plan)
    {
        var scale = plan.Scale;
        var label = DayText(day, today, ForecastGrid.LabelStep(scale));
        label.HorizontalAlignment = HorizontalAlignment.Center;

        var icon = Fluent.Icon(LookGlyph(WeatherReport.Look(day.Code)), "TextFillColorSecondaryBrush", scale.Icon);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.Visibility = plan.Icon ? Visibility.Visible : Visibility.Collapsed;

        var high = WidgetParts.Figure(WeatherReport.Degrees(day.High), scale.High);
        high.MinWidth = 0;
        var low = WidgetParts.Figure(WeatherReport.Degrees(day.Low), scale.Low, "TextFillColorSecondaryBrush");
        low.MinWidth = 0;

        var figures = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = ForecastGrid.Gap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { high, low },
        };

        var host = new Border
        {
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Padding = new Thickness(ForecastGrid.CellSidePadding, ForecastGrid.CellPadding, ForecastGrid.CellSidePadding, ForecastGrid.CellPadding),
            Child = new StackPanel
            {
                Spacing = ForecastGrid.PartGap,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { label, icon, figures },
            },
        };

        // [S-727] Today is a selected chip, as its row or column is in the other styles.
        if (day.Day == today)
        {
            ItemFill.Paint(host, ItemRole.Chip, ItemState.Selected);
        }

        ToolTipService.SetToolTip(host, Describe(day));
        return host;
    }
}
