# Memory (reading)

The module `memory` ([S-6911]): physical memory in use, the reading alone. Its tags are this file's
alone; the root `SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]), the meter
framework [S-1120]..[S-1123], the gauge [S-6302], the readouts' rule [S-6330] and the system hub
[S-2500]..[S-2513]; the memory figures are the SDK's `MemoryFigures`, which the host's source reads.
The id `memory` stays the reading's when the old Memory card was split (2026-11-12): its history is
`memoryHistory`, its details `memoryFacts`; the old card's look is the ready-made group "Memory".

Owning files: everything in this folder: `MemoryModule.cs` (descriptor, sizing, view factory, JSON
context), `MemoryConfig.cs` (its styles and what it asks the hub, `IReadsSystem`), `MemoryMeter.cs`
(its half of the shared `MeterView`: the pressure, the drawing per style, the used and free bar),
`Localization/Locales/` (its own `MemoryStr`). Tests: `Tests/MemoryTests.cs`.

- **[S-1130]** Memory: percent used, from `GlobalMemoryStatusEx` (total less available), the reading
  alone ([S-11001]): the header and the number at 28, one height (84), 120..320 wide, a new one 160.
  Nothing appears with size; no tier ladder is left.
- **[S-6315]** Memory styles, `MemoryConfig.Style` (`MemoryStyle`): **Number** (the reading alone),
  **Bar** ([S-6316]) and **Gauge** ([S-6302], [S-11003]). The old Graph style is the Memory history atom.
- **[S-6316]** **Bar**: the reading over one row ([S-6304]), the used share filled against the free:
  "9.6 GB used" as its label and "6.4 GB free" as its value, from the snapshot the reading comes from
  (`MemoryFigures`), so bar and number agree. One height (120), 240..480 wide (its meter needs the room).
- Its options are on the properties menu ([S-3719]): Style and Appearance; nothing of its own.
- **[S-2012]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`, the system
  folder's `Taskmgr.exe`); the module decides it (`MemoryModule.Opens`, through its part's `Opens`).

Needs a manual pass: the reading on a live desktop (the share moving, each style).
