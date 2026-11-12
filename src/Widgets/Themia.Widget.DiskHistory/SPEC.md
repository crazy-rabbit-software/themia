# Disk history (diskHistory)

The module `diskHistory` ([S-6911]): a drive's reads and writes per second over time, the history alone. Its tags are this file's alone; the root
`SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: the history atom [S-11002]) and the
chart ([S-1121], [S-1824], [S-6331]).

Owning files: everything in this folder: `DiskHistoryModule.cs` (descriptor, config, its half of the shared
`MeterView`, JSON context), `Localization/Locales/` (its own `DiskHistoryStr`). Tests: `Tests/DiskHistoryTests.cs`.

- **[S-11018]** Disk history: the reading's own series (so the two agree to the point) drawn as the
  history band edge to edge, no number. It starts headerless (it sits under its reading, [S-2590]) and
  may have one. 160..640 wide, 48..200 tall, a new one 160 × 64 (`MeterSizing.History`); a wider card is
  a longer chart of the same span. Its drive is its own setting, as the reading's ([S-1160], [S-1822]: the system drive, every drive, or one Windows lists), through the SDK's `DiskPart` and `DiskMenuLines` the reading shares; with a header, the header names the drive ([S-1161]). The chart's top is the window's peak rounded up to a doubling ([S-1821]). Its span (1, 5, 15, 30 min, 1 h; five minutes by default, [S-1121]) is
  a line on its menu's first level. A click opens what the reading's opens.

Needs a manual pass: the chart on a live desktop (a span changed, a wide card).
