# Brightness

The module `brightness` ([S-6911]): the display brightness, one slider. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections (Windows' panels [S-4400]) and the brightness
service's and rules' root tags: how a display is reached [S-4441] (the host's `BrightnessService`), which
displays one slider sets [S-4442] and the keys [S-4444] (the SDK's `BrightnessRules`), the newest level
winning [S-4443]; the slider's look is the volume tile's (`VolumeRules`, [S-2731]).

Owning files: everything in this folder: `BrightnessModule.cs` (descriptor, view factory, menu
lines, JSON context), `BrightnessConfig.cs` (the screens it dims), `BrightnessView.cs` (the slider and
the sun, the levels through `IWidgetHost.Brightness`, its own display through it too), `BrightnessMenu.cs`
(its properties-menu lines), `Preview/` (Debug renders: `--state levels|dim|none|pending`), `Localization/Locales/` (its own
`BrightnessStr`, 30 languages). Tests: its config under the root's rules is the module host's
`WindowsPanelsHostTests`, its menu `Tests/BrightnessMenuTests.cs`.

- **[S-4440]** Kind `Brightness` (123), add menu Devices & power ([S-3511]), 200×150, glyph the sun. One
  slider and the sun, no number, no display names (Karpo §35, §103: no flyout); a column stands it up
  as the volume tile ([S-2731]).
- **[S-4445]** No display has a brightness: "No display brightness to set". Until the displays have
  been looked at the widget draws its frame only.
- **[S-4446]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Screens it dims** (All screens / Only the
  screen it's on, [S-4442]).
- **[S-4447]** Screens it dims is offered only where it changes what is dimmed
  (`BrightnessRules.OffersScreens`, [S-6070]): with two displays with a brightness or more, or while
  "Only the screen it's on" is chosen (so it can go back to every screen); with one, both set that
  display (or the own choice sets none), so the line is not there. Until the displays have been
  looked at it is offered. The menu reads the host's last answer (`BrightnessDisplays`, installed at
  start, as `DriveList` is).

Needs a manual pass: a laptop panel and a DDC/CI monitor (two displays, mixed DPI, "only the screen
it's on"), the keys and Windows' quick settings moving the panel while the widget shows.
