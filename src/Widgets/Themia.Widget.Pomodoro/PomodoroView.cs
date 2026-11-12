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
using Themia.Widget.Pomodoro;

namespace Themia.App.Widgets;

/// <summary>
/// A focus and break timer.
/// <para>
/// The run state — phase, seconds left, whether it is going — lives in this
/// instance and nowhere else. It is deliberately not persisted and not shared ([S-150]): the same
/// widget shown on two monitors is two independent timers, which is also why only the instance that
/// is actually running can sound the cue, so a two-monitor desktop cannot echo it.
/// </para>
/// <para>
/// [S-1466] [S-6035] The face is the timers' ([S-6739], <see cref="PomodoroFace"/>): the time left as
/// the Readout on the Hero steps with the phase under it, centred in its box, in the Chart ring of the
/// phase's progress (Ring), over the bar (Bar) or alone (Digits); Start/Pause, Reset and Skip in the
/// Controls row at the bottom inset, centred under it, where the tile has room for them.
/// </para>
/// </summary>
internal sealed partial class PomodoroView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly ReadoutBlock _readout = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ChartRing _ring = new();
    private readonly Grid _face = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ChartBar _bar = new() { Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private readonly StackPanel _column = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ControlsBlock _controls = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private readonly Button _runButton;
    private readonly Button _resetButton;
    private readonly Button _skipButton;
    private readonly Button _shorterButton;
    private readonly Button _longerButton;

    // [S-11304] At Small the row is icon buttons: Start/Pause the accent's Play or Pause.
    private readonly ControlsBlock _iconControls = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, FamilySpace.S2, 0, 0) };
    private readonly Button _runIcon;
    private readonly Button _shorterIcon;
    private readonly Button _longerIcon;
    private readonly Button _resetIcon;
    private readonly Button _skipIcon;

    private Ticker? _ticker;
    private PomodoroConfig _config = new();
    private PomodoroState _state;

    /// <summary>[S-8111] How long the current phase runs in all: the menu's length, or a nudged one until the phase ends.</summary>
    private int _length;
    private IDisposable? _subscription;
    private PomodoroPlan _plan;
    private bool _preview;

    /// <summary>
    /// Swallows the callback <see cref="Ticker.Subscribe"/> makes straight away ([S-155]): this
    /// subscriber does not render a tick, it <i>is</i> the tick, so the subscribe-time call would take
    /// a second off the clock the instant the user pressed start.
    /// </summary>
    private bool _skipFirstTick;

    private bool _running;

    public PomodoroView()
    {
        Load(PomodoroCycle.Fresh(_config));

        // [S-8111] Start first, then − and + beside it, then Reset and Skip: the icons give way from the
        // last, so the nudges stay longest (the keys still reach Reset and Skip).
        _runButton = _controls.Main(PomodoroStr.WidgetsC_Pomodoro_Start.Text(), () => SetRunning(!_running));
        _shorterButton = _controls.Icon(Glyphs.Minus, PomodoroStr.Pomodoro_Shorter.Text(), () => Nudge(-1));
        _longerButton = _controls.Icon(Glyphs.Add, PomodoroStr.Pomodoro_Longer.Text(), () => Nudge(+1));
        _resetButton = _controls.Icon(Glyphs.Reset, PomodoroStr.Pomodoro_Reset.Text(), Reset);
        _skipButton = _controls.Icon(Glyphs.Next, PomodoroStr.WidgetsC_Pomodoro_Skip.Text(), Skip);
        _runIcon = _iconControls.MainIcon(Glyphs.Play, PomodoroStr.WidgetsC_Pomodoro_Start.Text(), () => SetRunning(!_running));
        _shorterIcon = _iconControls.Icon(Glyphs.Minus, PomodoroStr.Pomodoro_Shorter.Text(), () => Nudge(-1));
        _longerIcon = _iconControls.Icon(Glyphs.Add, PomodoroStr.Pomodoro_Longer.Text(), () => Nudge(+1));
        _resetIcon = _iconControls.Icon(Glyphs.Reset, PomodoroStr.Pomodoro_Reset.Text(), Reset);
        _skipIcon = _iconControls.Icon(Glyphs.Next, PomodoroStr.WidgetsC_Pomodoro_Skip.Text(), Skip);

        _face.Children.Add(_ring);
        _face.Children.Add(_readout);
        _column.Children.Add(_face);
        _column.Children.Add(_bar);

        // The content in the card's inset: the readout's box over the Controls row.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_controls, 1);
        SetRow(_iconControls, 1);
        Children.Add(_column);
        Children.Add(_controls);
        Children.Add(_iconControls);

        Render();
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _preview = surface.IsPreview;

        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] The fixed tooltips again; <see cref="Render"/> re-reads the rest.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_shorterButton, PomodoroStr.Pomodoro_Shorter.Text());
        ToolTipService.SetToolTip(_longerButton, PomodoroStr.Pomodoro_Longer.Text());
        ToolTipService.SetToolTip(_resetButton, PomodoroStr.Pomodoro_Reset.Text());
        ToolTipService.SetToolTip(_skipButton, PomodoroStr.WidgetsC_Pomodoro_Skip.Text());
        ToolTipService.SetToolTip(_shorterIcon, PomodoroStr.Pomodoro_Shorter.Text());
        ToolTipService.SetToolTip(_longerIcon, PomodoroStr.Pomodoro_Longer.Text());
        ToolTipService.SetToolTip(_resetIcon, PomodoroStr.Pomodoro_Reset.Text());
        ToolTipService.SetToolTip(_skipIcon, PomodoroStr.WidgetsC_Pomodoro_Skip.Text());
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as PomodoroConfig ?? new PomodoroConfig();

        // While the timer is stopped the clock follows the configured length, so changing it in
        // settings moves the digits and the bar together. While it is running the countdown owns
        // the remaining seconds and reloads them itself at the next flip.
        if (!_running)
        {
            Load(new PomodoroState(_state.Phase, PomodoroCycle.PhaseSeconds(_state.Phase, _config)));
        }

        Lay();
        Render();
    }

    /// <summary>[S-11304] The face is the config's, not the box's: a wider card centres the same face.</summary>
    public void ApplySize(WidgetArea area)
    {
    }

    /// <summary>
    /// [S-155] Start and pause. The subscription exists only while the timer runs: a paused pomodoro
    /// has nothing to do once a second, and the shared ticker stops once nothing is subscribed.
    /// </summary>
    private void SetRunning(bool running)
    {
        if (_running == running)
        {
            return;
        }

        _running = running;

        if (running)
        {
            _skipFirstTick = true;
            _subscription = _ticker?.Subscribe(TimeSpan.FromSeconds(1), Tick);
        }
        else
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        Render();
    }

    /// <summary>[S-1987] Space or Enter start and pause, Right skips, Home resets, + / Up and − / Down nudge ([S-8111]) (<see cref="ControlKeys.Pomodoro"/>).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_ticker is null)
        {
            return false;
        }

        var nudge = PomodoroKeys.Nudge(key);
        if (nudge != 0)
        {
            Nudge(nudge);
            return true;
        }

        switch (PomodoroKeys.Of(key))
        {
            case ControlKey.Toggle:
                SetRunning(!_running);
                return true;

            case ControlKey.Next:
                Skip();
                return true;

            case ControlKey.Reset:
                Reset();
                return true;

            default:
                return false;
        }
    }

    /// <summary>[S-02] Reset stops and returns to a fresh work phase. Nothing sounds.</summary>
    private void Reset()
    {
        SetRunning(false);
        Load(PomodoroCycle.Fresh(_config));
        Render();
    }

    /// <summary>[S-1466] Skip to the next phase, running or not. A skip is the user's own flip: silent.</summary>
    private void Skip()
    {
        Load(PomodoroCycle.Skip(_state, _config));
        Render();
    }

    /// <summary>[S-8111] A phase as the menu sets it: its length the configured one, a nudge forgotten.</summary>
    private void Load(PomodoroState state)
    {
        _state = state;
        _length = PomodoroCycle.PhaseSeconds(state.Phase, _config);
    }

    /// <summary>[S-8111] The current phase 5 minutes shorter (−1) or longer (+1), running or not; nothing at a bound.</summary>
    private void Nudge(int direction)
    {
        if (PomodoroNudge.Apply(_state, _length, direction) is { } nudged)
        {
            _state = nudged.State;
            _length = nudged.LengthSeconds;
            Render();
        }
    }

    private void Tick()
    {
        if (_skipFirstTick)
        {
            _skipFirstTick = false;
            return;
        }

        if (!_running)
        {
            return;
        }

        var tick = PomodoroCycle.Advance(
            _state,
            PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, _config),
            PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, _config));

        if (tick.Flipped)
        {
            Load(new PomodoroState(tick.Phase, tick.RemainingSeconds));
        }
        else
        {
            _state = new PomodoroState(tick.Phase, tick.RemainingSeconds);
        }

        // [S-152] The flip is the only thing that sounds, and it announces the phase being ENTERED.
        // Never from a preview card: it makes no sound ([S-1251]).
        if (tick.Flipped && !_preview)
        {
            SoundCue.Play(PomodoroSounds.Wave(_config.Sound, tick.Phase, _config.EffectiveVolume));
        }

        Render();
    }

    /// <summary>[S-6330] The time, the phase and the progress as they are now: a tick sets what changed and nothing else.</summary>
    private void Render()
    {
        var fraction = PomodoroCycle.Progress(_state, _length);

        // [S-6706] The phase is said by its word; neither it nor the break is coloured (no accent text, no success green).
        _readout.Show(PomodoroCycle.Clock(_state.RemainingSeconds), under: PomodoroCycle.PhaseLabel(_state.Phase));
        ToolTipService.SetToolTip(_readout, PomodoroCycle.NextLine(_state.Phase, _config));

        if (_plan.Look == PomodoroLook.Ring)
        {
            _ring.Show(fraction);
        }
        else if (_plan.Look == PomodoroLook.Bar)
        {
            _bar.Show(fraction);
        }

        _shorterButton.IsEnabled = PomodoroNudge.Can(_state, _length, -1);
        _longerButton.IsEnabled = PomodoroNudge.Can(_state, _length, +1);
        _shorterIcon.IsEnabled = _shorterButton.IsEnabled;
        _longerIcon.IsEnabled = _longerButton.IsEnabled;
        var run = (_running ? PomodoroStr.WidgetsC_Pomodoro_Pause : PomodoroStr.WidgetsC_Pomodoro_Start).Text();
        ControlsBlock.Relabel(_runButton, run);
        ControlsBlock.Reglyph(_runIcon, _running ? Glyphs.Pause : Glyphs.Play, run);
    }

    /// <summary>[S-11304] The face at the pomodoro's Size and style: the look, the readout's step, the ring, the bar, the controls worded or as icons.</summary>
    private void Lay()
    {
        _plan = PomodoroFace.Plan(_config);
        _readout.Use(_plan.Step, centred: true);

        var ringed = _plan.Look == PomodoroLook.Ring;
        _ring.Visibility = ringed ? Visibility.Visible : Visibility.Collapsed;
        _face.Width = ringed ? _plan.Ring : double.NaN;
        _face.Height = ringed ? _plan.Ring : double.NaN;
        _bar.Visibility = _plan.Look == PomodoroLook.Bar ? Visibility.Visible : Visibility.Collapsed;
        var icons = TimerFace.IconControls(_config.Size);
        _controls.Visibility = icons ? Visibility.Collapsed : Visibility.Visible;
        _iconControls.Visibility = icons ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _running = false;

        // A key the lease delivers after the card left ([S-802]) must not start a detached timer.
        _ticker = null;
    }
}
