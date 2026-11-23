# Music (Now playing)

The module `music` ([S-6911]): what Windows is playing, through its own media session, as **one widget** (owner,
2026-11-22: "i would rather have a few high quality versions of the now playing widget ... i would like to keep a
reasonable custmizability though, for the now playing widget for example i would like to be able to hide almost every
element separately, e. g. the pause play, next previous, artist, cover, etc. so the layouting engine underneath needs a
certain flexibility on this still."; later that day, of every widget: they "should ... keep their individuality", no
shared base; 2026-11-23: "now playing is still split into multiple widgets"). Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and the session's and the rules' root tags: hide when idle [S-1464], the seek bar's
mapping [S-1465], the keys [S-1468] and the album line [S-1469] are the SDK's `MusicRules`; the WinRT session manager is
the host's (`MediaSession`, behind `IWidgetHost.Media`), the widget's one connection to it the SDK's `NowPlayingFeed`; a
folded part keeping what it showed is the load's ([S-16511]).

Owning files: everything in this folder: `MusicModule.cs` (descriptor, view factory, menu lines, the parts it folds back,
JSON context), `MusicConfig.cs` (its styles, its element set and its repair, when it hides), `MusicPlan.cs` (the one plan,
pure: the shown set and the style to the face and its sizing; `MusicWords`, what the face says), `MusicView.cs` (the face
where the plan lays it, the session, the light, the art, the keys), `MusicMenu.cs` (its page's lines), `Samples/` (the add
page's picture), `Preview/` (Debug renders: `--state sample|playing|noArt|long|idle|noSession`), `Localization/Locales/`
(its own `MusicStr`, forty languages, its styles' and elements' names included). The seek bar (`MusicSeekBar`) and the
session's connection (`NowPlayingFeed`) are the SDK's. Tests: `Tests/MusicPlanTests.cs`, `Tests/MusicConfigTests.cs`,
`Tests/MusicMenuTests.cs`, `Tests/MusicOpenTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's
rules is the module host's `MusicHostTests`, the owner's desktop folding back `ResetFoldTests`.

- **[S-16500]** **One widget, a few styles** (`MusicConfig.Style`, `MusicStyle`, `"style": "side"`; [S-6000]): **Side
  cover** ([S-16503], a new card's), **Stacked** ([S-16504]), **Full art** ([S-16505]) and **Strip** ([S-16506]), each a
  layout of its own of the same track and controls, scaling with its card without changing its arrangement. It reads the
  session through one `NowPlayingFeed` for the whole widget: one media session, one snapshot, the cover, the texts, the
  times and the transport at the same moment. It starts without a header. Tests `MusicConfigTests.S16500_`.
- **[S-16501]** **Every element hidden on its own** (`MusicConfig.Shows`, `MusicElement`, stored as the set shown in its
  canonical order, `"shows": ["cover", "title", ...]`): the cover, the title, the artist, the album, the elapsed time, the
  progress bar, the length, previous, play/pause and next; a new card shows every one. On its page under the Style
  picker, three cards of switches: Track (the cover, where the style draws one: never in Strip; the title, the artist,
  the album), Progress (the elapsed time, the progress bar, the length) and Controls (previous, play or pause, next);
  then Hide when idle ([S-1479]). The last element shown cannot be switched off (`MusicElements.Toggle`); a stored set
  with an unknown or repeated element drops it, loudly, and an empty one is every element again (`MusicElements.Clean`).
  Tests `MusicConfigTests.S16501_`, `MusicMenuTests.S16501_`.
- **[S-16502]** **One plan, the shown set to the face and its sizing** (`MusicPlan`, pure). Every style is one block in
  the card's inset (12 on every side, never scaled), drawn at one scale: as large as the card's height allows, held by
  the width at the block's natural proportions (a larger card of the natural's shape draws the same face larger, its lines
  as long), and only at the floor by its least width (the column of texts, the progress line and the transport never
  narrower than 120, or its widest part); the texts and the bar take the width that scale leaves, so the block reaches the inset at both ends of the
  axis that stops it (WR-1..WR-3) and stands centred on the other ("Centred is fine"). The column: its head, the
  title (BodyStrong, primary) over the artist and album in one line (Body, secondary, whichever of the two shows), each cut
  with its ellipsis, and 12 right of them the transport (previous, play/pause as the one main action in the accent, next:
  28 icon buttons 8 apart, left to right in every language) at the column's end, centred down the texts ([S-16517]); 8;
  the progress line across the column (the elapsed time, 8, the seek bar, 8, the length; without the bar the two times
  with a "/" between them). **Nothing gives
  way** (WR-7, the audit of 2026-11-22: the transport had dropped out of a short card while the texts grew): every shown
  element is drawn at every size; the least card is the whole block at the legibility floor (a time's Caption at 9, the
  texts' Body at 9, a button's glyph at 12: 0.75 with times or buttons, WR-14), sized on the widest time there is (an
  hour-long track's "1:02:30") with a pixel to spare (the audit of 2026-11-23: an hour-long track at the least card and the
  strip at its least drew under the floors), the natural card the block at 1 with the
  style's column (300 beside a cover, 220 stacked, 276 in Full art; its texts the column less the transport's 112). **A hidden element closes its room**: its line, its
  gap and its share of the least and natural boxes go with it (a cover hidden is the column alone in every style; the
  transport alone is 124 × 52). A hover or a press never changes a part's place or size (WR-12): the plan is asked by
  the box, the style, the set and the times' template only. Tests `MusicPlanTests.S16502_` (every style × all, each
  element hidden, and several sets, at the least, natural, large and off-proportion boxes: every shown element drawn,
  inside the inset, none over another, the block on the inset, the natural at 1 and the least at the floor).
- **[S-17103]** **No taller than its block fills** (owner, 2026-11-23: "the now playing widget right now can be resized in the
  height as well for example, generally speaking this is good but the height resize should be limited to not exceed where
  the spacings at the bottom or top are not the same as they are in the other widgets too"; SPEC [S-17100]). Every style
  with a column is a face that scales as one (`MusicPlan.Sizing`, `WidgetSizing.ScalesAsOne`): its width stays the user's,
  its height stops where the block, scaled to fill the width, stands the inset from the card's top and foot (its natural
  less the inset, 376 × 64 beside a cover: at most 24 + 0.17 × (width − 24), the natural 400 × 88 the most at 400 wide,
  122 at 600, 156 at 800, in the whole cells that hold it); a wider card stands the block centred across. A cover alone
  stays a picture, its card the user's on both axes ([S-16507]). Tests `MusicPlanTests.S17103_`.
- **[S-16517]** **The transport stands right of the texts, right-aligned** (owner, 2026-11-23: "for the now playing
  widget move the action buttons on the right side of the text and right align them"). In the side cover, Stacked and
  Full art's foot the column's head holds the texts and, 12 right of them, the transport at the column's end, on the
  card's inset (Stacked: under the cover's right edge), centred down the texts' block; the progress line stands under
  them across the column. The strip keeps its transport at the line's end, on the inset, whatever room its line leaves.
  Hidden elements close up: no texts, the transport alone at the end; no transport, the texts take the width. A column
  with a transport spans its room ([S-16516] narrows only a column of texts alone). Tests `MusicPlanTests.S16517_`.
- **[S-16516]** **A face with nothing to take the width is as wide as its words** (the audit of 2026-11-23, its gap check
  measuring the face's parts: a title alone or the texts over the transport left the room after a short title as a band, up
  to 490 DIP, WR-3; a centred block stood on its boxes, its ink 5 DIP off centre, WR-1/WR-4). The view measures its texts
  uncut and its parts' ink (`MusicFit`); where nothing in the column or the strip takes the width it is given (no seek
  bar), the column is as wide as its widest text, its progress line or its transport, the block scales until that width
  or the height stops it, and it stands centred across. Down the block stands on its ink: the room over its first ink and
  under its last folds into the inset where the height stops it (a title's leading had stood its capitals 17 to 30 under
  the top), alike over and under where the width does; each text's first ink stands at its box's start (`Ink.InkStart`,
  its bearing taken at the scale drawn). With a bar, the bar takes the width as before; Full art's column stays on its art.
  Stacked with its cover stands on its ink too (the gap check of 2026-11-23: centred on its boxes, the times' room under
  their figures stood the foot 18 from the edge): the cover's top over the progress line's last ink. The face is laid twice
  in every box: by its parts' ink uncut, then by their ink as drawn in the boxes laid (a line cut short of its descenders
  stood 7 DIP high, stacked's narrow column), never more. Tests `MusicPlanTests.S16516_`.
- **[S-16503]** **Side cover**: the cover at the column's left as tall as the column (64 with every element shown, in the
  one radius), 12 from it the column. A new card 400 × 88; its least 226 × 73. Tests `MusicPlanTests.S16503_`.
- **[S-16504]** **Stacked**: the cover a square over the column, as wide as it (220), 12 over it; the transport at the
  column's end under the cover's right edge, beside the texts. The block keeps its proportions: centred across where the
  height stops it, down where the width does. A new card 244 × 320. Tests `MusicPlanTests.S16504_`.
- **[S-16505]** **Full art**: the cover is the card's band, edge to edge (cropped to fill; the card's corners); the column
  stands on the foot's inset across the card, over a scrim: the card's ground (the Solid role, its theme's colour) fading
  in from clear 24 over the column to 0.85 under it, so the texts keep their roles and read in either theme. The column
  grows as one block until the width or two thirds of the card's height stop it (the audit of 2026-11-23: it had kept to
  a fixed 0.4 of the height, 115 of 300 DIP at 900 × 300, WR-7), the art in sight over it; without a picture the music
  glyph stands in the art's room. A new card 300 × 224 (the art's room 136 over the column). Tests `MusicPlanTests.S16505_`.
- **[S-16506]** **Strip**: one line, no cover: the title and, after a dot, the artist and album (one Body line, the title
  semibold), the progress line (its bar 96) and the transport, 12 apart, each centred down the line, the transport at the line's
  end on the inset ([S-16517]); the line of text takes the width left (without one, the bar). A new card 506 × 52; its least 327 × 46. Tests `MusicPlanTests.S16506_`,
  `MusicMenuTests.S16506_`.
- **[S-16507]** **A cover alone** (nothing else shown, in any style): the picture is the card, edge to edge (the owner's
  exemption for a picture filling the card), a new card a square of 160, down to the grid's least widget (32); what
  a lone `musicCover` card becomes ([S-16513]). Tests `MusicPlanTests.S16507_`.
- **[S-16508]** **Nothing playing keeps the face** (WR-10, owner 2026-11-19: "it should show the default values"; the
  audit of 2026-11-22: the idle card had swapped its face for "Nothing playing" filling the card): the title's place says
  "--" (disabled) and the line under it why, in its Body ("Nothing playing", "Nothing is playing on this PC", "Reading
  the media session"); without that line why stands in the title's place. Both times say "-:--", the bar is empty, every
  button is there, disabled; the cover is the placeholder (the music glyph on the control fill). A stream with no end shows
  "-:--" for its length. `MusicWords`. Tests `MusicConfigTests.S16508_`.
- **[S-16509]** **The hover lights what opens** (WR-13; the audit of 2026-11-22: the hover lit nothing): the face is one
  item, the card edge to edge (`ItemMark`), whose light (the item layer's row fill, over the art and under the texts)
  shows at rest, under the pointer and pressed while a click opens the player ([S-2030]); with nothing to open it is
  clear. A button or the seek bar under the pointer lights alone: the face's light goes back to rest there. The item
  background reaches the light, the item text nothing (`IDrawsItems.DrawsItemText` false). It is no item of a list to the
  layout dump ([S-16514]): the checks measure the face's parts, and the cover is a picture (`PictureMark`). Renders' `--hover` light the
  face or a button's own hover state (a preview's bar seeks nothing and takes no pointer: the face lights over it). Tests `ItemKindsHostTests.S6570_S16509_`.
- **[S-16510]** **The times never move and are never cut** (WR-15, owner 2026-11-20: "the clock right now changes its
  layout depending on the time that it shows"; the audit of 2026-11-22: the length had dropped out of the least card and
  lost the tail of its "2" at rest, and moved under the pointer): the elapsed time and the length each stand in a box of
  the track length's template (every figure an 8: "8:88", "88:88" past ten minutes or for a stream, "8:88:88" past the
  hour) in Caption with a slack of 2, the elapsed time at the line's start, the length at its end (`HoldMark`: the gap
  check reads them by their template); the elapsed time ticks once a second while a track plays (from the session's
  anchor, and nothing at rest) and follows the pointer while scrubbing ([S-1465]); a new template (another track) lays
  the line again, a second never does. Tests `MusicPlanTests.S16510_`.
- **[S-16515]** **Every state in every style, and the element sets as states** (the audit of 2026-11-23: the other
  styles had been drawn in their sample alone, and only the whole set): the checks draw `music` in every state in every
  style (`EVERY_STATE_STYLES` in `scripts/renders.py`), and its Debug states hold the sets the folds and the page make,
  each the sample in that set and its card sized by it (`DrawnConfig`): `coverOnly`, `progressOnly`, `textsTransport`,
  `transportOnly`, `playOnly`, `titleOnly`. A sample's clock stands still (no tick while the feed is frozen), so a render of
  a playing track settles.
- **[S-16513]** **The cover and the progress fold back into it** (owner, 2026-11-23: "now playing is still split into
  multiple widgets"; [S-16230], [S-16511]): on load a placed `musicCover` or `musicProgress` (and a part of 21-22 Nov:
  `musicArtist`, `musicAlbum`, `musicPrevious`, `musicPlayPause`, `musicNext`, `musicElapsed`, `musicDuration`) that touches
  a `music` card, or another of them, becomes that card at the union of their boxes, showing every element its members
  showed (a `music` card from before the set: the texts and the transport); a lone one becomes a `music` showing just its
  own (a cover alone, [S-16507]; the elapsed time, the bar and the length). Its settings (Hide when idle) and its look
  stay the first card's. Tests `MusicConfigTests.S16513_`, `ResetFoldTests.S16513_`.
- **[S-11321]** **The picture is read again when the session's media properties change** (owner, 2026-11-21: "album
  art does not show the current album when playing a youtube video in the brave browser"). A browser hands over a
  video's picture a moment after its title, and may still hold the last video's when the title changes; the cover read
  the picture once per track and so kept none, or the last one. A snapshot says which reading of the media properties it
  is (`NowPlaying.Media`, counted by the host's `MediaSession` on every "media properties changed" notice and every change
  of the current session), and the cover reads the picture once per track and reading (`MusicWords.ArtKey`), where the
  style and the set draw a cover: the old picture stays until the new one is decoded, a reading with none shows the
  placeholder. Position and play/pause notices read nothing again. A preview card (the add page's picture, a render) reads
  no picture of the machine's, and a picture read before its sample took the face never lands over it. Tests `MusicConfigTests.S11321_`.
- **[S-1479]** Its options are all on its page ([S-3719], user request 2026-10-19); it has no settings page. Appearance
  ([S-3723]) and the Style picker with what the face shows ([S-16501]), then **Hide when idle** (Never / When no player
  is open / When nothing is playing, [S-1464]; edit mode still shows it). Tests `MusicMenuTests`.
- **[S-2030]** A click on the card ([S-2000]) opens the app that is playing: the media session's own app
  (`NowPlaying.AppId`, `SourceAppUserModelId`), a packaged app by its Apps-folder item, a desktop player brought to the
  front when it runs ([S-2003]); nothing with no session. The transport buttons and the seek bar keep their clicks
  ([S-2004]); the keys are [S-1468]'s. `MusicModule.Opens`.

Retired:

- **[S-650]** *(Retired by [S-1475], G5 2026-10-29: the transport is always the Controls row at the foot, so it never
  stands beside the lines.)*
- **[S-1475]** *(Retired with the split of 2026-11-12: the ladder of the art, the lines, the progress and the transport by
  the card's size; nothing gives way now, [S-16502].)*
- **[S-2155]** *(Retired with the split: the side cover is the Side cover style again, [S-16503].)*
- **[S-6406]** *(Retired with the split: the styles Artwork and text, Full artwork and Text only; the styles are
  [S-16500]'s.)*
- **[S-6407]** *(Retired with [S-6406]; Full art is [S-16505].)*
- **[S-11311]** *(Retired 2026-11-23, the owner: "now playing is still split into multiple widgets": the track atom, the
  title over the artist and album over the transport, one height; it is the one widget's column now, [S-16500],
  [S-16502].)*
- **[S-11320]** *(Retired 2026-11-23, the owner: "now playing is still split into multiple widgets": the cover alone as
  the widget `musicCover`, a square of 160 edge to edge; the cover is an element of the one widget, alone a picture filling
  the card ([S-16507]), and a placed one folds into it ([S-16513]).)*
- **[S-11330]** *(Retired 2026-11-23, the owner: "now playing is still split into multiple widgets": the progress alone as
  the widget `musicProgress`, the seek bar between the elapsed time and the length, one row; the progress line is the one
  widget's ([S-16502], [S-16510]), and a placed one folds into it ([S-16513]).)*
- **[S-11331]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single
  metric widgets stay")* Its scrub said on the view's links for a separate elapsed time.
- **[S-14920]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* A widget a part (the song title, the artist, the album, the three transport buttons, the
  elapsed time and the track's length as kinds of their own in a family Now playing); a placed part folds into the one
  widget on load ([S-16513]).
- **[S-14921]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The texts as text kinds.
- **[S-14922]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The buttons as icon kinds.
- **[S-15106]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The times as text kinds.
- **[S-15108]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* Every part an atom on the universal spacing.
- **[S-15109]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The elapsed time following a scrub through the view's links; the elapsed time
  follows its own scrub ([S-16510], [S-1465]).

Needs a manual pass: the widget on a live desktop (a player started, paused, skipped and sought, a stream with nothing to
seek, art and no art, each style, hide when idle, the hover over the face and over a button), and the owner's folded card.
