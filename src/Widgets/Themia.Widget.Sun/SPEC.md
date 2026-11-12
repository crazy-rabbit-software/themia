# Sun (SKY-SPLIT)

The module `sun` ([S-6911]): the sun's day where the observer is, as a **graphic**: its path across
the sky, or the day as a bar. An atom ([S-11170]..[S-11172]): its times are the `sunTimes` atom, the
old card the ready-made group "Sun"; its id stays `sun`. Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and the pair's shared rules (Sun and moon: the astronomy
[S-160]..[S-169], the two kinds and their shared place [S-3100], [S-3101]; styles [S-6000]).

Owning files: everything in this folder: `SunModule.cs` (descriptor, view factory, menu lines,
JSON context), `SunConfig.cs`, `SunLayout.cs` (its sizes), `SunDayBar.cs` (the day bar's marks),
`SunView.cs`, `SunMenu.cs` (the shared place line, `SkyPlaceRows`), `Preview/` (Debug renders),
`Localization/Locales/` (its own `SunStr`). Tests: `Tests/SunTests.cs`, `Tests/SunMenuTests.cs`.

- **[S-170]**, **[S-3102]** *(Retired by [S-11110]: no rows and no ladder in the sun; its times are the
  sun times' atom, [S-11115].)*
- **[S-11110]** **What it shows** is the graphic alone, in the style's look, filling its box in the
  inset at every size in its range: nothing appears or disappears. It starts headerless (a picture
  says what it is, `StartsHeaderless`); a header it is given names it, the place its label ([S-2260]).
- **[S-11111]** **Sizes** (`SunLayout.Sizing`, headerless): 200..480 wide, a new one 280. The arc's
  height a free range round half its content's width (112..252, a new one 152); the day bar one dot
  tall in the inset, one height (36). The arc fits its box in every season ([S-11806], root): the
  horizon near the foot (the night's dip under it), the day's curve up to the top inset.
- **[S-6045]** The sun's styles ([S-6000]), `SunConfig.Style` (`SunStyle`): **Arc** (the default: the
  path, [S-167], from the Chart's tokens: where the sun has been the accent at the stroke over its 16 %
  wash, the stretch still to come the Track role at the stroke, the night stretch a Track hairline,
  the horizon a Track hairline, the sun a 12 dot in the accent, tertiary below the horizon) and **Day
  bar** ([S-6046]). The old Times style is the `sunTimes` atom. Debug: `--state sample` pins the sample
  place at the sample moment.
- **[S-6046]** **Day bar** (`SunDayBar`): the local day (the machine's zone, midnight to midnight,
  a 23- or 25-hour day that long) as the Chart's 4 track (`ChartBar`, the Track role), the daylight its
  accent fill between sunrise and sunset, now a 12 dot on it (primary in daylight, tertiary at night),
  the track inset by the dot's half either end so the dot at midnight is not cut. Each mark is its
  instant's share of the day in real time; a sunrise or sunset not on this day fills from or to the
  bar's end; polar day fills the whole bar, polar night none of it.
- **[S-3107]** Every option is on the properties menu ([S-3719]); there is no settings page: Style and
  Appearance ([S-3723]), nothing under Style; then behind a divider **Location**, the sky's shared
  search ([S-3101]): the place's city (or its coordinates) at the line's end; under the answers Clear
  location ([S-2848]), Latitude and Longitude, each asked for as text ([S-3721]; a coordinate refused
  unless a number in range, [S-171]; the same value stores nothing).

Needs a manual pass: both styles at min, natural and max on the live desktop (showcase views, [S-1810]).
