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
- **[S-11110]** **What it shows** is the graphic alone, in the style's look, filling its box
  (the card, a plot edge to edge, [S-15812]) at every size in its range: nothing appears or disappears. A picture says what it is; a Header
  widget over it names it ([S-12481]). In a group it runs to a foot a mate joins ([S-12483]).
- **[S-11111]** **Sizes** (`SunLayout.Sizing`): 64..480 wide ([S-15808]), a new one 280. The arc's
  height a free range (40..252, a new one 152: half its content's width); the day bar one dot
  tall in the inset at its stated height (36). The arc fits its box in every season ([S-11806], root): the
  horizon near the foot (the night's dip under it), the day's curve up to the top ([S-15806]).
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
  bar's end; polar day fills the whole bar, polar night none of it. ([S-15812]: the track runs the card's whole
  width, the daylight's wash its whole height.)
- **[S-15806]** *(Its marker's inset retired by [S-15812]: the path now reaches the plot's edges too.)* **The arc is a
  plot edge to edge**: its plot is the card's whole box (`PlotMark`) and the path runs the box's whole width
  (`SunPathPlot.For`: no longer 13 %..87 % for a number strip that went with the times). Tests `SunPathGeometryTests.S15806_`.
- **[S-15807]** *(Retired by [S-15812]: the day bar is a plot, not a bar scaled as one in the inset.)*
- **[S-15812]** **Both styles are a plot, the card edge to edge** (owner, 2026-11-19, asked whether a graph's plot should
  reach the card's edges: "Plot edge to edge": the plot and its fill reach the card's edges; WR-1, WR-3; the centring
  exemptions do not relax graphs: "Lists, graphs and items are not relaxed"). The arc's horizon and path run from the
  card's left edge to its right and from its top (noon) to its foot (the night's lowest), the daylight's wash from the
  sunrise on the left edge; the day bar is the day from the card's left edge to its right: the Chart's 4 track across the
  card's middle, the daylight its accent fill on it over a wash of it from the card's top to its foot, now a 12 dot on
  the track (`SunLayout.DotAt`: its centre a half dot inside the card's sides). The sun's marker on the arc is drawn where
  it stands, so at the card's edge (noon on its top) the card cuts it. Before, the arc's path stood the marker's half (6)
  in from every side and the day bar was a 12 track in the inset, centred: 58 DIP over and under it at 330 × 130.
  Tests `SunTests.S15812_`.
- **[S-15808]** **It goes down to 64 wide** (WR-14, owner 2026-11-19: "for the widgets that can scale there should basically
  be no lower limit except for one that makes sense so you can still see the widget"): a drawing with no text, its least
  is where its 12 marker (a glyph's floor) still reads on a 40 track or a 64 × 40 arc, over the grid's least 32
  (`SunLayout.Narrowest`, `ArcLeast`). It had stopped at 200 × 112 (the bar 200 × 36). Tests `SunTests.S11111_S15808_`.
- **[S-15809]** **A cleared place keeps the face** (WR-10, [S-2848], [S-1881]): the sun with its place cleared runs under
  the card's compact Set up line, its face drawn for the default place. The Debug state `noPlace` draws it so: a render
  whose state draws a config the card would set up while the card's own would not stands it under that line
  (`WidgetCard.SetupForPreview`, `IWidgetPreviewState.DrawnConfig`).
- **[S-3107]** Every option is on the properties menu ([S-3719]); there is no settings page: Style and
  Appearance ([S-3723]), nothing under Style; then behind a divider **Location**, the sky's shared
  search ([S-3101]): the place's city (or its coordinates) at the line's end; under the answers Clear
  location ([S-2848]), Latitude and Longitude, each asked for as text ([S-3721]; a coordinate refused
  unless a number in range, [S-171]; the same value stores nothing).

Needs a manual pass: both styles at min, natural and max on the live desktop (showcase views, [S-1810]).
