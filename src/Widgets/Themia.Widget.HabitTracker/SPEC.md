# Habit tracker

The module `habitTracker` ([S-6911]): one of the habit lists kept on this PC. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the lists share, with their root tags:
the device-wide lists and `habits.json` ([S-1420], the host's `HabitPool` behind `IWidgetHost.Habits`
and, for the properties menu, `SettingsHost.Habits`), ticking ([S-1421]), the reset periods and their
keys ([S-1422]), the streak and best run ([S-1423], [S-1424]) and the history dots ([S-1426]); the
rules are the SDK's `HabitLists`, `HabitRules` and `HabitPeriods`, the file's store Core's
`HabitListStore`. The add footer is the SDK's `AddFooterRow` ([S-3850]), shared with the task list.

Owning files: everything in this folder: `HabitTrackerModule.cs` (descriptor, glyph, view factory,
menu lines, JSON context), `HabitTrackerConfig.cs` (the list shown, its styles, period and
history), `HabitTrackerView.cs` (the list through its host) with `.Rows.cs` (the rows, the editor, the
keys), `.Drag.cs` (reordering) and `.Heatmap.cs` (the heatmap style), `HabitLayout.cs` (the rows' plan and
the key map), `HabitSizing.cs` (each style's sizes), `HabitDrag.cs`, `HabitHeatmap.cs`, `HabitTrackerMenu.cs` (its properties-menu lines: the
lists, the period, the data folder through `SettingsHost.Launcher`), `Preview/` (Debug renders:
`--state sample|habits|few|empty`, over lists held in memory, `PreviewHabitLists`),
`Localization/Locales/` (its own `HabitTrackerStr`, 30 languages, its style names included). Tests:
`Tests/` (run by `tests/Themia.Modules.Tests`); its config and ladder under the root's rules are the
module host's `HabitHostTests` and `SettingRulesTests`.

- **[S-1425]** Habits are added, renamed, moved and deleted **in the widget** (original S-13/14/16/22/23):
  an "Add a habit" row at the foot (always shown for an empty list; Enter keeps it open for the
  next habit); rename by double-clicking the name, F2 or the row menu (Enter/leaving commits, Escape
  cancels, blank or unchanged commits nothing, the id and log never change); the row menu (shared
  `ItemMenu`) has Done/Not done, Rename, Delete (danger,
  no dialog). A habit is moved by dragging ([S-1621]) or with Alt+Shift+Up / Down on the focused row
  ([S-3802]); no menu has Move up / Move down.
  The properties menu holds no habit editor (original S-23).
- **[S-1427]** A habit is the family's Row block ([S-6711], G6 2026-10-29): its check the lead
  ([S-1421]), the name primary, the history dots its inline meter ([S-1426]), the current run its
  value (Body, secondary), the rate ("Done 5 of the last 7 days", Caption secondary) and, with Show
  streaks, the best run ("Best run: 16 days") its second line where its Lines are two ([S-11531]; the
  size ladder that gave them by tier, and the third line, are retired). The rate
  and the best run are lines, never columns. No summary strip or line ("3 of 5 done today · 71%",
  bar) and no completion chart (user decision, concise widgets [S-666]; both retired). The header's count ("3/5") is the neutral pill ([S-6706]); with no
  habit to draw the Status row at the top says why ([S-6714]): "Habits are not available." (no
  pool) and "No habits yet." (over the add footer) plainly, a deleted list or lists that could not
  be read as a problem. A focused row wears the Row block's ring ([S-1288]).
- **[S-1428]** Keyboard (original S-17): a click in the widget takes the shell's keyboard lease
  ([S-809]) and a clicked row takes the focus:
  Up/Down clamp, Home/End, Tab/Shift+Tab wrap, Enter ticks, F2 renames, Delete deletes, Escape gives
  the keyboard back. In an editor Tab commits and opens the next row's editor, falling into the add
  row past the last. Not verified live: needs a manual pass on the desktop.
- **[S-1982]** Space ticks the focused habit as Enter does, and Page Up / Page Down jump ten rows
  (`HabitLayout.Decide`, clamped; from no row, the first).
- **[S-1429]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style, for the Dots style only ([S-6408]): **Show streaks** (a switch)
  and **History** (typed, 0–31 periods, 0 hides the strip, [S-3726]). Then Appearance ([S-3723]),
  **Habit list** (the list shown at the end of its line; its submenu: the lists on this PC as a
  choice of one, each with its habit count, the pool's problem or "no lists" as a note; then
  Rename… and Delete this list for the list shown, and Add a list, which creates one and shows it;
  deleting is always confirmed, empty or not, original S-24, "Delete this list?" with what goes under it ([S-3727]); all
  greyed while the pool is read-only; without a pool a note says the lists are not available),
  under a divider **Resets** (Every hour / day / week / month, Custom interval) and **Interval**
  (custom only, typed and read by `HabitCadence.TryParseMinutes`, its reason under the box), and
  under another the data folder ([S-1430]): the list, its period and its file stand apart (user
  request 2026-10-19). Renaming a list retitles
  every widget showing it. (Tests `HabitTrackerMenuTests`.)
