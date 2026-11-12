# Memory history (memoryHistory)

The module `memoryHistory` ([S-6911]): physical memory in use over time, the history alone. Its tags are this file's alone; the root
`SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: the history atom [S-11002]) and the
chart ([S-1121], [S-1824], [S-6331]).

Owning files: everything in this folder: `MemoryHistoryModule.cs` (descriptor, config, its half of the shared
`MeterView`, JSON context), `Localization/Locales/` (its own `MemoryHistoryStr`). Tests: `Tests/MemoryHistoryTests.cs`.

- **[S-11013]** Memory history: the reading's own series (so the two agree to the point) drawn as the
  history band edge to edge, no number. It starts headerless (it sits under its reading, [S-2590]) and
  may have one. 160..640 wide, 48..200 tall, a new one 160 × 64 (`MeterSizing.History`); a wider card is
  a longer chart of the same span. Drawn in the usage pressure's role ([S-1123]). Its span (1, 5, 15, 30 min, 1 h; a minute by default, [S-1121]) is
  a line on its menu's first level. A click opens what the reading's opens.

Needs a manual pass: the chart on a live desktop (a span changed, a wide card).
