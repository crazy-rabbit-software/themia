# Memory details

The module `memoryDetails` ([S-6911]): memory in use, available, committed, cached, the pools and the
page file. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and what the
detail widgets share (the detail ladder [S-2400]..[S-2404], [S-3861], [S-3862], the system hub
[S-2500]..[S-2513]); the sheet's rules are the SDK's `MemoryDetailRules` with their root tags
([S-2408] the hero and the facts, [S-2409] the page files), read by the host's performance source.

Owning files: everything in this folder: `MemoryDetailsModule.cs` (descriptor, view factory, JSON
context; no settings rows), `MemoryDetailsConfig.cs` (what it asks the hub, `IReadsSystem`),
`MemoryDetailPart.cs` (its half of the shared `DetailView`), `Localization/Locales/` (its own
`MemoryDetailsStr`, 30 languages). Tests: `Tests/MemoryDetailsTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-9600]** Memory details as a module: it reads the hub's performance feed (the machine-wide
  memory figures and the page files, `SystemSource.Performance`) and charts nothing; it has nothing to
  choose, so no settings page ([S-1750]).

Needs a manual pass: the widget on a live desktop (the figures moving as programs start and close).
