using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.ScreenTime;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3330] The last week or month a day at a time: one bar per day stacked by app (the busiest
/// seven and "Other", in the same data colours as the donut and the list), or a calendar of rings;
/// its total is the reading atom's ([S-11500]). Hovering or selecting an app anywhere on the view lights its
/// part of every bar and dims the rest; the calendar then shades that app's days ([S-3333]).
/// <para>
/// [S-3325] The drawing is built when what it is made of changes (the style, the days, the
/// size, today, the language) and otherwise moved in place: a read that adds thirty seconds to today
/// sets one bar part's height and a tooltip, and builds nothing.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeChartView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetKeys, IWidgetTopEdge
{
    /// <summary>
    /// [S-5700] [S-6754] [S-15772] The bars are a graph's plot, the card's whole box (owner, 2026-11-19: "Plot edge to
    /// edge"), and the calendar's cells stand from the card's top-left edge (WR-1, WR-3): the view owns the card's top edge
    /// in both styles and keeps the inset over its words itself.
    /// </summary>
    public bool OwnsTopEdge => true;

    private readonly ScreenTimeLinks _links;

    /// <summary>[S-11924] The day on the view's links: this chart's pointed day published, another widget's followed.</summary>
    private readonly WidgetLinks _dates;

    /// <summary>[S-11924] The day another widget points at (a calendar's cell, a forecast's row); null: none.</summary>
    private DateLink? _linkedDay;

    /// <summary>[S-454] [S-12481] The day line names the window ("Last 7 days"): the day navigation's label alone.</summary>
    private readonly ScreenTimeDayNav _nav;

    private readonly Grid _drawing = new() { FlowDirection = FlowDirection.LeftToRight };

    /// <summary>[S-6753] [S-15774] The caption written with an empty window's chart, or with no recording at all: the chart stays.</summary>
    private readonly TextBlock _note = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);

    /// <summary>Every app part of every bar, with its day, so a focus lights the one part it is.</summary>
    private readonly List<(ChartPart Part, Border Block)> _parts = [];

    /// <summary>[S-15773] The part under this chart's pointer, and the part clicked here: one app on one day.</summary>
    private ChartPart? _hoverPart;
    private ChartPart? _pickPart;

    /// <summary>Every calendar cell (its ring or dot), with the day it is.</summary>
    private readonly List<(int Day, Cell Mark)> _cells = [];

    private readonly List<Grid> _columns = [];

    /// <summary>[S-3325] Each bar's own parts, kept by app; one per day, oldest first.</summary>
    private readonly List<Bar> _bars = [];

    /// <summary>[S-3325] Each heat cell's tooltip as last written, by cell.</summary>
    private readonly List<string?> _cellTips = [];

    /// <summary>[S-3325] What the drawing on screen was built for; null when there is none.</summary>
    private Frame? _frame;

    /// <summary>[S-450] [S-3310] The tracker, the day line, the app links and the dates: joined and left as one.</summary>
    private readonly ScreenTimeWiring _wiring;
    /// <summary>[S-450] The tracker, followed as every screen-time widget follows it.</summary>
    private readonly ScreenTimeFeed _feed;
    private IWidgetSurface? _surface;
    private ScreenTimeChartConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];

    /// <summary>[S-2272] Each drawn app's tone by its rank in the period.</summary>
    private IReadOnlyDictionary<string, ScreenTimeTone> _tones = new Dictionary<string, ScreenTimeTone>();
    private WidgetArea _area;
    private int? _day;

    public ScreenTimeChartView(IWidgetHost host)
    {
        _wiring = new ScreenTimeWiring(host, Render, ApplyFocus);
        (_links, _nav, _feed, _dates) = (_wiring.Links, _wiring.Nav, _wiring.Feed, _wiring.Dates);
        _dates.Follow(LinkTopics.HoveredDate, value =>
        {
            _linkedDay = value;
            ApplyFocus();
        });

        Background = Family.Brush(ColourRole.Clear);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                _pickPart = null;
                ApplyFocus();
            }
        };

        _drawing.VerticalAlignment = VerticalAlignment.Top;

        // [S-454] [S-12481] The day and its arrows, the content's first line; the drawing under it from the card's top edge.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _nav.Line.Margin = Family.SideInset;
        SetRow(_drawing, 0);
        SetRowSpan(_drawing, 2);
        SetRow(_note, 0);
        SetRowSpan(_note, 2);
        _note.Margin = Family.SideInset;
        _note.IsHitTestVisible = false;
        _note.Visibility = Visibility.Collapsed;
        Children.Add(_drawing);
        Children.Add(_nav.Line);
        Children.Add(_note);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ScreenTimeChartConfig ?? new ScreenTimeChartConfig();
        _wiring.Attach(_surface);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeChartConfig ?? new ScreenTimeChartConfig();
        Render();
    }

    public void Detach()
    {
        _wiring.Detach();
        Clear();
    }

    private ScreenTimePeriod Period => ScreenTimeRules.ChartPeriod(_config.Period);

    private ScreenTimeChartStyle ChartStyle => _config.Style;

    private LocalDay Today => _feed.Source?.Today ?? LocalDay.From(DateTimeOffset.Now);

    // ---- drawing ----------------------------------------------------------------------------

    private void Render()
    {
        if (_wiring.Detached)
        {
            return;
        }

        // [S-454] The day line names the window, "Last 7 days".
        _nav.Update(Period);

        // [S-6753] [S-15774] A window with nothing in it, or no recording at all, is still the chart (WR-10): its empty days
        // on their foot, why in a caption.
        _data = ScreenTimeData.Build(_feed.Source ?? NoRecording.Instance, Period, null);
        var why = _feed.Source is null ? Str.WidgetsC_ScreenTime_NotRecording.Text()
            : _data.Total <= 0 ? Str.WidgetsC_ScreenTime_NoActivityPeriod.Text()
            : string.Empty;
        _note.Text = why;
        _note.Visibility = why.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _drawing.Visibility = Visibility.Visible;

        if (_day is { } day && day >= _data.Range.Count)
        {
            _day = null;
        }

        Draw(_data);
    }

    private void Clear()
    {
        _frame = null;
        _parts.Clear();
        _cells.Clear();
        _cellTips.Clear();
        _columns.Clear();
        _bars.Clear();
        _drawing.Children.Clear();
        _drawing.ColumnDefinitions.Clear();
        _drawing.RowDefinitions.Clear();
    }

    /// <summary>[S-3331] [S-11505] The drawing, one height for its style and period, as wide as the card less its inset.</summary>
    private void Draw(ScreenTimeData data)
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        _segments = ScreenTimeRules.Segments(data.Apps, data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());
        _tones = ScreenTimeRules.Tones(_segments);

        // [S-12410] [S-6754] The bars are a chart: their plot is the card's whole box, edge to edge, the end labels
        // inside it at the inset (Plot edge to edge). [S-15772] The calendar's cells keep their one size from the card's
        // top-left edge, the last week the card reaches cut by its foot. [S-12483] The same joined or alone.
        var calendar = ChartStyle == ScreenTimeChartStyle.Calendar;
        var width = Math.Max(1, _area.Width);
        var height = Math.Max(1, Math.Floor(_area.Height));
        _drawing.Height = height;
        _drawing.VerticalAlignment = VerticalAlignment.Top;
        _drawing.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, height) };
        Edges(calendar);

        // [S-3325] Built once for what it is made of; every other read moves what is there.
        var frame = new Frame(ChartStyle, data.Range.Count, width, height, Today.Key, data.Range.Count > 0 ? data.Range[0].Date : string.Empty, Strings.Current, data.Total <= 0);
        if (frame != _frame)
        {
            Clear();
            _frame = frame;
            _drawing.Children.Add(calendar ? Calendar(data, width, height) : Bars(data, height));
        }

        if (calendar)
        {
            TipCells(data);
        }
        else
        {
            FillBars(data, height);
        }

        ApplyFocus();
    }

    /// <summary>[S-3325] What a drawing is built for: a read that matches it is drawn into the one on screen.</summary>
    private readonly record struct Frame(
        ScreenTimeChartStyle Style,
        int Days,
        double Width,
        double Height,
        string Today,
        string First,
        Locale Language,
        bool Empty);

    /// <summary>[S-3325] One day's bar: its column, the stack its parts stand in, its parts by app and its tooltip as last written.</summary>
    private sealed class Bar(Grid column, StackPanel stack)
    {
        public Grid Column { get; } = column;

        public StackPanel Stack { get; } = stack;

        public KeyedParts<Border> Parts { get; } = new();

        public string? Tip { get; set; }
    }

    /// <summary>
    /// [S-6754] [S-15774] The view owns the card's top edge: its words keep the card's inset over them themselves. The
    /// caption saying why a chart is empty stands over the bars' empty plot at the top, or on the calendar's foot.
    /// </summary>
    private void Edges(bool calendar)
    {
        _nav.Line.Margin = Family.SideInset with { Top = FamilySpace.Inset };
        var top = ScreenTimeChartLayout.Top(_config.Period);
        _note.VerticalAlignment = calendar ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _note.Margin = calendar
            ? Family.SideInset with { Bottom = FamilySpace.S1 }
            : Family.SideInset with { Top = top > 0 ? top : FamilySpace.Inset };
    }

    /// <summary>
    /// [S-3331] [S-6754] [S-15772] [S-15788] One bar per day, oldest left, today right, each split by app in the ring's order
    /// and scaled to the window's busiest day. Each day is a column of the plot from the card's top to its foot, the columns
    /// touching from its left edge to its right (WR-2, WR-3), its bar its column's whole width on the card's foot (no room
    /// between two bars); the first day's and today's names over the bars' foot at the inset
    /// (Caption, tertiary, [S-6717]: the labels stand inside the plot, over it). Builds the columns and the labels;
    /// <see cref="FillBars"/> stands the parts in them.
    /// </summary>
    private Grid Bars(ScreenTimeData data, double height)
    {
        var count = data.Range.Count;
        var chart = new Grid
        {
            Height = height,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // [S-6754] The bars' grid is the widget's plot, the card's whole box: the gap check judges it as a chart's.
        PlotMark.Mark(chart);

        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BarsTop) });
        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BarArea(height)) });

        for (var i = 0; i < count; i++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // The column takes the pointer for its day; its parts stack up from the base, busiest app at the bottom.
            var column = new Grid { Background = Family.Brush(ColourRole.Clear) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };

            // [S-15788] With nothing in the window every day keeps its bar as an empty track (WR-10: "it should show the default
            // values in the graph and not just not show the graph"), the plot's full height.
            if (data.Total <= 0)
            {
                column.Children.Add(new Border
                {
                    Background = Family.Brush(ColourRole.Track),
                    CornerRadius = Corners(true, true),
                    IsHitTestVisible = false,
                });
            }

            column.Children.Add(stack);
            _bars.Add(new Bar(column, stack));

            HoverDay(column, i);
            SetRow(column, 1);
            SetColumn(column, i);
            _columns.Add(column);
            chart.Children.Add(column);
        }

        if (count > 0)
        {
            var first = EndLabel(data.Range[0].Date, count, HorizontalAlignment.Left);
            var last = EndLabel(data.Range[count - 1].Date, count, HorizontalAlignment.Right);
            foreach (var label in new[] { first, last })
            {
                label.VerticalAlignment = VerticalAlignment.Bottom;
                label.IsHitTestVisible = false;
                label.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
                SetRow(label, 1);
                SetColumnSpan(label, count);
                chart.Children.Add(label);
            }
        }

        return chart;
    }

    /// <summary>[S-3331] An end label: a week's weekday ("Fri"), a month's date ("Sep 30").</summary>
    private static TextBlock EndLabel(string date, int days, HorizontalAlignment align)
    {
        var text = LocalDay.TryParse(date, out var day)
            ? days <= 7 ? LocaleFormat.WeekdayName(Strings.Current, day.DayOfWeek, NameWidth.Short) : LocaleFormat.Date(Strings.Current, day.Date, DateStyle.MonthDayShort)
            : date;
        var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, text);
        label.HorizontalAlignment = align;
        label.TextTrimming = TextTrimming.None;
        return label;
    }

    /// <summary>[S-6754] What stands over the bars: the day line under the inset where there is one; none, and the bars reach the top edge.</summary>
    private double BarsTop => ScreenTimeChartLayout.Top(_config.Period);

    /// <summary>[S-15772] The bars' own height: the drawing less what stands over them, to the card's foot (the labels stand over the bars).</summary>
    private double BarArea(double height) => Math.Max(1, height - BarsTop);

    /// <summary>
    /// [S-3331] [S-3325] Each day's parts at their share of the busiest day, the stack's outer corners
    /// the one radius, and the day's tooltip: a part is made only for an app new to its bar, and the
    /// stack is laid again only when its apps or their order changed.
    /// </summary>
    private void FillBars(ScreenTimeData data, double height)
    {
        var area = BarArea(height);
        _parts.Clear();

        for (var i = 0; i < _bars.Count && i < data.Range.Count; i++)
        {
            var bar = _bars[i];
            var day = data.Range[i];
            var parts = ScreenTimeRules.Stack(ScreenTimeRules.Visible(day.Apps, data.Hidden), _segments, data.Busiest);

            // Top down, as the stack draws them: the busiest app at the bottom.
            var keys = new string[parts.Count];
            for (var p = 0; p < parts.Count; p++)
            {
                keys[p] = parts[parts.Count - 1 - p].Key;
            }

            var day1 = i;
            var (blocks, reordered) = bar.Parts.Sync(keys, key => NewPart(key, day1), block => bar.Stack.Children.Remove(block));
            if (reordered)
            {
                bar.Stack.Children.Clear();
                foreach (var block in blocks)
                {
                    bar.Stack.Children.Add(block);
                }
            }

            for (var b = 0; b < blocks.Count; b++)
            {
                var part = parts[parts.Count - 1 - b];
                var block = blocks[b];
                block.Height = Math.Max(FamilyShape.Hairline, part.Fraction * area);
                block.CornerRadius = Corners(b == 0, b == blocks.Count - 1);
                block.Background = ScreenTimeLook.Brush(ScreenTimeRules.ToneOf(_tones, part.Key));
                _parts.Add((new ChartPart(part.Key, i), block));
            }

            var tip = $"{ScreenTimeLook.DayTip(day.Date, Today)} — {ScreenTimeRules.FormatDuration(data.Totals[i].Seconds)}";
            if (tip != bar.Tip)
            {
                bar.Tip = tip;
                ToolTipService.SetToolTip(bar.Column, tip);
            }
        }
    }

    /// <summary>[S-6704] A stack is one rectangle of the family's radius: its top part's top corners, its foot part's foot.</summary>
    private static CornerRadius Corners(bool top, bool foot)
        => new(top ? FamilyShape.Radius : 0, top ? FamilyShape.Radius : 0, foot ? FamilyShape.Radius : 0, foot ? FamilyShape.Radius : 0);

    /// <summary>
    /// [S-3325] [S-15773] An app's part of a day's bar, made the first time the app is in that bar: its hover (that part
    /// alone lights here; the app is the view's hover for the other widgets), its tap (that part picked here, the app
    /// selected on the view) and its hide menu (its tone is set as the bar is filled).
    /// </summary>
    private Border NewPart(string key, int day)
    {
        var part = new ChartPart(key, day);
        var block = new Border();
        block.PointerEntered += (_, _) => HoverPart(part);
        block.PointerExited += (_, _) => HoverPart(null);
        block.PointerCanceled += (_, _) => HoverPart(null);
        block.PointerCaptureLost += (_, _) => HoverPart(null);
        PointerPreview.Register(block, _ => HoverPart(part));
        block.Tapped += (_, e) =>
        {
            e.Handled = true;
            _links.Toggle(key);
            _pickPart = _links.Selected == key ? part : null;
            ApplyFocus();
        };
        ScreenTimeLook.AttachHideMenu(block, key, () => _feed.Source);
        return block;
    }

    /// <summary>[S-15773] The pointer on one part (or off, with null): that part lights here, its app on the view's links.</summary>
    private void HoverPart(ChartPart? part)
    {
        _hoverPart = part;
        if (part is { } on)
        {
            SetDay(on.Day);
        }

        _links.PointerHover(part?.App);
        ApplyFocus();
    }

    /// <summary>
    /// [S-3331] [S-456] [S-15772] Seven day cells wide, padded so the window ends on the last cell: every column is one
    /// weekday, its letter over it in the head row (Caption, tertiary; today's column primary), the seven sharing the card's
    /// width from its left edge to its right. Every week is a row of one fixed height (<see cref="ScreenTimeChartLayout.CellBox"/>,
    /// whole grid cells) whose cells hold a ring of one size, its day's share of the busiest day ([S-6717]: the Chart block's
    /// ring, the accent over its track), centred in its padding; the rows touch from the card's top edge (under the day line
    /// where there is one), the head row first, every week after it, the last one the card reaches cut by its foot.
    /// </summary>
    private Grid Calendar(ScreenTimeData data, double width, double height)
    {
        var cells = ScreenTimeRules.HeatmapCells(data.Totals.Count);
        var weeks = Math.Max(1, cells.Count / ScreenTimeRules.HeatmapColumns);
        var items = ItemGrids.Of(this);
        var box = ScreenTimeChartLayout.CellBox(items);
        var ring = ScreenTimeChartLayout.Ring(items);
        var head = ScreenTimeChartLayout.HeadRow(items);

        var grid = new Grid
        {
            Width = width,
            Height = height,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // [S-15772] A week is seven days: its columns share the card's width from its left edge to its right (WR-3), each
        // edge on a whole device pixel; the rings in them keep their one size.
        foreach (var share in ItemSize.Columns(width, ScreenTimeRules.HeatmapColumns, items))
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BarsTop) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(head) });
        for (var r = 0; r < weeks; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(box) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        const int first = 2;
        var today = Today.Key;
        var labelled = new bool[ScreenTimeRules.HeatmapColumns];
        _letters.Clear();
        _todayColumn = -1;

        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i] is not { } index)
            {
                continue;
            }

            var column = i % ScreenTimeRules.HeatmapColumns;
            var (date, _) = data.Totals[index];
            var mark = new Cell(box, ring);

            SetRow(mark.Box, (i / ScreenTimeRules.HeatmapColumns) + first);
            SetColumn(mark.Box, column);
            HoverDay(mark.Box, index);
            _cells.Add((index, mark));
            _cellTips.Add(null);
            grid.Children.Add(mark.Box);

            if (date == today)
            {
                _todayColumn = column;
            }

            if (!labelled[column] && LocalDay.TryParse(date, out var day))
            {
                labelled[column] = true;
                var label = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, LocaleFormat.WeekdayName(Strings.Current, day.DayOfWeek, NameWidth.Narrow));
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextTrimming = TextTrimming.None;

                // [S-14314] The letter's box is its column's head: the column across, the head row down.
                var letter = new Border { Child = label, IsHitTestVisible = false };
                HeadMark.Mark(letter);
                SetRow(letter, 1);
                SetColumn(letter, column);
                grid.Children.Add(letter);
                _letters.Add((column, label));
            }
        }

        foreach (var (column, label) in _letters)
        {
            label.Foreground = Family.Brush(column == _todayColumn ? ColourRole.TextPrimary : ColourRole.TextTertiary);
        }

        return grid;
    }

    /// <summary>The calendar's weekday letters by column, and today's column.</summary>
    private readonly List<(int Column, TextBlock Label)> _letters = [];
    private int _todayColumn = -1;

    /// <summary>
    /// [S-3331] [S-11505] [S-15772] One calendar cell: a box of one fixed size, the item (it takes the pointer for its day and
    /// lights under it), and the ring of its day's share standing in its padding.
    /// </summary>
    private sealed class Cell
    {
        private readonly ChartRing _ring = new();

        public Cell(double box, double ring)
        {
            Box = new Grid { Height = box, Background = Family.Brush(ColourRole.Clear) };
            _ring.Width = ring;
            _ring.Height = ring;
            _ring.HorizontalAlignment = HorizontalAlignment.Center;
            _ring.VerticalAlignment = VerticalAlignment.Center;
            Box.Children.Add(_ring);
            ItemMark.Mark(Box);
        }

        public Grid Box { get; }

        public void Show(double share) => _ring.Show(share);
    }

    /// <summary>[S-3325] Each heat cell's tooltip, written only when it says something else.</summary>
    private void TipCells(ScreenTimeData data)
    {
        for (var c = 0; c < _cells.Count; c++)
        {
            var (index, mark) = _cells[c];
            var box = mark.Box;
            if (index >= data.Totals.Count)
            {
                continue;
            }

            var (date, seconds) = data.Totals[index];
            var tip = $"{ScreenTimeLook.DayTip(date, Today)} — {ScreenTimeRules.FormatDuration(seconds)}";
            if (tip != _cellTips[c])
            {
                _cellTips[c] = tip;
                ToolTipService.SetToolTip(box, tip);
            }
        }
    }

    /// <summary>[S-11924] [S-15773] A day's column or cell: the pointer on it lights it, edge to edge (the item layer's row fill), and points at its day.</summary>
    private void HoverDay(Panel element, int index)
    {
        ItemFill.Paint(element, ItemRole.Row);
        element.PointerEntered += (_, _) => { ItemFill.Paint(element, ItemRole.Row, ItemState.Hover); SetDay(index); };
        element.PointerExited += (_, _) => { ItemFill.Paint(element, ItemRole.Row); SetDay(null); };
        element.PointerCanceled += (_, _) => { ItemFill.Paint(element, ItemRole.Row); SetDay(null); };
        element.PointerCaptureLost += (_, _) => { ItemFill.Paint(element, ItemRole.Row); SetDay(null); };
        PointerPreview.Register(element, pressed =>
        {
            ItemFill.Paint(element, ItemRole.Row, pressed ? ItemState.Pressed : ItemState.Hover);
            SetDay(index);
        });
    }

    private void SetDay(int? index)
    {
        if (_day == index)
        {
            return;
        }

        _day = index;
        PublishDay();
        ApplyFocus();
    }

    /// <summary>[S-11924] [S-3307] The day pointed at here (the pointer, or Left / Right) is the view's day under the pointer.</summary>
    private void PublishDay()
        => _dates.Set(LinkTopics.HoveredDate, _day is { } index && _data is { } data && index < data.Range.Count && LocalDay.TryParse(data.Range[index].Date, out var day)
            ? new DateLink(day.Date)
            : null);

    /// <summary>[S-11924] The day lit: this chart's own, else the one pointed at elsewhere when the period holds it.</summary>
    private int? LitDay => _day ?? (_linkedDay is { } link && _data is { } data ? ScreenTimeRules.DayIndex(data.Range, link.Date) : null);

    // ---- focus ([S-3333]) -----------------------------------------------------------------------

    /// <summary>[S-3333] Light the focused app's part of every bar (or shade the calendar by it), and say it over the drawing.</summary>
    private void ApplyFocus()
    {
        if (_wiring.Detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);
        var pointed = ScreenTimeRules.FocusPart(_links.Pointed, _segments, data.Apps);
        var picked = ScreenTimeRules.FocusPart(_links.Picked, _segments, data.Apps);

        // [S-15773] One part lights: the one under this chart's pointer (or clicked here); from the view's links only the
        // part that is both the app and the day pointed at, never that app on every day (WR-13).
        if (_pickPart is { } mine && _links.Selected != mine.App)
        {
            _pickPart = null;
        }

        var lit = LitDay;
        var pointedPart = _hoverPart ?? (pointed is { } app && lit is { } on ? new ChartPart(app, on) : null);
        var pickedPart = _pickPart ?? (picked is { } chosen && lit is { } at ? new ChartPart(chosen, at) : null);
        foreach (var (key, block) in _parts)
        {
            block.Opacity = ScreenTimeChartLayout.PartStrength(ScreenTimeRules.ToneOf(_tones, key.App).Alpha, key, pointedPart, pickedPart);
        }

        // [S-3312] [S-11902] A day pointed at (pointer or Left / Right): every other day at the links' "less".
        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Opacity = DayStrength(i);
        }

        ShadeCells(data, part);
    }

    /// <summary>[S-456] [S-3333] Each day's ring at its share of the busiest day, of the whole or of the focused part.</summary>
    private void ShadeCells(ScreenTimeData data, string? part)
    {
        if (_cells.Count == 0)
        {
            return;
        }

        var seconds = data.Range.Select(day => PartSeconds(data, day, part)).ToList();
        var busiest = seconds.Count == 0 ? 0 : seconds.Max();

        foreach (var (index, mark) in _cells)
        {
            mark.Show(busiest > 0 ? Math.Min(1, (double)seconds[index] / busiest) : 0);
            mark.Box.Opacity = DayStrength(index);
        }
    }

    /// <summary>[S-11902] A day's column or cell against the day pointed at: whole, or the others at 0.6.</summary>
    private double DayStrength(int index) => LitDay is { } d && d != index ? LinkEmphasis.Others(pointed: true, picked: false) : 1;

    /// <summary>A day's seconds of one part: an app, "Other" (the apps off the ring), or the whole day.</summary>
    private long PartSeconds(ScreenTimeData data, ScreenTimeDay day, string? part)
    {
        var apps = ScreenTimeRules.Visible(day.Apps, data.Hidden);

        if (part is null)
        {
            return ScreenTimeRules.Sum(apps);
        }

        if (part == ScreenTimeRules.OtherKey)
        {
            var named = _segments.Where(s => !s.IsOther).Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
            return ScreenTimeRules.Sum(apps.Where(app => !named.Contains(app.App)));
        }

        return ScreenTimeRules.Sum(apps.Where(app => app.App == part));
    }

    // ---- keys ([S-3307]) ---------------------------------------------------------------------------

    /// <summary>[S-3307] The chart takes the keyboard on a click: there are apps and days to step through.</summary>
    public bool WantsKeys => !_wiring.Detached && _segments.Count > 0;

    /// <summary>
    /// [S-3307] Up/Down, Page Up/Down, Home/End step through the apps in the ring's order and light
    /// each in every screen-time widget on the view; Enter/Space select; Left/Right step the
    /// day lit (the others at their wash); the Menu key hides the app; Escape lets the cursor and the day go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_wiring.Detached || _data is not { } data)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            var count = data.Range.Count;
            _day = key == WidgetKey.Left
                ? Math.Max(0, (_day ?? count) - 1)
                : Math.Min(count - 1, (_day ?? -1) + 1);
            PublishDay();
            ApplyFocus();
            return true;
        }

        if (key == WidgetKey.Escape && _day is not null)
        {
            _day = null;
            PublishDay();
            _links.HandleKey(key, [], _ => null, _feed.Source);
            ApplyFocus();
            return true;
        }

        var used = _links.HandleKey(key, [.. _segments.Select(s => s.Key)], app => _parts.Find(p => p.Part.App == app).Block, _feed.Source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}

/// <summary>
/// [S-15774] What the chart draws with no recording at all (WR-10: the widget keeps its face and says why): the window's
/// days as days with nothing in them, ending today, read by the same rules as a real history.
/// </summary>
internal sealed class NoRecording : IScreenTimeSource
{
    public static NoRecording Instance { get; } = new();

    public LocalDay Today => LocalDay.From(DateTimeOffset.Now);

    public ScreenTimePreferences Preferences { get; } = ScreenTimePreferences.From(null, 0, null);

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public ScreenTimeDay Day(string? date) => new(date ?? Today.Key, 0, [], 0);

    public IReadOnlyList<ScreenTimeDay> Range(int days)
    {
        var today = Today;
        return [.. Enumerable.Range(0, Math.Max(0, days)).Select(back => Day(today.AddDays(back - days + 1).Key))];
    }

    public void SetPreferences(ScreenTimePreferences preferences)
    {
        // ignore: with no recording there is nothing to keep preferences for
    }
}
