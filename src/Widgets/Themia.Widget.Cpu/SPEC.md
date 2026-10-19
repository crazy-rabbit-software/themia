# CPU (meter)

The module `cpu` ([S-6911]): processor load across all cores. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections and what the meters share (the meter framework [S-1120]..
[S-1123], [S-1762], the styles' looks [S-6300]..[S-6305] and the gauge [S-6314], the glides [S-6323],
the system hub [S-2500]..[S-2513]; the per-core reads [S-6310], the cores' history [S-6318] and the
core graphs' tile figure [S-6319], which the SDK's `CoreHistory`, `CoreGraphLayout` and the host's
`CpuCores` source keep).

Owning files: everything in this folder: `CpuModule.cs` (descriptor, view factory, menu lines,
JSON context), `CpuConfig.cs` (with its styles and what it asks the hub, `IReadsSystem`),
`CpuMeter.cs` (its half of the shared `MeterView`: the pressure, the look per style, the cores as
items with their history, the debug sample's sixteen cores), `Localization/Locales/` (its own `CpuStr`, 30 languages, its style names included).
The view, its faces and the core graphs (`MeterView`, `CoreGraphGrid`, `SparkBand`) are the SDK's.
Tests: `Tests/CpuTests.cs` (run by `tests/Themia.Modules.Tests`); what the hub reads for a style is
the module host's `MeterHostTests` (`S6310_`, `S6318_`).

- **[S-1140]** CPU: percent of all cores (`GetSystemTimes` delta, shared per tick [S-643]); the reading
  and its history only ([S-1762]). The clock, per-core strip, sensors, totals and top processes of the
  earlier ladder ([S-1141], [S-1142], [S-1146]..[S-1150]) are removed with their settings.
- **[S-6311]** CPU styles, `CpuConfig.Style` (`CpuStyle`): **Graph** ([S-1762]), **All cores**
  ([S-6312]), **Core grid** ([S-6313]), **Gauge** ([S-6302]), **Core graphs** ([S-6317]).
- **[S-6312]** **All cores**: a column per logical processor ([S-6303]), each in its own pressure colour.
- **[S-6313]** **Core grid**: a tile per logical processor ([S-6305]), shaded by its load ([S-6301]).
- **[S-6317]** **Core graphs** (`CpuStyle.CoreGraphs`, user request 2026-10-16): the reading over a
  tile per logical processor in the Core grid's arrangement ([S-6305], `BarLayout.Tiles`), each
  that core's history drawn by the shared `SparkBand` edge to edge in its tile (sliding as the main
  graph does, [S-1826]) in its load's pressure colour over the columns' faint track, its load above
  the line at the top left (`CoreGraphGrid`). The Chart span applies (the span line is offered in this
  style as in Graph, `GaugeConfig.StyleChartsHistory`). Debug: `--style coregraphs --state history`
  draws sixteen fixture cores.
- Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings page:
  Appearance, and under Style in the Graph and Core graphs styles the Chart span every meter has
  ([S-1122], `MeterMenuHostTests`).

Needs a manual pass: the meter on a live desktop (the load moving, each style, the core graphs on a
machine with many cores).
