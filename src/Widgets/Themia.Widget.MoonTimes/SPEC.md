# Moon times (SKY-SPLIT)

The module `moonTimes` ([S-6911]): the moon's times where the observer is, as fact rows the user picks.
An atom ([S-11170]): with the reading (`moon`) it makes the ready-made group "Moon" ([S-11172]); it is
what the moon's rows were. Its tags are this file's alone; the root `SPEC.md` holds the pair's shared
rules (the astronomy [S-160]..[S-169], moonrise and moonset [S-3104], the rise/set text [S-3106], the
next full or new moon [S-1491], the shared place [S-3100], [S-3101]).

Owning files: everything in this folder: `MoonTimesModule.cs`, `MoonTimesConfig.cs`, `MoonTimeRules.cs`
(the rows at an instant, from the SDK's `MoonPhase`), `MoonTimesView.cs`, `Preview/` (`--state
sample`), `Localization/Locales/` (its own `MoonTimesStr`: the rise and set names moved here from the
moon with their translations). Tests: `Tests/MoonTimesTests.cs`.

- **[S-11125]** **What it shows** is its pick (`MoonTimesConfig.Times`): which rows and in what order,
  moonrise, moonset, the next full moon, the next new moon (`MoonPhase.NextFull`, `NextNew`, on the
  reading's lunation), one fact row each ([S-6735]: the moon drawn at 16 as its lead, at its phase now
  for the rise and set and a role lower while it is down, full or new for those dates), one line; the
  next of rise and set BodyStrong. A new card picks moonrise, moonset and the next full moon. A name
  takes its short form, a date drops its weekday, where the row cannot hold both. Nothing sheds or
  appears with the box; a broken pick is repaired. It keeps the header by default.
- **[S-11126]** **Sizes** (`SkyFacts.Sizing`, [S-11170]): its header, a 28 row a time (1..4), the
  inset; 200..420 wide, a new one 240. Dragging the bottom edge sets the pick ([S-2442]).
- **[S-11127]** Its options are all on the properties menu ([S-3719]): **Times**, the pick
  (`SkyFactLines.Pick`); Appearance; behind a divider the sky's shared **Location** ([S-3101]).

Needs a live pass: the pick's reorder on the live menu, a drag of the bottom edge.
