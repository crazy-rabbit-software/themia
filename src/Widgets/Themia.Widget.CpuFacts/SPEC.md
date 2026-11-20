# CPU facts (CPU-FACTS)

The family project `Themia.Widget.CpuFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the processor's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `CpuDetailRules` ([S-2405] the facts, [S-2406] the clock, [S-2434]..[S-2436] and [S-2407] the temperature).

Owning files: everything in this folder: `CpuKinds.cs` (the kinds as data: id, key, glyph, history, the row's name, the
name and description on the add page, search words, the removed card's row), `CpuMetric.cs` (the module and config
bases, the part over the hub, the JSON context), `Localization/Locales/` (its own `CpuFactsStr`). Tests: `Tests/CpuMetricTests.cs`.

- **[S-11012]** *(Retired by [S-14610]: no card of picked facts.)*
- **[S-14610]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `cpuSpeed` "CPU speed", `cpuBaseSpeed` "CPU base speed", `cpuCoreCount` "CPU core count", `cpuLogical` "Logical processors", `cpuProcesses` "Processes", `cpuThreads` "Threads", `cpuHandles` "Handles", `cpuUptime` "Up time", `cpuTemperature` "CPU temperature", in the old card's order, the family **CPU
  facts** under System (`AddFolderId.CpuFacts`). Each reads the hub's load, counts and processor feeds (in the hub's read
  order, one tick's figures, [S-2501]; the processor's read is a new reading) and draws its fact of the sheet; one this
  read lacks is "--" dimmed. Trends ([S-14603]): CPU speed, processes, threads, handles and CPU temperature an arrow from the last two readings the card saw (no history kept); CPU base speed, CPU core count, logical processors and up time none (a text, a constant or a time: no Trend line). Its options: Appearance and, where it moves, Trend.
- **[S-2011]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`); every kind declares it (`CpuKinds`, `MetricKind.Opens`, [S-14706]), as do the card menu's first line and Enter ([S-14707]).

Needs a manual pass: the cards on a live desktop (the clock moving with load, a thermal zone or a tool's package sensor).
