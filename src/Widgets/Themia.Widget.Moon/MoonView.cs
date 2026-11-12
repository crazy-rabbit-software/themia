using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Moon;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3100] The moon where the observer is, computed offline, on the family's blocks: a Readout of how
/// much of it is lit, the moon drawn at its phase as the readout's lead and the phase's name under it,
/// then moonrise, moonset and the next full or new moon as fact rows.
/// <para>
/// [S-3105] The ladder is <see cref="MoonLayout"/>; this view only builds it. The drawing is
/// <see cref="SkyMoonDisc"/>, the same lit-limb outline the rows' leads use, mirrored for a southern
/// observer and a role lower while the moon is below the horizon ([S-169]).
/// </para>
/// <para>
/// [S-6050] The Phase month style draws the month instead: one small disc a day, each that day's
/// phase at local noon (<see cref="MoonMonth"/>), reflowed to the tile by <see cref="DotGrid"/>,
/// today ringed in the accent, the month's name over them where it fits.
/// </para>
/// </summary>
internal sealed partial class MoonView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetAligned
{
    /// <summary>[S-6050] Under this disc size the month's name gives its line to the discs.</summary>
    private const double MonthNameDisc = FamilyShape.Icon;

    // The readout: the moon at the step's size, the lit share, the phase's name under both (as a
    // Readout's lead glyph and its line under stand).
    private readonly SkyMoonDisc _disc = new();
    private readonly ReadoutBlock _value = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _valueLine = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _readout = new() { VerticalAlignment = VerticalAlignment.Top };

    private readonly StackPanel _rowStack = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly SkyMoonDisc _riseGlyph = new();
    private readonly SkyMoonDisc _setGlyph = new();
    private readonly SkyMoonDisc _majorGlyph = new();
    private readonly SkyEventRow _riseRow;
    private readonly SkyEventRow _setRow;
    private readonly SkyEventRow _majorRow;

    /// <summary>The rows in <see cref="MoonLayout.Ladder"/> order.</summary>
    private readonly SkyEventRow[] _rows;

    private MoonConfig _config = new();
    /// <summary>[S-2260] The header's trailing parts: the place as its label beside the widget's name.</summary>
    private readonly HeaderParts _about = new();

    public FrameworkElement? HeaderTrailing => _about;
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;
    private WidgetArea _area;
    private MoonPlan _plan;

    /// <summary>[S-6583] Where the readout sits: the widget's alignment, at the start by default (the family's data widgets are start-aligned).</summary>
    private ContentAlign _align = ContentAlign.Left;

    /// <summary>What a row has for its name and value, from the last <see cref="Lay"/>.</summary>
    private double _rowRoom = double.PositiveInfinity;

    /// <summary>[S-6050] The Phase month style: the month's name over a disc a day.</summary>
    private readonly StackPanel _month = new() { VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
    private readonly TextBlock _monthName = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly Canvas _monthGrid = new() { HorizontalAlignment = HorizontalAlignment.Left, FlowDirection = FlowDirection.LeftToRight };
    private readonly List<SkyMoonDisc> _monthDiscs = [];
    private readonly Ellipse _todayRing = new() { StrokeThickness = FamilyShape.Stroke };

    /// <summary>The day the month was last drawn for: a new day redraws it, a tick on the same day does not.</summary>
    private DateOnly _monthDrawn;
    private DotGridPlan _monthPlan;

    public MoonView()
    {
        _disc.Root.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _disc.Root.VerticalAlignment = VerticalAlignment.Center;
        _valueLine.Children.Add(_disc.Root);
        _valueLine.Children.Add(_value);
        _readout.Children.Add(_valueLine);
        _readout.Children.Add(_name);

        _riseRow = new SkyEventRow(_riseGlyph.Root);
        _setRow = new SkyEventRow(_setGlyph.Root);
        _majorRow = new SkyEventRow(_majorGlyph.Root);
        _rows = [_riseRow, _setRow, _majorRow];
        foreach (var row in _rows)
        {
            _rowStack.Children.Add(row.Root);
        }

        _todayRing.Stroke = Family.Brush(ColourRole.Accent);
        _month.Spacing = FamilySpace.S2;
        _month.Margin = Family.SideInset;
        _month.Children.Add(_monthName);
        _month.Children.Add(_monthGrid);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Children.Add(_readout);
        Children.Add(_rowStack);
        SetColumnSpan(_month, 2);
        SetRowSpan(_month, 2);
        Children.Add(_month);

        // Nothing shows until the first lay: a view waiting for setup is never attached, and the card's Set up row stands alone.
        _readout.Visibility = Visibility.Collapsed;
        _rowStack.Visibility = Visibility.Collapsed;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    private void OnLanguage(Locale locale) => Lay();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MoonConfig ?? new MoonConfig();
        PublishPlace();

        // One cadence for good: the config cannot change it, so subscribe once.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Tick);

        // [S-6005] The style picks the parts: a full lay, not only a refresh.
        Lay();
    }

    /// <summary>[S-6583] The widget's alignment, from the properties panel: where the readout sits; the rows keep the width.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Lay();
    }

