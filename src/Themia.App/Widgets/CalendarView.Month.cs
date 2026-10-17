using System.Globalization;
using Microsoft.UI;
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
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-572] / [S-2702] The month: the weeks holding its days, each day with as much of what is on it
/// as its cell holds — chips (a multi-day event one bar across its days, [S-2703]), dots, or the
/// number alone — under weekday names, initials or letters, or none on the smallest tile.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>Outer margin and inner padding of a month cell; together <see cref="CalendarFit.MonthCellChrome"/> with the gap under the number.</summary>
    private const double CellMargin = 1;

    private const double CellPadding = 2;

    private void DrawMonth(double width, double height)
    {
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        var inner = new WidgetArea(width, height).Deflate(Fluent.Space1, 0);
        var fit = CalendarFit.Month(inner.Width, Math.Max(0, inner.Height - Fluent.Space1), CalendarFit.MonthRows(_anchor, start));
        var cells = CalendarRules.MonthCells(_anchor, start);
        var columnWidth = inner.Width / 7;

        var grid = new Grid { Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, Fluent.Space1) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.HeaderHeight) });
        for (var r = 0; r < fit.Rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        for (var c = 0; c < 7; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // [S-564] The header follows the week start: Mo..Su or Su..Sa, as much as a column holds.
        if (fit.Weekdays != WeekdayLabel.None)
        {
            var order = CalendarRules.WeekdayOrder(start);
            var names = fit.Weekdays == WeekdayLabel.Short && fit.Density == MonthDensity.Chips;
            for (var c = 0; c < 7; c++)
            {
                var label = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                label.Text = WeekdayText(order[c], fit.Weekdays);
                label.TextTrimming = TextTrimming.None;
                label.HorizontalAlignment = names ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                label.Margin = names ? new Thickness(CellMargin + CellPadding + 1, 0, 0, 0) : new Thickness(0);
                SetColumn(label, c);
                grid.Children.Add(label);
            }
        }

        // Four dots and their gaps are 26px; a narrower cell takes two and the muted one.
        var dotLimit = columnWidth >= 32 ? 3 : 2;

        for (var r = 0; r < fit.Rows; r++)
        {
            var weekStart = cells[r * 7];
            var bars = fit.Density == MonthDensity.Chips
                ? CalendarFit.Bars(_events, _birthdays, weekStart, 7, zone, singleTimed: true)
                : [];

            for (var c = 0; c < 7; c++)
            {
                var day = weekStart.AddDays(c);
                var items = fit.Density == MonthDensity.Chips ? [] : CalendarRules.DayItems(_events, _birthdays, day, zone);
                var count = fit.Density == MonthDensity.Chips ? bars.Count(b => b.First <= c && c <= b.Last) : items.Count;
                var hidden = fit.Density == MonthDensity.Chips ? CalendarFit.Hidden(bars, c, fit.ChipLines) : 0;

                // [S-3620] The selected day (also the keys' cursor day, [S-3624]) is marked in its cell.
                var cell = DayCell(day, items, count, hidden, day.Month == _anchor.Month, day == today, _selected == day, fit, showColors, dotLimit);

                cell.Tapped += (_, _) => ToggleDay(day);
                WidgetItems.Mark(cell); // [S-814] a press on a day is the day's toggle


                if (count > 0)
                {
                    ToolTipService.SetToolTip(cell, DayTooltip(day, CalendarRules.DayItems(_events, _birthdays, day, zone)));
                }

                SetRow(cell, 1 + r);
                SetColumn(cell, c);
                grid.Children.Add(cell);
            }

            // [S-2703] The row's chips over its cells: one bar per item, across the days it covers.
            foreach (var bar in bars.Where(b => b.Lane < fit.ChipLines))
            {
                var chip = MonthChip(bar, showColors, fit.ChipLine, fit.Times);
                chip.VerticalAlignment = VerticalAlignment.Top;
                chip.Margin = new Thickness(
                    bar.ContinuesBefore ? CellMargin : CellMargin + CellPadding,
                    CellMargin + CellPadding + FluentType.Caption.LineHeight + 1 + (bar.Lane * fit.ChipLine),
                    bar.ContinuesAfter ? CellMargin : CellMargin + CellPadding,
                    0);
                SetRow(chip, 1 + r);
                SetColumn(chip, bar.First);
                SetColumnSpan(chip, bar.Last - bar.First + 1);
                grid.Children.Add(chip);
            }
        }

        _content.Children.Add(grid);
    }

    /// <summary>A day's cell: its number (today in the accent disc), the "+N" its chips left out, or its dots.</summary>
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

        // [S-2702] On the smallest tile a row is shorter than a caption line: the number's line box
        // shrinks to the row (the glyphs still fit), rather than the text being clipped.
        var line = Math.Min(FluentType.Caption.LineHeight, Math.Max(1, Math.Floor(fit.CellHeight)));
        var stack = new StackPanel { Spacing = 1 };

        // The number row carries the overflow count on the right, so "+N" never costs a chip line.
        var numberRow = new Grid
        {
            ColumnDefinitions =
            {
                // Chips: the number left, "+N" right. Otherwise the number is the cell, centred in it.
                new ColumnDefinition { Width = density == MonthDensity.Chips ? GridLength.Auto : new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = density == MonthDensity.Chips ? new GridLength(1, GridUnitType.Star) : GridLength.Auto },
            },
        };

        var number = Fluent.Text(
            FluentType.Caption,
            isToday ? "TextOnAccentFillColorPrimaryBrush" : inMonth ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush");
        number.Text = LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture));
        Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
        number.TextTrimming = TextTrimming.None;
        if (line < FluentType.Caption.LineHeight)
        {
            number.LineHeight = line;
            number.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        }

        // A day with something on it has its number in the strong weight: in the barest density
        // that is the one sign left, and it is not colour alone.
        if (count > 0)
        {
            number.FontWeight = FontWeights.SemiBold;
        }

        FrameworkElement numberHost = number;
        if (isToday)
        {
            // [S-572] Today is the accent disc Windows' own calendar flyout uses.
            numberHost = new Border
            {
                Child = number,
                Background = Accent(),
                CornerRadius = new CornerRadius(line / 2),
                MinWidth = line,
                Height = line,
                Padding = new Thickness(3, 0, 3, 0),
            };
            number.HorizontalAlignment = HorizontalAlignment.Center;
        }

        // [S-3620] With chips the selected day's number is ringed and its cell filled, rather than the
        // cell outlined: a chip spanning the day would be crossed by the outline (the chips are
        // translucent, so even an outline under them showed through). Today's disc keeps its accent
        // and takes the ring in the text colour.
        var chipped = density == MonthDensity.Chips;
        if (chipped && isSelected)
        {
            if (numberHost is Border disc)
            {
                disc.BorderThickness = new Thickness(1);
                disc.BorderBrush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
            }
            else
            {
                number.HorizontalAlignment = HorizontalAlignment.Center;
                numberHost = new Border
                {
                    Child = number,
                    CornerRadius = new CornerRadius(line / 2),
                    MinWidth = line,
                    Height = line,
                    Padding = new Thickness(3, 0, 3, 0),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Accent(),
                };
            }
        }

        if (fit.InlineDots && items.Count > 0)
        {
            // A wide, short cell: the dots beside the number, where there is room for them.
            var dots = Dots(items, showColors, dotLimit);
            dots.VerticalAlignment = VerticalAlignment.Center;
            dots.Margin = new Thickness(3, 0, 0, 0);
            numberHost = new StackPanel { Orientation = Orientation.Horizontal, Children = { numberHost, dots } };
        }

        numberHost.HorizontalAlignment = density == MonthDensity.Chips ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        numberRow.Children.Add(numberHost);

        if (hidden > 0)
        {
            var more = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            more.Text = LocaleFormat.Digits(Strings.Current, $"+{hidden.ToString(CultureInfo.InvariantCulture)}");
            more.HorizontalAlignment = HorizontalAlignment.Right;
            more.TextTrimming = TextTrimming.None;
            SetColumn(more, 1);
            numberRow.Children.Add(more);
        }

        stack.Children.Add(numberRow);

        if (density == MonthDensity.Dots && items.Count > 0)
        {
            stack.Children.Add(Dots(items, showColors, dotLimit));
        }

        var cell = new Border
        {
            Child = stack,

            // A bare number takes the whole cell: on a short tile the row is the number's own line,
            // and any padding or margin would clip it or today's disc.
            Padding = density == MonthDensity.Number ? new Thickness(0) : new Thickness(CellPadding, CellPadding, CellPadding, 0),
            Margin = density == MonthDensity.Number ? new Thickness(0) : new Thickness(CellMargin),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = density == MonthDensity.Number ? VerticalAlignment.Center : VerticalAlignment.Stretch,
            Background = Fluent.Clear(),

            // Without chips nothing spans a cell, so the selected one is outlined whole.
            BorderThickness = new Thickness(!chipped && isSelected ? 1 : 0),
            BorderBrush = !chipped && isSelected ? Accent() : null,
        };

        // [S-6513] A selected day holding chips is lit as a selected chip; the rest stay clear, so
        // the chips on them read against the card, or against the item colour where it is set.
        if (chipped && isSelected)
        {
            ItemFill.Paint(cell, ItemRole.Chip, ItemState.Selected);
        }

        return cell;
    }

    /// <summary>
    /// [S-2703] One item as a one-line chip, titled and coloured by its calendar when colours show; a
    /// bar that runs on past the row's edge is squared off there. In a wide cell a timed event leads
    /// with its start time ([S-1264], the classic S-44).
    /// </summary>
    private Border MonthChip(LaneItem bar, bool showColors, double chipLine, bool times)
    {
        var item = bar.Item;
        var text = Fluent.Text(FluentType.Caption);
        text.VerticalAlignment = VerticalAlignment.Center;
        var radius = 2.0;
        var chip = new Border
        {
            Child = text,
            Height = chipLine - 1,
            Padding = new Thickness(3, 0, 2, 0),
            CornerRadius = new CornerRadius(bar.ContinuesBefore ? 0 : radius, bar.ContinuesAfter ? 0 : radius, bar.ContinuesAfter ? 0 : radius, bar.ContinuesBefore ? 0 : radius),
        };
        ItemFill.Paint(chip, ItemRole.Chip);

        if (item.Birthday is { } b)
        {
            text.Text = BirthdayRules.FirstName(b.Name);
            chip.BorderThickness = new Thickness(2, 0, 0, 0);
            chip.BorderBrush = BirthdayBrush();
            return chip;
        }

        var e = item.Event!;
        if (times && !e.AllDay && !bar.ContinuesBefore)
        {
            text.Inlines.Add(new Run { Text = Clock(e.Start) + " ", Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray) });
            text.Inlines.Add(new Run { Text = Title(e) });
            Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
        }
        else
        {
            text.Text = Title(e);
        }

        if (showColors || bar.Last > bar.First)
        {
            chip.BorderThickness = new Thickness(bar.ContinuesBefore ? 0 : 2, 0, 0, 0);
            chip.BorderBrush = showColors ? ColorOf(e) : Accent();
        }

        MakeOpenable(chip, e);
        return chip;
    }

    /// <summary>
    /// [S-572] Up to three dots, then a muted one for more — two and the muted one in a cell too
    /// narrow for four, so the row never runs past the cell.
    /// </summary>
    private StackPanel Dots(IReadOnlyList<DayItem> items, bool showColors, int limit)
    {
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };

        foreach (var item in items.Take(limit))
        {
            dots.Children.Add(Dot(MarkOf(item, showColors)));
        }

        if (items.Count > limit)
        {
            dots.Children.Add(Dot(ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray)));
        }

        return dots;
    }

    private static Ellipse Dot(Brush fill) => new() { Width = 5, Height = 5, Fill = fill };
}
