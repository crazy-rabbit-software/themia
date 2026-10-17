using Microsoft.UI;
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
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3100] The sun's day (its path, sunrise, sunset and daylight), computed offline from a
/// configured latitude and longitude. The moon is its own widget (the Moon module).
/// <para>
/// The drawing is the sun's <b>real</b> path — altitude against how far it is through its passage
/// across the sky — so it flattens in winter, towers in summer and barely lifts off the horizon
/// inside the polar circles. [S-167] Elapsed daylight is filled in behind the sun; the night stretch
/// carries on the same way round the sky, sets right, nadir, rises left, with its depth compressed
/// against the daylight scale because a true-to-scale winter midnight would be deeper than the
/// whole widget. [S-168]
/// </para>
/// <para>
/// [S-1492] Its disclosure ladder: the graphic (never dropped, 46px floor); the number strip; the
/// event rows in priority order — sunrise and sunset (just the next of the two when only one row
/// fits), daylight with its change since yesterday, solar noon with the sun's height, civil dawn
/// and dusk. On a strip the rows stand beside the graphic; on a large tile the graphic keeps at
/// least 45% of the height and the rows go two to a line.
/// </para>
/// <para>
/// The drawing is a Windows control, not an illustration: the travelled arc and the daylight behind
/// it are the theme's accent, the untravelled arc and the night stretch are the theme's greys, the
/// horizon is a hairline in the divider colour, and the sun below the horizon changes to the
/// theme's <i>disabled</i> accent rather than being faded with an opacity. Nothing here names a
/// colour of its own, so the whole graphic follows light, dark and the high-contrast themes.
/// </para>
/// <para>
/// All the astronomy lives in <see cref="Themia.Core.Astronomy"/> as pure functions of an instant,
/// so this file only turns numbers into shapes.
/// </para>
/// <para>
/// [S-6045] Three styles, what was the sun path mode: Arc is the path above; Day bar draws the
/// local day as a bar, the daylight in the accent between sunrise and sunset ticks and the sun's
/// dot at now (<see cref="SunDayBar"/>, [S-6046]); Times draws no graphic, the rows carry it all
/// ([S-170]). The number strip and the rows are the same for the two graphics.
/// </para>
/// </summary>
internal sealed partial class SunView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware
{
    /// <summary>[S-170] The graphic is never dropped; below this it is simply drawn smaller.</summary>
    private const double GraphicFloor = 46;

    /// <summary>[S-1492] On a tile Expanded in height the graphic keeps this share of it; rows take the rest.</summary>
    private const double GraphicShare = 0.45;

    /// <summary>The number strip's height at its smallest step: a Caption line plus its top margin.</summary>
    private const double StripFloor = 24;

    /// <summary>[S-6046] The day bar's own floor: the bar and the ticks over and under it.</summary>
    private const double BarFloor = 28;

    private readonly Canvas _canvas = new();
    private readonly Polygon _elapsedFill = new();
    private readonly Polyline _dayTrack = new();
    private readonly Polyline _nightTrack = new();
    private readonly Polyline _doneTrack = new();
    private readonly Line _horizon = new();
    private readonly Ellipse _sun = new();

    // [S-6046] The day bar: the whole day, the daylight over it, and the sunrise and sunset ticks.
    private readonly Rectangle _barDay = new();
    private readonly Rectangle _barLight = new();
    private readonly Line _riseTick = new();
    private readonly Line _setTick = new();

    private readonly Grid _strip;
    private readonly StripLabel _riseLabel;
    private readonly StripLabel _dayLabel;
    private readonly StripLabel _setLabel;

    private readonly Grid _body;
    private readonly Grid _rowGrid = new();
    private readonly SkyEventRow _sunriseRow;
    private readonly SkyEventRow _sunsetRow;
    private readonly SkyEventRow _daylightRow;
    private readonly SkyEventRow _noonRow;
    private readonly SkyEventRow _dawnRow;
    private readonly SkyEventRow _duskRow;

    /// <summary>The rows in the order they are given up: the last one goes first.</summary>
    private readonly SkyEventRow[] _ladder;

