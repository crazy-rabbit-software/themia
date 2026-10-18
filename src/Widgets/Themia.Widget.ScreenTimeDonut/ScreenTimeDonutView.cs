using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Themia.Widget.ScreenTimeDonut;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3340] Each app's share of the day (or the week, or the month) as one ring, the total in its
/// hole. Hovering or selecting a wedge — here or in the chart or the app list on the same view —
/// lifts it, dims the rest and names it in the hole ([S-3343]).
/// <para>
/// The history is the app's one tracker ([S-458]), read through <see cref="IWidgetHost.ScreenTime"/>;
/// the widget has no timer and redraws when the tracker says something changed.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeDonutView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    /// <summary>[S-3343] The lit wedge's stroke against the ring's.</summary>
    private const double LiftFactor = 1.3;

    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;
    private readonly Canvas _canvas = new();
    private readonly TextBlock _total = Fluent.Text(FluentType.Caption);
    private readonly TextBlock _detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _hole;
    private readonly Grid _ring = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly List<(ScreenTimeSegment Segment, Shape Arc)> _arcs = [];

    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeDonutConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];
    private ScreenTimeDonutPlan _plan;
    private WidgetArea _area;
    private bool _detached;

    private readonly IWidgetHost _host;

    public ScreenTimeDonutView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus);
        _nav = new ScreenTimeDayNav(Render);

        // The whole tile takes taps, so a click on its empty space lets the selection go ([S-3302]).
        Background = new SolidColorBrush(Colors.Transparent);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        _total.TextTrimming = TextTrimming.None;
        _total.HorizontalAlignment = HorizontalAlignment.Center;
        _total.TextAlignment = TextAlignment.Center;
        Typography.SetNumeralAlignment(_total, FontNumeralAlignment.Tabular);
        _detail.HorizontalAlignment = HorizontalAlignment.Center;
        _detail.TextAlignment = TextAlignment.Center;
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;

        // The hole takes no pointer input, so the wedges under its box stay hoverable.
        _hole = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children = { _total, _detail },
        };

        _ring.Children.Add(_canvas);
        _ring.Children.Add(_hole);
        _ring.Margin = new Thickness(0, 0, 0, ScreenTimeLayout.Gap);

        _empty = Fluent.EmptyState(Glyphs.ScreenTimeDonut, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_ring);
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
        _config = config as ScreenTimeDonutConfig ?? new ScreenTimeDonutConfig();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        // [S-450] No timer of its own: the tracker ticks, and says so.
        UseSource(_host.ScreenTime);

        // [S-3300] The view's links: what is selected there already shows at once.
        _links.Join(_surface?.Links);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeDonutConfig ?? new ScreenTimeDonutConfig();
        Render();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        UseSource(null);
        _links.Leave();
        _arcs.Clear();
        _canvas.Children.Clear();
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

        _nav.Today = source is null ? null : () => source.Today;
    }

    private void OnLanguage(Locale locale)
    {
        _nav.Retext();
        Render();
    }

    // ---- drawing ----------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _nav.Update(_config.Period, _area);

        if (_source is null)
        {
            ShowEmpty(Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_source, _config.Period, _nav.Pinned);

        if (_data.Total <= 0)
        {
            ShowEmpty((_data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _ring.Visibility = Visibility.Visible;
        Draw();
    }

    private void ShowEmpty(string message)
    {
        _data = null;
        _segments = [];
        _arcs.Clear();
        _canvas.Children.Clear();
        _emptyText.Text = message;
        _empty.Visibility = Visibility.Visible;
        _ring.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3341] [S-3342] The ring: the busiest seven apps and one "Other", one arc each, from
    /// cumulative seconds so the edges meet without a seam, as large as the tile lets it be.
    /// </summary>
    private void Draw()
    {
        if (_data is not { } data || !_area.IsMeasured)
        {
            return;
        }

        _plan = ScreenTimeDonutLayout.Donut(_area);
        _segments = ScreenTimeRules.Segments(data.Apps, data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());

        var size = _plan.Ring;
        var radius = (size - (_plan.Thickness * LiftFactor)) / 2;
        var centre = size / 2;

        _arcs.Clear();
        _canvas.Children.Clear();
        _canvas.Width = size;
        _canvas.Height = size;
        _ring.Width = size;
        _ring.Height = size;

        foreach (var segment in _segments)
        {
            Shape arc;

            if (segment.Fraction >= 0.9999)
            {
                arc = new Ellipse { Width = radius * 2, Height = radius * 2 };
                Canvas.SetLeft(arc, centre - radius);
                Canvas.SetTop(arc, centre - radius);
            }
            else
            {
                arc = Arc(centre, radius, segment.Start, segment.Start + segment.Fraction);
            }

            arc.Stroke = ScreenTimeLook.For(segment.Key);
            arc.StrokeThickness = _plan.Thickness;
            ToolTipService.SetToolTip(arc, ScreenTimeLook.Tip(segment, data.Total));

            var key = segment.Key;
            arc.PointerEntered += (_, _) => Hover(key);
            arc.PointerExited += (_, _) => Hover(null);
            arc.PointerCanceled += (_, _) => Hover(null);
            arc.PointerCaptureLost += (_, _) => Hover(null);
            arc.Tapped += (_, e) =>
            {
                e.Handled = true;
                _links.Toggle(key);
                ApplyFocus();
            };
            ScreenTimeLook.AttachHideMenu(arc, key, () => _source);

            _arcs.Add((segment, arc));
            _canvas.Children.Add(arc);
        }

        var hole = size - (_plan.Thickness * 2);
        _hole.MaxWidth = hole * 0.9;
        _detail.MaxWidth = hole * 0.86;
        _detail.Visibility = _plan.Detail ? Visibility.Visible : Visibility.Collapsed;
        ApplyFocus();
    }

    private void Hover(string? key)
    {
        if (_links.PointerHover(key))
        {
            ApplyFocus();
        }
    }

    /// <summary>[S-3343] Lift the focused wedge, dim the rest, and say what it is in the hole.</summary>
    private void ApplyFocus()
    {
        if (_detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);

        foreach (var (segment, arc) in _arcs)
        {
            var emphasis = ScreenTimeRules.Emphasis(segment.Key, part);
            arc.Opacity = ScreenTimeLook.Opacity(emphasis);
            arc.StrokeThickness = emphasis == ScreenTimeEmphasis.Lit ? _plan.Thickness * LiftFactor : _plan.Thickness;
        }

        var focused = part is null ? null : _segments.FirstOrDefault(s => s.Key == part);
        var (seconds, line, over) = focused is { } f
            ? (f.Seconds, $"{f.Name} · {ScreenTimeLook.Percent(f.Seconds, data.Total)}", false)
            : Headline(data);

        var hole = _plan.Ring - (_plan.Thickness * 2);
        var (text, step) = ScreenTimeRules.RingText(seconds, hole * 0.86, hole * (_plan.Detail ? 0.4 : 0.6));
        _total.Apply(step);
        _total.Text = text;
        _total.Foreground = over ? ScreenTimeLook.Critical : ScreenTimeLook.Primary;
        _detail.Text = line;
        _detail.Visibility = _plan.Detail && line.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-3343] [S-457] The total, and under it the day's limit ("of 4h") or a window's average per day.</summary>
    private static (double Seconds, string Line, bool Over) Headline(ScreenTimeData data)
    {
        if (!data.Window)
        {
            return ScreenTimeRules.Goal(data.Total, data.DayLimit) is { } goal
                ? (data.Total, ScreenTimeDonutStr.Screentime_OfGoal.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))), goal.Over)
                : (data.Total, string.Empty, false);
        }

        var average = ScreenTimeRules.DailyAverage(data.Total, data.Days);
        var over = ScreenTimeRules.Goal(average, data.DayLimit) is { Over: true };
        return (data.Total, Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(average))), over);
    }

    /// <summary>An arc of the ring between two shares of the circle, starting at twelve o'clock.</summary>
    private static Microsoft.UI.Xaml.Shapes.Path Arc(double centre, double radius, double from, double to)
    {
        static Point At(double centre, double radius, double share)
        {
            var angle = (share * 2 * Math.PI) - (Math.PI / 2);
            return new Point(centre + (radius * Math.Cos(angle)), centre + (radius * Math.Sin(angle)));
        }

        var figure = new PathFigure { StartPoint = At(centre, radius, from), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = At(centre, radius, to),
            Size = new Size(radius, radius),
            IsLargeArc = to - from > 0.5,
            SweepDirection = SweepDirection.Clockwise,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Microsoft.UI.Xaml.Shapes.Path { Data = geometry };
    }

    // ---- keys ([S-3307], [S-1989]) -------------------------------------------------------------

    /// <summary>[S-3307] The ring takes the keyboard on a click: there are wedges to step through.</summary>
    public bool WantsKeys => !_detached && _arcs.Count > 0;

    /// <summary>
    /// [S-3307] Up/Down, Page Up/Down, Home/End step through the wedges, clockwise from twelve, and
    /// light each in every screen-time widget on the view; Enter/Space select; Left/Right step the
    /// day (a day period); the Menu key hides; Escape lets the cursor go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            return ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.StepKey(key == WidgetKey.Right);
        }

        var used = _links.HandleKey(key, [.. _segments.Select(s => s.Key)], app => _arcs.Find(a => a.Segment.Key == app).Arc, _source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}
