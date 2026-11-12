# Air quality (ONLINE-2)

The module `airQuality` ([S-6911]): the air quality **index** at a place and its band. An atom
([S-11170]..[S-11172]): its UV, pollutant and pollen rows are the `airFacts` atom, the old card the
ready-made group "Air quality"; its id stays `airQuality`. Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and what the online widgets share (the air reply's wire [S-5102]
in the SDK's `AirQuality.cs`, the header's stale rule [S-5160], the offline shelf [S-5161]; the
background web sync [S-2663]; the meters' gauge [S-6302]; the family's blocks [S-6702], [S-6717] and
the online widgets' state row [S-6734]).

Owning files: everything in this folder: `AirQualityModule.cs` (descriptor, view factory, settings
rows, JSON context, its glyph), `AirQualityConfig.cs` (with its styles), `AirQualityView.cs` (it reads
the reading through `IWidgetHost.Weather`, the SDK's `SkyFeed`), `AirLayout.cs` (both styles' sizes),
`AirText.cs` (the band's and the scale's words), `AirQualityMenu.cs`, `Preview/` (Debug renders:
`--state sample|poor|stale|failed`), `Localization/Locales/` (its own `AirQualityStr`). The rules it
shares with the air facts are the SDK's: `Themia.Sdk/Widgets/AirQualityRules.cs` (bands, tones and
roles, UV, pollen, `AirInterests`) and `AirQualityScale.cs`. Tests: `Tests/AirQualityTests.cs`,
`Tests/AirQualityMenuTests.cs`; the wire's own tests stay Core's (`OnlineWidgetsTests`, `S5102_`).

- **[S-5100]** Air quality at a place. A weather-family config (`AirQualityConfig : WeatherConfig`):
  the shared place picker ([S-2840]), the place as the header's label ([S-728], [S-2260]); no place is
  the Set up state ([S-1860]).
- **[S-5101]** The sync asks for it under its own interest (`AirInterest`, `AirInterests.For`, which the
  air facts share: one request a place; the weather case never claims it). Cadence: the shared weather
  cadence ([S-3780]) but never faster than hourly (`OnlineCadence.Air`): CAMS values are hourly in
  Europe, three-hourly elsewhere.
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
  "2.5 · Low" (drawn by the air facts, [S-11135]).
- **[S-5106]** Pollen (Europe, in season): levels from grains/m³ (low, moderate, high) at trees
  (alder, birch, olive) 1, 10, 100; grass and mugwort 1, 10, 50; ragweed 1, 5, 20; the worst type first
  (`AirQualityRules.Pollen`, drawn by the air facts, [S-11135]).
- **[S-5107]** *(Retired by [S-11130]: no readings and no ladder in the index.)*
- **[S-11130]** **What it shows**, the same at every size in its range: in the **Number** style the
  index as the Readout on the family's 28 ("25" and " EAQI", [S-2449]), its band the Caption under it
  (ending in its ellipsis where the card is narrow); in the **Gauge** style [S-6170]. A reading kept
  through a failed refresh stays and the header's problem glyph says so ([S-7042]); with none, or an
  answer without this index, the Status row at the top says why ([S-6734]).
- **[S-11131]** **Sizes** (`AirLayout.Sizing`, with its header): the number one height (100),
  160..320 wide, a new one 200 (the widest index "500 US AQI" fits 160); the gauge one size, its 160
  ring and the band's line in the inset (184 × 145).
- **[S-5108]** Its options are all on the properties menu ([S-3719]); it has no settings page:
  Appearance ([S-3723]), then behind a divider each Location (the weather family's search, [S-3722])
  and Index (Auto / European / US, the current one with the dot), whose submenu ends with the source
  line, greyed: CAMS and Open-Meteo ask to be credited. (Tests `AirQualityMenuTests`.)
- **[S-5109]** Retired (G3, 2026-10-29): the band strip in six status colours is gone.
- **[S-6169]** The air index's styles ([S-6000]), `AirQualityConfig.Style` (`AirQualityStyle`):
  **Number** (the default) and **Gauge**.
- **[S-6170]** **Gauge**: the Chart's half ring ([S-6717], the meters' gauge, [S-6302]) of 160, its arc
  to the index's place on six equal bands (`AirQualityRules.ScalePosition`: the value placed linearly
  inside its band, the open last band as wide as the one before) in the accent, Caution or Critical by
  the band; the index on 28 standing on the ring's foot, the band's words (Caption) centred under the
  ring. The ring and its band are centred unless the widget is aligned elsewhere ([S-6582]).

Needs a live pass: the widget against the real service (a place in and outside Europe for the index),
offline with a restored shelf.
