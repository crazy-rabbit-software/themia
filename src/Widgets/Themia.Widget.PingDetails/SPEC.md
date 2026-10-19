# Ping details

The module `pingDetails` ([S-6911]): latency statistics over a window of probes. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the
detail ladder [S-2400]..[S-2404], the system hub [S-2500]..[S-2513] and its one prober per host, which
the ping meter of the same host shares [S-2508]); the host checked at both boundaries is the SDK's
(`HostAddress`, the shared host line `PingHostLine`: [S-109]), the status the SDK's `PingStats` ([S-1170]).

Owning files: everything in this folder: `PingDetailsModule.cs` (descriptor, view factory, menu
lines, JSON context), `PingDetailsConfig.cs` (the host, the window and what it asks the hub,
`IReadsSystem`), `PingDetailPart.cs` (its half of the shared `DetailView`: the prober's history as the
window), `PingWindow.cs` (the window's statistics and the sheet, `PingWindow`, `PingDetailRules`),
`PingDetailsMenu.cs` (its properties menu: the host, then the window), `Localization/Locales/` (its own
`PingDetailsStr`, 30 languages). Tests: `Tests/PingDetailsTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-2426]** Ping statistics: the hub's prober for the host ([S-2508]), its window the prober's history, one at a time, a late
  answer dropped, a host change clears the window ([S-106], [S-107]); the host line as the meter's.
- **[S-2427]** `Window`: the latest 5–500 probes (offered 10, 30, 60, 120, 300; default 60, held for one
  not offered). Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, Host, then Window.
- **[S-2428]** Over the window: min, average and max of the answers; jitter the mean absolute difference
  of consecutive answers (a loss between two does not break the pair; under two answers, none); loss the
  unanswered share of every probe.
- **[S-2429]** The latest latency is the hero, the status (Online, Offline, Checking…) the caption; the
  header names the host. A lost probe dims the hero, its tooltip naming the host and the status.

Needs a manual pass: the widget on a live desktop (a host answering, a lossy one, the network going
away, a window of 300 filling).