- **[S-6408]** Habits' styles ([S-6000]), `HabitTrackerConfig.Style` (`HabitTrackerStyle`,
  `"style": "dots"`): **Dots** (the rows of [S-1427]), **Heatmap** and **Checklist**, in that
  order, over the same list, log and cadence. Show streaks and History are the Dots style's own
  (`HabitTrackerConfig.ShownSettings`, [S-6070]); the menu offers them for it alone and keeps their values.
- **[S-6409]** Heatmap (`HabitHeatmap`): GitHub's calendar over the log, columns of seven periods,
  the newest on the right, from the view's top inside the card's inset. Under a daily cadence the
  rows are the weekdays from the language's first day, so a column is a calendar week and the days
  still to come this week are blank; any other cadence fills its last column to the current period.
  One grid for the list (its config's, [S-11532]): each cell a column of the Chart block ([S-6717]),
  the track filled from its foot in the accent to the share of habits done (`HabitHeatmap.Fill`), its
  tooltip naming the period and "3 of 5"; never an opacity per share (the four shades went, G6). Or,
  with A grid per habit, each habit's name (Body, primary, 4 over its grid; grids 12 apart) over its
  own grid (a done period the accent, an open one the track, the current period's cell ticking it as
  its check does). A cell's corners are the family's 4, round below 8 (`HabitHeatmap.Radius`); cells
  2 apart; a wider card holds more weeks (at most 53). No add footer beside it (except under an empty
  list) and no row keys.
- **[S-6410]** Checklist: the current period only, each habit's check and name (`HabitLayoutInput.Checklist`):
  rows of one line, no run, dots, rate or best run; the add footer and the keys are the Dots style's
  ([S-1427], [S-1428]).
- **[S-11530]** **The tracker states its sizes** (owner decision 2026-11-12, [S-2438]): **Rows**, the
  habits shown (1..12, a new one 4; more scroll inside them), on its menu's first level; in the Dots
  and Checklist styles its height is 36 + the rows (28, or 40 with two lines) + the add footer (28,
  always there where a habit can be added, [S-3850]) + 12, stepped by a row, dragging the bottom edge
  sets Rows (`HabitSizing.SizeSets`); fewer habits leave their rows empty. 300 wide new, at most 520.
  No ladder, no tiers: what a row shows is the config's at every size (`HabitLayout.Plan`).
- **[S-11531]** **The Dots style's settings**, under Style: Show streaks (the run, a number), **Run in
  words** (off by default: "12 days" rather than "12", while the run is shown), History (the dots, 0..31,
  all drawn at every width) and **Lines** (1 or 2: the second the rate, with the streaks "· Best run: 16
  days" after it, [S-6712]). The card's least width holds the check, an 80 name, the dots and the run
  as asked (`HabitSizing.DotsWidth`, at least 220); the name takes the rest and ends in an ellipsis. A
  checklist row is one line at 220..520.
- **[S-11532]** **The heatmap's sizes**: one grid for the list (cells of 12) is one height, 36 + its
  seven rows + 12 = 144, 200..520 wide; **A grid per habit** (off by default, under Style) draws the
  first Rows habits each over its own grid of 8 cells, the height stepped by a habit (its name line,
  4, its grid, 12 between). A wider card is a longer calendar, never bigger cells.
- **[S-1430]** The menu ends with "Data folder" (issue #154), a submenu of a note (where the lists
  are kept) and **Open in Explorer**, which opens the folder
  holding `habits.json` (`IHabitLists.FilePath`, through `SettingsHost.Launcher.OpenContainingFolder`, so it is
  classified like every other launch), for a backup or another program. Not there when the lists
  have no file (no pool, or a debug render's kept in memory): there is no folder to open ([S-6070]).
- **[S-1621]** Habits are **reordered by dragging a row** in the widget (original S-13), beside the
  keyboard's Alt+Shift+Up / Down ([S-3802]). A press arms nothing until the pointer has moved 5 px
  (`HabitDrag.Threshold`), so a click still focuses and a double-click still renames; a press on the
  tick box or in the rename box never arms. The lifted row follows the pointer (clamped to the list),
  the rows it passes step one pitch aside (`HabitDrag.Shift`), the landing slot is the one the row is
  more than half over (`HabitDrag.Target`), and the list scrolls near its edges. Release commits one
  `MoveHabit` through the pool (a drag that comes home writes nothing); capture lost, a rebuild under
  the drag or Detach puts the rows back. Rows are only translated while captured, never reparented.
  In edit mode the card takes the press and the widget moves instead.

Needs a manual pass: the widget on a live desktop ([S-1425], [S-1428], [S-1429], [S-1430], [S-1621]:
a tick seen in a second widget of the same list, adding, renaming and deleting a habit in place, a
drag, the keys through the lease, the properties menu's lists and its data folder), never in a render or
a test, which write no habits.json.
