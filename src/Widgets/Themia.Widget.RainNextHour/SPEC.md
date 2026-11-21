# Rain (ONLINE-2: the sentence, the graph and the total)

The family project `Themia.Widget.RainNextHour` ([S-6901], `[assembly: ThemiaWidgetFamily]`): rain in the next two hours
at a place, **a widget a part** (owner, 2026-11-21: "split up rain next hour into three widgets too."): the sentence a
text kind (root `SPEC.md`, Text widgets (TEXTS)), the graph its own, the total a single metric (METRICS). Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality, rain
and exchange rates: the nowcast's wire and rules [S-5122]..[S-5124] in the SDK's `RainNowcast.cs`, the
stale rule [S-5160], the offline shelf [S-5161]; the background web sync [S-2663]).

Owning files: everything in this folder: `RainNextHourModule.cs` (the kinds, the three modules, the JSON context),
`RainNextHourConfig.cs` (the configs over one `RainConfig`), `RainNextHourView.cs` (the graph; it reads the nowcast
through `IWidgetHost.Weather`), `RainParts.cs` (the sentence's and the total's views over one `RainFeed`), `RainLayout.cs`
(the graph's layout and size), `RainText.cs` (the words), `RainNextHourMenu.cs` (the properties menu: the weather
family's `WeatherPlaceRows.LocationLine` and the source line), `Samples/` (`RainSample`, the one sample nowcast),
`Preview/` (Debug renders: `--state sample|dry|stale`, the sentence and the total `failed` too),
`Localization/Locales/` (its own `RainNextHourStr`). Tests:
`Tests/RainNextHourTests.cs`, `Tests/RainNextHourMenuTests.cs` (run by `tests/Themia.Modules.Tests`); the nowcast's own tests stay Core's
(`OnlineWidgetsTests`, `S5122_`..`S5124_`).

- **[S-5120]** Rain in the next two hours at a place, a weather-family config like [S-5100] (one `RainConfig` for the
  three parts), no setting beyond the place and the location name.
- **[S-14940]** **A widget a part**, in the family **Rain** under Weather (`AddFolderId.Rain`), in this order:
  `rainOutlook` **Rain next hour** (the sentence, [S-14942]), `rainNextHour` **Rain by the quarter hour** (the graph, the old
  card's id: a placed one is the graph now, its place kept; nothing converted, [S-14941]) and `rainTotal` **Rain total**
  ([S-14943]). Each asks for the same nowcast of its place (`RainInterest`, [S-5121]) and re-reads it every minute, so the
  three agree; each opens the place's forecast ([S-2037]) and has Refresh on its card menu.
- **[S-14941]** **The graph** is the old card less its sentence and caption: the columns and the window's ends
  ([S-5125], [S-5126]), the inset over the columns' top; why there is no rain to draw (the headline, or the answer's state)
  is its tooltip. (It moves onto the metric base's graph later.)
- **[S-14942]** **The sentence** (a text kind, [S-14934]): the headline ([S-5124]: "Rain in 15 min", "Rain stops in 30
  min", "Rain for the next 2 hours", "No rain in the next 2 hours"), on two lines at most, no lead glyph (it says "rain"
  itself), primary; a window too short to speak for the hour ("No forecast for the next hour") and no answer at all (its
  state, "Set a place", "Offline") drawn unavailable, the face kept ([S-14932]). `RainOutlookView.Shown`.
- **[S-14943]** **The total** (a single metric, [S-14604]): the millimetres the window holds ("1.2 mm": a tenth under 10,
  whole above, as the forecast writes an amount; "0 mm" dry), named "Next 2 hours" under the rain's glyph, no trend
  (`MetricHistory.None`); a window too short to speak for the hour or no answer "--", why its tooltip ([S-14602]). The
  heaviest quarter's intensity and the hour's chance, the old caption's other parts, are gone: the graph's columns are the
  intensity, the weather's precipitation fact the chance. `RainText.Total`.
- **[S-5121]** `RainInterest`; the weather cadence ([S-3780]). The sentence is re-read from the held
  nowcast every minute, so "in 30 min" counts down between fetches.
- **[S-5125]** The bars are the one chart's columns ([S-6750], `RainLayout.Chart`), one per quarter hour in equal
  cells across the card, edge to edge ([S-6754]), filled from the line's foot on a fixed scale (heavy rain fills the
  chart), a wet quarter never under 8 %, a dry one none (with none, the foot's hairline); the window's two ends under
  them in Caption tertiary ("Now" and the time two hours on, the chart's ends at the inset off the foot), left to right
  in every language.
- **[S-5126]** The graph's layout (`RainLayout`): the plot is the card's whole box ([S-6754]: the view owns its top edge);
  the inset over the bars, 48 tall at the graph's height (taller in a taller card: the plot takes the box), and their
  time line 4 under them, at the inset off the foot. A dry window, one too short to speak for the hour, or no answer at
  all keeps the chart, its foot's hairline and its time line ([S-6753]: never no chart). *(The sentence and its caption
  over the bars moved out with the split, [S-14940].)*
- **[S-11140]** **An atom** ([S-11170]): the bars, the same at every size in its range (no ladder, no part that comes or
  goes with the box). **Sizes** (`RainLayout.Sizing`): one height (92), 220..420 wide, a new one 300: the bars get wider. It follows the
  sync through the SDK's `SkyFeed` ([S-11171]).
- **[S-5127]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), then behind a divider each **Location** (the weather family's search, [S-2840],
  [S-3722]) and the source's credit, a note ("Source: Open-Meteo"). How often the weather is asked is
  ⚙ › Online updates', followed, no link row ([S-3590], [S-3749]).
- **[S-2037]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `RainNextHourModule.Opens`); nothing
  while it has no place (it shows Set up then).

Needs a live pass: the three widgets against the real service (a rainy hour, a dry one), offline with a restored shelf.
