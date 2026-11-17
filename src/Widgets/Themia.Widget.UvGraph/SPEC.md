# UV graph (uvGraph)

The module `uvGraph` ([S-6911]). Its tags are [S-10300]..[S-10399], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `UvGraphModule.cs` (descriptor, page lines, JSON context), `UvGraphConfig.cs`, `UvGraphRules.cs` (its mapping of the hourly answer ([S-12700]) to the frame, pure), `UvGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `UvGraphStr`). Tests: `Tests/UvGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10300]** The UV index hour by hour at a place: a weather graph ([S-12703]) over the hourly answer, with the place's daylight shaded behind it.
- **[S-10301]** Styles ([S-6000]): **Bars** (the default) and **Area**.
- **[S-10302]** Each hour's index from 0 against the highest, at least 6 (a low day is drawn low); the peak's figure over it (none on a day with no sun); each day's sunrise to sunset shaded (the Subtle role); the reading is the hour under way's index and band ("5 · Moderate"). A missing hour is a gap.
- **[S-10303]** The WHO bands ([S-5105]: Low under 3, Moderate under 6, High under 8, Very high under 11, Extreme) as the Chart block's roles: Low the accent, Moderate and High Caution, Very high and Extreme Critical; the reading primary while Low, else its band's role. Each band has its name.
- **[S-10304]** Its page ([S-12707]): Style, the place, Time span, the source (Open-Meteo).

Debug states: `sample` (an overcast autumn day: Low), `summer` (every band to Very high), `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
