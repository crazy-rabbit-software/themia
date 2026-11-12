# Mixer

The module `mixer` ([S-6911]): per-app volume for an output device. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the audio widgets share: the Core Audio worker
([S-440], [S-1220], the host's `CoreAudio` behind `IWidgetHost.Audio`), the device choice ([S-1221],
[S-1222], the shared `AudioDeviceLine`), previews never writing ([S-1233]), and the rows' rules, the SDK's
`MixerRules`, `MixerFill` and `AudioRules` with their root tags ([S-441]..[S-447] the rows, their
names, grouping, order and writes, [S-1225] the keys, [S-1228] the quiet rows, [S-1234] stepping,
[S-3251] apps and nothing else, [S-1624] a row's second line).

Owning files: everything in this folder: `MixerModule.cs` (descriptor, view factory, menu lines,
JSON context), `MixerConfig.cs` (the device and what the list holds and shows), `MixerView.cs` (the
list through the shared `AudioFeed`), `MixerView.Row.cs` (a row: a Row block, the icon through the
host's `AppIconSource`, the name, the slider and the mute), `MixerLayout.cs` (the rows' ladder),
`MixerMenu.cs` (its properties-menu lines), `Preview/` (Debug renders: `sample`, `unavailable`),
`Localization/Locales/` (its own `MixerStr`, 30 languages). Tests: its device choice beside the
volume tile's is the module host's `AudioHostTests`, its menu `Tests/MixerMenuTests.cs`, its rows
`Tests/MixerLayoutTests.cs`.

- **[S-448]** No device (none, the chosen one not connected, Core Audio not answering): one System
  sounds row drawn disabled at the middle (`MixerRules.StandIn`, [S-1940]), no key, menu or write; the
  reason ("No output device") is the tile's tooltip. Render `--state unavailable`. No rows on a
  device that is there: the Status row at the top ([S-6714]: the mixer's glyph, "No app is playing
  audio"). Failures are logged once, and again only when they change.
- **[S-449]** The rows are the family's Row block ([S-6711], [S-6712]; `MixerLayout`): [lead 16: the
  app's icon, or the system sounds' bell or an app's window glyph, secondary] 8 [name, primary] 12
  [slider: the Controls slider, rail Track, value the accent] 8 [mute: an icon button whose glyph is
  the level's band, [S-11805]], 28 tall, 40 with a second line ([S-1624]: what the app is doing) where Lines is
  two ([S-11309]). The slider takes three fifths of the room the lead, the gaps and the mute leave,
  never under its least (80), which the list's least width keeps beside the name. The config's rows
  show the playing apps, the rest a scroll away; the quiet rows fill the rows left ([S-1228]). A short
  list keeps its rows: no row is stretched.
- **[S-11805]** **Rows read as separate** (2026-11-12): on a one-line row (28) the mute is the family's 24
  button, inset 2 in the row, so the buttons of stacked rows stand 4 apart (the 28 button filled the row
  and the buttons stacked into one column with no gap); on a two-line row (40) it is the 28 icon button
  (`MixerLayout.Mute`). The row keeps its height: a setting's lines decide it, never the control.
  Tests `MixerLayoutTests.S11805_`.
- **[S-11309]** **A list atom.** The mixer's **Rows** (1 to 12, a new one 4) and **Lines** (one or two
  a row, a new one one) are settings on its menu, after App icons; the card follows
  (`MixerLayout.Sizing`): under its header the rows and the inset, stepped by a row and following
  Rows (dragging its bottom edge sets them, `MixerLayout.SizeSets`), 240 to 520 wide (a new one 300),
  where a wider card only gives the sliders more travel. No tier ladder is left (the name given way
  for the slider, the lines by the tier). Tests `MixerLayoutTests.S11309_`, `MixerMenuTests`. The keyboard's cursor is the Row block's ring ([S-1288]); a row
  has the item layer's hover. Render `--state sample` (five apps, Edge's two sessions grouped,
  Discord silent, Steam muted). Retired with it: the 40 and 56 DIP rows, the rows sharing an
  expanded card's height, the slider tucked under the name and the session pill ([S-2771]).
- **[S-2771]** The mixer shows no volume number (user decision, [S-666]): a row is its icon, name,
  slider and mute button; the level is the slider's position and the mute glyph's band (crossed
  out when muted). A row of several sessions ([S-442]) is one row with no count of its own. No percent beside a row at any width. The slider's own
  drag tooltip is the only number, while dragging.
- **[S-1235]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then in groups behind dividers: **Output device**
  (the shared search of [S-1222]; only where there is a choice, [S-1236]); which rows the list
  holds, **One row per app** ([S-442]), **Only apps playing now** and **System sounds** ([S-445]);
  what a row shows, **App icons**; and, as a note, how the keys work ([S-1225]).
- **[S-2026]** A click on the card ([S-2000]) opens Windows' Volume mixer page
  (`ms-settings:apps-volume`); the sliders and buttons keep their clicks ([S-2004]); the module
  decides it (`MixerModule.Opens`).

Needs a manual pass: the mixer on a live desktop (apps playing, one muted and dragged, a second
output device chosen and unplugged, the keys), never in a render: a render's sliders are inert.
