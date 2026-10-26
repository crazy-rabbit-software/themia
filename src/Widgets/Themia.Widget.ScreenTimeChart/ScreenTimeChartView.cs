using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeChart;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3330] The last week or month a day at a time: one bar per day stacked by app (the busiest
/// seven and "Other", in the same colours as the donut and the list), or a calendar shaded by use,
/// with the period's figure over it. Hovering or selecting an app anywhere on the view lights its
/// part of every bar and dims the rest; the calendar then shades that app's days ([S-3333]).
/// <para>
/// [S-3325] The drawing is built when what it is made of changes (the style, the days, the plan, the
/// size, today, the language) and otherwise moved in place: a read that adds thirty seconds to today
/// sets one bar part's height and a tooltip, and builds nothing.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeChartView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    private readonly ScreenTimeLinks _links;
    private readonly TextBlock _period = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _readout = new() { Margin = new Thickness(ScreenTimeLayout.Inset, 0, ScreenTimeLayout.Inset, 0) };
    private readonly TextBlock _total = Fluent.Text(FluentType.BodyStrong);
    private readonly TextBlock _detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Grid _drawing = new() { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(ScreenTimeLayout.Inset, 0, ScreenTimeLayout.Inset, ScreenTimeLayout.Gap) };
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    /// <summary>Every app part of every bar, by app key, so a focus can light them all at once.</summary>
    private readonly List<(string Key, Border Part)> _parts = [];

    /// <summary>Every heat cell's fill, with the day it is.</summary>
    private readonly List<(int Day, Border Shade, Border Box)> _cells = [];

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
    private ScreenTimeChartPlan _plan;
    private WidgetArea _area;
    private int? _day;
    private bool _detached;

    private readonly IWidgetHost _host;

    public ScreenTimeChartView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus);

        Background = new SolidColorBrush(Colors.Transparent);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        _period.VerticalAlignment = VerticalAlignment.Center;
        _period.Margin = new Thickness(0, 0, Fluent.Space2, 0);
        _total.Foreground = ScreenTimeLook.Primary;
        _total.TextTrimming = TextTrimming.CharacterEllipsis;
        Typography.SetNumeralAlignment(_total, FontNumeralAlignment.Tabular);
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _readout.Children.Add(_total);
        _readout.Children.Add(_detail);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowSpacing = ScreenTimeLayout.Gap;
        SetRow(_drawing, 1);

        _empty = Fluent.EmptyState(Glyphs.ScreenTimeChart, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;
        SetRowSpan(_empty, 2);

        Children.Add(_readout);
        Children.Add(_drawing);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _period;

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
        _period.Text = ScreenTimeRules.PeriodLabel(Period);
        _period.Visibility = _area.IsMeasured && _area.Width < 170 ? Visibility.Collapsed : Visibility.Visible;

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
        _emptyText.Text = message;
        _empty.Visibility = Visibility.Visible;
        _drawing.Visibility = Visibility.Collapsed;
        _readout.Visibility = Visibility.Collapsed;
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

    /// <summary>[S-3331] [S-3332] The figure over the drawing when it fits, and the drawing in what is left.</summary>
    private void Draw(ScreenTimeData data)
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        _plan = ScreenTimeChartLayout.Chart(_area, data.Range.Count, ChartStyle);
        _segments = ScreenTimeRules.Segments(data.Apps, data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());

        _readout.Visibility = _plan.Total is null ? Visibility.Collapsed : Visibility.Visible;
        if (_plan.Total is { } step)
        {
            _total.Apply(step);
        }

        _detail.Visibility = _plan.Detail ? Visibility.Visible : Visibility.Collapsed;

        var width = Math.Max(1, _area.Width - (2 * ScreenTimeLayout.Inset));
        var height = Math.Max(1, _plan.Drawing);

        // [S-3325] Built once for what it is made of; every other read moves what is there.
        var frame = new Frame(ChartStyle, data.Range.Count, _plan, width, height, Today.Key, data.Range.Count > 0 ? data.Range[0].Date : string.Empty, Strings.Current);
        if (frame != _frame)
        {
            Clear();
            _frame = frame;
            _drawing.Children.Add(ChartStyle == ScreenTimeChartStyle.Calendar ? Heatmap(data, width, height) : Bars(data, width, height));
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
        ScreenTimeChartPlan Plan,
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
    /// [S-3331] One bar per day, oldest left, today right, each split by app in the ring's order and
    /// scaled to the window's busiest day; weekday names under a week's bars when they fit. Builds the
    /// columns and their labels; <see cref="FillBars"/> stands the parts in them.
    /// </summary>
    private Grid Bars(ScreenTimeData data, double width, double height)
    {
        var count = data.Range.Count;
        var area = BarArea(height);
        var today = Today.Key;

        var chart = new Grid
        {
            ColumnSpacing = count <= 7 ? Fluent.Space2 : 2,
            Height = height,
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        for (var i = 0; i < count; i++)
        {
            var day = data.Range[i];
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var column = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(area) });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Parts stack up from the base, busiest app at the bottom.
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            column.Children.Add(stack);
            _bars.Add(new Bar(column, stack));

            if (_plan.Labels && LocalDay.TryParse(day.Date, out var date))
            {
                var label = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                label.Text = width / count >= 36
                    ? LocaleFormat.WeekdayName(Strings.Current, date.DayOfWeek, NameWidth.Short)
                    : LocaleFormat.WeekdayName(Strings.Current, date.DayOfWeek, NameWidth.Narrow);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.TextTrimming = TextTrimming.None;
                label.Margin = new Thickness(0, 2, 0, 0);

                if (day.Date == today)
                {
                    label.Foreground = ScreenTimeLook.AccentText;
                }

                SetRow(label, 1);
                column.Children.Add(label);
            }

            HoverDay(column, i);
            SetColumn(column, i);
            _columns.Add(column);
            chart.Children.Add(column);
        }

        return chart;
    }

    /// <summary>The bars' own height: the drawing less the weekday row.</summary>
    private double BarArea(double height) => Math.Max(1, height - (_plan.Labels ? FluentType.Caption.LineHeight + 2 : 0));

    /// <summary>
    /// [S-3331] [S-3325] Each day's parts at their share of the busiest day, the top one rounded, and
    /// the day's tooltip: a part is made only for an app new to its bar, and the stack is laid again
    /// only when its apps or their order changed.
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
                block.Height = Math.Max(1, part.Fraction * area);
                block.CornerRadius = b == 0 ? new CornerRadius(2, 2, 0, 0) : new CornerRadius(0);
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

    /// <summary>[S-3325] An app's part of a bar, made the first time the app is in that bar: its colour, hover, tap and hide menu.</summary>
    private Border NewPart(string key)
    {
        var block = new Border { Background = ScreenTimeLook.For(key) };
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
    /// [S-3331] Seven cells wide and padded so the window ends on the last cell: every column is one
    /// weekday. A cell's shade is its quartile of the busiest day — of the focused app's busiest day
    /// while one is focused ([S-3333]) — in the accent, so it re-themes.
    /// </summary>
    private Grid Heatmap(ScreenTimeData data, double width, double height)
    {
        var cells = ScreenTimeRules.HeatmapCells(data.Totals.Count);
        var rows = Math.Max(1, cells.Count / ScreenTimeRules.HeatmapColumns);
        var gap = 3.0;
        var header = FluentType.Caption.LineHeight + 2;
        var cell = Math.Min((width - (gap * 6)) / 7, (height - header - (gap * rows)) / rows);

        // Too small to letter: the weekday row goes, and the cells take its height.
        if (cell < 12)
        {
            header = 0;
            cell = Math.Min((width - (gap * 6)) / 7, (height - (gap * (rows - 1))) / rows);
        }

        // A week of cells stays a row of chips rather than stretching into slabs.
        cell = Math.Clamp(cell, 4, rows <= 2 ? 28 : 40);
        gap = cell < 8 ? 2 : gap;

        var grid = new Grid
        {
            ColumnSpacing = gap,
            RowSpacing = gap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };

        for (var c = 0; c < ScreenTimeRules.HeatmapColumns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var r = 0; r < rows; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell) });
        }

        var today = Today.Key;
        var labelled = new bool[ScreenTimeRules.HeatmapColumns];

        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i] is not { } index)
            {
                continue;
            }

            var column = i % ScreenTimeRules.HeatmapColumns;
            var (date, _) = data.Totals[index];

            // The shade is the inner fill's own opacity — the brush is the shared accent and must
            // stay whole — and the outer box is what a hovered day lights.
            var shade = new Border { CornerRadius = new CornerRadius(Math.Min(4, cell / 4)) };
            var box = new Border { Child = shade, CornerRadius = shade.CornerRadius, Background = new SolidColorBrush(Colors.Transparent) };

            if (date == today)
            {
                box.BorderBrush = ScreenTimeLook.Primary;
                box.BorderThickness = new Thickness(Fluent.Hairline);
            }

            SetRow(box, (i / ScreenTimeRules.HeatmapColumns) + 1);
            SetColumn(box, column);
            HoverDay(box, index);
            _cells.Add((index, shade, box));
            _cellTips.Add(null);
            grid.Children.Add(box);

            if (header > 0 && !labelled[column] && LocalDay.TryParse(date, out var day))
            {
                labelled[column] = true;
                var label = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                label.Text = LocaleFormat.WeekdayName(Strings.Current, day.DayOfWeek, NameWidth.Narrow);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.TextTrimming = TextTrimming.None;
                SetRow(label, 0);
                SetColumn(label, column);
                grid.Children.Add(label);
            }
        }

        return grid;
    }

    /// <summary>[S-3325] Each heat cell's tooltip, written only when it says something else.</summary>
    private void TipCells(ScreenTimeData data)
    {
        for (var c = 0; c < _cells.Count; c++)
        {
            var (index, _, box) = _cells[c];
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

        foreach (var (key, block) in _parts)
        {
            block.Opacity = ScreenTimeLook.Opacity(ScreenTimeRules.Emphasis(key, part));
        }

        for (var i = 0; i < _columns.Count; i++)
        {
            _columns[i].Opacity = _day is { } d && d != i ? 0.6 : 1;
        }

        ShadeCells(data, part);
        Readout(data, part);
    }

    /// <summary>[S-456] [S-3333] Quartiles of the busiest day, of the whole or of the focused part.</summary>
    private void ShadeCells(ScreenTimeData data, string? part)
    {
        if (_cells.Count == 0)
        {
            return;
        }

        var seconds = data.Range.Select(day => PartSeconds(data, day, part)).ToList();
        var busiest = seconds.Count == 0 ? 0 : seconds.Max();

        foreach (var (index, shade, box) in _cells)
        {
            var level = ScreenTimeRules.HeatLevel(seconds[index], busiest);
            shade.Background = level == 0 ? ScreenTimeLook.EmptyCell : ScreenTimeLook.Accent;
            shade.Opacity = level == 0 ? 1 : 0.25 + (0.75 * (level - 1) / 3);
            box.Opacity = _day is { } d && d != index ? 0.6 : 1;
        }
    }

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

    /// <summary>
    /// [S-3332] [S-3333] The figure: the window's total; a hovered day's; the focused app's over the
    /// window (or on the hovered day). The line under it says what the figure is.
    /// </summary>
    private void Readout(ScreenTimeData data, string? part)
    {
        var name = part is null ? null : _segments.FirstOrDefault(s => s.Key == part)?.Name;
        long seconds;
        string line;
        var over = false;

        if (_day is { } index && index < data.Range.Count)
        {
            var day = data.Range[index];
            seconds = PartSeconds(data, day, part);
            var label = ScreenTimeLook.DayTip(day.Date, Today);
            line = name is null ? label : $"{name} · {label}";
        }
        else if (part is not null)
        {
            seconds = data.Range.Sum(day => PartSeconds(data, day, part));
            line = $"{name} · {ScreenTimeLook.Percent(seconds, data.Total)}";
        }
        else
        {
            seconds = data.Total;
            var average = ScreenTimeRules.DailyAverage(data.Total, data.Days);
            var parts = new List<string> { Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(average))) };

            // [S-457] A window judges its daily average against the daily limit.
            if (ScreenTimeRules.Goal(average, data.DayLimit) is { } goal)
            {
                parts.Add(ScreenTimeChartStr.Screentime_GoalPerDay.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))));
                over = goal.Over;
            }

            line = string.Join(" · ", parts);
        }

        _total.Text = ScreenTimeRules.FormatDuration(seconds);
        _total.Foreground = over ? ScreenTimeLook.Critical : ScreenTimeLook.Primary;
        _detail.Text = line;
    }

    // ---- keys ([S-3307]) ---------------------------------------------------------------------------

    /// <summary>[S-3307] The chart takes the keyboard on a click: there are apps and days to step through.</summary>
    public bool WantsKeys => !_detached && _segments.Count > 0;

    /// <summary>
    /// [S-3307] Up/Down, Page Up/Down, Home/End step through the apps in the ring's order and light
    /// each in every screen-time widget on the view; Enter/Space select; Left/Right step the day the
    /// figure speaks of; the Menu key hides the app; Escape lets the cursor and the day go.
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
