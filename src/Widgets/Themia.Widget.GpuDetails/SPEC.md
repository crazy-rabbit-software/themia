# GPU details

The module `gpuDetails` ([S-6911]): one adapter's memory and sensors. Its tags are this file's alone;
the root `SPEC.md` holds the host's sections and what the detail widgets share (the detail ladder
[S-2400]..[S-2404], the virtualized list [S-6603], the system hub [S-2500]..[S-2513]); the sheet's
rules are the SDK's `GpuDetailRules` with their root tags ([S-2430] memory, [S-2432] the sensors),
read by the host's GPU sampler; the adapter line is the shared one, as the GPU meter's ([S-433], `GpuAdapterLine`).

Owning files: everything in this folder: `GpuDetailsModule.cs` (descriptor, view factory, menu
lines, JSON context), `GpuDetailsConfig.cs` (its adapter and what it asks the hub, `IReadsSystem`),
`GpuDetailPart.cs` (its half of the shared `DetailView`), `GpuDetailsMenu.cs` (its properties menu:
the adapter, offered with two or more), `Localization/Locales/` (its own `GpuDetailsStr`, 30 languages).
Tests: `Tests/GpuDetailsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-2431]** `Adapter`: a DXGI position, as the GPU meter's ([S-433]: the primary display's adapter
  by default), offered with two or more adapters; one that is gone shows the first. Its options are
  on the properties menu ([S-3719], user request 2026-10-19), no settings page: Appearance, then
  Adapter.

Needs a manual pass: the widget on a live desktop (video memory moving under a game, the sensors of a
WDDM 2.4+ driver, two adapters and the choice between them).
