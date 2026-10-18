namespace Themia.Core.Widgets;

/// <summary>
/// Which half of the cycle a pomodoro is in. A closed set, so an enum: each value picks a different
/// duration, a different colour and a different alarm, not a different label. The alarm is the
/// direction of a ringtone ([S-1461]): the alarm widget rings the focus one.
/// <para>
/// Deliberately not persisted — run state is per-window and ephemeral ([S-02]) — so it carries no
/// JSON converter.
/// </para>
/// </summary>
public enum PomodoroPhase
{
    Work = 0,
    Break = 1,
}
