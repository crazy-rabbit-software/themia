# Recent files

The module `recentFiles` ([S-6911]): the files opened last, as Windows lists them in Recent Items.
Its tags are this file's alone; the root `SPEC.md` holds what it reads of the host, with their root
tags: the Recent Items source and its rules ([S-4841], the host's `RecentItems` behind
`IWidgetHost.RecentFiles`, the SDK's `RecentFileRules`) and the live list ([S-4843]).

Owning files: everything in this folder: `RecentFilesModule.cs` (descriptor, glyph, view factory, JSON
context), `RecentFilesConfig.cs`, `RecentFilesView.cs` (the rows, the icons through
`IWidgetHost.FileIcons`, opening through its launcher, Open file location through
`IWidgetHost.FileOps`), `RecentFilesLayout.cs` (the ladder), `Preview/` (Debug renders:
`--state sample|empty`), `Localization/Locales/` (its own `RecentFilesStr`, 30 languages). Tests:
`Tests/RecentFilesLayoutTests.cs` (run by `tests/Themia.Modules.Tests`); its config beside Next
meeting's is the module host's `DeskInfoHostTests`.

- **[S-4840]** `RecentFiles` (126): the files opened last, as Windows lists them. No settings.
- **[S-4842]** The ladder (`RecentFilesLayout.Plan`): compact one-line rows on a Compact height; the
  folder under the name from a Regular width and height; when, as a column at the end from an
  Expanded width (else after the folder). The rest scrolls.
- **[S-4844]** A click focuses the row; a double-click or Enter opens the file (a gone one reads
  the list again; [S-7065], it opened on a click and checked edit mode itself, which the card does). The row menu: Open, Open
  file location (Explorer with it selected), Remove from list, which deletes its Recent Items
  shortcut as Explorer's "Remove from Recent" does; the file is never touched, and nothing outside
  the Recent Items folder is ever deleted.
- **[S-4845]** Keys through the lease ([S-1288]): Up/Down, Page Up/Down, Home/End move, Enter opens,
  Delete removes from the list, the Menu key opens the row's menu, Escape lets go.

Needs a live pass: opening files from Explorer and Office (the list updating), Remove from list
(and Explorer's Home agreeing), a file on a disconnected share.
