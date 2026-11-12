# Music (now playing)

The module `music` ([S-6911]): what Windows is playing, through its own media session. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and the session's and the rules' root
tags: hide when idle [S-1464], the seek bar's mapping [S-1465], the keys [S-1468] and the album line
[S-1469] are the SDK's `MusicRules`; the WinRT session manager is the host's (`MediaSession`, behind
`IWidgetHost.Media`).

Owning files: everything in this folder: `MusicModule.cs` (descriptor, view factory, menu lines,
JSON context), `MusicConfig.cs` (with its styles and when it hides), `MusicView.cs` (the session
through its host, the transport, the keys) with `MusicView.Layout.cs` (the ladder and the styles) and
`MusicSeekBar.cs`, `MusicMenu.cs` (its properties-menu lines), `Preview/` (Debug renders: `--state sample`),
`Localization/Locales/` (its own `MusicStr`, 30 languages, its style names included). Tests:
`Tests/MusicTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's rules is the
module host's `MusicHostTests`.

- **[S-650]** *(Retired by [S-1475], G5 2026-10-29: the transport is always the Controls row at the
  foot, so it never stands beside the lines.)*
- **[S-1475]** Now playing on the family's blocks (`MusicFace`): the lines at the content's top, the
  track on the Title step (BodyStrong on a compact width) and the artist and album in one Body line,
  secondary, each cut with an ellipsis; the transport the Controls row at the bottom inset
  (Previous, Play/Pause as the one main action in the accent, Next; on a narrow card Next and then
  Previous give way, [S-6716]); the progress row 8 over it: the elapsed time and the total in
  Caption, tertiary, at its two ends in columns as wide as their widest case (every figure an 8, so
  the bar does not move as the seconds go by), the seek bar between them 8 from each (its rail the
  Track role, its fill and thumb the accent). Height goes to the title, then the transport, then the
  artist, then the progress ([S-666]). The art is data: where the side cover does not hold
  ([S-2155]: XS, S, Tall), a 48 lead beside the two lines from a regular width; once the view area
  is expanded in height a square over the lines, as large as the room left allows (at least 96). No album line of its own, no larger title or 40 transport on L/XL (concise
  first). A disabled transport button keeps its place, its glyph disabled. Nothing playing is the
  Status row at the top (the music glyph and why, [S-6714]). No spectrum ([S-3200]).
- **[S-2155]** In the Artwork style the art takes the room (owner decision 2026-11-12;
  `MusicFace.Side`, tests `MusicFaceTests`): from a regular width the cover is a square at the
  content's left, as tall as the room between the header and the bottom inset, at most half the
  content's width, in the one radius (no picture: today's placeholder, the music glyph on the control
  fill, 48 from a 96 square, else 32); 12 from it a column with the title and the artist at its top
  and the progress row and the transport (Play the one accent action) at its bottom, by the ladder
  above. Where that column would be narrower than the whole transport (112: three 32 buttons 8
  apart) the cover gives way first, a smaller square, never the controls; a square that would be
  under the family's 48 is not drawn and the compact ladder above stands (XS, S and Tall at their
  tier sizes; M 144, Wide 100, L 264, XL 384). At L and XL the square is half the width and the
  room under it, beside the column's foot, stays empty ([S-666]). The Cover and Text styles keep
  their ladders.
- **[S-6406]** Now playing's styles ([S-6000]), `MusicConfig.Style` (`MusicStyle`,
  `"style": "artwork"`): **Artwork and text** (the ladder above), **Full artwork** ([S-6407]) and
  **Text only** (the same ladder with no art, none fetched), in that order: what was "Show album
  art", which is gone. Transport, progress and hide-when-idle stay options for every style ([S-1479]).
- **[S-6407]** Full artwork: the art is a band the card's width (cropped to fill; the music glyph on
  the control fill without one) from the view's top to 8 over the lines, which stand with the
  progress row and the transport at the content's foot as [S-1475] has them; the band takes what
  they leave, and under 48 it is not drawn (a strip shows the lines alone). Nothing is laid over the
  art, so no scrim (a colour made from numbers) is needed: the text keeps the card's roles.
- **[S-1479]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Show the transport** and **Show progress**
  (switches, for every style), a divider, and **Hide when idle** (Never / When no player is open /
  When nothing is playing, [S-1464]; edit mode still shows it). (Tests `MusicMenuTests`.)
- **[S-2030]** A click on the card ([S-2000]) opens the app that is playing: the media session's own
  app (`NowPlaying.AppId`, `SourceAppUserModelId`), a packaged app by its Apps-folder item, a
  desktop player brought to the front when it runs ([S-2003]); nothing with no session. The
  transport buttons and the seek bar keep their clicks ([S-2004]). `MusicModule.Opens`.

Needs a manual pass: the widget on a live desktop (a player started, paused, skipped and sought, a
stream with nothing to seek, art and no art, each style, hide when idle).
