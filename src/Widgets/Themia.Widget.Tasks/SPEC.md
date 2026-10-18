# Tasks

The module `tasks` ([S-6911]): one task list of an account (Microsoft To Do, Google Tasks), Today
across an account's lists, or a list kept on this PC. Its tags are this file's alone; the root
`SPEC.md` holds what it shares with the host and the week planner, with their root tags: the task
service and its offline store ([S-590], [S-591], [S-596], [S-597]), due dates and the list rules
([S-593]..[S-601], [S-610], [S-611]), the lists on this PC ([S-603], [S-604]), the ladder and the keys
([S-780]..[S-789], [S-1981], the SDK's `TaskLayout`).

Owning files: everything in this folder: `TasksModule.cs` (descriptor, view factory, JSON context),
`TasksConfig.cs`, `TasksView*.cs` (the rows, the panel and its drag, the list picker, over
`IWidgetHost.Tasks`), `TasksSettings.cs`, `Preview/` (Debug renders: `--state tasks|few|empty|done`,
the lists on this PC in memory), `Localization/Locales/` (its own `TasksStr`, 30 languages). Tests:
the config's are the module host's (`TasksConfigTests`, [S-611]); the rules' are Core's
(`TaskRulesTests`, `TaskSyncTests`, `LocalTaskListsTests`, `TaskLayoutTests`).

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
- **[S-607]** Tasks' one stale rule ([S-7042]): the header is the kit's (`KitHeader`: the problem
  glyph, the ring, the open count, before the list picker); a list on this PC that could not be
  saved and a change the provider refused are its notes; an account list whose last refresh failed
  with tasks on screen is one line in the kit's words, stamped with when they are from ("Work: No
  network · updated 10:42", `TaskFreshness`); the card menu's Refresh line says the age. The old
  "Last refresh failed: … Showing items from …" warning is gone.

Needs a live pass: a signed-in Microsoft and Google account (the list picker, Today, ticking,
renaming, deleting and adding a task landing on the server, offline edits catching up), a list on
this PC, dragging a row, the keys, and the showcase views on the live desktop.
