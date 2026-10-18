using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7035] [S-7036] The paged look: a page of whole rows in the plan's columns, each day's heading
/// over its first row (and again over a column that starts mid-day), the schedule rows of one
/// height, and the long titles given their second line from what is left under a column. Pages are
/// turned by the widget's own controls (<see cref="Page"/>); the cursor turns to the page of the row
/// it moves to.
/// </summary>
public sealed partial class KitList
{
    /// <summary>The page's columns, in the list's slot instead of the scroll.</summary>
    private readonly Grid _page = new() { ColumnSpacing = Fluent.Space2, Padding = Fluent.ListPadding };

    private IReadOnlyList<int> _pageStarts = [0];

    /// <summary>The focus rings over the page's rows, by place.</summary>
    private readonly Dictionary<int, Border> _rings = [];
    private int _pageIndex;
    private DateTimeOffset _drawnAt;

    /// <summary>[S-7035] The page on screen; set it and <see cref="Show"/> again to turn it (clamped to the pages there are).</summary>
    public int Page
    {
        get => _pageIndex;
        set => _pageIndex = Math.Max(0, value);
    }

    /// <summary>[S-7035] How many pages the last draw had.</summary>
    public int PageCount => _pageStarts.Count;

    /// <summary>[S-7035] Each page's first row, as the last draw fitted them.</summary>
    public IReadOnlyList<int> PageStarts => _pageStarts;

    /// <summary>[S-7035] The first item on the page on screen, or null for none.</summary>
    public KitItem? FirstOnPage => _pageStarts.Count > 0 && _pageIndex < _pageStarts.Count && _pageStarts[_pageIndex] < _items.Count ? _items[_pageStarts[_pageIndex]] : null;

    /// <summary>
    /// The view's own per-row hook, run on every row it draws (the calendar's links across widgets, its
    /// tooltip): what only the widget knows about a row. Rows are drawn anew on every show.
    /// </summary>
    public Action<KitItem, FrameworkElement>? Decorate { get; init; }

    private bool Paged => _spec.Overflow == KitOverflow.Page;

    /// <summary>[S-7035] The page holding row <paramref name="index"/>, drawn if it is not the one on screen.</summary>
    private void TurnTo(int index)
    {
        if (!Paged || index < 0 || index >= _items.Count)
        {
            return;
        }

        var page = KitPages.PageOf(_pageStarts, index);
        if (page != _pageIndex)
        {
            _pageIndex = page;
            BuildRows(_drawnAt);
        }
    }

