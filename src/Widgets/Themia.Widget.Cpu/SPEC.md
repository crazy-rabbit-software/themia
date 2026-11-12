# CPU (reading)

The module `cpu` ([S-6911]): processor load across all cores, the reading alone. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the system atoms share (the atoms
[S-11000]..[S-11009], the meter framework [S-1120]..[S-1123], the gauge [S-6302], the readouts' rule
[S-6330], the system hub [S-2500]..[S-2513]). The id `cpu` stays the reading's when the old CPU card
was split (2026-11-12): its history is `cpuHistory`, its cores `cpuCores`, its details `cpuFacts`; the
old card's look is the ready-made group "CPU" ([S-11090]).

Owning files: everything in this folder: `CpuModule.cs` (descriptor, sizing, view factory, JSON
context), `CpuConfig.cs` (its styles and what it asks the hub, `IReadsSystem`), `CpuMeter.cs` (its half
of the shared `MeterView`: the pressure, the drawing per style), `Localization/Locales/` (its own
`CpuStr`, its style names included). Tests: `Tests/CpuTests.cs` (run by `tests/Themia.Modules.Tests`);
what the hub reads is the module host's `MeterHostTests` (`S6310_`).

- **[S-1140]** CPU: percent of all cores (`GetSystemTimes` delta, shared per tick [S-643]), the reading
  alone ([S-11001]): the header ("CPU") and the number at 28 with its unit, one height (84), 120..320
  wide, a new one 160 (`MeterSizing.Reading`). Nothing appears with size; no tier ladder is left.
- **[S-6311]** CPU styles, `CpuConfig.Style` (`CpuStyle`): **Number** (the reading alone) and **Gauge**
  ([S-6302], [S-11003]: the dial as large as the box takes, 136..320 wide). The old Graph, All cores,
  Core grid and Core graphs styles are the CPU history and CPU cores atoms.
- Its options are on the properties menu ([S-3719]): Style and Appearance; nothing of its own.
- **[S-2010]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`, the system
  folder's `Taskmgr.exe`); the module decides it (`CpuModule.Opens`, through its part's `Opens`).

Needs a manual pass: the reading on a live desktop (the load moving, both styles).
