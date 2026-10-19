# Folder

The module `folder` ([S-6911]): a folder on this PC as a small Windows 11 file browser, in four styles,
with Explorer's selection, keys, menus, file operations, clipboard and drag and drop. Original:
`widgets/office/folder` on `main` (SPEC S-01..S-67). Its tags are this file's alone; the root `SPEC.md`
holds what it shares and reads of the host, with their root tags: the search's extensions and the
columns' and counts' formatting ([S-1321], [S-1322], the SDK's `FileNames` and `FolderFormatting`),
the icons, thumbnails, type names and cloud states ([S-1324], [S-5175], the host's `FileIcons` behind
`IWidgetHost.FileIcons`, the SDK's `CloudBadges`), the git badge ([S-1325], `IWidgetHost.Folders`), the
clipboard chords and selection keys ([S-1327]), the file clipboard ([S-1329], `IWidgetHost.FileClipboard`),
the shell's operations ([S-1330], [S-1331], `IWidgetHost.FileOps`), drops and virtual files ([S-1332],
`IWidgetDropTarget`, the SDK's `FileDrops`), the folder watch ([S-1333], `IWidgetHost.Folders`) and the
tests' rule ([S-1336]).

Owning files: everything in this folder: `FolderModule.cs` (descriptor, view factory, menu lines,
JSON context), `FolderConfig.cs` (with its styles, sizes, sort and navigation), `FolderView.cs` (the
listing and the watch through `IWidgetHost.Folders`, navigation) with `.Render.cs` (the ladder, rows,
tiles, icons through `IWidgetHost.FileIcons`), `.Input.cs` (pointer, keys, selection, the lasso, drag
out through `IWidgetHost.FileOps`, drops) and `.Commands.cs` (Explorer's menus through
`IWidgetHost.ExplorerMenus`, the file operations and clipboard through `IWidgetHost.FileOps` and
`IWidgetHost.FileClipboard`), its rules (`FolderColumns`, `FolderFilter`, `FolderGesture`, `FolderHistory`,
`FolderLasso`, `FolderLayout`, `FolderMenu`, `FolderRules`, `FolderSelection`), `FolderPropertiesMenu.cs` (its
properties-menu lines: the folder through the SDK's `PickFolder`), `Preview/` (Debug renders: `--state
web|wallpapers|lasso`, Windows' own folders; `--menu-png`'s folder menus through `IWidgetPreviewMenus`),
`Localization/Locales/` (its own `FolderStr`, 30 languages, its style names included). Tests:
`Tests/FolderBrowserTests.cs`, `FolderLassoTests.cs`, `FolderMenuTests.cs`, `FolderPropertiesMenuTests.cs`, `FolderRulesTests.cs` (run
by `tests/Themia.Modules.Tests`); its config and rules under the root's tags are the module host's
`FolderKindsTests` and `FolderHostTests`.

- **[S-1320]** Settings (orig. S-14..S-24, S-58), one config for every style ([S-4121]): `Path` (empty = profile), `ItemSize`
  ExtraSmall/Small/Medium/Large (rows 24/28/32/40, icons 12/14/16/20), `TileSize` Small/Medium/Large (80x90/110x120/150x160,
  icons 32/48/64), `Columns` (ordered; default Size, Modified, Type; `Repair` drops unknown/repeats loudly),
  `Navigation` Explorer/Navigate, `ClickMode` (`ItemClickMode`, default Double), `Sort`/`Descending`,
  `ShowHidden`, `ShowExtensions` (default off), `Style`. `ShowDetail`/`MaxCount`, and since [S-4121] `View`, `StretchTiles`,
  `Filter` and `FoldersFirst`, are gone (no compatibility).
- **[S-1323]** Ladder (`FolderLayout.Plan`): one row / one tile row always; an open search box; the details column header;
  list view adds unlabelled Modified (W >= Regular), Size (>= Expanded), Type (>= Huge), drawn right to left ([S-6411]); at Expanded x Expanded a breadcrumb
  (tail first, clickable), no status footer ("24 items · 2 selected", retired [S-666]); Huge width: grid tiles one size
  up. Grid columns = floor(width / tile), stretched to fill the row; rows keep the preset height (no stretch, [S-4121]);
  Icons view packs tiles to icon + 24. Over 1000 items: the first 1000 and a "N more in Explorer…" row.
- **[S-6411]** The rightmost column ends at the row's right inset, the same as the header's buttons, at
  every width and in every style (user report 2026-10-16: from 400 DIPs the list put Size last, blank for
  every folder, so the dates stopped a column short of the edge). The list's unlabelled columns are drawn
  Type, Size, Modified (Modified, filled by every row, last) and Size is left out when nothing shown is
  a file (`FolderLayout.ListColumns`); in Details the last column, whatever it is, is drawn from the right
  of its slot, header included (`FolderColumns.AlignsRight(column, last)`). Grid and Icons stretch their
  tiles to the row ([S-1323]).
- **[S-1326]** Header (trailing, shed by width): Up, Back, Search, Sort (not in Details), Forward, git badge, Home.
  Navigate mode keeps a 50-deep history restoring each place's scroll offset; Backspace and mouse X1/X2 go back/forward;
  a `.lnk` to a folder opens inside the widget. Explorer mode opens folders in Explorer.
- **[S-1993]** Type-ahead ([S-1980]) over the shown names: the match is selected alone and focused,
  realised if it is past the built rows, and scrolled into view. Not while renaming.
- **[S-1328]** Press -> click | rubber band | drag is the pure `FolderGesture` (orig. `gesture.ts`, S-65/#58): 5 DIP dead
  zone, every exit resets, each press wipes prior state, a generation token cancels a deferred drag. Background drags draw
  a band selecting every overlapped item, live ([S-6412]..[S-6415]).
- **[S-6412]** Where a band starts (`FolderPress`, `FolderLasso.Classify`), as Explorer's full-row Details view (user
  report 2026-10-16: rows fill the width, so a full folder had nowhere to start one): only an item's icon and name carry
  the item (click selects, drag carries it out, [S-1332]); a press on the rest of a row or tile, or between or below the
  items, draws the band once it passes the dead zone. A click there selects that item (never opens); on the background
  it clears. The content is at least the viewport tall, so the band can be drawn down to the bottom. Works outside edit
  mode (the press takes the keyboard lease as every widget press does, [S-800]); the band captures the pointer.
- **[S-6413]** How the band combines (now every item-selecting widget's, [S-6625]: `SelectionBand.Mode`, `ItemSelection.Band`): no modifier replaces the selection
  with what the band covers; Shift adds it to the selection the band started with; Ctrl toggles each covered item against
  it (Ctrl wins with both). Live: shrinking the band gives items back. The focus goes to the last covered item.
- **[S-6414]** What it covers ([S-6624], `SelectionBand.GridHits`): every item cell the band overlaps at all, from the layout's geometry
  (rows the list's width × row height; tiles the plan's cells), so items not built yet ([S-1887]) are selected too. The
  band is kept inside the content. Look: the desktop's band ([S-6624]), above the items; the items' marks are [S-6627].
- **[S-6415]** Edge scroll (`FolderLasso.ScrollVelocity`/`Step`): with the band's pointer within 24 DIPs of the list's top
  or bottom (a quarter of a short list), or past it, the list scrolls that way at 20 DIP/s per DIP of depth, at most
  2400 DIP/s, and the band follows. A frame tick (`CompositionTarget.Rendering`) runs only while the pointer is in a
  zone with the band down; a frame step is at most 0.1 s, so a stall never throws the list.
- **[S-6416]** The item menu is Windows 11 File Explorer's (user report 2026-10-16: it did not look like it), its
  commands per selection from `FolderMenu.Item` (Core, tested), drawn by `ItemMenu` ([S-1740]). Icon row: Cut, Copy,
  Paste (a single folder, the paste target; greyed with nothing to paste), Rename (one item), Share (files only, the
  shell's `Windows.ModernShare`), Delete. List: Open (a file's glyph, an open folder's), Open with > (one file: the apps
  `SHAssocEnumHandlers` recommends, at most 8, with their icons when the icon is an exe on disk, then Choose another app),
  Open in new window (one folder, navigate mode: Explorer), Open file location, Pin to Quick access (folders, shell
  verb `pintohome`), Pin to Start (one folder, exe or shortcut, `pintostartscreen`), Compress to ZIP file, Copy as path
  (quoted), Properties (Explorer's wrench); the cloud pins (a cloud item selected); Show more options. A shell verb the
  shell does not offer here (an already pinned folder) says so at the widget. Not ported, as not Explorer's built-ins:
  Open in Terminal, Open in new tab; app-added lines live under Show more options.
- **[S-6417]** Keys beside the lines are Explorer's, and only keys the widget answers through the lease ([S-1327]):
  Enter, Ctrl+X/C/V/A, F2, Delete, Ctrl+Shift+C on Copy as path (the selection's quoted paths), Alt+Enter on Properties
  (the selection's, or the shown folder's with nothing selected), and Shift+F10 on Show more options, which opens the
  shell's full menu as in Explorer (the Menu key opens ours). Written by [S-1753] (user report 2026-10-16: the
  shortcut naming did not match Explorer's).
- **[S-6418]** Background menu (`FolderMenu.Background`): Paste, Select all; Up (navigate mode), Refresh; New >; Open in
  Explorer, Copy as path, Properties; Show more options. Its lone Paste stays a labelled line ([S-1741]). No View or
  Sort submenus, though Explorer has them: how a widget looks is its settings' ([S-1746]).
- **[S-6419]** Compress to ZIP file (`ZipFiles`, `System.IO.Compression`, off the UI thread): one new ZIP beside the
  items, folders with all they hold (hidden files too; directory links not followed), named after the item the menu was
  opened on without its extension, made unique ("a (2).zip", `FolderMenu.ZipName`), then renamed in place as Explorer
  does. A failure removes the half-written ZIP (its own output, nothing of the user's) and says so at the widget.
- **[S-1334]** Every rowless state says which it is (status messages; "Nothing here matches." under a filter).
- **[S-1335]** Card menu: Open in Explorer, Refresh. The views, row or tile sizes, hidden items and file extensions
  are settings only ([S-1746]).
- **[S-1339]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-4120]** *(Retired: one widget kind per layout; they are one kind with four styles, [S-4123].)*
- **[S-4121]** Each style shows only what it draws (user decision 2026-10-12;
  `FolderConfig.ShownSettings`, [S-6070]), and the config holds the union, a style ignoring the rest (`Rows`,
  `Tiles`, `Extensions`, `DetailColumns` read a neutral value where the style draws none). Every
  style: the folder, Open files with (double / single click), Open folders (Explorer / inside the
  widget), Show hidden items. List: row size, file extensions, Sort by + Reverse. Details: row size,
  columns, file extensions; no sort row (its column headers sort, saved). Grid: tile size, file
  extensions, Sort by + Reverse. Icons: tile size, Sort by + Reverse; no extensions (it draws no
  names; a rename there edits the stem). One tile size for grid and icons (default Medium). Picking
  another style builds the menu again ([S-6071]); these lines stand under Style ([S-1337]). Retired for every style: the stored keyword filter (the
  header search covers it), Folders first (always on), Stretch tiles (tiles keep their preset height).
- **[S-1337]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style what the style draws ([S-4121]): Row size or Tile size (a
  choice), Details columns (a submenu of switches), Show file extensions, Sort by (Name / Date
  modified / Size, with Reverse order under them). Then Appearance ([S-3723]), **Folder** (its name
  at the line's end, the user folder's while empty) with Browse… (Windows' folder picker, [S-3730])
  and Type a path… (asked beside the menu, [S-3721]: checked by `FolderPath` as it is typed and
  refused with the reason, never repaired; empty means the user folder), **Show hidden items**,
  **Open items with** (Double / Single click) and **Open folders** (In Explorer / Inside the widget).
  (Tests `FolderPropertiesMenuTests`.)
- **[S-4122]** Debug renders: `--widget-png Folder <out> --style list|details|grid|icons --state web|wallpapers`
  lists `%WINDIR%\Web` or its Windows wallpapers (read-only) instead of the profile; its menu's
  lines are drawn by `--editbar-png` (`props-*-folder-*.png`).
- **[S-4123]** One kind, **Folder** (`Folder`, 17, `FolderConfig`, `folder`, 300×280, glyph E8B7), with
  four styles ([S-6000]; `FolderStyle`, stored as `"style"`, menu order): **List** (the default,
  E8B7), **Details** (F12B FolderHorizontal), **Grid** (ECA5 Tiles) and **Icons** (F0E2 GridView).
  One view (`FolderView`) reads the style from its config: a change re-lays and re-draws what is in
  hand, the listing, selection and history kept. Folder details, Folder grid and Folder icons were
  kinds of their own (114..116); a state file holding one drops that widget at load ([S-1050]).
  Their names still find the folder in search ([S-943]).

Not ported: header double-click opens Explorer (orig. S-07; the card owns the header, so "Open in
Explorer" is in the card menu); image-item inline toggles (orig. S-29).

Needs a live pass: a real folder (navigation, the lasso, double-click and Enter opening, rename,
New, Delete to the Recycle Bin), drag in and out with Explorer and Outlook, the Explorer menu's
two-tone icons and Show more options, the clipboard both ways with Explorer, a ZIP, OneDrive pin
states, edit mode leaving it inert, multi-monitor and mixed DPI.
