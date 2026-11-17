# Rain graph (rainGraph)

The module `rainGraph` ([S-6911]). Its tags are [S-10200]..[S-10299], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `RainGraphModule.cs` (descriptor, page lines, JSON context), `RainGraphConfig.cs`, `RainGraphRules.cs` (its mapping of the hourly answer ([S-12700]) or the rain widget's nowcast ([S-5122]) to the frame, pure), `RainGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `RainGraphStr`). Tests: `Tests/RainGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10200]** Rain at a place: a weather graph ([S-12703]) of columns, over the hourly answer or, in the nowcast style, the rain widget's 15-minute answer.
- **[S-10201]** Styles ([S-6000]): **Bars** (the default: an hour per column over the window) and **Next 2 hours** (the nowcast: a quarter per column).
- **[S-10202]** The hours: each hour's amount from 0 against the wettest hour, at least 2 mm (a drizzle stays low); each hour's chance (0..100 %) as the second, tertiary line (**Show the chance of rain**, on for a new one); the wettest hour's amount over its column (none on a dry window); the reading is the window's total ("2.6 mm") or "Dry". Heavy hours (7.6 mm) are Caution, violent ones (50 mm) Critical. A missing hour is a gap.
- **[S-10203]** The nowcast: the rain widget's window ([S-5124]: the quarter under way and the seven after it) on its fixed scale ([S-5125]), heavy quarters in Caution, the time line's end two hours on; the reading the window's total; a nowcast too old to speak for now draws the empty sentence. Each style asks for its own answer only: the hours the hourly one, the nowcast the rain's (`RainInterest`).
- **[S-10204]** Its page ([S-12707]): Style, the place, Time span and **Show the chance of rain** (the hours only), the source (Open-Meteo).

Debug states: `sample`, `storm` (a summer thunderstorm: an hour in Caution), `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
