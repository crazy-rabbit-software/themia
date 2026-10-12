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
/// [S-561] / [S-2704] The week: its day columns (seven, or the widget's own count from the week's
/// first day or from today, [S-2719]) under their weekday and number, a band of bars for all-day and
/// multi-day events and birthdays ([S-2703]), then on a large tile an hour grid, on a smaller one
/// each day's timed events as a list (dots in a narrow column).
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>The hour labels' column beside a week or day timeline: "07:00", or "07" when narrow.</summary>
    private const double HourLabelWidth = 44;

    private const double HourLabelNarrowWidth = 24;

    /// <summary>A day timeline narrower than this writes its hours as "07".</summary>
    private const double HourLabelsLongFrom = 160;

    /// <summary>A bar in a column too narrow for words: a coloured line.</summary>
    private const double ThinBar = 6;

    private void DrawWeek(double width, double height)
    {
        var (start, last) = Range();
        var days = last.DayNumber - start.DayNumber + 1;
        var today = Today;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;

        var inner = new WidgetArea(width, height).Deflate(Fluent.Space1, 0);
        var fit = CalendarFit.Week(inner.Width, inner.Height, days);
        var gutter = fit.Timeline ? HourLabelWidth : 0;
        var columnWidth = Math.Max(0, (inner.Width - gutter) / days);

        // The height ladder: the day numbers always, then the weekday line, then the bars, then the body.
        var room = new Disclosure(inner.Width, Math.Max(0, inner.Height - Fluent.Space1));
        room.Reserve(FluentType.BodyStrong.LineHeight + Fluent.Space1);
        var weekdays = room.Fits(FluentType.Caption.LineHeight + (2 * FluentType.Caption.LineHeight)) && room.Take(FluentType.Caption.LineHeight);

        var bars = CalendarFit.Bars(_events, _birthdays, start, days, zone, singleTimed: false);
        var barLine = fit.Dots ? ThinBar + 2 : CalendarFit.ChipLine;
        var wantedLines = Math.Min(CalendarFit.LaneCount(bars), fit.Timeline ? 3 : 2);

        // Keep a line of the body for the timed events: the band never takes the whole column.
        var bandLines = 0;
        while (bandLines < wantedLines && room.Fits(((bandLines + 1) * barLine) + FluentType.Caption.LineHeight))
        {
            bandLines++;
        }

        room.Reserve(bandLines * barLine);
        var bodyHeight = room.Remaining;

        var grid = new Grid { Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, Fluent.Space1) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bandLines * barLine) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        if (fit.Timeline)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gutter) });
        }

        for (var c = 0; c < days; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var first = fit.Timeline ? 1 : 0;

        // Each day's timed events of one day only; the rest are bars.
        var timedByDay = new List<CalendarEvent>[days];
        for (var c = 0; c < days; c++)
        {
            var day = start.AddDays(c);
            timedByDay[c] = [.. CalendarRules.DayItems(_events, [], day, zone)
                .Select(i => i.Event!)
                .Where(e => !e.AllDay && !CalendarFit.IsMultiDay(e, zone))];

            var header = WeekHeader(day, day == today, weekdays ? fit.Weekdays : WeekdayLabel.None);
            var items = CalendarRules.DayItems(_events, _birthdays, day, zone);
            if (items.Count > 0)
            {
                ToolTipService.SetToolTip(header, DayTooltip(day, items));
            }

            // [S-3620] The selected day's head is underlined; a click on a head selects its day.
            if (_selected == day)
            {
                header.BorderThickness = new Thickness(0, 0, 0, 2);
                header.BorderBrush = Accent();
            }

            header.Tapped += (_, _) => ToggleDay(day);


            SetColumn(header, first + c);
            grid.Children.Add(header);
        }

        // [S-2703] The band: one bar per all-day or multi-day event and birthday, across its days.
        foreach (var bar in bars.Where(b => b.Lane < bandLines))
        {
            var chip = fit.Dots ? ThinBarOf(bar, showColors) : MonthChip(bar, showColors, CalendarFit.ChipLine, times: false);
            chip.VerticalAlignment = VerticalAlignment.Top;
            chip.Margin = new Thickness(1, bar.Lane * barLine, 1, 0);
            SetRow(chip, 1);
            SetColumn(chip, first + bar.First);
            SetColumnSpan(chip, bar.Last - bar.First + 1);
            grid.Children.Add(chip);
        }

        if (fit.Timeline)
        {
            DrawWeekTimeline(grid, start, timedByDay, bars, bandLines, bodyHeight, columnWidth, showColors);
        }
        else
        {
            for (var c = 0; c < days; c++)
            {
                var hiddenBars = CalendarFit.Hidden(bars, c, bandLines);
                var column = fit.Dots
                    ? WeekDots(timedByDay[c], hiddenBars, bodyHeight, showColors)
                    : WeekList(timedByDay[c], hiddenBars, bodyHeight, columnWidth, showColors, fit.Detail);
                SetRow(column, 2);
                SetColumn(column, c);
                grid.Children.Add(column);
            }
        }

        _content.Children.Add(grid);
    }

    /// <summary>A column's head: the weekday (as much as fits) over the day number, today in the accent disc.</summary>
    private static Border WeekHeader(DateOnly day, bool isToday, WeekdayLabel label)
    {
        var header = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center };

        if (label != WeekdayLabel.None)
        {
            var weekday = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            weekday.Text = WeekdayText(day.DayOfWeek, label);
            weekday.TextTrimming = TextTrimming.None;
            weekday.HorizontalAlignment = HorizontalAlignment.Center;
            header.Children.Add(weekday);
        }

        var number = Fluent.Text(FluentType.BodyStrong, isToday ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");
        number.Text = LocaleFormat.Digits(Strings.Current, day.Day.ToString(CultureInfo.InvariantCulture));
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.TextTrimming = TextTrimming.None;
        Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
        var line = FluentType.BodyStrong.LineHeight;
        header.Children.Add(isToday
            ? new Border { Child = number, Background = Accent(), CornerRadius = new CornerRadius(line / 2), MinWidth = line, Height = line, Padding = new Thickness(4, 0, 4, 0) }
            : number);

        return new Border { Child = header, Padding = new Thickness(0, 0, 0, Fluent.Space1), Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent) };
    }

    /// <summary>A bar with no room for words: its calendar's colour as a rounded line, the title on hover.</summary>
    private Border ThinBarOf(LaneItem bar, bool showColors)
    {
        var line = new Border
        {
            Height = ThinBar,
            CornerRadius = new CornerRadius(ThinBar / 2),
            Background = MarkOf(bar.Item, showColors),
        };

        if (bar.Item.Event is { } e)
        {
            MakeOpenable(line, e);
        }
        else if (bar.Item.Birthday is { } b)
        {
            ToolTipService.SetToolTip(line, Str.WidgetsA_Calendar_BirthdayOf.Format(("name", b.Name)));
        }

        return line;
    }

    /// <summary>
    /// [S-2704] A narrow column: one dot per timed event, stacked, as many as fit, then a muted dot
    /// for the rest (and for bars the band had no line for).
    /// </summary>
    private StackPanel WeekDots(IReadOnlyList<CalendarEvent> timed, int hiddenBars, double height, bool showColors)
    {
        var stack = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        var fits = Disclosure.WholeRows(height - 2, 5, 3);
        var more = timed.Count + hiddenBars > fits;
        var shown = Math.Max(0, more ? fits - 1 : fits);

        foreach (var e in timed.Take(shown))
        {
            var dot = Dot(showColors ? ColorOf(e) : Accent());
            ToolTipService.SetToolTip(dot, Describe(e));
            stack.Children.Add(dot);
        }

        if (more && fits > 0)
        {
            stack.Children.Add(Dot(FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray)));
        }

        return stack;
    }

    /// <summary>
    /// [S-2704] A column's timed events as chips: the start time over the title from 64 px columns
    /// (start–end and the place under it from 100 px, [S-1265]), the title alone in a narrower one;
    /// as many as fit whole, then "+N". [S-2717] The room left under them gives long titles a second line.
    /// </summary>
    private StackPanel WeekList(IReadOnlyList<CalendarEvent> timed, int hiddenBars, double height, double columnWidth, bool showColors, bool detail)
    {
        var column = new StackPanel { Spacing = 2, Margin = new Thickness(1, 0, 1, 0) };
        var stacked = columnWidth >= CalendarFit.WeekdayNamesFrom;
        var line = FluentType.Caption.LineHeight;
        var room = new Disclosure(columnWidth, height, gap: 2);
        var shown = 0;
        var titles = new List<TextBlock>();

        for (var i = 0; i < timed.Count; i++)
        {
            var e = timed[i];
            var lines = stacked ? 2 + (detail && e.Location is not null ? 1 : 0) : 1;
            var cost = (lines * line) + 2;

            // Keep a line for "+N" when something will not fit.
            var after = timed.Count - i - 1 + hiddenBars;
            if (!room.Fits(cost + (after > 0 ? line + 2 : 0)) || !room.Take(cost))
            {
                break;
            }

            var (chip, title) = WeekChip(e, showColors, stacked, detail);
            column.Children.Add(chip);
            titles.Add(title);
            shown++;
        }

        var rest = timed.Count - shown + hiddenBars;

        // The chip's edge and padding and the column's margins come off the title's line.
        var titleWidth = columnWidth - 9;
        var leftover = room.Remaining - (rest > 0 ? line + 2 : 0);
        var second = CalendarFit.SecondLines([.. titles.Select(t => Overflows(t, titleWidth))], leftover, line);
        for (var k = 0; k < titles.Count; k++)
        {
            if (second[k])
            {
                WrapTitle(titles[k]);
            }
        }
        if (rest > 0)
        {
            var more = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            more.Text = LocaleFormat.Digits(Strings.Current, $"+{rest.ToString(CultureInfo.InvariantCulture)}");
            more.HorizontalAlignment = HorizontalAlignment.Center;
            column.Children.Add(more);
        }

        return column;
    }

    /// <summary>
    /// [S-561] A timed chip: its start time above the title so the title keeps the column's width; a
    /// wide column ([S-1265]) shows the end time too and puts the place under the title.
    /// </summary>
    private (Border Chip, TextBlock Title) WeekChip(CalendarEvent e, bool showColors, bool stacked, bool detail)
    {
        var stack = new StackPanel { Spacing = 0 };
        var chip = new Border
        {
            Child = stack,
            Padding = new Thickness(3, 1, 2, 1),
            CornerRadius = new CornerRadius(2),
            Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = showColors ? ColorOf(e) : Accent(),
        };

        if (stacked)
        {
            var time = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            time.Text = detail && e.End > e.Start ? $"{Clock(e.Start)}–{Clock(e.End)}" : Clock(e.Start);
            Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            stack.Children.Add(time);
        }

        var title = Fluent.Text(FluentType.Caption);
        title.Text = Title(e);
        stack.Children.Add(title);

        if (detail && e.Location is { } place)
        {
            var where = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            where.Text = place;
            stack.Children.Add(where);
        }

        MakeOpenable(chip, e);
        return (chip, title);
    }

    /// <summary>
    /// [S-2704] / [S-2705] The large week: an hour grid of the hours that fit, the week's timed events
    /// placed on it in side-by-side lanes where they overlap ([S-573]).
    /// </summary>
    private void DrawWeekTimeline(Grid grid, DateOnly start, List<CalendarEvent>[] timedByDay, IReadOnlyList<LaneItem> bars, int bandLines, double height, double columnWidth, bool showColors)
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
        var focus = CalendarFit.FocusHour(all.Where(e => CalendarRules.StartDay(e, zone) >= focusDay), focusDay, today, DateTimeOffset.Now, zone);
        var window = CalendarFit.Hours(height, earliest, latest, focus);

        var labels = HourLabels(window, long_: true);
        SetRow(labels, 2);
        SetColumn(labels, 0);
        grid.Children.Add(labels);

        var rules = HourRules(window);
        SetRow(rules, 2);
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
                ToolTipService.SetToolTip(canvas, DayTooltip(day, CalendarRules.DayItems(_events, _birthdays, day, zone)));
            }

            SetRow(canvas, 2);
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
            var label = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            label.Text = LocaleFormat.Digits(Strings.Current, h.ToString("00", CultureInfo.InvariantCulture) + (long_ ? ":00" : string.Empty));
            label.TextTrimming = TextTrimming.None;
            Canvas.SetLeft(label, Fluent.Space1);
            Canvas.SetTop(label, ((h - window.First) * window.HourHeight) + 1);
            canvas.Children.Add(label);
        }

        return canvas;
    }

    /// <summary>A hairline at the top of every hour, across the timeline.</summary>
    private static Grid HourRules(HourWindow window)
    {
        var rules = new Grid { IsHitTestVisible = false };
        for (var h = 0; h < window.Count; h++)
        {
            rules.Children.Add(new Border
            {
                Height = Fluent.Hairline,
                Background = FluentLayout.Themed("DividerStrokeColorDefaultBrush", Colors.Gray),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, h * window.HourHeight, 0, 0),
            });
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
        var canvas = new Canvas { Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent) };
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
            var blockHeight = Math.Max(FluentType.Caption.LineHeight, (span * timelineHeight) - 1);
            var block = TimedBlock(e, showColors, blockHeight, Math.Max(0, (expectedWidth / Math.Max(1, lanes[i].Columns)) - 2));
            block.Height = blockHeight;
            Canvas.SetTop(block, top * timelineHeight);
            canvas.Children.Add(block);
            blocks.Add((block, lanes[i].Column, lanes[i].Columns));
        }

        // [S-2705] Today's timeline marks the time now with an accent rule.
        if (day == Today)
        {
            var now = DateTimeOffset.Now;
            if (CalendarFit.Place(now, now, day, zone, window) is { } at)
            {
                var rule = new Border { Height = 2, Background = Accent(), IsHitTestVisible = false };
                Canvas.SetTop(rule, (at.Top * timelineHeight) - 1);
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

                block.Width = Math.Max(0, (width / columns) - 2);
                Canvas.SetLeft(block, column * (width / columns));
            }
        };

        return canvas;
    }
}
