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
/// [S-3100] The sun's day (its path, sunrise, sunset and daylight), computed offline from a
/// configured latitude and longitude, on the family's blocks: a Chart part (the path, or the day as a
/// bar) and fact rows. The moon is its own widget (the Moon module).
/// <para>
/// The path is the sun's <b>real</b> one — altitude against how far it is through its passage across
/// the sky — so it flattens in winter, towers in summer and barely lifts off the horizon inside the
/// polar circles. [S-167] Where the sun has been is the Chart's series: the accent at its stroke over
/// a wash of it; the stretch still to come is the Track role at the stroke, the night stretch under
/// the horizon a Track hairline, carried on the same way round the sky (sets right, nadir, rises
/// left) with its depth compressed against the daylight scale. [S-168]
/// </para>
/// <para>
/// [S-3102] The ladder is <see cref="SunLayout"/>; this view only builds it. [S-6045] Three styles:
/// Arc is the path; Day bar draws the local day as the Chart's track, the daylight its accent fill and
/// now a dot on it (<see cref="SunDayBar"/>, [S-6046]); Times draws no graphic, the rows carry it all
/// ([S-170]). All the astronomy lives in <see cref="Themia.Core.Astronomy"/> as pure functions of an
/// instant, so this file only turns numbers into shapes.
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

    private readonly StackPanel _rowStack = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly SkyEventRow _sunriseRow = new(Glyphs.Sun);
    private readonly SkyEventRow _sunsetRow = new(Glyphs.Sun);
    private readonly SkyEventRow _daylightRow = new(Glyphs.Clock);
    private readonly SkyEventRow _noonRow = new(Glyphs.Sun);
    private readonly SkyEventRow _dawnRow = new(Glyphs.Sun);
    private readonly SkyEventRow _duskRow = new(Glyphs.Moon);

    /// <summary>The rows in <see cref="SunLayout.Ladder"/> order.</summary>
    private readonly SkyEventRow[] _ladder;

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
    private SunPlan _plan;

    public SunView()
    {
        _ladder = [_sunriseRow, _sunsetRow, _daylightRow, _noonRow, _dawnRow, _duskRow];
        LabelRows();

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
        _canvas.Margin = new Thickness(FamilySpace.Inset, 0, 0, 0);

        foreach (var row in _ladder)
        {
            _rowStack.Children.Add(row.Root);
        }

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Children.Add(_canvas);
        Children.Add(_rowStack);

        // Nothing shows until the first lay: a view waiting for setup is never attached, and the card's Set up row stands alone.
        _canvas.Visibility = Visibility.Collapsed;
        _rowStack.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// The fixed row labels, in the app's language. The first row, whose label follows the next
    /// event, is labelled again on every refresh.
    /// </summary>
    private void LabelRows()
    {
        _sunsetRow.SetLabel(Str.WidgetUi_Sunset.Text(), SunStr.WidgetsC_SunMoon_Short_Sunset.Text());
        _daylightRow.SetLabel(SunStr.WidgetUi_Daylight.Text(), SunStr.WidgetsC_SunMoon_Short_Daylight.Text());
        _noonRow.SetLabel(SunStr.WidgetsC_SunMoon_SolarNoon.Text());
        _dawnRow.SetLabel(SunStr.WidgetsC_SunMoon_Dawn.Text());
        _duskRow.SetLabel(SunStr.WidgetsC_SunMoon_Dusk.Text());
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>The language changed: relabel the rows and rewrite every reading.</summary>
    private void OnLanguage(Locale locale)
    {
        LabelRows();
        Lay();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as SunConfig ?? new SunConfig();
        PublishPlace();
        Resubscribe();

        // Lay ends in a Refresh, so the rows and the drawing pick the new config up in the same pass.
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

    /// <summary>[S-170] [S-3102] What is shown at this size, from <see cref="SunLayout"/>.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        _plan = SunLayout.Plan(_area, _config.Style, _config.ShowEvents);
        var plan = _plan;
        var bar = _config.Style == SunStyle.DayBar;

        _canvas.Visibility = Show(plan.GraphicWidth > 0);
        _canvas.Width = plan.GraphicWidth;
        _canvas.Height = plan.GraphicHeight;

        // [S-6005] One style's shapes, never the other's.
        foreach (var shape in (UIElement[])[_elapsedFill, _dayTrack, _nightTrack, _doneTrack, _horizon])
        {
            shape.Visibility = Show(!bar);
        }

        _bar.Visibility = Show(bar);

        // The rows: 8 under the graphic, beside it from the top, or from the top with no graphic. They
        // carry their own side inset.
        var graphic = plan.GraphicWidth > 0;
        ColumnDefinitions[0].Width = plan.Side ? new GridLength(plan.RowsLeft) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = plan.Side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SetRow(_rowStack, graphic && !plan.Side ? 1 : 0);
        SetRowSpan(_rowStack, graphic && !plan.Side ? 1 : 2);
        SetColumn(_rowStack, plan.Side ? 1 : 0);
        SetColumnSpan(_rowStack, plan.Side ? 1 : 2);
        _rowStack.Margin = new Thickness(0, graphic && !plan.Side ? FamilySpace.S2 : 0, 0, 0);
        _rowStack.Visibility = Show(plan.Rows > 0);
        var room = SkyRows.Room(_area.Width - plan.RowsLeft);
        for (var i = 0; i < _ladder.Length; i++)
        {
            var row = _ladder[i];
            row.Root.Visibility = Show(i < plan.Rows);
            row.Lines = plan.Lines;
            row.Room = room;
        }

        Refresh();
    }

    /// <summary>Recompute the rows and redraw. Called on a tick, a config change and a resize.</summary>
    private void Refresh()
    {
        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;

        var times = SolarPosition.TimesFor(_now, latitude, longitude);
        var next = SolarPosition.NextEvent(_now, latitude, longitude);

        var sunrise = Clock(times.Sunrise);
        var sunset = Clock(times.Sunset);
        var daylight = SkyText.Daylight(times);

        // The time remaining rides on whichever of the two rows is next, and that row is emphasised.
        var countdown = next is { } evt ? SkyText.Countdown(evt.At - _now) : null;
        var riseCountdown = next?.Kind == SunEventKind.Sunrise ? countdown : null;
        var setCountdown = next?.Kind == SunEventKind.Sunset ? countdown : null;

        // [S-1492] One row: the next event, in the first row's place.
        if (_plan.Rows == 1 && next?.Kind == SunEventKind.Sunset)
        {
            _sunriseRow.SetLabel(Str.WidgetUi_Sunset.Text(), SunStr.WidgetsC_SunMoon_Short_Sunset.Text());
            _sunriseRow.Set(sunset, setCountdown);
        }
        else
        {
            _sunriseRow.SetLabel(Str.WidgetUi_Sunrise.Text(), SunStr.WidgetsC_SunMoon_Short_Sunrise.Text());
            _sunriseRow.Set(sunrise, riseCountdown);
        }

        _sunsetRow.Set(sunset, setCountdown);

        // [S-1490] The change since yesterday is the daylight row's note; noon carries the sun's height.
        var change = SolarPosition.DaylightChange(_now, latitude, longitude);
        _daylightRow.SetPlain(daylight, change is { } delta ? SkyText.Change(delta) : null);

        var noon = SolarPosition.NoonFor(_now, latitude, longitude);
        _noonRow.SetPlain(Clock(noon.At), SkyText.Degrees(noon.AltitudeDegrees));

        var twilight = SolarPosition.CivilTwilightFor(_now, latitude, longitude);
        var civil = SunStr.WidgetsC_SunMoon_Civil.Text();
        _dawnRow.SetPlain(Clock(twilight.Sunrise), civil);
        _duskRow.SetPlain(Clock(twilight.Sunset), civil);

        Redraw();
    }

    /// <summary>
    /// Rebuild the drawing. Cheap by construction: a few shapes and about a hundred points,
    /// recomputed only on a tick, a config change or a resize — never per frame.
    /// </summary>
    private void Redraw()
    {
        var (width, height) = (_plan.GraphicWidth, _plan.GraphicHeight);
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

    /// <summary>
    /// A wall-clock time for an instant on the language's clock ("07:28", "7:28 AM"). An absent time is
    /// an em dash, because inside the polar circles there is no event to show. [S-161]
    /// </summary>
    private static string Clock(DateTimeOffset? moment)
        => moment is { } instant ? LocaleFormat.Time(TimeOnly.FromDateTime(instant.ToLocalTime().DateTime)) : "—";

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>[S-2260] The header names the widget ("Sun"); the place stands in its label, none once it is cleared ([S-2848]).</summary>
    private void PublishPlace() => _about.SetAbout(_config.PlaceLabel);
}
