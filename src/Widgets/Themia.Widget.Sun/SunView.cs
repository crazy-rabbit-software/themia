using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
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
/// bar (Day bar, <see cref="SunDayBar"/>, [S-6046]), filling the box it is given in the inset. The
/// times are the <c>sunTimes</c> atom's, never drawn here.
/// <para>
/// The path is the sun's <b>real</b> one — altitude against how far it is through its passage across
/// the sky — so it flattens in winter, towers in summer and barely lifts off the horizon inside the
/// polar circles. [S-167] Where the sun has been is the Chart's series: the accent at its stroke over
/// a wash of it; the stretch still to come is the Track role at the stroke, the night stretch under
/// the horizon a Track hairline. [S-168] All the astronomy lives in <see cref="Themia.Core.Astronomy"/>.
/// </para>
/// </summary>
internal sealed partial class SunView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware
{
    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,

        // [S-1113] The day is a time axis (and east to west): left to right in every language.
        FlowDirection = FlowDirection.LeftToRight,
    };

    private readonly Polygon _elapsedFill = new() { Opacity = FamilyShape.Wash };
    private readonly Polyline _dayTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _nightTrack = new() { StrokeThickness = FamilyShape.Hairline, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polyline _doneTrack = new() { StrokeThickness = FamilyShape.Stroke, StrokeLineJoin = PenLineJoin.Round };
    private readonly Line _horizon = new() { StrokeThickness = FamilyShape.Hairline };
    private readonly Ellipse _sun = new() { Width = SunLayout.Dot, Height = SunLayout.Dot };

    // [S-6046] The day bar: the day as the Chart's track, the daylight its fill.
    private readonly ChartBar _bar = new();

    private SunConfig _config = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    /// <summary>[S-2260] The header's trailing parts: the place as its label beside the widget's name.</summary>
    private readonly HeaderParts _about = new();

    public FrameworkElement? HeaderTrailing => _about;
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private DateTimeOffset _now = DateTimeOffset.Now;

    private WidgetArea _area;

    /// <summary>The graphic's box: the content less the inset either side and at the foot.</summary>
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

        _canvas.Children.Add(_elapsedFill);
        _canvas.Children.Add(_dayTrack);
        _canvas.Children.Add(_nightTrack);
        _canvas.Children.Add(_doneTrack);
        _canvas.Children.Add(_horizon);
        _canvas.Children.Add(_bar);
        _canvas.Children.Add(_sun);
        // [S-6703] The card's inset on the sides and the foot; the top is the card's ([S-11600]).
        _canvas.Margin = Family.Inset with { Top = 0 };
        Children.Add(_canvas);

        // Nothing shows until the first lay: a view waiting for setup is never attached, and the card's Set up row stands alone.
        _canvas.Visibility = Visibility.Collapsed;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>The language changed: the drawing has no words, but the header's label may.</summary>
    private void OnLanguage(Locale locale) => Lay();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as SunConfig ?? new SunConfig();
        PublishPlace();
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
        var interval = _config.TickInterval;

        // Only when the cadence actually changed: remaking the subscription on every config touch
        // made dragging a settings slider stutter for every widget on the desktop.
        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, Tick);
    }

    private void Tick()
    {
        _now = DateTimeOffset.Now;
        Refresh();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    /// <summary>[S-11110] The graphic fills its box: the content in the inset, whatever the style.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        var bar = _config.Style == SunStyle.DayBar;
        _box = (Math.Max(0, _area.Width - (2 * FamilySpace.Inset)), bar ? SunLayout.Dot : Math.Max(0, _area.Height - FamilySpace.Inset));
        _canvas.Visibility = Visibility.Visible;
        _canvas.Width = _box.Width;
        _canvas.Height = _box.Height;

        // [S-6005] One style's shapes, never the other's.
        foreach (var shape in (UIElement[])[_elapsedFill, _dayTrack, _nightTrack, _doneTrack, _horizon])
        {
            shape.Visibility = Show(!bar);
        }

        _bar.Visibility = Show(bar);
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
        if (_canvas.Visibility != Visibility.Visible || !(width > 0) || !(height > 0))
        {
            return;
        }

        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;

        // A marker at the edge of the plot would otherwise bleed over the rows beneath it.
        _canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };

        if (_config.Style == SunStyle.DayBar)
        {
            RedrawBar(width, SunDayBar.Of(_now, SolarPosition.TimesFor(_now, latitude, longitude), TimeZoneInfo.Local));
            return;
        }

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
        Place(plot.PointAt(new SkySample(position.Progress, position.AltitudeDegrees)));
        _sun.Fill = Family.Brush(position.Up ? ColourRole.Accent : ColourRole.TextTertiary);
    }

    /// <summary>
    /// [S-6046] The day bar: midnight to midnight across as the Chart's track, the daylight its accent
    /// fill, now a dot on it (primary in daylight, tertiary at night). The track is inset by the dot's
    /// half either end, so the dot at midnight is not cut by the edge.
    /// </summary>
    private void RedrawBar(double width, SunDayBarPlan plan)
    {
        var half = SunLayout.Dot / 2;
        var span = Math.Max(1, width - SunLayout.Dot);
        _bar.Width = span;
        Canvas.SetLeft(_bar, half);
        Canvas.SetTop(_bar, (SunLayout.Dot - FamilyShape.Track) / 2);

        var (from, to) = plan.Daylight;
        _bar.Show(to, from);

        Place(new PlotPoint(half + (plan.Now * span), half));
        _sun.Fill = Family.Brush(plan.NowLit ? ColourRole.TextPrimary : ColourRole.TextTertiary);
    }

    private void Place(PlotPoint centre)
    {
        Canvas.SetLeft(_sun, centre.X - (SunLayout.Dot / 2));
        Canvas.SetTop(_sun, centre.Y - (SunLayout.Dot / 2));
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

    /// <summary>[S-2260] The header names the widget ("Sun"); the place stands in its label, none once it is cleared ([S-2848]).</summary>
    private void PublishPlace() => _about.SetAbout(_config.PlaceLabel);
}
