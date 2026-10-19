# Top processes

The module `topProcesses` ([S-6911]): the busiest processes, by processor or by memory. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the
detail ladder [S-2400]..[S-2404], the virtualized list [S-6603], the system hub [S-2500]..[S-2513]);
the list's rules are the SDK's `ProcessTable` with their root tags ([S-2410] the snapshot, [S-2411] the
ranking, [S-2412] the ceiling, [S-2413] grouping, [S-2414] the share, [S-2433] the icons).

Owning files: everything in this folder: `TopProcessesModule.cs` (descriptor, view factory, menu
lines, JSON context), `TopProcessesConfig.cs` (the ranking, the ceiling, the grouping, what it asks the
hub, `IReadsSystem`), `ProcessesPart.cs` (its half of the shared `DetailView`: the rows, each with its
program's icon through the host's `AppIconSource` and `ProcessPaths`), `TopProcessesMenu.cs` (its
properties menu), `Localization/Locales/` (its own `TopProcessesStr`, 30 languages). Tests:
`Tests/TopProcessesTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's rules
is the module host's `DetailHostTests`.

- **[S-9700]** Top processes as a module: it reads the hub's process snapshot
  (`SystemSource.Processes`) and charts nothing; it ranks by processor, lists as many rows as the tile
  has room for and groups a program's processes into one row by default, and a clone keeps all three.
  Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings page:
  Appearance, Sort by, Rows (Fit, 3, 5, 10, 20, 50; a count not offered holds Fit), Group by program.

Needs a manual pass: the widget on a live desktop (a busy program rising, sort by memory, grouping
off, a list of 50 scrolled).
