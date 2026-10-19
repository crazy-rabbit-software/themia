# Air quality (ONLINE-2)

The module `airQuality` ([S-6911]): air quality, UV and pollen at a place. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality,
rain and exchange rates: the air reply's wire [S-5102] in the SDK's `AirQuality.cs`, the header's stale
rule [S-5160], the offline shelf [S-5161]; the background web sync [S-2663]; the meters' gauge [S-6302]).

Owning files: everything in this folder: `AirQualityModule.cs` (descriptor, view factory, settings
rows, JSON context, its glyph), `AirQualityConfig.cs` (with its index scale and styles),
`AirQualityView.cs` and `AirQualityView.Gauge.cs` (they read the reading through
`IWidgetHost.Weather`), `AirQualityRules.cs` (bands, tones, UV, pollen), `AirLayout.cs` (both
styles' ladders), `AirText.cs` (its words), `AirQualityMenu.cs` (its properties-menu lines: the weather
family's `WeatherPlaceRows.LocationLine`, the index and the source line), `Preview/` (Debug renders: `--state sample|poor`),
`Localization/Locales/` (its own `AirQualityStr`, 30 languages, its style names included). Tests:
`Tests/AirQualityTests.cs` (run by `tests/Themia.Modules.Tests`); the wire's own tests stay Core's
(`OnlineWidgetsTests`, `S5102_`), the two that read the reply through this widget's rules and the
side-by-side family tests are in the module test host (`OnlineFamilyTests`, `OnlineStylesTests`).

- **[S-5100]** Air quality, UV and pollen at a place. A weather-family config (`AirQualityConfig :
  WeatherConfig`): the shared place picker ([S-2840]), the location-name switch and header rule
  ([S-728]); no place is the Set up state ([S-1860]).
- **[S-5101]** The sync asks for it under its own interest (`AirInterest`, before the weather case,
  which never claims it: `WeatherInterests.For(WeatherConfig)` is only WeatherNow and WeatherForecast).
  Cadence: the shared weather cadence ([S-3780]) but never faster than hourly (`OnlineCadence.Air`):
  CAMS values are hourly in Europe, three-hourly elsewhere. Rain follows the weather cadence as is.
- **[S-5103]** The index is a setting: Automatic (default), European (EAQI) or US (AQI). Automatic
  goes by the **place**, not the user's region (the index is about the air there): European inside
  CAMS Europe's domain, 30°N–72°N and 25°W–45°E, US everywhere else. One reply carries both indices.
- **[S-5104]** Bands: European 0–20 Good, –40 Fair, –60 Moderate, –80 Poor, –100 Very poor, above
  Extremely poor; US 0–50 Good, –100 Moderate, –150 Unhealthy for sensitive groups, –200 Unhealthy,
  –300 Very unhealthy, above Hazardous (a boundary value is in the better band). The first two bands
  are drawn in the Fluent success fill, the next two in caution, the last two in critical; the band
  is always also written out.
- **[S-5105]** UV: WHO categories (under 3 low, 3–6 moderate, 6–8 high, 8–11 very high, 11+ extreme),
  "2.5 · Low".
- **[S-5106]** Pollen (Europe, in season): the worst type and its level, "Grass · Moderate", or "None
  in the air"; levels from grains/m³ (low, moderate, high) at trees (alder, birch, olive) 1, 10, 100;
  grass and mugwort 1, 10, 50; ragweed 1, 5, 20. No pollen row where there is no pollen forecast.
- **[S-5107]** Ladder (`AirLayout.Air`): the index and its band are the hero (XS: number, unit,
  band). The readings in priority order UV, PM2.5, pollen, PM10, ozone, then (the large tiles'
  rungs) the other agency's index and each pollen in the air with its level, as stat cells in two or
  three columns where two fit across (not on a tall column), one stat line each on a tall or narrow
  tile, a side column on a strip ≥360 wide (two columns from 640). Spare height grows the hero.
- **[S-5108]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it has
  no settings page: Appearance ([S-3723]), Location (the weather family's search, [S-3722], the
  location-name switch under it) and Index (Auto / European / US, the current one with the dot), whose
  submenu ends with the source line, greyed: CAMS and Open-Meteo ask to be credited. (Tests `AirQualityMenuTests`.)
- **[S-5109]** Once every reading is shown, the next rung is the band strip: six equal segments in
  their status colours, a marker at the index (placed linearly inside its band; the open last band
  as wide as the one before), the breakpoints under it in Caption.
- **[S-6169]** The air widget's styles ([S-6000]), `AirQualityConfig.Style` (`AirQualityStyle`):
  **Number** ([S-5107], the default) and **Gauge**.
- **[S-6170]** **Gauge** (`AirLayout.AirGauge`): the meters' half-circle gauge ([S-6302],
  `GaugeDial`) filled to the index's place on the six equal bands (`AirQualityRules.ScalePosition`,
  the strip's own mapping, [S-5109]) in the band's status colour, the index in its bowl
  (`GaugeLayout.Reading`); then the band's words on a line under it; then the readings in the
  number style's order, as a column beside the gauge on a strip (≥ 360 wide, two columns from
  640) or as stat lines under it on a tall column while the gauge keeps the column's width. No band
  strip (the gauge is the scale). A box too small for a gauge is the index alone.

Needs a live pass: the widget against the real service (a place in and outside Europe for the index
and pollen), offline with a restored shelf.