    /// <summary>[S-663] The card's word on how much room there is.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Tick()
    {
        _now = DateTimeOffset.Now;
        Refresh();
    }

    /// <summary>[S-3105] Place the parts for this size, then fill them in.</summary>
    private void Lay()
    {
        // [S-6005] One style's parts at a time: the readout and its rows, or the month.
        var month = _config.Style == MoonStyle.PhaseMonth;
        _readout.Visibility = Show(!month && _area.IsMeasured);
        _rowStack.Visibility = Show(!month && _area.IsMeasured);
        _month.Visibility = Show(month);

        if (month)
        {
            LayMonth();
            return;
        }

        if (_monthDiscs.Count > 0)
        {
            _monthDiscs.Clear();
            _monthGrid.Children.Clear();
        }

        if (!_area.IsMeasured)
        {
            return;
        }

        _plan = MoonLayout.Plan(_area, LocaleFormat.Percent(Strings.Current, 100));
        var face = _plan.Face;
        var side = face.Side;

        // The readout at the view's top in the inset (beside the rows, its column holds the inset on its left only).
        ColumnDefinitions[0].Width = side ? new GridLength(face.HeroColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SetColumnSpan(_readout, side ? 1 : 2);
        _readout.Margin = side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        _readout.HorizontalAlignment = Aligned.Horizontal(_align);
        _valueLine.HorizontalAlignment = Aligned.Horizontal(_align);
        _name.TextAlignment = Aligned.Text(_align);
        _name.HorizontalAlignment = Aligned.Horizontal(_align);

        _value.Use(face.Hero);
        _disc.Resize(face.Hero.FontSize);
        _disc.Root.Visibility = Show(_plan.Disc);
        Family.Step(_name, FamilyType.Under(face.Hero));
        _name.Visibility = Show(face.Under);

        // The rows: 8 under the readout, or beside it from the top. They carry their own side inset.
        SetRow(_rowStack, side ? 0 : 1);
        SetRowSpan(_rowStack, side ? 2 : 1);
        SetColumn(_rowStack, side ? 1 : 0);
        SetColumnSpan(_rowStack, side ? 1 : 2);
        _rowStack.Margin = new Thickness(0, side ? 0 : FamilySpace.S2, 0, 0);
        _rowStack.Visibility = Show(face.ListRows > 0);

        _rowRoom = SkyRows.Room(side ? _area.Width - face.HeroColumn : _area.Width);
        for (var i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i];
            row.Root.Visibility = Show(i < face.ListRows);
            row.Lines = face.ListLines;
            row.Room = _rowRoom;
        }

        Refresh();
    }

    /// <summary>
    /// [S-6050] [S-6061] The month's grid for this size: a disc a day reflowed to the content, the
    /// month's name over it only while the discs keep the family's icon size under it.
    /// </summary>
    private void LayMonth()
    {
        var width = Math.Max(0, _area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, _area.Height - FamilySpace.Inset);
        var days = DateTime.DaysInMonth(_now.LocalDateTime.Year, _now.LocalDateTime.Month);
        var under = DotGrid.Fit(days, width, height - FamilyType.Caption.LineHeight - FamilySpace.S2);
        var named = under.Diameter >= MonthNameDisc;
        _monthPlan = named ? under : DotGrid.Fit(days, width, height);
        _monthName.Visibility = Show(named);
        _monthDrawn = default;
        RefreshMonth();
    }

