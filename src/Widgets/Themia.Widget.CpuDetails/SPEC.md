# CPU details

The module `cpuDetails` ([S-6911]): the processor's figures beside its load. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the
detail ladder [S-2400]..[S-2404], [S-3861], [S-3862], the system hub [S-2500]..[S-2513]); the sheet's
rules are the SDK's `CpuDetailRules` with their root tags ([S-2405] the hero and the facts, [S-2406]
the clock, [S-2407] the temperature), read by the host's processor source.

Owning files: everything in this folder: `CpuDetailsModule.cs` (descriptor, view factory, JSON
context; no settings rows), `CpuDetailsConfig.cs` (what it asks the hub, `IReadsSystem`),
`CpuDetailPart.cs` (its half of the shared `DetailView`: the three feeds held, the sheet built from
one tick's figures), `Localization/Locales/` (its own `CpuDetailsStr`, 30 languages). Tests:
`Tests/CpuDetailsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-9500]** CPU details as a module: it reads the hub's load, counts and processor feeds
  (`SystemSource.Cpu`, `Performance`, `Processor`, in the hub's read order, so one tick's figures go
  together, [S-2501]) and charts nothing; it has nothing to choose, so no settings page ([S-1750]).

Needs a manual pass: the widget on a live desktop (the clock moving with load, the temperature on a
machine whose firmware reports a thermal zone).
