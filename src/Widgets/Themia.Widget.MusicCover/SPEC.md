# Album art (musicCover)

The module `musicCover` ([S-6911]): the cover of what Windows is playing, alone, an atom of the old
Now playing card (owner decision 2026-11-12). Its tags are [S-11320]..[S-11329], this file's alone;
the root `SPEC.md` holds the host's sections; the session's rules are the SDK's `MusicRules`, its
connection the SDK's `NowPlayingFeed` ([S-11311]).

Owning files: everything in this folder: `MusicCoverModule.cs` (descriptor, view factory, menu lines,
JSON context), `MusicCoverConfig.cs`, `MusicCoverFace.cs` (its square), `MusicCoverView.cs`, `Preview/`
(Debug renders: `--state sample`), `Localization/Locales/` (its own `MusicCoverStr`; the hide-when-idle
words copied with their translations from `music`). Tests: `Tests/MusicCoverTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-11320]** **The cover alone** (add menu: Media): the current track's cover drawn to the card's
  edges (it owns its top edge, [S-5700]), cropped to fill the square; without one (no track, no art)
  the music glyph (32) on the control fill. It reads the same media session as the track atom, the art
  fetched once per track. As large as its card (owner, 2026-11-20: "the size should not be settable, it is set by
  the size of the widget"; the Size line, [S-2449], is gone): a new one a square of 160, the least the grid's least
  widget (32, WR-14, which still holds the glyph), no practical largest ([S-12411]); the picture fills whatever
  shape the card is (`MusicCoverFace.Sizing`). It starts without a header. Its menu: **Hide when idle** ([S-1464],
  as the track atom's). A click opens the app that is playing,
  as the track atom's ([S-2030]). Tests `MusicCoverTests.S11320_`.

Needs a manual pass: a live track's cover, a track without one, a change of track.
