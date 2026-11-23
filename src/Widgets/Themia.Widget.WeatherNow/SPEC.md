# Weather summary (WEATHER)

The module `weatherNow` ([S-6911]), named **Weather summary** ([S-13904]): the weather's **reading** where the user
says they are, from open-meteo: the sky, the temperature, the condition. An atom ([S-11170]..[S-11172]: the old card's
readings are the weather facts, a widget a fact ([S-13900]), the old card the ready-made group "Weather"); its id stays
`weatherNow`. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and what
the weather widgets share (the request [S-720], the place [S-728], [S-2260], the stored
answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2853], the glyphs [S-733]; the background
web sync [S-2663]; the family's blocks [S-6702] and the online widgets' state row [S-6734]).

Owning files: everything in this folder: `WeatherNowModule.cs` (descriptor, view factory, JSON
context), `WeatherNowConfig.cs` (with its styles), `WeatherNowView.cs` (it reads the reading through
`IWidgetHost.Weather`, the SDK's `SkyFeed`), `WeatherNowStyles.cs` (each style's sizes),
`WeatherNowMenu.cs` (its properties-menu lines), `Preview/` (Debug renders: `--state
sample|stale|failed`, the SDK's `SampleWeather` at `SamplePlace`), `Localization/Locales/` (its own
`WeatherNowStr`). Tests: `Tests/WeatherNowTests.cs`, `Tests/WeatherNowMenuTests.cs`,
`Tests/WeatherNowOpenTests.cs` (run by `tests/Themia.Modules.Tests`).

- [S-722]: moved with the readings into the weather facts family's `SPEC.md` (`Themia.Widget.WeatherFacts`).
- **[S-13904]** Its name is **Weather summary** in the catalogue, the add page and the selection's page, in every
  language (owner, 2026-11-19: "the weather widget should be called weather summary or similar", when the weather facts
  became a widget a fact, [S-13900]); its id stays `weatherNow`. Test `WeatherNowTests.S13904_`.
- **[S-723]** *(Retired by [S-11100]: no ladder, no readings, no 40 step.)*
- **[S-11100]** **What it shows** is the reading and nothing else, the same at every size in its range:
  the temperature as the Readout on the family's 28 ([S-2449], [S-6702]) with its unit, the condition
  the Caption under it, the sky's glyph per [S-6160]'s style. The readings are the weather facts'
  ([S-11105]), the days the forecast's: never drawn here. A reading kept through a failed refresh
  stays and the status's problem glyph over its top end says so ([S-7042], [S-12481], [S-16433]); with none, the
  face stays and why is its compact line ([S-16432]). The condition stands alone on its line ("Overcast"): the place
  is the group's Header widget's to name, once ([S-12801], [S-9604]). The reading keeps the inset at its
  sides and foot, so it is centred in what the sizing counted ([S-12800]); in a group it stands against a
  side a mate joins, its foot's inset gone there ([S-12483]).
- **[S-11101]** **Sizes** (`WeatherNowStyles.Sizing`): each style one height (the inset 12, its
  content, the inset 12): Standard 76 (the 28's line and the Caption under it), Large icon 76 (the 48
  glyph beside a 52 block), Minimal 60 (the 28 alone); 140..320 wide, a new one
  180 (Large icon 200, Minimal 140): the widest reading ("-18°F", led by its glyph or beside the 48) fits
  140; past 320 the card is only empty. A condition longer than the card ends in its ellipsis.
- **[S-6160]** The reading's styles ([S-6000]), `WeatherNowConfig.Style` (`WeatherNowStyle`):
  **Standard** (the sky's glyph leading the temperature at its step, centred unless aligned elsewhere, [S-16454]),
  **Large icon**
  ([S-6161]) and **Minimal** ([S-6162]), in that order. Nothing is offered under Style.
- **[S-6161]** **Large icon**: the sky's glyph at 48 beside the temperature, the condition under the
  temperature, the pair in the content's middle ([S-12401]), centred unless the widget is aligned elsewhere ([S-6582]).
- **[S-6162]** **Minimal**: the temperature and its unit alone on 28, in the content's middle ([S-12401]), centred
  unless aligned elsewhere; no glyph, no condition.
- **[S-6174]** Its options are all on the properties menu ([S-3719]); it has no settings page. Style
  and Appearance ([S-3723]), then behind a divider **Location**, the weather family's search ([S-3722],
  `WeatherPlaceRows`, Clear location under it, [S-2848]). What it follows and does not set, the
  temperature unit (⚙ › Language & region) and the update interval (⚙ › Online updates › Weather), has no
  line, no link row either ([S-3590], [S-3749]). (Tests `WeatherNowMenuTests`.)
