# CPU cores (cpuCores)

The module `cpuCores` ([S-6911]): each logical processor's load. Its tags are this file's alone; the
root `SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: the items atom [S-11005]), the
per-core reads [S-6310], the cores' history [S-6318] and the looks [S-6303], [S-6305], [S-6317].

Owning files: everything in this folder: `CpuCoresModule.cs` (descriptor, styles, config, its half of
the shared `MeterView`, JSON context), `Localization/Locales/` (its own `CpuCoresStr`, the style names
moved from the CPU's with their translations). Tests: `Tests/CpuCoresTests.cs`; what the hub reads is
the module host's `MeterHostTests` (`S6310_`, `S6318_`).

- **[S-11011]** CPU cores: the hub's one per-core read (`SystemSource.CpuCores`, beside the total's
  series), each core in its own usage pressure's role ([S-6301]), in the card's inset; it starts
  headerless ([S-2590]). Styles (`CpuCoresStyle`, the CPU's old core styles): **Bars** (a column per
  core, [S-6303]; 120..640 × 48..200, a new one 160 × 64), **Grid** (a ring per core, [S-6305];
  112..360 square, a new one 144) and **Graphs** (a cell per core, its own history edge to edge with its
  load in Caption over its top corner, [S-6317]; 200..640 × 128..400, a new one 280 × 200). Sixteen
  cores fit every style at its least; the cells only grow. The Graphs style offers the Chart span under
  Style ([S-6318]: the hub keeps each core's history for it); the others have no setting.

Needs a manual pass: a machine with many cores (32, 64), each style at its least.
