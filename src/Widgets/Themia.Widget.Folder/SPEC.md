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

- **[S-1320]** Settings (orig. S-14..S-24, S-58), one config for every style ([S-4121]): `Path` (empty = profile),
  `Columns` (ordered; default Size, Modified, Type; `Repair` drops unknown/repeats loudly),
  `Navigation` Explorer/Navigate, `ClickMode` (`ItemClickMode`, default Double), `Sort`/`Descending`,
  `ShowHidden`, `ShowExtensions` (default off), `Style`. `ShowDetail`/`MaxCount`, and since [S-4121] `View`, `StretchTiles`,
  `Filter` and `FoldersFirst`, are gone (no compatibility); so are `ItemSize` (rows of 24/28/32/40) and `TileSize`
  (tiles of 80/110/150) since the family's rows and cells decide their size ([S-6704], [S-6713]; group G6 2026-10-29).
- **[S-1323]** Layout (`FolderLayout.Plan`, on the family's blocks): over the items, as the config says
  ([S-11203]), the search box (a 32 text box at the side inset, 8 over the items; also while the header's Search
  has it open), the path as a row of 28 (tail first, clickable: the folders above in Body secondary, the shown one
  primary, 16 chevrons tertiary, 4 apart) and the details column titles (a Caption line and 8, as the kit's table,
  [S-7034]); no status footer ("24 items · 2 selected", retired [S-666]). Rows are the Row block's one line of 28
  ([S-6711], [S-6712]): the file's own 16 icon as the lead, the name primary; List's one value is the date modified
  (secondary) where Date modified is on, Details puts every chosen column in the name's place (`RowBlock.Cells`, Body
  secondary, 12 apart; none dropped for want of room). Grid and Icons are Row.Cell grids ([S-6713],
  `RowRules.Cells`): the icon (or the picture's thumbnail) 32 at every width, Grid's name one
  Caption line under it (the whole name on selection, [S-6629]), Icons the icon alone; the cells share the width out
  12 apart, their marks 4 around them. Only whole rows (or rows of cells) show; the rest scrolls under the fade.
  Over 1000 items: the first 1000 and a "N more in Explorer…" row (a Row block with the open glyph, its name
  secondary). Retired with group G6: the list's unlabelled Size and Type columns (Details is the table), XL's one-size-up
  tiles and the tiles' two- and three-line names.
- **[S-11203]** Converted to the sizing ([S-2438]), not split: a file browser keeps a **free size**, 220..800
  wide and 160..640 high (`FolderLayout.Sizing`), and is the one kind where more room shows more of the user's
  own entries, as a window does (more rows, more columns of cells; the rest scrolls). What stood or fell with
  the box is a setting under Style, each on or off: **Search box** (off: the header's Search opens it, as
  before; on: it always stands and the header has no Search), **Path** (off), and in Details **Column
  titles** (on) and in List **Date modified** (on). Each style states its own natural size and least: the
  list, the grid and the icons 300 wide, Details its least width and 60 more, its least the insets, the 16
  lead, a name of 120 and every chosen column with its 12 (492 for Size, Modified, Type); a new one's height
  the whole rows (or rows of cells) nearest 280 under what stands over them (the list 272, the grid 292,
  the icons 300, Details 268); its least height holds what stands over the items and one row. No tier
  decides anything: the path at Expanded × Expanded, the columns that fit, the 48 cells and the list's date
  from 180 are gone (`FolderColumns.Fit` with them).
- **[S-6411]** The rightmost column ends at the row's right inset, the same as the header's buttons, at
  every width and in every style (user report 2026-10-16: from 400 DIPs the list put Size last, blank for
  every folder, so the dates stopped a column short of the edge). The list has one value, the date modified,
  end-aligned as every Row block's value; in Details the last column, whatever it is, is drawn from the right
  of its slot, its title included (`FolderColumns.AlignsRight(column, last)`). Grid and Icons share the row's
  width out between their cells ([S-1323]).
- **[S-1326]** Header ([S-6710], `FolderLayout.Header`): at most two 24 buttons — Back and Up in navigate mode (the
  disabled role when they cannot go), else Search and Sort (Search alone in Details, whose column titles sort) — the
  git badge as its informing label (List and Details) and the busy ring; the header folds them by its own rule.
  Forward, Home (navigate mode, where they can act) and, in navigate mode, Search are on the card menu ([S-1335]).
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
- **[S-1334]** Every rowless state says which it is in the Status row at the top of the content ([S-6714]): the
  folder's glyph for loading, an empty folder and "Nothing here matches." under a filter; the warning for a folder
  that cannot be read (missing, denied, not a path).
- **[S-1335]** Card menu: in navigate mode Search, Forward (when there is one) and Home (away from the widget's
  folder), the header having room for two buttons only ([S-1326]); then Open in Explorer, Refresh. The views, the
  columns, hidden items and file extensions are settings only ([S-1746]).
- **[S-1338]** Sort by name is Explorer's order (`FolderRules.CompareNatural`, issue 194): a run of digits
  compares by its value, so "Week 2" comes before "Week 10", and the rest ignores case; names that still tie
  ("7" and "007", "README" and "readme") get one fixed ordinal order. Folders stay first, and the name is the
  tie-break of the date and size sorts too.
- **[S-1339]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-4120]** *(Retired: one widget kind per layout; they are one kind with four styles, [S-4123].)*
- **[S-4121]** Each style shows only what it draws (user decision 2026-10-12;
  `FolderConfig.ShownSettings`, [S-6070]), and the config holds the union, a style ignoring the rest (`Extensions`,
  `DetailColumns` read a neutral value where the style draws none). Every
  style: the folder, Open files with (double / single click), Open folders (Explorer / inside the
  widget), Show hidden items. List: file extensions, Sort by + Reverse. Details: columns, file extensions; no sort
  row (its column titles sort, saved). Grid: file extensions, Sort by + Reverse. Icons: Sort by + Reverse; no
  extensions (it draws no names; a rename there edits the stem). Picking
  another style builds the menu again ([S-6071]); these lines stand under Style ([S-1337]). Retired for every style: the stored keyword filter (the
  header search covers it), Folders first (always on), Stretch tiles (tiles keep their height), and with group G6 the
  Row size and the Tile size (the family's rows and cells, [S-1320]).
- **[S-1337]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style what the style draws ([S-4121]): Details columns (a submenu of switches), Show
  file extensions, Sort by (Name / Date modified / Size, with Reverse order under them). Then Appearance ([S-3723]) and, each group
  behind a divider, **Folder** (its name
  at the line's end, the user folder's while empty) with Browse… (Windows' folder picker, [S-3730])
  and Type a path… (asked beside the menu, [S-3721]: checked by `FolderPath` as it is typed and
  refused with the reason, never repaired; empty means the user folder) and **Show hidden items**;
  then **Open items with** (Double / Single click) and **Open folders** (In Explorer / Inside the widget).
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
- **[S-2031]** A click on the card ([S-2000]) opens the folder it shows now (where it navigated to,
  else its own) in File Explorer (`FolderModule.Opens`). The file area is the widget's own: a click
  there selects, starts a band or clears the selection and never reaches the card ([S-2004]), so the
  click that opens is on the header (its icon and title).

Not ported: header double-click opens Explorer (orig. S-07; a single click on the header does,
[S-2031], and "Open in Explorer" is in the card menu); image-item inline toggles (orig. S-29).

Needs a live pass: a real folder (navigation, the lasso, double-click and Enter opening, rename,
New, Delete to the Recycle Bin), drag in and out with Explorer and Outlook, the Explorer menu's
two-tone icons and Show more options, the clipboard both ways with Explorer, a ZIP, OneDrive pin
states, edit mode leaving it inert, multi-monitor and mixed DPI.
