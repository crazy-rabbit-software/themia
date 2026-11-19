# Air quality graph (airQualityGraph)

The module `airQualityGraph` ([S-6911]). Its tags are [S-10400]..[S-10499], this file's alone; the weather graphs' one frame ([S-12703]), sizing ([S-12704]), window ([S-12705]), panel ([S-12706]), config and page ([S-12707]) and top line ([S-12708]) are the root `SPEC.md`'s (WEATHER-GRAPHS).

Owning files: everything in this folder: `AirQualityGraphModule.cs` (descriptor, page lines, JSON context), `AirQualityGraphConfig.cs`, `AirQualityGraphRules.cs` (its mapping of the hourly air answer ([S-12701]) to the frame, pure), `AirQualityGraphView.cs` (it reads the answer through `IWidgetHost.Weather`), `Samples/` (the add page's picture, `SampleHours`), `Preview/` (Debug renders), `Localization/Locales/` (its own `AirQualityGraphStr`). Tests: `Tests/AirQualityGraphTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10400]** The air hour by hour for the next days at a place: a weather graph ([S-12703]) over the air's hourly answer (`AirHoursInterest`, the air's cadence), its window 1, 2 or 4 days (2 for a new one).
- **[S-10401]** Its chart look ([S-6752]): Chart type **Area** (the default, its look until now), Line or Bars; Detail Simple or Detailed ([S-6751]).
- **[S-10402]** **Shows**: the air quality index (the default; on its **Index**, Automatic by the place as the air widget's, [S-5103], European or US) or one pollutant, PM2.5, PM10, O₃ or NO₂ in µg/m³. Each hour's value from 0 against the highest, at least the top of its second band (a clean window stays low); the worst hour's figure over it; the reading is the hour under way's value and band ("25 · Fair"). The kind's glyph leads the reading ([S-12801]); a pollutant's name and unit follow it ("PM2.5 µg/m³", data the
  reading needs), the index none (its band's words say what it is; "Air quality" and the place are the Header widget's). A missing hour is a gap.
- **[S-10403]** Bands: the index's are the air widget's ([S-5104]); a pollutant's are the European Environment Agency's (µg/m³, the upper edges of Good, Fair, Moderate, Poor, Very poor): PM2.5 10, 20, 25, 50, 75; PM10 20, 40, 50, 100, 150; O₃ 50, 100, 130, 240, 380; NO₂ 40, 90, 120, 230, 340; a value on an edge is the better band. As roles ([S-6170]): the two good bands the accent, the next two Caution, the last two Critical; the reading primary while good. Names: the European words, or the US index's for the US index.
- **[S-10404]** Its page ([S-12707]): Chart type and Detail (under Style), the place, Time span, **Shows**, **Index** (for the index), the source (CAMS · Open-Meteo). A measure or a scale that is none is repaired to Index and Automatic.

Debug states: `sample`, `smog` (four times the particles: Caution and Critical), `stale`, `loading`, `empty`, `failed`. The add page's weather subject, its third run (the weather graphs); a click opens MSN Weather's forecast for its place ([S-2037]).

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]), and the real service.
