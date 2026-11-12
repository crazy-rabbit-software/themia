# Stopwatch (KARPO-2)

The module `stopwatch` ([S-6911]): a stopwatch after Karpo (`karpo-app`, `docs/widgets.md` §84 and
§92). Its tags, [S-4600]..[S-4605] and [S-11302], are this file's alone; the root `SPEC.md` holds the host's
sections (the keyboard lease [S-800], the shared ticker [S-155], the timers' sized face [S-11300]).

Owning files: everything in this folder: `StopwatchModule.cs` (descriptor, view factory, JSON
context, glyph), `StopwatchConfig.cs`, `StopwatchRules.cs` (the record, the state, the text and the
key map `StopwatchKeys`), `StopwatchFace.cs` (the face at its Size, the sizing and its lap rows), `StopwatchView.cs`,
`Preview/` (Debug renders), `Localization/Locales/` (its own `StopwatchStr`, 30 languages). Tests:
`Tests/StopwatchTests.cs`, `Tests/StopwatchFaceTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4600]** A stopwatch (add menu: Time, [S-3511]) on the timers' sized face ([S-11300]): the time
  as its Readout, the lap under way its line under, the Controls row under it: Start, Pause or Resume as
  the one main action (worded; at Small an icon button, Play or Pause), then Lap and Reset as icon
  buttons; under them the newest laps it lists ([S-11302]). No settings page ([S-1750]).
- **[S-4601]** One start button: idle starts, running pauses, paused resumes. Lap only while running (at
  most 999), Reset from any state but idle. A lap is a total; its length is the difference to the one
  before. Lap and Reset are greyed (the disabled role) where they do nothing, never taken away, so the
  row never moves.
- **[S-4602]** The stopwatch is the widget's config (`phase`, `bankedMs`, `startedAt`, `lapsMs`),
  written on every press and read back on attach: a running stopwatch is still running after a restart.
  In a session it counts on the monotonic clock (the performance counter), so setting Windows' clock or
  the zone never moves it. `startedAt` is written from the monotonic run time (now less how long the run
  has lasted) and read back clamped, so a clock set back while Themia was closed reads as no time
  passed, never as negative time. A damaged record (running with no start, laps out of order) is read
  safely and repaired loudly at load. Two views of one widget (one view on two displays) share it: a
  press in one is taken by the other at once.
- **[S-4603]** Written "0:00", "12:34", "1:02:03", whole seconds begun not rounded, while it runs (the
  shared ticker wakes it once a second and only while it runs, [S-155]); paused, with the tenth after
  the language's decimal sign ("12:34.5") under an hour, the time secondary; from an hour a stopped time
  shows its hours and no tenth ("1:02:03"). Laps always carry their tenth. The Readout is sized for the
  widest it draws (every figure an 8: "88:88.8" or "88:88:88", the wider, `StopwatchFace.Worst`), so
  nothing moves as it counts; it stops at 99:59:59.9.
- **[S-4604]** Keys (the lease, [S-800]): Space or Enter start and pause, Right takes a lap, Home resets;
  the letters L and R do the same (type-ahead, `StopwatchKeys.Letter`).
- **[S-4605]** *(Retired: the face by the tile's size, the laps' column from an expanded width; the
  face is the Size's and the laps are a setting, [S-11302].)*
- **[S-11302]** **Its size and its laps are settings.** Each stopwatch widget is its own stopwatch (its
  state is its config, [S-4602]; two widgets never share one), so its laps are a setting of the one
  widget, not a widget of their own. Its menu: **Size** (Small, Medium, Large: the time on 40, 68, 96,
  [S-11300]) and **Laps** (0 to 10, a new one 0): that many one-line fact rows (`RowBlock`, no lead,
  28 each) under the Controls row, edge to edge, newest first, "Lap N" and its length; "No laps yet" in
  the first row until the first lap. The card follows (`StopwatchFace.Sizing`): the inset, the face, 8,
  the row, the lap rows, the inset; one height for its config, stepped by a lap row (dragging its
  bottom edge sets Laps, `StopwatchFace.SizeSets`), up to half again wider with the face centred. It
  starts without a header. No tier ladder is left. Tests `StopwatchFaceTests.S11302_`.

Not ported: Karpo's 100 ms tick in an open folder; a running time here is whole seconds.

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]); a running stopwatch
across a restart of the packaged build.
