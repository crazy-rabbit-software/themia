using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2707] The year: twelve small months, arranged 4×3, 3×4, 6×2, 2×6, 12×1 or 1×12 by the tile's
/// shape; on a tile too small for twelve, fewer around the current month. A day that has something
/// on it is marked (its first item's colour on a square, a strong number with a stripe under it);
/// today is the accent. Each day's events are on its hover.
/// </summary>
internal sealed partial class CalendarView
{
    private void DrawYear(double width, double height)
    {
        var gap = Fluent.Space2;
        var inner = new WidgetArea(width, height).Deflate(Fluent.Space2, 0);
        var fit = CalendarFit.Year(inner.Width, Math.Max(0, inner.Height - Fluent.Space1), gap);
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var firstMonth = CalendarFit.FirstMonth(_anchor, fit.Months);
        var year = _anchor.Year;
        var showColors = ShowColors;
        var byDay = CalendarFit.ByDay(_events, _birthdays, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), TimeZoneInfo.Local);
        var miniWidth = (inner.Width - ((fit.Columns - 1) * gap)) / fit.Columns;

        var grid = new Grid
        {
            Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, Fluent.Space1),
            ColumnSpacing = gap,
            RowSpacing = gap,
        };

        for (var c = 0; c < fit.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var r = 0; r < fit.Rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        // The month's name as long as the small month is wide: "September", "Sep", "S".
        var nameWidth = miniWidth >= FluentType.EstimateWidth(FluentType.Caption, 9) ? NameWidth.Wide
            : miniWidth >= FluentType.EstimateWidth(FluentType.Caption, 3) ? NameWidth.Short
            : NameWidth.Narrow;

        for (var i = 0; i < fit.Months; i++)
        {
            var month = firstMonth + i;
            var mini = MiniMonth(year, month, start, fit, nameWidth, today, byDay, showColors);
            SetColumn(mini, i % fit.Columns);
            SetRow(mini, i / fit.Columns);
            grid.Children.Add(mini);
        }

        _content.Children.Add(grid);
    }

    /// <summary>One small month: its name, the weekday letters when there is room, and its six weeks of day cells.</summary>
    private StackPanel MiniMonth(
        int year,
        int month,
        Themia.Core.Model.WeekStartDay start,
        YearFit fit,
        NameWidth nameWidth,
        DateOnly today,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<DayItem>> byDay,
        bool showColors)
    {
        var current = today.Year == year && today.Month == month;
        var stack = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };

        var name = Fluent.Text(FluentType.Strong(FluentType.Caption), current ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        name.Text = LocaleFormat.MonthName(Strings.Current, month, nameWidth);
        name.Margin = new Thickness(fit.Numbers ? 2 : 0, 0, 0, 0);
        stack.Children.Add(name);

        var cell = Math.Floor(fit.DayCell);
        var days = new Grid();
        for (var c = 0; c < 7; c++)
        {
            days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
        }

        var row0 = 0;
        if (fit.Weekdays)
        {
            days.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
            var order = CalendarRules.WeekdayOrder(start);
            for (var c = 0; c < 7; c++)
            {
                var letter = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                letter.Text = WeekdayText(order[c], WeekdayLabel.Narrow);
                letter.TextTrimming = TextTrimming.None;
                letter.HorizontalAlignment = HorizontalAlignment.Center;
                letter.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(letter, c);
                days.Children.Add(letter);
            }

            row0 = 1;
        }

        for (var r = 0; r < 6; r++)
        {
            days.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
        }

        var cells = CalendarFit.MiniMonth(year, month, start);
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i] is not { } day)
            {
                continue;
            }

            var items = byDay.TryGetValue(day, out var list) ? list : [];
            var element = fit.Numbers
                ? YearNumber(day, day == today, items, cell, showColors)
                : YearSquare(day == today, items, cell, showColors);

            if (items.Count > 0)
            {
                ToolTipService.SetToolTip(element, DayTooltip(day, items));
            }

            SetRow(element, row0 + (i / 7));
            SetColumn(element, i % 7);
            days.Children.Add(element);
        }

        stack.Children.Add(days);
        return stack;
    }

    /// <summary>A day with its number: today the accent disc, a day with something a strong number over a stripe of its first item's colour.</summary>
    private Border YearNumber(DateOnly day, bool isToday, IReadOnlyList<DayItem> items, double cell, bool showColors)
    {
        var number = Fluent.Text(FluentType.Caption, isToday ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
        number.Text = day.Day.ToString(CultureInfo.InvariantCulture);
        number.TextTrimming = TextTrimming.None;
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.VerticalAlignment = VerticalAlignment.Center;
        Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);

        if (items.Count > 0)
        {
            number.FontWeight = FontWeights.SemiBold;
        }

        return new Border
        {
            Child = number,
            Width = cell - 1,
            Height = cell - 1,
            CornerRadius = new CornerRadius(isToday ? (cell - 1) / 2 : 0),
            Background = isToday ? Accent() : FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0, 0, 0, items.Count > 0 && !isToday ? 2 : 0),
            BorderBrush = items.Count > 0 ? MarkOf(items[0], showColors) : null,
        };
    }

    /// <summary>A day too small for its number: a square, filled with its first item's colour when it has something, today ringed.</summary>
    private Border YearSquare(bool isToday, IReadOnlyList<DayItem> items, double cell, bool showColors)
    {
        var side = Math.Max(2, cell - 1);
        return new Border
        {
            Width = side,
            Height = side,
            CornerRadius = new CornerRadius(Math.Min(2, side / 4)),
            Background = items.Count > 0
                ? MarkOf(items[0], showColors)
                : FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray),
            BorderThickness = new Thickness(isToday ? Math.Max(1, Math.Floor(side / 5)) : 0),
            BorderBrush = isToday ? FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White) : null,
        };
    }
}
