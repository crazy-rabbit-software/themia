# Sun times (SUN-TIMES)

The family project `Themia.Widget.SunTimes` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the sun's times where the observer is, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the pair's shared rules (the astronomy [S-160]..[S-169], the civil twilight, noon and daylight change [S-1490], the shared place [S-3100], [S-3101]).

Owning files: everything in this folder: `SunKinds.cs`, `SunMetric.cs` (module, config base and view, the JSON context),
`SunTimeRules.cs` (the times at an instant, from the SDK's `SolarPosition`), `Samples/`, `Preview/` (`--state sample`),
`Localization/Locales/` (its own `SunTimesStr`). Tests: `Tests/SunMetricTests.cs`.

- **[S-11115]** The times: sunrise, sunset, daylight (its length), solar noon, dawn and dusk (civil), each worked out from the
  sky for the observer at an instant; a rise or set that does not happen (inside the polar circles) is the em dash ([S-161]);
  the next of sunrise and sunset is known.
- **[S-11116]** *(Retired by [S-14690]: one row, no pick to size.)*
- **[S-11117]** *(Retired by [S-14690]: no Times pick on the menu.)*
- **[S-14690]** **A widget a time** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `sunrise` "Sunrise", `sunset` "Sunset", `daylight` "Daylight", `solarNoon` "Solar noon", `dawn` "Dawn", `dusk` "Dusk", the family **Sun times** under Weather
  (`AddFolderId.SunTimes`), worked out again each minute. A time has no trend (no Trend line). Its options: Appearance and
  the sky's shared Location ([S-3101]). With the graphic (`sun`) its rise, set and daylight make the ready-made group "Sun"
  ([S-11172]). A sun time opens nothing on a
  click and lights nothing under the pointer: the Sun widget opens nothing either ([S-14706]).

Needs a live pass: the cards on a live desktop at a far northern place.
