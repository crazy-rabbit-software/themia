# Weather facts (WEATHER-FACTS)

The family project `Themia.Widget.WeatherFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the weather's
readings where the user says they are, **one widget a fact**. Atoms ([S-11170]): with the weather summary's sky and condition
(`weatherSky`, `weatherCondition`, whose third part the temperature is, [S-15020]) they make the ready-made groups "Weather
summary", "Weather" and "Weather station" ([S-11172], [S-2474], [S-15024]); a placed summary's temperature becomes
`weatherTemperature` in place, plain (no graph; the whole card where it was Minimal, [S-15025]). Its tags are this file's
and [S-722]; the root `SPEC.md` holds the host's sections and what the weather widgets share (the request [S-720], the
place [S-2840]..[S-2853], Refresh [S-730], the sync [S-2663]).

Owning files: everything in this folder: `WeatherFactKinds.cs` (the kinds as data, `MetricKind`: each one's id, row name,
name, description, search words, glyph and history; each one's module and config), `WeatherFactModule.cs` (the family's
module over the SDK's `MetricModule`: the place's line, the view, the JSON context), `WeatherFactConfig.cs`,
`WeatherFactRules.cs` (the facts, their values, numbers, histories and hourly figures), `WeatherFactView.cs` (the single
metric's `MetricView` over the SDK's `SkyFeed`: the reading and the place's hours), `Samples/`, `Preview/` (Debug renders: `--state sample|stale|failed`,
the same sample reading and place as the reading's), `Localization/Locales/` (its own `WeatherFactsStr`: the fact names
with their translations). Tests: `Tests/WeatherFactTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-722]** The readings (`WeatherFactKind`, in this order): Today (today's high and low, "H 16°  L
  8°"), Feels like, Humidity, Wind ("11 km/h NW"), Precipitation (the last hour's amount while it
  rains, else today's chance), Pressure, UV index, Cloud cover, Gusts, Sunrise, Sunset. A reading the
  service did not send is the em dash, never a row left out.
- **[S-11105]** *(Retired by [S-13900]: no widget of picked facts.)*
- **[S-11106]** *(Retired by [S-13901]: one row, no pick to size.)*
- **[S-11107]** *(Retired by [S-13903]: no Facts pick on the menu.)*
- **[S-12402]** *(Retired by [S-13901]: one row fills the card.)*
- **[S-13900]** **A widget a fact** (owner, 2026-11-19: "weather facts shows all the weather facts but these should
  actually be separate widgets, all of them, each of those widgets should basically just hold one of the rows that the
  weather facts widget held. the weather widget should be called weather summary or similar. with changes like that the
  amount of "widgets" in the library will explode, because later we will split other widgets into smaller peaces
  too."): each reading of [S-722] is a kind of its own, in the facts' order: `weatherHighLow` "High and low",
  `weatherFeelsLike` "Feels like", `weatherHumidity` "Humidity", `weatherWind` "Wind", `weatherPrecipitation`
  "Precipitation", `weatherPressure` "Pressure", `weatherUv` "UV index", `weatherCloudCover` "Cloud cover",
  `weatherGusts` "Gusts", `weatherSunrise` "Sunrise", `weatherSunset` "Sunset" (the fact's name, in every language
  but "High and low"), each with its own description, search words and glyph (its row's). One project holds them, a
  family ([S-6901]); on the add page they are one family, **Weather facts**, under Weather (`AddFolderId.WeatherFacts`,
  [S-3511]), each at home there alone. The widget of picked facts (`weatherFacts`) is gone: a placed one is dropped on
  load, not converted, with one logged line counting them ([S-6908]). Tests `WeatherFactTests.S13900_`,
  `UnknownWidgetTests.S13900_`.
- **[S-13901]** **What it shows** is its fact on the single metric's one face ([S-14601]: its 16 glyph, the name, the value at
  the end, the trend beside it), the row the facts widget drew ([S-6711], `FactList`, [S-12811]); a name or value too long
  ends in its ellipsis. **Size**: the single metric's ([S-14600]): its one row in whole grid cells from the card's top edge
  to its foot edge (30 on the default grid; it was 52, the 28 row centred in the inset over and under it, owner,
  2026-11-20: "the individual weather facts like pressure have a way to high height. the contents should also fit to the
  height so that the top and bottom spacing is the same as on other widgets."), 200 wide at least, a new one 240, the width
  relaxed ([S-12411]).
- **[S-13902]** With no reading its row stays, its label and "--" drawn unavailable, and why is its tooltip
  ([S-12813]); a reading kept through a failed refresh stays and the status's problem glyph says so ([S-7042],
  [S-12481]: the place is the reading's to name), the row leaving the glyph its room at its end so the glyph never
  stands on the value. Every fact card at one place reads the reading's answer: one request
  ([S-2663]).
- **[S-13903]** Its options are all on the properties menu ([S-3719]): Appearance, **Location** (the weather
  family's search, [S-3722]) and, where its fact has a history, **Trend** ([S-14608]); its fact is its kind, never a setting; the temperature unit and the update interval are
  ⚙'s, followed, no link row ([S-3749]). A click on the card opens the forecast for its place, as the reading's
  ([S-2035]; every kind declares it, `MetricOpens.Weather`, [S-14706]), and so do Enter and the card menu's first line, "Open on MSN
  Weather" ([S-14707]); Refresh is on its card menu ([S-730]). While its trend is Line, its graph's **Chart type** (Area, Line,
  Bars) and **Detail** stand under Trend ([S-14703]).

- **[S-14608]** **Trend** ([S-14603]; owner, 2026-11-20: "you should also have the option to show trendlines as well or
  just the trend with arrows."): feels like, humidity, wind, precipitation, pressure, UV and gusts draw an arrow and a
  line over the place's hours (the weather graphs' one hourly answer, asked only while a trend over them is drawn): the
  arrow this hour against the last ("previous hour"), rounded as shown, and the line the hours from the last one on (where
  it was and where it goes); cloud cover, which the hours do not carry, an arrow from the last two readings the card saw;
  today's high and low and the sun's times none (no Trend line).
- **[S-13905]** **The weather graphs are these metrics' graphs** ([S-14710], [S-14713]): every fact with hours has its
  graph over the place's hours (Area, Line or Bars; 12, 24 or 48 hours, a day by default), by default as the graph it merged
  drew (the wind, the gusts and the pressure a line, the precipitation and the UV columns, the rest the area). **Temperature**
  (`weatherTemperature`, the temperature now, "14°", first in the facts' order) is the temperature graph's metric; a placed
  temperature, humidity, wind, pressure, UV or rain graph is its fact with its graph shown ([S-14713]).

- **[S-15560]** **A day ahead** ([S-15513], the repeater's day series): the facts that mean something for a day ahead
  take a day from today (`IDayChoice`, `Day`, 0 today; the properties menu's **Day** after the place, Today, Tomorrow,
  In n days up to 13, marked as the shared `day` setting): high and low, precipitation, wind, UV, sunrise and sunset
  (and the day parts, [S-13919]); feels like, humidity, pressure, cloud cover and gusts are the present's and hold none (a day
  stored on one is today after repair, as is a day past the forecast's fourteen). On a day ahead a fact reads that day's
  forecast from the same reply as today's (the current request carries fourteen days): the high and low, the chance of
  rain (else the amount), the strongest wind, the highest UV, the sun's times; a day past the reply is the em dash. A day
  ahead has no trend over the hours (they are today's) and its high and low's row is named after the day ("Tue").
- **[S-15561]** *(Retired 2026-11-21 by [S-13919]: the sky as a row of words is gone; a day's sky is the day part `weatherSky`, an
  icon.)*
- **[S-15562]** *(Retired 2026-11-21 by [S-13919]: the day as a metric row is gone; a day's name is the day part `weatherDay`, a
  text.)*

- **[S-13910]** **Weather forecast** (`weatherDays`; owner, 2026-11-21: "you should be able to change the row / column /
  whatever layout in the settings of the weather forecast, the layouting should also be done on the same grid that lays
  over the desktop too"; "the weather forecast widget should not have a separate background, it should be transparent"):
  the days ahead, each day a composition the person makes of real widgets, the day facts on the single metric's face
  ([S-14600]) and the design elements ([S-10100]), laid in its day box on the desktop's grid and drawn again for every day
  it shows (the engine: root SPEC REPEATER). It has no body and no background of its own, ever (a fill set on it is put
  back to none); its days are its parts' cards. On the add page among the weather widgets.
- **[S-13911]** **Its page** holds what is set once: its place (the weather family's, [S-2840]: every part shows it,
  [S-15529]), its **Layout** (Rows: days down; Columns: days across; Grid: across, then down), its **Spacing** in grid
  cells (Grid: row and column spacing), when it **Starts** (Today, Tomorrow, In N days), its **Days** ([S-13918]) and
  **Edit layout** ([S-13917]).
- **[S-13912]** **Its day box** is one day's size in grid cells, repeated (owner: "One day's box, repeated"; "i should also
  be able to change the size of the row inside of there"): resized on the desktop by its frame's grips in its layout
  editing ([S-15526]).
- **[S-13913]** **Its own view** draws nothing but, with no part yet, how to add one ("Click again to edit its layout"). It
  shows no Set up row and no waiting row, and neither do its parts and their days (`IWidgetCardHost.HeldByContainer`): what
  they lack is set on its page, and they show their own empty reading ("--", the unknown sky, the bare track).
- **[S-13914]** **Adding and removing parts** happens on the desktop, in its layout editing ([S-13917]); its page never
  lists them. A new part is a single metric at its least, anything else at its natural, held to the day box and never under
  its own least, on whole cells.
- **[S-13915]** **A new forecast is put together already** (owner, 2026-11-21: "create a good looking weather forecast
  widget based on this system that can be edited afterwards"; `WeatherDaysConfig.FirstParts`, laid by `Repeaters.Seed`): a
  row a day of the day's name (9 cells, at its start), its sky (4), its low (7, at its end), its range bar (10) and its high
  (7, at its start), four cells tall, side by side from its corner; its width its day's, its height a week of them. The add
  page's picture draws it so. Every part is a widget: moved, resized, removed or joined by others in its layout editing.
  The Weather station's days ahead are a forecast from tomorrow of one high and low part ([S-11172]).

- **[S-13916]** **Its day box and its size are one**: the engine's [S-15535] (Rows: its width its day's; Columns: its
  height). Tests `WeatherDaysTests.S13916_`.
- **[S-13917]** **Its layout editing** ([S-15526]) and **the parts' shelf** (owner: "the atoms that can be added to the
  forecast widget should be shown in a bento to the side in the row edit mode and should be addable by just dragging them
  into it. when you drag them out of it they are removed"; "the widget atoms should be shown as preview in the edit mode of
  the forecast"): the shelf of the engine's [S-15537], on the desktop's grid: each kind its day takes as a real card at the
  size it lands with, its name under it, its heading "Drag onto a day" and Done. A part dragged onto any of its days is
  added at the same place in the day box (its middle under the pointer, on whole cells) and selected; let go elsewhere,
  nothing. A part dragged out of the day is muted while it is out and removed if let go there; one undo step either way.

- **[S-13918]** **Days** (owner, 2026-11-21: "you should be able to set how many items the weather forecast should show.
  it should then get scrollable if it overflows"): how many days it shows from its first, 1 to 14, a week by default; more
  than its card holds scroll ([S-15536]). The one count that is a setting ([S-14410] holds for every other).
- **[S-13919]** **The day parts** (owner, 2026-11-21: "it should actually be based on the text and icon widgets of the
  existing atoms"): what a day is put together of, each a widget of its own with the weather family's place and a Day
  (`IDayChoice`), both the forecast's while it holds one ([S-15529]): **Day** (`weatherDay`: "Today" or the weekday), **High**
  (`weatherHigh`: "16°") and **Low** (`weatherLow`: "8°", the secondary role) on the one text face ([S-14930], aligned as
  any text, [S-6583]); **Sky** (`weatherSky`) on the one icon face (`GlyphFace`: the day's sky as its glyph, scaled to its
  box, a fifth of its shorter side round it). With no reading: "--" and the unknown sky, unavailable, why in the tooltip.
  They replace the metric kinds Sky and Day ([S-15561], [S-15562]). Rules `DayParts`, tests `WeatherDaysTests.S13919_`.
- **[S-13920]** **Temperature range** (`weatherRange`; owner, 2026-11-21: "add a temperature range bar component to the
  forecast as well"): the day's low to high on the family's bar (`ChartBar`), along its box's long side (down a box taller
  than wide, the low at its foot), on the scale of every day the reading holds (the lowest low to the highest high), so a
  column of them compares; the track alone with no reading (`DayParts.Range`).
- **[S-13921]** **A short text's card** (`TextKind.Width`, `TextRules.ShortWidth`): a day part's text is new at 80 wide
  (the narrowest a widget is new at) and scales down from there as any text face ([S-12833]), so it stands in a day's row.
- **[S-13922]** **Its days' outlines** in edit mode are drawn by the surface, where the cards are (a dashed hairline round
  each day it shows, on device pixels; owner, 2026-11-21: "the dashed lines around the individual rows are off"), never by
  its own view.

Needs a live pass: the cards against the real service, a group of fact cards placed and joined on the desktop.
