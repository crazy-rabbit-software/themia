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
internal sealed partial class MoonView : Grid, IWidgetView, IWidgetAligned
{
    private readonly SkyMoonDisc _disc = new();
    private readonly ReadoutBlock _value = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _valueLine = new() { Orientation = Orientation.Horizontal };
    private readonly SpreadStack _readout = new() { Visibility = Visibility.Collapsed };

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one.</summary>
    private readonly ReadingFit _fit = new();


    private MoonConfig _config = new();

    private Ticker? _ticker;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;

    /// <summary>[S-6050] The Phase month style: the month's name over a disc a day.</summary>
    private readonly SpreadStack _month = new() { Visibility = Visibility.Collapsed };
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
        // [S-12808] The readout (or the month) is one block, planned at the natural box and scaled up as one
        // ([S-12483]: the same joined or alone).

        _todayRing.Stroke = Family.Brush(ColourRole.Accent);
        _todayRing.Width = MoonLayout.Pitch;
        _todayRing.Height = MoonLayout.Pitch;
        _month.Spacing = FamilySpace.S2;
        _month.Children.Add(_monthName);
        _month.Children.Add(_monthGrid);
        _monthGrid.Width = MoonLayout.Grid.Width;
        _monthGrid.Height = MoonLayout.Grid.Height;

        _fit.Parts.Add(_readout);
        // [S-14324] The month stands its own width where the alignment puts it (centred by default), never spread over the
        // content's width with its days at the start (WR-3, owner 2026-11-20: "Centred is fine").
        _fit.Parts.Add(_month);

        // [S-12833] The phase's name and the month's give way where the card would draw them under the floor.
        _fit.GivesWay(_name, FamilyType.Under(FamilyType.Big28).FontSize);
        _fit.GivesWay(_monthName, FamilyType.Caption.FontSize);
        Children.Add(_fit);
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
        _fit.Face = MoonLayout.Face(_config);

        // One cadence for good: the config cannot change it, so subscribe once.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Tick);
        var month = _config.Style == MoonStyle.PhaseMonth;
        _readout.Visibility = Show(!month);
        _month.Visibility = Show(month);
        _monthDrawn = default;
        Align();
        Refresh();
    }

    /// <summary>[S-6583] The widget's alignment, from the selection's page: where the readout sits.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        Align();
    }

    /// <summary>
    /// [S-6583] [S-14324] The block where the card's alignment puts it while the style aligns (the disc's readout); the month,
    /// which takes no alignment (<c>AlignApplies</c>), centred where the height stops it (owner, 2026-11-20: "Centred is fine").
    /// </summary>
    private void Align()
    {
        var align = ((IContentAligned)_config).AlignApplies ? _align : ContentAlign.Center;
        _fit.Align = align;
        _name.TextAlignment = Aligned.Text(align);
    }

    private ContentAlign _align = ContentAlign.Center;

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

        // [S-15673] The grid's box is its ink: the discs from its edges, today's ring where it stands on one.
        var box = MoonLayout.GridOf(days.Count, days.ToList().FindIndex(d => d.Today));
        _monthGrid.Width = box.Width;
        _monthGrid.Height = box.Height;
        for (var i = 0; i < days.Count; i++)
        {
            var disc = _monthDiscs[i];
            var (column, row) = (i % MoonLayout.Columns, i / MoonLayout.Columns);
            disc.Resize(MoonLayout.Disc);
            disc.Update(days[i].Phase, southern, up: true);
            Canvas.SetLeft(disc.Root, box.Left + (column * MoonLayout.Pitch));
            Canvas.SetTop(disc.Root, box.Top + (row * MoonLayout.Pitch));
            ToolTipService.SetToolTip(disc.Root, $"{LocaleFormat.Date(locale, days[i].Date, DateStyle.MonthDay)} · {MoonPhase.Label(days[i].Name)}");
            _monthGrid.Children.Add(disc.Root);

            if (days[i].Today)
            {
                // [S-6050] Today ringed in the accent at the Chart's stroke, in the gap round its disc.
                Canvas.SetLeft(_todayRing, box.Left + (column * MoonLayout.Pitch) - MoonLayout.Ring);
                Canvas.SetTop(_todayRing, box.Top + (row * MoonLayout.Pitch) - MoonLayout.Ring);
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

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
