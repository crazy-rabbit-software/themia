# Temperature graph (temperatureGraph)

The module `temperatureGraph` ([S-6911]). Its tags are [S-10100]..[S-10199], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `TemperatureGraphModule.cs` (descriptor, page lines, JSON context), `TemperatureGraphConfig.cs`, `TemperatureGraphRules.cs` (its mapping of the hourly answer ([S-12700]) to the frame, pure), `TemperatureGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `TemperatureGraphStr`). Tests: `Tests/TemperatureGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10100]** The temperature hour by hour at a place: a weather graph ([S-12703]) over the place's hourly answer, its window 12, 24 or 48 hours (24 for a new one).
- **[S-10101]** Its chart look ([S-6752]): Chart type **Area** (the default, its look until now), Line or Bars; Detail Simple or Detailed ([S-6751]).
- **[S-10102]** Each hour's temperature in the shared unit ([S-3770]) against the window's range widened to at least 5 °C (9 °F); the feels-like temperature as the second, tertiary line on the same scale (**Show feels like**, on for a new one); the highest hour's figure over its point and the lowest's under it ("24°"); the reading is the hour under way's ("9°C"). A missing hour is a gap; fewer than two hours draw the empty sentence.
- **[S-10103]** Heat as the Chart block's roles, read on the °C value whatever the unit shown: Caution from 30 °C, Critical from 35 °C; below, the accent (cold has no role of its own).
- **[S-10104]** Its page ([S-12707]): Chart type and Detail (under Style), the place, Time span, **Show feels like**, the source (Open-Meteo).

Debug states: `sample`, `summer` (33 °C: Caution and Critical), `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
