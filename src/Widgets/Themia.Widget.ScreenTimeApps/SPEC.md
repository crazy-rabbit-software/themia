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
  `ShowPercent` (off). A row: icon (or initial chip in the app's colour), name, the average and the
  share when shown, the time last; over its own limit the time is critical, and the tooltip says the limit.
- **[S-3356]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Day, Week, Month, [S-3321]), under a divider
  the list's own (user request 2026-10-19): **Most apps listed** (typed, 0 to 50; 0 reads "As many as fit", [S-1211]), **Daily average** (over a window
  only, [S-6074]: a day's average is its time, so the figure would repeat it), **Share of total**, then the shared lines ([S-3323]: of the limits each app's).
- **[S-3352]** Ladder (`ScreenTimeAppsLayout.Apps`): whole compact rows; the time always, the name from
  160 DIPs wide (under it a row is the icon and the time, the name in its tooltip: a squeezed name
  read as one letter), the share from 200, a window's average from 300. No headline, no footer ([S-666]): the total is the donut's and the chart's.
- **[S-3353]** Linked focus: the focused row has the hover fill, the others dim; the selected row
  keeps Fluent's selection pill and fill; the keyboard cursor has the focus ring ([S-1288]). Hover a
  row hovers the app; click selects (again: lets go); the space under the rows clears.
- **[S-3354]** Keys: Up/Down, PgUp/PgDn, Home/End move the cursor down the list; Enter/Space select;
  Left / Right step the day (a day period); Menu hides; Escape lets the cursor go and keeps the
  selection (Escape also arrives when the lease ends).

Needs a manual pass: the widget on a live desktop ([S-3350], [S-3353], [S-3354]: real apps tracked and
listed with their icons, a limit gone over, the rows lit with the chart and the donut, the keys through
the lease), never in a render or a test, which track nothing.
