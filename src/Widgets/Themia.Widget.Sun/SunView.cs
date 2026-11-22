using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Themia.Widget.Sun;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11110] The sun's day where the observer is, as a graphic, computed offline from the configured
/// latitude and longitude on the family's Chart tokens: the path across the sky (Arc) or the day as a
/// bar (Day bar, <see cref="SunDayBar"/>, [S-6046]). The times are the <c>sunTimes</c> atom's, never drawn here.
/// <para>
/// The path is the sun's <b>real</b> one — altitude against how far it is through its passage across
/// the sky — so it flattens in winter, towers in summer and barely lifts off the horizon inside the
/// polar circles. [S-167] Where the sun has been is the Chart's series: the accent at its stroke over
/// a wash of it; the stretch still to come is the Track role at the stroke, the night stretch under
/// the horizon a Track hairline. [S-168] All the astronomy lives in <see cref="Themia.Core.Astronomy"/>.
/// </para>
/// <para>
/// [S-15812] Both styles are a graph's plot, the card edge to edge (owner, 2026-11-19: "Plot edge to edge"; WR-1, WR-3):
/// the arc's horizon and path from the card's left edge to its right and its top to its foot; the day bar's track across
/// the card's middle from edge to edge, the daylight a wash from the card's top to its foot over its stretch. The sun's
/// marker is drawn where it stands, so at an edge of the card (noon on the top, a sunrise on the left) the card cuts it.
/// </para>
/// </summary>
internal sealed partial class SunView : Grid, IWidgetView, IWidgetSizeAware
{
    /// <summary>[S-15812] The plot: the card's whole box (<see cref="PlotMark"/>), clipped there.</summary>
    private readonly Canvas _plot = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,