    private void BuildPage(DateTimeOffset now)
    {
        _drawnAt = now;
        _page.Children.Clear();
        _page.ColumnDefinitions.Clear();

        // Every item has its slot in the rows, so the cursor and the menus find it by place; only the
        // page's rows are drawn.
        for (var i = 0; i < _items.Count; i++)
        {
            _rowElements.Add(new Border());
        }

        if (_items.Count == 0)
        {
            _pageStarts = [0];
            _pageIndex = 0;
            return;
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        var keys = _items.Select(i => i.Group?.Key ?? string.Empty).ToList();
        var box = Math.Max(0, _area.Height - Fluent.ListPadding.Bottom);
        var firstHeading = KitPages.ShowsFirstHeading(_items[0].Group?.Day, today, _area.Height);

        _pageStarts = KitPages.Pages(keys, _plan.Columns, box, _plan.HeadingHeight, _plan.RowHeight, firstHeading);
        _pageIndex = Math.Clamp(_pageIndex, 0, _pageStarts.Count - 1);

        // [S-2716] "Today" and "Tomorrow" carry their date where the heading line holds it whole.
        var dated = KitDays.Dated(Strings.Current, _plan.ColumnWidth - (2 * Fluent.Space4));
        var timeColumn = KitLadder.TimeColumn(WidestTimeLabel(), _plan.ColumnWidth);
        var titleWidth = ScheduleTitleWidth(timeColumn);
        var at = _pageStarts[_pageIndex];

        for (var c = 0; c < _plan.Columns && at < _items.Count; c++)
        {
            _page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var heading = firstHeading || at > 0;
            var count = KitPages.Fit(keys, at, box, _plan.HeadingHeight, _plan.RowHeight, heading);
            var stack = new StackPanel { Spacing = 0 };
            var rows = new List<(Border Row, TextBlock Title)>(count);

            for (var i = at; i < at + count; i++)
            {
                var item = _items[i];
                if (KitPages.HeadingBefore(keys, at, i, heading))
                {
                    stack.Children.Add(Heading(item.Group, today, dated));
                }

                var (row, title) = ScheduleRow(item, i, timeColumn);
                row.Height = _plan.RowHeight;
                stack.Children.Add(row);
                rows.Add((row, title));
                _rowElements[i] = row;
            }

            // [S-2717] The column's rows are in at one title line each; what is left under them gives
            // the long titles their second line, in order.
            var leftover = box - KitPages.ColumnHeight(keys, at, count, _plan.HeadingHeight, _plan.RowHeight, heading);
            var second = Disclosure.SecondLines([.. rows.Select(r => Overflows(r.Title, titleWidth))], leftover, FluentType.BodyStrong.LineHeight);
            for (var k = 0; k < rows.Count; k++)
            {
                if (second[k])
                {
                    rows[k].Row.Height = _plan.RowHeight + FluentType.BodyStrong.LineHeight;
                    WrapTitle(rows[k].Title);
                }
            }

            SetColumn(stack, c);
            _page.Children.Add(stack);
            at += count;
        }
    }

    /// <summary>[S-2716] [S-3622] A day's heading: its words, in the accent when the day is the one selected.</summary>
    private static Border Heading(KitGroup? group, DateOnly today, bool dated)
    {
        var text = Fluent.Text(FluentType.BodyStrong, group is { Selected: true } ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        text.Text = group?.Heading ?? (group?.Day is { } day ? KitDays.Heading(Strings.Current, day, today, dated) : string.Empty);

        return new Border
        {
            Child = text,
            Height = FluentType.BodyStrong.LineHeight + Fluent.Space2 + Fluent.Space1,
            Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space1),
        };
    }

    /// <summary>
    /// [S-571] The widest thing the time column shows, measured in the current language: "All day"
    /// and "Now" run far longer than a clock in some languages. The spec names them.
    /// </summary>
    private double WidestTimeLabel()
    {
        var widest = 0.0;
        foreach (var (label, strong) in _spec.TimeLabels?.Invoke() ?? [])
        {
            var probe = Fluent.Text(strong ? FluentType.BodyStrong : FluentType.Body);
            probe.Language = UiLanguage.Tag(Strings.Current);
            probe.TextTrimming = TextTrimming.None;
            probe.Text = label;
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }

        return widest;
    }

    /// <summary>
    /// [S-2717] The width a schedule row's title has: the column less the row's padding, the stripe,
    /// the time column beside it (not on a narrow column) and, with where and who in columns, their
    /// share of the rest (1 : 0.6 : 0.45).
    /// </summary>
    private double ScheduleTitleWidth(double timeColumn)
    {
        var width = _plan.ColumnWidth - (Fluent.Space4 - Fluent.Space2) - (_plan.Narrow ? Fluent.Space2 : Fluent.Space4) - Fluent.Space2;
        if (!_plan.Narrow)
        {
            width -= timeColumn + Fluent.Space2;
        }

        return _plan.Details ? (width - (2 * Fluent.Space2)) / 2.05 : width;
    }

    /// <summary>
    /// [S-7036] One schedule row: the stripe, the time column (start over end, a word in the strong
    /// step for one under way, or the lead's glyph), then the title over its detail. On a narrow column
    /// ([S-574]) the time moves under the title, where it costs a line rather than half the width; with
    /// details in columns ([S-1267]) where and who get their own beside the title.
    /// </summary>
    private (Border Row, TextBlock Title) ScheduleRow(KitItem item, int index, double timeColumn)
    {
        var schedule = item.Schedule ?? new KitSchedule(null);
        var compact = _plan.Compact;
        var narrow = _plan.Narrow;

        var when = new StackPanel { Spacing = 0, Width = timeColumn, Margin = new Thickness(0, 0, Fluent.Space2, 0), VerticalAlignment = VerticalAlignment.Center };
        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        var title = Fluent.Text(FluentType.BodyStrong);
        title.Text = TitleText(item);
        text.Children.Add(title);

        if (schedule.Start is { } start)
        {
            var first = Fluent.Text(schedule.Strong ? FluentType.BodyStrong : FluentType.Body, "TextFillColorSecondaryBrush");
            first.Text = start;

            // A clock is never cut; a word longer than the column's cap trims rather than clips.
            first.TextTrimming = schedule.Word ? TextTrimming.CharacterEllipsis : TextTrimming.None;
            Typography.SetNumeralAlignment(first, FontNumeralAlignment.Tabular);
            when.Children.Add(first);

            if (!compact && schedule.End is { } end)
            {
                var last = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                last.Text = end;
                Typography.SetNumeralAlignment(last, FontNumeralAlignment.Tabular);
                when.Children.Add(last);
            }
        }
        else if (item.Lead.Kind == KitLeadKind.Icon && Enum.TryParse<KitIcon>(item.Lead.Value, out var glyph))
        {
            var icon = Fluent.Icon(KitIcons.Glyph(glyph), item.Lead.Tone == KitTone.Neutral ? "TextFillColorSecondaryBrush" : ToneText(item.Lead.Tone));
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            when.Children.Add(icon);
        }

        string? second;
        if (schedule.Note is { } note)
        {
            second = compact && !narrow ? null : note;
        }
        else
        {
            var meta = string.Join(" · ", new[] { schedule.Where, schedule.Who }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var span = schedule.Span ?? string.Empty;
            second = narrow
                ? (compact || meta.Length == 0 ? span : $"{span} · {meta}")
                : (compact || meta.Length == 0 || _plan.Details ? null : meta);
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

        if (_plan.Details)
        {
            // [S-1267] Where, then who, each in its own column, tertiary like the line they replace.
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.45, GridUnitType.Star) });

            for (var i = 0; i < 2; i++)
            {
                var cell = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                cell.Text = (schedule.Note is null ? (i == 0 ? schedule.Where : schedule.Who) : null) ?? string.Empty;
                cell.VerticalAlignment = VerticalAlignment.Center;
                cell.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                SetColumn(cell, 3 + i);
                body.Children.Add(cell);
            }
        }

        // [S-566] The calendar's colour as a stripe before the time, when it has one; else the tone's
        // (a birthday's caution hue), else none.
        var bar = new Border
        {
            Width = 2,
            Margin = new Thickness(0, 2, Fluent.Space2 - 2, 2),
            CornerRadius = new CornerRadius(1),
            Background = schedule.Mark is { } mark
                ? HexBrush.Of(mark) ?? Fluent.Brush("AccentFillColorDefaultBrush")
                : Fluent.Brush(item.Tone == KitTone.Neutral ? "SubtleFillColorTransparentBrush" : ToneFill(item.Tone)),
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

        // The focus ring lies over the row, out to its edges, and takes no room: the row lays out as
        // one that cannot be focused, and focusing it moves nothing.
        var padding = new Thickness(Fluent.Space4 - Fluent.Space2, 0, narrow ? Fluent.Space2 : Fluent.Space4, 0);
        var ring = new Border
        {
            Margin = new Thickness(-padding.Left, 0, -padding.Right, 0),
            IsHitTestVisible = false,
        };
        RowFocus.Prepare(ring);
        var holder = new Grid { Children = { body, ring } };

        var row = Fluent.Row(holder, compact);
        row.Padding = padding;
        holder.VerticalAlignment = VerticalAlignment.Stretch;
        body.VerticalAlignment = VerticalAlignment.Center;
        row.Tag = index;
        WidgetItems.Mark(row);
        _rings[index] = ring;

        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => Entries(item)));

        if ((item.Tooltip ?? item.Link?.OriginalString) is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        Decorate?.Invoke(item, row);
        return (row, title);
    }

    /// <summary>
    /// [S-2717] Whether a one-line text runs past <paramref name="width"/>: a title that would take a
    /// second line if it had one. Measured as drawn (its step, weight and language).
    /// </summary>
    private static bool Overflows(TextBlock text, double width)
    {
        if (!(width > 0) || string.IsNullOrEmpty(text.Text))
        {
            return false;
        }

        var trimming = text.TextTrimming;
        text.Language = UiLanguage.Tag(Strings.Current);
        text.TextTrimming = TextTrimming.None;
        text.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var wide = text.DesiredSize.Width > width + Disclosure.Tolerance;
        text.TextTrimming = trimming;
        return wide;
    }

    /// <summary>[S-2717] A title given its second line: it wraps by word and ends in an ellipsis after it.</summary>
    private static void WrapTitle(TextBlock text)
    {
        text.TextWrapping = TextWrapping.WrapWholeWords;
        text.MaxLines = KitPages.MaxTitleLines;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
    }
}
