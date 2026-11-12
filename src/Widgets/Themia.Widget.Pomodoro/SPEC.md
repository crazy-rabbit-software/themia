# Pomodoro

The module `pomodoro` ([S-6911]): a focus and break timer, its ringtone sounding on the flip. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and what the pomodoro shares
with the alarm (the ringtones themselves [S-1461]..[S-1463], the cue's player [S-153], reached
through the SDK's `SoundCue`; styles [S-6000], the timers' face [S-6739], the ring [S-6717], the keyboard lease [S-800], the
ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `PomodoroModule.cs` (descriptor, view factory, menu
lines, JSON context, glyph), `PomodoroConfig.cs`, `PomodoroCycle.cs` (the cycle as arithmetic, the key
map `PomodoroKeys`), `PomodoroView.cs`, `PomodoroMenu.cs` (its properties-menu lines), `Preview/` (Debug renders),
`Localization/Locales/` (its own `PomodoroStr`, 30 languages). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

From the original's own tags (`main`'s pomodoro, its S-01, S-02, S-04; written down at the move
from the tests that pinned them under those numbers, which are the root's geometry tags here):

- **[S-8101]** A phase is 1 to 180 minutes, focus and break alike: a stored 0, a negative or a
  larger number is clamped, so a damaged file never flips once a second for ever (the original's
  S-01).
- **[S-8102]** A fresh cycle is a full focus phase; the readout is the minutes and the seconds
  padded to two ("25:00", "0:05", "180:00"), never a negative clock (the original's S-02).
- **[S-8104]** Progress is the elapsed share of the phase, kept within 0..1 when the durations
  change mid-phase (the original's S-04).

- **[S-150]** Pomodoro run state — phase, seconds left, running — lives in the view instance and is
  never persisted. The same widget on two monitors is two independent timers, which is also why only
  a running instance can sound the cue.
- **[S-151]** At zero the phase flips and the new phase's full duration loads, still running. One
  second remaining is the last second, not a spare one.
- **[S-152]** The cue sounds on the flip alone and announces the phase being *entered*: ascending
  into focus, descending into a break. Nothing sounds on start, pause or reset.
- **[S-155]** The 1 Hz subscription exists only while the timer runs, and the callback the shared
  ticker makes at subscribe time is swallowed: that immediate fire is right for a view that renders
  the time and wrong for one that *is* the tick.
- **[S-6035]** The pomodoro's styles ([S-6000]), `PomodoroConfig.Style` (`PomodoroStyle`):
  **Ring** (the default: the time left in the Chart block's ring of the phase's progress wherever a
  ring holds it, else the Bar style's face), **Bar** (the Chart block's bar, the 4 track, 8 under the
  time, the content's width, where the time's floor step, the bar and the Controls row the tile has
  room for fit) and **Digits** (neither). `DrawsRing` / `DrawsBar`. The ring's and the bar's fill is
  the accent in both phases: neither phase is coloured ([S-6706]: no accent text, no success green).
  Debug: `--state sample` (focus, 9:24 left, paused).
- **[S-644]** A short pomodoro tile keeps the time left: the Controls row is the first part to give
  way (where the time's floor step, 8 and the 28 row do not fit), then the bar; the keys still start,
  skip and reset it ([S-1987]).
- **[S-1460]** Pomodoro `Sound` (`AlarmSound`: off + the original's twelve — chime (default),
  marimba, crystal, pluck, droplet, birdsong, zen, bell, gong, arcade, digital, buzzer; listed in
  that order, gentle → insistent) and `Volume` 0..100 (default 70, its line hidden while off). Unknown
  names read as chime (lenient converter); `Repair` clamps the volume. Changing either previews the
  focus cue. The old `Alarm` bool is gone (no compatibility).
- **[S-1466]** Pomodoro face, the timers' ([S-6739], `PomodoroFace.Plan`): the time left as the
  Readout on the Hero steps (a one-readout widget, [S-6737]), sized on the longer phase's widest time
  (its figures as 8s, `PomodoroFace.Worst`) so neither a tick nor a new phase moves it, centred in
  its box, the phase ("Focus", "Break") its line under where it fits and "Next: Break 5:00" its
  tooltip; Start (Pause while it runs) as the one main action and −, +, Reset and Skip as 32 icon
  buttons after it in the Controls row at the bottom inset, centred, the icons giving way from the
  last on a narrow tile (Skip, Reset, then the nudges, [S-8111]); where the row does not fit, none of
  them shows.
  Skip loads the other phase in full, silently, keeping the running state.
- **[S-1467]** *(Retired: the XL "N sessions done" row (dots and count) and the session counter are
  gone; XL shows the ring, the phase, the next phase and the controls like L.)*
- **[S-1987]** Pomodoro keys (`PomodoroKeys.Of`): Space or Enter start / pause, Right skips to
  the next phase, Home resets: the three buttons in their order; + or Up and − or Down
  (`PomodoroKeys.Nudge`) are the nudges ([S-8111]), at a bound doing nothing. Nothing after the card left.
- **[S-8111]** The − and + nudge (issue 186, a student's request: change the time without going
  into the menu): − makes the **current** phase 5 minutes shorter, + 5 minutes longer, running or
  not, outside edit mode (the buttons take their own click, never the card's). One rule
  (`PomodoroNudge`): the time left and the phase's length move together, so the time already gone and
  the ring's or bar's share of it stay put, and an idle phase starts 5 minutes shorter or longer. A
  press that would leave less than the menu's least (1 minute) or make the phase longer than its most
  (180 minutes, [S-8101]) does nothing, its button disabled: a nudge is 5 minutes or nothing. The nudge
  is this phase's alone, run state like the rest ([S-150]), never the stored Focus or Break length:
  those lengths are the standard every phase loads, a nudge is "a bit more of this one", and a
  write would change the standard from a button that says nothing about it (and, as run state is the
  instance's, race the same widget on another monitor). The next flip, a skip or a reset loads the
  menu's length again; a changed length in the menu while stopped does too. Debug: `--state running`
  (9:24 left, going), `--state least` (a fresh 5-minute break, − disabled).
- **[S-8110]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Focus length** and **Break length** (typed,
  1–180 min, [S-3726], the clamped length at the end of the line, [S-8101]); under a divider
  **Alarm sound** (a choice of off and the twelve ringtones, [S-1460]) and **Alarm volume** (0–100 %
  in tens, only while a sound is chosen, [S-6074]); a new sound or volume previews the focus cue
  once. (Tests `PomodoroMenuTests`.)

Needs a manual pass: a whole cycle on the live desktop with a sound (the cue on each flip, nothing
on start, pause or reset) and every tier on the showcase views ([S-1810]).
