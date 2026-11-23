using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2702] [S-572] [S-12832] The month: its day cells tile the card ([S-12832], <see cref="MonthLayouts"/>): seven
/// columns from the card's left edge to its right, the weekday line in Caption tertiary over them, the weeks from under
/// it down, whole grid cells each. A cell is filled by its content: its day's events as chips under its number where
/// it has room for a chip line (a span a bar on the item layer's chip fill with its calendar's colour as a 2 px bar at
/// its start, a meeting a line of its dot, start and title; "+k more" on the number's line), its dots where it does not,
/// its number alone on a day of nothing; the number grows with the cell (primary, tertiary outside the month,
/// SemiBold where something is on, today's on the accent's round pill).
/// </summary>
internal sealed partial class CalendarView
{
    private void DrawMonth(double width, double height, double top)
    {
        var start = _config.EffectiveWeekStart;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        // [S-11544] Six weeks; the ones the card holds at whole cells, the anchor's among them.
        var weeks = MonthPlan.Weeks(_anchor, start);
        var focus = today.Year == _anchor.Year && today.Month == _anchor.Month ? today : _anchor;
        var anchorRow = Math.Max(0, weeks.ToList().FindLastIndex(w => w <= focus));
        var most = 0;
        foreach (var weekStart in weeks)
        {
            for (var c = 0; c < 7; c++)
            {
                most = Math.Max(most, CalendarRules.DayItems(Events, Birthdays, weekStart.AddDays(c), zone).Count);
            }
        }

        var layout = MonthLayouts.Plan(width, height + top, top, _area.Grid(XamlRoot?.RasterizationScale ?? 1), anchorRow, most, _config.MonthEvents == Themia.Core.Model.MonthEvents.Chips ? MonthChipRoom.Titles : MonthChipRoom.Dots);

        // [S-12832] The month stands <paramref name="top"/> under the card's top edge: the view starts the card's inset under
        // it, so the grid stands that much higher (or lower) than the view's top.
        var grid = new Grid { Margin = new Thickness(0, top - FamilySpace.ContentTop, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(layout.HeaderHeight) });

        // [S-14500] The weeks share the room under the weekday line evenly to the card's foot, each edge on a whole device
        // pixel laid here (WR-2: a grid's own rounding of its stars stood rows 0.7 over each other), nothing left under the last.
        var scale = XamlRoot?.RasterizationScale ?? 1;
        for (var r = 0; r < layout.Rows; r++)
        {
            var from = ItemSize.OnPixels(layout.HeaderHeight + (r * layout.RowHeight), scale);
            var to = ItemSize.OnPixels(layout.HeaderHeight + ((r + 1) * layout.RowHeight), scale);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Max(0, to - from)) });
        }

        // [S-564] The header follows the week start: Mo..Su or Su..Sa, as much as a column holds; [S-15795] with the status's
        // glyph in the last column's room, as much as that column holds beside it (no letter cut or pushed past it).
        var glyphRoom = layout.HeaderHeight >= KitStatus.Side ? _status.Room : 0;
        var weekdays = CalendarFit.Weekdays(layout.LeastColumn - (glyphRoom > 0 ? FamilySpace.S1 + glyphRoom : 0));
        if (weekdays == WeekdayLabel.None && CalendarFit.Weekdays(layout.LeastColumn) != WeekdayLabel.None)
        {
            weekdays = WeekdayLabel.Narrow;
        }
        var chipped = layout.Face == MonthFace.Chips;

        // [S-12813] The month stands on the card's edges: the status's glyph stands in its weekday line's band, at its end
        // on the cells' own padding, over nothing of a day's cell (a stale month's warning stood over the first week's last
        // number), and the last weekday's word leaves it room.
        var room = weekdays != WeekdayLabel.None && layout.HeaderHeight >= KitStatus.Side ? _status.Room : 0;
        _status.StandAt(weekdays != WeekdayLabel.None && layout.HeaderHeight >= KitStatus.Side
            ? top - FamilySpace.ContentTop + ((layout.HeaderHeight - KitStatus.Side) / 2)
            : null, room > 0 ? FamilySpace.S1 : null);

        if (weekdays != WeekdayLabel.None)
        {
            var order = CalendarRules.WeekdayOrder(start);
            var header = Columns(layout.Columns);
            for (var c = 0; c < 7; c++)
            {
                var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, WeekdayText(order[c], weekdays));
                label.TextTrimming = TextTrimming.None;
                label.HorizontalAlignment = chipped ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Bottom;
                var leaves = room > 0 && c == layout.Columns.Count - 1 ? FamilySpace.S1 + room : 0;
                var holder = new Border { Child = label, Padding = new Thickness(chipped ? FamilySpace.S1 : 0, 0, leaves, 0) };

                // [S-14314] The letters are their column's head: on the cells' own padding, in line with the numbers under them.
                HeadMark.Mark(holder);
                SetColumn(holder, c);
                header.Children.Add(holder);
            }

            grid.Children.Add(header);
        }

        // [S-16449] As many dots a line as stand whole in a cell's padding, the "more" one among them.
        var dotLimit = MonthLayouts.DotSlots(layout.LeastColumn);
        var moreWords = MonthPlan.MoreWords(layout.LeastColumn);
        var times = chipped && layout.LeastColumn >= CalendarFit.ChipTimeFrom;
        for (var r = 0; r < layout.Rows; r++)
        {
            var weekStart = weeks[layout.FirstRow + r];
            var week = Columns(layout.Columns);

            // [S-11547] The week's chips on the lanes the cells hold, each day's items and its "+k more".
            var plan = chipped ? MonthPlan.Week(Events, Birthdays, weekStart, zone, layout.ChipLanes) : null;
            if (chipped)
            {
                // The number's line under the cell's padding, then one row per chip lane, the rest of the cell.
                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilySpace.S1 + layout.NumberLine) });
                for (var lane = 0; lane < layout.ChipLanes; lane++)
                {
                    week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CalendarFit.ChipLine) });
                }

                week.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (var c = 0; c < 7; c++)
            {
                var day = weekStart.AddDays(c);
                var items = CalendarRules.DayItems(Events, Birthdays, day, zone);
                var count = plan is not null ? plan.Items[c] : items.Count;
                var hidden = plan is not null ? plan.Hidden[c] : 0;

                // [S-3620] The selected day (also the keys' cursor day, [S-3624]) is marked in its cell.
                var cell = DayCell(day, items, count, hidden, day.Month == _anchor.Month, day == today, _selected == day, layout, showColors, dotLimit, moreWords);

                // [S-11923] The day under the pointer is the view's; the day pointed at elsewhere takes the hover fill.
                LinkDay(cell, day, rest: chipped && _selected == day ? () => ItemFill.Paint(cell, ItemRole.Chip, ItemState.Selected) : null);

                // [S-2004] The click is the widget's own: it never reaches the card.
                cell.Tapped += (_, args) =>
                {
                    args.Handled = true;
                    ToggleDay(day);
                };
                WidgetItems.Mark(cell); // [S-814] a press on a day is the day's toggle

                if (items.Count > 0)
                {
                    ToolTipService.SetToolTip(cell, DayTooltip(day, items));
                }

                SetColumn(cell, c);
                SetRowSpan(cell, Math.Max(1, week.RowDefinitions.Count));
                week.Children.Add(cell);
            }

            // [S-2703] The row's chips over its cells: one bar per item, across the days it covers.
            foreach (var bar in plan?.Shown ?? [])
            {
                var chip = MonthChip(bar, showColors, times, zone, MonthPlan.Dimmed(bar, weekStart, _anchor));
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

    /// <summary>[S-12832] Seven columns of <paramref name="widths"/>: the month's, edge to edge.</summary>
    private static Grid Columns(IReadOnlyList<double> widths)
    {
        var grid = new Grid();
        foreach (var width in widths)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, width)) });
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
        MonthLayout layout,
        bool showColors,
        int dotLimit,
        bool moreWords)
    {
        var chipped = layout.Face == MonthFace.Chips;

        // [S-12832] The number grows with the cell: its line is the layout's, its size the Caption's share of it.
        var number = Family.Text(FamilyType.Caption, isToday ? ColourRole.TextOnAccent : inMonth ? ColourRole.TextPrimary : ColourRole.TextTertiary,
            LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture)));
        var line = layout.NumberLine;
        Family.Step(number, new TypeStep(Math.Round(line * FamilyType.Caption.FontSize / FamilyType.Caption.LineHeight, 1), line, false));
        number.TextTrimming = TextTrimming.None;

        // A day with something on it has its number in the strong weight: where there are no marks that is the one
        // sign left, and it is not colour alone.
        if (count > 0)
        {
            number.FontWeight = FontWeights.SemiBold;
        }

        FrameworkElement numberHost = number;
        if (isToday)
        {
            // [S-572] Today is the accent's round pill, as Windows' own calendar flyout marks it.
            number.HorizontalAlignment = HorizontalAlignment.Center;
            numberHost = TodayPill(number, line, layout.LeastColumn);

            // A chip's number row keeps its start on the cell's padding: only a centred pill reaches into it.
            if (chipped)
            {
                numberHost.Margin = default;
            }
        }

        numberHost.HorizontalAlignment = chipped ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        numberHost.VerticalAlignment = VerticalAlignment.Top;

        FrameworkElement face = numberHost;
        if (chipped)
        {
            // [S-11547] The number row carries the overflow at its end ("+k more", "+k" in a narrow column), so it
            // never costs a chip line.
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
                more.VerticalAlignment = VerticalAlignment.Center;
                more.TextTrimming = TextTrimming.CharacterEllipsis;
                SetColumn(more, 1);
                numberRow.Children.Add(more);
            }

            numberRow.VerticalAlignment = VerticalAlignment.Top;
            face = numberRow;
        }
        else if (layout.Face == MonthFace.Dots && items.Count > 0)
        {
            // [S-15794] The number in the middle of its line, its dots under it: the dots take nothing off the number.
            // [S-16442] They sink into the cell's foot padding only as far as the figures leave them no room, so the
            // padding stays under them (the last week's dots stood on the card's border).
            numberHost.VerticalAlignment = VerticalAlignment.Center;
            var dots = Dots(items, showColors, dotLimit);
            dots.VerticalAlignment = VerticalAlignment.Bottom;
            dots.Margin = new Thickness(0, 0, 0, -MonthLayouts.DotSink(layout.RowHeight, layout.NumberLine));
            face = new Grid { Children = { numberHost, dots } };
        }
        else
        {
            // A number alone takes the middle of its cell.
            numberHost.VerticalAlignment = VerticalAlignment.Center;
        }

        var cell = new Border
        {
            Child = face,
            Padding = chipped ? new Thickness(FamilySpace.S1, FamilySpace.S1, FamilySpace.S1, 0) : new Thickness(FamilySpace.S1),
            Background = Family.Brush(ColourRole.Clear),
        };

        // [S-12832] A day's cell is one of the month's items: it tiles the card with the others.
        ItemMark.Mark(cell);

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
    /// <summary>
    /// [S-572] Today's pill round its number. [S-16449] It may stand in the cell's side padding (a fill, never text), so in a
    /// narrow column (22.7 at 160 wide) it holds its two figures whole, never cut flat by the cell with "23" past it.
    /// </summary>
    private static Border TodayPill(TextBlock number, double line, double column = double.PositiveInfinity) => new()
    {
        Child = number,
        Background = Accent(),
        Height = line,
        MinWidth = Math.Min(line, column),
        MaxWidth = column,
        Margin = double.IsFinite(column) ? new Thickness(-FamilySpace.S1, 0, -FamilySpace.S1, 0) : default,
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
            LinkOther(chip);
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

        // [S-16449] Every item where they all stand in the slots; else one slot short, the "more" dot in the last.
        var take = items.Count <= limit ? limit : Math.Max(1, limit - 1);
        foreach (var item in items.Take(take))
        {
            // [S-15791] A dot is its event's mark: the event pointed at elsewhere keeps it whole, every other dot dims.
            var dot = Dot(MarkOf(item, showColors));
            if (item.Event is { } ev)
            {
                LinkEvent(dot, ev);
            }
            else
            {
                LinkOther(dot);
            }

            dots.Children.Add(dot);
        }

        if (items.Count > take)
        {
            dots.Children.Add(Dot(Family.Brush(ColourRole.TextTertiary)));
        }

        return dots;
    }

    /// <summary>[S-572] A day's dot: the track's 4, round.</summary>
    private static Ellipse Dot(Brush fill) => new() { Width = CalendarFit.Dot, Height = CalendarFit.Dot, Fill = fill };
}
