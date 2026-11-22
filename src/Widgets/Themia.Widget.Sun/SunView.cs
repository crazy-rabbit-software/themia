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
/// [S-15806] The arc is a graph's plot, the card edge to edge (owner, 2026-11-19: "Plot edge to edge"): the horizon runs
/// from the card's left edge to its right, the path from the left to the right and from the card's top to its foot, the
/// marker's half kept inside it so it is never cut. [S-15807] The day bar is a one-readout block: its track runs the
/// content's width at the inset and the bar grows as one with the card until the height or its natural width's share
/// stops it, centred where the width stops it (WR-7; owner, 2026-11-20: "Centred is fine").
/// </para>
/// </summary>
internal sealed partial class SunView : Grid, IWidgetView, IWidgetSizeAware
{
    /// <summary>[S-15806] The arc's plot: the card's whole box (<see cref="PlotMark"/>), clipped there.</summary>
    private readonly Canvas _arc = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,

        // [S-1113] The day is a time axis (and east to west): left to right in every language.
        FlowDirection = FlowDirection.LeftToRight,
    };

    /// <summary>[S-15806] The path's box in the plot: the plot less the marker's half on every side.</summary>
    private readonly Canvas _path = new();

    private readonly Polygon _elapsedFill = new() { Opacity = FamilyShape.Wash };
    private readonly Polyline _dayTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _nightTrack = new() { StrokeThickness = FamilyShape.Hairline, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _doneTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Line _horizon = new() { StrokeThickness = FamilyShape.Hairline };
    private readonly Ellipse _sun = new() { Width = SunLayout.Dot, Height = SunLayout.Dot };

    /// <summary>[S-6046] [S-15807] The day bar at its natural size, scaled as one to its room (<see cref="SunLayout.BarScale"/>).</summary>
    private readonly Canvas _barBlock = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
        FlowDirection = FlowDirection.LeftToRight,
        Height = SunLayout.Dot,
    };

    private readonly ScaleTransform _barScale = new();

    // [S-6046] The day bar: the day as the Chart's track, the daylight its fill.
    private readonly ChartBar _bar = new();
    private readonly Ellipse _barDot = new() { Width = SunLayout.Dot, Height = SunLayout.Dot };

    /// <summary>
    /// [S-14319] The day bar's room: every place its dot stands in through the day, the track's whole length (the content's
    /// whole width at the inset). A held reading's box, so what judges the face reads the bar's sides there.
    /// </summary>
    private readonly Border _barRoom = new() { IsHitTestVisible = false, Height = SunLayout.Dot };

    private SunConfig _config = new();

    private Ticker? _ticker;
    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private DateTimeOffset _now = DateTimeOffset.Now;

    private WidgetArea _area;

    /// <summary>The arc's plot (the card) or the bar's width at its natural size.</summary>
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

        _path.Children.Add(_elapsedFill);
        _path.Children.Add(_dayTrack);
        _path.Children.Add(_nightTrack);
        _path.Children.Add(_doneTrack);
        _path.Children.Add(_sun);
        Canvas.SetLeft(_path, SunPathPlot.MarkerHalf);
        Canvas.SetTop(_path, SunPathPlot.MarkerHalf);
        _arc.Children.Add(_horizon);
        _arc.Children.Add(_path);

        // [S-15806] The plot stands on the card's top edge (the card gives the inset over the view) and runs to its sides
        // and foot.
        _arc.Margin = ListEdge.Margin;
        PlotMark.Mark(_arc);

        _barBlock.Children.Add(_barRoom);
        _barBlock.Children.Add(_bar);
        _barBlock.Children.Add(_barDot);
        _barBlock.RenderTransform = _barScale;
        HoldMark.Mark(_barRoom);

        Children.Add(_arc);
        Children.Add(_barBlock);

        // Nothing shows until the first lay.
        _arc.Visibility = Visibility.Collapsed;
        _barBlock.Visibility = Visibility.Collapsed;
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

    /// <summary>[S-11110] The graphic fills its box: the arc the card, the bar the content's width at its inset.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var bar = _config.Style == SunStyle.DayBar;
        _arc.Visibility = Show(!bar);
        _barBlock.Visibility = Show(bar);

        if (bar)
        {
            // [S-15807] The content's room: the inset at the sides and the foot (the card gives the top).
            var room = (Width: Math.Max(0, _area.Width - (2 * FamilySpace.Inset)), Height: Math.Max(0, _area.Height - FamilySpace.Inset));
            var scale = SunLayout.BarScale(room.Width, room.Height);
            _barScale.ScaleX = scale;
            _barScale.ScaleY = scale;
            _box = (room.Width / scale, SunLayout.Dot);
            _barBlock.Width = _box.Width;
            _barBlock.Margin = new Thickness(FamilySpace.Inset, Math.Max(0, (room.Height - (SunLayout.Dot * scale)) / 2), 0, 0);
        }
        else
        {
            // [S-15806] The card's whole box: the view's room and the card's inset over it.
            _box = (_area.Width, _area.Height + FamilySpace.Inset);
            _arc.Width = _box.Width;
            _arc.Height = _box.Height;
            _arc.Clip = new RectangleGeometry { Rect = new Rect(0, 0, _box.Width, _box.Height) };
        }

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
            RedrawBar(width, SunDayBar.Of(_now, SolarPosition.TimesFor(_now, latitude, longitude), TimeZoneInfo.Local));
            return;
        }

        // [S-15806] The path in the plot less the marker's half on every side, so the marker at noon, at a sunrise on
        // the left edge or at the night's foot is never cut; the horizon across the whole plot.
        var half = SunPathPlot.MarkerHalf;
        var (pathWidth, pathHeight) = (Math.Max(1, width - (2 * half)), Math.Max(1, height - (2 * half)));
        var ground = SolarPosition.SubsolarPoint(_now);
        var declination = ground.Latitude;
        var position = SkyPath.PositionOf(ground, latitude, longitude);
        var plot = SunPathPlot.For(pathWidth, pathHeight, latitude, declination);

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
        _horizon.Y1 = half + plot.HorizonY;
        _horizon.Y2 = half + plot.HorizonY;

        // The sun under the horizon is still drawn, a role lower, so the night reads as the same journey.
        Place(_sun, plot.PointAt(new SkySample(position.Progress, position.AltitudeDegrees)));
        _sun.Fill = Family.Brush(position.Up ? ColourRole.Accent : ColourRole.TextTertiary);
    }

    /// <summary>
    /// [S-6046] [S-15807] The day bar: midnight to midnight across the whole track as the Chart's track, the daylight its
    /// accent fill, now a dot on it (primary in daylight, tertiary at night), its centre kept a half dot inside the track's
    /// ends so the dot at midnight is not cut by the edge.
    /// </summary>
    private void RedrawBar(double width, SunDayBarPlan plan)
    {
        var half = SunLayout.Dot / 2;
        _barRoom.Width = width;
        _bar.Width = width;
        Canvas.SetLeft(_bar, 0);
        Canvas.SetTop(_bar, (SunLayout.Dot - FamilyShape.Track) / 2);

        var (from, to) = plan.Daylight;
        _bar.Show(to, from);

        Place(_barDot, new PlotPoint(SunLayout.DotAt(plan.Now, width), half));
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
