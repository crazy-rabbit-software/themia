using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

namespace Themia.App.Widgets;

/// <summary>
/// [S-11120] The moon where the observer is, computed offline, on the family's blocks. Disc: a Readout of
/// how much of it is lit on 28, the moon drawn at its phase as the readout's lead (<see cref="SkyMoonDisc"/>,
/// mirrored for a southern observer and a role lower while below the horizon, [S-169]) and the phase's
/// name under it. Phase month ([S-6050]): a disc a day of the month on <see cref="MoonLayout"/>'s fixed
/// grid, today ringed in the accent, the month's name over it. The rise, set and next full and new moon
/// are the moon times' atom.
/// </summary>
internal sealed partial class MoonView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetAligned
{
    private readonly SkyMoonDisc _disc = new();
    private readonly ReadoutBlock _value = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _valueLine = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _readout = new() { VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };

    private MoonConfig _config = new();

    /// <summary>[S-2260] The header's trailing parts: the place as its label beside the widget's name.</summary>
    private readonly HeaderParts _about = new();

    public FrameworkElement? HeaderTrailing => _about;
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;

    /// <summary>[S-6583] Where the readout sits: the widget's alignment, at the start by default.</summary>
    private ContentAlign _align = ContentAlign.Left;

    /// <summary>[S-6050] The Phase month style: the month's name over a disc a day.</summary>
    private readonly StackPanel _month = new() { VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
    private readonly TextBlock _monthName = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly Canvas _monthGrid = new() { HorizontalAlignment = HorizontalAlignment.Left, FlowDirection = FlowDirection.LeftToRight };
    private readonly List<SkyMoonDisc> _monthDiscs = [];
    private readonly Ellipse _todayRing = new() { StrokeThickness = FamilyShape.Stroke };

    /// <summary>The day the month was last drawn for: a new day redraws it, a tick on the same day does not.</summary>
    private DateOnly _monthDrawn;

    public MoonView()
    {
        _disc.Resize(FamilyType.Big28.FontSize);
        _disc.Root.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _disc.Root.VerticalAlignment = VerticalAlignment.Center;
        _value.Use(FamilyType.Big28);
        Family.Step(_name, FamilyType.Under(FamilyType.Big28));
        _valueLine.Children.Add(_disc.Root);
        _valueLine.Children.Add(_value);
        _readout.Children.Add(_valueLine);
        _readout.Children.Add(_name);
        _readout.Margin = Family.SideInset;

        _todayRing.Stroke = Family.Brush(ColourRole.Accent);
        _todayRing.Width = MoonLayout.Pitch;
        _todayRing.Height = MoonLayout.Pitch;
        _month.Spacing = FamilySpace.S2;
        _month.Margin = Family.SideInset;
        _month.Children.Add(_monthName);
        _month.Children.Add(_monthGrid);
        _monthGrid.Width = MoonLayout.Columns * MoonLayout.Pitch;
        _monthGrid.Height = MoonLayout.Rows * MoonLayout.Pitch;

        Children.Add(_readout);
        Children.Add(_month);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    private void OnLanguage(Locale locale)
    {
        _monthDrawn = default;
        Refresh();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MoonConfig ?? new MoonConfig();
        PublishPlace();

        // One cadence for good: the config cannot change it, so subscribe once.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Tick);
        var month = _config.Style == MoonStyle.PhaseMonth;
        _readout.Visibility = Show(!month);
        _month.Visibility = Show(month);
        _monthDrawn = default;
        Refresh();
    }

    /// <summary>[S-6583] The widget's alignment, from the properties panel: where the readout sits.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        _readout.HorizontalAlignment = Aligned.Horizontal(_align);
        _name.TextAlignment = Aligned.Text(_align);
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

    /// <summary>[S-6051] The discs for the month holding now, rebuilt when the day changes.</summary>
    private void RefreshMonth()
    {
        var days = MoonMonth.Days(_now, TimeZoneInfo.Local);
        var today = days.FirstOrDefault(d => d.Today).Date;
        if (today == _monthDrawn && _monthDiscs.Count == days.Count)
        {
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
        _monthName.Text = LocaleFormat.MonthName(locale, today.Month);

        var inset = (MoonLayout.Pitch - MoonLayout.Disc) / 2;
        for (var i = 0; i < days.Count; i++)
        {
            var disc = _monthDiscs[i];
            var (column, row) = (i % MoonLayout.Columns, i / MoonLayout.Columns);
            disc.Resize(MoonLayout.Disc);
            disc.Update(days[i].Phase, southern, up: true);
            Canvas.SetLeft(disc.Root, (column * MoonLayout.Pitch) + inset);
            Canvas.SetTop(disc.Root, (row * MoonLayout.Pitch) + inset);
            ToolTipService.SetToolTip(disc.Root, $"{LocaleFormat.Date(locale, days[i].Date, DateStyle.MonthDay)} · {MoonPhase.Label(days[i].Name)}");
            _monthGrid.Children.Add(disc.Root);

            if (days[i].Today)
            {
                // [S-6050] Today ringed in the accent at the Chart's stroke, in the gap round its disc.
                Canvas.SetLeft(_todayRing, column * MoonLayout.Pitch);
                Canvas.SetTop(_todayRing, row * MoonLayout.Pitch);
                _monthGrid.Children.Add(_todayRing);
            }
        }
    }

    /// <summary>Recompute the readout. Called on a tick, a config change and a language change.</summary>
    private void Refresh()
    {
        if (_config.Style == MoonStyle.PhaseMonth)
        {
            RefreshMonth();
            return;
        }

        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;
        var moon = MoonPhase.At(_now);
        var up = SkyPath.PositionOf(MoonPhase.SublunarPoint(_now), latitude, longitude).Up;
        _disc.Update(moon.Phase, latitude < 0, up);

        var lit = ReadoutRules.Split(LocaleFormat.Percent(Strings.Current, Math.Round(moon.Illumination * 100)));
        _value.Show(lit.Value, lit.Unit);
        _name.Text = MoonPhase.Label(moon.Name);
    }

    /// <summary>[S-2260] The header names the widget ("Moon"); the place stands in its label, none once it is cleared ([S-2848]).</summary>
    private void PublishPlace() => _about.SetAbout(_config.PlaceLabel);

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
