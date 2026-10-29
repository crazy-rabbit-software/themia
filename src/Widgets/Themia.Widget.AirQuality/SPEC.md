# Air quality (ONLINE-2)

The module `airQuality` ([S-6911]): air quality, UV and pollen at a place. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality,
rain and exchange rates: the air reply's wire [S-5102] in the SDK's `AirQuality.cs`, the header's stale
rule [S-5160], the offline shelf [S-5161]; the background web sync [S-2663]; the meters' gauge [S-6302]; the family's
blocks [S-6702], [S-6711], [S-6717] and the online widgets' state row [S-6734]).

Owning files: everything in this folder: `AirQualityModule.cs` (descriptor, view factory, settings
rows, JSON context, its glyph), `AirQualityConfig.cs` (with its index scale and styles),
`AirQualityView.cs` and `AirQualityView.Gauge.cs` (they read the reading through
`IWidgetHost.Weather`), `AirQualityRules.cs` (bands, tones and their roles, UV, pollen), `AirLayout.cs` (both
styles' faces), `AirText.cs` (its words), `AirQualityMenu.cs` (its properties-menu lines: the weather
family's `WeatherPlaceRows.LocationLine`, the index and the source line), `Preview/` (Debug renders: `--state sample|poor|stale|failed`),
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
  are good (the index in the primary role, the gauge's arc in the accent: no success green, [S-6706]),
  the next two Caution, the last two Critical (`AirQualityRules.ValueRole`, `ArcRole`); the band is
  always also written out.
- **[S-5105]** UV: WHO categories (under 3 low, 3–6 moderate, 6–8 high, 8–11 very high, 11+ extreme),
  "2.5 · Low".
- **[S-5106]** Pollen (Europe, in season): the worst type and its level, "Grass · Moderate", or "None
  in the air"; levels from grains/m³ (low, moderate, high) at trees (alder, birch, olive) 1, 10, 100;
  grass and mugwort 1, 10, 50; ragweed 1, 5, 20. No pollen row where there is no pollen forecast.
- **[S-5107]** The number style's face (`AirLayout.Number`), the detail widgets' plan ([S-2402],
  `DetailFace`): the index is the Readout at the view's top ("25" and " EAQI", its band the line under
  it), on the step the widest index ("500 US AQI") takes, 28, 40 or 68 and never larger ([S-6702]); the
  readings in priority order UV, PM2.5, pollen, PM10, ozone, then the other agency's index and each
  other pollen in the air with its level, as fact rows with their 16 glyphs (the value primary,
  [S-6711]), whole rows only, beside the index from the top on an expanded width (its column two
  fifths of the card), else 8 under it; XS and S are the index alone. Room left stays empty. A reading
  kept through a failed refresh stays and the header's problem glyph says so ([S-7042]); with none, the
  Status row at the top says why ([S-6734]).
- **[S-5108]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it has
  no settings page: Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722], the
  location-name switch under it) and Index (Auto / European / US, the current one with the dot), whose
  submenu ends with the source line, greyed: CAMS and Open-Meteo ask to be credited. (Tests `AirQualityMenuTests`.)
- **[S-5109]** Retired (G3, 2026-10-29): the band strip in six status colours is gone (a green and
  a scale of its own beside the index); the gauge style is the index on its scale.
- **[S-6169]** The air widget's styles ([S-6000]), `AirQualityConfig.Style` (`AirQualityStyle`):
  **Number** ([S-5107], the default) and **Gauge**.
- **[S-6170]** **Gauge** (`AirLayout.Gauge`): the Chart's half ring ([S-6717], the meters' gauge,
  [S-6302]) as wide as its box (the content, or the side column less the inset) while its half, its
  stroke and the band's line under it fit the height; its arc to the index's place on six equal bands
  (`AirQualityRules.ScalePosition`: the value placed linearly inside its band, the open last band as
  wide as the one before) in the accent, Caution or Critical by the band; the index on the Readout's
  step for the bowl (its width less 24 a side, its half less 8, `MeterPlan.GaugeReading`) standing on
  the ring's foot, the band's words centred under the ring on the index's line under (Caption under 28,
  Body under 40 and 68). The ring and its band are centred in their box unless the widget is aligned
  elsewhere ([S-6582]). The readings as the number style's: beside it on an expanded width, else 8
  under it, whole rows. A ring under 48 is not drawn: the index alone with its band under it.

Needs a live pass: the widget against the real service (a place in and outside Europe for the index
and pollen), offline with a restored shelf.