- **[S-2035]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherNowModule.Opens`); nothing while
  it has no place (it shows Set up then).
- **[S-16432]** *(Replaced by [S-16447]: why as a Caption line at the foot at its own size kept the block from filling its
  card, T36/B39 at 300 × 164, the line a 122 × 16 box under a 270 DIP cloud at 900 × 300.)* A released hold forgets its
  mark (`HoldMark.Forget`), so a readout held once is judged by what it draws again.
- **[S-16447]** **No reading keeps the face, filled as a reading's** (WR-10: "the widget keeps its face ... and says why
  in a compact line"; WR-1, WR-5: the block fills its card as the sample's does): the style's face stays
  (`WeatherNowStyles.Unread`): the sky unknown (the weather glyph leading the reading in Standard, beside it at 48 in Large
  icon), the reading "--" with its degree, disabled; the block keeps a reading's two lines, so it is drawn as large as the
  sample's, standing on the sky's ink and the line under's; Minimal, with no sky, holds its "--" to the widest reading
  (`ReadoutBlock.Hold`, "-18°F"), never blown up by its thin dashes. Not set up says nothing more (the card's Set up row). Test
  `WeatherNowTests.S16447_`. (Its why as the scaled line under is replaced by [S-16453].)
- **[S-16453]** **Why is a compact line in the line under's place** (WR-10: "says why in a compact line"; the third
  audit of 2026-11-23: as the scaled line under, "Can't reach the service" grew to 809 × 99 DIP at 900 × 300 and shrank
  the face, its glyph 64 against the sample's 94 at 300 × 164): the face keeps its line under empty
  (`WeatherNowStyles.UnderSlot`, a figure space: no ink, so the block keeps a reading's two lines and is drawn as large
  as the sample's), and why is one Caption line at its own size laid over that line's place on its baseline, under the
  face where its alignment puts it (`WeatherNowStyles.WhyLeft`: centred with a centred face, inside the card's insets),
  never past the card's inset, never part of the block, so it neither widens nor scales it; where that place is under a
  Caption line or under a sentence's start across (`WeatherNowStyles.WhyFits`), why is the face's tooltip alone (it
  always is the tooltip too). Minimal, whose sample draws no line under, keeps one with no reading for why to stand in.
  Test `WeatherNowTests.S16453_`.
- **[S-16454]** **The summary centres until an alignment is chosen** (docs/WIDGET-RULES.md, owner 2026-11-20: "Centred
  is fine", the leftover on the other axis shared alike on both sides): every style's default alignment is Center
  (Standard stood from the start, 253 DIP left over at its end at 350 × 70); beside the status mark ([S-16433]) a
  centred block keeps the mark's room at its start too, so it stays in the middle. Left and Right stay the person's
  choice ([S-6580]). Tests `WeatherNowTests.S16454_`, `ContentAlignmentTests.S6582_`.
- **[S-16433]** **The problem glyph never stands over the reading** (WR-14: a face scaled down stays read): a reading kept
  through a failed refresh shows the status's problem glyph over the card's top end corner; where the block would stand
  under it the block's room ends the glyph's width sooner, and where the room left beside it cannot hold the block at its
  floor (the least card, 39 × 32) the glyph gives way and its lines are the face's tooltip (`WeatherNowStyles.Mark`:
  Clear, Beside, Yields; `MetricRules.UnderMark`). Test `WeatherNowTests.S16433_`.
- **[S-16434]** **The card lights under the pointer** (WR-13, [S-15745]): it is one item that opens MSN Weather on a click
  ([S-2035]), so the item layer's hover fill lights it whole under the pointer and its pressed fill pressed (`OpensLight`),
  never in edit mode or while it has no place.
- **[S-12401]** **The reading fills the content** ([S-12400]): it is the content's one item, so a box
  taller than its style's height (whole grid cells) puts the room above and under it alike, never a band
  under it; across, it stands where its alignment says ([S-6583]).

- **[S-15020]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Weather summary is one widget again, [S-11100].)* A widget a part (`weatherSky`, `weatherCondition`, the facts' `weatherTemperature`).
- **[S-15021]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Weather summary is one widget again, [S-11100].)* The sky as an icon kind (`weatherSky`).
- **[S-15023]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Weather summary is one widget again, [S-11100].)* The condition as a text kind (`weatherCondition`).
- **[S-15024]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Weather summary is one widget again, [S-11100].)* The groups started with the three parts; they start with the summary again.
- **[S-15025]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Weather summary is one widget again, [S-11100].)* A placed summary became its parts in place; the parts now fold back into it ([S-16200]).
- **[S-16200]** **The parts fold back into one summary** (owner, 2026-11-22: "before 19th. but the single metric widgets
  stay"): a state file's placed `weatherSky` and `weatherCondition` (the kinds are gone) become one Weather summary in
  place, once per group of touching parts (cells that meet along an edge, chained), its box the union of theirs, its id and
  place the first part's in the drawing order, at the first part's place in that order (`KindMerges`, `MergeFrom` of the
  removed kinds, [S-14713]). A `weatherTemperature` that touches such a group folds in with it (the temperature was the
  summary's own reading, so no second one stays beside it, `MergeFrom.Joins`); one that touches none stays the single
  metric it is. The card is then fitted to the summary's sizing as every loaded card is. One logged line a fold. Tests
  `KindMergesTests.S16200_`, `WeatherNowFoldTests`.

Needs a live pass: the widget against the real service, offline with a restored shelf, the first-run
desktop's bare large-icon reading.
