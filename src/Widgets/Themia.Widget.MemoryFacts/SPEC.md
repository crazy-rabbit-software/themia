# Memory facts (memoryFacts)

The module `memoryFacts` ([S-6911]), which replaces Memory details: memory's facts as fact rows. Its
tags are this file's alone; the root `SPEC.md` holds what the facts atoms share ([S-11007]) and the
sheet's rules, the SDK's `MemoryDetailRules` ([S-2408] the facts, [S-2409] the page files), read by the
host's performance source. The old card's look is the ready-made group "Memory details" ([S-11090]).

Owning files: everything in this folder: `MemoryFactsModule.cs` (descriptor, menu line, JSON context),
`MemoryFactsConfig.cs` (the pick, what it asks the hub), `MemoryFactsPart.cs` (its half of the shared
`DetailView`), `Localization/Locales/` (its own `MemoryFactsStr`). Tests: `Tests/MemoryFactsTests.cs`.

- **[S-11017]** Memory facts reads the hub's performance feed (`SystemSource.Performance`) and charts
  nothing. Its facts, in the kind's order: In use (the old hero, used of total), Available, Committed,
  Cached, Paged pool, Non-paged pool, Page file; a new one shows the first four. No hero: the share is
  the Memory reading's. Its one line is the pick ([S-11007]).
- **[S-2013]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`) (a row's click
  stays the row's, [S-2004]); the module decides it (`MemoryFactsModule.Opens`, through its part's `Opens`).

Needs a manual pass: the widget on a live desktop (the figures moving as programs start and close).
