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
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2707] [S-11542] [S-15780] The year: twelve small months, as squares or the day numbers under the weekday letters,
/// each in a cell of its one size, touching from the card's edges (<see cref="Themia.Widget.Calendar.CalendarSizing.YearCells"/>). A day that has something on it is marked (its
/// first item's colour on a square, a strong number with a stripe under it); today is the accent.
/// Each day's events are on its hover.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>[S-3653] Under this side a day's ring is a hairline: a stroke of 2 would fill the day.</summary>
    private const double RingStrokeFrom = 10;

    private void DrawYear(double width, double height)
    {
        var fit = Themia.Widget.Calendar.CalendarSizing.Year(_config.YearNumbers);
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var firstMonth = CalendarFit.FirstMonth(_anchor, fit.Months);
        var year = _anchor.Year;
        var showColors = ShowColors;
        var byDay = CalendarFit.ByDay(Events, Birthdays, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), TimeZoneInfo.Local);
        var miniWidth = 7 * fit.DayCell;

        // [S-15780] From the card's top edge (under a state's row, right under it) the months' cells at their one size,
        // touching from its left edge, as many a line as the width reaches (the last cut by its right edge), the months in
        // reading order, the last line the card reaches cut by its foot; each an item, its small month centred in its padding.
        var lift = _stated ? 0 : FamilySpace.ContentTop;
        var box = height + lift;
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);

        // [S-16446] Where the card holds all twelve at their own size, the year is a grid of the card (WR-9): the months'
        // cells share it edge to edge in the columns and lines that draw them largest, each small month scaled up as one.
        var share = Themia.Widget.Calendar.CalendarSizing.YearShares(fit, width, box);
        var shared = share.Factor >= 1;
        int across;
        int lines;
        var grid = new Grid { Margin = new Thickness(0, -lift, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
        if (shared)
        {
            across = share.Columns;
            lines = share.Rows;
            foreach (var column in ItemSize.Columns(width, across, cells))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(column) });
            }

            foreach (var line in ItemSize.Columns(box, lines, cells))
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(line) });
            }
        }
        else
        {
            var reach = Themia.Widget.Calendar.CalendarSizing.YearCells(fit, width, box, cells);
            across = reach.Columns;
            lines = (fit.Months + across - 1) / across;
            var down = reach.Down with { Shown = lines };
            for (var c = 0; c < across; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(reach.Across.Height(c)) });
            }

            for (var r = 0; r < lines; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(down.Height(r)) });
            }
        }

        // The month's name as long as the small month is wide: "September", "Sep", "S".
        var nameWidth = miniWidth >= FamilyType.EstimateWidth(FamilyType.Caption, 9) ? NameWidth.Wide
            : miniWidth >= FamilyType.EstimateWidth(FamilyType.Caption, 3) ? NameWidth.Short
            : NameWidth.Narrow;

        for (var i = 0; i < fit.Months; i++)
        {
            var month = firstMonth + i;
            var mini = MiniMonth(year, month, start, fit, nameWidth, today, byDay, showColors);
            mini.VerticalAlignment = VerticalAlignment.Center;
            FrameworkElement drawn = shared
                ? new ScaleBox { Factor = share.Factor, Child = mini, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : mini;
            var cell = new Border
            {
                Child = drawn,
                Padding = new Thickness(Themia.Widget.Calendar.CalendarSizing.YearPad),
                Background = Family.Brush(ColourRole.Clear),
            };
            ItemMark.Mark(cell);
            SetColumn(cell, i % across);
            SetRow(cell, i / across);
            grid.Children.Add(cell);
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
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };

        // The month on view's name is primary (the accent never colours a text), the others secondary.
        var name = Family.Text(FamilyType.Caption, current ? ColourRole.TextPrimary : ColourRole.TextSecondary, LocaleFormat.MonthName(Strings.Current, month, nameWidth));
        name.FontWeight = FontWeights.SemiBold;
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
                var letter = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
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
                ? YearNumber(day, day == today, day == _selected, items, cell, showColors)
                : YearSquare(day == today, day == _selected, items, cell, showColors);

            if (items.Count > 0)
            {
                ToolTipService.SetToolTip(element, DayTooltip(day, items));
            }

            // [S-3620] A click on a day selects it for the calendars on the view.
            // [S-2004] The click is the widget's own: it never reaches the card.
            element.Tapped += (_, args) =>
            {
                args.Handled = true;
                ToggleDay(day);
            };
            WidgetItems.Mark(element); // [S-814]

            // [S-11923] A plain day takes the hover fill for the day pointed at; today's disc and a day in its
            // event's colour keep their face and only publish.
            if (day == today || (!fit.Numbers && items.Count > 0))
            {
                PublishDay(element, day);
            }
            else
            {
                LinkDay(element, day, fit.Numbers ? ItemRole.Row : ItemRole.Chip);
            }

            SetRow(element, row0 + (i / 7));
            SetColumn(element, i % 7);
            days.Children.Add(element);
        }

        stack.Children.Add(days);
        return stack;
    }

    /// <summary>A day with its number: today the accent disc, a day with something a strong number over a stripe of its first item's colour.</summary>
    private Border YearNumber(DateOnly day, bool isToday, bool isSelected, IReadOnlyList<DayItem> items, double cell, bool showColors)
    {
        var number = Family.Text(FamilyType.Caption, isToday ? ColourRole.TextOnAccent : ColourRole.TextPrimary, day.Day.ToString(CultureInfo.InvariantCulture));
        number.TextTrimming = TextTrimming.None;
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.VerticalAlignment = VerticalAlignment.Center;

        if (items.Count > 0)
        {
            number.FontWeight = FontWeights.SemiBold;
        }

        // [S-3620] The selected day is ringed (a hairline): in the accent, or the text colour round today's disc.
        var marked = items.Count > 0 && !isToday && !isSelected;
        return new Border
        {
            Child = number,
            Width = cell,
            Height = cell,
            CornerRadius = isToday || isSelected ? Family.Round(cell, cell) : new CornerRadius(0),
            Background = isToday ? Accent() : Family.Brush(ColourRole.Clear),
            BorderThickness = isSelected ? new Thickness(FamilyShape.Hairline) : new Thickness(0, 0, 0, marked ? FamilyShape.Stroke : 0),
            BorderBrush = isSelected ? (isToday ? Family.Brush(ColourRole.TextPrimary) : Accent()) : marked ? MarkOf(items[0], showColors) : null,
        };
    }

    /// <summary>A day too small for its number: a square, filled with its first item's colour when it has something, today ringed.</summary>
    private Border YearSquare(bool isToday, bool isSelected, IReadOnlyList<DayItem> items, double cell, bool showColors)
    {
        var side = Math.Max(FamilyShape.Stroke, cell - FamilyShape.Hairline);
        var ring = isToday || isSelected;
        var square = new Border
        {
            Width = side,
            Height = side,
            Background = items.Count > 0 ? MarkOf(items[0], showColors) : null,
            BorderThickness = new Thickness(ring ? (side >= RingStrokeFrom ? FamilyShape.Stroke : FamilyShape.Hairline) : 0),
            BorderBrush = isSelected ? Accent() : isToday ? Family.Brush(ColourRole.TextPrimary) : null,
        };

        // [S-6513] An empty day is a chip of the item layer; a day with something is its colour.
        if (items.Count == 0)
        {
            ItemFill.Paint(square, ItemRole.Chip);
        }

        return square;
    }
}
