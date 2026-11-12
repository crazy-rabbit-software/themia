# Music (now playing: the track)

The module `music` ([S-6911]): what Windows is playing, through its own media session, as its track
atom (the id stays `music`; owner decision 2026-11-12, the old card is split into atoms). Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and the session's and the rules' root
tags: hide when idle [S-1464], the seek bar's mapping [S-1465], the keys [S-1468] and the album line
[S-1469] are the SDK's `MusicRules`; the WinRT session manager is the host's (`MediaSession`, behind
`IWidgetHost.Media`), one widget's connection to it the SDK's `NowPlayingFeed` ([S-11311]). The cover
and the progress are atoms of their own (`musicCover`, `musicProgress`, their own SPEC.md); the old
styles come back as the ready-made groups Now playing and Now playing, large ([S-11340]).

Owning files: everything in this folder: `MusicModule.cs` (descriptor, view factory, menu lines,
JSON context), `MusicConfig.cs` (when it hides), `MusicFace.cs` (its sizing), `MusicView.cs` (the lines,
the transport, the keys), `MusicMenu.cs` (its properties-menu lines), `Preview/` (Debug renders:
`--state sample`), `Localization/Locales/` (its own `MusicStr`, 30 languages). The seek bar
(`MusicSeekBar`) and the session's connection (`NowPlayingFeed`) are the SDK's, shared by the atoms.
Tests: `Tests/MusicTests.cs`, `Tests/MusicMenuTests.cs` (run by `tests/Themia.Modules.Tests`); its config
under the root's rules is the module host's `MusicHostTests`.

- **[S-650]** *(Retired by [S-1475], G5 2026-10-29: the transport is always the Controls row at the
  foot, so it never stands beside the lines.)*
- **[S-1475]** *(Retired with the split: the ladder of the art, the lines, the progress and the
  transport by the card's size; the track atom is [S-11311], the cover [S-11320], the progress
  [S-11330].)*
- **[S-11311]** **The track atom** (`MusicFace`, `MusicView`): the track's title (BodyStrong,
  primary) over the artist and album in one Body line (secondary), each cut with its ellipsis, at the
  content's top; the transport (Previous, Play/Pause as the one main action in the accent, Next: 28
  icon buttons, left to right in every language) 4 under them at the bottom inset. A disabled
  transport button keeps its place, its glyph disabled. Nothing playing is the Status row at the top
  (the music glyph and why, [S-6714]). What it shows is fixed: no art, no progress, no styles. Its
  card (`MusicFace.Sizing`): the inset, the two lines, 4, the transport, the inset (96, a Small cover's
  side), one height, 200 to 420 wide (a new one 244), where a wider card only gives the lines more
  room. It starts without a header. It reads the session through the SDK's `NowPlayingFeed`, as the
  cover and the progress do, so the three show the same track at the same moment. Tests
  `MusicTests.S11311_`.
- **[S-2155]** *(Retired with the split: the side cover is the cover atom beside the track atom, the
  Now playing group, [S-11340].)*
- **[S-6406]** *(Retired with the split: the styles Artwork and text, Full artwork and Text only; the
  first two come back as the ready-made groups Now playing and Now playing, large, [S-11340], the
  third is the track atom alone.)*
- **[S-6407]** *(Retired with [S-6406].)*
- **[S-1479]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Hide when idle** (Never / When no player is open
  / When nothing is playing, [S-1464]; edit mode still shows it). *(Show the transport and Show
  progress are retired with the split.)* (Tests `MusicMenuTests`.)
- **[S-2030]** A click on the card ([S-2000]) opens the app that is playing: the media session's own
  app (`NowPlaying.AppId`, `SourceAppUserModelId`), a packaged app by its Apps-folder item, a
  desktop player brought to the front when it runs ([S-2003]); nothing with no session. The
  transport buttons keep their clicks ([S-2004]). `MusicModule.Opens`.

Needs a manual pass: the widget on a live desktop (a player started, paused, skipped, a stream with
nothing to seek, hide when idle), and the three atoms of a Now playing group agreeing on a live track.
