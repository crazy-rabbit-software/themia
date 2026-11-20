# Memory facts (MEMORY-FACTS)

The family project `Themia.Widget.MemoryFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the memory's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `MemoryDetailRules` ([S-2408], [S-2409] the page files).

Owning files: everything in this folder: `MemoryKinds.cs`, `MemoryMetric.cs` (module and config bases, the part over the
hub's performance feed and the memory's share series, the JSON context), `Localization/Locales/` (its own `MemoryFactsStr`).
Tests: `Tests/MemoryMetricTests.cs`.

- **[S-11017]** *(Retired by [S-14630]: no card of picked facts.)*
- **[S-14630]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `memoryInUse` "Memory in use", `memoryAvailable` "Available memory", `memoryCommitted` "Committed memory", `memoryCached` "Cached memory", `memoryPagedPool` "Paged pool", `memoryNonPagedPool` "Non-paged pool", `memoryPageFile` "Page file", the family **Memory facts** under System
  (`AddFolderId.MemoryFacts`). Each reads the hub's performance figures and draws its fact; in use is used of total, its
  number the bytes used. Trends ([S-14603]): memory in use an arrow and a line; available memory, committed memory, cached memory, paged pool, non-paged pool and page file an arrow from the last two readings the card saw (no history kept); in use's line is the memory reading's own share
  series, read only while a line is drawn. A placed `memoryFacts` card is replaced in place by its rows ([S-14606]).
- **[S-2013]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`); every kind declares it (`MemoryKinds`, `MetricKind.Opens`, [S-14706]), as do the card menu's first line and Enter ([S-14707]).

Needs a manual pass: the cards on a live desktop (memory moving under load, a machine without a page file).
