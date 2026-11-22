# Rain next hour (ONLINE-2)

The module `rainNextHour` ([S-6911]): rain in the next two hours at a place. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality, rain
and exchange rates: the nowcast's wire and rules [S-5122]..[S-5124] in the SDK's `RainNowcast.cs`, the
stale rule [S-5160], the offline shelf [S-5161]; the background web sync [S-2663]).

Owning files: everything in this folder: `RainNextHourModule.cs` (descriptor, view factory, menu
lines, JSON context), `RainNextHourConfig.cs`, `RainNextHourView.cs` (it reads the nowcast through
`IWidgetHost.Weather`), `RainLayout.cs` (its layout and size), `RainText.cs` (its words), `RainNextHourMenu.cs`
(its properties menu: the weather family's `WeatherPlaceRows.LocationLine` and the source line), `Preview/` (Debug renders:
`--state sample|dry`), `Localization/Locales/` (its own `RainNextHourStr`, 30 languages). Tests:
`Tests/RainNextHourTests.cs`, `Tests/RainNextHourMenuTests.cs` (run by `tests/Themia.Modules.Tests`); the nowcast's own tests stay Core's
(`OnlineWidgetsTests`, `S5122_`..`S5124_`).

- **[S-5120]** Rain in the next two hours at a place, a weather-family config like [S-5100], no
  setting beyond the place and the location name.
- **[S-5121]** `RainInterest`; the weather cadence ([S-3780]). The sentence is re-read from the held
  nowcast every minute, so "in 30 min" counts down between fetches.
- **[S-5125]** The bars are the one chart's columns ([S-6750], `RainLayout.Chart`), one per quarter hour in equal
  cells across the card, edge to edge ([S-6754]), filled from the line's foot on a fixed scale (heavy rain fills the
  chart), a wet quarter never under 8 %, a dry one none (with none, the foot's hairline); the window's two ends under
  them in Caption tertiary ("Now" and the time two hours on, the chart's ends at the inset off the foot), left to right
  in every language.
- **[S-5126]** The layout (`RainLayout`): the plot is the card's whole box ([S-6754]: the view owns its top edge);
  over its top, at the inset, the sentence on one Title line (its ellipsis where the card is narrow); then the caption
  (Body, secondary: the heaviest rain, the total, the chance; its ellipsis too); then 8 down the bars, 48 tall at the
  atom's height (taller in a taller card: the plot takes the box), and their time line 4 under them, at the inset off
  the foot. A dry window, one too short to speak for the hour, or no answer at all keeps the chart, its foot's hairline
  and its time line, with its one plain sentence ("No rain in the next 2 hours", the answer's state) as the caption
  over them in place of the sentence and its caption ([S-6753]: never no chart).
- **[S-11140]** **An atom** ([S-11170]): the sentence, its caption and the bars, the same at every size
  in its range (no ladder, no part that comes or goes with the box). **Sizes** (`RainLayout.Sizing`):
  one height (148), 220..420 wide, a new one 300: the bars get wider. It follows the
  sync through the SDK's `SkyFeed` ([S-11171]).
- **[S-5127]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), then behind a divider each **Location** (the weather family's search, [S-2840],
  [S-3722]) and the source's credit, a note ("Source: Open-Meteo"). How often the weather is asked is
  ⚙ › Online updates', followed, no link row ([S-3590], [S-3749]).
- **[S-2037]** A click on the card ([S-2000]) opens MSN Weather's forecast for the widget's place,
  in the UI language's market and the unit shown ([S-2006], `RainNextHourModule.Opens`); nothing
  while it has no place (it shows Set up then).

- **[S-14940]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Rain next hour is one widget again, [S-5126].)* A widget a part, the family Rain under Weather.
- **[S-14941]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Rain next hour is one widget again, [S-5126].)* The graph, the old card less its sentence and caption.
- **[S-14942]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Rain next hour is one widget again, [S-5126].)* The sentence as a text kind (`rainOutlook`).
- **[S-14943]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay"; Rain next hour is one widget again, [S-5126].)* The total as a single metric (`rainTotal`, made by the split; no metric kind of it before).
- **[S-16201]** **The parts fold back into one card** (owner, 2026-11-22: "before 19th"): a state file's placed `rainOutlook`
  and `rainTotal` (the kinds are gone) fold into Rain next hour in place, once per group of touching parts and rain cards
  (cells that meet along an edge, chained): one card at the union of their boxes, the group's rain card keeping its id and
  place, else its first part's (`MergeFold.Touching`, root [S-16230]); it is then fitted to the card's one height as every
  loaded card is. Test `RainNextHourTests.S16201_`.

Needs a live pass: the widget against the real service (a rainy hour, a dry one), offline with a restored shelf.
