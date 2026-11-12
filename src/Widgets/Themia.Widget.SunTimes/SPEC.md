# Sun times (SKY-SPLIT)

The module `sunTimes` ([S-6911]): the sun's times where the observer is, as fact rows the user picks.
An atom ([S-11170]): with the graphic (`sun`) it makes the ready-made group "Sun" ([S-11172]); it is
what the sun's rows and its Times style were. Its tags are this file's alone; the root `SPEC.md` holds
the pair's shared rules (the astronomy [S-160]..[S-169], the civil twilight, noon and daylight change
[S-1490], the shared place [S-3100], [S-3101]).

Owning files: everything in this folder: `SunTimesModule.cs` (descriptor, sizing, menu lines, JSON
context), `SunTimesConfig.cs`, `SunTimeRules.cs` (the rows at an instant, from the SDK's
`SolarPosition`), `SunTimesView.cs` (the SDK's `SkyEventRow`), `Preview/` (`--state sample`: the
sample place at the sample moment, as the graphic's), `Localization/Locales/` (its own `SunTimesStr`:
the row names moved here from the sun with their translations). Tests: `Tests/SunTimesTests.cs`.

- **[S-11115]** **What it shows** is its pick (`SunTimesConfig.Times`): which rows and in what order,
  sunrise, sunset, daylight (its length), solar noon, dawn, dusk (civil), one fact row each ([S-6735]:
  its 16 lead, the name, the time at the end), one line; the next of sunrise and sunset BodyStrong. A
  new card picks sunrise, sunset and daylight. A rise or set that does not happen (inside the polar
  circles) is the em dash ([S-161]). A name takes its short form where the long one and the value do
  not fit side by side ([S-6735]). Nothing sheds or appears with the box. A stored pick that is empty,
  holds a row twice or one it does not have is repaired. It keeps the header by default (the place its
  label, [S-2260]).
- **[S-11116]** **Sizes** (`SkyFacts.Sizing`, [S-11170]): its header, a 28 row a time (1..6), the
  inset; 200..420 wide, a new one 240. Dragging the bottom edge sets the pick ([S-2442]).
- **[S-11117]** Its options are all on the properties menu ([S-3719]): **Times**, the pick
  (`SkyFactLines.Pick`); Appearance; behind a divider the sky's shared **Location** ([S-3101], as the
  sun's [S-3107]).

Needs a live pass: the pick's reorder on the live menu, a drag of the bottom edge.
