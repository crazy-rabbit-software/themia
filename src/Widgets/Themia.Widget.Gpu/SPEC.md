# GPU (reading)

The module `gpu` ([S-6911]): one graphics adapter's load, the reading alone (the id stays the reading's;
its history is `gpuHistory`, its engines `gpuEngines`, its details `gpuFacts`, and the old card's look the
ready-made group "GPU"). Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and what the meters share (the meter framework [S-1120]..[S-1126],
[S-1762], [S-1824], the styles' looks [S-6300]..[S-6305] and the gauge [S-6314], the readouts' rule [S-6330],
the system hub [S-2500]..[S-2513]); the sampler and the load's rules are the host's and the SDK's with
their root tags ([S-430] the tile and the adapter's short name, [S-431] DXGI and the counters, [S-434]
the busiest engine, [S-435] NVML, [S-6322] the engines, `GpuRules`), the adapter line the shared one
([S-2431], `GpuAdapterLine`).

Owning files: everything in this folder: `GpuModule.cs` (descriptor, view factory, menu lines,
JSON context), `GpuConfig.cs` (with its styles, its adapter and what it asks the hub, `IReadsSystem`),
`GpuMeter.cs` (its half of the shared `MeterView` on the SDK's `GpuPart`, which the GPU history and
engines share: the adapter's series, its name and whether it is there, the absent stand-in), `GpuMenu.cs`
(its properties menu: the adapter),
`Localization/Locales/` (its own `GpuStr`, 30 languages, its style names included). Tests:
`Tests/GpuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-432]** Pressure colours the trace, never the number: Caution from 70% load, Critical from 90%,
  the accent below ([S-1123]).
- **[S-433]** One adapter per widget (user decision; a laptop's two GPUs are two widgets):
  `GpuConfig.Adapter`, a DXGI position (software adapters left out). DXGI lists the adapter driving
  the primary display first, so the default 0 is that one, else the first; a negative position is
  repaired to 0, loudly. The Adapter choice on the properties menu names each adapter and is offered only with two or more
  (the GPU details' line, [S-2431]). No adapter at all, or a chosen one that is not there (an eGPU
  unplugged), draws the tile in the disabled look ([S-1940]): "--", the history a flat line at the
  middle, the gauge at the middle; the reason ("No graphics adapter",
  "This adapter is not connected") is the tile's tooltip. No panels, no stacking. Render `--state unavailable`.
- **[S-436]** Read on the hub's one cadence, every second ([S-2503]). Choosing another adapter
  watches that adapter's own series, so its history is its own.
- **[S-6320]** GPU styles, `GpuConfig.Style` (`GpuStyle`): **Number** (the reading alone, [S-11001]:
  one height, 84, 120..320 wide) and **Gauge** ([S-6321], [S-11003]). The old Graph and Engines styles
  are the GPU history and GPU engines atoms.
- **[S-6321]** The GPU is a meter as the CPU and memory are (`MeterView` with `GpuMeter`, user decision
  of the family's G1, 2026-10-29): its styles are the meters' looks ([S-6300]): the number and the
  Gauge (the load in the bowl, [S-6302]).
- **[S-1200]** Its options are on the properties menu ([S-3719]), no settings page: Appearance and the
  Adapter choice ([S-433]). The chart and its span are the GPU history atom's.
- **[S-1201]** The tile is a reading atom's face ([S-11001]): the load at 28 under the header, nothing
  else; no tier ladder is left.
- **[S-2014]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`, the system
  folder's `Taskmgr.exe`); the module decides it (`GpuModule.Opens`, through its part's `Opens`).

Needs a manual pass: the meter on a live desktop (a game or a video moving the load, each style, two
adapters and the choice between them, an eGPU unplugged).
