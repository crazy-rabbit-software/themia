# Screen time: chart

The module `screenTimeChart` ([S-6911]): the screen-time history over a week or a month. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what the three screen-time widgets
share, with their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the
host's `ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu,
`SettingsHost.ScreenTime`), the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`), the
period and the chart's own period and style rules ([S-3321], [S-3330], the SDK's `ScreenTimeRules`), the
calendar ([S-456]), the linked focus ([S-3310]) and the sample history the previews draw ([S-3355], the
SDK's `SampleScreenTime`).

Owning files: everything in this folder: `ScreenTimeChartModule.cs` (descriptor, view factory, menu
lines, JSON context), `ScreenTimeChartConfig.cs` (the period and the style, `ScreenTimeChartStyle`), `ScreenTimeChartView.cs`
(the bars and the calendar through its host, the keys), `ScreenTimeChartLayout.cs` (the ladder),
`ScreenTimeChartMenu.cs` (its properties menu, ending with the shared lines), `Preview/` (Debug renders:
`--state sample|hovered|selected|packaged`), `Localization/Locales/` (its own `ScreenTimeChartStr`, 30
languages). Tests: `Tests/ScreenTimeChartTests.cs`, `Tests/ScreenTimeChartMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3335]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), **Period** (Week, Month: a day reads as the week,
  [S-3330]), then the shared lines ([S-3323]: of the limits the day's). Bars or Calendar is the
  menu's Style line ([S-3336]), not a line of its own.
- **[S-3336]** Bars and Calendar are the widget's **styles** ([S-6000]; what was its own Style choice
  is the style, never a second setting): `ScreenTimeChartStyle` (Bars the default, then Calendar),
  `ScreenTimeChartConfig.Style` over `IStyled`, stored as `"style": "bars" | "calendar"`
  (`StyleJsonConverter`; an unknown name is repaired to Bars at load, [S-6004]), copied in `Clone`,
  its labels `styles.screenTimeChart.*`. `--widget-png ScreenTimeChart … --style calendar` draws it.
- **[S-3331]** Bars: one bar per day, oldest left, 8 apart for a week and 4 for a month, stacked by
  app in the ring's order (the busiest seven, `ScreenTimeRules.DrawingApps`, then Other) in the
  accent's ramp by rank, Other and the apps past the fourth tertiary at the wash ([S-2272]), scaled to the busiest day, the stack one
  rectangle of the family's radius (its top part's top corners, its foot part's foot, [S-6704]); under
  them only the two ends are labelled ([S-6717]: Caption, tertiary): the first day and today, a week's
  by weekday ("Fri", "Thu"), a month's by date ("Sep 30", "Oct 29"); today's accent label and the
  weekday row under every bar went. Calendar ([S-456]): seven columns 4 apart, a cell a ring of its
  day's share of the busiest day (the Chart block's `ChartRing`, the accent over the track) from 16, a
  dot under it (the accent for any use, the track for none), at most 40; the weekday letters over them
  (Caption, tertiary, today's column primary) while the cells stay rings; every week while a cell keeps
  8, else the latest weeks that fit (`ScreenTimeChartLayout.Calendar`).
- **[S-3332]** Ladder (`ScreenTimeChartLayout.Chart`): the drawing (never dropped, at least 32), then the
  figure over it, the family's Readout ([S-6702]: 28 or 40 by the area, sized on "888h 59m" so it
  does not jump, 8 over the drawing), the bars' end labels (a 4 gap and a Caption line, inside the
  drawing), the line under the figure; the drawing takes the rest of the view's area less the card's
  bottom inset. The figure is the window's total (Critical while its per-day average is over the daily
  limit) and the line its per-day average and the "of 4h/day" limit ([S-457]); a hovered day
  (pointer, or Left / Right) shows that day, every other day at its wash ([S-3333]). Nothing to draw
  is the Status block's one row at the top ([S-6714]). The header's label names the window ([S-454]).
- **[S-3333]** Linked focus ([S-3310]): the focused app's part of every bar is drawn whole and every
  other part at its wash (the family's 16 %, [S-6704]; it was 35 %); the calendar's rings show that
  app's seconds per day against its busiest; the figure is that app's time over the window (or on the
  hovered day) with its share. Hovering a part hovers its app; clicking it selects (again: lets go);
  clicking empty space clears the selection.
- **[S-3334]** Keys: Up/Down, PgUp/PgDn, Home/End step through the apps in the ring's order (the
  linked cursor, [S-3307]); Enter/Space select; Left / Right step the day the figure speaks of; Menu
  hides; Escape lets the cursor and the day go.

Needs a manual pass: the widget on a live desktop ([S-3331], [S-3333], [S-3334]: a real week and month
of use as bars and as a calendar, an app lit with the donut and the list, the keys through the lease),
never in a render or a test, which track nothing.
