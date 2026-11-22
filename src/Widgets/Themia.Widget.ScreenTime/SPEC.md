# Screen time (the family: the reading, the chart, the apps and the ring)

The family project `Themia.Widget.ScreenTime` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the four screen-time widgets,
each a kind with its own id, module, config and view, over the one tracker, sharing what every one of them holds alike
(`ScreenTimeWiring`: the tracker followed, the day navigation, the app links and the view's dates, joined on Attach and
left on Detach), one JSON context (`ScreenTimeModule.cs`) and one string table (`ScreenTimeStr`, every kind's keys). The
sections below are each kind's; their tags are this file's.

## Screen time (the reading)

The module `screenTime` ([S-6911]): the screen-time total of a day or a rolling week or month, as a
reading. One of the four screen-time atoms (owner decision 2026-11-12, [S-2438]): this reading, the
chart (`screenTimeChart`), the app list (`screenTimeApps`) and the ring (`screenTimeDonut`), all over the
one tracker and its shared preferences, joined by the view's links; the ready-made group "Screen time"
is the reading over the chart over the apps ([S-11520]). Its tags are [S-11500]..[S-11504] and [S-14282]; the root
`SPEC.md` holds what the screen-time widgets share ([S-450], [S-454], [S-457], [S-458], [S-459],
[S-3310], [S-3321]..[S-3325], [S-3355]).

Owning files: `ScreenTimeModule.cs` (descriptor, menu lines, the family's JSON context),
`ScreenTimeConfig.cs` (the period), `ScreenTimeReading.cs` (its size and its words, pure),
`ScreenTimeView.cs` (the readout through its host), `Preview/` (Debug renders: `--state
sample|hovered|selected|over|packaged`), `Localization/Locales/` (the family's `ScreenTimeStr`; its name is
the add menu's "Screen time" with its translations, "of {value}/day" moved from the chart with its
translations, "of {value}" the donut's). Tests: `Tests/ScreenTimeTests.cs`.

- **[S-11500]** **The reading** shows a day on its first line, a tool line with its arrows ([S-12481], [S-454];
  a window, the last 7 or 30 days, has no such line: [S-12803]) and draws the period's total on the family's 28
  (`FamilyType.Big28`, start-aligned, [S-2449]) with one line under it: one height (12 + 24 + 36 + 16 + 12 =
  100 for a day, 76 for a window, `ScreenTimeReading.Sizing`), 160..320 wide (the least holds "888h 59m"),
  a new one 200 × that. It is what the chart's figure was ([S-3332], retired): the chart draws the bars
  alone. Nothing to read keeps the readout ([S-12813]): "--" while nothing records, none spent ("0m") in a period that recorded, disabled, why as its line under.
- **[S-11501]** **Its words** (`ScreenTimeReading.Headline`): a day's total with "of 4h" under it where
  a daily limit is set; a window's total with its average per day and "of 6h/day" ("3h 21m/day · of
  6h/day"); over the limit (a window: its average) the figure is Critical ([S-457]). The line ends in
  an ellipsis on a narrow card.
- **[S-11502]** Its menu: **Period** (Day, Week, Month; a new one Week, as the chart it stands over in
  the group), then the shared lines ([S-3323]: of the limits the day's).
- **[S-11503]** **Links** ([S-3310]): it joins the view's `HoveredApp`/`SelectedApp` as the chart's figure
  did; a focused app stands in place of the total, its time over the period and its share under it
  ("Code · 41%"). It publishes nothing and takes no keys: it has no parts to point at.

- **[S-14282]** **The day line stands with its reading** (owner, 2026-11-19, docs/WIDGET-RULES.md WR-7: "A reading
  and its caption stay together and scale up"; "Keep them together"; "All together, scaled"). The day and its arrows
  ([S-454], a tool line) stand right over the total and its line under, as wide as the reading, one block that the card
  scales as one ([S-12808], [S-14280]), never a line on the card's top corners over a reading centred lower down.
Needs a manual pass: the reading on a live desktop beside the chart and the list (a real week, an app
lit in the chart shown here), never in a render or a test, which track nothing.
- View links: with the period Day it shows the day selected on the view, as far as its arrows reach ([S-11928]).

## Screen time: chart

The module `screenTimeChart` ([S-6911]): the screen-time history over a week or a month. Its tags are
this section's; the root `SPEC.md` holds the host's sections and what the three screen-time widgets
share, with their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the
host's `ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu,
`SettingsHost.ScreenTime`), the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`), the
period and the chart's own period and style rules ([S-3321], [S-3330], the SDK's `ScreenTimeRules`), the
calendar ([S-456]), the linked focus ([S-3310]) and the sample history the previews draw ([S-3355], the
SDK's `SampleScreenTime`).

Owning files: `ScreenTimeChartModule.cs` (descriptor, view factory, menu
lines), `ScreenTimeChartConfig.cs` (the period and the style, `ScreenTimeChartStyle`), `ScreenTimeChartView.cs`
(the bars and the calendar through its host, the keys), `ScreenTimeChartLayout.cs` (its sizes),
`ScreenTimeChartMenu.cs` (its properties menu, ending with the shared lines), `Preview/` (Debug renders:
`--state sample|hovered|selected|packaged`), `Localization/Locales/` (the family's `ScreenTimeStr`). Tests: `Tests/ScreenTimeChartTests.cs`, `Tests/ScreenTimeChartMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3335]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Week, Month: a day reads as the week,
  [S-3330]), then the shared lines ([S-3323]: of the limits the day's). Bars or Calendar is the
  menu's Style line ([S-3336]), not a line of its own.
- **[S-3336]** Bars and Calendar are the widget's **styles** ([S-6000]; what was its own Style choice
  is the style, never a second setting): `ScreenTimeChartStyle` (Bars the default, then Calendar),
  `ScreenTimeChartConfig.Style` over `IStyled`, stored as `"style": "bars" | "calendar"`
  (`StyleJsonConverter`; an unknown name is repaired to Bars at load, [S-6004]), copied in `Clone`,
  its labels `styles.screenTimeChart.*`. `--render widget ScreenTimeChart … --style calendar` draws it.
- **[S-3331]** Bars: one bar per day, oldest left, touching ([S-15788]), stacked by
  app in the ring's order (the busiest seven, `ScreenTimeRules.DrawingApps`, then Other) in the
  accent's ramp by rank, Other and the apps past the fourth tertiary at the wash ([S-2272]), scaled to the busiest day, the stack one
  rectangle of the family's radius (its top part's top corners, its foot part's foot, [S-6704]); under
  them only the two ends are labelled, always ([S-6717]: Caption, tertiary): the first day and today, a
  week's by weekday ("Fri", "Thu"), a month's by date ("Sep 30", "Oct 29"). Calendar ([S-456]): seven
  columns of day cells ([S-15772]), a cell a ring of its day's share of the busiest day (the Chart
  block's `ChartRing`, the accent over the track) in its box's padding, the weekday letters
  over them (Caption, tertiary, today's column primary), every week of the window.
- **[S-3332]** *(Retired by [S-11505]: no ladder; the figure and its line are the reading atom's,
  [S-11500].)*
- **[S-11505]** **The chart is the drawing alone** (owner decision 2026-11-12, [S-2438]): its first line
  the day line naming the window where it has one (a tool line, [S-454], [S-12481]), 200..520 wide (a new one 300), one
  height per style (`ScreenTimeChartLayout.Sizing`): Bars 12 + 72 of bars + their labels' row (4 + 16) + 12 = 116;
  Calendar its letters' row (20) over its weeks of 30 cells from the card's top edge (a week 50, a month's five weeks
  170). A wider card draws wider bars or more room right of the calendar; nothing appears or goes with the size.
  The bars are a graph's plot, the card's whole box (owner, 2026-11-19: "Plot edge to edge", [S-6754]): the view owns
  the card's top edge, the first day's column stands on the card's left edge and the last on its right, the bars rise
  from the card's foot to its top (under the day line where a day has one), the end labels standing over the bars' feet
  at the inset; the caption keeps the inset over it.
  The total over it is the reading atom's ([S-11500]); the ready-made group "Screen time" stands that
  reading over it ([S-11520]). A window with no use, or no recording at all, is the chart all the same: its days' bars
  as empty tracks, why as a caption ([S-6753], [S-15774], [S-15788]).
- **[S-15772]** **The days are items of one fixed size, touching from the card's edges** (docs/WIDGET-RULES.md WR-1..WR-6):
  the bars' day columns touch from the card's left edge to its right and from its top to its foot (a plot: they share
  the width), each bar standing in its column's padding (4 each side for a week, 2 for a month; the 8 or 4 between two
  bars is that padding); the calendar's weeks are rows of one height at every size of the card
  (`ScreenTimeChartLayout.CellBox`: the ring's 20 and 8 in whole grid cells, 30 on the default grid) whose rings keep one
  size (`Ring`, the box less 8), touching from the card's top edge, the weekday letters' row first (`HeadRow`, a Caption
  line in whole cells, each letter its column's head, `HeadMark`), every week under it, the last the card reaches cut by
  its foot; a week is seven days, so its seven columns share the card's width from its left edge to its right (WR-3,
  `ItemSize.Columns`), each ring centred in its cell. Before, the rings grew with the card (20 to 78) and stood centred in
  the inset. Each cell and each day column lights under the pointer (the item layer's row fill, `PointerPreview`). Tests
  `ScreenTimeChartTests.S11505_S15772_`, `S3331_S15772_`.
- **[S-15773]** **The part pointed at is one app on one day** (WR-13: "Hover lights exactly the item under the pointer";
  "never several items that share a sender, organiser or kind"): the pointer on an app's part of a day's bar lights that
  part alone (every other part at its tone times 0.6; a click picks that part, the others at 0.3,
  `ScreenTimeChartLayout.PartStrength` over a `ChartPart`), never that app's parts on every day; the app is still the
  view's hover and selection for the other screen-time widgets (the donut's slice, the list's row: one item there). An
  app pointed at or picked elsewhere lights a part here only with a day pointed at too (that app on that day); the
  calendar's rings still show the focused app's seconds per day ([S-3333]). Tests `ScreenTimeChartTests.S15773_`.
- **[S-15774]** **No recording keeps the chart** (WR-10: "the widget keeps its face ... and says why in a compact line"):
  with no tracker the chart draws the window's days with nothing in them (`NoRecording`, read by the same rules) and the
  "recorded while Themia runs" sentence as its caption, never the Status block's row alone; the caption stands over the
  bars' empty plot at the inset, or on the calendar's foot. Preview states `empty` (a recording with nothing in the window)
  and `off`.
- **[S-15788]** **Bars touch; an empty window keeps its bars** (WR-2: "the items have no spacing between them"; WR-3; WR-10):
  every day's bar is its column's whole width, the bars touching from the card's left edge to its right, no padding
  between them (8 or 4 had stood between them, 4 at each side of the card); with nothing in the window every day keeps
  its bar as an empty track the plot's full height, in the track's role, the caption over it saying why.
- **[S-15799]** **The bars' feet are square; their least is the labels over a plot** (WR-2, WR-14): a bar's foot stands
  square on the card's foot and an empty track is square, so touching bars leave no notch of the card between them (the
  rounded feet had left a 4 DIP notch at every seam); the top part keeps the family's radius at its top. The bars go down
  to the inset, their end labels' row and the inset in whole cells (50; 116 before), the plot what is left under the
  labels. Tests `ScreenTimeChartTests.S15799_`.
- **[S-3333]** Linked focus ([S-3310]) in the links' one look ([S-11902]): the part pointed at or picked (one app on one
  day, [S-15773]) whole and every other part at its tone times 0.6 (pointed) or 0.3 (picked); the calendar's rings show the focused app's seconds per day against its
  busiest (the reading atom says that app's time, [S-11503]). A day pointed at (the pointer over its bar
  or ring, or Left / Right) keeps every other day at 0.6.
  Hovering a part hovers its app; clicking it selects (again: lets go); clicking empty space clears
  the selection.
- **[S-3334]** Keys: Up/Down, PgUp/PgDn, Home/End step through the apps in the ring's order (the
  linked cursor, [S-3307]); Enter/Space select; Left / Right step the day lit; Menu hides; Escape lets
  the cursor and the day go.

Needs a manual pass: the widget on a live desktop ([S-3331], [S-3333], [S-3334]: a real week and month
of use as bars and as a calendar, an app lit with the donut and the list, the keys through the lease),
never in a render or a test, which track nothing.
- View links: its pointed day is the view's day under the pointer, and another widget's day lights its bar or ring ([S-11924]).

## Screen time: apps

The module `screenTimeApps` ([S-6911]): the apps of the screen-time history, busiest first. Its tags
are this section's; the root `SPEC.md` holds the host's sections and what the three screen-time
widgets share, with their root tags: the one tracker and its file ([S-450], [S-454], [S-458],
[S-459], the host's `ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu,
`SettingsHost.ScreenTime`), the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`),
the period ([S-3321]), the ceiling and the Other row ([S-1211], [S-3351], the SDK's
`ScreenTimeRules`), the linked focus ([S-3310]), the icons ([S-3960], [S-3965]) and the sample
history the previews draw ([S-3355], the SDK's `SampleScreenTime`).

Owning files: `ScreenTimeAppsModule.cs` (descriptor, view factory, menu
lines), `ScreenTimeAppsConfig.cs` (the period, the ceiling, the figures),
`ScreenTimeAppsSpec.cs` (its kit spec, a segment as a kit item), `ScreenTimeAppsView.cs` (a kit list, [S-7074], under
the day line: the binding over the tracker, the app's icon through the installed `AppIconSource` as the rich look's
lead, the links' one look on the rows the kit drew; the kit's keys with the day steps and the pick its own; the
row menu the kit's (Hide); nothing recorded or no activity the kit's state row), `ScreenTimeAppsLayout.cs` (the rows as its config says), `ScreenTimeAppsMenu.cs` (its
properties menu, ending with the shared lines), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (the family's `ScreenTimeStr`). Tests: `Tests/ScreenTimeAppsTests.cs`,
`Tests/ScreenTimeAppsMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3350]** `ScreenTimeAppsConfig`: `Period`, `Rows` and `Lines` ([S-11510]), `ShowAverage` (on),
  `ShowPercent` (off). A row is the family's Row block ([S-6711], G6 2026-10-29): the app's icon as
  its lead (16 on a one-line row, 32 on a two-line one, [S-6704]; while it loads and when it has none,
  a 16 dot in the app's tone, the accent at its rank's step, [S-2272]: the initial chip went), the name
  (Body, primary), the time as the value (Body, secondary; Critical over its own limit, and the tooltip
  says the limit). The share and, over a window, the per-day average (as far as they are on) are the
  row's second line ("38% · 2h 10m/day", Caption secondary) where its Lines are two; else they are in
  the tooltip only.
