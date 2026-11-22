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

- **[S-15560]** **A day ahead** (built 2026-11-21 with the composed forecast, kept as the facts' own setting when the forecast
  went back to one widget, 2026-11-22): the facts that mean something for a day ahead take a day from today (`IDayChoice`,
  `Day`, 0 today; the properties menu's **Day** after the place, Today, Tomorrow, In n days up to 13, marked as the shared
  `day` setting): high and low, precipitation, wind, UV, sunrise and sunset; feels like, humidity, pressure, cloud cover and
  gusts are the present's and hold none (a day stored on one is today after repair, as is a day past the forecast's
  fourteen, `WeatherReport.ForecastDays`). On a day ahead a fact reads that day's forecast from the same reply as today's (the
  current request asks for and keeps fourteen days, [S-720]): the high and low, the chance of rain (else the amount), the
  strongest wind, the highest UV, the sun's times; a day past the reply is the em dash. A day ahead has no trend over the
  hours (they are today's) and its high and low's row is named after the day ("Tue").
- **[S-15561]** *(Retired 2026-11-21: the sky as a row of words; then the day part `weatherSky`, itself gone 2026-11-22 with
  the composed forecast.)*
- **[S-15562]** *(Retired 2026-11-21: the day as a metric row; then the day part `weatherDay`, itself gone 2026-11-22 with
  the composed forecast.)*

- **[S-13910]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Weather forecast (`weatherDays`), a container of day parts.
- **[S-13911]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its page (place, layout, spacing, start, days, Edit layout).
- **[S-13912]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its day box.
- **[S-13913]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its own view.
- **[S-13914]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Adding and removing parts.
- **[S-13915]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* A new forecast put together already.
- **[S-13916]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its day box and its size are one.
- **[S-13917]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its layout editing and the parts' shelf.
- **[S-13918]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Days, a count that scrolls.
- **[S-13919]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* The day parts (`weatherDay`, `weatherHigh`, `weatherLow`, `weatherSky` with a day).
- **[S-13920]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Temperature range (`weatherRange`).
- **[S-13921]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* A short text's card.
- **[S-13922]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; the weather forecast is one widget again, [S-6163].)* Its days' outlines in edit mode.

Needs a live pass: the cards against the real service, a group of fact cards placed and joined on the desktop.
