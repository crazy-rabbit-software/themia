# Ping facts (PING-FACTS)

The family project `Themia.Widget.PingFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): a host's latency statistics, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the hub's one prober per host ([S-2508]), the host checked at both boundaries (`HostAddress`, the shared host line `PingHostLine`, [S-109]) and the status (`PingStats`, [S-1170]).

Owning files: everything in this folder: `PingKinds.cs`, `PingMetric.cs` (module and config bases, the part over the
host's prober, the JSON context), `PingWindow.cs` (the window's statistics and the sheet, `PingWindow`, `PingDetailRules`),
`Localization/Locales/` (its own `PingFactsStr`). Tests: `Tests/PingMetricTests.cs`.

- **[S-11023]** *(Retired by [S-14650]: no card of picked statistics.)*
- **[S-14650]** **A widget a statistic** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `pingAverage` "Average ping", `pingMinimum` "Fastest ping", `pingMaximum` "Slowest ping", `pingJitter` "Jitter", `pingLoss` "Packet loss", `pingAnswered` "Pings answered", the family **Ping facts** under System
  (`AddFolderId.PingFacts`). Each is its statistic over the window ([S-2428]); each probe back is a new reading. Trends
  ([S-14603]): average ping, fastest ping, slowest ping, jitter and packet loss an arrow and a line; pings answered none (a text, a constant or a time: no Trend line); a statistic's line is that statistic over the window ending at each of the latest
  probes (the prober's history is the series). Its options: Host, Statistics cover (the window), Trend. A placed
  `pingFacts` card is replaced in place by its rows, with its host and window ([S-14606]). Refresh probes now.
- **[S-2426]** Ping statistics: the hub's prober for the host ([S-2508]), its window the prober's history, one at a time, a late
  answer dropped, a host change clears the window ([S-106], [S-107]); the host line as the meter's.
- **[S-2427]** `Window`: the latest 5–500 probes (offered 10, 30, 60, 120, 300; default 60, held for one not offered).
- **[S-2428]** Over the window: min, average and max of the answers; jitter the mean absolute difference of consecutive answers
  (a loss between two does not break the pair; under two answers, none); loss the unanswered share of every probe.
- **[S-2429]** The sheet keeps the latest latency and the status for the note: before any probe is back a card's row is "--"
  with the status ("Checking…") its tooltip; no line names the host ([S-12801]: a group's Header widget does).
- **[S-2021]** A click on the card ([S-2000]) opens Windows' network status page (`ms-settings:network-status`); the module decides it (`PingMetricModule.Opens`).

Needs a manual pass: the cards on a live desktop (a host answering, a lossy one, the network going away, a window of 300 filling).
