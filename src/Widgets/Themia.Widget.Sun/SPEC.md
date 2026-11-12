# Sun (SKY-SPLIT)

The module `sun` ([S-6911]): the sun's day where the observer is: its path, rise, set and
daylight. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and the
pair's shared rules (Sun and moon: the astronomy [S-160]..[S-169], the civil twilight, noon and
daylight change [S-1490], the two kinds and their shared place [S-3100], [S-3101], the rise/set
text [S-3106]; styles [S-6000], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `SunModule.cs` (descriptor, view factory, menu lines,
JSON context), `SunConfig.cs`, `SunLayout.cs` (the ladder), `SunDayBar.cs` (the day bar's marks), `SunView.cs`, `SunMenu.cs`
(its properties menu: the Events switch and the shared place line, `SkyPlaceRows`), `Preview/` (Debug
renders), `Localization/Locales/` (its own `SunStr`, 30 languages). Tests: `Tests/SunTests.cs`,
`Tests/SunMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-170]** The two display settings are independent, except that hiding the graphic forces the
  rows on: a widget showing nothing is never the answer. The graphic itself is never dropped — the
  arc keeps a 48 floor (`SunLayout.ArcFloor`) and keeps drawing; rows shed whole, last first. The
  number strip under the graphic is gone (G3, 2026-10-29): its times are the rows' (concise first,
  [S-666]). The moon percentage it once shed is the Moon widget's ([S-3105]).
- **[S-3102]** Sun ladder on the family's blocks (`SunLayout`, [S-6711], [S-6712]; G3 2026-10-29):
  the Header (the sun's glyph, the widget's name, the place as its label, [S-2260]), then the graphic at the content's top in the
  inset, then whole fact rows in priority sunrise, sunset, daylight (its change the note), solar noon
  (the sun's height the note), dawn, dusk ("civil" the note), 8 under it ([S-6735]: the next event's
  name BodyStrong, its countdown or a row's note the second line where the tier gives rows two
  lines; with one row it is the next event). The arc keeps 48 and takes what the rows leave, never
  taller than half its width; on an expanded width (Wide, L, XL) it stands in a column of two fifths
  of the card and the rows beside it from the top, as the detail widgets' figure ([S-2402]). Beside a
  graphic, rows need a regular width (180): XS is the graphic alone. Room left at L and XL stays empty.
- **[S-6045]** The sun's styles ([S-6000]), `SunConfig.Style` (`SunStyle`): **Arc** (the default: the
  path, [S-167], from the Chart's tokens: where the sun has been the accent at the stroke over its 16 %
  wash, the stretch still to come the Track role at the stroke, the night stretch a Track hairline,
  the horizon a Track hairline, the sun a 12 dot in the accent, tertiary below the horizon), **Day
  bar** ([S-6046]) and **Times** (no graphic: the rows from the content's top, on whatever the Events
  switch says, [S-170]). The rows are the same under both graphics. Debug: `--state sample` pins 12
  October 2026, 14:32.
- **[S-6046]** **Day bar** (`SunDayBar`): the local day (the machine's zone, midnight to midnight,
  a 23- or 25-hour day that long) as the Chart's 4 track (`ChartBar`, the Track role), the daylight its
  accent fill between sunrise and sunset, now a 12 dot on it (primary in daylight, tertiary at night),
  the track inset by the dot's half either end so the dot at midnight is not cut. Each mark is its
  instant's share of the day in real time; a sunrise or sunset not on this day fills from or to the
  bar's end; polar day fills the whole bar, polar night none of it. The bar is one dot tall at the
  content's top, over its rows at every width (a wide thing stands over its rows).
- **[S-3107]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page. Under Style, after the styles: the **Events** switch, offered by the arc and the
  day bar, not by Times (which is the events, [S-6074]). On the first level: Appearance ([S-3723]),
  then behind a divider **Location**, the sky's shared search ([S-3101]): the place's city (or its coordinates) at the
  line's end; under the answers Clear location ([S-2848]), Latitude and Longitude, each
  asked for as text ([S-3721]; a coordinate refused unless a number in range, [S-171]; the same value
  stores nothing).

Needs a manual pass: the three styles at every tier on the live desktop (showcase views, [S-1810]).
