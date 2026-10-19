# Memory (meter)

The module `memory` ([S-6911]): physical memory in use. Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and what the meters share (the meter framework [S-1120]..[S-1123],
[S-1762], the styles' looks [S-6300]..[S-6305] and the gauge [S-6314], the glides [S-6323], the system
hub [S-2500]..[S-2513]; the memory figures, `MemoryFigures` in the SDK, which the host's source reads).

Owning files: everything in this folder: `MemoryModule.cs` (descriptor, view factory, menu lines,
JSON context), `MemoryConfig.cs` (with its styles and what it asks the hub, `IReadsSystem`),
`MemoryMeter.cs` (its half of the shared `MeterView`: the pressure, the look per style, the used and
free bar), `Localization/Locales/` (its
own `MemoryStr`, 30 languages, its style names included). Tests: `Tests/MemoryTests.cs` (run by
`tests/Themia.Modules.Tests`); the meters' shared settings are the module host's `MeterHostTests`.

- **[S-1130]** Memory: percent used, from `GlobalMemoryStatusEx` (total less available).
- **[S-6315]** Memory styles, `MemoryConfig.Style` (`MemoryStyle`): **Graph** ([S-1762]), **Bar**
  ([S-6316]), **Gauge** ([S-6302]).
- **[S-6316]** **Bar**: one row ([S-6304]), the used share filled against the free: "9.6 GB used" as
  its label and "6.4 GB free" as its value, from the snapshot the reading comes from
  (`MemoryFigures`), so bar and number agree.
- Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings page:
  Appearance, and under Style in the Graph style the Chart span every meter has ([S-1122]).

Needs a manual pass: the meter on a live desktop (the reading moving, each style).
