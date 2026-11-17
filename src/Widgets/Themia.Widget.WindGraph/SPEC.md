# Wind graph (windGraph)

The module `windGraph` ([S-6911]). Its tags are [S-10500]..[S-10599], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `WindGraphModule.cs` (descriptor, page lines, JSON context), `WindGraphConfig.cs`, `WindGraphRules.cs` (its mapping of the hourly answer ([S-12700]) to the frame, pure), `WindGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `WindGraphStr`). Tests: `Tests/WindGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10500]** Wind hour by hour at a place: a weather graph ([S-12703]) over the hourly answer, with an arrow line under the plot.
- **[S-10501]** Styles ([S-6000]): **Line** (the default) and **Bars**.
- **[S-10502]** Each hour's mean wind (km/h) from 0 against the strongest drawn, at least 30 km/h; the gusts as the second, tertiary line (**Show gusts**, on for a new one); the strongest hour's figure over it; an arrow per hour that has room (a 16 glyph and 4 apart) pointing where the wind blows to; the reading is the hour under way's speed and where it comes from ("14 km/h SW"). A missing hour is a gap.
- **[S-10503]** A gale (Beaufort 8, 62 km/h) is Caution, a storm (Beaufort 10, 89 km/h) Critical; below, the accent.
- **[S-10504]** Its page ([S-12707]): Style, the place, Time span, **Show gusts**, the source (Open-Meteo).

Debug states: `sample`, `calm`, `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
