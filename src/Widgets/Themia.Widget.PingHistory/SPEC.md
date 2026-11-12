# Ping history (pingHistory)

The module `pingHistory` ([S-6911]): one host's latency over time, the history alone. Its tags are this file's alone; the root
`SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: the history atom [S-11002]) and the
chart ([S-1121], [S-1824], [S-6331]).

Owning files: everything in this folder: `PingHistoryModule.cs` (descriptor, config, its half of the shared
`MeterView`, JSON context), `Localization/Locales/` (its own `PingHistoryStr`). Tests: `Tests/PingHistoryTests.cs`.

- **[S-11022]** Ping history: the reading's own series (so the two agree to the point) drawn as the
  history band edge to edge, no number. It starts headerless (it sits under its reading, [S-2590]) and
  may have one. 160..640 wide, 48..200 tall, a new one 160 × 64 (`MeterSizing.History`); a wider card is
  a longer chart of the same span. Its host is its own setting, as the reading's ([S-109], [S-1175]); the probes are the host's one prober ([S-2508]), and the header, when it has one, says the host and shows the problem glyph while it does not answer, through the SDK's `PingPart` the reading shares. Against the window's slowest answer ([S-1172]). Its span (1, 5, 15, 30 min, 1 h; a quarter of an hour by default, [S-1121]) is
  a line on its menu's first level. A click opens what the reading's opens.

Needs a manual pass: the chart on a live desktop (a span changed, a wide card).
