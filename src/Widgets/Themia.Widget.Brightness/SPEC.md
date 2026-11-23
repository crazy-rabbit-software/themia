# Brightness

The module `brightness` ([S-6911]): the display brightness, one slider. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections (Windows' panels [S-4400]) and the brightness
service's and rules' root tags: how a display is reached [S-4441] (the host's `BrightnessService`), which
displays one slider sets [S-4442] and the keys [S-4444] (the SDK's `BrightnessRules`), the newest level
winning [S-4443]; the slider's look is the volume tile's (`VolumeRules`, [S-2731]).

Owning files: everything in this folder: `BrightnessModule.cs` (descriptor, view factory, menu
lines, JSON context), `BrightnessConfig.cs` (the screens it dims), `BrightnessRow.cs` (the row's
measures and sizing, pure), `BrightnessView.cs` (the row: the sun, the level and the slider, the levels through
`IWidgetHost.Brightness`, its own display through it too), `BrightnessMenu.cs`
(its properties-menu lines), `Preview/` (Debug renders: `--state sample|dim|unavailable|pending`), `Localization/Locales/` (its own
`BrightnessStr`, 30 languages). Tests: its config under the root's rules is the module host's
`WindowsPanelsHostTests`, its menu `Tests/BrightnessMenuTests.cs`, its row `Tests/BrightnessRowTests.cs`.

- **[S-4440]** Kind `Brightness` (123), add menu Devices & power ([S-3511]), glyph the sun. One slider, no display names
  (Karpo §35, §103: no flyout), drawn as one row ([S-17184]).
- **[S-17184]** **One row** (owner, 2026-11-23: "brightness slider should be moved to the quick settings and should have a
  row design and not columnar design"), as Windows' quick settings draw it (`BrightnessRow`): in the card's side insets the
  sun (16, its glyph), 8, the level (Body, tabular figures, its column held to "100%" so the slider never moves), 12, the
  Controls block's slider across what is left ([S-6716]: its rail the Track role, its value the accent); the row centred in
  the card. Its sizing is the row's: one height, kept (the inset, the slider's thumb, the inset: 46, 50 on the grid; never
  relaxed, [S-12411], as a rolled-up fence's), at least its sun, level and the slider's 80 of travel wide, a new one 300, as wide
  as the user makes it: a resize makes the
  slider longer and nothing else. The level follows the slider while it is dragged.
  With several displays (`--state sample`) it stays one row with one level: the slider sets them together ([S-4442]), so the
  level is the one the slider shows (its own display's, else the first's). Which tab of the gallery holds it is the
  gallery's ([S-16740]). Tests `BrightnessRowTests.S17184_`.
- [S-2152] (the level as a 28 Readout over the slider, the card 120 tall) retired by [S-17184].
- **[S-4445]** No display has a brightness (or, with "Only the screen it's on", not that one): the
  slider drawn disabled, at the middle ([S-1940], [S-1941], user request 2026-10-19), the level "--" disabled,
  "No display brightness to set" as the row's tooltip and the slider's name; no key or wheel moves it.
  Until the displays have been looked at it draws the same default, "--" beside the disabled slider, without a
  reason ([S-12813]: never an empty card; never a wrong level).
- **[S-4446]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Screens it dims** (All screens / Only the
  screen it's on, [S-4442]).
- **[S-4447]** Screens it dims is offered only where it changes what is dimmed
  (`BrightnessRules.OffersScreens`, [S-6070]): with two displays with a brightness or more, or while
  "Only the screen it's on" is chosen (so it can go back to every screen); with one, both set that
  display (or the own choice sets none), so the line is not there. Until the displays have been
  looked at it is offered. The menu reads the host's last answer (`BrightnessDisplays`, installed at
  start, as `DriveList` is).
- **[S-2027]** A click on the card ([S-2000]) opens Windows' Display page (`ms-settings:display`);
  the sliders keep their clicks ([S-2004]); the module decides it (`BrightnessModule.Opens`).

Needs a manual pass: a laptop panel and a DDC/CI monitor (two displays, mixed DPI, "only the screen
it's on"), the keys and Windows' quick settings moving the panel while the widget shows.