- **[S-3356]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Day, Week, Month, [S-3321]), under a divider
  the list's own (user request 2026-10-19): **Rows** and, where a row has figures for a second line,
  **Lines** ([S-11510], `KitMenuLines.Rows`), **Daily average** (over a window
  only, [S-6074]: a day's average is its time, so the figure would repeat it), **Share of total**, then the shared lines ([S-3323]: of the limits each app's).
- **[S-3352]** *(Retired by [S-11510]: no ladder; what it shows is its config's.)*
- **[S-11510]** **The list states its rows** (owner decision 2026-11-12, [S-2438]; `MaxApps` became
  Rows): `Rows` 1..20 (a new one 5) and `Lines` 1 or 2 (a new one 1) on the kit's rows contract
  (`IKitRows`, [S-2440]): its height is 12 + its day line (24, [S-12481]) + its rows (28, or 40 where its Lines are two and a row has
  figures for the second: `ScreenTimeAppsConfig.RowsShown`) + 12, 220..520 wide (a new one 300 × 188),
  Rows decide its natural height; the card shows the rows it holds at their optimal, stretched to fill it, and a resize stores none ([S-14500], [S-14502]). Every row has its icon, its name and its
  time at every width; the busiest apps fill the rows, the last one Other when apps are left over
  ([S-3351]); fewer apps leave the rows under them empty. No headline, no footer ([S-666]): the total
  is the reading's. Nothing to list (not recording, no activity) is the Status block's one row at the
  top ([S-6714]). Stored rows or lines out of range are repaired to the nearest.
- **[S-3353]** Linked focus in the links' one look ([S-11901]): the row of the app pointed at has the item
  layer's hover fill, the row of the app picked the selected fill and the accent pill at its start;
  nothing else in the list changes (no row dims, [S-6705]); the keyboard cursor has the Row block's ring
  ([S-1288]). Hover a row hovers the app; click selects (again: lets go); the space under
  the rows clears. A past day pinned on its day line, its first line ([S-454], [S-12481]), goes back to today from the card menu.
- **[S-3354]** Keys: Up/Down, PgUp/PgDn, Home/End move the cursor down the list; Enter/Space select;
  Left / Right step the day (a day period); Menu hides; Escape lets the cursor go and keeps the
  selection (Escape also arrives when the lease ends).

Needs a manual pass: the widget on a live desktop ([S-3350], [S-3353], [S-3354]: real apps tracked and
listed with their icons, a limit gone over, the rows lit with the chart and the donut, the keys through
the lease), never in a render or a test, which track nothing.
- View links: with the period Day it shows the day selected on the view, as far as its arrows reach ([S-11928]).

## Screen time: donut

The module `screenTimeDonut` ([S-6911]): the screen-time history as one ring. Its tags are this section's; the root `SPEC.md` holds the host's sections and what the three screen-time widgets share, with
their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the host's
`ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu, `SettingsHost.ScreenTime`),
the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`), the period ([S-3321]), the
wedges and durations ([S-452]), the ring's figure ([S-1210]), the linked focus ([S-3310], the SDK's
`ScreenTimeRules.FocusPart`) and the sample history the previews draw ([S-3355], the SDK's
`SampleScreenTime`).

