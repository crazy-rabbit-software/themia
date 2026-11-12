# Screen time (the reading)

The module `screenTime` ([S-6911]): the screen-time total of a day or a rolling week or month, as a
reading. One of the four screen-time atoms (owner decision 2026-11-12, [S-2438]): this reading, the
chart (`screenTimeChart`), the app list (`screenTimeApps`) and the ring (`screenTimeDonut`), all over the
one tracker and its shared preferences, joined by the view's links; the ready-made group "Screen time"
is the reading over the chart over the apps ([S-11520]). Its tags are [S-11500]..[S-11504]; the root
`SPEC.md` holds what the screen-time widgets share ([S-450], [S-454], [S-457], [S-458], [S-459],
[S-3310], [S-3321]..[S-3325], [S-3355]).

Owning files: everything in this folder: `ScreenTimeModule.cs` (descriptor, menu lines, JSON context),
`ScreenTimeConfig.cs` (the period), `ScreenTimeReading.cs` (its size and its words, pure),
`ScreenTimeView.cs` (the readout through its host), `Preview/` (Debug renders: `--state
sample|hovered|selected|over|packaged`), `Localization/Locales/` (its own `ScreenTimeStr`; its name is
the add menu's "Screen time" with its translations, "of {value}/day" moved from the chart with its
translations, "of {value}" the donut's). Tests: `Tests/ScreenTimeTests.cs`.

- **[S-11500]** **The reading** keeps its header (its name; the period as the header's label: "Last 7
  days", or a day with its arrows, [S-454]) and draws the period's total on the family's 28
  (`FamilyType.Big28`, start-aligned, [S-2449]) with one line under it: one height (36 + 36 + 16 + 12 =
  100 at the family's tokens, `ScreenTimeReading.Sizing`), 160..320 wide (the least holds "888h 59m"),
  a new one 200 × that. It is what the chart's figure was ([S-3332], retired): the chart draws the bars
  alone. Nothing to read (not recording, no use in the period) is the Status block's one row ([S-6714]).
- **[S-11501]** **Its words** (`ScreenTimeReading.Headline`): a day's total with "of 4h" under it where
  a daily limit is set; a window's total with its average per day and "of 6h/day" ("3h 21m/day · of
  6h/day"); over the limit (a window: its average) the figure is Critical ([S-457]). The line ends in
  an ellipsis on a narrow card.
- **[S-11502]** Its menu: **Period** (Day, Week, Month; a new one Week, as the chart it stands over in
  the group), then the shared lines ([S-3323]: of the limits the day's).
- **[S-11503]** **Links** ([S-3310]): it joins the view's `HoveredApp`/`SelectedApp` as the chart's figure
  did; a focused app stands in place of the total, its time over the period and its share under it
  ("Code · 41%"). It publishes nothing and takes no keys: it has no parts to point at.

Needs a manual pass: the reading on a live desktop beside the chart and the list (a real week, an app
lit in the chart shown here), never in a render or a test, which track nothing.
- View links: with the period Day it shows the day selected on the view, as far as its arrows reach ([S-11928]).
