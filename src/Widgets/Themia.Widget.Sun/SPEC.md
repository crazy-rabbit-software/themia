# Sun (SKY-SPLIT)

The module `sun` ([S-6911]): the sun's day where the observer is: its path, rise, set and
daylight. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and the
pair's shared rules (Sun and moon: the astronomy [S-160]..[S-169], the civil twilight, noon and
daylight change [S-1490], the two kinds and their shared place [S-3100], [S-3101], the rise/set
text [S-3106]; styles [S-6000], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `SunModule.cs` (descriptor, view factory, menu lines,
JSON context), `SunConfig.cs`, `SunDayBar.cs` (the day bar's marks), `SunView.cs`, `SunMenu.cs`
(its properties menu: the Events switch and the shared place line, `SkyPlaceRows`), `Preview/` (Debug
renders), `Localization/Locales/` (its own `SunStr`, 30 languages). Tests: `Tests/SunTests.cs`,
`Tests/SunMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-170]** The two display settings are independent, except that hiding the graphic forces the
  rows on: a widget showing nothing is never the answer. The graphic itself is never dropped — it is
  sized down to a 46px floor and keeps drawing. Only extras shed, cheapest-first by width: the
  countdown, then the label icons and the moon percentage, then the daylight label, then the number
  strip. (An earlier version of this line had the daylight label going before the icons. The code
  was right and the spec was wrong: "12h 6m" says more than two glyphs do.) This is the Sun widget's
  rule; the moon percentage it once shed is the Moon widget's ([S-3105]).
- **[S-3102]** Sun ladder: [S-1492] without the moon — graphic (46 px floor, [S-170]) → strip →
  rows in priority sunrise, sunset, daylight (+change), solar noon (+altitude), dawn, dusk. One row
  is the next event. Rows beside the graphic on a strip; two to a line on E×E (sunrise | sunset,
  daylight | noon, dawn | dusk) with the graphic keeping 45% of the height.
- **[S-6045]** The sun's styles ([S-6000]), `SunConfig.Style` (`SunStyle`; what was the sun path
  mode, whose "graphic only" is gone: the number strip sheds by size anyway, [S-170]): **Arc** (the
  default: the path, [S-167]), **Day bar** ([S-6046]) and **Times** (no graphic: the rows carry
  everything and are on whatever the Events switch says, [S-170]). The strip and the rows are the
  same under both graphics. Debug: `--state sample` pins 12 October 2026, 14:32.
- **[S-6046]** **Day bar** (`SunDayBar`): the local day (the machine's zone, midnight to midnight,
  a 23- or 25-hour day that long) as a rounded bar in the disabled fill, the daylight in the
  accent's lighter step between sunrise and sunset ticks that stand a little over and under it, the
  sun's dot at now (dimmed while it is down, ringed in the base colour so it reads on the daylight).
  Each mark is its instant's share of the day in real time; a sunrise or sunset not on this day has
  no tick; polar day lights the whole bar, polar night none of it. The bar keeps a 28 DIP floor and
  no share of a large tile: the rows take the rest; it thickens with the height, 8 to 20 DIPs, and
  sits at the foot of its room, right over the number strip.
- **[S-3107]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page. Under Style, after the styles: the **Events** switch, offered by the arc and the
  day bar, not by Times (which is the events, [S-6074]). On the first level: Appearance ([S-3723]),
  then behind a divider **Location**, the sky's shared search ([S-3101]): the place's city (or its coordinates) at the
  line's end; under the answers Clear location ([S-2848]), Latitude and Longitude, each
  asked for as text ([S-3721]; a coordinate refused unless a number in range, [S-171]; the same value
  stores nothing).
- **[S-649]** The sun strip's icons and countdowns appear only where a strip column can hold them
  (250px and 280px tiles), not where the tile can.

Needs a manual pass: the three styles at every tier on the live desktop (showcase views, [S-1810]).
