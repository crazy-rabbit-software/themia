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
the installed `AppIconSource`), `ScreenTimeAppsLayout.cs` (the rows as its config says), `ScreenTimeAppsMenu.cs` (its
properties menu, ending with the shared lines), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (its own `ScreenTimeAppsStr`, 30 languages). Tests: `Tests/ScreenTimeAppsTests.cs`,
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
  (`IKitRows`, [S-2440]): its height is 36 + its rows (28, or 40 where its Lines are two and a row has
  figures for the second: `ScreenTimeAppsConfig.RowsShown`) + 12, 220..520 wide (a new one 300 × 188),
  dragging the bottom edge sets Rows (`KitSizing.SizeSets`). Every row has its icon, its name and its
  time at every width; the busiest apps fill the rows, the last one Other when apps are left over
  ([S-3351]); fewer apps leave the rows under them empty. No headline, no footer ([S-666]): the total
  is the reading's. Nothing to list (not recording, no activity) is the Status block's one row at the
  top ([S-6714]). Stored rows or lines out of range are repaired to the nearest.
- **[S-3353]** Linked focus in the links' one look ([S-11901]): the row of the app pointed at has the item
  layer's hover fill, the row of the app picked the selected fill and the accent pill at its start;
  nothing else in the list changes (no row dims, [S-6705]); the keyboard cursor has the Row block's ring
  ([S-1288]). Hover a row hovers the app; click selects (again: lets go); the space under
  the rows clears. A past day pinned in the header ([S-454]) goes back to today from the card menu.
- **[S-3354]** Keys: Up/Down, PgUp/PgDn, Home/End move the cursor down the list; Enter/Space select;
  Left / Right step the day (a day period); Menu hides; Escape lets the cursor go and keeps the
  selection (Escape also arrives when the lease ends).

Needs a manual pass: the widget on a live desktop ([S-3350], [S-3353], [S-3354]: real apps tracked and
listed with their icons, a limit gone over, the rows lit with the chart and the donut, the keys through
the lease), never in a render or a test, which track nothing.
