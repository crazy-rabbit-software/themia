# GPU facts (gpuFacts)

The module `gpuFacts` ([S-6911]), which replaces GPU details: one adapter's memory and sensors as fact
rows. Its tags are this file's alone; the root `SPEC.md` holds what the facts atoms share ([S-11007])
and the sheet's rules, the SDK's `GpuDetailRules` ([S-2430] memory, [S-2432] the sensors), read by the
host's GPU sampler; the adapter line is the shared one ([S-433], `GpuAdapterLine`). The old card's look
is the ready-made group "GPU details" ([S-11090]).

Owning files: everything in this folder: `GpuFactsModule.cs` (descriptor, view factory, JSON context),
`GpuFactsConfig.cs` (its adapter, the pick, what it asks the hub), `GpuFactsPart.cs` (its half of the
shared `DetailView`), `GpuFactsMenu.cs` (its properties menu: the adapter, then the pick),
`Localization/Locales/` (its own `GpuFactsStr`). Tests: `Tests/GpuFactsTests.cs`, `GpuFactsMenuTests.cs`.

- **[S-11016]** GPU facts: Dedicated memory (the old hero, used of total, where the adapter has its own),
  Shared memory, Temperature, Fan, GPU clock, Memory clock and Load (the reading's own figure, last); a
  new one shows the first four. No hero. The header's label names the adapter ([S-2260]).
- **[S-2431]** `Adapter`: a DXGI position, as the GPU reading's ([S-433]: the primary display's adapter
  by default), offered with two or more adapters; one that is gone shows the first. Its options are on
  the properties menu ([S-3719]): Appearance, Adapter, then Facts ([S-11007]).
- **[S-2015]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`) (a row's click
  stays the row's, [S-2004]); the module decides it (`GpuFactsModule.Opens`, through its part's `Opens`).

Needs a manual pass: the widget on a live desktop (video memory moving under a game, the sensors of a
WDDM 2.4+ driver, two adapters and the choice between them).
