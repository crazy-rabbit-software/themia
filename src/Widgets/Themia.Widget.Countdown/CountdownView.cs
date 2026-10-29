using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Countdown;

namespace Themia.App.Widgets;

/// <summary>
/// Time left until one date the user cares about ([S-6036]): one widget, one target. [C-01]
/// <para>
/// [S-1447] [S-6739] The face is the timers': the duration as the Readout on the Hero steps with the
/// target's date under it, sized on the reading's shape (<see cref="CountdownLayout.Plan"/>), and on L
/// and XL the progress bar 8 under it; the name is the card's header ([S-6037]). It sits where the
/// widget's alignment puts it ([S-6580], centred by default).
/// </para>
/// <para>
/// [S-6030] The style is how the duration draws: the text, the Chart ring of the time left with the
/// text in its hole ([S-6031]), or split-flap cards ([S-6032], <see cref="FlipCard"/>, <see cref="FlipRules"/>)
/// with their units under them; a tile too small for its ring or its cards draws the text.
/// </para>
/// </summary>
internal sealed partial class CountdownView : Grid, IWidgetView, IWidgetSizeAware, IWidgetAligned, IWidgetSurfaceAware
{
    private readonly ReadoutBlock _readout = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ChartRing _ring = new();
    private readonly Grid _face = new();
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };

    /// <summary>[S-6032] The Flip style: one card per unit, its unit under it.</summary>
    private readonly StackPanel _flipRow = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, Spacing = FlipRules.Gap };
    private readonly List<(FlipCard Card, TextBlock Unit)> _flips = [];
    private readonly StackPanel _column = new() { VerticalAlignment = VerticalAlignment.Center };

    private CountdownPlan _plan;

    /// <summary>The reading's shape the face was laid out for ([S-1447]): a new one lays it out again.</summary>
    private string _shape = string.Empty;

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private CountdownConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WidgetArea _area;
    private bool _detached;

    public CountdownView()
    {
        _face.Children.Add(_ring);
        _face.Children.Add(_readout);
        _column.Children.Add(_face);
        _column.Children.Add(_flipRow);
        _column.Children.Add(_bar);

        // The content in the card's inset, under the header.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        Children.Add(_column);
    }

    /// <summary>[S-6037] The name is the card's header; an untitled countdown keeps the widget's own.</summary>
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        ShowName();
    }

    private void ShowName() => _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(_config.Label) ? null : _config.Label);

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] The reading and the date are worded again in the new language.</summary>
    private void OnLanguage(Locale locale) => Rebuild();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as CountdownConfig ?? new CountdownConfig();
        ShowName();
        Rebuild();
    }

    /// <summary>[S-6583] Where the time left, the date, the ring, the cards and the bar sit: the widget's alignment, centred by default ([S-6582]).</summary>
    private ContentAlign _align = ContentAlign.Center;

    /// <summary>[S-6583] The widget's alignment, from the properties panel.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Rebuild();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Rebuild();
    }

    /// <summary>The target, its format or the room changed: cadence, layout and letters.</summary>
    private void Rebuild()
    {
        if (_detached)
        {
            return;
        }

        Resubscribe();
        Lay();
        Refresh();
    }

    /// <summary>
    /// Only when the cadence actually changed. Dropping and remaking the subscription on every
    /// config touch makes dragging a settings slider stutter. [C-20]
    /// </summary>
    private void Resubscribe()
    {
        // [S-142] Nothing to re-letter means nothing to wake for.
        if (_config.Target is null)
        {
            _subscription?.Dispose();
            _subscription = null;
            _subscribedInterval = TimeSpan.Zero;
            return;
        }

        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
    }

    /// <summary>[S-1447] [S-6030] The face for the tile, the style and the reading's shape.</summary>
    private void Lay()
    {
        // [S-1862] No target: the card shows its Set up row over this, so draw nothing.
        if (_config.Target is not { } target)
        {
            _column.Visibility = Visibility.Collapsed;
            return;
        }

        _column.Visibility = Visibility.Visible;
        if (!_area.IsMeasured)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var reading = CountdownFormatter.Read(target, now, _config.Format, Strings.Current).Text;
        var cards = CountdownLayout.Cards(target, now, _config.Format == CountdownFormat.DaysHoursMinutesSeconds);
        _shape = CountdownLayout.Shape(reading);
        _plan = CountdownLayout.Plan(_config.Style, _area, reading, cards, CountdownLayout.Progress(_config, now) is not null);

        // [S-6583] The face where the widget's alignment puts it; the bar keeps the content's width.
        var ringed = _plan.Look == CountdownLook.Ring;
        var place = Aligned.Horizontal(_align);
        _face.HorizontalAlignment = place;
        _flipRow.HorizontalAlignment = place;
        _readout.HorizontalAlignment = ringed ? HorizontalAlignment.Center : place;
        _readout.Use(_plan.Step, centred: ringed || _align == ContentAlign.Center);
        _readout.MaxWidth = ringed ? ChartRules.Hole(_plan.Ring).Width : double.PositiveInfinity;

        _ring.Visibility = ringed ? Visibility.Visible : Visibility.Collapsed;
        _face.Width = ringed ? _plan.Ring : double.NaN;
        _face.Height = ringed ? _plan.Ring : double.NaN;
        _face.Visibility = _plan.Look == CountdownLook.Flip ? Visibility.Collapsed : Visibility.Visible;
        _flipRow.Visibility = _plan.Look == CountdownLook.Flip ? Visibility.Visible : Visibility.Collapsed;
        _bar.Visibility = _plan.Bar ? Visibility.Visible : Visibility.Collapsed;

        if (_plan.Look == CountdownLook.Flip)
        {
            LayFlip(cards);
        }
        else if (_flips.Count > 0)
        {
            // [S-6005] What one style built is let go when another is chosen.
            _flips.Clear();
            _flipRow.Children.Clear();
        }
    }

    /// <summary>[S-6032] One card per unit, its unit under it on the cards' line under where it fits.</summary>
    private void LayFlip(IReadOnlyList<CountdownCard> cards)
    {
        if (_flips.Count != cards.Count)
        {
            _flips.Clear();
            _flipRow.Children.Clear();
            for (var i = 0; i < cards.Count; i++)
            {
                var card = new FlipCard();
                var unit = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
                unit.TextAlignment = TextAlignment.Center;
                unit.HorizontalAlignment = HorizontalAlignment.Center;
                unit.TextTrimming = TextTrimming.CharacterEllipsis;
                _flips.Add((card, unit));
                _flipRow.Children.Add(new StackPanel { Children = { card.Root, unit } });
            }
        }

        var under = FamilyType.Under(_plan.Flip.Step);
        for (var i = 0; i < _flips.Count; i++)
        {
            _flips[i].Card.Lay(_plan.Flip);
            _flips[i].Card.Redraw();
            Family.Step(_flips[i].Unit, under);
            _flips[i].Unit.Text = UnitText(cards[i].Unit);
            _flips[i].Unit.MaxWidth = _plan.Flip.CardWidth;
            _flips[i].Unit.Visibility = _plan.Under ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string UnitText(CountdownUnit unit) => (unit switch
    {
        CountdownUnit.Days => CountdownStr.Styles_Countdown_UnitDays,
        CountdownUnit.Hours => CountdownStr.Styles_Countdown_UnitHours,
        CountdownUnit.Minutes => CountdownStr.Styles_Countdown_UnitMinutes,
        _ => CountdownStr.Styles_Countdown_UnitSeconds,
    }).Text();

    private void OnTick()
    {
        if (!_detached)
        {
            Refresh();
        }
    }

    /// <summary>[S-6330] Re-letter the readout. Called on every tick, so it allocates nothing but strings; a new shape lays the face out again.</summary>
    private void Refresh()
    {
        if (_config.Target is not { } target)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var zone = TimeZoneInfo.Local;
        var reading = CountdownFormatter.Read(target, now, _config.Format, Strings.Current);
        if (_area.IsMeasured && CountdownLayout.Shape(reading.Text) != _shape)
        {
            Lay();
        }

        // [C-40] Gone by: the readout carries it in the critical role.
        var role = reading.Elapsed ? ColourRole.Critical : ColourRole.TextPrimary;
        _readout.Show(reading.Text, under: _plan.Under ? CountdownFormatter.TargetLabel(target, now, zone, Strings.Current) : null, valueRole: role);

        if (_plan.Look == CountdownLook.Ring)
        {
            // [S-1448] Without the time the target was set the ring is its track alone.
            _ring.Show(CountdownLayout.Left(_config, now) ?? double.NaN);
        }

        if (_plan.Look == CountdownLook.Flip)
        {
            var cards = CountdownLayout.Cards(target, now, _config.Format == CountdownFormat.DaysHoursMinutesSeconds);

            // The days card comes and goes at the last day: a new row of cards, not new figures.
            if (cards.Count != _flips.Count)
            {
                Lay();
            }

            for (var i = 0; i < _flips.Count && i < cards.Count; i++)
            {
                _flips[i].Card.Role = role;
                _flips[i].Card.Show(cards[i].Digits, IsLoaded);
            }
        }

        // [C-05] The whole instant, for a date line that is necessarily abbreviated (or not shown).
        ToolTipService.SetToolTip(_column, CountdownFormatter.FullTargetLabel(target, zone, Strings.Current));

        if (_plan.Bar && CountdownLayout.Progress(_config, now) is { } progress)
        {
            _bar.Show(progress);
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }
}
