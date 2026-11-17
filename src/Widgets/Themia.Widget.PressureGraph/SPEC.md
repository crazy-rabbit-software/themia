# Pressure graph (pressureGraph)

The module `pressureGraph` ([S-6911]). Its tags are [S-10700]..[S-10799], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `PressureGraphModule.cs` (descriptor, page lines, JSON context), `PressureGraphConfig.cs`, `PressureGraphRules.cs` (its mapping of the hourly answer ([S-12700]) to the frame, pure), `PressureGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `PressureGraphStr`). Tests: `Tests/PressureGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10700]** Sea-level air pressure hour by hour at a place: a weather graph ([S-12703]) over the hourly answer.
- **[S-10701]** Styles ([S-6000]): **Line** (the default) and **Area**.
- **[S-10702]** Each hour's pressure against the window's range widened to at least 10 hPa (a steady day is drawn flat), all in the accent; the highest and lowest hours' figures (bare hectopascals) over and under their points; the reading the hour under way's ("1012 hPa"). A missing hour is a gap.
- **[S-10704]** Its page ([S-12707]): Style, the place, Time span, the source (Open-Meteo).

Debug states: `sample`, `falling`, `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
