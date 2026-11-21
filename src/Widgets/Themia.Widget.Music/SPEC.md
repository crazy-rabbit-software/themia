# Now playing (the family: the title, the artist, the album and the buttons)

The family project `Themia.Widget.Music` ([S-6901], `[assembly: ThemiaWidgetFamily]`): what Windows is playing, through
its own media session, **a widget a part** (owner, 2026-11-21: "now playing widget should be split into the individual
buttons, the title and the album name."). Its texts are kinds of the text base (root `SPEC.md`, Text widgets (TEXTS),
[S-14930]..[S-14936]); its buttons are icon kinds ([S-15003]). Its tags are this file's; the root `SPEC.md` holds the host's sections and
the session's and the rules' root tags: hide when idle [S-1464], the seek bar's mapping [S-1465], the keys [S-1468] and the
album line [S-1469] are the SDK's `MusicRules`; the WinRT session manager is the host's (`MediaSession`, behind
`IWidgetHost.Media`), one widget's connection to it the SDK's `NowPlayingFeed`. The cover and the progress are their own
projects (`musicCover`, `musicProgress`, their own SPEC.md), in the same add-page family.

Owning files: everything in this folder: `MusicKinds.cs` (the kinds, what each text shows, the JSON context),
`MusicConfig.cs` (the configs: when each hides), `MusicText.cs` (the text kinds' module and view), `MusicButton.cs` (the
buttons' module, their words and looks, their view on the icon face), `MusicMenu.cs` (the one properties-menu line), `Samples/`,
`Preview/` (Debug renders: the texts `--state sample|idle|bare|long`, the buttons `sample|playing|idle`),
`Localization/Locales/` (its own `MusicStr`). Tests: `Tests/MusicTests.cs`, `Tests/MusicMenuTests.cs`,
`Tests/MusicOpenTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's rules is the module host's
`MusicHostTests`.

- **[S-650]** *(Retired by [S-1475], G5 2026-10-29: the transport is always the Controls row at the
  foot, so it never stands beside the lines.)*
- **[S-1475]** *(Retired with the split: the ladder of the art, the lines, the progress and the
  transport by the card's size; the track atom is [S-11311], the cover [S-11320], the progress
  [S-11330].)*
- **[S-11311]** *(Retired by [S-14920], owner 2026-11-21: the track atom, its title over the artist and album line over
  the transport in one card, is split into a widget a part.)*
- **[S-14920]** **A widget a part**: `music` **Song title** (the old track atom's id: a placed one is the title now, its
  Hide when idle kept; nothing converted), `musicArtist` **Artist**, `musicAlbum` **Album**, `musicPrevious` **Previous
  track**, `musicPlayPause` **Play or pause**, `musicNext` **Next track**, in that order in the family **Now playing**
  under Music & media (`AddFolderId.NowPlaying`), the cover and the progress after them. Each reads the session through
  its own `NowPlayingFeed`, as the cover and the progress do, so every part shows the same track at the same moment. Each
  has one option, **Hide when idle** ([S-1464], the shared setting, [S-1479]).
- **[S-14921]** **The texts** (text kinds, [S-14934]): the title (primary), the artist and the album (secondary), one line
  each, led by their glyph (a note, a person, a disc: the three look alike without it), cut with their ellipsis at the
  card's edge ([S-14931]). The artist is a part of its own because the old card showed it (its line was the artist and
  the album). With no track the title says why ("Nothing playing", "No media playing") and the artist and the album their
  words for none ("No artist", "No album"), unavailable, why their tooltip; a track with no artist or album (a stream, a
  video) the same, an album named as its artist said once (`MusicRules.Album`). `MusicKinds.Shown`.
- **[S-14922]** **The buttons**: previous, play or pause, next, each an icon kind on the icon base ([S-15000]..[S-15005];
  owner, 2026-11-21: "there should be a shared widget backend for widgets that show just an icon"): its glyph alone filling
  its card (a 76 square when new, down to 32), what a click does its tooltip and its screen reader's name ("Previous",
  "Play", "Pause", "Next"); the item layer's hover and pressed fills across the whole card, never a size change (WR-12).
  Play or pause is the accent while it plays (On, as a toggle that is on) and offers Pause; a button the player cannot do
  now, and every button with no track, keeps its glyph drawn unavailable, why its tooltip with no track ([S-1940], WR-10).
  A click, or Space or Enter while it has the keyboard, asks the session through the host (`NowPlayingFeed.Press`), never
  optimistic: the session's own notification draws the result. In edit mode the press is the card's (select, move), never
  a press of the button ([S-1911]). A preview presses nothing. Left to right in every language ([S-1113]: they are three
  cards the user places). `MusicButtons.Shown`. Was: a Tile with its name under the glyph (the name the glyph alone said).
- **[S-2155]** *(Retired with the split: the side cover is the cover atom beside the track atom, the
  Now playing group, [S-11340].)*
- **[S-6406]** *(Retired with the split: the styles Artwork and text, Full artwork and Text only; the
  first two come back as the ready-made groups Now playing and Now playing, large, [S-11340], the
  third is the track atom alone.)*
- **[S-6407]** *(Retired with [S-6406].)*
- **[S-1479]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Hide when idle** (Never / When no player is open
  / When nothing is playing, [S-1464]; edit mode still shows it), on every part of the family. *(Show the transport and
  Show progress are retired with the split.)* (Tests `MusicMenuTests`.)
- **[S-2030]** A click on a text ([S-2000], [S-14935]) opens the app that is playing: the media session's own app
  (`NowPlaying.AppId`, `SourceAppUserModelId`), a packaged app by its Apps-folder item, a desktop player brought to the
  front when it runs ([S-2003]); nothing before the first read or with no session; the text lights under the pointer while
  it opens something, and the card menu's first line is "Open the player". A button's click is its own ([S-2004]).
  `TextOpens.Player`.

Needs a manual pass: the family on a live desktop (a player started, paused, skipped, a stream with nothing to seek, hide
when idle), the buttons pressed against a real player (Spotify, a browser tab, Media Player), and every part of a Now
playing group agreeing on a live track.
