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
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-561] / [S-2704] [S-11545] [S-11546] The week: its day columns (seven, or the widget's own count from
/// the week's first day or from today, [S-2719]) under their weekday and number. As the config says:
/// the day strip, each column's marks and the chosen day's events as the agenda's rows under it; or,
/// with titles in the week, a band of bars for all-day and multi-day events and birthdays ([S-2703])
/// over an hour grid of the config's hours.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>The hour labels' column beside a week or day timeline: "07:00", or "07" when narrow.</summary>
    private const double HourLabelWidth = CalendarSizing.HourLabels;

    private const double HourLabelNarrowWidth = 24;

    /// <summary>A day timeline narrower than this writes its hours as "07".</summary>
    private const double HourLabelsLongFrom = 160;

    private void DrawWeek(double width, double height)
    {
        var (start, last) = Range();
        var days = last.DayNumber - start.DayNumber + 1;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        var inner = new WidgetArea(width, height).Deflate(FamilySpace.Inset, 0);
        if (!_config.WeekTitles)
        {
            DrawWeekMarks(width, height, start, days);
            return;
        }

        HideAgenda();

        // [S-11546] Titles in the columns: the heads, the band's one line, the config's hours under it.
        var gutter = HourLabelWidth;
        var columnWidth = Math.Max(0, (inner.Width - gutter) / days);
        var labels = CalendarFit.Weekdays(columnWidth);
        var bars = CalendarFit.Bars(Events, Birthdays, start, days, zone, singleTimed: false);
        var barLine = CalendarFit.ChipLine;
        var bandLines = Math.Min(CalendarFit.LaneCount(bars), 1);
        var bodyHeight = Math.Max(0, inner.Height - FamilySpace.Inset - CalendarSizing.WeekHead - (bandLines * barLine));

        // The heads, one row per band line (a bar sits in its lane's row: no margin of its own), the body.
        var grid = new Grid { Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var lane = 0; lane < bandLines; lane++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(barLine) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var body = 1 + bandLines;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gutter) });

        for (var c = 0; c < days; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        const int first = 1;

        // Each day's timed events of one day only; the rest are bars.
        var timedByDay = new List<CalendarEvent>[days];
        for (var c = 0; c < days; c++)
        {
            var day = start.AddDays(c);
            timedByDay[c] = [.. CalendarRules.DayItems(Events, [], day, zone)
                .Select(i => i.Event!)
                .Where(e => !e.AllDay && !CalendarFit.IsMultiDay(e, zone))];

            var header = WeekHeader(day, day == today, labels == WeekdayLabel.None ? WeekdayLabel.Narrow : labels);

            // [S-3620] The selected day's head is underlined; a click on a head selects its day.
            if (_selected == day)
            {
                header.BorderThickness = new Thickness(0, 0, 0, FamilyShape.Stroke);
                header.BorderBrush = Accent();
            }

            ChoosesDay(header, day, CalendarRules.DayItems(Events, Birthdays, day, zone));
            LinkDay(header, day); // [S-11923]

            SetColumn(header, first + c);
            grid.Children.Add(header);
        }

        // [S-2703] The band: one bar per all-day or multi-day event and birthday, across its days.
        foreach (var bar in bars.Where(b => b.Lane < bandLines))
        {
            var chip = MonthChip(bar, showColors, times: false, zone);
            chip.VerticalAlignment = VerticalAlignment.Top;
            SetRow(chip, 1 + bar.Lane);
            SetColumn(chip, first + bar.First);
            SetColumnSpan(chip, bar.Last - bar.First + 1);
            grid.Children.Add(chip);
        }

        DrawWeekTimeline(grid, body, start, timedByDay, bars, bandLines, bodyHeight, columnWidth, showColors);

        _content.Children.Add(grid);
    }

    /// <summary>
    /// [S-2281] [S-11545] The day strip: each column's head over its marks ([S-2282]), the chosen day's
    /// head on the selection fill ([S-2284]), and under the strip the chosen day's events as the agenda's
    /// rows ([S-2285]), the config's rows of them.
    /// </summary>
    private void DrawWeekMarks(double width, double height, DateOnly start, int days)
    {
        var zone = TimeZoneInfo.Local;
        var today = Today;
        var showColors = ShowColors;
        var chosen = CalendarFit.ChosenDay(_selected, start, days, today);
        var byDay = new IReadOnlyList<DayItem>[days];
        for (var c = 0; c < days; c++)
        {
            byDay[c] = CalendarRules.DayItems(Events, Birthdays, start.AddDays(c), zone);
        }

        // [S-11545] [S-12815] The strip is a row of day items on the card's top edge (under a state's row, right under it):
        // the days share the card's width from its left edge to its right (WR-3), touching (WR-2), each its weekday and
        // number at the card's inset over its marks; the band ends on a grid line, the chosen day's rows right under it
        // (WR-1: nothing between the strip and the first row).
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        var lift = _stated ? 0 : FamilySpace.ContentTop;
        var band = CalendarSizing.StripBand(cells);

        // [S-11545] [S-14502] [S-14810] The chosen day's rows under the strip at their one size, as many as the card reaches,
        // the last cut by its foot (its WeekRows a new card's).
        var under = Math.Max(0, height + lift - band);
        var rowsArea = _area.IsMeasured
            ? new WidgetArea(width, Math.Max(0, under - FamilySpace.Inset), _area.Cell, XamlRoot?.RasterizationScale ?? 1)
            : new WidgetArea(width, CalendarSizing.ClampWeekRows(_config.WeekRows) * FamilyShape.Row1);
        var columns = ItemSize.Columns(width, days, cells);
        var labels = CalendarFit.Weekdays(Math.Max(0, width) / Math.Max(1, days));
        var plan = KitPlanner.Plan(rowsArea, AgendaSpec.Spec, new KitChoices(KitShow.None) { Rows = new KitRowsChoice(CalendarSizing.ClampWeekRows(_config.WeekRows), 1) });

        // The strip, then the empty day's sentence: rows of the content, no margins.
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var grid = new Grid { Margin = new Thickness(0, -lift, 0, 0), Height = band, VerticalAlignment = VerticalAlignment.Top };
        for (var c = 0; c < days; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columns[c]) });

            var day = start.AddDays(c);
            var column = new StackPanel { Padding = new Thickness(0, FamilySpace.Inset, 0, 0) };
            var header = WeekHeader(day, day == today, labels == WeekdayLabel.None ? WeekdayLabel.Narrow : labels);
            header.CornerRadius = Family.Radius;
            if (day == chosen)
            {
                ItemFill.Paint(header, ItemRole.Chip, ItemState.Selected);
            }

            ChoosesDay(header, day, byDay[c]);

            // [S-11923] The chosen day's head keeps its selected fill when another day is pointed at.
            LinkDay(header, day, rest: day == chosen ? () => ItemFill.Paint(header, ItemRole.Chip, ItemState.Selected) : null);
            column.Children.Add(header);

            if (byDay[c].Count > 0)
            {
                var marks = WeekMarks(byDay[c], showColors);
                ChoosesDay(marks, day, byDay[c]);
                PublishDay(marks, day);
                column.Children.Add(marks);
            }

            // [S-12831] Each day is one of the strip's items: its column's whole band.
            var item = new Border { Child = column, Background = Family.Brush(ColourRole.Clear) };
            ItemMark.Mark(item);
            SetColumn(item, c);
            grid.Children.Add(item);
        }

        _content.Children.Add(grid);

        // The rows' room over them in the rows' host (the view's second row; the list stands the inset over its top).
        var top = band + FamilySpace.ContentTop - lift;
        var items = byDay[chosen.DayNumber - start.DayNumber];
        if (items.Count == 0)
        {
            HideAgenda();

            // [S-2285] A day with nothing says so in the Status block's row where the first row would be.
            var none = new StatusBlock { Margin = default };
            SetRow(none, 1);
            none.Show(StatusKind.Empty, Themia.App.Shell.Glyphs.Calendar, (chosen == today ? CalendarStr.Calendar_NoEventsToday : CalendarStr.WidgetsA_Calendar_NoEvents).Text());
            _content.Children.Add(none);
            return;
        }

        var now = _clock();
        Func<CalendarEvent, string>? mark = showColors ? MarkOf : null;
        var rows = new List<KitItem>(items.Count);
        _agendaItems.Clear();
        foreach (var item in items)
        {
            var row = AgendaSpec.DayRow(item, chosen, now, mark);
            _agendaItems[row.Id] = item;
            rows.Add(row);
        }

        _rowsTop.Height = top;
        _agenda.Visibility = Visibility.Visible;
        _agenda.Page = 0;
        _agenda.Show(rows, plan, rowsArea, now);
    }

    /// <summary>[S-2284] A click on a day's head or marks chooses it (the selected day, [S-3620]); its hover lists the day.</summary>
    private void ChoosesDay(FrameworkElement element, DateOnly day, IReadOnlyList<DayItem> items)
    {
        if (items.Count > 0)
        {
            ToolTipService.SetToolTip(element, DayTooltip(day, items));
        }

        // [S-2004] The click is the widget's own: it never reaches the card.
        element.Tapped += (_, args) =>
        {
            args.Handled = true;
            ToggleDay(day);
        };
        WidgetItems.Mark(element); // [S-814]
    }

    /// <summary>
    /// [S-2282] A day's marks: a short round bar per item in its calendar's colour (a birthday's in the
    /// tertiary role), the column's width less the family's gap either side, at most three, then "+n".
    /// </summary>
    private StackPanel WeekMarks(IReadOnlyList<DayItem> items, bool showColors)
    {
        var stack = new StackPanel { Spacing = FamilySpace.S1, Background = Family.Brush(ColourRole.Clear) };
        var (shown, more) = CalendarFit.Marks(items.Count);
        foreach (var item in items.Take(shown))
        {
            stack.Children.Add(new Border
            {
                Height = FamilyShape.Track,
                CornerRadius = Family.Round(FamilyShape.Track, FamilyShape.Track),
                Margin = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
                Background = MarkOf(item, showColors),
            });
        }

        if (more > 0)
        {
            var rest = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, LocaleFormat.Digits(Strings.Current, $"+{more.ToString(CultureInfo.InvariantCulture)}"));
            rest.TextTrimming = TextTrimming.None;
            rest.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(rest);
        }

        return stack;
    }

    /// <summary>A column's head: the weekday (as much as fits) over the day number, today in the accent disc.</summary>
    private static Border WeekHeader(DateOnly day, bool isToday, WeekdayLabel label)
    {
        var header = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        if (label != WeekdayLabel.None)
        {
            var weekday = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, WeekdayText(day.DayOfWeek, label));
            weekday.TextTrimming = TextTrimming.None;
            weekday.HorizontalAlignment = HorizontalAlignment.Center;
            header.Children.Add(weekday);
        }

        var number = Family.Text(FamilyType.BodyStrong, isToday ? ColourRole.TextOnAccent : ColourRole.TextPrimary, LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture)));
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.TextTrimming = TextTrimming.None;
        FrameworkElement face = number;
        if (isToday)
        {
            face = TodayPill(number, FamilyType.BodyStrong.LineHeight);
            face.HorizontalAlignment = HorizontalAlignment.Center;
        }

        header.Children.Add(face);
        return new Border { Child = header, Padding = new Thickness(0, 0, 0, FamilySpace.S1), Background = Family.Brush(ColourRole.Clear) };
    }

    /// <summary>
    /// [S-2704] / [S-2705] [S-11546] The week with titles: an hour grid of the config's hours, the week's timed events
    /// placed on it in side-by-side lanes where they overlap ([S-573]).
    /// </summary>
    private void DrawWeekTimeline(Grid grid, int body, DateOnly start, List<CalendarEvent>[] timedByDay, IReadOnlyList<LaneItem> bars, int bandLines, double height, double columnWidth, bool showColors)
    {
        var zone = TimeZoneInfo.Local;
        var today = Today;
        var days = timedByDay.Length;
        var all = timedByDay.SelectMany(d => d).ToList();
        var earliest = 24;
        var latest = 0;
        for (var c = 0; c < days; c++)
        {
            var (e, l) = CalendarFit.EventHours(timedByDay[c], start.AddDays(c), zone);
            earliest = Math.Min(earliest, e);
            latest = Math.Max(latest, l);
        }

        var focusDay = today >= start && today < start.AddDays(days) ? today : start;
        var focus = CalendarFit.FocusHour(all.Where(e => CalendarRules.StartDay(e, zone) >= focusDay), focusDay, today, _clock(), zone);
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        var window = CalendarFit.HoursOf(CalendarSizing.HoursIn(height, cells), ItemSize.Optimal(CalendarSizing.Hour, cells), earliest, latest, focus);

        var labels = HourLabels(window, long_: true);
        SetRow(labels, body);
        SetColumn(labels, 0);
        grid.Children.Add(labels);

        var rules = HourRules(window);
        SetRow(rules, body);
        SetColumn(rules, 1);
        SetColumnSpan(rules, days);
        grid.Children.Add(rules);

        for (var c = 0; c < days; c++)
        {
            var day = start.AddDays(c);
            var canvas = TimelineCanvas(timedByDay[c], day, window, columnWidth, showColors);
            var hidden = CalendarFit.Hidden(bars, c, bandLines) + timedByDay[c].Count(e => CalendarFit.Place(e.Start, e.End, day, zone, window) is null);
            if (hidden > 0)
            {
                ToolTipService.SetToolTip(canvas, DayTooltip(day, CalendarRules.DayItems(Events, Birthdays, day, zone)));
            }

            SetRow(canvas, body);
            SetColumn(canvas, 1 + c);
            grid.Children.Add(canvas);
        }
    }

    /// <summary>The hours' labels down a timeline's gutter.</summary>
    private static Canvas HourLabels(HourWindow window, bool long_)
    {
        var canvas = new Canvas { IsHitTestVisible = false };
        for (var h = window.First; h < window.End; h++)
        {
            var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, LocaleFormat.Digits(Strings.Current, h.ToString("00", CultureInfo.InvariantCulture) + (long_ ? ":00" : string.Empty)));
            label.TextTrimming = TextTrimming.None;
            Canvas.SetTop(label, (h - window.First) * window.HourHeight);
            canvas.Children.Add(label);
        }

        return canvas;
    }

    /// <summary>A hairline at the top of every hour, across the timeline.</summary>
    private static Canvas HourRules(HourWindow window)
    {
        var rules = new Canvas { IsHitTestVisible = false };
        rules.SizeChanged += (_, args) =>
        {
            foreach (var rule in rules.Children.OfType<Border>())
            {
                rule.Width = args.NewSize.Width;
            }
        };

        for (var h = 0; h < window.Count; h++)
        {
            var rule = new Border { Height = FamilyShape.Hairline, Background = Family.Brush(ColourRole.Track) };
            Canvas.SetTop(rule, h * window.HourHeight);
            rules.Children.Add(rule);
        }

        return rules;
    }

    /// <summary>
    /// One day's timed events on a timeline of <paramref name="window"/>'s hours, overlapping ones in
    /// side-by-side lanes ([S-573]); the width is the column's, so the blocks are sized on layout.
    /// <paramref name="expectedWidth"/> is that width as the layout will give it, for what a block
    /// has room to say ([S-2717]).
    /// </summary>
    private Canvas TimelineCanvas(IReadOnlyList<CalendarEvent> timed, DateOnly day, HourWindow window, double expectedWidth, bool showColors)
    {
        var zone = TimeZoneInfo.Local;
        var canvas = new Canvas { Background = Family.Brush(ColourRole.Clear) };
        var timelineHeight = window.Count * window.HourHeight;
        var placed = timed
            .Select(e => (Event: e, Place: CalendarFit.Place(e.Start, e.End, day, zone, window)))
            .Where(p => p.Place is not null)
            .ToList();
        var lanes = CalendarRules.Lanes([.. placed.Select(p => (p.Event.Start.UtcTicks, Math.Max(p.Event.End.UtcTicks, p.Event.Start.UtcTicks)))]);
        var blocks = new List<(Border Block, int Column, int Columns)>();

        for (var i = 0; i < placed.Count; i++)
        {
            var (e, place) = placed[i];
            var (top, span) = place!.Value;
            var blockHeight = Math.Max(FamilyType.Caption.LineHeight, (span * timelineHeight) - FamilyShape.Hairline);
            var block = TimedBlock(e, showColors, blockHeight, Math.Max(0, (expectedWidth / Math.Max(1, lanes[i].Columns)) - FamilyShape.Stroke));
            block.Height = blockHeight;
            Canvas.SetTop(block, top * timelineHeight);
            canvas.Children.Add(block);
            blocks.Add((block, lanes[i].Column, lanes[i].Columns));
        }

        // [S-2705] Today's timeline marks the time now with an accent rule.
        if (day == Today)
        {
            var now = _clock();
            if (CalendarFit.Place(now, now, day, zone, window) is { } at)
            {
                var rule = new Border { Height = FamilyShape.Stroke, Background = Accent(), IsHitTestVisible = false };
                Canvas.SetTop(rule, (at.Top * timelineHeight) - (FamilyShape.Stroke / 2));
                canvas.Children.Add(rule);
                blocks.Add((rule, 0, 0));
            }
        }

        canvas.SizeChanged += (_, args) =>
        {
            var width = args.NewSize.Width;
            foreach (var (block, column, columns) in blocks)
            {
                if (columns == 0)
                {
                    block.Width = width;
                    continue;
                }

                block.Width = Math.Max(0, (width / columns) - FamilyShape.Stroke);
                Canvas.SetLeft(block, column * (width / columns));
            }
        };

        return canvas;
    }
}
