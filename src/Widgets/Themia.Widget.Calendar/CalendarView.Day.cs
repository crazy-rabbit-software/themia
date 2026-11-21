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
/// [S-573] / [S-2705] [S-11543] The day: its all-day and multi-day events and birthdays on one band line
/// (one bar, or chips shared among several) over a timeline of the config's hours around the next
/// event (or now), with overlapping events side by side. A day with no all-day item gives the band's
/// line to its hours.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>[S-2718] A span bar names its dates beside its title from this width.</summary>
    private const double SpanDatesFrom = 200;

    private void DrawDay(double width, double height)
    {
        var day = _anchor;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;
        var items = CalendarRules.DayItems(Events, Birthdays, day, zone);
        var allDay = items.Where(i => i.Birthday is not null || i.Event!.AllDay || CalendarFit.IsMultiDay(i.Event, zone)).ToList();
        var timed = items.Where(i => i.Event is { AllDay: false } e && !CalendarFit.IsMultiDay(e, zone)).Select(i => i.Event!).ToList();

        // [S-15671] The day is rows from the card's top edge (under a state's row, right under it): its band's row when it
        // has all-day items ([S-11543]: its own bar, or chips shared among several), then its hours, each an item at an
        // hour's one fixed size, every one the card reaches, the last cut by its foot (WR-4, WR-6).
        var lift = _stated ? 0 : FamilySpace.ContentTop;
        var band = CalendarSizing.DayBand(allDay.Count);
        var bandRow = CalendarSizing.DayBandRow(band, lift);
        var gutter = width >= HourLabelsLongFrom ? HourLabelWidth : HourLabelNarrowWidth;
        var timelineWidth = Math.Max(0, width - gutter - (2 * FamilySpace.Inset));
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        var hours = CalendarSizing.DayHours(height + lift - bandRow, cells);

        var root = new Grid { Margin = new Thickness(0, -lift, 0, 0), Background = Family.Brush(ColourRole.Clear) };

        // [S-11923] [S-11903] The day view is its one day: the pointer on it is that day, and that day pointed
        // at elsewhere lights it whole, as a card that is the match.
        LinkDay(root, day);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        if (band.Lines > 0)
        {
            // [S-15671] The band's row runs from edge to edge, its line in the inset and the hours' gutter, its padding.
            var row = new Border
            {
                Child = DayBand(allDay, day, timelineWidth, band, showColors),
                Height = bandRow,
                Padding = new Thickness(FamilySpace.Inset + gutter, lift, FamilySpace.Inset, 0),
                Background = Family.Brush(ColourRole.Clear),
            };
            ItemMark.Mark(row);
            root.Children.Add(row);
        }

        var (earliest, latest) = CalendarFit.EventHours(timed, day, zone);
        var window = CalendarFit.HoursOf(hours.Shown, hours.Pitch, earliest, latest, CalendarFit.FocusHour(timed, day, Today, _clock(), zone));

        // The hours' rows, the first one's rule left out where it would stand on the card's top edge.
        var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        for (var i = 0; i < window.Count; i++)
        {
            rows.Children.Add(HourRow(window.First + i, hours.Height(i), gutter, long_: gutter == HourLabelWidth, rule: i > 0 || bandRow > 0 || lift == 0));
        }

        SetRow(rows, 1);
        root.Children.Add(rows);

        // The events over the rows, on the timeline beside the hours' labels.
        var canvas = TimelineCanvas(timed, day, window, timelineWidth, showColors);
        canvas.Margin = new Thickness(FamilySpace.Inset + gutter, 0, FamilySpace.Inset, 0);
        canvas.VerticalAlignment = VerticalAlignment.Top;
        canvas.Height = hours.Start(window.Count);
        SetRow(canvas, 1);
        root.Children.Add(canvas);

        if (items.Count == 0)
        {
            var none = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
            none.Text = (day == Today ? CalendarStr.Calendar_NoEventsToday : CalendarStr.WidgetsA_Calendar_NoEvents).Text();
            none.HorizontalAlignment = HorizontalAlignment.Center;
            none.VerticalAlignment = VerticalAlignment.Center;
            none.Margin = new Thickness(FamilySpace.Inset + gutter, 0, FamilySpace.Inset, 0);
            SetRow(none, 1);
            root.Children.Add(none);
        }

        _content.Children.Add(root);
    }

    /// <summary>
    /// [S-15671] One hour of the day: a row of the hour's one fixed size from the card's left edge to its right (an item,
    /// WR-3), its label centred in it in the gutter on the row's inset, a hairline at its top across the timeline.
    /// </summary>
    private static Border HourRow(int hour, double height, double gutter, bool long_, bool rule)
    {
        var body = new Grid();
        if (rule)
        {
            body.Children.Add(new Border
            {
                Height = FamilyShape.Hairline,
                Margin = new Thickness(gutter, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Background = Family.Brush(ColourRole.Track),
            });
        }

        var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, LocaleFormat.Digits(Strings.Current, hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + (long_ ? ":00" : string.Empty)));
        label.TextTrimming = TextTrimming.None;
        label.VerticalAlignment = VerticalAlignment.Center;
        body.Children.Add(label);

        var row = new Border
        {
            Child = body,
            Height = height,
            Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0),
            Background = Family.Brush(ColourRole.Clear),
            IsHitTestVisible = false,
        };
        ItemMark.Mark(row);
        return row;
    }

    /// <summary>
    /// [S-2718] The band over the day's timeline: one full-width span bar per item on the lines the fit
    /// gave it; when items are left over, the last line shares its width among them ([S-2706]).
    /// </summary>
    private StackPanel DayBand(IReadOnlyList<DayItem> allDay, DateOnly day, double width, DayBandFit band, bool showColors)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, FamilySpace.S1) };

        var own = band.SharedLast ? band.Lines - 1 : band.Lines;
        for (var i = 0; i < own; i++)
        {
            stack.Children.Add(SpanBar(allDay[i], day, width, band.LineHeight, showColors));
        }

        if (band.SharedLast)
        {
            stack.Children.Add(SharedBandLine([.. allDay.Skip(own)], day, width, band.LineHeight, showColors));
        }

        return stack;
    }

    /// <summary>
    /// [S-2718] One item as a bar across the day: its colour along the edge, the title, and, where the
    /// bar is wide enough, the days it spans. One that began before the day or runs past it is
    /// squared off and marked with a chevron on that side, so it reads as part of a longer span.
    /// </summary>
    private Border SpanBar(DayItem item, DateOnly day, double width, double lineHeight, bool showColors)
    {
        var zone = TimeZoneInfo.Local;
        var before = item.Event is { } ev && CalendarRules.StartDay(ev, zone) < day;
        var after = item.Event is { } ev2 && CalendarFit.LastDay(ev2, zone) > day;
        var radius = FamilyShape.Radius;

        var body = new Grid { ColumnSpacing = FamilySpace.S1, VerticalAlignment = VerticalAlignment.Center };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Family.Text(FamilyType.Body);
        title.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(title, 1);
        body.Children.Add(title);

        if (before)
        {
            body.Children.Add(SpanChevron(ChevronLeft, 0));
        }

        if (after)
        {
            body.Children.Add(SpanChevron(ChevronRight, 3));
        }

        var bar = new Border
        {
            Child = body,
            Height = lineHeight - FamilySpace.S1,
            Margin = new Thickness(0, 0, 0, FamilySpace.S1),
            Padding = new Thickness(FamilySpace.S2, 0, FamilySpace.S2, 0),
            CornerRadius = new CornerRadius(before ? 0 : radius, after ? 0 : radius, after ? 0 : radius, before ? 0 : radius),
            BorderThickness = new Thickness(before ? 0 : FamilyShape.Stroke, 0, 0, 0),
            BorderBrush = MarkOf(item, showColors),
        };
        ItemFill.Paint(bar, ItemRole.Chip);

        if (item.Birthday is { } b)
        {
            title.Text = Str.WidgetsA_Calendar_BirthdayOf.Format(("name", BirthdayRules.Label(b, day)));
            ToolTipService.SetToolTip(bar, title.Text);
            return bar;
        }

        var e = item.Event!;
        title.Text = Title(e);

        // The days of a span, where the bar has room for them beside the title.
        if (width >= SpanDatesFrom && CalendarFit.IsMultiDay(e, zone))
        {
            var dates = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
            dates.Text = CalendarRules.Range(CalendarRules.StartDay(e, zone), CalendarFit.LastDay(e, zone), Strings.Current);
            dates.TextTrimming = TextTrimming.None;
            dates.VerticalAlignment = VerticalAlignment.Center;
            Typography.SetNumeralAlignment(dates, FontNumeralAlignment.Tabular);
            SetColumn(dates, 2);
            body.Children.Add(dates);
        }

        MakeOpenable(bar, e);
        return bar;
    }

    /// <summary>[S-2718] The chevron on the side a span runs on past the day.</summary>
    private static FontIcon SpanChevron(string glyph, int column)
    {
        var icon = UiLanguage.Mirrored(new FontIcon
        {
            Glyph = glyph,
            FontSize = FamilyShape.Icon,
            Foreground = Family.Brush(ColourRole.TextTertiary),
            VerticalAlignment = VerticalAlignment.Center,
        });
        SetColumn(icon, column);
        return icon;
    }

    /// <summary>
    /// [S-2706] The band's last line when items are left over: chips side by side sharing the width,
    /// each trimmed to its share; what does not fit at a readable width is "+N", listed on hover.
    /// </summary>
    private Grid SharedBandLine(IReadOnlyList<DayItem> allDay, DateOnly day, double width, double lineHeight, bool showColors)
    {
        const double MinChip = 56;
        const double MaxChip = 200;
        var fits = Math.Max(1, (int)Math.Floor((width + FamilySpace.S1) / (MinChip + FamilySpace.S1)));
        var shown = allDay.Count <= fits ? allDay.Count : Math.Max(1, fits - 1);

        var bar = new Grid { ColumnSpacing = FamilySpace.S1, Height = lineHeight };

        for (var i = 0; i < shown; i++)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = MaxChip });
            var chip = AllDayChip(allDay[i], day, lineHeight, showColors);
            SetColumn(chip, i);
            bar.Children.Add(chip);
        }

        if (shown < allDay.Count)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var more = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
            more.Text = LocaleFormat.Digits(Strings.Current, $"+{(allDay.Count - shown).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            more.VerticalAlignment = VerticalAlignment.Center;
            ToolTipService.SetToolTip(more, DayTooltip(day, allDay));
            SetColumn(more, shown);
            bar.Children.Add(more);
        }

        return bar;
    }

    private Border AllDayChip(DayItem item, DateOnly day, double lineHeight, bool showColors)
    {
        // A chip on a span bar's line reads at the bar's size; on the short fallback line, a caption.
        var text = Family.Text(lineHeight > CalendarFit.ChipLine ? FamilyType.Body : FamilyType.Caption);
        text.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border
        {
            Child = text,
            Height = lineHeight - FamilySpace.S1,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            CornerRadius = Family.Radius,
            BorderThickness = new Thickness(FamilyShape.Stroke, 0, 0, 0),
            BorderBrush = MarkOf(item, showColors),
        };
        ItemFill.Paint(chip, ItemRole.Chip);

        if (item.Birthday is { } b)
        {
            text.Text = Str.WidgetsA_Calendar_BirthdayOf.Format(("name", BirthdayRules.Label(b, day)));
            ToolTipService.SetToolTip(chip, text.Text);
            return chip;
        }

        var e = item.Event!;
        text.Text = Title(e);
        MakeOpenable(chip, e);
        return chip;
    }

    /// <summary>
    /// [S-573] A timed event on a timeline: title, then times; a block tall enough for a third line
    /// ([S-1266]) gives a long title its second line ([S-2717]), then where and who; one a line tall
    /// keeps the title only (<see cref="CalendarFit.BlockParts"/>).
    /// </summary>
    private Border TimedBlock(CalendarEvent e, bool showColors, double height, double width)
    {
        var line = FamilyType.Caption.LineHeight;
        var stack = new StackPanel();

        var title = Family.Text(FamilyType.Caption);
        title.Text = Title(e);
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);

        var meta = string.Join(" · ", new[] { e.Location, e.Organizer }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // The block's padding and coloured edge come off the title's line.
        var lines = LayoutMath.WholeLines(height, line);
        var parts = CalendarFit.BlockParts(lines, Overflows(title, width - (2 * FamilySpace.S1) - FamilyShape.Stroke), meta.Length > 0);

        if (parts.TitleLines > 1)
        {
            WrapTitle(title);
        }

        if (parts.Time)
        {
            var time = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
            time.Text = e.End > e.Start ? $"{Clock(e.Start)}–{Clock(e.End)}" : Clock(e.Start);
            Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            stack.Children.Add(time);
        }

        if (parts.Meta)
        {
            var where = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
            where.Text = meta;
            stack.Children.Add(where);
        }

        var block = new Border
        {
            Child = stack,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            CornerRadius = Family.Radius,
            BorderThickness = new Thickness(FamilyShape.Stroke, 0, 0, 0),
            BorderBrush = showColors ? ColorOf(e) : Accent(),
        };
        ItemFill.Paint(block, ItemRole.Chip);

        MakeOpenable(block, e);
        return block;
    }
}
