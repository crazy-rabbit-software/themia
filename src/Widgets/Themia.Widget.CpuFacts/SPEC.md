# CPU facts (cpuFacts)

The module `cpuFacts` ([S-6911]), which replaces CPU details: the processor's facts as fact rows. Its
tags are this file's alone; the root `SPEC.md` holds what the facts atoms share ([S-11007]) and the
sheet's rules, the SDK's `CpuDetailRules` with their root tags ([S-2405] the facts, [S-2406] the clock,
[S-2434]..[S-2436] and [S-2407] the temperature), read by the host's processor source. The old card's
look is the ready-made group "CPU details" ([S-11090]).

Owning files: everything in this folder: `CpuFactsModule.cs` (descriptor, menu line, JSON context),
`CpuFactsConfig.cs` (the pick, what it asks the hub), `CpuFactsPart.cs` (its half of the shared
`DetailView`: the three feeds held, the sheet built from one tick's figures), `Localization/Locales/`
(its own `CpuFactsStr`). Tests: `Tests/CpuFactsTests.cs`.

- **[S-11012]** CPU facts reads the hub's load, counts and processor feeds (`SystemSource.Cpu`,
  `Performance`, `Processor`, in the hub's read order, so one tick's figures go together, [S-2501]) and
  charts nothing. Its facts, in the kind's order: Speed (the old hero, the effective clock), Base speed,
  Cores, Logical processors, Processes, Threads, Handles, Up time, Thermal zone or CPU package; a new
  one shows the first four. No hero: the load is the CPU reading's. The header's label names the
  processor ([S-2260]).
- **[S-2011]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`) (a row's click
  stays the row's, [S-2004]); the module decides it (`CpuFactsModule.Opens`, through its part's `Opens`).

Needs a manual pass: the widget on a live desktop (the clock moving with load, the temperature on a
machine whose firmware reports a thermal zone, and on one running Core Temp or HWiNFO with Shared
Memory Support on); the pick reordered by dragging in the menu.
