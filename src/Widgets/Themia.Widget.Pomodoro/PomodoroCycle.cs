using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Pomodoro;

namespace Themia.Core.Widgets;

/// <summary>[S-8112] Where a pomodoro is right now: which phase of the cycle (0-based) and its seconds left.</summary>
public readonly record struct PomodoroState(int Index, int RemainingSeconds);

/// <summary>
/// The result of one second passing (or a skip).
/// </summary>
/// <param name="Flipped">
/// True only on the tick that rolled into the next phase. This is what the cue hangs off ([S-152]):
/// nothing sounds on start, pause or reset, because none of those go through here.
/// </param>
/// <param name="Stopped">[S-8114] True where the flip ended the cycle and Repeat is off: the first phase is loaded and the timer stops.</param>
public readonly record struct PomodoroTick(int Index, int RemainingSeconds, bool Flipped, bool Stopped = false);

/// <summary>
/// The pomodoro's cycle, as pure arithmetic: the phases in order ([S-8112]), the flip from one to the next
/// ([S-151]), the end of the cycle ([S-8114]), the time left and the words the face says.
/// <para>
/// Separated from the view so the phase flip is reachable without running a widget for 25 minutes.
/// Lengths are passed in rather than read from config here, so a test can flip a two-second phase.
/// </para>
/// </summary>
public static class PomodoroCycle
{
    /// <summary>[S-8112] The phases a config runs through: its own, or the classic cycle where it holds none (an unrepaired config).</summary>
    public static IReadOnlyList<PomodoroPhaseSpec> Phases(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Phases is { Count: > 0 } phases && phases.All(p => p is not null) ? phases : PomodoroConfig.Classic();
    }

    /// <summary>[S-8101] How long a phase runs, in seconds, from its clamped length.</summary>
    public static int Seconds(PomodoroPhaseSpec phase)
    {
        ArgumentNullException.ThrowIfNull(phase);
        return phase.EffectiveMinutes * 60;
    }

    /// <summary>[S-8112] Every phase's length in seconds, in order.</summary>
    public static int[] Lengths(PomodoroConfig config) => [.. Phases(config).Select(Seconds)];

    /// <summary>[S-8112] An index held to the cycle: one past its end (fewer phases after an edit) is its first.</summary>
    public static int Normalise(int index, int count) => count <= 0 || index < 0 || index >= count ? 0 : index;

    /// <summary>[S-8102] A fresh cycle: the first phase in full, stopped.</summary>
    public static PomodoroState Fresh(PomodoroConfig config) => At(config, 0);

    /// <summary>[S-8112] Phase <paramref name="index"/> (held to the cycle) in full.</summary>
    public static PomodoroState At(PomodoroConfig config, int index)
    {
        var lengths = Lengths(config);
        var at = Normalise(index, lengths.Length);
        return new PomodoroState(at, lengths[at]);
    }

    /// <summary>
    /// [S-151] [S-8114] Advance by one second. On reaching zero the next phase in the cycle loads in
    /// <b>full</b>, so the timer keeps running without a pause at the boundary; after the last phase the
    /// cycle starts again with <paramref name="repeat"/>, or loads its first phase and stops without it.
    /// <para>
    /// One second remaining is the last second, not a spare one: a tick from 1 is the flip. A state that
    /// somehow arrived at zero or below (a config edit while stopped, say) also flips rather than
    /// counting downwards for ever.
    /// </para>
    /// </summary>
    public static PomodoroTick Advance(PomodoroState state, IReadOnlyList<int> lengths, bool repeat)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        if (lengths.Count == 0)
        {
            return new PomodoroTick(0, Math.Max(0, state.RemainingSeconds - 1), Flipped: false);
        }

        var index = Normalise(state.Index, lengths.Count);
        if (state.RemainingSeconds > 1)
        {
            return new PomodoroTick(index, state.RemainingSeconds - 1, Flipped: false);
        }

