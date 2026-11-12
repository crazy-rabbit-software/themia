# Weather facts (WEATHER-FACTS)

The module `weatherFacts` ([S-6911]): the weather's readings where the user says they are, as fact
rows the user picks. An atom ([S-11170]): with the reading (`weatherNow`) it makes the ready-made
group "Weather" ([S-11172]), the old weather card. Its tags are this file's and [S-722]; the root
`SPEC.md` holds the host's sections and what the weather widgets share (the request [S-720], the place
[S-2840]..[S-2853], the header's label [S-728], [S-2260], Refresh [S-730], the sync [S-2663]).

Owning files: everything in this folder: `WeatherFactsModule.cs` (descriptor, sizing, view factory,
JSON context), `WeatherFactsConfig.cs`, `WeatherFactRules.cs` (the facts, their names and values),
`WeatherFactsView.cs` (the SDK's `SkyFeed` and `SkyFactList`), `WeatherFactsMenu.cs`, `Preview/`
(Debug renders: `--state sample|stale|failed`, the same sample reading and place as the reading's),
`Localization/Locales/` (its own `WeatherFactsStr`: the fact names moved here from the reading with
their translations). Tests: `Tests/WeatherFactsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-722]** The readings (`WeatherFactKind`, in this order): Today (today's high and low, "H 16°  L
  8°"), Feels like, Humidity, Wind ("11 km/h NW"), Precipitation (the last hour's amount while it
  rains, else today's chance), Pressure, UV index, Cloud cover, Gusts, Sunrise, Sunset. A reading the
  service did not send is the em dash, never a row left out (the card's height is the pick's).
- **[S-11105]** **What it shows** is its pick (`WeatherFactsConfig.Facts`): which facts and in what
  order, one fact row each ([S-6711]: its 16 glyph, the name, the value at the end), from the content's
  top; a new card picks Today, Feels like, Humidity, Wind. Nothing sheds or appears with the box; a
  name or value too long ends in its ellipsis. A stored pick that is empty, holds a fact twice or one
  it does not have is repaired (load validation, logged). A reading kept through a failed refresh stays
  and the header's problem glyph says so ([S-7042]); with none the Status row says why ([S-6734]). It
  reads the same answer as the reading at the same place: one request ([S-2663]).
- **[S-11106]** **Sizes** (`SkyFacts.Sizing`, [S-11170]): the height its header's 36, a 28 row a fact,
  the inset 12, stepped by a row and following the pick (1..11 rows; four: 160); 200..420 wide, a new
  one 240. Dragging the bottom edge sets the pick ([S-2442]): shorter keeps its first facts, taller adds
  the facts not picked yet in the kind's order.
- **[S-11107]** Its options are all on the properties menu ([S-3719]): **Facts**, the pick
  (`SkyFactLines.Pick`: the picked facts in order, dragged to reorder, each removed from its own line
  but the last, then an Add line for each fact not picked); Appearance; behind a divider **Location**
  (the weather family's search, [S-3722]); behind a divider the links **Temperature unit** and **Update
  interval** ([S-3749]). A click on the card opens the forecast for its place, as the reading's
  ([S-2035]). Tests `WeatherFactsTests`.

Needs a live pass: the pick's reorder and remove on the live menu, a drag of the bottom edge, the
card against the real service.
