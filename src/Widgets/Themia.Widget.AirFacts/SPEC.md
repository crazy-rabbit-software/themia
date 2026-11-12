# Air facts (ONLINE-2)

The module `airFacts` ([S-6911]): UV, the pollutants and pollen at a place, as fact rows the user
picks. An atom ([S-11170]): with the index (`airQuality`) it makes the ready-made group "Air quality"
([S-11172]); it is what the index's rows were. Its tags are this file's alone; the root `SPEC.md` and
the air index's `SPEC.md` hold what they share (the reply [S-5102], the interest [S-5101], UV and
pollen [S-5105], [S-5106], the stale rule [S-5160]).

Owning files: everything in this folder: `AirFactsModule.cs`, `AirFactsConfig.cs`, `AirFactRules.cs`
(the rows and their words, over the SDK's `AirQualityRules`), `AirFactsView.cs` (`SkyFeed`,
`SkyFactList`), `Preview/` (`--state sample|poor|stale|failed`: the index's fixtures), `Localization/
Locales/` (its own `AirFactsStr`: the UV, pollutant and pollen words moved here from the index with
their translations). Tests: `Tests/AirFactsTests.cs`.

- **[S-11135]** **What it shows** is its pick (`AirFactsConfig.Facts`): which rows and in what order,
  UV ("2.5 · Low", [S-5105]), PM2.5, pollen (the worst in the air and its level, "Grass · Moderate", or
  "None in the air", [S-5106]), PM10, ozone (µg/m³: a whole number from 10, one decimal under it), then
  each pollen alone (alder, birch, grass, mugwort, olive, ragweed: its level), one fact row each; a new
  card picks UV, PM2.5, pollen, PM10. A reading the service did not send (no pollen forecast outside
  Europe) is the em dash, never a row left out. Nothing sheds or appears with the box; a broken pick is
  repaired. It reads the same air answer as the index at the same place (`AirInterests`, [S-5101]). A
  reading kept through a failed refresh stays and the header's problem glyph says so ([S-7042]); with
  none the Status row says why ([S-6734]). It keeps the header by default (the place its label).
- **[S-11136]** **Sizes** (`SkyFacts.Sizing`, [S-11170]): its header, a 28 row a reading (1..11), the
  inset; 200..420 wide, a new one 240. Dragging the bottom edge sets the pick ([S-2442]).
- **[S-11137]** Its options are all on the properties menu ([S-3719]): **Facts**, the pick
  (`SkyFactLines.Pick`); Appearance; behind a divider **Location** (the weather family's search, [S-3722]).

Needs a live pass: the card against the real service (a place in and outside Europe), the pick on the
live menu.
