# Mixer

The module `mixer` ([S-6911]): per-app volume for an output device. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the audio widgets share: the Core Audio worker
([S-440], [S-1220], the host's `CoreAudio` behind `IWidgetHost.Audio`), the device choice ([S-1221],
[S-1222], the shared `AudioDeviceRow`), previews never writing ([S-1233]), and the rows' rules, the SDK's
`MixerRules`, `MixerFill` and `AudioRules` with their root tags ([S-441]..[S-447] the rows, their
names, grouping, order and writes, [S-1225] the keys, [S-1227] [S-1228] the rows by room, [S-1234]
stepping, [S-3251] apps and nothing else, [S-1624] the expanded card).

Owning files: everything in this folder: `MixerModule.cs` (descriptor, view factory, settings rows,
JSON context), `MixerConfig.cs` (the device and what the list holds and shows), `MixerView.cs` (the
list through the shared `AudioFeed`), `MixerView.Row.cs` (a row: icon through the host's
`AppIconSource`, name, slider, mute) and `HugRow.cs` (the row's layout), `MixerSettings.cs` (its page),
`Localization/Locales/` (its own `MixerStr`, 30 languages). Tests: its device choice beside the
volume tile's is the module host's `AudioHostTests`.

- **[S-448]** No device: "No output device". No rows: "No app is playing audio". Failures are logged
  once, and again only when they change.
- **[S-449]** From 360 px a row is one line (name, slider, mute); narrower, the slider goes
  under the name; under 170 px a row is icon and mute ([S-2771]: no percent).
- **[S-2771]** The mixer shows no volume number (user decision, [S-666]): a row is its icon, name
  (and session pill), slider and mute button; the level is the slider's position and the mute
  glyph's band (crossed out when muted). No percent beside a row at any width. The slider's own
  drag tooltip is the only number, while dragging.

Needs a manual pass: the mixer on a live desktop (apps playing, one muted and dragged, a second
output device chosen and unplugged, the keys), never in a render: a render's sliders are inert.
