# Year progress (YEAR-PROGRESS)

The module `yearProgress` ([S-6911]): how much of the day, week, month and year has passed. Its tags,
[S-4800]..[S-4805] and [S-6040]..[S-6042], are this file's alone; the root `SPEC.md` holds the
host's sections (styles [S-6000], the ring and the dot grid [S-6060], [S-6061], the ladder rules
[S-660]..[S-666]).

Owning files: everything in this folder: `YearProgressModule.cs` (descriptor, view factory, JSON
context), `YearProgressConfig.cs`, `TimeProgress.cs` (the spans, the ladders), `YearProgressView.cs`,
`Preview/` (Debug renders), `Localization/Locales/` (its own `YearProgressStr`, 30 languages).
Tests: `Tests/YearProgressTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4800]** `YearProgress` (124): how much of the day, week, month and year has passed, each a
  label, a percentage and a bar. Its one setting is its style ([S-6040], on the properties panel,
  [S-6536]), so it has no settings page ([S-1750]): the week start and the clock are the regional settings ([S-3770]).
- **[S-4801]** A span runs from one local midnight to the next one after it (`CalendarDays.LocalMidnight`),
  and its fraction is elapsed over real length in instants (`TimeProgress.Of`): a 23-hour
  spring-forward day is half gone at 12:30, a 25-hour one is 25 hours, a leap year 366 days,
  February of one 29. The day is read in the machine's zone, never in UTC. The percentage is
  floored (`TimeProgress.Percent`), so 100 % is only ever the end.
- **[S-4802]** The week starts on the regional first day. Its number is ISO 8601's for a Monday
  week (31 Dec 2026 is week 53) and North America's for a Sunday or Saturday week (week 1 holds
  1 January).
- **[S-4803]** The ladder (`YearProgressLayout.Plan`, on the area less the 16 DIP inset): too narrow
  for a label beside a percentage (< 96 DIPs, XS) or too short for two rows, the year alone, its
  percentage as large as the tile allows over its bar and the year number. Otherwise rows in the
  priority Year, Day, Month, Week, drawn in list order (Day, Week, Month, Year); once all four fit
  with a line each, each carries a detail line, and with room for it (a Tall or larger tile) the
  year's percentage stands large (TitleLarge, one decimal) over them. The XS hero keeps a half
  inset top and bottom and drops the year number before it shrinks. A wide strip that holds four 96-DIP columns stands
  the four side by side. Growing a tile never drops a row.
- **[S-4804]** One tick a minute on the shared ticker (`YearProgressConfig.TickInterval`): the day
  bar moves a fraction of a percent per minute; nothing changes faster.
- **[S-4805]** The details: the day's time left ("9 h 28 min left"), the week's number ("Week 42"),
  the month's and the year's day ("Day 285 of 365"). Every span's tooltip says its label, the
  percentage to one decimal and its detail. Debug: `--state sample` pins Monday 12 October 2026, 14:32.
- **[S-6040]** Year progress's styles ([S-6000]), `YearProgressConfig.Style` (`YearProgressStyle`):
  **Bars** (the default, [S-4803]), **Rings** ([S-6041]) and **Dots** ([S-6042]); the spans, their
  fractions and tooltips are the same in all three.
- **[S-6041]** **Rings** (`YearStyleLayout.Rings`, on the same inset area): a ring per span
  (`RingPart`, [S-6060]) with its percentage inside and its name under it, in the arrangement (four
  across, two by two, one column) that draws them largest; while four rings would be under 56 DIPs
  the bars' priority gives up the rest (Year and Day, then the year alone, its name the last thing
  dropped). Concise: a larger tile draws larger rings, never more.
- **[S-6042]** **Dots** (`YearStyleLayout.Dots`): the year as one dot a day (365 or 366), reflowed
  to the tile ([S-6061]): the days gone in the secondary text fill, today a size up in the accent,
  the days to come in the disabled fill; the caption "2026 · 78 %" over them while the grid keeps a
  6 DIP pitch under it. Under a 3 DIP pitch the year's percentage stands alone as in [S-4803].

Needs a manual pass: the three styles at every tier on the live desktop (showcase views, [S-1810]).
