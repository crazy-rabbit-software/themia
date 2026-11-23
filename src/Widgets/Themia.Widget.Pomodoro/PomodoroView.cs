using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
/// [S-8117] [S-16603] The face is one block (<see cref="PomodoroBlock"/>) laid by one pure rule
/// (<see cref="PomodoroFace.Lay"/>): the time left as the Readout, the caption (the phase and its place in the cycle)
/// and the Controls row, inside the sectioned ring on the Ring style, with the sectioned bar on the Bar style; the
/// card's shape picks how they stand and they scale as one to fill it.
/// </para>
/// </summary>
internal sealed partial class PomodoroView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly ReadoutBlock _time = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly PomodoroRing _ring = new();
    private readonly TextBlock _caption = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly PomodoroBar _bar = new();
    /// <summary>[S-16608] The Controls as two blocks: the main action, and the icon buttons beside it or under it.</summary>
    private readonly ControlsBlock _main = new();
    private readonly ControlsBlock _tools = new();

    /// <summary>[S-16608] The main action's words over unseen copies of each of its labels: the button is as wide as the widest.</summary>
    private readonly Grid _runHeld = new();
    private readonly TextBlock _runLabel;
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

        // [S-8111] Start first, then − and + beside it, then Reset and Skip.
        _runButton = _main.Main(PomodoroStr.WidgetsC_Pomodoro_Start.Text(), () => SetRunning(!_running));
        _shorterButton = _tools.Icon(Glyphs.Minus, PomodoroStr.Pomodoro_Shorter.Text(), () => Nudge(-1));
        _longerButton = _tools.Icon(Glyphs.Add, PomodoroStr.Pomodoro_Longer.Text(), () => Nudge(+1));
        _resetButton = _tools.Icon(Glyphs.Reset, PomodoroStr.Pomodoro_Reset.Text(), Reset);
        _skipButton = _tools.Icon(Glyphs.Next, PomodoroStr.WidgetsC_Pomodoro_Skip.Text(), Skip);
        _main.VerticalAlignment = VerticalAlignment.Top;
        _tools.VerticalAlignment = VerticalAlignment.Top;

        // [S-16608] WR-12: Start becomes Pause on a press; held to the wider, the button and its neighbours never move.
        _runLabel = (TextBlock)_runButton.Content;
        _runLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _runButton.Content = _runHeld;

        _block = new PomodoroBlock(_ring, _time, _caption, _bar, _main, _tools);
        _block.Arranged += Align;

        // The content in the card's inset: the card gives the top, the view the sides and the foot.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        Children.Add(_block);

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
            ToolTipService.SetToolTip(_block, PomodoroCycle.NextLine(_config, index));
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
        var run = (_running ? PomodoroStr.WidgetsC_Pomodoro_Pause : PomodoroStr.WidgetsC_Pomodoro_Start).Text();
        if (_runLabel.Text != run)
        {
            _runLabel.Text = run;
            AutomationProperties.SetName(_runButton, run);
        }
    }

    /// <summary>
    /// [S-8117] [S-16600] The face for the style: the time on its one step, held to its widest reading, and the widths
    /// that stay whatever the time, the phase or the run state reads (WR-12: nothing changes size on a press), so the
    /// face's scale holds.
    /// </summary>
    private void Lay()
    {
        _plan = PomodoroFace.Plan(_config, Strings.Current);
        _ring.Visibility = _plan.Look == PomodoroLook.Ring ? Visibility.Visible : Visibility.Collapsed;
        _bar.Visibility = _plan.Look == PomodoroLook.Bar ? Visibility.Visible : Visibility.Collapsed;
        _block.Look = _plan.Look;
        _caption.Text = string.Empty;

        // The widest each part reads: the time on the longest phase's 8s, the longest caption, Start or Pause.
        var worst = PomodoroFace.Worst(_config);
        _time.MinWidth = Math.Max(Measured(worst.Value, _plan.Step), ReadoutRules.Width(_plan.Step, worst));

        // [S-14319] Held to the widest time its phases show: a tick never moves the figures (WR-15).
        // [S-16607] Unpadded, aligned in its box as the caption is (WR-7): a "9:24" stands where a "25:00" does, centred.
        _time.Hold(worst.Value, aligned: true);
        _caption.MinWidth = Enumerable.Range(0, _phases.Count).Select(i => Measured(PomodoroCycle.Caption(_phases, i), FamilyType.Body)).DefaultIfEmpty(0).Max();
        HoldRunLabels();
        Align(_block.Arrangement);
    }

    /// <summary>[S-16608] The run button's unseen labels, one per word it may show in the current language, under its words.</summary>
    private void HoldRunLabels()
    {
        _runHeld.Children.Clear();
        foreach (var label in PomodoroFace.MainLabels(Strings.Current))
        {
            var held = new TextBlock { Text = label, TextWrapping = TextWrapping.NoWrap, Opacity = 0, IsHitTestVisible = false };
            Family.Step(held, FamilyType.Body);
            AutomationProperties.SetAccessibilityView(held, AccessibilityView.Raw);
            _runHeld.Children.Add(held);
        }

        _runHeld.Children.Add(_runLabel);
    }

    /// <summary>
    /// [S-16603] The time and the caption centred where the parts stand centred (stacked, in the ring), on their start side
    /// by side and beside the upright bar: the time's figures then start where its box does, so its ink reaches the card's
    /// side (WR-3) however far the face is scaled.
    /// </summary>
    private void Align(PomodoroArrangement arrangement)
    {
        var centred = arrangement is PomodoroArrangement.Stacked or PomodoroArrangement.Ring;
        _caption.TextAlignment = centred ? TextAlignment.Center : TextAlignment.Left;
        _time.Use(_plan.Step, centred ? HorizontalAlignment.Center : HorizontalAlignment.Left);
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
