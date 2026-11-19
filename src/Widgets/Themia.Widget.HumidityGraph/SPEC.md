# Humidity graph (humidityGraph)

The module `humidityGraph` ([S-6911]). Its tags are [S-10600]..[S-10699], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `HumidityGraphModule.cs` (descriptor, page lines, JSON context), `HumidityGraphConfig.cs`, `HumidityGraphRules.cs` (its mapping of the hourly answer ([S-12700]) to the frame, pure), `HumidityGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `HumidityGraphStr`). Tests: `Tests/HumidityGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10600]** Relative humidity hour by hour at a place: a weather graph ([S-12703]) over the hourly answer.
- **[S-10601]** Its chart look ([S-6752]): Chart type **Area** (the default, its look until now), Line or Bars; Detail Simple or Detailed ([S-6751]).
- **[S-10602]** Each hour's humidity against the window's range widened to at least 20 points and held inside 0..100 %, all in the accent (humidity has no danger band of its own); the highest hour's figure over its point and the lowest's under it; the reading the hour under way's ("84%"). A missing hour is a gap.
- **[S-10604]** Its page ([S-12707]): Chart type and Detail (under Style), the place, Time span, the source (Open-Meteo).

Debug states: `sample`, `summer`, `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
