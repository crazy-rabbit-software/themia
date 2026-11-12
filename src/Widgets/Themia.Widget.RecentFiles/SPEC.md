# Recent files

The module `recentFiles` ([S-6911]): the files opened last, as Windows lists them in Recent Items.
Its tags are this file's alone; the root `SPEC.md` holds what it reads of the host, with their root
tags: the Recent Items source and its rules ([S-4841], the host's `RecentItems` behind
`IWidgetHost.RecentFiles`, the SDK's `RecentFileRules`) and the live list ([S-4843]).

Owning files: everything in this folder: `RecentFilesModule.cs` (descriptor, glyph, view factory, JSON
context), `RecentFilesConfig.cs`, `RecentFilesView.cs` (the rows, the icons through
`IWidgetHost.FileIcons`, opening through its launcher, Open file location through
`IWidgetHost.FileOps`), `Preview/` (Debug renders: `--state sample|empty`), `Localization/Locales/`
(its own `RecentFilesStr`, 30 languages). Tests: `Tests/RecentFilesSizingTests.cs` (run by
`tests/Themia.Modules.Tests`); its config beside Next meeting's is the module host's `DeskInfoHostTests`.

- **[S-4840]** `RecentFiles` (126): the files opened last, as Windows lists them. Its settings are
  its rows ([S-11200]).
- **[S-4842]** On the family's rows ([S-6711], [S-6712]): the file's icon (16) as the lead, the name
  primary, when it was opened the value, and its folder on the second line where Lines per row is two.
  Nothing opened, or the folder unread, is the state row's sentence at the top.
- **[S-11200]** Converted to the sizing ([S-2438]) on the kit's rows contract (`IKitRows`,
  `KitSizing`, [S-2440]), though its view is its own: **Rows** (1..20, default 6) and **Lines per row**
  (1 or 2, default 2: the folder line) are its menu's two lines (`KitMenuLines.Rows`); its height
  follows them (36 + rows × 28 or 40 + 12, no add footer), 220..520 wide, a new one 300 × 288; dragging
  its bottom edge sets Rows. The icon and the time are always shown; more files scroll inside the rows.
  No ladder is left: nothing appears or disappears with its size.
- **[S-4844]** A click focuses the row, drawn as the desktop draws a selected file and the row
  under the pointer lit as its hover ([S-6627]: the chrome laid behind the row, over its whole width,
  so marking it moves nothing; one row, no multi-selection); a double-click or Enter opens the file (a gone one reads
  the list again; [S-7065], it opened on a click and checked edit mode itself, which the card does). The row menu: Open, Open
  file location (Explorer with it selected), Remove from list, which deletes its Recent Items
  shortcut as Explorer's "Remove from Recent" does; the file is never touched, and nothing outside
  the Recent Items folder is ever deleted.
- **[S-4845]** Keys through the lease ([S-1288]): Up/Down, Page Up/Down, Home/End move, Enter opens,
  Delete removes from the list, the Menu key opens the row's menu, Escape lets go.
- **[S-2033]** A click on the card ([S-2000]) opens File Explorer at Windows' Recent items
  (`shell:Recent`); a file's row keeps its click (it focuses; a double-click opens the file,
  [S-7065]); the module decides it (`RecentFilesModule.Opens`).
- View links: a row is its file; the pointer or the keyboard cursor on it publishes the file pointed at, and a row
  pointed at elsewhere on the view is drawn as under the pointer ([S-11945], [S-11948]).

Needs a live pass: opening files from Explorer and Office (the list updating), Remove from list
(and Explorer's Home agreeing), a file on a disconnected share.
