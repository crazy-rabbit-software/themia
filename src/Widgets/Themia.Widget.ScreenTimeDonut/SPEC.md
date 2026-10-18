# Screen time: donut

The module `screenTimeDonut` ([S-6911]): the screen-time history as one ring. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the three screen-time widgets share, with
their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the host's
`ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the settings page, `SettingsHost.ScreenTime`),
the shared hidden apps and limits ([S-3322], `ScreenTimeSettingsRows`), the period ([S-3321]), the
wedges and durations ([S-452]), the ring's figure ([S-1210]), the linked focus ([S-3310], the SDK's
`ScreenTimeRules.FocusPart`) and the sample history the previews draw ([S-3355], the SDK's
`SampleScreenTime`).

Owning files: everything in this folder: `ScreenTimeDonutModule.cs` (descriptor, view factory, settings
rows, JSON context), `ScreenTimeDonutConfig.cs` (the period), `ScreenTimeDonutView.cs` (the ring through
its host, the keys), `ScreenTimeDonutLayout.cs` (the ladder), `ScreenTimeDonutSettings.cs` (its page,
ending with the shared rows), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (its own `ScreenTimeDonutStr`, 30 languages). Tests:
`Tests/ScreenTimeDonutTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3340]** `ScreenTimeDonutConfig`: `Period` only. One ring, the total in its hole.
- **[S-3341]** The wedges: the busiest seven apps and one Other, from cumulative seconds ([S-452]).
  No legend: the list is its own widget ([S-666]); a wedge's tooltip names it.
- **[S-3342]** Ladder (`ScreenTimeDonutLayout.Donut`): the ring as large as the tile allows (at least 56,
  the 16 DIP inset at the sides), a seventh of it the stroke; the hole's figure is [S-1210]'s fit,
  and its second line (the day's "of 4h", a window's "2h 10m/day", or the focused app's share)
  appears once the hole is 72 DIPs wide.
- **[S-3343]** Linked focus: the focused wedge is lifted (its stroke 1.3 times as thick), the others
  dimmed, and the hole names it ("Code · 41%"). An app folded into Other lights Other
  (`ScreenTimeRules.FocusPart`); a hidden one lights nothing. Click selects, empty space clears.
- **[S-3344]** Keys: Up/Down, PgUp/PgDn, Home/End step through the wedges clockwise from twelve;
  Enter/Space select; Left / Right step the day (a day period); Menu hides; Escape lets the cursor go.

Needs a manual pass: the widget on a live desktop ([S-3340], [S-3341], [S-3343], [S-3344]: real apps
tracked as wedges, a wedge lit with the chart and the list, the keys through the lease), never in a
render or a test, which track nothing.
