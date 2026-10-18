# Network (meter)

The module `network` ([S-6911]): the machine's network throughput. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the meters share (the meter framework
[S-1120]..[S-1126], [S-1762], the glides [S-6323], the system hub [S-2500]..[S-2513]); the throughput's
rules are the SDK's `NetworkRules` with their root tags ([S-1190] per adapter, [S-1192] the counted
adapters), the numbers face the SDK's (`MeterNumbers`, [S-6264]).

Owning files: everything in this folder: `NetworkModule.cs` (descriptor, view factory, settings rows,
JSON context), `NetworkConfig.cs` (with its styles and what it asks the hub, `IReadsSystem`),
`NetworkMeter.cs` (its half of the shared `MeterView`: the peak-scaled chart with its 1 KB/s floor,
the look per style), `NetworkSettings.cs` (its page: the rows every meter ends with),
`Localization/Locales/` (its own `NetworkStr`, 30 languages, its style names included). Tests: its
styles side by side with the other I/O meters' are the module host's `IoStylesHostTests`.

- **[S-1191]** Download is the reading; the chart's two series (download filled, upload the thin
  line) share one top: the window's peak of both, floor 1 KB/s (`RateWindow`), never the session's
  all-time peak.
- **[S-6270]** Network: `NetworkConfig.Style`, **Graph** ([S-1191]) and **Numbers** ([S-6264]).

Needs a manual pass: the meter on a live desktop (a download and an upload moving the chart, both styles).
