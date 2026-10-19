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

- **[S-650]** Now playing stacks on a tall, narrow tile (art, lines, transport) and moves the
  transport under the lines when beside them it would leave the title under 120px.
- **[S-1475]** Music ladder: XS title (+ artist) over play/pause; S/Wide lines with the transport
  beside (or under, or play only) and art on a strip, seek bar along the bottom (times beside it
  on a wide one); M art + lines + transport, seek bar with the times under it; Tall stacked like
  the media flyout (art, lines, seek, times, transport); L/XL art up to half the width beside
  title/artist/album/40px transport, seek row with times along the bottom. No spectrum ([S-3200]).
- **[S-6406]** Now playing's styles ([S-6000]), `MusicConfig.Style` (`MusicStyle`,
  `"style": "artwork"`): **Artwork and text** (the ladder above), **Full artwork** ([S-6407]) and
  **Text only** (the same ladder with no art, none fetched), in that order: what was "Show album
  art", which is gone. Transport, progress and hide-when-idle stay options for every style ([S-1479]).
- **[S-6407]** Full artwork: the art fills the card edge to edge (cropped to fill; the music glyph
  on the control fill without one), and the title (BodyStrong, Subtitle at L/XL) with the artist
  where it fits, the transport beside them (play/pause alone on a Compact-wide card or when the
  lines would get under 120 DIPs) and the seek bar under them (its times beside it from Expanded
  width) sit at its foot over a scrim: the theme's own base fill fading in, eased, over a band as tall as the foot, to 80 %,
  so the text keeps its theme colours on any art. The foot never takes more than 60 % of the card;
  the artist and then the bar give way first.
- **[S-1479]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Show the transport** and **Show progress**
  (switches, for every style), a divider, and **Hide when idle** (Never / When no player is open /
  When nothing is playing, [S-1464]; edit mode still shows it). (Tests `MusicMenuTests`.)

Needs a manual pass: the widget on a live desktop (a player started, paused, skipped and sought, a
stream with nothing to seek, art and no art, each style, hide when idle).
