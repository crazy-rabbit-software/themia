# Weather summary (WEATHER, the family: the sky, the temperature and the condition)

The family project `Themia.Widget.WeatherNow` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the weather **now** where the
user says they are, from open-meteo, **a widget a part** (owner, 2026-11-21: "weather summary should also be split into
three widgets."). The **Weather summary** card (`weatherNow`: the sky's glyph, the temperature on the 28 and the condition
under it, in three styles) is gone; its three honest parts are **the sky** (`weatherSky`, an icon kind of the icon base,
root `SPEC.md` ICONS [S-15000]..[S-15006]), **the temperature** (`weatherTemperature`, the weather facts' single metric,
`Themia.Widget.WeatherFacts`, METRICS [S-14600]..; never a second temperature widget) and **the condition** (`weatherCondition`,
a text kind of the text base, TEXTS [S-14930]..). Its tags are this file's; the root `SPEC.md` holds the host's sections and
what the weather widgets share (the request [S-720], the place [S-728], [S-2260], the stored answers [S-729], Refresh [S-730],
the place row [S-2840]..[S-2853], the glyphs [S-733]; the background web sync [S-2663]; the in-place replacement [S-15022]).

Owning files: everything in this folder: `WeatherNowKinds.cs` (the kinds, their modules, the JSON context), `WeatherNowConfig.cs`
(the configs: the place), `WeatherNowFeed.cs` (the one connection to the place's reading, `IWidgetHost.Weather` through the
SDK's `SkyFeed`), `WeatherSkyView.cs`, `WeatherConditionView.cs`, `WeatherNowMenu.cs` (the place's lines), `Samples/` (the
sample day's reading at `SamplePlace`), `Preview/` (Debug renders: `--state sample|stale|failed`), `Localization/Locales/`
(its own `WeatherNowStr`). The split's data is the SDK's (`Themia.Sdk/Widgets/WeatherSummary.cs`), so both families that hold
a part declare one split. Tests: `Tests/WeatherNowTests.cs`, `Tests/WeatherNowMenuTests.cs` (run by
`tests/Themia.Modules.Tests`), `tests/Themia.Modules.Tests/WeatherNowSplitTests.cs`.

- [S-722]: moved with the readings into the weather facts family's `SPEC.md` (`Themia.Widget.WeatherFacts`).
- **[S-13904]** *(Retired by [S-15020]: there is no Weather summary widget; its name is the family's and the ready-made
  group's, [S-15024].)*
- **[S-723]** *(Retired by [S-11100]: no ladder, no readings, no 40 step.)*
- **[S-11100]**, **[S-11101]**, **[S-6160]**, **[S-6161]**, **[S-6162]**, **[S-12401]** *(Retired by [S-15020], owner
  2026-11-21: the card, its sizes and its three styles (Standard, Large icon, Minimal) are the parts; a look of the parts is
  their placing, the group's [S-15024].)*
- **[S-15020]** **A widget a part**, each over the same reading of its place (`WeatherNowFeed`; the weather facts read the
  same answer, so a group's parts agree): **Weather icon** (`weatherSky`), **Condition** (`weatherCondition`) and, from the
  weather facts, **Temperature** (`weatherTemperature`). The two of this project are at home in the add page's **Weather
  summary** family under Weather & sky (`AddFolderId.WeatherSummary`, the first of the subject, where the card stood), the
  sky first and one of Start's Popular (the card's place). Each config holds the place alone (`WeatherNowPartConfig`): no
  style, no unit, no cadence (followed, [S-3590]).
- **[S-15021]** **The sky** is an icon kind ([S-15003]): the condition's glyph (`WeatherGlyph`, [S-733]) filling its card
  ([S-15002]), the condition in words its tooltip; without a reading the weather's glyph drawn unavailable, why its tooltip
  ("Set a place", "Offline", WR-10) and its line under the glyph where the card has the room, a failure the status's problem
  glyph ([S-15710], [S-15711]); a reading kept through a failed refresh stays and the status's problem glyph says so
  ([S-7042]). Its colour is the item text's ([S-15001]).
  It takes a **Day** (`IDayChoice`: Today, or a day ahead; the shared `day` setting, after its place): a day ahead's sky
  is that day's forecast code (`WeatherReport.SkyCode`), unavailable past the forecast; what a weather forecast's day
  shows of the sky (the weather facts' [S-13919]).
- **[S-15025]** **A placed summary becomes its parts in place** (root `SPEC.md` [S-15022], `KindMerges`): the sky in its left
  third, its whole height; the temperature over the condition in the two thirds beside it, half its height each; each a copy
  with its id and the kind's (`w07-weatherSky`), its look and its place; its Minimal look (the temperature alone) the
  temperature alone, in its whole spot with its id. Its style goes; its alignment is the widget's and stays. The parts
  declare it (`WeatherSummary.Sky`, `.Condition`, `.Temperature`); once its display's metrics settle the three are laid
  so each keeps its minimum (the line beside the sky moved, else the sky stacked above the two), root `SPEC.md` [S-15026].
- **[S-15023]** **The condition** is a text kind ([S-14934]): "Partly cloudy", one line on a detail's tone (regular,
  secondary, [S-14933]: it was the caption under the temperature), no lead (the sky is its own widget), alone on its line
  (the place is the group's Header widget's to name, [S-12801]); with no reading "--" drawn unavailable, why its line at
  the card's foot where it has the room and its tooltip, a failure the status's problem glyph ([S-14932], [S-15713],
  [S-15710], [S-15711]); never the why in the condition's place.
- **[S-15024]** **The groups**: **Weather summary** (new: the sky on the left, as tall as the temperature's readout, two
  rows, over the condition's line beside it, one panel under its Header), and **Weather** and **Weather station** start
  with the same three parts where the card stood (`ReadyMadeGroups.Summary`); the first-run desktop's bare weather is the
  three parts ([S-893]); the Appearance page's bento holds the temperature where it held the card ([S-2311]).
- **[S-6174]** Its options are all on the properties menu ([S-3719]); it has no settings page. Appearance ([S-3723]), then
  behind a divider **Location**, the weather family's search ([S-3722], `WeatherPlaceRows`, Clear location under it,
  [S-2848]). What it follows and does not set, the temperature unit (⚙ › Language & region) and the update interval
  (⚙ › Online updates › Weather), has no line, no link row either ([S-3590], [S-3749]). (Tests `WeatherNowMenuTests`.)
- **[S-2035]** A click on a part ([S-2000]) opens MSN Weather's forecast for its place, in the UI language's market and
  the unit shown ([S-2006], `IconOpens.Weather`, `TextOpens.Weather`), as does its card menu's first line; nothing while it
  has no place (it shows Set up then).

Needs a live pass: the parts against the real service, offline with a restored shelf, the first-run desktop's bare parts,
and a state with a placed summary loading as its parts on the desktop.
