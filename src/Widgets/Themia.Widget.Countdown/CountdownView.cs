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
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>
/// Time left until one date the user cares about ([S-6036]): one widget, one target. [C-01]
/// <para>
/// [S-11303] The face is the timers': the duration as the Readout on the family's one-readout step with
/// the target's date under it, sized on the widest reading of its format (<see cref="CountdownLayout.Plan"/>),
/// and where it is on the progress bar 8 under it; the event's name over it, in the widget's own content
/// ([S-6037], [S-12481]: no card has a header). It sits where the widget's alignment puts it ([S-6580],
/// centred by default).
/// </para>
/// <para>
/// [S-6030] The style is how the duration draws: the text, the Chart ring of the time left with the
/// text in its hole ([S-6031]), or split-flap cards ([S-6032], <see cref="FlipCard"/>, <see cref="FlipRules"/>)
/// with their units under them.
/// </para>
/// </summary>
internal sealed partial class CountdownView : Grid, IWidgetView, IWidgetAligned
{
    /// <summary>[S-12481] The event's name over the face: the header's look, one line, ending in its ellipsis.</summary>
    private readonly TextBlock _name = Family.Text(FamilyType.BodyStrong, ColourRole.TextSecondary);

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one (in the view's padding).</summary>
    private readonly ReadingFit _fit = new() { Margin = default };

    private readonly ReadoutBlock _readout = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ChartRing _ring = new();
    private readonly Grid _face = new();
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };

    /// <summary>[S-6032] The Flip style: one card per unit, its unit under it.</summary>
    private readonly StackPanel _flipRow = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, Spacing = FlipRules.Gap };
    private readonly List<(FlipCard Card, TextBlock Unit)> _flips = [];

    private CountdownPlan _plan;

    private Ticker? _ticker;
    private CountdownConfig _config = new();

    /// <summary>[S-2050] The clock the countdown reads: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;
    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private bool _detached;

    public CountdownView()
    {
        _face.Children.Add(_ring);
        _face.Children.Add(_readout);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _name.TextWrapping = TextWrapping.NoWrap;
        _name.Height = FamilySpace.HeaderLine;
        _name.Margin = new Thickness(0, 0, 0, FamilySpace.HeaderGap);
        // [S-12808] The name, the face, the flip cards and the bar: one block, planned at the natural box and scaled
        // up as one.
        _fit.AddGivesWay(_name, FamilyType.BodyStrong.FontSize);
        _fit.Parts.Add(_face);
        _fit.Parts.Add(_flipRow);
        _fit.Parts.Add(_bar);
        _fit.Span(_bar);

        // The content in the card's inset; the top is the card's.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        Children.Add(_fit);
    }

    /// <summary>[S-6037] [S-12481] The event's name over the face; an untitled countdown has no line for it.</summary>
    private void ShowName()
    {
        _name.Text = CountdownLayout.Named(_config) ? _config.Label.Trim() : string.Empty;
        _name.Visibility = _name.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

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

    /// <summary>[S-6583] The widget's alignment, from the selection's page.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _fit.Align = align;
        if (align == _align)
        {
            return;
        }

        _align = align;
        Rebuild();
    }

    /// <summary>The target, its format or the room changed: cadence, layout and letters.</summary>
    private void Rebuild()
    {
        // [S-12833] In a smaller card the date under the time (the cards' units) and the name give way before the time.
        _fit.Face = CountdownLayout.Face(_config, Strings.Current);
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
        _ = _tick.Set(_ticker, _config.Target is null ? TimeSpan.Zero : _config.TickInterval, OnTick);
    }

    /// <summary>[S-11303] The face in the countdown's style.</summary>
    private void Lay()
    {
        // [S-1862] No target: the card shows its Set up row over this, so draw nothing.
        if (_config.Target is not { } target)
        {
            _fit.Visibility = Visibility.Collapsed;
            return;
        }

        _fit.Visibility = Visibility.Visible;
        var now = _clock();
        var cards = CountdownLayout.Cards(target, now, _config.Format == CountdownFormat.DaysHoursMinutesSeconds);
        _plan = CountdownLayout.Plan(_config, Strings.Current);

        // [S-6583] The face where the widget's alignment puts it (the fit places the name, the face and the cards: [S-14301]);
        // the bar keeps the content's width.
        var ringed = _plan.Look == CountdownLook.Ring;
        _readout.HorizontalAlignment = ringed ? HorizontalAlignment.Center : Aligned.Horizontal(_align);
        _readout.Use(_plan.Step, centred: ringed || _align == ContentAlign.Center);
        _readout.MaxWidth = ringed ? ChartRules.Hole(_plan.Ring).Width : double.PositiveInfinity;

        _ring.Visibility = ringed ? Visibility.Visible : Visibility.Collapsed;
        _face.Width = ringed ? _plan.Ring : double.NaN;
        _face.Height = ringed ? _plan.Ring : double.NaN;
        _face.Visibility = _plan.Look == CountdownLook.Flip ? Visibility.Collapsed : Visibility.Visible;
        _flipRow.Visibility = _plan.Look == CountdownLook.Flip ? Visibility.Visible : Visibility.Collapsed;
        _bar.Visibility = _config.DrawsProgress ? Visibility.Visible : Visibility.Collapsed;

        if (_plan.Look == CountdownLook.Flip)
        {
            LayFlip(cards);
        }
        else if (_flips.Count > 0)
        {
            // [S-6005] What one style built is let go when another is chosen.
            Forget();
            _flips.Clear();
            _flipRow.Children.Clear();
        }
    }

    /// <summary>[S-6032] One card per unit, its unit under it on the cards' line under.</summary>
    private void LayFlip(IReadOnlyList<CountdownCard> cards)
    {
        if (_flips.Count != cards.Count)
        {
            Forget();
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
                _fit.GivesWay(unit, FamilyType.Under(_plan.Flip.Step).FontSize);
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
        }
    }

    /// <summary>[S-12833] The cards' units the fit hides where they give way: let go with their cards.</summary>
    private void Forget()
    {
        foreach (var (_, unit) in _flips)
        {
            _fit.Forget(unit);
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

    /// <summary>[S-6330] Re-letter the readout. Called on every tick, so it allocates nothing but strings.</summary>
    private void Refresh()
    {
        if (_config.Target is not { } target)
        {
            return;
        }

        var now = _clock();
        var zone = TimeZoneInfo.Local;
        var reading = CountdownFormatter.Read(target, now, _config.Format, Strings.Current);

        // [C-40] Gone by: the readout carries it in the critical role.
        var role = reading.Elapsed ? ColourRole.Critical : ColourRole.TextPrimary;
        _readout.Show(reading.Text, under: CountdownFormatter.TargetLabel(target, now, zone, Strings.Current), valueRole: role);

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
        ToolTipService.SetToolTip(_fit, CountdownFormatter.FullTargetLabel(target, zone, Strings.Current));

        // [S-11303] Without the time the target was set the bar is its track alone.
        if (_config.DrawsProgress)
        {
            _bar.Show(CountdownLayout.Progress(_config, now) ?? 0);
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
        _tick.Dispose();
    }
}
