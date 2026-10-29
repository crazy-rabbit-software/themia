# Stopwatch (KARPO-2)

The module `stopwatch` ([S-6911]): a stopwatch after Karpo (`karpo-app`, `docs/widgets.md` §84 and
§92). Its tags, [S-4600]..[S-4605], are this file's alone; the root `SPEC.md` holds the host's
sections (the keyboard lease [S-800], the shared ticker [S-155], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `StopwatchModule.cs` (descriptor, view factory, JSON
context, glyph), `StopwatchConfig.cs`, `StopwatchRules.cs` (the record, the state, the text and the
key map `StopwatchKeys`), `StopwatchFace.cs` (the face and the laps' column), `StopwatchView.cs`,
`Preview/` (Debug renders), `Localization/Locales/` (its own `StopwatchStr`, 30 languages). Tests:
`Tests/StopwatchTests.cs`, `Tests/StopwatchFaceTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4600]** A stopwatch (add menu: Time, [S-3511]) on the timers' face ([S-6739]): the time as
  its Readout, the lap under way its line under, the Controls row under it: Start, Pause or Resume as
  the one main action (worded), then Lap and Reset as icon buttons; the laps beside it from an
  expanded width, newest first. No settings page ([S-1750]).
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
  the language's decimal sign ("12:34.5"), the time secondary. Laps always carry their tenth. The
  Readout is sized for the widest reading of its hour (every figure an 8 with its tenth,
  `StopwatchFace.Widest`), so its step never changes as it counts; it stops at 99:59:59.9.
- **[S-4604]** Keys (the lease, [S-800]): Space or Enter start and pause, Right takes a lap, Home resets;
  the letters L and R do the same (type-ahead, `StopwatchKeys.Letter`).
- **[S-4605]** The face (`StopwatchFace.Plan`, `TimerFace.Plan`): the time on the Hero steps, centred
  both ways over the centred Controls row, which shows only where the 28 line, 8 and the row fit (XS
  keeps the time and its lap line); from an expanded width (Wide, L, XL) the laps stand in a column of
  two fifths of the view at its end edge, from the content's top: fact rows (`RowBlock`, no lead) of
  "Lap N" and its length, its total the second line where the tier gives rows two lines ([S-6712]),
  whole rows only; "No laps yet" is the Status row at the column's top until the first lap. Narrower,
  the lap line alone says where the laps are ([S-666]): the strip's buttons beside the time, the
  half-and-half columns, the laps under the time on a tall tile and the total's own column are gone.

Not ported: Karpo's 100 ms tick in an open folder; a running time here is whole seconds.

Needs a manual pass: every tier on the live desktop (showcase views, [S-1810]); a running stopwatch
across a restart of the packaged build.
