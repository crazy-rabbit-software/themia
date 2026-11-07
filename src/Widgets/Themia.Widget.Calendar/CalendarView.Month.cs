using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2702] [S-572] The month: a grid of the family's tokens inside the card's inset ([S-6703]). The
/// weekday header in Caption tertiary, the days' numbers in Caption (primary, tertiary outside the
/// month, BodyStrong's weight where something is on), today's on the accent's round pill, the week
/// rows sharing the height (the weeks around today when not every week's line fits,
/// <see cref="CalendarFit.Month"/>). A cell shows its items as chips (Caption on the item layer's
/// chip fill, the calendar's colour as a 2 px bar at its start, the one radius, lanes 4 apart), as
/// dots (4, 4 apart) or by its number's weight alone.
/// </summary>
internal sealed partial class CalendarView
{
    private void DrawMonth(double width, double height)
    {
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        var cells = CalendarRules.MonthCells(_anchor, start);
        var rows = CalendarFit.MonthRows(_anchor, start);
        var focus = FocusRow(cells, rows, SelectedInView ? _selected!.Value : InView(today) ? today : _anchor);
        var innerWidth = Math.Max(0, width - (2 * FamilySpace.Inset));
        if (!CalendarFit.HoldsDays(innerWidth / 7))
        {
            DrawDayReadout(width, height);
            return;
        }

        var fit = CalendarFit.Month(innerWidth, Math.Max(0, height - FamilySpace.Inset), rows, focus);
        var columnWidth = innerWidth / 7;

        var grid = new Grid { Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.HeaderHeight) });
        for (var r = 0; r < fit.Rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        // [S-564] The header follows the week start: Mo..Su or Su..Sa, as much as a column holds.
        if (fit.Weekdays != WeekdayLabel.None)
        {
            var order = CalendarRules.WeekdayOrder(start);
            var chips = fit.Density == MonthDensity.Chips;
            var header = Columns7();
            for (var c = 0; c < 7; c++)
            {
                var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, WeekdayText(order[c], fit.Weekdays));
                label.TextTrimming = TextTrimming.None;
                label.HorizontalAlignment = chips ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                var holder = new Border { Child = label, Padding = new Thickness(chips ? FamilySpace.S1 : 0, 0, 0, 0) };
                SetColumn(holder, c);
                header.Children.Add(holder);
            }

            grid.Children.Add(header);
        }

        // Three dots and the muted one are 28: a narrower cell takes two and the muted one.
        var dotLimit = columnWidth >= 32 ? 3 : 2;

        for (var r = 0; r < fit.Rows; r++)
        {
            var weekStart = cells[(fit.FirstRow + r) * 7];
            var week = Columns7();
            var bars = fit.Density == MonthDensity.Chips
                ? CalendarFit.Bars(_events, _birthdays, weekStart, 7, zone, singleTimed: true)
                : [];

            if (fit.Density == MonthDensity.Chips)
            {
                // The number's line under the cell's padding, then one row per chip line, the rest empty.
                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CalendarFit.MonthCellChrome + FamilyType.Caption.LineHeight) });
                for (var lane = 0; lane < fit.ChipLines; lane++)
                {
                    week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.ChipLine) });
                }

                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (var c = 0; c < 7; c++)
            {
                var day = weekStart.AddDays(c);
                var items = fit.Density == MonthDensity.Chips ? [] : CalendarRules.DayItems(_events, _birthdays, day, zone);
                var count = fit.Density == MonthDensity.Chips ? bars.Count(b => b.First <= c && c <= b.Last) : items.Count;
                var hidden = fit.Density == MonthDensity.Chips ? CalendarFit.Hidden(bars, c, fit.ChipLines) : 0;

                // [S-3620] The selected day (also the keys' cursor day, [S-3624]) is marked in its cell.
                var cell = DayCell(day, items, count, hidden, day.Month == _anchor.Month, day == today, _selected == day, fit, showColors, dotLimit);

                // [S-2004] The click is the widget's own: it never reaches the card.
                cell.Tapped += (_, args) =>
                {
                    args.Handled = true;
                    ToggleDay(day);
                };
                WidgetItems.Mark(cell); // [S-814] a press on a day is the day's toggle

                if (count > 0)
                {
                    ToolTipService.SetToolTip(cell, DayTooltip(day, CalendarRules.DayItems(_events, _birthdays, day, zone)));
                }

                SetColumn(cell, c);
                SetRowSpan(cell, Math.Max(1, week.RowDefinitions.Count));
                week.Children.Add(cell);
            }

            // [S-2703] The row's chips over its cells: one bar per item, across the days it covers.
            foreach (var bar in bars.Where(b => b.Lane < fit.ChipLines))
            {
                var chip = MonthChip(bar, showColors, fit.Times);
                SetRow(chip, 1 + bar.Lane);
                SetColumn(chip, bar.First);
                SetColumnSpan(chip, bar.Last - bar.First + 1);
                week.Children.Add(chip);
            }

            SetRow(week, 1 + r);
            grid.Children.Add(week);
        }

        _content.Children.Add(grid);
    }

    /// <summary>
    /// [S-2702] A tile too narrow for a day grid: the day the view is on (the selected one, else
    /// today) as a Readout, its weekday under it, at the content's top inside the inset.
    /// </summary>
    private void DrawDayReadout(double width, double height)
    {
        var day = SelectedInView ? _selected!.Value : InView(Today) ? Today : _anchor;
        var readout = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Top };
        readout.Size(_area, Math.Max(0, width - (2 * FamilySpace.Inset)), Math.Max(0, height - FamilySpace.Inset), new ReadoutText(LocaleFormat.Digits(Strings.Current, "00"), LineUnder: true));
        readout.Show(LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture)), under: CalendarText.WeekdayName(day.DayOfWeek));
        _content.Children.Add(new Grid { Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset), Children = { readout } });
    }

    /// <summary>The week row holding <paramref name="day"/> among the month's <paramref name="rows"/>; 0 when it is not in the grid.</summary>
    private static int FocusRow(IReadOnlyList<DateOnly> cells, int rows, DateOnly day)
    {
        for (var r = 0; r < rows && (r * 7) + 6 < cells.Count; r++)
        {
            if (cells[r * 7] <= day && day <= cells[(r * 7) + 6])
            {
                return r;
            }
        }

        return 0;
    }

    private static Grid Columns7()
    {
        var grid = new Grid();
        for (var c = 0; c < 7; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        return grid;
    }

    private Border DayCell(
        DateOnly day,
        IReadOnlyList<DayItem> items,
        int count,
        int hidden,
        bool inMonth,
        bool isToday,
        bool isSelected,
        MonthFit fit,
        bool showColors,
        int dotLimit)
    {
        var density = fit.Density;
        var chipped = density == MonthDensity.Chips;

        var number = Family.Text(FamilyType.Caption, isToday ? ColourRole.TextOnAccent : inMonth ? ColourRole.TextPrimary : ColourRole.TextTertiary,
            LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture)));
        number.TextTrimming = TextTrimming.None;

        // A day with something on it has its number in the strong weight: in the barest density
        // that is the one sign left, and it is not colour alone.
        if (count > 0)
        {
            number.FontWeight = FontWeights.SemiBold;
        }

        FrameworkElement numberHost = number;
        if (isToday)
        {
            // [S-572] Today is the accent's round pill, as Windows' own calendar flyout marks it.
            number.HorizontalAlignment = HorizontalAlignment.Center;
            numberHost = TodayPill(number, FamilyType.Caption.LineHeight);
        }

        if (fit.InlineDots && items.Count > 0)
        {
            // A wide, short cell: the dots beside the number, where there is room for them.
            var dots = Dots(items, showColors, dotLimit);
            dots.VerticalAlignment = VerticalAlignment.Center;
            numberHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.S1, Children = { numberHost, dots } };
        }

        numberHost.HorizontalAlignment = chipped ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        numberHost.VerticalAlignment = VerticalAlignment.Top;

        FrameworkElement face = numberHost;
        if (chipped)
        {
            // The number row carries the overflow count at its end, so "+N" never costs a chip line.
            var numberRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                Children = { numberHost },
            };

            if (hidden > 0)
            {
                var more = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, LocaleFormat.Digits(Strings.Current, $"+{hidden.ToString(CultureInfo.InvariantCulture)}"));
                more.HorizontalAlignment = HorizontalAlignment.Right;
                more.TextTrimming = TextTrimming.None;
                SetColumn(more, 1);
                numberRow.Children.Add(more);
            }

            numberRow.VerticalAlignment = VerticalAlignment.Top;
            face = numberRow;
        }
        else if (density == MonthDensity.Dots)
        {
            var stack = new StackPanel { Spacing = FamilySpace.S1, VerticalAlignment = VerticalAlignment.Top, Children = { numberHost } };
            if (items.Count > 0)
            {
                stack.Children.Add(Dots(items, showColors, dotLimit));
            }

            face = stack;
        }
        else
        {
            // A bare number takes the whole cell, centred in its row.
            numberHost.VerticalAlignment = VerticalAlignment.Center;
        }

        var cell = new Border
        {
            Child = face,
            Padding = density == MonthDensity.Number ? new Thickness(0) : new Thickness(FamilySpace.S1, FamilySpace.S1, FamilySpace.S1, 0),
            CornerRadius = Family.Radius,
            Background = Family.Brush(ColourRole.Clear),
        };

        // [S-3620] Without chips nothing spans a cell, so the selected one is outlined whole: a ring
        // laid over the cell, so the number does not move.
        if (!chipped && isSelected)
        {
            cell.Child = null;
            var ring = new Border
            {
                BorderThickness = new Thickness(FamilyShape.Hairline),
                BorderBrush = Accent(),
                CornerRadius = Family.Radius,
                IsHitTestVisible = false,
            };
            var holder = new Grid { Children = { face, ring } };
            ring.Margin = new Thickness(-cell.Padding.Left, -cell.Padding.Top, -cell.Padding.Right, -cell.Padding.Bottom);
            cell.Child = holder;
        }

        // [S-6513] [S-3620] A selected day holding chips is lit as a selected chip (an outline would
        // cross the chips spanning it); the rest stay clear, so the chips on them read against the card.
        if (chipped && isSelected)
        {
            ItemFill.Paint(cell, ItemRole.Chip, ItemState.Selected);
        }

        return cell;
    }

    /// <summary>[S-572] Today's number on the accent: a pill one line tall, round, 4 either side of the number.</summary>
    private static Border TodayPill(TextBlock number, double line) => new()
    {
        Child = number,
        Background = Accent(),
        Height = line,
        MinWidth = line,
        Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
        CornerRadius = Family.Round(line, line),
    };

    /// <summary>
    /// [S-2703] A chip: Caption on the item layer's chip fill, one line tall, the item's mark as a 2 px
    /// bar at its start (none where it continues from the week before), the one radius at the ends it
    /// has, 4 in from the day's edge at both.
    /// </summary>
    private Border MonthChip(LaneItem bar, bool showColors, bool times)
    {
        var item = bar.Item;
        var text = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
        text.VerticalAlignment = VerticalAlignment.Center;
        var r = FamilyShape.Radius;
        var chip = new Border
        {
            Child = text,
            Height = FamilyType.Caption.LineHeight,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            Margin = new Thickness(bar.ContinuesBefore ? 0 : FamilySpace.S1, 0, bar.ContinuesAfter ? 0 : FamilySpace.S1, 0),
            CornerRadius = new CornerRadius(bar.ContinuesBefore ? 0 : r, bar.ContinuesAfter ? 0 : r, bar.ContinuesAfter ? 0 : r, bar.ContinuesBefore ? 0 : r),
            BorderThickness = new Thickness(bar.ContinuesBefore ? 0 : FamilyShape.Stroke, 0, 0, 0),
            BorderBrush = MarkOf(item, showColors),
        };
        ItemFill.Paint(chip, ItemRole.Chip);

        if (item.Birthday is { } b)
        {
            text.Text = BirthdayRules.FirstName(b.Name);
            return chip;
        }

        var e = item.Event!;
        if (times && !e.AllDay && !bar.ContinuesBefore)
        {
            text.Inlines.Add(Family.Run(FamilyType.Caption, ColourRole.TextSecondary, Clock(e.Start) + " "));
            text.Inlines.Add(new Run { Text = Title(e) });
        }
        else
        {
            text.Text = Title(e);
        }

        MakeOpenable(chip, e);
        return chip;
    }

    private StackPanel Dots(IReadOnlyList<DayItem> items, bool showColors, int limit)
    {
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.S1, HorizontalAlignment = HorizontalAlignment.Center };

        foreach (var item in items.Take(limit))
        {
            dots.Children.Add(Dot(MarkOf(item, showColors)));
        }

        if (items.Count > limit)
        {
            dots.Children.Add(Dot(Family.Brush(ColourRole.TextTertiary)));
        }

        return dots;
    }

    /// <summary>[S-572] A day's dot: the track's 4, round.</summary>
    private static Ellipse Dot(Brush fill) => new() { Width = CalendarFit.Dot, Height = CalendarFit.Dot, Fill = fill };
}
