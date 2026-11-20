# Screen time: donut

The module `screenTimeDonut` ([S-6911]): the screen-time history as one ring. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the three screen-time widgets share, with
their root tags: the one tracker and its file ([S-450], [S-454], [S-458], [S-459], the host's
`ScreenTimeService` behind `IWidgetHost.ScreenTime` and, for the properties menu, `SettingsHost.ScreenTime`),
the shared hidden apps and limits ([S-3322], [S-3323], `ScreenTimeMenuLines`), the period ([S-3321]), the
wedges and durations ([S-452]), the ring's figure ([S-1210]), the linked focus ([S-3310], the SDK's
`ScreenTimeRules.FocusPart`) and the sample history the previews draw ([S-3355], the SDK's
`SampleScreenTime`).

Owning files: everything in this folder: `ScreenTimeDonutModule.cs` (descriptor, view factory, menu
lines, JSON context), `ScreenTimeDonutConfig.cs` (the period, the Size), `ScreenTimeDonutView.cs` (the ring through
its host, the keys), `ScreenTimeDonutLayout.cs` (its sizes), `ScreenTimeDonutMenu.cs` (its properties
menu, ending with the shared lines), `Preview/` (Debug renders: `--state sample|hovered|selected|packaged`),
`Localization/Locales/` (its own `ScreenTimeDonutStr`, 30 languages). Tests:
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
  `ScreenTimeDonutLayout.Sizing`); a larger card draws a larger ring, as large as the view's slot holds under the day
  line in that inset (`ScreenTimeDonutLayout.RingIn`, the view's `ApplySize`), at the content's top, centred across;
  from 216 its figure may take 40. Tests `ScreenTimeDonutTests.S11515_`. Its wedges stand in the family's track (4,
  [S-6704]); the hole's figure is [S-1210]'s on 28 (40 may stand in a large ring's hole), centred, its
  line under (the day's "of 4h", a window's "2h 10m/day", or the focused app's share) where it fits,
  cut to the hole's width. Nothing to draw keeps the ring ([S-12813]): its track alone, and in the hole "--" while nothing records or none spent ("0m") in a period that recorded, disabled, why under it where the hole holds it and always as the ring's tooltip.
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
