# Moon times (MOON-TIMES)

The family project `Themia.Widget.MoonTimes` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the moon's times where the observer is, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the pair's shared rules (the astronomy [S-160]..[S-169], moonrise and moonset [S-3104], the rise/set text [S-3106], the next full or new moon [S-1491], the shared place [S-3100], [S-3101]).

Owning files: everything in this folder: `MoonKinds.cs`, `MoonMetric.cs` (module, config base and view, the JSON context),
`MoonTimeRules.cs` (the times at an instant, from the SDK's `MoonPhase`), `Samples/`, `Preview/` (`--state sample`),
`Localization/Locales/` (its own `MoonTimesStr`). Tests: `Tests/MoonMetricTests.cs`.

- **[S-11125]** The times: the next moonrise and moonset ([S-3104], their day where it is not today, an em dash where there is
  none, [S-3106]), the next full moon and the next new moon ([S-1491]) as dates, on the reading's lunation.
- **[S-11126]** *(Retired by [S-14695]: one row, no pick to size.)*
- **[S-11127]** *(Retired by [S-14695]: no Times pick on the menu.)*
- **[S-14695]** **A widget a time** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `moonrise` "Moonrise", `moonset` "Moonset", `nextFullMoon` "Next full moon", `nextNewMoon` "Next new moon", the family **Moon times** under Weather
  (`AddFolderId.MoonTimes`), worked out again each minute, each led by the family's moon glyph. A time has no trend. Its
  options: Appearance and the sky's shared Location ([S-3101]). With the reading (`moon`) its rise, set and next full moon
  make the ready-made group "Moon" ([S-11172]). A placed `moonTimes` card is replaced in place by its rows ([S-14606]).

Needs a live pass: the cards on a live desktop.
