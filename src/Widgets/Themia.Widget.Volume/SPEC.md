# Volume

The module `volume` ([S-6911]): an output device's volume, one row: the speaker, the level and the slider. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the audio widgets share: the Core Audio worker
([S-440], [S-1220], the host's `CoreAudio` behind `IWidgetHost.Audio`), the device choice ([S-1221],
[S-1222], the shared `AudioDeviceLine`), previews never writing ([S-1233]), and the slider's rules, the
SDK's `VolumeRules` and `AudioRules` with their root tags ([S-1226] the keys, [S-1234] stepping,
[S-2731] the axis, [S-2732] the wheel, [S-2733] Home and End).

Owning files: everything in this folder: `VolumeModule.cs` (descriptor, view factory, menu lines,
JSON context), `VolumeConfig.cs` (the device and whether it may set the level), `VolumeView.cs` (the
row: the speaker, the level and the slider through the shared `AudioFeed`; its measures the SDK's `LevelRow`, Brightness's), `VolumeMenu.cs` (its properties-menu lines), `Localization/Locales/`
(its own `VolumeStr`, 30 languages). Tests: its device choice beside the mixer's is the module host's
`AudioHostTests`, its menu `Tests/VolumeMenuTests.cs`, its row `Tests/VolumeRowTests.cs`.

- **[S-2730]** The volume's controls: the speaker at the row's start (glyph by level, crossed out when muted; click mutes,
  tooltip Mute/Unmute) and the 0..100 slider ([S-6716]: the rail Track, the value the accent), drawn as one row
  ([S-17225]). No level meter. Options ([S-2734]): output device and "Allow changes"; read-only greys the controls rather
  than hiding them. No device (none, the chosen one not connected, Core Audio not answering) draws the speaker and the slider
  disabled, the slider at the middle ([S-1940], [S-1941]), the reason as the tile's tooltip, and the level "--" disabled
  with the reason its tooltip ([S-12813]); no key, wheel or click does anything. Until Core Audio first answers the row is
  "--" beside the slider. Renders `--state sample|unavailable`.
- **[S-17225]** **One row, Brightness's** (owner, 2026-11-24: *"volume slider should go into the quick settings too"*, beside
  Brightness, whose row it takes so the two read as a pair; the SDK's `LevelRow`, [S-17184]): in the card's side insets the
  speaker (its 16 glyph where the sun stands, a button no taller than the slider's thumb), 8, the level (Body, tabular
  figures, its column held to "100%" so the slider never moves), 12, the slider across what is left; the row centred in the
  card. Its sizing is the row's: one height, kept (`KeepsHeight`: the inset, the slider's thumb, the inset), at least its
  speaker, level and the slider's 80 of travel wide, a new one 300, as wide as the user makes it: a resize makes the slider
  longer and nothing else. Muted, the level it returns to in the disabled role, the slider at 0. The output device's name is
  no longer drawn (the row has no line under; the device is the page's Output device line). The level follows the slider
  while it is dragged. Tests `VolumeRowTests.S17225_`. The Mixer is unchanged.
- [S-2151] (the level as a 28 Readout over the controls, the device's name under it, the card 136 tall) retired by [S-17225].
- **[S-2734]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Output device** (the shared search of
  [S-1222]; only where there is a choice, [S-1236]), a divider, **Allow changes** (a switch) and,
  while changes are allowed, as a note, how the keys work ([S-1226]: the keys are ignored without,
  so the note is not there).
- **[S-2025]** A click on the card ([S-2000]) opens Windows' Sound page (`ms-settings:sound`); the
  slider and the mute button keep their clicks ([S-2004]); the module decides it
  (`VolumeModule.Opens`).

Needs a manual pass: the tile on a live desktop (the level moved by Windows' own slider and by the
tile, muted, a second device chosen and unplugged, the keys and the wheel), never in a render: a
render's slider is inert.
