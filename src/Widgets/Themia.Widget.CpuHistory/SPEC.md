# CPU history (cpuHistory)

The module `cpuHistory` ([S-6911]): the processor's load over time, the history alone. Its tags are
this file's alone; the root `SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: the
history atom [S-11002]) and the chart ([S-1121], [S-1824], [S-6331]).

Owning files: everything in this folder: `CpuHistoryModule.cs` (descriptor, config, its half of the
shared `MeterView`, JSON context), `Localization/Locales/` (its own `CpuHistoryStr`). Tests:
`Tests/CpuHistoryTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-11010]** CPU history: the CPU reading's own series (`SystemSource.Cpu`, so the two agree to the
  point) drawn as the history band edge to edge, in the usage pressure's role ([S-1123]), no number. It
  starts headerless (it sits under its reading, [S-2590]) and may have one. 160..640 wide, 48..200
  tall, a new one 160 × 64 (`MeterSizing.History`); a wider card is a longer chart of the same span.
  Its one setting is the Chart span (1, 5, 15, 30 min, 1 h; a minute by default, [S-1121]) on its menu's
  first level. A click opens Task Manager, as the reading's does.

Needs a manual pass: the chart on a live desktop (a span changed, a wide card).