    /// <summary>The accent, as a fill. The travelled arc, the daylight behind it and the sun.</summary>
    private readonly Brush _accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);

    /// <summary>The theme's own dimmed accent, for the sun once it is under the horizon.</summary>
    private readonly Brush _accentDim = ThemeBrush.Of("AccentFillColorDisabledBrush", Colors.SlateGray);

    private readonly Brush _tertiary = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _disabled = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.DimGray);
    private readonly Brush _divider = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray);
    private readonly Brush _markerRing = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black);

    private SunConfig _config = new();

    /// <summary>The card this view draws on, which owns the header it names.</summary>
    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private DateTimeOffset _now = DateTimeOffset.Now;

    private WidgetArea _area;

    /// <summary>How many rows the ladder settled on; one means "the next event only".</summary>
    private int _rowCount;

    public SunView()
    {
        BuildCanvas();

        _riseLabel = new StripLabel(Glyphs.Sun);
        _dayLabel = new StripLabel(Glyphs.Clock);
        _setLabel = new StripLabel(Glyphs.Sun);
        _strip = BuildStrip();

        _sunriseRow = new SkyEventRow(Fluent.Icon(Glyphs.Sun));
        _sunsetRow = new SkyEventRow(Fluent.Icon(Glyphs.Sun));
        _daylightRow = new SkyEventRow(Fluent.Icon(Glyphs.Clock));
        _noonRow = new SkyEventRow(Fluent.Icon(Glyphs.Sun));
        _dawnRow = new SkyEventRow(Fluent.Icon(Glyphs.Sun));
        _duskRow = new SkyEventRow(Fluent.Icon(Glyphs.Moon));
        LabelRows();

        _ladder = [_sunriseRow, _sunsetRow, _daylightRow, _noonRow, _dawnRow, _duskRow];

        _rowGrid.Margin = new Thickness(0, 0, 0, Fluent.Space2);
        _rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        foreach (var row in _ladder)
        {
            _rowGrid.Children.Add(row.Root);
        }

        // The graphic and its number strip share a column.
        var graphicColumn = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
        };

        SetRow(_canvas, 0);
        SetRow(_strip, 1);
        graphicColumn.Children.Add(_canvas);
        graphicColumn.Children.Add(_strip);

        _body = new Grid
        {
            // The rows carry the inset as their own padding, so only the parts above them take it
            // as a margin; that is what lets a row highlight reach both edges of the widget.
            Margin = new Thickness(Fluent.Space4, 0, Fluent.Space4, Fluent.Space2),
        };

        _body.Children.Add(graphicColumn);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Children.Add(_body);
        Children.Add(_rowGrid);

        _canvas.SizeChanged += (_, _) => Redraw();
    }

    /// <summary>
    /// The fixed row labels, in the app's language. The first row, whose label follows the next
    /// event, is labelled again on every refresh.
    /// </summary>
    private void LabelRows()
    {
        _sunsetRow.SetLabel(Str.WidgetUi_Sunset.Text(), Str.WidgetsC_SunMoon_Short_Sunset.Text());
        _daylightRow.SetLabel(Str.WidgetUi_Daylight.Text(), Str.WidgetsC_SunMoon_Short_Daylight.Text());
        _noonRow.SetLabel(Str.WidgetsC_SunMoon_SolarNoon.Text());
        _dawnRow.SetLabel(Str.WidgetsC_SunMoon_Dawn.Text());
        _duskRow.SetLabel(Str.WidgetsC_SunMoon_Dusk.Text());
    }

    private void BuildCanvas()
    {
        // [S-1113] The day's arc is a time axis (and east to west): left to right in every language.
        _canvas.FlowDirection = FlowDirection.LeftToRight;

        // The elapsed daylight is the accent behind the curve. The alpha is the shape's, never the
        // shared brush's: the brush comes out of the application's resources and is the same object
        // every other accent surface in the app is painted with.
        _elapsedFill.Fill = _accent;
        _elapsedFill.Opacity = Fluent.ChartWash;

        // The stretch still to come: present, but plainly secondary.
        _dayTrack.Stroke = _tertiary;
        _dayTrack.StrokeThickness = Fluent.Hairline;
        _dayTrack.StrokeLineJoin = PenLineJoin.Round;

        // Under the horizon. Dashed and in the theme's disabled text grey rather than a faded copy
        // of the day track.
        _nightTrack.Stroke = _disabled;
        _nightTrack.StrokeThickness = Fluent.Hairline;
        _nightTrack.StrokeDashArray = [3, 3];

        // Where the sun has already been, in the accent: the one emphasised line in the drawing.
        _doneTrack.Stroke = _accent;
        _doneTrack.StrokeThickness = Fluent.ChartStroke;
        _doneTrack.StrokeLineJoin = PenLineJoin.Round;

        // A hairline in the colour Windows rules with, not a faded text colour.
        _horizon.Stroke = _divider;
        _horizon.StrokeThickness = Fluent.Hairline;

        _sun.Fill = _accent;

        // The day bar: the night in the disabled control fill, the daylight the accent's lighter
        // step (so the sun's dot, in the accent itself, stands on it), the ticks the tertiary grey.
        ItemFill.Paint(_barDay, ItemRole.Track, ItemState.Disabled);
        _barLight.Fill = ThemeBrush.Of("AccentFillColorTertiaryBrush", Colors.LightSkyBlue);
        _riseTick.Stroke = _tertiary;
        _riseTick.StrokeThickness = Fluent.Hairline;
        _setTick.Stroke = _tertiary;
        _setTick.StrokeThickness = Fluent.Hairline;

        _canvas.MinHeight = GraphicFloor;
        _canvas.HorizontalAlignment = HorizontalAlignment.Stretch;
        _canvas.VerticalAlignment = VerticalAlignment.Stretch;
        _canvas.IsHitTestVisible = false;

        _canvas.Children.Add(_elapsedFill);
        _canvas.Children.Add(_dayTrack);
        _canvas.Children.Add(_nightTrack);
        _canvas.Children.Add(_doneTrack);
        _canvas.Children.Add(_horizon);
        _canvas.Children.Add(_barDay);
        _canvas.Children.Add(_barLight);
        _canvas.Children.Add(_riseTick);
        _canvas.Children.Add(_setTick);
        _canvas.Children.Add(_sun);
    }

    /// <summary>
    /// The number strip. Its three columns put each label on the gridline of the curve point it
    /// names — 13%, 50% and 87%, the same anchors the plot uses for sunrise, noon and sunset.
    /// </summary>
    private Grid BuildStrip()
    {
        var strip = new Grid
        {
            Margin = new Thickness(0, Fluent.Space1, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(0.26, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.48, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(0.26, GridUnitType.Star) },
            },
        };

        SetColumn(_riseLabel.Root, 0);
        SetColumn(_dayLabel.Root, 1);
        SetColumn(_setLabel.Root, 2);

        strip.Children.Add(_riseLabel.Root);
        strip.Children.Add(_dayLabel.Root);
        strip.Children.Add(_setLabel.Root);

        return strip;
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
        Refresh();
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as SunConfig ?? new SunConfig();
        PublishTitle();

        Resubscribe();

        // Lay ends in a Refresh, so the readout and the drawing pick the new config up in the same
        // pass rather than in two.
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

        // Only when the cadence actually changed. Dropping and remaking the subscription on every
        // config touch made dragging a settings slider stutter, because each frame tore down the
        // timer subscription for every widget on the desktop and built it again.
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

    /// <summary>
    /// [S-170]/[S-1492] What is shown at this size.
    /// <para>
    /// The graphic is never dropped — the user asked for it — so only extras shed as the tile
    /// shrinks: first the countdown, then the label icons, then the
    /// daylight label, then the number strip. The rows are a ladder on <see cref="Disclosure"/>:
    /// whole rows of what the graphic leaves, in priority order. With the graphic hidden every row
    /// that fits shows, since a widget showing nothing is never the answer.
    /// </para>
    /// </summary>
    private void Lay()
    {
        var width = _area.Width;
        var height = _area.Height;

        // [S-6045] Times is the old hidden path: no graphic at all.
        var graphic = _config.Style != SunStyle.Times;
        var bar = _config.Style == SunStyle.DayBar;
        var floor = bar ? BarFloor : GraphicFloor;

        // The two settings are independent, with one guard: with the graphic hidden the rows are
        // forced on. [S-170]
        var rows = _config.ShowEvents || !graphic;
        var numbers = graphic;

        // Windows' 32px list row on a tile that is small either way; the full 40 from a panel.
        var compact = _area.WidthClass == SizeClass.Compact || _area.HeightClass <= SizeClass.Regular;
        var rowHeight = compact ? Fluent.RowHeightCompact : Fluent.RowHeight;

        // [S-1492] A strip puts the rows beside the graphic; a large tile two rows to a line.
        var side = graphic && rows && _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Regular;
        var pairs = rows && !side && _area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);
        var graphicWidth = side ? Math.Floor(width * 0.5) : width;
        var rowWidth = side ? width - graphicWidth : pairs ? width / 2 : width;

        // The thresholds are the width the shed-able parts need at their ramp step, so nothing ever
        // has to be clipped to fit.
        //
        // [S-649] The strip's side columns are 26% of the content width, and the extras have to fit
        // in one of those, not in the tile: an icon beside "07:28" needs about 56px, which a
        // column only has from a 250px tile, and "in 6h 45m" under it needs about 64px, from
        // 280px. At the old 160 and 190 a 200px tile drew each icon over the time beside it
        // ("07:2⊙ 11h 33m☼ 19:0") and trimmed the countdown to "in 6…".
        //
        // [S-1109] On top of those, each text that differs by language is measured in the words it
        // actually has: German's "in 10 h 4 min" is half again as long as "in 10h 4m", and is shed
        // from a column it would be trimmed in.
        // A side column must hold the time at the smallest step at least: "11:58 PM" on a 12-hour
        // clock does not fit a 200px tile's, where "23:58" does, and the rows say it instead.
        var showStrip = numbers && height >= 80 && graphicWidth >= 120
            && StripColumn(graphicWidth) >= FluentType.EstimateWidth(FluentType.Caption, WidestTime().Length) + Fluent.Space1;
        var showCountdown = graphicWidth >= 280 && height >= 120
            && StripColumn(graphicWidth) >= FluentType.EstimateWidth(FluentType.Caption, WidestCountdown().Length);
        var showDaylightLabel = graphicWidth >= 140;
        var showSmallExtras = graphicWidth >= 250;
        var rowNotes = rowWidth >= 280;

        // What the rows may have: everything the graphic does not keep. On a strip the rows have the
        // whole height beside it.
        var room = new Disclosure(width, height - Fluent.Space2);

        if (graphic && !side)
        {
            // [S-6046] A bar needs no share of a large tile: its floor and the strip; the rows take the rest.
            var keep = _area.HeightClass >= SizeClass.Expanded && !bar
                ? Math.Max(GraphicFloor, height * GraphicShare)
                : floor + (showStrip ? StripFloor : 0);
            room.Reserve(keep);
        }

        var lines = rows ? room.TakeRows(rowHeight, pairs ? (_ladder.Length + 1) / 2 : _ladder.Length) : 0;
        _rowCount = rows ? Math.Min(_ladder.Length, pairs ? lines * 2 : lines) : 0;

        // [S-1492] Below two rows a pair of sunrise/sunset is half an answer: the one row is the next
        // event, whichever it is (the Refresh fills it in).
        PlaceRows(pairs, side);

        _canvas.Visibility = Show(graphic);
        _body.Visibility = Show(graphic);

        _strip.Visibility = Show(showStrip);
        _rowGrid.Visibility = Show(_rowCount > 0);

        // What a row has for its label, note and value: its width less its padding and glyph.
        var rowRoom = rowWidth - (2 * Fluent.Space4) - Fluent.IconSize - Fluent.Space3 - (pairs ? Fluent.Space2 / 2 : 0);

        foreach (var row in _ladder)
        {
            row.SetDensity(compact);
            row.Room = rowRoom;
            row.AllowCountdown = showCountdown || (side && rowNotes);
            row.AllowNote = rowNotes;
        }


        _riseLabel.AllowCountdown = showCountdown;
        _dayLabel.AllowCountdown = false;
        _setLabel.AllowCountdown = showCountdown;

        // The graphic keeps its floor but gives the rest of the tile back when the rows are hidden.
        _canvas.MinHeight = graphic ? floor : 0;

        // [S-6005] One style's shapes, never the other's.
        foreach (var shape in (UIElement[])[_elapsedFill, _dayTrack, _nightTrack, _doneTrack, _horizon])
        {
            shape.Visibility = Show(!bar);
        }

        foreach (var shape in (UIElement[])[_barDay, _barLight, _riseTick, _setTick])
        {
            shape.Visibility = Show(bar);
        }

        ApplyType(graphicWidth, side ? height : height - (lines * rowHeight), showDaylightLabel, showSmallExtras);
        Refresh();
    }

    /// <summary>
    /// Put the graphic and the rows where this shape wants them, and the first <see cref="_rowCount"/>
    /// rows of the ladder into the row grid — one column, or two to a line on a large tile.
    /// </summary>
    private void PlaceRows(bool pairs, bool side)
    {
        // Column: graphic over rows. Side: graphic left, rows right, both full height. Graphic hidden:
        // the rows sit at the top, because nothing above them is pushing them down.
        var graphic = _config.Style != SunStyle.Times;

        SetRow(_body, 0);
        SetColumn(_body, 0);
        SetRowSpan(_body, side ? 2 : 1);
        SetColumnSpan(_body, side ? 1 : 2);

        SetRow(_rowGrid, side || !graphic ? 0 : 1);
        SetColumn(_rowGrid, side ? 1 : 0);
        SetRowSpan(_rowGrid, side || !graphic ? 2 : 1);
        SetColumnSpan(_rowGrid, side ? 1 : 2);

        ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[0].Height = graphic ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[1].Height = graphic && !side ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        _rowGrid.VerticalAlignment = graphic && !side ? VerticalAlignment.Bottom : side ? VerticalAlignment.Center : VerticalAlignment.Top;

        _rowGrid.ColumnDefinitions[1].Width = pairs ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _rowGrid.ColumnSpacing = pairs ? Fluent.Space2 : 0;

        while (_rowGrid.RowDefinitions.Count < _ladder.Length)
        {
            _rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < _ladder.Length; i++)
        {
            var root = _ladder[i].Root;
            var shown = i < _rowCount;
            root.Visibility = Show(shown);

            // Pairs read across: sunrise beside sunset, daylight beside noon, dawn beside dusk.
            SetRow(root, pairs ? i / 2 : i);
            SetColumn(root, pairs ? i % 2 : 0);
        }
    }

    /// <summary>
    /// Pick the ramp steps and the drawn sizes for this tile.
    /// <para>
    /// Only the number strip actually scales, because it is the graphic's readout and a large tile
    /// should say it loudly. The rows stay on their list-row steps at every size: Windows grows a
    /// list by showing more of it, not by growing its type.
    /// </para>
    /// </summary>
    private void ApplyType(double width, double height, bool daylightLabel, bool icons)
    {
        // Each strip column is 26% of the content width, and holds an icon, a time and a little
        // slack: three characters' worth beside the time, eight for "07:28", eleven for "11:58 PM".
        var column = StripColumn(width);
        var band = Math.Max(0, (height - GraphicFloor) * 0.4);
        var time = WidestTime().Length;
        var value = FluentType.Strong(FluentType.Fit(column, band, Math.Max(8, time + 3), FluentType.Caption, FluentType.Subtitle));

        _riseLabel.SetStep(value);
        _dayLabel.SetStep(value);
        _setLabel.SetStep(value);

        // The icons only where the time still fits beside one at this step.
        icons = icons && column >= FluentType.EstimateWidth(value, time) + Fluent.IconSize + Fluent.Space1;
        _riseLabel.ShowIcon(icons);
        _dayLabel.ShowIcon(icons);
        _setLabel.ShowIcon(icons);

        // The daylight sits in the middle column (48%), and only where its longest wording fits at
        // the step just chosen, beside its icon when that shows: "12 h 32 min" crowded both times
        // on a 200px tall tile in German.
        var middle = Math.Max(0, (width - (Fluent.Space4 * 2)) * 0.48);
        // A gap either side, so it never touches the times beside it.
        var needed = FluentType.EstimateWidth(value, WidestDuration().Length) + (icons ? Fluent.IconSize + Fluent.Space1 : 0) + (2 * Fluent.Space2);
        _dayLabel.Root.Visibility = Show(daylightLabel && middle >= needed);
    }

    /// <summary>One of the strip's side columns: 26% of the content width.</summary>
    private static double StripColumn(double graphicWidth) => Math.Max(0, (graphicWidth - (Fluent.Space4 * 2)) * 0.26);

    /// <summary>The longest a time of day can read on the current language's clock ("11:58 PM").</summary>
    private static string WidestTime() => LocaleFormat.Time(new TimeOnly(23, 58));

    /// <summary>The longest a daylight span can read in the current language ("23 h 59 min").</summary>
    private static string WidestDuration()
        => Str.WidgetsC_SunMoon_Duration_HoursMinutes.Format(("hours", 23), ("minutes", 59));

    /// <summary>The longest a countdown can read in the current language ("in 23 h 59 min").</summary>
    private static string WidestCountdown() => Str.SunMoon_InTime.Format(("n", WidestDuration()));

    /// <summary>Recompute the readout and redraw. Called on a tick, a config change and a resize.</summary>
    private void Refresh()
    {
        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;

        var times = SolarPosition.TimesFor(_now, latitude, longitude);
        var next = SolarPosition.NextEvent(_now, latitude, longitude);

        var sunrise = Clock(times.Sunrise);
        var sunset = Clock(times.Sunset);
        var daylight = SkyText.Daylight(times);

        // The time remaining rides on whichever of the two rows is next, and that row is
        // emphasised. There is no separate banner.
        var countdown = next is { } evt ? SkyText.Countdown(evt.At - _now) : null;
        var riseCountdown = next?.Kind == SunEventKind.Sunrise ? countdown : null;
        var setCountdown = next?.Kind == SunEventKind.Sunset ? countdown : null;

        _riseLabel.Set(sunrise, riseCountdown);
        _dayLabel.Set(daylight, null);
        _setLabel.Set(sunset, setCountdown);

        // [S-1492] One row: the next event, in the first row's place.
        if (_rowCount == 1 && next?.Kind == SunEventKind.Sunset)
        {
            _sunriseRow.SetLabel(Str.WidgetUi_Sunset.Text(), Str.WidgetsC_SunMoon_Short_Sunset.Text());
            _sunriseRow.Set(sunset, setCountdown);
        }
        else
        {
            _sunriseRow.SetLabel(Str.WidgetUi_Sunrise.Text(), Str.WidgetsC_SunMoon_Short_Sunrise.Text());
            _sunriseRow.Set(sunrise, riseCountdown);
        }

        _sunsetRow.Set(sunset, setCountdown);

        // [S-1490] The change since yesterday is the daylight row's note; noon carries the sun's height.
        var change = SolarPosition.DaylightChange(_now, latitude, longitude);
        _daylightRow.SetPlain(daylight, change is { } delta ? SkyText.Change(delta) : null);

        var noon = SolarPosition.NoonFor(_now, latitude, longitude);
        _noonRow.SetPlain(Clock(noon.At), SkyText.Degrees(noon.AltitudeDegrees));

        var twilight = SolarPosition.CivilTwilightFor(_now, latitude, longitude);
        var civil = Str.WidgetsC_SunMoon_Civil.Text();
        _dawnRow.SetPlain(Clock(twilight.Sunrise), civil);
        _duskRow.SetPlain(Clock(twilight.Sunset), civil);

        Redraw();
    }

    /// <summary>
    /// Rebuild the drawing. Cheap by construction: six shapes and about a hundred points,
    /// recomputed only on a tick, a config change or a resize — never per frame.
    /// </summary>
    private void Redraw()
    {
        if (_canvas.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;

        if (!(width > 0) || !(height > 0))
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
            _elapsedFill.Visibility = Visibility.Visible;
            _doneTrack.Visibility = Visibility.Visible;
        }
        else
        {
            _elapsedFill.Visibility = Visibility.Collapsed;
            _doneTrack.Visibility = Visibility.Collapsed;
        }

        _horizon.X1 = 0;
        _horizon.X2 = width;
        _horizon.Y1 = plot.HorizonY;
        _horizon.Y2 = plot.HorizonY;

        var marker = plot.PointAt(new SkySample(position.Progress, position.AltitudeDegrees));
        Place(_sun, marker, MarkerRadius(width, height));

        // The sun below the horizon is still drawn, in the theme's disabled accent, so the night
        // stretch reads as the same journey rather than as an empty track.
        _sun.Fill = position.Up ? _accent : _accentDim;
        _sun.Stroke = null;

        // A marker at the edge of the plot would otherwise bleed over the rows beneath it.
        _canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
    }

    /// <summary>
    /// [S-6046] The day bar: midnight to midnight across, rounded at its ends; the daylight in the
    /// accent between the sunrise and sunset ticks, which stand a little over and under it; the
    /// sun's dot at now, dimmed while it is down. Thicker on a taller canvas, by steps of its height,
    /// and at the canvas's foot, right over the strip.
    /// </summary>
    private void RedrawBar(double width, double height, SunDayBarPlan plan)
    {
        // At the foot of the canvas, so the strip and the rows read on from it whatever room is left above.
        var band = Math.Clamp(Math.Floor(height * 0.3), 8, 20);
        var top = Math.Max(Fluent.Space1, Math.Floor(height - band - Fluent.Space2));
        var radius = band / 2;
        var marker = MarkerRadius(width, height) + 1;

        // The bar is inset by the marker, so the sun at midnight is not cut by the edge.
        var left = marker;
        var span = Math.Max(1, width - (2 * marker));

        _barDay.Width = span;
        _barDay.Height = band;
        _barDay.RadiusX = radius;
        _barDay.RadiusY = radius;
        Canvas.SetLeft(_barDay, left);
        Canvas.SetTop(_barDay, top);

        var (from, to) = plan.Daylight;
        _barLight.Width = Math.Max(0, (to - from) * span);
        _barLight.Height = band;
        _barLight.RadiusX = Math.Min(radius, _barLight.Width / 2);
        _barLight.RadiusY = radius;
        _barLight.Visibility = Show(to > from);
        Canvas.SetLeft(_barLight, left + (from * span));
        Canvas.SetTop(_barLight, top);

        Tick(_riseTick, plan.Rise);
        Tick(_setTick, plan.Set);

        Place(_sun, new PlotPoint(left + (plan.Now * span), top + (band / 2)), marker);
        _sun.Fill = plan.NowLit ? _accent : _accentDim;

        // A ring of the card's own colour round the dot, so it reads on the daylight as well as off it.
        _sun.Stroke = _markerRing;
        _sun.StrokeThickness = Fluent.ChartStroke;
        _canvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };

        void Tick(Line tick, double? at)
        {
            tick.Visibility = Show(at is not null);
            var x = Math.Round(left + ((at ?? 0) * span)) + 0.5;
            tick.X1 = x;
            tick.X2 = x;
            tick.Y1 = Math.Max(0, top - Fluent.Space1);
            tick.Y2 = Math.Min(height, top + band + Fluent.Space1);
        }
    }

    /// <summary>The sun's dot, in three steps rather than as a fraction of the canvas.</summary>
    private static double MarkerRadius(double width, double height)
    {
        var smaller = Math.Min(width, height);
        return smaller >= 160 ? 6 : smaller >= 96 ? 5 : 4;
    }

    private static void Place(Ellipse ellipse, PlotPoint centre, double radius)
    {
        ellipse.Width = radius * 2;
        ellipse.Height = radius * 2;
        Canvas.SetLeft(ellipse, centre.X - radius);
        Canvas.SetTop(ellipse, centre.Y - radius);
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
    /// A wall-clock time for an instant on the language's clock ("07:28", "7:28 AM"), as the
    /// original's locale time. An absent time is an em dash, because inside the polar circles there
    /// is no event to show. [S-161]
    /// </summary>
    private static string Clock(DateTimeOffset? moment)
        => moment is { } instant ? LocaleFormat.Time(TimeOnly.FromDateTime(instant.ToLocalTime().DateTime)) : "—";

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;


    /// <summary>One label in the number strip: a 16px glyph, a time, and the countdown under it.</summary>
    private sealed class StripLabel
    {
        private readonly FontIcon _icon;
        private readonly TextBlock _value = Fluent.Text(FluentType.BodyStrong);
        private readonly TextBlock _countdown = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");

        public StripLabel(string glyph)
        {
            _icon = Fluent.Icon(glyph);

            _countdown.HorizontalAlignment = HorizontalAlignment.Center;
            _countdown.Visibility = Visibility.Collapsed;

            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space1,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { _icon, _value },
            };

            Root = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { line, _countdown },
            };
        }

        public StackPanel Root { get; }

        public bool AllowCountdown { get; set; } = true;

        public void ShowIcon(bool visible) => _icon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>The strip's readout is the one thing here that scales, and it scales by step.</summary>
        public void SetStep(TypeStep step) => _value.Apply(step);

        public void Set(string value, string? countdown)
        {
            _value.Text = value;
            _countdown.Text = countdown ?? string.Empty;
            _countdown.Visibility = countdown is not null && AllowCountdown ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Tell the card what this widget is about. The catalogue can only say "Weather" or
    /// "Folder"; the widget knows which place, which folder, which ticker — and on a desktop
    /// holding three of them, that is the only thing telling them apart.
    /// </summary>
    private void PublishTitle()
    {
        var title = _config.Place;
        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }

}
