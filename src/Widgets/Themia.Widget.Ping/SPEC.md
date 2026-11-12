# Ping (reading)

The module `ping` ([S-6911]): one host's latency, the reading alone (the id stays the reading's; its
history is `pingHistory`, its statistics `pingFacts`, and the old cards' looks the ready-made groups
"Ping" and "Ping details"). Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the meters share (the meter framework [S-1120]..[S-1126], [S-1762],
[S-1167], the readouts' rule [S-6330], the system hub [S-2500]..[S-2513] with its one prober per host [S-2508]);
the probe's rules are the SDK's with their root tags ([S-109] the host checked at both boundaries,
`HostAddress` and the shared host line, `PingHostLine`; [S-1170] the status, [S-1172] the chart, `PingStats`; [S-6263]
the signal bars, `PingSignal`).

Owning files: everything in this folder: `PingModule.cs` (descriptor, view factory, menu lines,
JSON context), `PingConfig.cs` (with its styles, its host and what it asks the hub, `IReadsSystem`),
`PingMeter.cs` (its half of the shared `MeterView` on the SDK's `PingPart`, which the ping history
shares: the header's problem glyph, the host as the header's label ([S-2260]), the Refresh command),
`PingMenu.cs` (its properties menu: the host), `Localization/Locales/` (its own `PingStr`, 30 languages, its style names included). Tests:
its config under the root's rules is the module host's `PingHostTests`; its styles beside the other
I/O meters' are `IoStylesHostTests`.

- **[S-1174]** Ping "Refresh" on the card's menu probes now; a probe already out is the answer
  coming, so it is not doubled ([S-106]).
- **[S-1175]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then Host (the host measured at its end, the default resolver while none is typed;
  a click asks for another beside the menu, refused as typed when it is not a host, [S-109]).
- **[S-6273]** Ping: `PingConfig.Style`, **Number** (the latency at 28, [S-11001]: one height, 84) and
  **Signal bars** ([S-6263], [S-11004]: the bars under the latency, 24..48 tall as the card is); the
  header's problem glyph ([S-1170]) and Refresh are the same in both. The old Graph style is the Ping
  history atom. No tier ladder is left.
- **[S-2020]** A click on the card ([S-2000]) opens Windows' network status page
  (`ms-settings:network-status`); the module decides it (`PingModule.Opens`, through its part's
  `Opens`).

Needs a manual pass: the meter on a live desktop (a host answering, one that does not, the network
going away, Refresh, both styles).
