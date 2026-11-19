# Weather facts (WEATHER-FACTS)

The family project `Themia.Widget.WeatherFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the weather's
readings where the user says they are, **one widget a fact**. Atoms ([S-11170]): with the reading (`weatherNow`, "Weather
summary") they make the ready-made groups "Weather" and "Weather station" ([S-11172], [S-2474]). Its tags are this file's
and [S-722]; the root `SPEC.md` holds the host's sections and what the weather widgets share (the request [S-720], the
place [S-2840]..[S-2853], Refresh [S-730], the sync [S-2663]).

Owning files: everything in this folder: `WeatherFactKinds.cs` (the kinds: each one's module and config, its id, name,
description and search words), `WeatherFactModule.cs` (the family's descriptor, sizing, view factory, JSON context),
`WeatherFactConfig.cs`, `WeatherFactRules.cs` (the facts, their names and values), `WeatherFactView.cs` (the SDK's
`SkyFeed` and `FactList`), `WeatherFactMenu.cs`, `Samples/`, `Preview/` (Debug renders: `--state sample|stale|failed`,
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
- **[S-13901]** **What it shows** is its fact's row and nothing else, the row the facts widget drew ([S-6711]: its 16
  glyph, the name, the value at the end; `FactList`, [S-12811]), from the content's top, grown to the card ([S-12807]:
  no gap over it); a name or value too long ends in its ellipsis. **Size** (`WeatherFactModule.Sizing`): the inset 12,
  the 28 row, the inset 12 (52 tall); 200 wide at least, a new one 240; relaxed ([S-12411]); no steps and nothing to set
  by a resize.
- **[S-13902]** With no reading its row stays, its label and "--" drawn unavailable, and why is its tooltip
  ([S-12813]); a reading kept through a failed refresh stays and the status's problem glyph says so ([S-7042],
  [S-12481]: the place is the reading's to name), the row leaving the glyph its room at its end so the glyph never
  stands on the value. Every fact card at one place reads the reading's answer: one request
  ([S-2663]).
- **[S-13903]** Its options are all on the properties menu ([S-3719]): Appearance and **Location** (the weather
  family's search, [S-3722]); its fact is its kind, never a setting; the temperature unit and the update interval are
  ⚙'s, followed, no link row ([S-3749]). A click on the card opens the forecast for its place, as the reading's
  ([S-2035]); Refresh is on its card menu ([S-730]).

Needs a live pass: the cards against the real service, a group of fact cards placed and joined on the desktop.
