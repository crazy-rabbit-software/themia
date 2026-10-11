using System.Globalization;
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
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-450] Where the day went: the total, drawn as a ring of apps (or a bar, a stack of days, a
/// calendar), with the apps listed beside or under it.
/// <para>
/// The history is not this widget's: it is the app's one tracker ([S-458]), read through
/// <see cref="AppServices.ScreenTime"/>, so every screen-time widget shows the same numbers and
/// removing one deletes nothing. The widget redraws when the tracker says something changed —
/// every sampling tick, a resolved name — and when the user moves through the days.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    /// <summary>A legend row: Fluent's compact list row.</summary>
    private const double RowHeight = Fluent.RowHeightCompact;

    /// <summary>From this width the drawing and the legend sit side by side.</summary>
    private const double SideBySideWidth = 300;

    /// <summary>Below this width the header has room for the widget's name and nothing else.</summary>
    private const double NavHidesBelow = 170;

    private static readonly string ChevronLeft = char.ConvertFromUtf32(0xE76B);
    private static readonly string ChevronRight = char.ConvertFromUtf32(0xE76C);

    private static readonly Brush PrimaryBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private static readonly Brush SecondaryBrush = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
    private static readonly Brush CriticalBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private static readonly Brush OtherBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);

    private readonly Grid _body = new();
    private readonly Grid _drawing = new();
    private readonly StackPanel _readout = new() { Spacing = 0 };
    private readonly TextBlock _total = Fluent.Text(FluentType.Subtitle);
    private readonly TextBlock _detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _legend = new();
    private readonly List<LegendRow> _rows = [];
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly StackPanel _nav = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly Button _back;
    private readonly Button _forward;
    private readonly Button _today;
    private readonly TextBlock _navLabel = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Dictionary<string, Brush> _appBrushes = new(StringComparer.Ordinal);

    /// <summary>What the drawing is made of, by key, so hovering one part can dim the others.</summary>
    private readonly List<(string Key, UIElement Element)> _parts = [];

    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeConfig _config = new();
    private string? _pinned;
    private string? _focus;
    private Model _model = Model.Empty;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private WidgetArea _area;
    private bool _detached;

    public ScreenTimeView()
    {
        _total.Foreground = PrimaryBrush;
        Typography.SetNumeralAlignment(_total, FontNumeralAlignment.Tabular);
        _total.TextTrimming = TextTrimming.CharacterEllipsis;
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _readout.Children.Add(_total);
        _readout.Children.Add(_detail);

        _emptyText.Text = Str.Screentime_NoActivity.Text();
        _empty = Fluent.EmptyState(Glyphs.ScreenTime, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        // [S-454] The day navigation rides on the card's header, the way the calendar's does.
        _back = NavButton(ChevronLeft, Str.Screentime_PrevDay.Text(), () => Navigate(-1));
        _forward = NavButton(ChevronRight, Str.Screentime_NextDay.Text(), () => Navigate(1));
        _today = NavButton(Glyphs.Calendar, Str.Screentime_BackToToday.Text(), () => { _pinned = null; Render(); });
        _navLabel.VerticalAlignment = VerticalAlignment.Center;
        _navLabel.TextAlignment = TextAlignment.Center;
        _navLabel.Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0);
        _nav.Children.Add(_back);
        _nav.Children.Add(_navLabel);
        _nav.Children.Add(_forward);
        _nav.Children.Add(_today);

        _footer.VerticalAlignment = VerticalAlignment.Bottom;

        Children.Add(_body);
        Children.Add(_footer);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _nav;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-450] No timer of its own: the tracker ticks, and says so.
        if (_source is null)
        {
            _source = AppServices.ScreenTime;

            if (_source is not null)
            {
                _source.Changed += Render;
            }
        }

        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeConfig ?? new ScreenTimeConfig();
        Render();
    }

    /// <summary>[S-1110] The header buttons' tooltips again; <see cref="Render"/> re-reads every other text.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_back, Str.Screentime_PrevDay.Text());
        ToolTipService.SetToolTip(_forward, Str.Screentime_NextDay.Text());
        ToolTipService.SetToolTip(_today, Str.Screentime_BackToToday.Text());
        Render();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        if (_source is not null)
        {
            _source.Changed -= Render;
            _source = null;
        }

        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
        _legend.Children.Clear();
    }

    private void Navigate(int delta)
    {
        if (_source is null)
        {
            return;
        }

        _pinned = ScreenTimeRules.Step(_pinned, _source.Today, delta);
        _focus = null;
        Render();
    }

    private static Button NavButton(string glyph, string tooltip, Action click)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = FluentType.Caption.FontSize },
            Padding = new Thickness(Fluent.Space1 + 2, Fluent.Space1, Fluent.Space1 + 2, Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // A disabled arrow is dimmed, not boxed: the stock disabled fill would draw a grey tile
        // around the one arrow that cannot be pressed.
        var clear = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        button.Resources["ButtonBackgroundDisabled"] = clear;
        button.Resources["ButtonBorderBrushDisabled"] = clear;

        ToolTipService.SetToolTip(button, tooltip);
        button.Click += (_, _) => click();
        return button;
    }

    // ---- what is shown -----------------------------------------------------------------------

    /// <summary>Everything one render draws from, worked out before anything is drawn.</summary>
    private sealed record Model(
        bool Window,
        int Days,
        long Total,
        IReadOnlyList<ScreenTimeApp> Apps,
        IReadOnlyList<ScreenTimeDay> Range,
        IReadOnlyList<(string Date, long Seconds)> Totals,
        long Busiest,
        long Switches,
        IReadOnlySet<string> Hidden)
    {
        public static Model Empty { get; } = new(false, 1, 0, [], [], [], 0, 0, new HashSet<string>());
    }

    private Model Build(IScreenTimeSource source)
    {
        var period = ScreenTimeRules.ClampPeriod(_config.Period);
        var hidden = ScreenTimeRules.Hidden(_config.HiddenApps);

        if (period == ScreenTimePeriod.Day)
        {
            var day = source.Day(_pinned);
            var apps = ScreenTimeRules.Visible(day.Apps, hidden);
            var total = ScreenTimeRules.Sum(apps);

            return new Model(false, 1, total, ScreenTimeRules.Top(apps, apps.Count), [day], [], 0, day.Switches, hidden);
        }

        var length = ScreenTimeRules.RangeLength(period);
        var range = source.Range(length);
        var aggregate = ScreenTimeRules.Aggregate(range, hidden);
        var windowTotal = ScreenTimeRules.Sum(aggregate);
        var totals = ScreenTimeRules.DayTotals(range, hidden);

        return new Model(
            true,
            length,
            windowTotal,
            ScreenTimeRules.Top(aggregate, aggregate.Count),
            range,
            totals,
            ScreenTimeRules.Busiest(totals),
            ScreenTimeRules.TotalSwitches(range),
            hidden);
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        UpdateNav();

        if (_source is null)
        {
            ShowEmpty(Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _model = Build(_source);

        if (_model.Total <= 0)
        {
            ShowEmpty((_model.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _body.Visibility = Visibility.Visible;

        if (_focus is not null && _focus != ScreenTimeRules.OtherKey
            && !_model.Apps.Any(a => a.App == _focus) && !_model.Totals.Any(t => t.Date == _focus))
        {
            _focus = null;
        }

        Lay();
    }

    private void ShowEmpty(string message)
    {
        _emptyText.Text = message;
        _empty.Visibility = Visibility.Visible;
        _body.Visibility = Visibility.Collapsed;
        _footer.Visibility = Visibility.Collapsed;
    }

    /// <summary>[S-454] The header: day navigation for a day, the window's name for a window.</summary>
    private void UpdateNav()
    {
        var narrow = _area.IsMeasured && _area.Width <= 220;
        var window = ScreenTimeRules.ClampPeriod(_config.Period) != ScreenTimePeriod.Day;

        _nav.Visibility = _area.IsMeasured && _area.Width < NavHidesBelow ? Visibility.Collapsed : Visibility.Visible;

        _back.Visibility = window ? Visibility.Collapsed : Visibility.Visible;
        _forward.Visibility = window ? Visibility.Collapsed : Visibility.Visible;

        if (window)
        {
            _today.Visibility = Visibility.Collapsed;
            _navLabel.Text = ScreenTimeRules.PeriodLabel(_config.Period);
            return;
        }

        var today = _source?.Today ?? LocalDay.From(DateTimeOffset.Now);
        _navLabel.Text = ScreenTimeRules.DayLabel(_pinned, today);
        _navLabel.MinWidth = narrow ? 0 : Fluent.Space6 * 3;
        _forward.IsEnabled = _pinned is not null;
        _back.IsEnabled = ScreenTimeRules.CanGoBack(_pinned, today);

        // On a narrow card the jump back to today goes first: the arrows still get there.
        _today.Visibility = _pinned is not null && !narrow ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- figures -------------------------------------------------------------------------------

    private long? DayLimit => ScreenTimeRules.Limit(_config.GoalSeconds);

    /// <summary>[S-452]/[S-453]/[S-457] The headline: the total (a window's per-day average when judged), and the line under it.</summary>
    private (double Seconds, string Detail, bool Over) Headline()
    {
        if (_focus is { } focus)
        {
            if (_segments.FirstOrDefault(s => s.Key == focus) is { } segment)
            {
                var share = ScreenTimeRules.Share(segment.Seconds, _model.Total);
                return (segment.Seconds, $"{segment.Name} · {LocaleFormat.Percent(Strings.Current, Math.Round(share * 100))}", false);
            }

            var day = _model.Totals.FirstOrDefault(t => t.Date == focus);
            if (day.Date is not null && LocalDay.TryParse(day.Date, out var date))
            {
                return (day.Seconds, Formatting.DayHeading(date.Date, (_source?.Today ?? date).Date), false);
            }
        }

        var parts = new List<string>();
        var over = false;

        if (_model.Window)
        {
            var average = ScreenTimeRules.DailyAverage(_model.Total, _model.Days);

            if (_config.ShowAverage)
            {
                parts.Add(Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(average))));
            }

            // [S-457] A window judges its daily average against the daily limit.
            if (ScreenTimeRules.Goal(average, DayLimit) is { } goal)
            {
                parts.Add(Str.Screentime_GoalPerDay.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))));
                over = goal.Over;
            }
        }
        else if (ScreenTimeRules.Goal(_model.Total, DayLimit) is { } goal)
        {
            parts.Add(Str.Screentime_OfGoal.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))));
            over = goal.Over;
        }

        if (_config.ShowSwitches && _model.Switches > 0)
        {
            parts.Add(Str.WidgetsC_ScreenTime_Switches.Plural(_model.Switches));
        }

        return (_model.Total, string.Join(" · ", parts), over);
    }

    /// <summary>
    /// Write the headline into a total and its line. In the ring's hole (<paramref name="hole"/> is
    /// its text box) the total takes the largest type that fits, and drops its minutes when even
    /// Caption cannot hold them ([S-1210]); elsewhere the caller has sized it.
    /// </summary>
    private void ApplyHeadline(TextBlock total, TextBlock detail, (double Width, double Height)? hole = null)
    {
        var (seconds, line, over) = Headline();

        if (hole is { } box)
        {
            var (text, step) = ScreenTimeRules.RingText(seconds, box.Width, box.Height);
            total.Apply(step);
            total.Text = text;
        }
        else
        {
            total.Text = ScreenTimeRules.FormatDuration(seconds);
        }

        total.Foreground = over ? CriticalBrush : PrimaryBrush;
        detail.Text = line;
        detail.Visibility = line.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-452] An app's colour: the hue its key hashes to, at one saturation and lightness for every app.</summary>
    private Brush AppBrush(ScreenTimeSegment segment)
    {
        if (segment.IsOther)
        {
            return OtherBrush;
        }

        if (!_appBrushes.TryGetValue(segment.Key, out var brush))
        {
            var (r, g, b) = ScreenTimeRules.HslToRgb(ScreenTimeRules.Hue(segment.Key), 0.62, 0.60);
            brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
            _appBrushes[segment.Key] = brush;
        }

        return brush;
    }

    // ---- focus --------------------------------------------------------------------------------

    /// <summary>[S-452]/[S-456] Light one app or one day, dim the rest, and say what it is in the headline.</summary>
    private void SetFocus(string? key)
    {
        if (_focus == key)
        {
            return;
        }

        _focus = key;
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        foreach (var (key, element) in _parts)
        {
            element.Opacity = _focus is null || key == _focus ? 1 : 0.35;
        }

        foreach (var row in _rows)
        {
            row.Dim(_focus is not null && row.Key != _focus);
        }

        ApplyHeadline(_total, _detail);

        if (_ringTotal is not null && _ringDetail is not null)
        {
            ApplyHeadline(_ringTotal, _ringDetail, _ringHole);
        }
    }

    // ---- keys ([S-1989]) -------------------------------------------------------------------------

    /// <summary>The apps the legend shows right now, top to bottom: what the arrows step through.</summary>
    private List<string> LegendKeys()
        => [.. _rows.Where(row => row.Host.Visibility == Visibility.Visible && row.Key is not null).Select(row => row.Key!)];

    /// <summary>[S-1989] A tile with a legend, or with days to step, takes the keyboard on a click.</summary>
    public bool WantsKeys => !_detached && (LegendKeys().Count > 0 || ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day);

    /// <summary>
    /// [S-1989] Up/Down, Page Up/Down and Home/End (<see cref="ItemCursor"/>) light one app of the
    /// legend as hovering does — the rest dim and the headline names it; the Menu key opens its Hide
    /// menu; Left/Right step the day as the header's arrows do (a day period only, never past today);
    /// Escape lets the app go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        if (key == WidgetKey.Escape)
        {
            var had = _focus is not null;
            SetFocus(null);
            return had;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            var today = _source?.Today ?? LocalDay.From(DateTimeOffset.Now);
            var allowed = ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day
                && (key == WidgetKey.Left ? ScreenTimeRules.CanGoBack(_pinned, today) : _pinned is not null);

            if (allowed)
            {
                Navigate(key == WidgetKey.Left ? -1 : 1);
            }

            return allowed;
        }

        var keys = LegendKeys();
        var current = _focus is null ? -1 : keys.IndexOf(_focus);

        if (key == WidgetKey.Menu)
        {
            var row = current < 0 ? null : _rows.Find(r => r.Key == keys[current]);
            if (row is null || keys[current] == ScreenTimeRules.OtherKey)
            {
                return false;
            }

            var app = keys[current];
            return ItemMenu.Open(row.Host, [new ItemMenuEntry(Glyphs.Hide, Str.Screentime_HideApp.Text(), () => Hide(app))]);
        }

        if (key is not (WidgetKey.Up or WidgetKey.Down or WidgetKey.Home or WidgetKey.End or WidgetKey.PageUp or WidgetKey.PageDown)
            || ItemCursor.Move(keys.Count, current < 0 ? null : current, key) is not { } next)
        {
            return false;
        }

        SetFocus(keys[next]);
        _rows.Find(r => r.Key == keys[next])?.Host.StartBringIntoView();
        return true;
    }

    /// <summary>[S-455] Hide an app from every view of this widget. Its time is still recorded.</summary>
    private void Hide(string key)
    {
        if (key == ScreenTimeRules.OtherKey || _config.HiddenApps.Contains(key, StringComparer.Ordinal))
        {
            return;
        }

        _config.HiddenApps.Add(key);
        _surface?.RequestSave();
        _focus = null;
        Render();
    }

    private void AttachHideMenu(FrameworkElement element, ScreenTimeSegment segment)
    {
        if (segment.IsOther)
        {
            return;
        }

        element.RightTapped += (_, args) =>
            ItemMenu.Show(element, args, [new ItemMenuEntry(Glyphs.Hide, Str.Screentime_HideApp.Text(), () => Hide(segment.Key))]);
    }

    private void Hoverable(UIElement element, string key)
    {
        element.PointerEntered += (_, _) => SetFocus(key);
        element.PointerExited += (_, _) => SetFocus(null);
        element.PointerCanceled += (_, _) => SetFocus(null);
        element.PointerCaptureLost += (_, _) => SetFocus(null);
        _parts.Add((key, element));
    }
}
