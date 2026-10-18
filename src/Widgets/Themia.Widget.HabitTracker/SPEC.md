# Habit tracker

The module `habitTracker` ([S-6911]): one of the habit lists kept on this PC. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the lists share, with their root tags:
the device-wide lists and `habits.json` ([S-1420], the host's `HabitPool` behind `IWidgetHost.Habits`
and, for the settings page, `SettingsHost.Habits`), ticking ([S-1421]), the reset periods and their
keys ([S-1422]), the streak and best run ([S-1423], [S-1424]) and the history dots ([S-1426]); the
rules are the SDK's `HabitLists`, `HabitRules` and `HabitPeriods`, the file's store Core's
`HabitListStore`. The add footer is the SDK's `AddFooterRow` ([S-3850]), shared with the task list.

Owning files: everything in this folder: `HabitTrackerModule.cs` (descriptor, glyph, view factory,
settings rows, JSON context), `HabitTrackerConfig.cs` (the list shown, its styles, period and
history), `HabitTrackerView.cs` (the list through its host) with `.Rows.cs` (the rows, the editor, the
keys), `.Drag.cs` (reordering) and `.Heatmap.cs` (the heatmap style), `HabitLayout.cs` (the ladder and
the key map), `HabitDrag.cs`, `HabitHeatmap.cs`, `HabitTrackerSettings.cs` (its page: the lists, the
period, the data folder through `SettingsHost.Launcher`), `Preview/` (Debug renders:
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
  The settings page holds no habit editor (original S-23).
- **[S-1427]** Ladder (`HabitLayout.Plan`): XS box + name, two rows → S + run (number) → M + dots
  and the add footer ([S-3850], pinned at the bottom; always under an empty list) → Wide + run in words and a 7-period rate per row → L all of that → XL + best run
  per row. Rows are 40px (32 on a Compact side); the viewport holds whole rows and the rest scroll.
  The height a short list leaves stays empty: no summary strip or line ("3 of 5 done today · 71%",
  bar) and no completion chart fills it (user decision, concise widgets [S-666]; both retired).
- **[S-1428]** Keyboard (original S-17): a click in the widget takes the shell's keyboard lease
  ([S-809]) and a clicked row takes the focus:
  Up/Down clamp, Home/End, Tab/Shift+Tab wrap, Enter ticks, F2 renames, Delete deletes, Escape gives
  the keyboard back. In an editor Tab commits and opens the next row's editor, falling into the add
  row past the last. Not verified live: needs a manual pass on the desktop.
- **[S-1982]** Space ticks the focused habit as Enter does, and Page Up / Page Down jump ten rows
  (`HabitLayout.Decide`, clamped; from no row, the first).
- **[S-1429]** Settings: the list picker/editor (pick, rename as typed, habit count, add, delete
  — always confirmed, empty or not, original S-24), Resets, Interval (custom only), Show streaks,
  History (both for the Dots style only, [S-6408]). Renaming a list retitles every widget showing it.
- **[S-6408]** Habits' styles ([S-6000]), `HabitTrackerConfig.Style` (`HabitTrackerStyle`,
  `"style": "dots"`): **Dots** (the rows of [S-1427]), **Heatmap** and **Checklist**, in that
  order, over the same list, log and cadence. Show streaks and History are the Dots style's own
  (`HabitTrackerConfig.ShownSettings`, [S-6070]); the page offers them for it alone and keeps their values.
- **[S-6409]** Heatmap (`HabitHeatmap`): GitHub's calendar over the log, columns of seven periods,
  the newest on the right. Under a daily cadence the rows are the weekdays from the language's first
  day, so a column is a calendar week and the days still to come this week are blank; any other
  cadence fills its last column to the current period. Decided by size: when every habit's name
  (Body) and its own seven-row grid of at least 4 weeks of 6-DIP cells fit, each habit gets one (a
  done period in the accent, the current period's cell ticking it as its box does); otherwise the
  list shares one grid, each cell shaded in four steps by the share of habits done (rounded up) and
  its tooltip naming the period and "3 of 5". Room goes to bigger cells (2-DIP gaps, at most 20), then
  more weeks (at most 53). No add footer beside it (except under an empty list) and no row keys.
- **[S-6410]** Checklist: the current period only, each habit's box and name (`HabitLayoutInput.Checklist`):
  no run, dots, rate or best run at any size; the rows, the add footer and the keys are the Dots
  style's ([S-1427], [S-1428]).
- **[S-1430]** Settings end with "Data folder" (issue #154): **Open in Explorer** opens the folder
  holding `habits.json` (`IHabitLists.FilePath`, through `SettingsHost.Launcher.OpenContainingFolder`, so it is
  classified like every other launch), for a backup or another program. Off when the pool has no
  file (a debug render keeps it in memory).
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
drag, the keys through the lease, the settings page's lists and its data folder), never in a render or
a test, which write no habits.json.
