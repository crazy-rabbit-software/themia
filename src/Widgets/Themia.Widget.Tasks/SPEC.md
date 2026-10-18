# Tasks

The module `tasks` ([S-6911]): one task list of an account (Microsoft To Do, Google Tasks), Today
across an account's lists, or a list kept on this PC. Its tags are this file's alone; the root
`SPEC.md` holds what it shares with the host and the week planner, with their root tags: the task
service and its offline store ([S-590], [S-591], [S-596], [S-597]), due dates and the list rules
([S-593]..[S-601], [S-610], [S-611]), the lists on this PC ([S-603], [S-604]), the ladder and the keys
([S-780]..[S-789], [S-1981]: the kit's check look, `KitChecks`, [S-7037], [S-7038]).

Owning files: everything in this folder: `TasksModule.cs` (descriptor, view factory, JSON context),
`TasksConfig.cs`, `TaskSpec.cs` (a task as a kit item, its actions, the check spec),
`TaskFreshness.cs`, `TasksView*.cs` (over `IWidgetHost.Tasks`: the screens, the writes the kit's list
hands back, the list picker), `TasksSettings.cs`, `Preview/` (Debug renders: `--state tasks|few|empty|done|stale`,
the lists on this PC in memory), `Localization/Locales/` (its own `TasksStr`, 30 languages). Tests:
the config's are the module host's (`TasksConfigTests`, [S-611]); the rules' are Core's
(`TaskRulesTests`, `TaskSyncTests`, `LocalTaskListsTests`, `TaskLayoutTests` over the kit's check
look); the spec's are `Tests/TaskSpecTests.cs` and `Tests/TaskFreshnessTests.cs`.

- **[S-592]** The list picker offers the account's default list, Today, then its lists. The lists are
  stored per account ([S-2650]) and read again with every sync (single-flight; a failure with nothing
  stored is said).
- **[S-602]** Style ([S-6400]): List puts the due date under the title, Compact beside it (under it anyway in a
  column under 240px wide). Rows are Fluent's fixed heights at minimum and grow only for a wrapped
  title; an important task's title is semibold. The size ladder is [S-780].
- **[S-6400]** Tasks' styles ([S-6000]), `TasksConfig.Style` (`TasksStyle`, `"style": "list"`):
  **List** and **Compact** ([S-602]), what was its Layout setting; no other look option exists.
- **[S-605]** Retired (the app is unreleased: no hearth-era config is read). Nulls a hand edit leaves in
  a Tasks config's strings, an unknown source, layout or due filter, a text filter over 100
  characters, and a broken row order are repaired on load.
- **[S-785]** An open important task leads its title with a "!" in the critical colour (classic
  S-11) where its column is at least 160px wide; the weight and the tooltip say it everywhere.
- **[S-615]** Tasks' one stale rule ([S-7042]): the header is the kit's (`KitHeader`: the problem
  glyph, the ring, the open count, before the list picker); a list on this PC that could not be
  saved and a change the provider refused are its notes; an account list whose last refresh failed
  with tasks on screen is one line in the kit's words, stamped with when they are from ("Work: No
  network · updated 10:42", `TaskFreshness`); the card menu's Refresh line says the age. The old
  "Last refresh failed: … Showing items from …" warning is gone.
- **[S-616]** A task's menu and keys are the kit's ([S-7011], [S-7020]; `TaskSpec`): the menu is
  Mark as done (Mark as not done, with the undo glyph), then Rename, then Delete, Rename and Delete in
  its icon row with F2 and Delete shown; a task opens for editing, so a double-click and Enter rename
  it (Enter ticked it before); Space ticks; Left and Right step like Up and Down; Alt+Shift+Up / Down
  move it; Up with nothing focused lands on the first task. A task the provider has not confirmed
  yet shows Rename disabled.
- **[S-617]** Every state that is not a list is the kit's state panel ([S-7060], `KitStatePanel`), over
  the list's own slot above the add row: the glyph over the sentence, the Accounts link where only
  the user can mend it, and Tasks' own way out ([S-7039]: "Try again" for lists or a list that could
  not be read, "Start a list" for a list on this PC that is gone). The panel keeps its link at every
  size and sheds the glyph, then lines, first ([S-642]); Tasks' own host dropped the link under 140px.

Needs a live pass: a signed-in Microsoft and Google account (the list picker, Today, ticking,
renaming, deleting and adding a task landing on the server, offline edits catching up), a list on
this PC, dragging a row, the keys, and the showcase views on the live desktop.
