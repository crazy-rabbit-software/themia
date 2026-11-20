using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// A focus and break timer that runs through its phases ([S-8112]).
/// <para>
/// The run state — which phase, seconds left, whether it is going — lives in this instance and nowhere
/// else. It is deliberately not persisted and not shared ([S-150]): the same widget shown on two monitors
/// is two independent timers, which is also why only the instance that is actually running can sound
/// the cue, so a two-monitor desktop cannot echo it.
/// </para>
/// <para>
/// [S-8117] The face is one block (<see cref="PomodoroBlock"/>) scaled as one to the card ([S-12808]): the
/// time left as the Readout (in the sectioned ring on the Ring style), the caption (the phase and its place
/// in the cycle), the sectioned bar on the Bar style and the Controls row; stacked in a tall card, side by
/// side in a wide one, by the card's aspect (<see cref="PomodoroFace.Choose"/>).
/// </para>
/// </summary>
internal sealed partial class PomodoroView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys
{
    /// <summary>[S-12808] The block, planned at its natural size and scaled as one (in the view's padding).</summary>
    private readonly ReadingFit _fit = new() { Margin = default };

    private readonly ReadoutBlock _time = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly PomodoroRing _ring = new();
    private readonly Grid _dial = new();
    private readonly TextBlock _caption = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly PomodoroBar _bar = new();
    private readonly ControlsBlock _controls = new();
    private readonly PomodoroBlock _block;
    private readonly Button _runButton;
    private readonly Button _resetButton;
    private readonly Button _skipButton;
    private readonly Button _shorterButton;
    private readonly Button _longerButton;

    private Ticker? _ticker;
    private PomodoroConfig _config = new();
    private IReadOnlyList<PomodoroPhaseSpec> _phases = PomodoroConfig.Classic();
    private int[] _lengths = [];
    private PomodoroPhaseKind[] _kinds = [];
    private PomodoroState _state;

    /// <summary>[S-8111] How long the current phase runs in all: its configured length, or a nudged one until the phase ends.</summary>
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
        _caption.TextWrapping = TextWrapping.NoWrap;
        _dial.Children.Add(_ring);
        _dial.Children.Add(_time);

        // [S-8111] Start first, then − and + beside it, then Reset and Skip.
        _runButton = _controls.Main(PomodoroStr.WidgetsC_Pomodoro_Start.Text(), () => SetRunning(!_running));
        _shorterButton = _controls.Icon(Glyphs.Minus, PomodoroStr.Pomodoro_Shorter.Text(), () => Nudge(-1));
        _longerButton = _controls.Icon(Glyphs.Add, PomodoroStr.Pomodoro_Longer.Text(), () => Nudge(+1));
        _resetButton = _controls.Icon(Glyphs.Reset, PomodoroStr.Pomodoro_Reset.Text(), Reset);
        _skipButton = _controls.Icon(Glyphs.Next, PomodoroStr.WidgetsC_Pomodoro_Skip.Text(), Skip);
        _controls.VerticalAlignment = VerticalAlignment.Top;

        _block = new PomodoroBlock(_dial, _caption, _bar, _controls);

        // The content in the card's inset: the card gives the top, the view the sides and the foot.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _fit.Parts.Add(_block);
        Children.Add(_fit);

        ApplyPhases();
        Load(PomodoroCycle.Fresh(_config));
        Lay();
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

    /// <summary>[S-1110] The fixed tooltips again; <see cref="Lay"/> and <see cref="Render"/> re-read the rest.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_shorterButton, PomodoroStr.Pomodoro_Shorter.Text());
        ToolTipService.SetToolTip(_longerButton, PomodoroStr.Pomodoro_Longer.Text());
        ToolTipService.SetToolTip(_resetButton, PomodoroStr.Pomodoro_Reset.Text());
        ToolTipService.SetToolTip(_skipButton, PomodoroStr.WidgetsC_Pomodoro_Skip.Text());
        Lay();
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as PomodoroConfig ?? new PomodoroConfig();
        ApplyPhases();

