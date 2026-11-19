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
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2702] [S-572] The month: a grid of the family's tokens inside the card's inset ([S-6703]). The
/// weekday header in Caption tertiary, the days' numbers in Caption (primary, tertiary outside the
/// month, BodyStrong's weight where something is on), today's on the accent's round pill, six week
/// rows of the config's cells ([S-11544], <see cref="CalendarSizing.Month"/>), filling the content
/// ([S-12400]). A cell shows its items as dots (4, 4 apart) or, with titles ([S-11547],
/// <see cref="MonthPlan"/>), as the config's lines of chips: a span a bar on the item layer's chip fill
/// with its calendar's colour as a 2 px bar at its start, a meeting a line of its dot, start and title;
/// "+k more" on the number's line.
/// </summary>
internal sealed partial class CalendarView
{
    private void DrawMonth(double width, double height)
    {
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        // [S-11544] Six weeks of the config's cells at every size: the height was made for them.
        var weeks = MonthPlan.Weeks(_anchor, start);
        var innerWidth = Math.Max(0, width - (2 * FamilySpace.Inset));
        var fit = CalendarSizing.Month(_config.MonthEvents, innerWidth, _config.MonthLines);
        var columnWidth = innerWidth / 7;

        // [S-12400] [S-12807] The weeks reach from under the header to the foot's inset: the first week's cells
        // under the header, the last week's marks on the inset, never a band left under them nor on the card's
        // edge; a box taller than their cells spaces them evenly (each week's room over it). The sides keep the
        // inset as the day numbers' own.
        var room = height - fit.HeaderHeight - FamilySpace.Inset;
        var slots = ItemStretch.Reach(room, [.. Enumerable.Repeat(StretchItem.Fixed(fit.CellHeight), fit.Rows)], 0, 0, XamlRoot?.RasterizationScale ?? 1);

        var grid = new Grid { Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.HeaderHeight) });
        var over = new double[fit.Rows];
        for (var r = 0; r < fit.Rows; r++)
        {
            over[r] = Math.Max(0, slots[r].Start - (r > 0 ? slots[r - 1].End : 0));
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(over[r] + slots[r].Length) });
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

        var chipped = fit.Density == MonthDensity.Chips;
        var moreWords = MonthPlan.MoreWords(columnWidth);
        for (var r = 0; r < fit.Rows; r++)
        {
            var weekStart = weeks[fit.FirstRow + r];
            var week = Columns7();

            // [S-11547] The week's chips on the config's lines, each day's items and its "+k more".
            var plan = chipped ? MonthPlan.Week(_events, _birthdays, weekStart, zone, fit.ChipLines) : null;

            if (chipped)
            {
                // The number's line under the cell's padding, then one row per chip line, the rest empty.
                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CalendarSizing.MonthNumberLine) });
                for (var lane = 0; lane < fit.ChipLines; lane++)
                {
                    week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fit.ChipLine) });
                }

                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // [S-11547] A hairline over each week, as a month view parts its weeks.
                if (r > 0)
                {
                    week.BorderThickness = new Thickness(0, FamilyShape.Hairline, 0, 0);
                    week.BorderBrush = Family.Brush(ColourRole.Track);
                }
            }

            for (var c = 0; c < 7; c++)
            {
                var day = weekStart.AddDays(c);
                var items = chipped ? [] : CalendarRules.DayItems(_events, _birthdays, day, zone);
                var count = plan is not null ? plan.Items[c] : items.Count;
                var hidden = plan is not null ? plan.Hidden[c] : 0;

                // [S-3620] The selected day (also the keys' cursor day, [S-3624]) is marked in its cell.
                var cell = DayCell(day, items, count, hidden, day.Month == _anchor.Month, day == today, _selected == day, fit, showColors, dotLimit, moreWords);

                // [S-11923] The day under the pointer is the view's; the day pointed at elsewhere takes the hover fill.
                LinkDay(cell, day, rest: fit.Density == MonthDensity.Chips && _selected == day ? () => ItemFill.Paint(cell, ItemRole.Chip, ItemState.Selected) : null);

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
            foreach (var bar in plan?.Shown ?? [])
            {
                var chip = MonthChip(bar, showColors, fit.Times, zone, MonthPlan.Dimmed(bar, weekStart, _anchor));
                SetRow(chip, 1 + bar.Lane);
                SetColumn(chip, bar.First);
                SetColumnSpan(chip, bar.Last - bar.First + 1);
                week.Children.Add(chip);
            }

            ListReach.Shift(week, over[r]);
            SetRow(week, 1 + r);
            grid.Children.Add(week);
        }

        _content.Children.Add(grid);
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
        int dotLimit,
        bool moreWords)
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
            // [S-11547] The number row carries the overflow at its end ("+k more", "+k" in a narrow
            // column), so it never costs a chip line.
            var numberRow = new Grid
            {
                ColumnSpacing = FamilySpace.S1,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                Children = { numberHost },
            };

            if (hidden > 0)
            {
                var more = Family.Text(FamilyType.Caption, inMonth ? ColourRole.TextSecondary : ColourRole.TextTertiary, moreWords
                    ? CalendarStr.Calendar_MoreCount.Plural(hidden)
                    : LocaleFormat.Digits(Strings.Current, $"+{hidden.ToString(CultureInfo.InvariantCulture)}"));
                more.HorizontalAlignment = HorizontalAlignment.Right;
                more.TextTrimming = TextTrimming.CharacterEllipsis;
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
    /// [S-2703] [S-11547] A chip, one Caption line tall, 4 in from the day's edge at both ends. A span
    /// (all-day, several days, a birthday) is a bar: on the item layer's chip fill, the item's mark as a
    /// 2 px bar at its start (none where it continues from the week before), the one radius at the ends
    /// it has. A timed event is a line on no fill: its calendar's dot, its start where the column holds
    /// it, its title, as a month view lists a day's meetings.
    /// </summary>
    private Border MonthChip(LaneItem bar, bool showColors, bool times, TimeZoneInfo zone, bool dimmed = false)
    {
        var item = bar.Item;
        // [S-11547] A chip wholly on the days around the month is dimmed as their numbers are.
        var text = Family.Text(FamilyType.Caption, dimmed ? ColourRole.TextTertiary : ColourRole.TextPrimary);
        var lead = dimmed ? ColourRole.TextTertiary : ColourRole.TextSecondary;
        text.VerticalAlignment = VerticalAlignment.Center;

        if (MonthPlan.Look(bar, zone) == MonthChipLook.Timed)
        {
            var e = item.Event!;
            if (times)
            {
                text.Inlines.Add(Family.Run(FamilyType.Caption, lead, Clock(e.Start) + " "));
                text.Inlines.Add(new Run { Text = Title(e) });
            }
            else
            {
                text.Text = Title(e);
            }

            var dot = Dot(MarkOf(item, showColors));
            dot.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(text, 1);
            var line = new Border
            {
                Child = new Grid
                {
                    ColumnSpacing = FamilySpace.S1,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    },
                    Children = { dot, text },
                },
                Height = FamilyType.Caption.LineHeight,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
                Margin = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
                CornerRadius = Family.Radius,
                Background = Family.Brush(ColourRole.Clear),
            };
            MakeOpenable(line, e);
            return line;
        }

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

        // A timed span (an overnight train) leads with its start where it starts.
        var span = item.Event!;
        if (times && !span.AllDay && !bar.ContinuesBefore)
        {
            text.Inlines.Add(Family.Run(FamilyType.Caption, lead, Clock(span.Start) + " "));
            text.Inlines.Add(new Run { Text = Title(span) });
        }
        else
        {
            text.Text = Title(span);
        }

        MakeOpenable(chip, span);
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
