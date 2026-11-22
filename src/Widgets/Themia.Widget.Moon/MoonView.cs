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
internal sealed partial class MoonView : Grid, IWidgetView, IWidgetAligned, IWidgetSizeAware
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

    /// <summary>
    /// [S-6050] [S-15805] The Phase month style: the month's name in the inset over its day cells, which are the card's grid
    /// under it, its sides to its sides and its foot (<see cref="MoonLayout.Cells"/>).
    /// </summary>
    private readonly Grid _month = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _monthName = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly Grid _monthGrid = new() { FlowDirection = FlowDirection.LeftToRight };
    private readonly List<SkyMoonDisc> _monthDiscs = [];
    private readonly List<Border> _monthCells = [];
    private readonly Ellipse _todayRing = new() { StrokeThickness = FamilyShape.Stroke, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };

    /// <summary>The cell's panel today's ring stands in.</summary>
    private Panel? _ringHolder;

    /// <summary>[S-15805] The view's room, the month's cells laid in what its name leaves of it.</summary>
    private WidgetArea _area;

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

        // [S-15805] The name in the inset at the card's top (the card gives it); the cells from under it to the card's sides
        // and foot, seven columns of one width.
        _month.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _month.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _monthName.TextTrimming = TextTrimming.CharacterEllipsis;
        var name = new Border { Child = _monthName, Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0) };
        SetRow(_monthGrid, 1);
        for (var column = 0; column < MoonLayout.Columns; column++)
        {
            _monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _month.Children.Add(name);
        _month.Children.Add(_monthGrid);

        _fit.Parts.Add(_readout);

        // [S-12833] The phase's name gives way where the card would draw it under the floor.
        _fit.GivesWay(_name, FamilyType.Under(FamilyType.Big28).FontSize);
        Children.Add(_fit);
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
        var month = _config.Style == MoonStyle.PhaseMonth;
        _fit.Face = MoonLayout.Face(_config);

        // One cadence for good: the config cannot change it, so subscribe once.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Tick);
        _fit.Visibility = Show(!month);
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

    /// <summary>[S-15805] The card's word on its room: the month's cells share it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        SizeMonth();
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
            var disc = new SkyMoonDisc();
            disc.Root.HorizontalAlignment = HorizontalAlignment.Center;
            disc.Root.VerticalAlignment = VerticalAlignment.Center;
            _monthDiscs.Add(disc);

            // [S-15805] A day's cell is one of the month's items: it tiles the card with the others (WR-2, WR-3).
            var cell = new Border { Background = Family.Brush(ColourRole.Clear), Child = new Grid { Children = { disc.Root } } };
            ItemMark.Mark(cell);
            _monthCells.Add(cell);
        }

        _monthDiscs.RemoveRange(days.Count, _monthDiscs.Count - days.Count);
        _monthCells.RemoveRange(days.Count, _monthCells.Count - days.Count);
        _monthGrid.Children.Clear();
        // The ring leaves the cell it ringed (its Parent is not set for a child added in code: kept here).
        _ringHolder?.Children.Remove(_todayRing);
        _ringHolder = null;

        _monthName.Text = LocaleFormat.MonthName(locale, today.Month);
        Ink.InkStart(_monthName);

        var rows = MoonLayout.Cells(1, 1, days.Count).Rows;
        while (_monthGrid.RowDefinitions.Count < rows)
        {
            _monthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        while (_monthGrid.RowDefinitions.Count > rows)
        {
            _monthGrid.RowDefinitions.RemoveAt(_monthGrid.RowDefinitions.Count - 1);
        }

        for (var i = 0; i < days.Count; i++)
        {
            var cell = _monthCells[i];
            SetColumn(cell, i % MoonLayout.Columns);
            SetRow(cell, i / MoonLayout.Columns);
            _monthDiscs[i].Update(days[i].Phase, southern, up: true);
            ToolTipService.SetToolTip(cell, $"{LocaleFormat.Date(locale, days[i].Date, DateStyle.MonthDay)} · {MoonPhase.Label(days[i].Name)}");
            _monthGrid.Children.Add(cell);

            if (days[i].Today && cell.Child is Grid inside)
            {
                // [S-6050] Today ringed in the accent at the Chart's stroke, in the gap round its disc.
                inside.Children.Add(_todayRing);
                _ringHolder = inside;
            }
        }

        SizeMonth();
    }

    /// <summary>[S-15805] The discs and today's ring at the size the month's cells give them (<see cref="MoonLayout.Cells"/>).</summary>
    private void SizeMonth()
    {
        if (_config.Style != MoonStyle.PhaseMonth || !_area.IsMeasured || _monthDiscs.Count == 0)
        {
            return;
        }

        var height = _area.Height - FamilyType.Caption.LineHeight;
        var cells = MoonLayout.Cells(_area.Width, height, _monthDiscs.Count);
        foreach (var disc in _monthDiscs)
        {
            disc.Resize(cells.Disc);
        }

        _todayRing.Width = cells.Ring;
        _todayRing.Height = cells.Ring;
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