    /// <summary>[S-6051] The discs for the month holding now, rebuilt when the day changes.</summary>
    private void RefreshMonth()
    {
        var days = MoonMonth.Days(_now, TimeZoneInfo.Local);
        var today = days.FirstOrDefault(d => d.Today).Date;
        if (today == _monthDrawn && _monthDiscs.Count == days.Count)
        {
            return;
        }

        var plan = _monthPlan;

        // Not measured yet: nothing to place (a ring of no size is a negative width).
        if (plan.Columns == 0 || plan.Pitch < 2)
        {
            _monthGrid.Children.Clear();
            return;
        }

        _monthDrawn = today;
        var southern = _config.EffectiveLatitude < 0;
        var locale = Strings.Current;

        while (_monthDiscs.Count < days.Count)
        {
            _monthDiscs.Add(new SkyMoonDisc());
        }

        _monthDiscs.RemoveRange(days.Count, _monthDiscs.Count - days.Count);
        _monthGrid.Children.Clear();
        _monthGrid.Width = plan.Width;
        _monthGrid.Height = plan.Height;
        _monthName.Text = LocaleFormat.MonthName(locale, today.Month);

        var size = Math.Max(1, Math.Floor(plan.Diameter));
        var inset = (plan.Pitch - size) / 2;
        for (var i = 0; i < days.Count; i++)
        {
            var disc = _monthDiscs[i];
            var (column, row) = plan.Cell(i);
            disc.Resize(size);
            disc.Update(days[i].Phase, southern, up: true);
            Canvas.SetLeft(disc.Root, (column * plan.Pitch) + inset);
            Canvas.SetTop(disc.Root, (row * plan.Pitch) + inset);
            ToolTipService.SetToolTip(disc.Root, $"{LocaleFormat.Date(locale, days[i].Date, DateStyle.MonthDay)} · {MoonPhase.Label(days[i].Name)}");
            _monthGrid.Children.Add(disc.Root);

            if (days[i].Today)
            {
                // [S-6050] Today ringed in the accent at the Chart's stroke, in the gap round its disc.
                var ring = Math.Floor(plan.Pitch);
                _todayRing.Width = ring;
                _todayRing.Height = ring;
                Canvas.SetLeft(_todayRing, (column * plan.Pitch) + ((plan.Pitch - ring) / 2));
                Canvas.SetTop(_todayRing, (row * plan.Pitch) + ((plan.Pitch - ring) / 2));
                _monthGrid.Children.Add(_todayRing);
            }
        }
    }

    /// <summary>Recompute the readout. Called on a tick, a config change, a resize and a language change.</summary>
    private void Refresh()
    {
        if (_config.Style == MoonStyle.PhaseMonth)
        {
            RefreshMonth();
            return;
        }

        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;
        var southern = latitude < 0;
        var locale = Strings.Current;

        var moon = MoonPhase.At(_now);
        var up = SkyPath.PositionOf(MoonPhase.SublunarPoint(_now), latitude, longitude).Up;

        _disc.Update(moon.Phase, southern, up);
        _riseGlyph.Update(moon.Phase, southern, up);
        _setGlyph.Update(moon.Phase, southern, up);
        var lit = ReadoutRules.Split(LocaleFormat.Percent(locale, Math.Round(moon.Illumination * 100)));
        _value.Show(lit.Value, lit.Unit);
        _name.Text = MoonPhase.Label(moon.Name);

        var times = MoonPhase.NextRiseSet(_now, latitude, longitude);
        var next = times.Next;
        var countdown = next is { } evt ? SkyText.Countdown(evt.At - _now) : null;
        var riseNext = next is { IsRise: true };

        var riseLabel = (MoonStr.WidgetsC_Moon_Rise.Text(), MoonStr.WidgetsC_Moon_Short_Rise.Text());
        var setLabel = (MoonStr.WidgetsC_Moon_Set.Text(), MoonStr.WidgetsC_Moon_Short_Set.Text());
        var riseValue = SkyText.EventTime(locale, times.Rise, _now);
        var setValue = SkyText.EventTime(locale, times.Set, _now);

        // [S-3105] With one row, it is the next of the two; with both, rise then set, the next one
        // carrying its countdown.
        if (_plan.Face.ListRows == 1 && next is { IsRise: false })
        {
            _riseRow.SetLabel(setLabel.Item1, setLabel.Item2);
            _riseRow.Set(setValue, countdown);
        }
        else
        {
            _riseRow.SetLabel(riseLabel.Item1, riseLabel.Item2);
            _riseRow.Set(riseValue, riseNext ? countdown : null);
        }

        _setRow.SetLabel(setLabel.Item1, setLabel.Item2);
        _setRow.Set(setValue, next is { IsRise: false } ? countdown : null);

        // [S-1491] The next full or new moon, in the machine's zone like every other time here; its
        // weekday goes first where the row cannot hold it beside the phase's name ("Oct 26", [S-6735]).
        var major = MoonPhase.NextMajor(_now);
        var majorLabel = MoonPhase.Label(major.Name);
        var majorDate = SkyText.ShortDate(locale, major.At.ToLocalTime());
        if (!SkyRows.Fits(_rowRoom, majorLabel, majorDate))
        {
            majorDate = LocaleFormat.Date(locale, DateOnly.FromDateTime(major.At.ToLocalTime().DateTime), DateStyle.MonthDayShort);
        }

        _majorRow.SetLabel(majorLabel);
        _majorRow.SetPlain(majorDate, null);
        _majorGlyph.Update(major.Name == MoonPhaseName.FullMoon ? 0.5 : 0, southern, up: true);
    }

    /// <summary>[S-2260] The header names the widget ("Moon"); the place stands in its label, none once it is cleared ([S-2848]).</summary>
    private void PublishPlace() => _about.SetAbout(_config.PlaceLabel);

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
