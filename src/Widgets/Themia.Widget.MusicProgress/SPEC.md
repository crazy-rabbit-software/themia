# Track progress (musicProgress)

The module `musicProgress` ([S-6911]): how far into what Windows is playing it is, an atom of the old
Now playing card (owner decision 2026-11-12). Its tags are [S-11330]..[S-11339], this file's alone;
the root `SPEC.md` holds the host's sections; the seek bar's mapping [S-1465] and the clock are the
SDK's `MusicRules`, the seek bar the SDK's `MusicSeekBar`, the session's connection `NowPlayingFeed`
([S-11311]).

Owning files: everything in this folder: `MusicProgressModule.cs` (descriptor, view factory, menu
lines, JSON context), `MusicProgressConfig.cs`, `MusicProgressFace.cs` (its row and sizing),
`MusicProgressView.cs`, `Preview/` (Debug renders: `--state sample|hovered`), `Localization/Locales/` (its own
`MusicProgressStr`; the hide-when-idle words copied with their translations from `music`). Tests:
`Tests/MusicProgressTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-11330]** **The progress alone** (add menu: Media): one row in the card's inset, the elapsed
  time and the total in Caption, tertiary, at its two ends in columns as wide as their widest case
  (every figure an 8, so the bar does not move as the seconds go by), the seek bar between them 8
  from each (its rail the Track role, its fill and thumb the accent; click to jump, drag to scrub,
  [S-1465]), left to right in every language. A larger card scales the row up as one, the bar never narrower than
  in a card of the natural width, the row centred in the height when the width holds it ([S-12814]). The pointer on the bar shows its thumb and changes nothing else: the row keeps its scale and place ([S-12816]). A stream with no end shows no total; no track, an empty
  rail. The elapsed time ticks once a second while a track plays, from the session's anchor, and
  nothing at rest. Its card (`MusicProgressFace.Sizing`): the inset, the row, the inset (40), one
  height, 200 to 520 wide (a new one 340), where a wider card only lengthens the bar. It starts
  without a header. Its menu: **Hide when idle** ([S-1464]). Tests `MusicProgressTests.S11330_`.

- **[S-11331]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* Its scrub said on the view's links for a separate elapsed time: the elapsed time is its own
  again and follows the pointer while scrubbing ([S-11330]).

Needs a manual pass: seeking a live track (click, drag, a player that refuses), a stream with no end.
