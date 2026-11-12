# GPU engines (gpuEngines)

The module `gpuEngines` ([S-6911]): one graphics adapter's engines. Its tags are this file's alone; the
root `SPEC.md` holds what the system atoms share ([S-11000]..[S-11009]: rows of items [S-11006]) and the
engines' rules ([S-6322], `GpuRules`).

Owning files: everything in this folder: `GpuEnginesModule.cs` (descriptor, config, menu, its half of
the shared `MeterView` on the SDK's `GpuPart`, JSON context), `Localization/Locales/` (its own
`GpuEnginesStr`; the engine names are the SDK's, moved from the GPU's with their translations). Tests:
`Tests/GpuEnginesTests.cs`.

- **[S-11015]** GPU engines (the GPU's old Engines style as its own atom): under the header (the
  adapter's name as its label, [S-2260]) a fact row per engine type with its bar ([S-6304]): 3D, Copy,
  Video encode, Video decode, always in that order, one an adapter does not report "--" with no bar,
  so it is four rows at one height (160), 200..420 wide (`MeterSizing.ItemRows`). Its adapter is its
  setting, as the reading's ([S-433]); an adapter that is not there draws the rows disabled ([S-1940]).

Needs a manual pass: the rows on a live desktop (a video moving the decode row, two adapters).
