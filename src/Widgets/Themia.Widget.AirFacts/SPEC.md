# Air facts (AIR-FACTS)

The family project `Themia.Widget.AirFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): UV, the pollutants and pollen at a place, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root `SPEC.md` and the air index's `SPEC.md` hold what they share (the reply [S-5102], the interest [S-5101], UV and pollen [S-5105], [S-5106], the stale rule [S-5160]).

Owning files: everything in this folder: `AirKinds.cs`, `AirMetric.cs` (module, config base and view over the air answer and
the air hours, the JSON context), `AirFactRules.cs` (the readings and their words, over the SDK's `AirQualityRules`),
`Samples/` (the index's sample day, its air hours the air graph's), `Preview/` (`--state sample|poor|stale|failed`),
`Localization/Locales/` (its own `AirFactsStr`). Tests: `Tests/AirMetricTests.cs`.

- **[S-11135]** The readings: UV ("2.5 · Low", [S-5105]), PM2.5, the worst pollen in the air and its level ("Grass · Moderate",
  or "None in the air", [S-5106]), PM10, ozone (µg/m³: a whole number from 10, one decimal under it), then each pollen alone
  (alder, birch, grass, mugwort, olive, ragweed: its level). A reading the service did not send (no pollen forecast outside
  Europe) is the em dash. Each reads the same air answer as the index at the same place (`AirInterests`, [S-5101]).
- **[S-11136]** *(Retired by [S-14680]: one row, no pick to size.)*
- **[S-11137]** *(Retired by [S-14680]: no Facts pick on the menu.)*
- **[S-14680]** **A widget a reading** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."; and: "split the air facts into individual widgets too."): `airUv` "UV level", `airPm25` "PM2.5", `airPollen` "Pollen", `airPm10` "PM10", `airOzone` "Ozone", `airAlder` "Alder pollen", `airBirch` "Birch pollen", `airGrass` "Grass pollen", `airMugwort` "Mugwort pollen", `airOlive` "Olive pollen", `airRagweed` "Ragweed pollen",
  the family **Air facts** under Weather (`AddFolderId.AirFacts`). A reading kept through a failed refresh stays and the
  status's problem glyph says so ([S-7042]); with none the row stays, "--" dimmed, and why is its tooltip ([S-12813]); the
  place is the index's to name. Trends ([S-14603]): PM2.5, PM10 and ozone an arrow and a line; UV level, alder pollen, birch pollen, grass pollen, mugwort pollen, olive pollen and ragweed pollen an arrow from the last two readings the card saw (no history kept); pollen none (a text, a constant or a time: no Trend line); a pollutant's arrow is this hour against the
  last and its line the hours from the last on, of the place's air hours (the air graph's one answer, asked only while a
  trend over them is drawn). Its options: Appearance, Location (the weather family's search, [S-3722]), Trend where it
  moves; Refresh on its card menu. A click on the card, Enter and the card menu's first line ("Open on MSN Weather") open the forecast for its place, as
  the air graph's ([S-14706], [S-14707], `MetricOpens.Weather`); while a pollutant's trend is Line its graph's Chart type
  (Area, Line, Bars) and Detail stand under Trend ([S-14703]).
- **[S-14685]** **The air graph is these metrics' graphs** ([S-14710], [S-14713]): PM2.5, PM10, ozone and the **air quality
  index** (`airIndex`, "25 · Fair", the index on the place's scale, European in Europe, else the US index, [S-5103], first in
  the family's order) draw their graph over the place's air hours (Area, Line or Bars; one, two or four days, two by
  default). A placed air graph is the metric of the measure it drew, the index (and NO₂, which has no metric) the index's.

Needs a live pass: the cards against the real service (a place in and outside Europe).
