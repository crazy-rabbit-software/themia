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
/// [S-571] / [S-2709] The agenda: what is coming up, grouped by day, in pages of the rows that fit
/// whole — a small tile shows a couple of rows, a large one two columns of them — paged with the
/// header's arrows and the keys rather than scrolled.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>Below this column width the time column folds into the line under the title.</summary>
    private const double AgendaNarrowBelow = 260;

    private void DrawAgenda(double width, double height)
    {
        var today = Today;
        var (first, last) = CalendarRules.ViewRange(CalendarLayout.Agenda, _anchor, today, _config.EffectiveWeekStart);
        var groups = CalendarRules.Agenda(_events, _birthdays, first, last, TimeZoneInfo.Local, _config is AgendaConfig agenda ? agenda.EffectiveAgendaCount : AgendaConfig.DefaultCount, DateTimeOffset.Now);

        var items = groups.SelectMany(g => g.Items.Select(i => (g.Day, Item: i))).ToList();

        _agendaFirst = null;
        if (items.Count == 0)
        {
            _agendaPages = 1;
            _agendaPage = 0;
            _state.Show(Str.Calendar_NoUpcoming.Text(), offerAccounts: false);
            return;
        }

        var columns = width >= CalendarFit.AgendaTwoColumnsFrom ? 2 : 1;
        var columnWidth = columns == 1 ? width : (width - Fluent.Space2) / 2;
        var compact = height > 0 && height < Fluent.CompactBelow;
        var narrow = columnWidth > 0 && columnWidth < AgendaNarrowBelow;
        var metaColumns = columnWidth >= CalendarFit.AgendaColumnsFrom && !compact;
        var timeColumn = CalendarFit.AgendaTimeColumn(WidestTimeLabel(), columnWidth);

        // One row height per render, so the fit is exact: two lines where a second one is drawn.
        var twoLines = narrow || !compact;
        var rowHeight = twoLines
            ? FluentType.BodyStrong.LineHeight + FluentType.Caption.LineHeight + (2 * Fluent.Space1)
            : Math.Max(Fluent.RowHeightCompact, FluentType.BodyStrong.LineHeight + (2 * Fluent.Space1));
        var headingHeight = FluentType.BodyStrong.LineHeight + Fluent.Space2 + Fluent.Space1;
        var box = Math.Max(0, height - Fluent.ListPadding.Bottom);

        var days = items.Select(i => i.Day).ToList();
        var firstHeading = CalendarFit.ShowsFirstHeading(days[0], today, height);
        var pages = CalendarFit.AgendaPages(days, columns, box, headingHeight, rowHeight, firstHeading);
        _agendaPages = pages.Count;

        // [S-3622] A day selected on the view turns the agenda to the page that holds it.
        if (_agendaJump is { } jump)
        {
            _agendaJump = null;
            if (CalendarShare.AgendaPageOf(days, pages, jump) is { } page)
            {
                _agendaPage = page;
            }
        }

        _agendaPage = Math.Clamp(_agendaPage, 0, pages.Count - 1);

        var showColors = ShowColors;
        var now = DateTimeOffset.Now;
        var grid = new Grid { ColumnSpacing = Fluent.Space2, Padding = Fluent.ListPadding };
        var at = pages[_agendaPage];

        for (var c = 0; c < columns && at < items.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var heading = firstHeading || at > 0;
            var count = CalendarFit.AgendaFit(days, at, box, headingHeight, rowHeight, heading);
            var stack = new StackPanel { Spacing = 0 };

            for (var i = at; i < at + count; i++)
            {
                var (day, item) = items[i];
                if (i == at ? heading : day != days[i - 1])
                {
                    // [S-3622] The selected day's heading is in the accent.
                    var text = Fluent.Text(FluentType.BodyStrong, day == _selected ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
                    text.Text = Formatting.DayHeading(day, today);

                    stack.Children.Add(new Border { Child = text, Height = headingHeight, Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space1) });
                }

                var row = AgendaRow(item, day, now, compact, narrow, metaColumns, showColors, timeColumn);
                row.Height = rowHeight;
                _hovers.Add(Fluent.Hover(row));
                stack.Children.Add(row);
                _agendaFirst ??= item.Event;
            }

            SetColumn(stack, c);
            grid.Children.Add(stack);
            at += count;
        }

        _content.Children.Add(grid);
    }

    /// <summary>
    /// [S-571] The widest thing the time column shows, measured in the current language: "All day"
    /// and "Now" run far longer than a clock in some languages ("Toute la journée", "طوال اليوم"). The clock is
    /// written as the rows write it, in the language's digits and separator ("٠٠:٠٠", "00.00").
    /// </summary>
    private static double WidestTimeLabel()
    {
        var widest = 0.0;
        foreach (var (type, label) in new[] { (FluentType.Body, Str.Calendar_AllDay.Text()), (FluentType.BodyStrong, Str.WidgetsA_Calendar_Now.Text()), (FluentType.Body, LocaleFormat.Time(Strings.Current, TimeOnly.MinValue, HourCycle.H24)) })
        {
            var probe = Fluent.Text(type);
            probe.Language = UiLanguage.Tag(Strings.Current);
            probe.TextTrimming = TextTrimming.None;
            probe.Text = label;
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }

        return widest;
    }

    /// <summary>
    /// [S-571] One agenda row: the time column (start over end, "All day", or "Now" in the strong
    /// step for one under way), then the title over where and who. On a narrow tile ([S-574]) the
    /// time moves under the title, where it costs a line rather than half the width; on a wide one
    /// ([S-1267]) where and who get columns of their own beside the title.
    /// </summary>
    private Border AgendaRow(DayItem item, DateOnly day, DateTimeOffset now, bool compact, bool narrow, bool metaColumns, bool showColors, double timeColumn)
    {
        string? location = null;
        string? organizer = null;
        var when = new StackPanel { Spacing = 0, Width = timeColumn, Margin = new Thickness(0, 0, Fluent.Space2, 0), VerticalAlignment = VerticalAlignment.Center };
        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        var title = Fluent.Text(FluentType.BodyStrong);
        text.Children.Add(title);

        Brush? stripe = null;
        string? second;

        if (item.Birthday is { } b)
        {
            var gift = Fluent.Icon(GiftGlyph, "SystemFillColorCautionBrush");
            gift.HorizontalAlignment = HorizontalAlignment.Left;
            when.Children.Add(gift);
            title.Text = BirthdayRules.Label(b, day);
            second = compact && !narrow ? null : Str.WidgetsA_Calendar_Birthday.Text();
            stripe = BirthdayBrush();
        }
        else
        {
            var e = item.Event!;

            // [S-571] The part of the event on this row's day: a timed event over several days reads
            // its own hours on its first and last day and "All day" between.
            var part = CalendarRules.AgendaSpan(e, day, TimeZoneInfo.Local);
            var running = !part.WholeDay && AgendaRules.IsInProgress(false, part.Start, part.End, now);
            var startText = part.WholeDay ? Str.Calendar_AllDay.Text() : running ? Str.WidgetsA_Calendar_Now.Text() : Clock(part.Start);
            var start = Fluent.Text(running ? FluentType.BodyStrong : FluentType.Body, "TextFillColorSecondaryBrush");
            start.Text = startText;
            // A clock is never cut; a word label longer than the column's cap trims rather than clips.
            start.TextTrimming = part.WholeDay || running ? TextTrimming.CharacterEllipsis : TextTrimming.None;
            Typography.SetNumeralAlignment(start, FontNumeralAlignment.Tabular);
            when.Children.Add(start);

            if (!part.WholeDay && !compact && part.End > part.Start)
            {
                var end = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                end.Text = Clock(part.End);
                Typography.SetNumeralAlignment(end, FontNumeralAlignment.Tabular);
                when.Children.Add(end);
            }

            title.Text = Title(e);

            var meta = string.Join(" · ", new[] { e.Location, e.Organizer }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var span = part.WholeDay || running || part.End <= part.Start ? startText : $"{startText}–{Clock(part.End)}";

            second = narrow
                ? (compact || meta.Length == 0 ? span : $"{span} · {meta}")
                : (compact || meta.Length == 0 || metaColumns ? null : meta);

            if (metaColumns)
            {
                location = e.Location;
                organizer = e.Organizer;
            }

            if (showColors)
            {
                stripe = ColorOf(e);
            }
        }

        if (second is not null)
        {
            var detail = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            detail.Text = second;
            Typography.SetNumeralAlignment(detail, FontNumeralAlignment.Tabular);
            text.Children.Add(detail);
        }

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1),
        };

        if (metaColumns)
        {
            // [S-1267] Where, then who, each in its own column, tertiary like the line they replace.
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.45, GridUnitType.Star) });

            for (var i = 0; i < 2; i++)
            {
                var cell = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                cell.Text = (i == 0 ? location : organizer) ?? string.Empty;
                cell.VerticalAlignment = VerticalAlignment.Center;
                cell.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                SetColumn(cell, 3 + i);
                body.Children.Add(cell);
            }
        }

        // [S-566] The calendar's colour as a stripe before the time, when colours show; a birthday's
        // is the caution hue, as its chips are.
        var bar = new Border
        {
            Width = 2,
            Margin = new Thickness(0, 2, Fluent.Space2 - 2, 2),
            CornerRadius = new CornerRadius(1),
            Background = stripe ?? FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
        };

        SetColumn(bar, 0);
        SetColumn(when, 1);
        SetColumn(text, 2);
        body.Children.Add(bar);
        body.Children.Add(text);

        if (!narrow)
        {
            body.Children.Add(when);
        }

        var row = Fluent.Row(body, compact);
        row.Padding = new Thickness(Fluent.Space4 - Fluent.Space2, 0, narrow ? Fluent.Space2 : Fluent.Space4, 0);

        if (item.Event is { } ev)
        {
            MakeOpenable(row, ev);
        }

        return row;
    }
}