Owning files: `ScreenTimeDonutModule.cs` (descriptor, view factory, menu
lines), `ScreenTimeDonutConfig.cs` (the period, the Size), `ScreenTimeDonutView.cs` (the ring through
its host, the keys), `ScreenTimeDonutLayout.cs` (its sizes), `ScreenTimeDonutMenu.cs` (its properties
menu, ending with the shared lines), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (the family's `ScreenTimeStr`). Tests:
`Tests/ScreenTimeDonutTests.cs`, `Tests/ScreenTimeDonutMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3340]** `ScreenTimeDonutConfig`: `Period` ([S-11515]: no Size). One ring, the total in its hole.
- **[S-3345]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Day, Week, Month, [S-3321]), then the shared lines
  ([S-3323]: of the limits the day's).
- **[S-3341]** The wedges: the busiest seven apps and one Other, from cumulative seconds ([S-452]),
  each in its app's tone: the accent's ramp by rank, Other tertiary at the wash ([S-2272]).
  No legend: the list is its own widget ([S-666]); a wedge's tooltip names it.
- **[S-3342]** *(Retired by [S-11515]; since 2026-11-20 the ring is as large as the card holds.)*
- **[S-11515]** **A ring as large as its card** (owner decision 2026-11-12, [S-2438]; 2026-11-20: "the size should
  not be settable, it is set by the size of the widget", the Size line, [S-2449], is gone): a new one's ring is 168,
  the least 128 (its figure on 28 with its line still fits the hole), the card round it 12 at the sides and the foot,
  12 and its day line (24, a tool line, [S-454], [S-12481]) over it (192 × 216 natural, 152 × 176 least,
  `ScreenTimeDonutLayout.Sizing`); a larger card draws a larger ring: the day line over the ring, the figure in its
  hole, is one block planned at the natural ring and scaled as one to the card ([S-14284], WR-7, `ReadingFit`;
  `ScreenTimeDonutLayout.Plan`), its track and its figure growing with it. Tests `ScreenTimeDonutTests.S11515_`,
  `S14284_`. Its wedges stand in the family's track (4 in the plan,
  [S-6704]); the hole's figure is [S-1210]'s on 28, centred, its
  line under (the day's "of 4h", a window's "2h 10m/day", or the focused app's share) where it fits,
  cut to the hole's width. Nothing to draw keeps the ring ([S-12813]): its track alone, and in the hole "--" while nothing records or none spent ("0m") in a period that recorded, disabled, why under it where the hole holds it and always as the ring's tooltip.
- **[S-14284]** **The day line, the ring and its figure are one block** (owner, 2026-11-19, docs/WIDGET-RULES.md WR-7:
  "A reading and its caption stay together and scale up"; "All together, scaled"; as the reading's [S-14282]). The day
  and its arrows stand right over the ring, as wide as it; the block is planned once, at the natural ring (168, its
  figure on 28 in the hole, `ScreenTimeDonutLayout.Plan`), and scaled as one to the card (`ReadingFit`, [S-14280]): a
  larger card draws a larger ring with a larger figure and day line, the ring's track (4 in the plan) and the figure
  growing with it; the day line states its ink (`ToolLine` as `IInked`: its words' capitals and baseline), so the
  block's first ink is its words, not its buttons' boxes. Before, the ring was sized to the card on its own (`RingIn`), the day line stood across the card's
  top corners and the figure stayed on 28 or 40 in a ring of any size. Tests `ScreenTimeDonutTests.S14284_`.
- **[S-3343]** Linked focus in the links' one look ([S-11902]): the wedge pointed at or picked is whole,
  every other at its tone times 0.6 (pointed) or 0.3 (picked) (the lit wedge's 1.3 times thicker stroke
  went: one track), and the hole names it ("Code · 41%"). An app
  folded into Other lights Other (`ScreenTimeRules.FocusPart`); a hidden one lights nothing. Click
  selects, empty space clears. A past day pinned on its day line ([S-454]) goes back to today from the
  card menu.
- **[S-3344]** Keys: Up/Down, PgUp/PgDn, Home/End step through the wedges clockwise from twelve;
  Enter/Space select; Left / Right step the day (a day period); Menu hides; Escape lets the cursor go.

Needs a manual pass: the widget on a live desktop ([S-3340], [S-3341], [S-3343], [S-3344]: real apps
tracked as wedges, a wedge lit with the chart and the list, the keys through the lease), never in a
render or a test, which track nothing.
- View links: with the period Day it shows the day selected on the view, as far as its arrows reach ([S-11928]).
