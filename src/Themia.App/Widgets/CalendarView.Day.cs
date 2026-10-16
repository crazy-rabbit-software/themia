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
/// [S-573] / [S-2705] The day: its all-day and multi-day events and birthdays as span bars over a
/// timeline of as many whole hours as the tile holds — the whole day on a tall tile, a few hours
/// around the next event (or now) on a small one — with overlapping events side by side.
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
        var items = CalendarRules.DayItems(_events, _birthdays, day, zone);
        var allDay = items.Where(i => i.Birthday is not null || i.Event!.AllDay || CalendarFit.IsMultiDay(i.Event, zone)).ToList();
        var timed = items.Where(i => i.Event is { AllDay: false } e && !CalendarFit.IsMultiDay(e, zone)).Select(i => i.Event!).ToList();

        // [S-2718] The band's bars first, as many as leave the timeline its hours; the hours take the rest.
        var room = new Disclosure(width, Math.Max(0, height - Fluent.Space1));
        var band = CalendarFit.DayBand(room.Remaining, allDay.Count);
        if (band.Lines > 0)
        {
            room.Reserve((band.Lines * band.LineHeight) + Fluent.Space1);
        }

        var gutter = width >= HourLabelsLongFrom ? HourLabelWidth : HourLabelNarrowWidth;
        var timelineWidth = Math.Max(0, width - gutter - Fluent.Space2);

        var root = new Grid { Padding = new Thickness(0, 0, Fluent.Space2, Fluent.Space1) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gutter) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (band.Lines > 0)
        {
            root.Children.Add(DayBand(allDay, day, timelineWidth, band, showColors));
        }

        var (earliest, latest) = CalendarFit.EventHours(timed, day, zone);
        var window = CalendarFit.Hours(room.Remaining, earliest, latest, CalendarFit.FocusHour(timed, day, Today, DateTimeOffset.Now, zone));

        var labels = HourLabels(window, long_: gutter == HourLabelWidth);
        SetRow(labels, 1);
        root.Children.Add(labels);

        var rules = HourRules(window);
        SetRow(rules, 1);
        SetColumn(rules, 1);
        root.Children.Add(rules);

        var canvas = TimelineCanvas(timed, day, window, timelineWidth, showColors);
        SetRow(canvas, 1);
        SetColumn(canvas, 1);
        root.Children.Add(canvas);

        if (items.Count == 0)
        {
            var none = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            none.Text = (day == Today ? Str.Calendar_NoEventsToday : Str.WidgetsA_Calendar_NoEvents).Text();
            none.HorizontalAlignment = HorizontalAlignment.Center;
            none.VerticalAlignment = VerticalAlignment.Center;
            SetRow(none, 1);
            SetColumn(none, 1);
            root.Children.Add(none);
        }

        _content.Children.Add(root);
    }

    /// <summary>
    /// [S-2718] The band over the day's timeline: one full-width span bar per item on the lines the fit
    /// gave it; when items are left over, the last line shares its width among them ([S-2706]).
    /// </summary>
    private StackPanel DayBand(IReadOnlyList<DayItem> allDay, DateOnly day, double width, DayBandFit band, bool showColors)
    {
        var stack = new StackPanel { Spacing = 0, Margin = new Thickness(0, 0, 0, Fluent.Space1) };
        SetColumn(stack, 1);

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
        var radius = Fluent.ControlRadius;

        var body = new Grid { ColumnSpacing = Fluent.Space1, VerticalAlignment = VerticalAlignment.Center };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Fluent.Text(FluentType.Body);
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
            Height = lineHeight - 2,
            Margin = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0),
            CornerRadius = new CornerRadius(before ? 0 : radius, after ? 0 : radius, after ? 0 : radius, before ? 0 : radius),
            BorderThickness = new Thickness(before ? 0 : 3, 0, 0, 0),
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
            var dates = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
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
            FontSize = FluentType.Caption.FontSize,
            Foreground = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray),
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
        var fits = Math.Max(1, (int)Math.Floor((width + Fluent.Space1) / (MinChip + Fluent.Space1)));
        var shown = allDay.Count <= fits ? allDay.Count : Math.Max(1, fits - 1);

        var bar = new Grid { ColumnSpacing = Fluent.Space1, Height = lineHeight };

        for (var i = 0; i < shown; i++)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 200 });
            var chip = AllDayChip(allDay[i], day, lineHeight, showColors);
            SetColumn(chip, i);
            bar.Children.Add(chip);
        }

        if (shown < allDay.Count)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var more = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
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
        var text = Fluent.Text(lineHeight > CalendarFit.ChipLine ? FluentType.Body : FluentType.Caption);
        text.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border
        {
            Child = text,
            Height = lineHeight - (lineHeight > CalendarFit.ChipLine ? 2 : 1),
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            CornerRadius = new CornerRadius(lineHeight > CalendarFit.ChipLine ? Fluent.ControlRadius : 2),
            BorderThickness = new Thickness(2, 0, 0, 0),
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
        var line = FluentType.Caption.LineHeight;
        var stack = new StackPanel { Spacing = 0 };

        var title = Fluent.Text(FluentType.Caption);
        title.Text = Title(e);
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);

        var meta = string.Join(" · ", new[] { e.Location, e.Organizer }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // The block's padding and coloured edge come off the title's line.
        var lines = Disclosure.WholeRows(height, line);
        var parts = CalendarFit.BlockParts(lines, Overflows(title, width - (2 * Fluent.Space1) - 2), meta.Length > 0);

        if (parts.TitleLines > 1)
        {
            WrapTitle(title);
        }

        if (parts.Time)
        {
            var time = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            time.Text = e.End > e.Start ? $"{Clock(e.Start)}–{Clock(e.End)}" : Clock(e.Start);
            Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            stack.Children.Add(time);
        }

        if (parts.Meta)
        {
            var where = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            where.Text = meta;
            stack.Children.Add(where);
        }

        var block = new Border
        {
            Child = stack,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = showColors ? ColorOf(e) : Accent(),
        };
        ItemFill.Paint(block, ItemRole.Chip);

        MakeOpenable(block, e);
        return block;
    }
}
