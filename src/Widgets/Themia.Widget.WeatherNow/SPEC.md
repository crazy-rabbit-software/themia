# Weather now (WEATHER)

The module `weatherNow` ([S-6911]): the weather's **reading** where the user says they are, from
open-meteo: the sky, the temperature, the condition. An atom ([S-11170]..[S-11172]: the old card's
readings are the `weatherFacts` atom, the old card the ready-made group "Weather"); its id stays
`weatherNow`. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and what
the weather widgets share (the request [S-720], the header's place label [S-728], [S-2260], the stored
answers [S-729], Refresh [S-730], the place row [S-2840]..[S-2853], the glyphs [S-733]; the background
web sync [S-2663]; the family's blocks [S-6702] and the online widgets' state row [S-6734]).

Owning files: everything in this folder: `WeatherNowModule.cs` (descriptor, view factory, JSON
context), `WeatherNowConfig.cs` (with its styles), `WeatherNowView.cs` (it reads the reading through
`IWidgetHost.Weather`, the SDK's `SkyFeed`), `WeatherNowStyles.cs` (each style's sizes),
`WeatherNowMenu.cs` (its properties-menu lines), `Preview/` (Debug renders: `--state
sample|stale|failed`, the SDK's `SampleWeather` at `SamplePlace`), `Localization/Locales/` (its own
`WeatherNowStr`). Tests: `Tests/WeatherNowTests.cs`, `Tests/WeatherNowMenuTests.cs`,
`Tests/WeatherNowOpenTests.cs` (run by `tests/Themia.Modules.Tests`).

- [S-722]: moved with the readings into the `weatherFacts` module's `SPEC.md`.
- **[S-723]** *(Retired by [S-11100]: no ladder, no readings, no 40 step.)*
- **[S-11100]** **What it shows** is the reading and nothing else, the same at every size in its range:
  the temperature as the Readout on the family's 28 ([S-2449], [S-6702]) with its unit, the condition
  the Caption under it, the sky's glyph per [S-6160]'s style. The readings are the weather facts'
  ([S-11105]), the days the forecast's: never drawn here. A reading kept through a failed refresh
  stays and the header's problem glyph says so ([S-7042]); with none, the Status row at the top says why
  ([S-6734]). The header (by default) names it, the place its label ([S-2260]; none once cleared, [S-2848]).
- **[S-11101]** **Sizes** (`WeatherNowStyles.Sizing`, with its header): each style one height (the
  header's 36, its content, the inset 12): Standard 100 (the 28's line and the Caption under it),
  Large icon 100 (the 48 glyph beside a 52 block), Minimal 84 (the 28 alone); 140..320 wide, a new one
  180 (Large icon 200, Minimal 140): the widest reading ("-18°F", led by its glyph or beside the 48) fits
  140; past 320 the card is only empty. A condition longer than the card ends in its ellipsis.
- **[S-6160]** The reading's styles ([S-6000]), `WeatherNowConfig.Style` (`WeatherNowStyle`):
  **Standard** (the sky's glyph leading the temperature at its step, start-aligned), **Large icon**
  ([S-6161]) and **Minimal** ([S-6162]), in that order. Nothing is offered under Style.
- **[S-6161]** **Large icon**: the sky's glyph at 48 beside the temperature, the condition under the
  temperature, the pair at the content's top, centred unless the widget is aligned elsewhere ([S-6582]).
- **[S-6162]** **Minimal**: the temperature and its unit alone on 28, at the content's top, centred
  unless aligned elsewhere; no glyph, no condition.
- **[S-6174]** Its options are all on the properties menu ([S-3719]); it has no settings page. Style
  and Appearance ([S-3723]), then behind a divider **Location**, the weather family's search ([S-3722],
  `WeatherPlaceRows`, Clear location under it, [S-2848]), then behind a divider the links to what it
  follows and does not set ([S-3749], [S-3590]): **Temperature unit** (⚙ › Language & region) and
  **Update interval** (⚙ › Online updates › Weather). (Tests `WeatherNowMenuTests`.)
- **[S-2035]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `WeatherNowModule.Opens`); nothing while
  it has no place (it shows Set up then).

Needs a live pass: the widget against the real service, offline with a restored shelf, the first-run
desktop's bare large-icon reading.
