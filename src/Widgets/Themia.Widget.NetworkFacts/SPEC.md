# Network facts (networkFacts)

The module `networkFacts` ([S-6911]), which replaces networkDetails: down and up, the session's totals, the addresses and every
adapter, laid out as Task Manager's network page. Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the detail widgets share (the facts atoms [S-11007], the
system hub [S-2500]..[S-2513]); the sheet's rules are the SDK's `NetworkDetailRules` with their root
tags ([S-2415] the hero and the facts, [S-2416] the adapters' rows, [S-2418] only an address,
[S-2419] the local address), the throughput's `NetworkRules` ([S-1190], [S-1192]).

Owning files: everything in this folder: `NetworkFactsModule.cs` (descriptor, view factory, menu
lines, JSON context), `NetworkFactsConfig.cs` (the public address switch and what it asks the hub,
`IReadsSystem`), `NetworkFactsPart.cs` (its half of the shared `DetailView`), `NetworkFactsMenu.cs`
(its properties menu), `Localization/Locales/` (its own `NetworkFactsStr`, 30 languages). Tests:
`Tests/NetworkFactsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-11020]** Network facts: Local address, Received since start, Sent since start, Link speed,
  Public address, Download and Upload (the rates are the Network reading's, last); a new one shows the
  first four. The public address is never a default: picking it is what asks the public service
  ([S-2417]), and the menu's note under the pick says whom and how often. No hero; the adapter list is
  the Network adapters atom.
- **[S-2417]** The public address asks `api.ipify.org` through the host's one lookup, only while the pick
  shows it ([S-11020]); its options are on the properties menu ([S-3719]): Appearance, then Facts and
  the note saying whom it asks.
- **[S-2019]** A click on the card ([S-2000]) opens Windows' network status page
  (`ms-settings:network-status`) (a row's click stays the row's, [S-2004]); the module decides it
  (`NetworkFactsModule.Opens`, through its part's `Opens`).

Needs a manual pass: the widget on a live desktop (an adapter coming up and going away, the public
address switched on, offline).