        // [S-8112] While the timer is stopped the clock follows the phase's configured length, so changing it
        // moves the digits and the sections together. While it runs the countdown owns the seconds left and
        // loads the next phase itself at the flip; a phase that is gone (fewer phases now) starts the cycle again.
        var index = PomodoroCycle.Normalise(_state.Index, _lengths.Length);
        if (!_running || index != _state.Index)
        {
            Load(new PomodoroState(index, _lengths[index]));
        }

        Lay();
        Render();
    }

    /// <summary>[S-8112] The config's phases, their lengths and kinds, read once per config.</summary>
    private void ApplyPhases()
    {
        _phases = PomodoroCycle.Phases(_config);
        _lengths = PomodoroCycle.Lengths(_config);
        _kinds = [.. _phases.Select(p => p.Kind)];
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

    /// <summary>[S-1987] Space or Enter start and pause, Right skips, Home resets, + / Up and − / Down nudge ([S-8111]).</summary>
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

    /// <summary>[S-02] Reset stops and returns to the cycle's first phase in full. Nothing sounds.</summary>
    private void Reset()
    {
        SetRunning(false);
        Load(PomodoroCycle.Fresh(_config));
        Render();
    }

    /// <summary>[S-1466] [S-8114] Skip to the next phase, running or not; past the last one the cycle starts again, or stops without Repeat. Silent.</summary>
    private void Skip()
    {
        var tick = PomodoroCycle.Skip(_state, _lengths, _config.Repeat);
        Load(new PomodoroState(tick.Index, tick.RemainingSeconds));
        if (tick.Stopped)
        {
            SetRunning(false);
        }

        Render();
    }

    /// <summary>[S-8111] A phase as its config sets it: its length the configured one, a nudge forgotten.</summary>
    private void Load(PomodoroState state)
    {
        var index = PomodoroCycle.Normalise(state.Index, _lengths.Length);
        _state = state with { Index = index };
        _length = _lengths.Length > 0 ? _lengths[index] : state.RemainingSeconds;
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

        var tick = PomodoroCycle.Advance(_state, _lengths, _config.Repeat);
        if (tick.Flipped)
        {
            Load(new PomodoroState(tick.Index, tick.RemainingSeconds));
        }
        else
        {
            _state = new PomodoroState(tick.Index, tick.RemainingSeconds);
        }

        // [S-152] The flip is the only thing that sounds, and it announces the phase being ENTERED.
        // Never from a preview card: it makes no sound ([S-1251]).
        if (tick.Flipped && !_preview && _phases.Count > 0)
        {
            SoundCue.Play(PomodoroSounds.Wave(_config.Sound, PomodoroCycle.Entering(_phases[_state.Index].Kind), _config.EffectiveVolume));
        }

        // [S-8114] The end of a cycle that does not repeat: the first phase is loaded, the timer stops.
        if (tick.Stopped)
        {
            SetRunning(false);
        }

        Render();
    }

    /// <summary>[S-6330] The time, the caption and the sections as they are now: a tick sets what changed and nothing else.</summary>
    private void Render()
    {
        if (_lengths.Length == 0)
        {
            return;
        }

        var index = _state.Index;
        var fraction = PomodoroCycle.Progress(_state, _length);
        _time.Show(PomodoroCycle.Clock(_state.RemainingSeconds));
        var caption = PomodoroCycle.Caption(_phases, index);
        if (_caption.Text != caption)
        {
            // The caption and what comes next change at a flip, never on a tick.
            _caption.Text = caption;
            ToolTipService.SetToolTip(_dial, PomodoroCycle.NextLine(_config, index));
            AutomationProperties.SetName(this, caption);
        }

        // [S-8115] The current phase's section is as long as it runs now (a nudge included).
        if (_plan.Look != PomodoroLook.Digits)
        {
            var lengths = (int[])_lengths.Clone();
            lengths[index] = _length;
            var sections = PomodoroSections.Of(lengths, _kinds, index, fraction);
            if (_plan.Look == PomodoroLook.Ring)
            {
                _ring.Show(sections);
            }
            else
            {
                _bar.Show(sections);
            }
        }

        _shorterButton.IsEnabled = PomodoroNudge.Can(_state, _length, -1);
        _longerButton.IsEnabled = PomodoroNudge.Can(_state, _length, +1);
        ControlsBlock.Relabel(_runButton, (_running ? PomodoroStr.WidgetsC_Pomodoro_Pause : PomodoroStr.WidgetsC_Pomodoro_Start).Text());
    }

    /// <summary>
    /// [S-8117] The face for the style: the time's step, the ring, the bar; and the widths that stay whatever the
    /// time, the phase or the run state reads (WR-12: nothing changes size on a press), so the block's scale holds.
    /// </summary>
    private void Lay()
    {
        _plan = PomodoroFace.Plan(_config, Strings.Current);
        var ringed = _plan.Look == PomodoroLook.Ring;
        _ring.Visibility = ringed ? Visibility.Visible : Visibility.Collapsed;
        _dial.Width = ringed ? _plan.Ring : double.NaN;
        _dial.Height = ringed ? _plan.Ring : double.NaN;
        _bar.Visibility = _plan.Look == PomodoroLook.Bar ? Visibility.Visible : Visibility.Collapsed;
        _block.RingFace = ringed;
        _caption.Text = string.Empty;

        // The widest each part reads: the time on the longest phase's 8s (in Stand), the longest caption, Start or Pause.
        _caption.MinWidth = Enumerable.Range(0, _phases.Count).Select(i => Measured(PomodoroCycle.Caption(_phases, i), FamilyType.Body)).DefaultIfEmpty(0).Max();
        _runButton.MinWidth = Math.Max(
            Measured(PomodoroStr.WidgetsC_Pomodoro_Start.Text(), FamilyType.Body),
            Measured(PomodoroStr.WidgetsC_Pomodoro_Pause.Text(), FamilyType.Body)) + (2 * FamilyShape.ButtonPadding);
        Stand(_block.Arrangement);
    }

    /// <summary>[S-8117] Choose the arrangement from the room the block is given (the view less its padding), before the block is measured.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var room = new Size(
            Math.Max(0, availableSize.Width - Padding.Left - Padding.Right),
            Math.Max(0, availableSize.Height - Padding.Top - Padding.Bottom));
        var arrangement = PomodoroFace.Choose(room.Width, room.Height, _plan);
        if (arrangement != _block.Arrangement)
        {
            Stand(arrangement);
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// [S-8117] The block in <paramref name="arrangement"/>: the time on that arrangement's step, as wide as its widest
    /// reading; stacked, the time and the caption centred; side by side, both on the start.
    /// </summary>
    private void Stand(PomodoroArrangement arrangement)
    {
        _block.Arrangement = arrangement;
        var ringed = _plan.Look == PomodoroLook.Ring;
        var step = _plan.StepIn(arrangement);
        var worst = PomodoroFace.Worst(_config);
        _block.FaceInk = ringed ? LineInk.None : LineInk.Of(step);
        _time.MinWidth = ringed ? 0 : Math.Max(Measured(worst.Value, step), ReadoutRules.Width(step, worst));
        var centred = arrangement == PomodoroArrangement.Tall || ringed;
        _time.Use(step, centred ? HorizontalAlignment.Center : HorizontalAlignment.Left);

        // [S-14319] Held to the widest time its phases show: a tick never moves the figures.
        _time.Hold(worst.Value);
        _time.HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _caption.TextAlignment = arrangement == PomodoroArrangement.Tall ? TextAlignment.Center : TextAlignment.Left;
    }

    /// <summary>How wide <paramref name="text"/> is drawn on <paramref name="step"/>, in whole DIPs.</summary>
    private static double Measured(string text, TypeStep step)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap };
        Family.Step(block, step);
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Ceiling(block.DesiredSize.Width);
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
