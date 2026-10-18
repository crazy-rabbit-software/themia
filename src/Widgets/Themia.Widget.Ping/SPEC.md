# Ping (meter)

The module `ping` ([S-6911]): one host's latency. Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the meters share (the meter framework [S-1120]..[S-1126], [S-1762],
[S-1167], the glides [S-6323], the system hub [S-2500]..[S-2513] with its one prober per host [S-2508]);
the probe's rules are the SDK's with their root tags ([S-109] the host checked at both boundaries,
`HostAddress` and the shared host row; [S-1170] the status, [S-1172] the chart, `PingStats`; [S-6263]
the signal bars, `PingSignal`).

Owning files: everything in this folder: `PingModule.cs` (descriptor, view factory, settings rows,
JSON context), `PingConfig.cs` (with its styles, its host and what it asks the hub, `IReadsSystem`),
`PingMeter.cs` (its half of the shared `MeterView`: the latency scale, the status dot, the title, the
Refresh command, the look per style), `PingSettings.cs` (its page: the host, then the rows every meter
ends with), `Localization/Locales/` (its own `PingStr`, 30 languages, its style names included). Tests:
its config under the root's rules is the module host's `PingHostTests`; its styles beside the other
I/O meters' are `IoStylesHostTests`.

- **[S-1174]** Ping "Refresh" on the card's menu probes now; a probe already out is the answer
  coming, so it is not doubled ([S-106]).
- **[S-1175]** Ping settings: Host, then the shared meter rows.
- **[S-6273]** Ping: `PingConfig.Style`, **Graph** ([S-1172]) and **Signal bars** ([S-6263]); the
  header's status dot and Refresh are the same in both.

Needs a manual pass: the meter on a live desktop (a host answering, one that does not, the network
going away, Refresh, both styles).
