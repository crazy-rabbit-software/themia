# Pomodoro

The module `pomodoro` ([S-6911]): a focus and break timer, its ringtone sounding on the flip. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and what the pomodoro shares
with the alarm (the ringtones themselves [S-1461]..[S-1463], the cue's player [S-153], reached
through the SDK's `SoundCue`; styles [S-6000], the ring [S-6060], the keyboard lease [S-800], the
ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `PomodoroModule.cs` (descriptor, view factory, settings
rows, JSON context, glyph), `PomodoroConfig.cs`, `PomodoroCycle.cs` (the cycle as arithmetic, the key
map `PomodoroKeys`), `PomodoroView.cs`, `PomodoroSettings.cs` (its page), `Preview/` (Debug renders),
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
  **Ring** (the default, today's look: the ring on tall and large tiles, [S-1466], the bar on the
  others), **Bar** (the bar wherever the ladder has room for it, never a ring: a large tile stacks
  the digits over the bar) and **Digits** (neither: the digits take the room). `DrawsRing` /
  `DrawsBar`. The ring is the shared `RingPart` ([S-6060]). Debug: `--state sample` (focus, 9:24 left, paused).
- **[S-644]** A short pomodoro tile keeps the countdown: a wide one moves the controls beside it, a
  narrow one drops the progress bar. Stacked, a 120px tile had no room left for the digits.
- **[S-1460]** Pomodoro `Sound` (`AlarmSound`: off + the original's twelve — chime (default),
  marimba, crystal, pluck, droplet, birdsong, zen, bell, gong, arcade, digital, buzzer; listed in
  that order, gentle → insistent) and `Volume` 0..100 (default 70, row hidden while off). Unknown
  names read as chime (lenient converter); `Repair` clamps the volume. Changing either previews the
  focus cue. The old `Alarm` bool is gone (no compatibility).
- **[S-1466]** Pomodoro ladder: XS digits over start/pause; short strips (S, Wide) phase + digits
  left, controls right (+ skip and "Next: Break 5:00" on a wide strip), the bar along the bottom;
  M phase, digits, bar, start/reset/skip, next line; Tall/L/XL a progress ring with the digits
  inside (stacked on Tall; a side column with phase, next, controls on L/XL). Skip loads the other
  phase in full, silently, keeping the running state.
- **[S-1467]** *(Retired: the XL "N sessions done" row (dots and count) and the session counter are
  gone; XL shows the ring, the phase, the next phase and the controls like L.)*
- **[S-1987]** Pomodoro keys (`PomodoroKeys.Of`): Space or Enter start / pause, Right skips to
  the next phase, Home resets: the three buttons in their order. Nothing after the card left.

Needs a manual pass: a whole cycle on the live desktop with a sound (the cue on each flip, nothing
on start, pause or reset) and every tier on the showcase views ([S-1810]).
