# Brightness

The module `brightness` ([S-6911]): the display brightness, one slider. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections (Windows' panels [S-4400]) and the brightness
service's and rules' root tags: how a display is reached [S-4441] (the host's `BrightnessService`), which
displays one slider sets [S-4442] and the keys [S-4444] (the SDK's `BrightnessRules`), the newest level
winning [S-4443]; the slider's look is the volume tile's (`VolumeRules`, [S-2731]).

Owning files: everything in this folder: `BrightnessModule.cs` (descriptor, view factory, menu
lines, JSON context), `BrightnessConfig.cs` (the screens it dims), `BrightnessView.cs` (the slider and
the sun, the levels through `IWidgetHost.Brightness`, its own display through it too), `BrightnessMenu.cs`
(its properties-menu lines), `Preview/` (Debug renders: `--state levels|dim|unavailable|pending`), `Localization/Locales/` (its own
`BrightnessStr`, 30 languages). Tests: its config under the root's rules is the module host's
`WindowsPanelsHostTests`, its menu `Tests/BrightnessMenuTests.cs`.

- **[S-4440]** Kind `Brightness` (123), add menu Devices & power ([S-3511]), 200×150, glyph the sun. One
  slider, no display names (Karpo §35, §103: no flyout): the Controls block's slider at the card's
  bottom inset as the volume tile's ([S-2731], [S-6716]); the sun is the header's glyph (the one at
  the slider's start went with the family, G5), the level the slider's name and tooltip.
- **[S-2152]** Over the slider the level is a Readout, as the meters beside it (owner decision
  2026-11-12; the root's [S-2150], `LevelFace`): "70%" at the view's top on 28 (S), 40 (M, Wide,
  Tall) or 68 (L, XL), following the slider while it is dragged; XS is the slider alone. It has no
  line under: the displays' names are not in the host's answer (`BrightnessDisplay` carries the GDI
  device name only), and a line would need a host contract change first. With several displays
  (`--state levels`) it stays one widget with one Readout: the slider sets them together ([S-4442]),
  so the Readout is the level the slider shows (its own display's, else the first's), not a Row per
  display, which would be a value per display with no control of its own. No display to set
  ([S-4445]) draws no Readout.
- **[S-4445]** No display has a brightness (or, with "Only the screen it's on", not that one): the
  slider drawn disabled, at the middle ([S-1940], [S-1941], user request 2026-10-19),
  "No display brightness to set" as the tile's tooltip and the slider's name; no key or wheel moves it.
  Until the displays have been looked at the widget draws its frame only.
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