        // [S-1113] The day is a time axis (and east to west): left to right in every language.
        FlowDirection = FlowDirection.LeftToRight,
    };

    // The arc.
    private readonly Polygon _elapsedFill = new() { Opacity = FamilyShape.Wash };
    private readonly Polyline _dayTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _nightTrack = new() { StrokeThickness = FamilyShape.Hairline, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _doneTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Line _horizon = new() { StrokeThickness = FamilyShape.Hairline };
    private readonly Ellipse _sun = new() { Width = SunLayout.Dot, Height = SunLayout.Dot };

    // [S-6046] [S-15812] The day bar: the day as the Chart's track across the card's middle, the daylight its accent fill
    // on it over a wash of it the card's height, now a dot on it.
    private readonly Rectangle _daylightWash = new() { Opacity = FamilyShape.Wash };
    private readonly Line _track = new() { StrokeThickness = FamilyShape.Track };
    private readonly Line _daylight = new() { StrokeThickness = FamilyShape.Track };
    private readonly Ellipse _barDot = new() { Width = SunLayout.Dot, Height = SunLayout.Dot };

    private SunConfig _config = new();

    private Ticker? _ticker;
    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private DateTimeOffset _now = DateTimeOffset.Now;

    private WidgetArea _area;

    /// <summary>The plot's box: the card's.</summary>
    private (double Width, double Height) _box;

    public SunView()
    {
        var accent = Family.Brush(ColourRole.Accent);
        var track = Family.Brush(ColourRole.Track);
        _elapsedFill.Fill = accent;
        _doneTrack.Stroke = accent;
        _dayTrack.Stroke = track;
        _nightTrack.Stroke = track;
        _horizon.Stroke = track;
        _daylightWash.Fill = accent;
        _track.Stroke = track;
        _daylight.Stroke = accent;

        foreach (var shape in (UIElement[])[_elapsedFill, _dayTrack, _nightTrack, _doneTrack, _horizon, _sun, _daylightWash, _track, _daylight, _barDot])
        {
            _plot.Children.Add(shape);
        }

        // [S-15812] The plot stands on the card's top edge (the card gives the inset over the view) and runs to its sides
        // and foot.
        _plot.Margin = ListEdge.Margin;
        PlotMark.Mark(_plot);
        Children.Add(_plot);

        // Nothing shows until the first lay.
        _plot.Visibility = Visibility.Collapsed;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>The language changed: the drawing has no words; laid again all the same.</summary>
    private void OnLanguage(Locale locale) => Lay();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as SunConfig ?? new SunConfig();
        Resubscribe();

        Lay();
    }

    /// <summary>[S-663] The card's word on how much room there is.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void Resubscribe()
    {
        // Only when the cadence actually changed: remaking the subscription on every config touch
        // made dragging a settings slider stutter for every widget on the desktop.
        _ = _tick.Set(_ticker, _config.TickInterval, Tick);
    }

    private void Tick()
    {
        _now = DateTimeOffset.Now;
        Refresh();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _tick.Dispose();
    }

    /// <summary>[S-15812] The plot fills the card in either style.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var bar = _config.Style == SunStyle.DayBar;

        // [S-6005] One style's shapes, never the other's.
        foreach (var shape in (UIElement[])[_elapsedFill, _dayTrack, _nightTrack, _doneTrack, _horizon, _sun])
        {
            shape.Visibility = Show(!bar);
        }

        foreach (var shape in (UIElement[])[_daylightWash, _track, _daylight, _barDot])
        {
            shape.Visibility = Show(bar);
        }

        // The card's whole box: the view's room and the card's inset over it.
        _box = (_area.Width, _area.Height + FamilySpace.Inset);
        _plot.Width = _box.Width;
        _plot.Height = _box.Height;
        _plot.Clip = new RectangleGeometry { Rect = new Rect(0, 0, _box.Width, _box.Height) };
        _plot.Visibility = Visibility.Visible;
        Refresh();
    }

    /// <summary>Redraw at the instant in hand. Called on a tick, a config change and a resize.</summary>
    private void Refresh() => Redraw();

    /// <summary>
    /// Rebuild the drawing. Cheap by construction: a few shapes and about a hundred points,
    /// recomputed only on a tick, a config change or a resize — never per frame.
    /// </summary>
    private void Redraw()
    {
        var (width, height) = _box;
        if (!_area.IsMeasured || !(width > 0) || !(height > 0))
        {
            return;
        }

        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;

        if (_config.Style == SunStyle.DayBar)
        {
            RedrawBar(width, height, SunDayBar.Of(_now, SolarPosition.TimesFor(_now, latitude, longitude), TimeZoneInfo.Local));
            return;
        }

        // [S-15812] The path in the whole plot: the day from its left edge to its right, noon on its top, the night's
        // lowest on its foot.
        var ground = SolarPosition.SubsolarPoint(_now);
        var declination = ground.Latitude;
        var position = SkyPath.PositionOf(ground, latitude, longitude);
        var plot = SunPathPlot.For(width, height, latitude, declination);

        _dayTrack.Points = ToPoints(SunPathGeometry.Curve(SkyPath.Samples(declination, latitude, false), plot));
        _nightTrack.Points = ToPoints(SunPathGeometry.Curve(SkyPath.Samples(declination, latitude, true), plot));

        if (position.Up)
        {
            var elapsed = SkyPath.Samples(declination, latitude, false, 32, 0, position.Progress);
            _elapsedFill.Points = ToPoints(SunPathGeometry.Area(elapsed, plot));
            _doneTrack.Points = ToPoints(SunPathGeometry.Curve(elapsed, plot));
        }

        _elapsedFill.Visibility = Show(position.Up);
        _doneTrack.Visibility = Show(position.Up);

        _horizon.X1 = 0;
        _horizon.X2 = width;
        _horizon.Y1 = plot.HorizonY;
        _horizon.Y2 = plot.HorizonY;

        // The sun under the horizon is still drawn, a role lower, so the night reads as the same journey.
        Place(_sun, plot.PointAt(new SkySample(position.Progress, position.AltitudeDegrees)));
        _sun.Fill = Family.Brush(position.Up ? ColourRole.Accent : ColourRole.TextTertiary);
    }

    /// <summary>
    /// [S-6046] [S-15812] The day bar: midnight to midnight from the card's left edge to its right as the Chart's track
    /// across its middle, the daylight its accent fill over a wash of it from the card's top to its foot, now a dot on it
    /// (primary in daylight, tertiary at night), its centre kept a half dot inside the card's sides.
    /// </summary>
    private void RedrawBar(double width, double height, SunDayBarPlan plan)
    {
        var middle = height / 2;
        _track.X1 = 0;
        _track.X2 = width;
        _track.Y1 = middle;
        _track.Y2 = middle;

        var (from, to) = plan.Daylight;
        var lit = to > from;
        _daylight.Visibility = Show(lit);
        _daylightWash.Visibility = Show(lit);
        _daylight.X1 = from * width;
        _daylight.X2 = to * width;
        _daylight.Y1 = middle;
        _daylight.Y2 = middle;
        _daylightWash.Width = Math.Max(0, (to - from) * width);
        _daylightWash.Height = height;
        Canvas.SetLeft(_daylightWash, from * width);
        Canvas.SetTop(_daylightWash, 0);

        Place(_barDot, new PlotPoint(SunLayout.DotAt(plan.Now, width), middle));
        _barDot.Fill = Family.Brush(plan.NowLit ? ColourRole.TextPrimary : ColourRole.TextTertiary);
    }

    private static void Place(Ellipse dot, PlotPoint centre)
    {
        Canvas.SetLeft(dot, centre.X - (SunLayout.Dot / 2));
        Canvas.SetTop(dot, centre.Y - (SunLayout.Dot / 2));
    }

    private static PointCollection ToPoints(IReadOnlyList<PlotPoint> points)
    {
        var collection = new PointCollection();

        foreach (var point in points)
        {
            collection.Add(new Point(point.X, point.Y));
        }

        return collection;
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
