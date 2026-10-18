# Volume

The module `volume` ([S-6911]): an output device's volume, one slider. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the audio widgets share: the Core Audio worker
([S-440], [S-1220], the host's `CoreAudio` behind `IWidgetHost.Audio`), the device choice ([S-1221],
[S-1222], the shared `AudioDeviceRow`), previews never writing ([S-1233]), and the slider's rules, the
SDK's `VolumeRules` and `AudioRules` with their root tags ([S-1226] the keys, [S-1234] stepping,
[S-2731] the axis, [S-2732] the wheel, [S-2733] Home and End).

Owning files: everything in this folder: `VolumeModule.cs` (descriptor, view factory, settings rows,
JSON context), `VolumeConfig.cs` (the device and whether it may set the level), `VolumeView.cs` (the
slider and the speaker through the shared `AudioFeed`), `VolumeSettings.cs` (its page), `Localization/Locales/`
(its own `VolumeStr`, 30 languages). Tests: its device choice beside the mixer's is the module host's
`AudioHostTests`.

- **[S-2730]** The volume tile is the slider and nothing else, at every size (user decision): a
  speaker button at the slider's start (glyph by level, crossed out when muted; click mutes, tooltip
  Mute/Unmute) and the 0..100 slider. No percentage, no level meter, no device name. The card header
  follows the shared header rules. Settings: output device and "Allow changes"; read-only greys the
  controls rather than hiding them. Unavailable/missing device shows the empty state.

Needs a manual pass: the tile on a live desktop (the level moved by Windows' own slider and by the
tile, muted, a second device chosen and unplugged, the keys and the wheel), never in a render: a
render's slider is inert.
