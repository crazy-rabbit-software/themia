# Screen time: apps

The module `screenTimeApps` ([S-6911]): the apps of the screen-time history, busiest first. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections and what the three screen-time
widgets share, with their root tags: the one tracker and its file ([S-450], [S-454], [S-458],
[S-459], the host's `ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu,
`SettingsHost.ScreenTime`), the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`),
the period ([S-3321]), the ceiling and the Other row ([S-1211], [S-3351], the SDK's
`ScreenTimeRules`), the linked focus ([S-3310]), the icons ([S-3960], [S-3965]) and the sample
history the previews draw ([S-3355], the SDK's `SampleScreenTime`).

Owning files: everything in this folder: `ScreenTimeAppsModule.cs` (descriptor, view factory, menu
lines, JSON context), `ScreenTimeAppsConfig.cs` (the period, the ceiling, the figures),
`ScreenTimeAppsView.cs` (the rows through its host, the keys) with `.Row.cs` (a row, its icon through
the installed `AppIconSource`), `ScreenTimeAppsLayout.cs` (the ladder), `ScreenTimeAppsMenu.cs` (its
properties menu, ending with the shared lines), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (its own `ScreenTimeAppsStr`, 30 languages). Tests: `Tests/ScreenTimeAppsTests.cs`,
`Tests/ScreenTimeAppsMenuTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's ceiling is the module host's
`ScreenTimeHostTests`.

- **[S-3350]** `ScreenTimeAppsConfig`: `Period`, `MaxApps` ([S-1211]), `ShowAverage` (on),
  `ShowPercent` (off). A row is the family's Row block ([S-6711], G6 2026-10-29): the app's icon as
  its lead (32 from a width of 180, else 16, [S-6704]; while it loads and when it has none, a 16 dot
  in the app's tone, the accent at its rank's step, [S-2272]: the initial chip went), the name (Body, primary), the time as the
  value (Body, secondary; Critical over its own limit, and the tooltip says the limit). The share and,
  over a window, the per-day average (as far as they are on) are the row's second line ("38% · 2h
  10m/day", Caption secondary) where the tier gives rows two lines; else they are in the tooltip only.
- **[S-3356]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Day, Week, Month, [S-3321]), under a divider
  the list's own (user request 2026-10-19): **Most apps listed** (typed, 0 to 50; 0 reads "As many as fit", [S-1211]), **Daily average** (over a window
  only, [S-6074]: a day's average is its time, so the figure would repeat it), **Share of total**, then the shared lines ([S-3323]: of the limits each app's).
- **[S-3352]** Ladder (`ScreenTimeAppsLayout.Apps`): whole rows of 28 (40 with the figures' second
  line from a regular tile both ways, [S-6712]) in the view's area less the card's bottom inset; the
  time always, the name from 160 DIPs wide (under it a row is the icon and the time, the name in its
  tooltip: a squeezed name read as one letter), the 32 icon from 180. No headline, no footer ([S-666]):
  the total is the donut's and the chart's. Nothing to list (not recording, no activity) is the Status
  block's one row at the top ([S-6714]), not a centred empty state.
- **[S-3353]** Linked focus: the focused row has the item layer's hover fill and every other row's
  texts are tertiary (a row has no opacity of its own, [S-6705]); the selected row keeps the selected
  fill (Fluent's 3 DIP selection pill went: not a family shape); the keyboard cursor has the Row
  block's ring ([S-1288]). Hover a row hovers the app; click selects (again: lets go); the space under
  the rows clears. A past day pinned in the header ([S-454]) goes back to today from the card menu.
- **[S-3354]** Keys: Up/Down, PgUp/PgDn, Home/End move the cursor down the list; Enter/Space select;
  Left / Right step the day (a day period); Menu hides; Escape lets the cursor go and keeps the
  selection (Escape also arrives when the lease ends).

Needs a manual pass: the widget on a live desktop ([S-3350], [S-3353], [S-3354]: real apps tracked and
listed with their icons, a limit gone over, the rows lit with the chart and the donut, the keys through
the lease), never in a render or a test, which track nothing.
