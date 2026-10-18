using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>Where a pomodoro is right now.</summary>
public readonly record struct PomodoroState(PomodoroPhase Phase, int RemainingSeconds);

/// <summary>
/// The result of one second passing.
/// </summary>
/// <param name="Flipped">
/// True only on the tick that rolled into the next phase. This is what the alarm hangs off ([S-06]):
/// nothing sounds on start, pause or reset, because none of those go through here.
/// </param>
public readonly record struct PomodoroTick(PomodoroPhase Phase, int RemainingSeconds, bool Flipped);

/// <summary>
/// The pomodoro countdown, as pure arithmetic.
/// <para>
/// Separated from the view so the phase flip is reachable without running a widget for 25 minutes.
/// Durations are passed in rather than read from config here, so a test can flip a two-second phase.
/// </para>
/// </summary>
public static class PomodoroCycle
{
    /// <summary>A fresh cycle: the start of a work phase, stopped. [S-02]</summary>
    public static PomodoroState Fresh(PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new PomodoroState(PomodoroPhase.Work, config.EffectiveWorkMinutes * 60);
    }

    /// <summary>
    /// How long a phase runs, in seconds, from the clamped config. [S-01]
    /// </summary>
    public static int PhaseSeconds(PomodoroPhase phase, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return (phase == PomodoroPhase.Work ? config.EffectiveWorkMinutes : config.EffectiveBreakMinutes) * 60;
    }

    /// <summary>
    /// Advance by one second. On reaching zero the phase flips and the new phase's <b>full</b>
    /// duration is loaded, so the timer keeps running without a pause at the boundary. [S-03]
    /// <para>
    /// One second remaining is the last second, not a spare one: a tick from 1 is the flip. A state
    /// that somehow arrived at zero or below — a config edit while stopped, say — also flips rather
    /// than counting downwards for ever.
    /// </para>
    /// </summary>
    public static PomodoroTick Advance(PomodoroState state, int workSeconds, int breakSeconds)
    {
        if (state.RemainingSeconds > 1)
        {
            return new PomodoroTick(state.Phase, state.RemainingSeconds - 1, Flipped: false);
        }

        var next = state.Phase == PomodoroPhase.Work ? PomodoroPhase.Break : PomodoroPhase.Work;
        var loaded = next == PomodoroPhase.Work ? workSeconds : breakSeconds;

        // A duration of zero would flip again on the very next tick and alarm at 1 Hz for ever.
        return new PomodoroTick(next, Math.Max(1, loaded), Flipped: true);
    }

    /// <summary>
    /// How much of the current phase has gone, 0 at the start and 1 at the flip. [S-04]
    /// <para>
    /// Clamped, because <c>remaining</c> and <c>total</c> can disagree for one tick after the
    /// durations are changed in settings while the timer is running.
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

    /// <summary>The word for a phase, as the widget says it.</summary>
    public static string PhaseLabel(PomodoroPhase phase) => PhaseLabel(Strings.Current, phase);

    /// <summary><see cref="PhaseLabel(PomodoroPhase)"/> in an explicit language.</summary>
    public static string PhaseLabel(Locale locale, PomodoroPhase phase)
        => Strings.Get(locale, phase == PomodoroPhase.Work ? Str.WidgetUi_Focus : Str.WidgetUi_Break);

    /// <summary>The phase after <paramref name="phase"/>.</summary>
    public static PomodoroPhase Other(PomodoroPhase phase) => phase == PomodoroPhase.Work ? PomodoroPhase.Break : PomodoroPhase.Work;

    /// <summary>
    /// [S-1466] Skip to the next phase now, with its full length loaded. A skip is the user's own
    /// flip, so it never sounds; the caller keeps the running state as it was.
    /// </summary>
    public static PomodoroState Skip(PomodoroState state, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var next = Other(state.Phase);
        return new PomodoroState(next, PhaseSeconds(next, config));
    }

    /// <summary>[S-1466] "Next: Break 5:00" — what the flip will load.</summary>
    public static string NextLine(PomodoroPhase phase, PomodoroConfig config) => NextLine(Strings.Current, phase, config);

    /// <summary><see cref="NextLine(PomodoroPhase, PomodoroConfig)"/> in an explicit language.</summary>
    public static string NextLine(Locale locale, PomodoroPhase phase, PomodoroConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var next = Other(phase);
        return Strings.Format(locale, Str.WidgetsC_Pomodoro_Next, ("phase", PhaseLabel(locale, next)), ("time", Clock(PhaseSeconds(next, config))));
    }
}
