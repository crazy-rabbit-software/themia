# GPU facts (GPU-FACTS)

The family project `Themia.Widget.GpuFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): one graphics adapter's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `GpuDetailRules` ([S-2430] memory, [S-2432] the sensors), and the adapter line ([S-433], `GpuAdapterLine`).

Owning files: everything in this folder: `GpuKinds.cs`, `GpuMetric.cs` (module and config bases, the part over the hub's
GPU feed and the adapter's series, the menu's adapter line, the JSON context), `Localization/Locales/` (its own
`GpuFactsStr`). Tests: `Tests/GpuMetricTests.cs`.

- **[S-11016]** *(Retired by [S-14620]: no card of picked facts.)*
- **[S-14620]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `gpuDedicatedMemory` "GPU dedicated memory", `gpuSharedMemory` "GPU shared memory", `gpuTemperature` "GPU temperature", `gpuFan` "GPU fan", `gpuClock` "GPU clock", `gpuMemoryClock` "GPU memory clock", `gpuLoad` "GPU load", the family **GPU facts** under System
  (`AddFolderId.GpuFacts`). Each reads the hub's GPU feed (one query a due tick for every GPU widget) and draws its fact of
  the adapter's sheet; no adapter keeps the row, "--" dimmed, why its tooltip ([S-1942]). Trends ([S-14603]):
  GPU load an arrow and a line; GPU dedicated memory, GPU shared memory, GPU temperature, GPU fan, GPU clock and GPU memory clock an arrow from the last two readings the card saw (no history kept); the load's line is the adapter's own series, read only while a line is drawn. 
- **[S-2431]** `Adapter`: a DXGI position, as the GPU reading's ([S-433]: the primary display's adapter by default), offered
  with two or more adapters (the GPU atoms' shared line); one that is gone shows the first; one that is no position is the
  first, loudly. Its options are on the properties menu ([S-3719]): Appearance, Adapter, then Trend where it moves.
- **[S-2015]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`); every kind declares it (`GpuKinds`, `MetricKind.Opens`, [S-14706]), as do the card menu's first line and Enter ([S-14707]).

Needs a manual pass: the cards on a live desktop (video memory under a game, a WDDM 2.4+ driver's sensors, two adapters).