        return Next(index, lengths, repeat);
    }

    /// <summary>
    /// [S-1466] [S-8114] Skip to the next phase now, with its full length loaded. A skip is the user's own
    /// flip, so it never sounds; the caller keeps the running state as it was, unless the skip ended a
    /// cycle that does not repeat.
    /// </summary>
    public static PomodoroTick Skip(PomodoroState state, IReadOnlyList<int> lengths, bool repeat)
    {
        ArgumentNullException.ThrowIfNull(lengths);
        return lengths.Count == 0 ? new PomodoroTick(0, 0, Flipped: false) : Next(Normalise(state.Index, lengths.Count), lengths, repeat);
    }

    private static PomodoroTick Next(int index, IReadOnlyList<int> lengths, bool repeat)
    {
        var next = index + 1;
        var wrapped = next >= lengths.Count;
        if (wrapped)
        {
            next = 0;
        }

        // A length of zero would flip again on the very next tick and sound at 1 Hz for ever.
        return new PomodoroTick(next, Math.Max(1, lengths[next]), Flipped: true, Stopped: wrapped && !repeat);
    }

    /// <summary>
    /// [S-8104] How much of the current phase has gone, 0 at the start and 1 at the flip.
    /// <para>
    /// Clamped, because <c>remaining</c> and <c>total</c> can disagree for one tick after the
    /// lengths are changed in settings while the timer is running.
    /// </para>
    /// </summary>
    public static double Progress(PomodoroState state, int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return 0;
        }

        return Math.Clamp((totalSeconds - state.RemainingSeconds) / (double)totalSeconds, 0, 1);
    }

    /// <summary>The readout, <c>M:SS</c>. Minutes are not padded; a leading zero on a timer reads as a clock.</summary>
    public static string Clock(int remainingSeconds)
    {
        var seconds = Math.Max(0, remainingSeconds);
        var minutes = seconds / 60;

        return $"{minutes.ToString(CultureInfo.InvariantCulture)}:{(seconds % 60):D2}";
    }

    /// <summary>[S-8112] The word for a kind of phase: Focus, Short break, Long break.</summary>
    public static string KindLabel(Locale locale, PomodoroPhaseKind kind) => (kind switch
    {
        PomodoroPhaseKind.ShortBreak => PomodoroStr.Pomodoro_Phase_ShortBreak,
        PomodoroPhaseKind.LongBreak => PomodoroStr.Pomodoro_Phase_LongBreak,
        _ => PomodoroStr.WidgetUi_Focus,
    }).Text(locale);

    /// <summary>[S-8112] A phase's name: the person's own, or its kind's.</summary>
    public static string Label(PomodoroPhaseSpec phase) => Label(Strings.Current, phase);

    /// <summary><see cref="Label(PomodoroPhaseSpec)"/> in an explicit language.</summary>
    public static string Label(Locale locale, PomodoroPhaseSpec phase)
    {
        ArgumentNullException.ThrowIfNull(phase);
        return string.IsNullOrWhiteSpace(phase.Name) ? KindLabel(locale, phase.Kind) : phase.Name.Trim();
    }

    /// <summary>
    /// [S-8116] The caption by the progress: the current phase's name and where it stands in the cycle
    /// ("Focus · 3 of 8"); a cycle of one phase says its name alone.
    /// </summary>
    public static string Caption(Locale locale, IReadOnlyList<PomodoroPhaseSpec> phases, int index)
    {
        ArgumentNullException.ThrowIfNull(phases);
        if (phases.Count == 0)
        {
            return string.Empty;
        }

        var at = Normalise(index, phases.Count);
        var name = Label(locale, phases[at]);
        return phases.Count == 1 ? name : PomodoroStr.WidgetsC_Pomodoro_Position.Format(locale,
            ("phase", name),
            ("index", (at + 1).ToString(CultureInfo.InvariantCulture)),
            ("count", phases.Count.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary><see cref="Caption(Locale, IReadOnlyList{PomodoroPhaseSpec}, int)"/> in the current language.</summary>
    public static string Caption(IReadOnlyList<PomodoroPhaseSpec> phases, int index) => Caption(Strings.Current, phases, index);

    /// <summary>[S-1466] "Next: Short break 5:00": what the flip after phase <paramref name="index"/> loads.</summary>
    public static string NextLine(PomodoroConfig config, int index) => NextLine(Strings.Current, config, index);

    /// <summary><see cref="NextLine(PomodoroConfig, int)"/> in an explicit language.</summary>
    public static string NextLine(Locale locale, PomodoroConfig config, int index)
    {
        var phases = Phases(config);
        var next = phases[(Normalise(index, phases.Count) + 1) % phases.Count];
        return PomodoroStr.WidgetsC_Pomodoro_Next.Format(locale, ("phase", Label(locale, next)), ("time", Clock(Seconds(next))));
    }

    /// <summary>[S-152] The cue that enters a phase of <paramref name="kind"/>: ascending into focus, descending into a break.</summary>
    public static PomodoroPhase Entering(PomodoroPhaseKind kind) => kind == PomodoroPhaseKind.Focus ? PomodoroPhase.Work : PomodoroPhase.Break;
}

/// <summary>[S-8111] The phase after a nudge: where it is now and how long the phase runs in all.</summary>
public readonly record struct PomodoroNudged(PomodoroState State, int LengthSeconds);

/// <summary>
/// [S-8111] The − and + nudge (issue 186): the current phase 5 minutes shorter or longer, without the
/// menu. One rule whether the timer runs or not: the time left and the phase's length move together,
/// so the time already gone (and its section's share of it) stays where it was, and an idle phase simply
/// starts that much shorter or longer. The nudge is this phase's alone, run state like the rest
/// ([S-150]): the phases' lengths stay the standard every new phase loads ([S-8112]), so a flip, a
/// skip or a reset forgets it. A press that would leave less than a minute, or make the phase longer
/// than the menu's most, does nothing: a nudge is 5 minutes or nothing, never a part step.
/// </summary>
public static class PomodoroNudge
{
    /// <summary>One press: 5 minutes.</summary>
    public const int StepSeconds = 5 * 60;

    /// <summary>The least a shorter nudge leaves: the menu's least length.</summary>
    public const int LeastSeconds = PomodoroConfig.MinMinutes * 60;

    /// <summary>The most a longer nudge makes the phase: the menu's most.</summary>
    public const int MostSeconds = PomodoroConfig.MaxMinutes * 60;

    /// <summary>[S-8111] Whether a press of <paramref name="direction"/> (−1 shorter, +1 longer) does anything: its button is disabled where not.</summary>
    public static bool Can(PomodoroState state, int lengthSeconds, int direction) => direction switch
    {
        < 0 => state.RemainingSeconds - StepSeconds >= LeastSeconds && lengthSeconds - StepSeconds >= LeastSeconds,
        > 0 => lengthSeconds + StepSeconds <= MostSeconds && state.RemainingSeconds + StepSeconds <= MostSeconds,
        _ => false,
    };

    /// <summary>[S-8111] The phase after one press of <paramref name="direction"/>, or null where <see cref="Can"/> says it does nothing.</summary>
    public static PomodoroNudged? Apply(PomodoroState state, int lengthSeconds, int direction)
    {
        if (!Can(state, lengthSeconds, direction))
        {
            return null;
        }

        var delta = Math.Sign(direction) * StepSeconds;
        return new PomodoroNudged(state with { RemainingSeconds = state.RemainingSeconds + delta }, lengthSeconds + delta);
    }
}

/// <summary>[S-1987] The pomodoro's key map (<see cref="ControlKey"/>): a pure function of the key, so it is tested here and the view only carries the answer out.</summary>
public static class PomodoroKeys
{
    /// <summary>
    /// [S-1987] Pomodoro: Space or Enter starts and pauses (the accent button), Right skips to the next
    /// phase, Home resets the cycle — the three buttons, in the order they sit.
    /// </summary>
    public static ControlKey Of(WidgetKey key) => key switch
    {
        WidgetKey.Space or WidgetKey.Enter => ControlKey.Toggle,
        WidgetKey.Right => ControlKey.Next,
        WidgetKey.Home => ControlKey.Reset,
        _ => ControlKey.None,
    };

    /// <summary>[S-1987] [S-8111] The nudge keys: + or Up makes the phase 5 minutes longer (+1), − or Down shorter (−1); any other key 0.</summary>
    public static int Nudge(WidgetKey key) => key switch
    {
        WidgetKey.Plus or WidgetKey.Up => 1,
        WidgetKey.Minus or WidgetKey.Down => -1,
        _ => 0,
    };
}
