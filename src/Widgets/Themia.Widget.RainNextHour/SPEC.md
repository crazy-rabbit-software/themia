# Rain next hour (ONLINE-2)

The module `rainNextHour` ([S-6911]): rain in the next two hours at a place. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the online widgets share (Air quality, rain
and exchange rates: the nowcast's wire and rules [S-5122]..[S-5124] in the SDK's `RainNowcast.cs`, the
header's age line [S-5160], the offline shelf [S-5161]; the background web sync [S-2663]).

Owning files: everything in this folder: `RainNextHourModule.cs` (descriptor, view factory, settings
rows, JSON context), `RainNextHourConfig.cs`, `RainNextHourView.cs` (it reads the nowcast through
`IWidgetHost.Weather`), `RainLayout.cs` (its ladder), `RainText.cs` (its words), `RainNextHourSettings.cs`
(its page: the weather family's `WeatherPlaceRows` and the source line), `Preview/` (Debug renders:
`--state sample|dry`), `Localization/Locales/` (its own `RainNextHourStr`, 30 languages). Tests:
`Tests/RainNextHourTests.cs` (run by `tests/Themia.Modules.Tests`); the nowcast's own tests stay Core's
(`OnlineWidgetsTests`, `S5122_`..`S5124_`).

- **[S-5120]** Rain in the next two hours at a place, a weather-family config like [S-5100], no
  setting beyond the place and the location name.
- **[S-5121]** `RainInterest`; the weather cadence ([S-3780]). The sentence is re-read from the held
  nowcast every minute, so "in 30 min" counts down between fetches.
- **[S-5125]** The bars are on a fixed scale (heavy rain fills the chart), a wet quarter never thinner
  than 8%, a dry one a hairline on the baseline; "Now", +1 h and +2 h under them, left to right in
  every language.
- **[S-5126]** Ladder (`RainLayout.Rain`): the sentence always, at the step its width allows
  (BodyStrong to Title; two lines on a tile too narrow for one), then the caption, then the bars
  where 32 DIP of them and their time line fit, taking the rest of the height.
- **[S-5127]** Settings: Place, Location name, and the source line.

Needs a live pass: the widget against the real service (a rainy hour, a dry one), offline with a restored shelf.
