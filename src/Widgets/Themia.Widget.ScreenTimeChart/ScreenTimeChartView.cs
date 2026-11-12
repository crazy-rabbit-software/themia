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
using Themia.Widget.ScreenTimeChart;
using Glyphs = Themia.App.Shell.Glyphs;

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
internal sealed partial class ScreenTimeChartView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    private readonly ScreenTimeLinks _links;

    /// <summary>[S-454] The header names the window ("Last 7 days"): the day navigation's label alone.</summary>
    private readonly ScreenTimeDayNav _nav;

    private readonly Grid _drawing = new() { FlowDirection = FlowDirection.LeftToRight, Margin = Family.SideInset };

    /// <summary>[S-6714] Nothing to draw: the Status block's one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };

    /// <summary>Every app part of every bar, by app key, so a focus can light them all at once.</summary>
    private readonly List<(string Key, Border Part)> _parts = [];

    /// <summary>Every calendar cell (its ring or dot), with the day it is.</summary>
    private readonly List<(int Day, Cell Mark)> _cells = [];

    private readonly List<Grid> _columns = [];

    /// <summary>[S-3325] Each bar's own parts, kept by app; one per day, oldest first.</summary>
    private readonly List<Bar> _bars = [];

    /// <summary>[S-3325] Each heat cell's tooltip as last written, by cell.</summary>
    private readonly List<string?> _cellTips = [];

    /// <summary>[S-3325] What the drawing on screen was built for; null when there is none.</summary>
    private Frame? _frame;

    private IScreenTimeSource? _source;
    private IWidgetSurface? _surface;
    private ScreenTimeChartConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];

    /// <summary>[S-2272] Each drawn app's tone by its rank in the period.</summary>
    private IReadOnlyDictionary<string, ScreenTimeTone> _tones = new Dictionary<string, ScreenTimeTone>();
    private WidgetArea _area;
    private int? _day;
    private bool _detached;

    private readonly IWidgetHost _host;

    public ScreenTimeChartView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus, LinkTopics.HoveredApp, LinkTopics.SelectedApp);
        _nav = new ScreenTimeDayNav(Render);

        Background = Family.Brush(ColourRole.Clear);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        _drawing.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_drawing);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _nav.Host;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ScreenTimeChartConfig ?? new ScreenTimeChartConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        UseSource(_host.ScreenTime);
        _links.Join(_surface?.Links);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeChartConfig ?? new ScreenTimeChartConfig();
        Render();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        UseSource(null);
        _links.Leave();
        Clear();
    }

    private void UseSource(IScreenTimeSource? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        if (_source is not null)
        {
            _source.Changed -= Render;
        }

        _source = source;

        if (_source is not null)
        {
            _source.Changed += Render;
        }
    }

    private void OnLanguage(Locale locale) => Render();

    private ScreenTimePeriod Period => ScreenTimeRules.ChartPeriod(_config.Period);

    private ScreenTimeChartStyle ChartStyle => _config.Style;

    private LocalDay Today => _source?.Today ?? LocalDay.From(DateTimeOffset.Now);

    // ---- drawing ----------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        // [S-454] The header names the window, "Last 7 days".
        _nav.Update(Period);

        if (_source is null)
        {
            ShowEmpty(Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_source, Period, null);

        if (_data.Total <= 0)
        {
            ShowEmpty(Str.WidgetsC_ScreenTime_NoActivityPeriod.Text());
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _drawing.Visibility = Visibility.Visible;

        if (_day is { } day && day >= _data.Range.Count)
        {
            _day = null;
        }

        Draw(_data);
    }

    private void ShowEmpty(string message)
    {
        _data = null;
        _segments = [];
        Clear();
        _empty.Show(StatusKind.Empty, Glyphs.ScreenTimeChart, message);
        _empty.Visibility = Visibility.Visible;
        _drawing.Visibility = Visibility.Collapsed;
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

        var width = Math.Max(1, _area.Width - (2 * FamilySpace.Inset));
        var height = ScreenTimeChartLayout.Drawing(ChartStyle, _config.Period);
        _drawing.Height = height;

        // [S-3325] Built once for what it is made of; every other read moves what is there.
        var frame = new Frame(ChartStyle, data.Range.Count, width, height, Today.Key, data.Range.Count > 0 ? data.Range[0].Date : string.Empty, Strings.Current);
        if (frame != _frame)
        {
            Clear();
            _frame = frame;
            _drawing.Children.Add(ChartStyle == ScreenTimeChartStyle.Calendar ? Calendar(data, width, height) : Bars(data, height));
        }

        if (ChartStyle == ScreenTimeChartStyle.Calendar)
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
        Locale Language);

    /// <summary>[S-3325] One day's bar: its column, the stack its parts stand in, its parts by app and its tooltip as last written.</summary>
    private sealed class Bar(Grid column, StackPanel stack)
    {
        public Grid Column { get; } = column;

        public StackPanel Stack { get; } = stack;

        public KeyedParts<Border> Parts { get; } = new();

        public string? Tip { get; set; }
    }

    /// <summary>
    /// [S-3331] One bar per day, oldest left, today right, 8 apart for a week and 4 for a month, each
    /// split by app in the ring's order and scaled to the window's busiest day; the first day's and
    /// today's names at the two ends under them (Caption, tertiary, [S-6717]). Builds the columns and
    /// the labels; <see cref="FillBars"/> stands the parts in them.
    /// </summary>
    private Grid Bars(ScreenTimeData data, double height)
    {
        var count = data.Range.Count;
        var chart = new Grid
        {
            ColumnSpacing = ScreenTimeChartLayout.BarGap(count),
            Height = height,
            VerticalAlignment = VerticalAlignment.Top,
        };

        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(BarArea(height)) });
        chart.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < count; i++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // The column takes the pointer for its day; its parts stack up from the base, busiest app at the bottom.
            var column = new Grid { Background = Family.Brush(ColourRole.Clear) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            column.Children.Add(stack);
            _bars.Add(new Bar(column, stack));

            HoverDay(column, i);
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

    /// <summary>The bars' own height: the drawing less the end labels' row.</summary>
    private static double BarArea(double height) => Math.Max(1, height - ScreenTimeChartLayout.LabelRow);

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

            var (blocks, reordered) = bar.Parts.Sync(keys, NewPart, block => bar.Stack.Children.Remove(block));
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
                _parts.Add((part.Key, block));
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

    /// <summary>[S-3325] An app's part of a bar, made the first time the app is in that bar: its hover, tap and hide menu (its tone is set as the bar is filled).</summary>
    private Border NewPart(string key)
    {
        var block = new Border();
        block.PointerEntered += (_, _) => HoverApp(key);
        block.PointerExited += (_, _) => HoverApp(null);
        block.PointerCanceled += (_, _) => HoverApp(null);
        block.PointerCaptureLost += (_, _) => HoverApp(null);
        block.Tapped += (_, e) =>
        {
            e.Handled = true;
            _links.Toggle(key);
            ApplyFocus();
        };
        ScreenTimeLook.AttachHideMenu(block, key, () => _source);
        return block;
    }

    /// <summary>
    /// [S-3331] [S-456] Seven cells wide, 4 apart, padded so the window ends on the last cell: every
    /// column is one weekday, its letter over it (Caption, tertiary; today's column primary) while there
    /// is room. A cell is a ring of its day's share of the busiest day ([S-6717]: the Chart block's ring,
    /// the accent over its track) from 16, a dot under it (the accent for any use, the track for none);
    /// a drawing too short for every week shows the latest weeks.
    /// </summary>
    private Grid Calendar(ScreenTimeData data, double width, double height)
    {
        var cells = ScreenTimeRules.HeatmapCells(data.Totals.Count);
        var weeks = Math.Max(1, cells.Count / ScreenTimeRules.HeatmapColumns);
        var cell = ScreenTimeChartLayout.Cell;

        // [S-11505] The columns share the width; each ring keeps its one size, centred in its column.
        var grid = new Grid
        {
            ColumnSpacing = FamilySpace.S1,
            RowSpacing = FamilySpace.S1,
            Width = width,
            VerticalAlignment = VerticalAlignment.Top,
        };

        for (var c = 0; c < ScreenTimeRules.HeatmapColumns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FamilyType.Caption.LineHeight) });
        for (var r = 0; r < weeks; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
        }

        const int first = 1;
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
            var mark = new Cell(cell);

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
                label.TextTrimming = TextTrimming.None;
                SetRow(label, 0);
                SetColumn(label, column);
                grid.Children.Add(label);
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
    /// [S-3331] [S-11505] One calendar cell: a ring of its day's share (the Chart block's), centred in its
    /// column. Its box takes the pointer for its day.
    /// </summary>
    private sealed class Cell
    {
        private readonly ChartRing _ring = new();

        public Cell(double side)
        {
            Box = new Grid { Width = side, Height = side, HorizontalAlignment = HorizontalAlignment.Center, Background = Family.Brush(ColourRole.Clear) };
            Box.Children.Add(_ring);
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

    private void HoverDay(FrameworkElement element, int index)
    {
        element.PointerEntered += (_, _) => SetDay(index);
        element.PointerExited += (_, _) => SetDay(null);
        element.PointerCanceled += (_, _) => SetDay(null);
        element.PointerCaptureLost += (_, _) => SetDay(null);
    }

    private void SetDay(int? index)
    {
        if (_day == index)
        {
            return;
        }

        _day = index;
        ApplyFocus();
    }

    private void HoverApp(string? key)
    {
        if (_links.PointerHover(key))
        {
            ApplyFocus();
        }
    }

    // ---- focus ([S-3333]) -----------------------------------------------------------------------

    /// <summary>[S-3333] Light the focused app's part of every bar (or shade the calendar by it), and say it over the drawing.</summary>
    private void ApplyFocus()
    {
        if (_detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);
        var pointed = ScreenTimeRules.FocusPart(_links.Pointed, _segments, data.Apps);
        var picked = ScreenTimeRules.FocusPart(_links.Picked, _segments, data.Apps);

        foreach (var (key, block) in _parts)
        {
            block.Opacity = ScreenTimeRules.Opacity(ScreenTimeRules.ToneOf(_tones, key), key, pointed, picked);
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
    private double DayStrength(int index) => _day is { } d && d != index ? LinkEmphasis.Others(pointed: true, picked: false) : 1;

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
    public bool WantsKeys => !_detached && _segments.Count > 0;

    /// <summary>
    /// [S-3307] Up/Down, Page Up/Down, Home/End step through the apps in the ring's order and light
    /// each in every screen-time widget on the view; Enter/Space select; Left/Right step the
    /// day lit (the others at their wash); the Menu key hides the app; Escape lets the cursor and the day go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _data is not { } data)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            var count = data.Range.Count;
            _day = key == WidgetKey.Left
                ? Math.Max(0, (_day ?? count) - 1)
                : Math.Min(count - 1, (_day ?? -1) + 1);
            ApplyFocus();
            return true;
        }

        if (key == WidgetKey.Escape && _day is not null)
        {
            _day = null;
            _links.HandleKey(key, [], _ => null, _source);
            ApplyFocus();
            return true;
        }

        var used = _links.HandleKey(key, [.. _segments.Select(s => s.Key)], app => _parts.Find(p => p.Key == app).Part, _source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}
