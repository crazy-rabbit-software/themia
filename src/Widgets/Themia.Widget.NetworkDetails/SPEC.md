# Network details

The module `networkDetails` ([S-6911]): down and up, the session's totals, the addresses and every
adapter, laid out as Task Manager's network page. Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the detail widgets share (the detail ladder [S-2400]..[S-2404], the
system hub [S-2500]..[S-2513]); the sheet's rules are the SDK's `NetworkDetailRules` with their root
tags ([S-2415] the hero and the facts, [S-2416] the adapters' rows, [S-2418] only an address,
[S-2419] the local address), the throughput's `NetworkRules` ([S-1190], [S-1192]).

Owning files: everything in this folder: `NetworkDetailsModule.cs` (descriptor, view factory, menu
lines, JSON context), `NetworkDetailsConfig.cs` (the public address switch and what it asks the hub,
`IReadsSystem`), `NetworkDetailPart.cs` (its half of the shared `DetailView`), `NetworkDetailsMenu.cs`
(its properties menu), `Localization/Locales/` (its own `NetworkDetailsStr`, 30 languages). Tests:
`Tests/NetworkDetailsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-2417]** `ShowPublicAddress` (off by default: it asks a third party) asks `api.ipify.org` through the
  guarded `WebFetch` client, only while such a widget lives. Its options are on the properties menu
  ([S-3719], user request 2026-10-19), no settings page: Appearance, then the Public address switch
  and under it, greyed, whom it asks and how often.

Needs a manual pass: the widget on a live desktop (an adapter coming up and going away, the public
address switched on, offline).
