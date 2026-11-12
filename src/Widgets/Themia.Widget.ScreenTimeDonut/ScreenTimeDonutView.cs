using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Themia.Widget.ScreenTimeDonut;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3340] Each app's share of the day (or the week, or the month) as one ring of data colours
/// ([S-6705]) in the family's track, the total in its hole on the Readout's steps ([S-1210]). Hovering
/// or selecting a wedge — here or in the chart or the app list on the same view — dims the rest to
/// their wash and names it in the hole ([S-3343]).
/// <para>
/// The history is the app's one tracker ([S-458]), read through <see cref="IWidgetHost.ScreenTime"/>;
/// the widget has no timer and redraws when the tracker says something changed. [S-3325] A wedge is
/// kept per app across reads: a read moves its arc and its tip, and only a new app gets a new one.
/// </para>
/// </summary>
internal sealed partial class ScreenTimeDonutView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetKeys, IWidgetContextActions
{
    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;
    private readonly Canvas _canvas = new();

    /// <summary>[S-1210] The figure in the hole: the family's Readout, centred.</summary>
    private readonly ReadoutBlock _figure = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
    };

    private readonly Grid _ring = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-6714] Nothing to draw: the Status block's one row at the top.</summary>
    private readonly StatusBlock _empty = new() { Visibility = Visibility.Collapsed };
    private readonly List<(ScreenTimeSegment Segment, Wedge Arc)> _arcs = [];

    /// <summary>[S-3325] One wedge per app (and Other), kept across reads.</summary>
    private readonly KeyedParts<Wedge> _wedges = new();

    private IWidgetSurface? _surface;
    private IScreenTimeSource? _source;
    private ScreenTimeDonutConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];

    /// <summary>[S-2272] Each wedge's tone by its app's rank in the period.</summary>
    private IReadOnlyDictionary<string, ScreenTimeTone> _tones = new Dictionary<string, ScreenTimeTone>();
    private ScreenTimeDonutPlan _plan = ScreenTimeDonutLayout.Donut(ReadoutSizes.Default);
    private bool _detached;

    private readonly IWidgetHost _host;

    public ScreenTimeDonutView(IWidgetHost host)
    {
        _host = host;
        _links = new ScreenTimeLinks(ApplyFocus);
        _nav = new ScreenTimeDayNav(Render);

        // The whole tile takes taps, so a click on its empty space lets the selection go ([S-3302]).
        Background = Family.Brush(ColourRole.Clear);
        Tapped += (_, _) =>
        {
            if (_links.ClearSelection())
            {
                ApplyFocus();
            }
        };

        // The figure takes no pointer input, so the wedges under its box stay hoverable.
        _ring.Children.Add(_canvas);
        _ring.Children.Add(_figure);

        // [S-11515] The ring stands at the content's top, in the card's inset at the sides and the foot.
        _ring.VerticalAlignment = VerticalAlignment.Top;
        _ring.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);

        Children.Add(_ring);
        Children.Add(_empty);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _nav.Host;

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

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
        DropWedges();
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

        _nav.Update(_config.Period);

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
        DropWedges();
        _empty.Show(StatusKind.Empty, Glyphs.ScreenTimeDonut, message);
        _empty.Visibility = Visibility.Visible;
        _ring.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3341] [S-11515] The ring: the busiest seven apps and one "Other", one arc each, from
    /// cumulative seconds so the edges meet without a seam, at the size its Size says.
    /// </summary>
    private void Draw()
    {
        if (_data is not { } data)
        {
            return;
        }

        _plan = ScreenTimeDonutLayout.Donut(_config.Size);
        _segments = ScreenTimeRules.Segments(data.Apps, data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());
        _tones = ScreenTimeRules.Tones(_segments);

        var size = _plan.Ring;
        var radius = (size - _plan.Thickness) / 2;
        var centre = size / 2;

        _canvas.Width = size;
        _canvas.Height = size;
        _ring.Width = size;
        _ring.Height = size;

        // [S-3325] The wedges of the apps still on the ring are moved, not built again.
        var (wedges, _) = _wedges.Sync([.. _segments.Select(s => s.Key)], NewWedge, DropWedge);
        _arcs.Clear();

        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            var wedge = wedges[i];
            wedge.Place(centre, radius, segment.Start, segment.Fraction);
            wedge.Tip(ScreenTimeLook.Tip(segment, data.Total));
            wedge.Path.Stroke = ScreenTimeLook.Brush(ScreenTimeRules.ToneOf(_tones, segment.Key));
            _arcs.Add((segment, wedge));
        }

        ApplyFocus();
    }

    private void Hover(string? key)
    {
        if (_links.PointerHover(key))
        {
            ApplyFocus();
        }
    }

    /// <summary>[S-3343] Dim every wedge but the focused one to its wash, and say what it is in the hole.</summary>
    private void ApplyFocus()
    {
        if (_detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);

        foreach (var (segment, wedge) in _arcs)
        {
            wedge.Path.Opacity = ScreenTimeRules.Opacity(ScreenTimeRules.ToneOf(_tones, segment.Key), ScreenTimeRules.Emphasis(segment.Key, part));
        }

        var focused = part is null ? null : _segments.FirstOrDefault(s => s.Key == part);
        var (seconds, line, over) = focused is { } f
            ? (f.Seconds, $"{f.Name} · {ScreenTimeLook.Percent(f.Seconds, data.Total)}", false)
            : Headline(data);

        // [S-1210] The figure on the largest Readout step its hole holds, the line under it where it fits.
        var figure = ScreenTimeRules.Figure(seconds, _plan.Largest, _plan.Hole, line.Length > 0);
        _figure.Use(figure.Step, centred: true);

        // The line under is cut to the hole's width at the figure's height, so it never runs over the ring.
        var height = figure.Step.LineHeight + (figure.Under ? FamilyType.Under(figure.Step).LineHeight : 0);
        _figure.MaxWidth = Math.Floor(ScreenTimeRules.HoleWidth(height, _plan.Hole));
        _figure.Show(figure.Text, under: figure.Under ? line : null, valueRole: over ? ColourRole.Critical : ColourRole.TextPrimary);
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

    /// <summary>[S-3325] A new app on the ring: its wedge, its hover, tap and hide menu, once (its tone is set as the ring is drawn).</summary>
    private Wedge NewWedge(string key)
    {
        var wedge = new Wedge(key);
        var arc = wedge.Path;
        arc.StrokeThickness = _plan.Thickness;
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
        _canvas.Children.Add(arc);
        return wedge;
    }

    private void DropWedge(Wedge wedge) => _canvas.Children.Remove(wedge.Path);

    private void DropWedges()
    {
        _wedges.Clear(DropWedge);
        _arcs.Clear();
        _canvas.Children.Clear();
    }

    /// <summary>
    /// [S-3341] [S-3325] One app's wedge: an arc of the ring between two shares of the circle, from
    /// twelve o'clock, as a path (two half arcs when it is the whole ring: an arc back to its own start
    /// draws nothing). Its geometry and its tip are written only when they say something else.
    /// </summary>
    private sealed class Wedge(string key)
    {
        private (double Centre, double Radius, double From, double Share)? _placed;
        private string? _tip;

        public string Key { get; } = key;

        public Microsoft.UI.Xaml.Shapes.Path Path { get; } = new();

        public void Place(double centre, double radius, double from, double share)
        {
            var placed = (centre, radius, from, share);
            if (_placed == placed)
            {
                return;
            }

            _placed = placed;
            Path.Data = Geometry(centre, radius, from, share);
        }

        public void Tip(string tip)
        {
            if (tip == _tip)
            {
                return;
            }

            _tip = tip;
            ToolTipService.SetToolTip(Path, tip);
        }

        private static PathGeometry Geometry(double centre, double radius, double from, double share)
        {
            static Point At(double centre, double radius, double share)
            {
                var angle = (share * 2 * Math.PI) - (Math.PI / 2);
                return new Point(centre + (radius * Math.Cos(angle)), centre + (radius * Math.Sin(angle)));
            }

            var size = new Size(radius, radius);
            var figure = new PathFigure { StartPoint = At(centre, radius, from), IsClosed = false, IsFilled = false };

            if (share >= 0.9999)
            {
                figure.Segments.Add(new ArcSegment { Point = At(centre, radius, from + 0.5), Size = size, SweepDirection = SweepDirection.Clockwise });
                figure.Segments.Add(new ArcSegment { Point = At(centre, radius, from), Size = size, SweepDirection = SweepDirection.Clockwise });
            }
            else
            {
                figure.Segments.Add(new ArcSegment
                {
                    Point = At(centre, radius, from + share),
                    Size = size,
                    IsLargeArc = share > 0.5,
                    SweepDirection = SweepDirection.Clockwise,
                });
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }
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

        var used = _links.HandleKey(key, [.. _segments.Select(s => s.Key)], app => _wedges.Of(app)?.Path, _source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}
