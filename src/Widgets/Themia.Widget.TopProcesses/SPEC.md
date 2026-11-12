# Top processes

The module `topProcesses` ([S-6911]): the busiest processes, by processor or by memory. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the
detail list [S-2404], the list atoms [S-11008], the virtualized list [S-6603], the system hub [S-2500]..[S-2513]);
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
  (`SystemSource.Processes`) and charts nothing; it ranks by processor, shows five rows and groups a
  program's processes into one row by default, and a clone keeps all three. It is a list atom
  ([S-11008]): its rows are a setting (`Rows`, 1..20, `IKitRows`), each two lines (the program, the other
  figure under it), the value column as before; every process is in the list and the rows past its
  count scroll inside them. Its height follows the rows (36 + 40 a row + 12, `KitSizing`, 220..520 wide,
  a new one 300 × 248) and dragging its bottom edge sets them; "Fit" is gone, and no tier ladder is
  left. Its options are on the properties menu ([S-3719]), no settings page: Appearance, Sort by, Rows
  (`KitMenuLines.Rows`, no lines choice), Group by program.
- **[S-2017]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`, the system
  folder's `Taskmgr.exe`) (a row's click stays the row's, [S-2004]); the module decides it
  (`TopProcessesModule.Opens`, through its part's `Opens`).

- End task (root [S-2437], issue 197): a row's right-click menu (or the Menu key on the focused row) has
  End task, drawn as a destructive line, which ends what the row stands for (`ProcessesPart.RowMenu`
  over `ProcessTable.Members`): the program's every process when grouped, else that one process. A
  refused end (a system or elevated process) is a warning notice; the list reads again at once. Themia's
  own row, and a row whose processes have gone, have no menu of their own (the card's opens).

Needs a manual pass: the widget on a live desktop (a busy program rising, sort by memory, grouping
off, a list of 50 scrolled; End task on a program of one and of several processes, grouped and not,
and on a system process for the notice).
