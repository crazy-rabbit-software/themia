# Track progress (musicProgress)

The module `musicProgress` ([S-6911]): how far into what Windows is playing it is, an atom of the old
Now playing card (owner decision 2026-11-12). Its tags are [S-11330]..[S-11339], this file's alone;
the root `SPEC.md` holds the host's sections; the seek bar's mapping [S-1465] and the clock are the
SDK's `MusicRules`, the seek bar the SDK's `MusicSeekBar`, the session's connection `NowPlayingFeed`
([S-11311]).

Owning files: everything in this folder: `MusicProgressModule.cs` (descriptor, view factory, menu
lines, JSON context), `MusicProgressConfig.cs`, `MusicProgressFace.cs` (its sizing),
`MusicProgressView.cs`, `Preview/` (Debug renders: `--state sample|hovered`), `Localization/Locales/` (its own
`MusicProgressStr`; the hide-when-idle words copied with their translations from `music`). Tests:
`Tests/MusicProgressTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-11330]** **The progress alone** (add menu: Media): the seek bar alone, a bar atom (root `SPEC.md`, [S-15104];
  owner, 2026-11-21: "track progress should be split into three widgets too, two text based widgets and a scaling
  progress bar that also fills up to the universal spacing given"): its rail fills the card's box less the universal
  spacing, as long and as thick as that room, its ends round (the Track role), its value the accent from the start;
  click to jump, drag to scrub ([S-1465]), left to right in every language. The elapsed time and the track's length are
  text atoms of the Now playing family (`musicElapsed`, `musicDuration`, [S-15106]). A thin rail shows its thumb under
  the pointer and changes nothing else ([S-12816]). No track, an empty rail (WR-10). The bar advances once a second
  while a track plays, from the session's anchor, and nothing at rest. Its card (`MusicProgressFace.Sizing`): a new one
  240 by 20 (the 4 track in the spacing), free on both axes: a wider card lengthens the bar, a taller one thickens it.
  Its menu: **Hide when idle** ([S-1464]). Tests `MusicProgressTests.S11330_`.

- **[S-11331]** **Its scrub is the view's** (root `SPEC.md`, [S-11970]): while the bar is scrubbed it says the track
  and the position under the pointer on the view's links (`ScrubbedTrack`; `MusicProgressFace.Scrub`), so the elapsed
  time beside it follows; committed or abandoned, it lets go. Tests `MusicProgressTests.S11331_`.

Needs a manual pass: seeking a live track (click, drag, a player that refuses), a stream with no end.
