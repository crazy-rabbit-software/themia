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
/// [S-573] / [S-2705] The day: its all-day events and birthdays in a bar, then a timeline of as many
/// whole hours as the tile holds — the whole day on a tall tile, a few hours around the next event
/// (or now) on a small one — with overlapping events side by side.
/// </summary>
internal sealed partial class CalendarView
{
    private void DrawDay(double width, double height)
    {
        var day = _anchor;
        var zone = TimeZoneInfo.Local;
        var showColors = ShowColors;
        var items = CalendarRules.DayItems(_events, _birthdays, day, zone);
        var allDay = items.Where(i => i.Birthday is not null || i.Event!.AllDay || CalendarFit.IsMultiDay(i.Event, zone)).ToList();
        var timed = items.Where(i => i.Event is { AllDay: false } e && !CalendarFit.IsMultiDay(e, zone)).Select(i => i.Event!).ToList();

        var room = new Disclosure(width, Math.Max(0, height - Fluent.Space1));
        var bar = allDay.Count > 0 && room.Take(CalendarFit.ChipLine + Fluent.Space1);
        var gutter = width >= HourLabelsLongFrom ? HourLabelWidth : HourLabelNarrowWidth;

        var root = new Grid { Padding = new Thickness(0, 0, Fluent.Space2, Fluent.Space1) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gutter) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (bar)
        {
            root.Children.Add(AllDayBar(allDay, day, width - gutter - Fluent.Space2, showColors));
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

        var canvas = TimelineCanvas(timed, day, window, showColors);
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
    /// The all-day bar: chips side by side sharing the width, each trimmed to its share; what does not
    /// fit at a readable width is "+N", listed on hover.
    /// </summary>
    private Grid AllDayBar(IReadOnlyList<DayItem> allDay, DateOnly day, double width, bool showColors)
    {
        const double MinChip = 56;
        var fits = Math.Max(1, (int)Math.Floor((width + Fluent.Space1) / (MinChip + Fluent.Space1)));
        var shown = allDay.Count <= fits ? allDay.Count : Math.Max(1, fits - 1);

        var bar = new Grid { ColumnSpacing = Fluent.Space1, Margin = new Thickness(0, 0, 0, Fluent.Space1), MaxHeight = CalendarFit.ChipLine };
        SetColumn(bar, 1);

        for (var i = 0; i < shown; i++)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 200 });
            var chip = AllDayChip(allDay[i], day, showColors);
            SetColumn(chip, i);
            bar.Children.Add(chip);
        }

        if (shown < allDay.Count)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var more = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            more.Text = $"+{(allDay.Count - shown).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            more.VerticalAlignment = VerticalAlignment.Center;
            ToolTipService.SetToolTip(more, DayTooltip(day, allDay));
            SetColumn(more, shown);
            bar.Children.Add(more);
        }

        return bar;
    }

    private Border AllDayChip(DayItem item, DateOnly day, bool showColors)
    {
        var text = Fluent.Text(FluentType.Caption);
        text.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border
        {
            Child = text,
            Height = CalendarFit.ChipLine - 1,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            CornerRadius = new CornerRadius(2),
            Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = MarkOf(item, showColors),
        };

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
    /// ([S-1266]) adds where and who; one a line tall keeps the title only.
    /// </summary>
    private Border TimedBlock(CalendarEvent e, bool showColors, double height)
    {
        var line = FluentType.Caption.LineHeight;
        var stack = new StackPanel { Spacing = 0 };

        var title = Fluent.Text(FluentType.Caption);
        title.Text = Title(e);
        title.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(title);

        if (height >= 2 * line)
        {
            var time = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            time.Text = e.End > e.Start ? $"{Clock(e.Start)}–{Clock(e.End)}" : Clock(e.Start);
            Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            stack.Children.Add(time);
        }

        var meta = string.Join(" · ", new[] { e.Location, e.Organizer }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (meta.Length > 0 && height >= 3 * line)
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
            Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = showColors ? ColorOf(e) : Accent(),
        };

        MakeOpenable(block, e);
        return block;
    }
}
