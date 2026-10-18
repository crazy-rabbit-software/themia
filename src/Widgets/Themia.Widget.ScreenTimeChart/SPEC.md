# Screen time: chart

The module `screenTimeChart` ([S-6911]): the screen-time history over a week or a month. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what the three screen-time widgets
share, with their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the
host's `ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the settings page,
`SettingsHost.ScreenTime`), the shared hidden apps and limits ([S-3322], `ScreenTimeSettingsRows`), the
period and the chart's own period and style rules ([S-3321], [S-3330], the SDK's `ScreenTimeRules`), the
calendar ([S-456]), the linked focus ([S-3310]) and the sample history the previews draw ([S-3355], the
SDK's `SampleScreenTime`).

Owning files: everything in this folder: `ScreenTimeChartModule.cs` (descriptor, view factory, settings
rows, JSON context), `ScreenTimeChartConfig.cs` (the period and the style), `ScreenTimeChartView.cs`
(the bars and the calendar through its host, the keys), `ScreenTimeChartLayout.cs` (the ladder),
`ScreenTimeChartSettings.cs` (its page, ending with the shared rows), `Preview/` (Debug renders:
`--state sample|hovered|selected|packaged`), `Localization/Locales/` (its own `ScreenTimeChartStr`, 30
languages). Tests: `Tests/ScreenTimeChartTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3331]** Bars: one bar per day, oldest left, stacked by app in the ring's order (the busiest
  seven, `ScreenTimeRules.DrawingApps`, then Other), scaled to the busiest day, today's weekday in the
  accent. Heatmap: [S-456]'s calendar.
- **[S-3332]** Ladder (`ScreenTimeChartLayout.Chart`): the drawing (never dropped, at least 32), then the
  figure over it (Subtitle from Expanded height at Regular width, else BodyStrong), the weekday row
  under a week's bars (when each column has 16 DIPs), the detail line; the drawing takes the rest.
  The figure is the window's total and the line its per-day average and the "of 4h/day" limit
  ([S-457]); a hovered day (pointer, or Left / Right) shows that day.
- **[S-3333]** Linked focus ([S-3310]): the focused app's part of every bar is lit and every other
  part dimmed; the calendar shades by that app's seconds per day; the figure is that app's time over
  the window (or on the hovered day) with its share. Hovering a part hovers its app; clicking it
  selects (again: lets go); clicking empty space clears the selection.
- **[S-3334]** Keys: Up/Down, PgUp/PgDn, Home/End step through the apps in the ring's order (the
  linked cursor, [S-3307]); Enter/Space select; Left / Right step the day the figure speaks of; Menu
  hides; Escape lets the cursor and the day go.

Needs a manual pass: the widget on a live desktop ([S-3331], [S-3333], [S-3334]: a real week and month
of use as bars and as a calendar, an app lit with the donut and the list, the keys through the lease),
never in a render or a test, which track nothing.
