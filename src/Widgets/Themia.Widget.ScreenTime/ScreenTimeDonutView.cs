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
using Windows.Foundation;
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
internal sealed partial class ScreenTimeDonutView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    private readonly ScreenTimeLinks _links;
    private readonly ScreenTimeDayNav _nav;

    /// <summary>[S-11928] The day selected on the view, followed.</summary>
    private readonly WidgetLinks _dates;
    private readonly Canvas _canvas = new();

    /// <summary>[S-1210] The figure in the hole: the family's Readout, centred.</summary>
    private readonly ReadoutBlock _figure = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
    };

    private readonly Grid _ring = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-12813] The ring with nothing on it: its track, under the wedges while there are none.</summary>
    private readonly Microsoft.UI.Xaml.Shapes.Ellipse _track = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly List<(ScreenTimeSegment Segment, Wedge Arc)> _arcs = [];

    /// <summary>[S-3325] One wedge per app (and Other), kept across reads.</summary>
    private readonly KeyedParts<Wedge> _wedges = new();

    private IWidgetSurface? _surface;
    /// <summary>[S-450] [S-3310] The tracker, the day line, the app links and the dates: joined and left as one.</summary>
    private readonly ScreenTimeWiring _wiring;
    /// <summary>[S-450] The tracker, followed as every screen-time widget follows it.</summary>
    private readonly ScreenTimeFeed _feed;
    private ScreenTimeDonutConfig _config = new();
    private ScreenTimeData? _data;
    private IReadOnlyList<ScreenTimeSegment> _segments = [];

    /// <summary>[S-2272] Each wedge's tone by its app's rank in the period.</summary>
    private IReadOnlyDictionary<string, ScreenTimeTone> _tones = new Dictionary<string, ScreenTimeTone>();
    private readonly ScreenTimeDonutPlan _plan = ScreenTimeDonutLayout.Plan;

    /// <summary>[S-14284] The day line over the ring, its figure in the hole: one block, planned at the natural ring and scaled as one.</summary>
    private readonly ReadingFit _fit = new();

    public ScreenTimeDonutView(IWidgetHost host)
    {
        _wiring = new ScreenTimeWiring(host, Render, ApplyFocus);
        (_links, _nav, _feed, _dates) = (_wiring.Links, _wiring.Nav, _wiring.Feed, _wiring.Dates);

        // [S-11928] A day selected on the view (a calendar's click) is the day a Day period shows.
        _wiring.FollowSelectedDay(() => _config.Period);

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
        _ring.Children.Add(_track);
        _ring.Children.Add(_canvas);
        _ring.Children.Add(_figure);

        // [S-14284] WR-7 (owner, 2026-11-19: "Keep them together"; "All together, scaled"): the day and its arrows
        // ([S-454], [S-12481]) stand right over the ring, as wide as it, and the two with the figure in the hole are one
        // block that the card scales as one (in the fit's inset at the sides and the foot).
        _ring.HorizontalAlignment = HorizontalAlignment.Center;
        _fit.Parts.Add(new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { _nav.Line, _ring } });
        Children.Add(_fit);
    }

    /// <summary>[S-454] Back to today, while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _nav.ContextActions;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as ScreenTimeDonutConfig ?? new ScreenTimeDonutConfig();

        // [S-450] No timer of its own: the tracker ticks, and says so.
        _wiring.Attach(_surface);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ScreenTimeDonutConfig ?? new ScreenTimeDonutConfig();
        Render();
    }

    public void Detach()
    {
        _wiring.Detach();
        DropWedges();
    }

    // ---- drawing ----------------------------------------------------------------------------

    private void Render()
    {
        if (_wiring.Detached)
        {
            return;
        }

        _nav.Update(_config.Period);

        if (_feed.Source is null)
        {
            ShowEmpty("--", Str.WidgetsC_ScreenTime_NotRecording.Text());
            return;
        }

        _data = ScreenTimeData.Build(_feed.Source, _config.Period, _nav.Pinned);

        if (_data.Total <= 0)
        {
            ShowEmpty(ScreenTimeRules.FormatDuration(0), (_data.Window ? Str.WidgetsC_ScreenTime_NoActivityPeriod : Str.Screentime_NoActivity).Text());
            return;
        }

        _ring.Visibility = Visibility.Visible;
        Draw();
    }

    /// <summary>
    /// [S-12813] Nothing to draw: the ring stays, its track alone, and in its hole <paramref name="value"/> ("--"
    /// while nothing records, none spent in a period that recorded) drawn unavailable with
    /// <paramref name="message"/> under it where the hole holds it, and always as the ring's tooltip.
    /// </summary>
    private void ShowEmpty(string value, string message)
    {
        _data = null;
        _segments = [];
        DropWedges();
        SizeRing(showTrack: true);
        ToolTipService.SetToolTip(_ring, message);

        var figure = ScreenTimeRules.Figure(0, _plan.Largest, _plan.Hole, under: true);
        _figure.Use(figure.Step, centred: true);
        var height = figure.Step.LineHeight + (figure.Under ? FamilyType.Under(figure.Step).LineHeight : 0);
        _figure.MaxWidth = Math.Floor(ScreenTimeRules.HoleWidth(height, _plan.Hole));
        _figure.Show(value, under: figure.Under ? message : null, valueRole: ColourRole.TextDisabled);
        _ring.Visibility = Visibility.Visible;
    }

    /// <summary>The ring's box at the plan's size; its track shown only with no wedge on it.</summary>
    private void SizeRing(bool showTrack)
    {
        var size = _plan.Ring;
        _canvas.Width = size;
        _canvas.Height = size;
        _ring.Width = size;
        _ring.Height = size;
        _track.Width = size;
        _track.Height = size;
        _track.StrokeThickness = _plan.Thickness;
        _track.Stroke = Family.Brush(ColourRole.Track);
        _track.Visibility = showTrack ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3341] [S-11515] The ring: the busiest seven apps and one "Other", one arc each, from
    /// cumulative seconds so the edges meet without a seam, at the natural ring the block is scaled from ([S-14284]).
    /// </summary>
    private void Draw()
    {
        if (_data is not { } data)
        {
            return;
        }

        _segments = ScreenTimeRules.Segments(data.Apps, data.Total, ScreenTimeRules.DrawingApps, Str.Screentime_Other.Text());
        _tones = ScreenTimeRules.Tones(_segments);

        var size = _plan.Ring;
        var radius = (size - _plan.Thickness) / 2;
        var centre = size / 2;

        SizeRing(showTrack: false);
        ToolTipService.SetToolTip(_ring, null);

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
        if (_wiring.Detached || _data is not { } data)
        {
            return;
        }

        var part = ScreenTimeRules.FocusPart(_links.Focus, _segments, data.Apps);
        var pointed = ScreenTimeRules.FocusPart(_links.Pointed, _segments, data.Apps);
        var picked = ScreenTimeRules.FocusPart(_links.Picked, _segments, data.Apps);

        foreach (var (segment, wedge) in _arcs)
        {
            wedge.Path.Opacity = ScreenTimeRules.Opacity(ScreenTimeRules.ToneOf(_tones, segment.Key), segment.Key, pointed, picked);
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
                ? (data.Total, ScreenTimeStr.Screentime_OfGoal.Format(("value", ScreenTimeRules.FormatLimit(goal.Limit))), goal.Over)
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
        ScreenTimeLook.AttachHideMenu(arc, key, () => _feed.Source);
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
    public bool WantsKeys => !_wiring.Detached && _arcs.Count > 0;

    /// <summary>
    /// [S-3307] Up/Down, Page Up/Down, Home/End step through the wedges, clockwise from twelve, and
    /// light each in every screen-time widget on the view; Enter/Space select; Left/Right step the
    /// day (a day period); the Menu key hides; Escape lets the cursor go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_wiring.Detached)
        {
            return false;
        }

        if (key is WidgetKey.Left or WidgetKey.Right)
        {
            return ScreenTimeRules.ClampPeriod(_config.Period) == ScreenTimePeriod.Day && _nav.StepKey(key == WidgetKey.Right);
        }

        var used = _links.HandleKey(key, [.. _segments.Select(s => s.Key)], app => _wedges.Of(app)?.Path, _feed.Source);
        if (used)
        {
            ApplyFocus();
        }

        return used;
    }
}
