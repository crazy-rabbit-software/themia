# Shortcuts

The module `shortcuts` ([S-6911]): links to files, folders, apps, web addresses, Windows locations and
app layouts, as tiles, a list or a table. Its tags are this file's alone; the root `SPEC.md` holds what
it shares and reads of the host, with their root tags: the bin's Empty ([S-4863],
the host's `RecycleBinStatus` behind `IWidgetHost.RecycleBin`), the keyboard move ([S-3802]), the
desktop surface's one drop target and a browser's or the Start menu's drag ([S-1020], [S-1402]), the
app layouts (the tree, the walk, the placement and window matching, the editor: [S-1403]..[S-1409],
[S-1411]..[S-1414]; `LayoutTree`, `LayoutPlan` and `AppLayoutModel` in the SDK, the host's
`AppLayoutLauncher` and `LayoutEditorWindow` behind `IWidgetHost.AppLayouts`, which finds an item
through `IHoldsAppLayouts`) and the layout item's naming, menu and repair ([S-1405], [S-1406]).

Owning files: everything in this folder: `ShortcutsModule.cs` (descriptor, glyph, view factory, menu
lines, JSON context), `ShortcutsConfig.cs` (the items, the styles and switches, the layout items),
`ShortcutsView.cs` (the items, health, search, the bin badge through `IWidgetHost.RecycleBin`) with
`.Cells.cs` (tiles, rows, icons through `AppIconSource`, site icons through `IWidgetHost.Favicons`),
`.Input.cs` (selection, keys, the item menu, opening through its launcher, layouts through
`IWidgetHost.AppLayouts`) and `.Drop.cs` (drops through `IWidgetDropTarget`, a Start-menu app through
`IWidgetHost.InstalledApps`, reorder, the drag out through `IWidgetHost.FileOps`), `ShortcutRules.cs`,
`ShortcutDrop.cs`, `ShortcutIcons.cs` (a shortcut's own icon), `ShortcutLayout.cs`, `ShortcutSelection.cs`, `ShortcutsMenu.cs` (its properties-menu lines: pickers
through `SettingsHost.OwnPicker`, the layout editor through `SettingsHost.AppLayouts`), `Preview/`
(Debug renders: `--state sample|grid3|grid7|grid20|list7|details7|icons`), `Localization/Locales/` (its own
`ShortcutsStr`, 30 languages). Tests: `Tests/ShortcutsTests.cs`, `Tests/ShortcutDropTests.cs`, `Tests/ShortcutIconsTests.cs` (run by
`tests/Themia.Modules.Tests`); its config under the root's rules is the module host's
`ShortcutsHostTests`, `LauncherPersistenceTests` and `ListStylesHostTests`.

- **[S-1380]** Config (original S-21..S-23, S-33, S-46..S-48, S-71): `Style` Grid (default) / List /
  Details ([S-6401]), `GridIconOnly`, `ShowFileExtension`, `ClickMode` (shared `ItemClickMode`, Double by
  default). Enums are written as names; `Repair` brings unknown values back, drops null items and mends
  blank or repeated ids. `ShowTarget` and `OpenOnSingleClick` are gone (the details view and `ClickMode`
  replace them), and with group G6 (2026-10-29) `GridSize`, `ListSize` and `GridStretchHeight`: the family's
  cells and rows decide their size and keep it ([S-6704], [S-6713]).
- **[S-6401]** Shortcuts' styles ([S-6000]) are its views, `ShortcutsConfig.Style`
  (`ShortcutsStyle`, `"style": "grid"`): **Tiles** (Grid), **List** and **Details**, in that order;
  the View expander is gone. Under Style the menu offers icons only for Tiles
  (`ShortcutsConfig.ShownSettings`, [S-6070]) and file extensions for all three (not on a grid of icons
  alone, which draws no names). The tile size, row size and stretch lines are gone ([S-1380]).
- **[S-1381]** Display name: label, else host without `www.`, else the Windows location's name, else
  the file name without its extension unless `ShowFileExtension` (original S-05, S-18, S-48).
- **[S-1382]** Grid (`ShortcutLayout.Grid`): Row.Cell cells ([S-6713]), the config's icon (Icon size:
  Small 32, Large 48) over its name in Caption (none on a grid of icons alone), one line (the whole name
  on selection, [S-6629]); the config's columns share the width out 12 apart, the marks 4 around the
  cells; the cells fill from the top-left, row by row. Nothing in it follows the box ([S-11202]).
- **[S-1383]** Real icons: a file, folder, executable or `shell:` location gets Explorer's icon
  through `IShellItemImageFactory` (icon only, never a thumbnail) at twice the drawn size, one
  extraction at a time (concurrent first requests return E_PENDING; one immediate re-ask, no wait),
  cached per path, size and variant; a web address gets the site's own `/favicon.ico` through the
  shared guarded `FaviconCache` (no third-party service). The kind's glyph at the icon's own size shows
  until the icon arrives and stays when there is none. List and details rows are the Row block
  ([S-6711], [S-6712], `ShortcutLayout.Rows`), one column: the list's rows as its Lines per row say, one
  line of 28 with a 16 icon or two lines of 40 with a 32 icon (the name, the target under it: the site,
  the folder it is in, or "Windows location"); the details table one line.
- **[S-1384]** Details view: the table on the Row block (`RowBlock.Cells`, Body: the name primary, the rest
  secondary, 12 apart): name; the **Target** column and the **Kind** column (Web address / Windows
  location / File / Folder / Missing / Not allowed) as the two switches under Style say (Target on, Kind
  off for a new one); column titles (a Caption line and 8, secondary, at the text edge) over the columns
  it has, whenever it has one besides the name. No status footer.
- **[S-11202]** Converted to the sizing ([S-2438]): the user's list has a known length, so the size
  follows it (`ShortcutLayout.Sizing`). **Grid**: Columns (1..8, a new one 4) and Icon size (Small 32,
  Large 48) under Style; its width is its columns (12 + n × (cell + 12), a named cell 64 or 80, an icon
  alone 32 or 48), stepped by a column, dragging its side sets Columns (`SizeSets`, the items keep their
  ids); its one height is the rows its shortcuts take (36 + rows × (cell + 12), at most 6 rows, then it
  scrolls). **List**: Lines per row (1 or 2, a new one 2) under Style; one height, one row per shortcut
  (at most 12, then it scrolls), 220..520 wide, a new one 300. **Details**: the Target and Kind
  switches; one height (the titles and one row per shortcut, at most 12), from its least width (the
  lead, a name of 120, a target of 160 and a kind of 120 where shown) to 360 more. An open search box
  takes its line over the items, which scroll in the rest; the card keeps its size. The tier ladder is
  gone: no tile size, no second list column, no columns or titles by width. Repair holds Columns 1..8,
  Lines 1..2 and a known Icon size.
- **[S-1385]** Search (original S-43): the header's search button (a 24 header button, [S-6710]) or the card
  menu opens a box above the items (a 32 text box at the side inset, 8 over them); it narrows to items whose
  shown name or target holds the text, ignoring case. Escape closes it and clears the query; Enter opens the
  first match. Typing rides the keyboard lease.
- **[S-1386]** Broken targets (original S-44): a local path is checked on attach and every minute on
  the thread pool; a missing one is faded (the desktop's cut look, `SelectionLook.Faded`), struck through and
  its tooltip says "(target missing)".
  Web and shell items are never marked. A UNC path is never probed (no icon, no existence check):
  touching it connects to that server with the user's credentials, and the path came from a file
  anything can rewrite. It still opens when clicked. A refused target is faded with a warning glyph
  (the critical role) and its refusal in the tooltip, and cannot be opened.
- **[S-1387]** Item menu (original S-26): Open (disabled when refused) or "Open N" on a right-click
  inside a multi-selection; web: Copy URL; file: Open file location, Copy path; shell: Copy path;
  Rename (off for several; no moves: an item is dragged, [S-1410], or moved with Alt+Shift+Up /
  Down on the cursor, [S-3802]); Remove / Remove N (danger). Remove keeps one
  step of "Undo remove" on the card menu. Rename edits in place (Enter / losing focus commits,
  Escape cancels); a name equal to the derived one clears the label.
- **[S-1388]** Options (original S-16, S-45, S-61), all on the properties menu ([S-3719], user request
  2026-10-19); the widget has **no settings page**, so no More settings line and no Settings on its
  card menu ([S-1750]). Under Style: what the look draws with ([S-6401]); then Appearance and,
  each group behind a divider, **Open with** (Double click / Single click), then **Add**: File… and Folder… (the pickers), Web address or path… (asked
  beside the menu, [S-3721]: `LaunchTargets` refuses it as it is typed, with the reason, and it
  cannot be added while refused; nothing is corrected), App layout… (added, then its editor,
  [S-1411]; greyed where there is none), and Windows locations, one switch each with Explorer's
  icon: on adds it, off removes every item pointing there, so the switch says what the widget holds;
  and the shortcuts themselves ([S-3729]). Shortcuts are ordered, renamed and removed on the widget ([S-1387], [S-1410]); the list editor
  is gone. (Tests `ShortcutsMenuTests`.)
- **[S-1389]** Recycle Bin (original S-75): an item pointing at `shell:RecycleBinFolder` shows a
  count badge ("99+" cap) over a 32 or 48 icon: the neutral count pill over a picture ([S-6706],
  `HeaderBlock.Count`: Caption on the solid fill), at the icon's top-right corner moved out 8 and up 4 by a
  transform; none on a 16 icon (no room: the tooltip says how many), none when empty or unknown; tooltip
  "name — N items (size)" / "name — empty". `RecycleBinStatus` is process-wide, single-flight,
  asked when the bin changes, never on a clock: the command window registers for the shell's change
  notifications on the Recycle Bin folder (`SHChangeNotifyRegister`, recursive, shell level), and a
  change asks again only while a widget holds the bin (`RecycleBinStatus.Watch`); a change with nobody
  watching marks the answer stale and the next watcher asks once. A change during a query asks once more
  after it, so a burst costs two queries. It keeps the last answer on failure, and the icon is fetched per state (empty / full).
- **[S-1390]** Selection (original S-40..S-42, S-62, S-63) is the desktop's, through the one item
  selection ([S-6620]..[S-6629]): click, Ctrl, Shift, a press on a selected item narrowing on release
  (dragging it reorders the whole selection, [S-1410]), right-click, the rubber band from the empty
  space (Ctrl toggles, Shift adds, [S-6625]), a background click clearing it (Ctrl or Shift keeps it),
  the selection keys (arrows by the grid, Shift extends, Ctrl+arrows move the focus, Space/Ctrl+Space
  toggle, Home/End, PageUp/PageDown, Ctrl+A) and the look, a labelled tile's full name included
  ([S-6629]); the selection stays, drawn inactive, while the user is elsewhere ([S-6626]). The
  widget's own keys while it holds the lease (`IWidgetKeys`, [S-800]): Enter opens the selection,
  Delete removes it, F2 renames, the Menu key opens the item menu, Escape cancels rename, then closes
  search, then clears the selection. "Select all" is on the card menu too.
- **[S-1995]** Type-ahead ([S-1980]) over the shortcut names selects the match alone and moves the
  cursor to it; not while a rename box or the search box is open.
- **[S-1391]** Card menu (original S-27, S-49): Search, Open on single / double click, Select all,
  Undo remove. The view, icons only and file extensions are on the properties menu only ([S-1746], [S-1388]).
- **[S-1392]** Items enter the widget's own surface through one method (`AddItems`); every
  target-specific choice (icon, menu, open, health) switches on `LaunchKind`. The seams PD builds on.
- **[S-1393]** A new Shortcuts widget starts with the Recycle Bin and This PC (original S-20); the
  "under the clock on a new screen" placement belongs to the default layout and is not ported here. One
  with no shortcut left waits in the card's Set up row: "Add a shortcut" ([S-1862]); a drop still lands there.
- **[S-1400]** Files, folders, links and web addresses dropped onto the Shortcuts widget's content become
  items at the end, by reference (never copied). The drop arrives only through the desktop surface's one
  OLE target and `IWidgetDropTarget` (no XAML `AllowDrop`, no target of the widget's own). Every target
  goes through `LaunchTargets`; refused ones and ones the widget already holds are skipped (logged).
  Files win: URLs count only when no path came with the drop (orig. S-34). The widget shows an accent
  edge and the caption "Add to Shortcuts" while it would take the drop (orig. S-32).
- **[S-1401]** A `.url` becomes its http(s) address named after the file; a `.url` to any other scheme
  (`steam://`) is kept as the `.url` file itself with its `IconFile=` as the item's `IconPath` — the
  launch allow list is not widened (orig. S-29/S-73). A `.lnk` becomes its target named as the link,
  unless it passes arguments, has an icon of its own (`ShortcutLinks.KeepsLinkItself`, issue 190) or
  cannot be resolved, then the link itself (orig. S-30). `IconPath` must
  be a local path (repair drops anything else).
- **[S-1416]** A Start menu app dropped as a shell item (no path, [S-1402]) becomes the Start menu's own
  shortcut of that name; a packaged (Store / MSIX) app with none becomes its Apps-folder item
  `shell:AppsFolder\<AUMID>`, named as Start names it (issue 189: Claude); one with neither, or one
  the widget already holds, is skipped (logged). `ShortcutDrop.FromStartMenu`.
- **[S-1417]** A shortcut's own icon (issue 157): each shortcut's line in the properties menu's list (an
  app layout's too) has an **Icon** submenu, its line ending with what is chosen (the emoji, or the
  picture's file name): **Picture…** (the shell's picker over png, jpg, jpeg, bmp, gif, ico and svg;
  stored as `IconPicture`, a local path), **Emoji…** (asked beside the menu, [S-3721]: one emoji or
  character, typed or from Windows' emoji panel, Win + .; stored as `IconEmoji`) and **Automatic icon**
  (off while there is none). A picture replaces an emoji and an emoji a picture; Reset clears both and
  keeps a `.url`'s own icon ([S-1401]). The cell draws it in the icon's square at every size of the
  ladder ([S-1382], [S-1383]): the emoji at once in the glyph's place (colour, its ink box scaled to the
  square), a picture loaded over the glyph like a real icon (an `.ico` through the shell, which picks
  its best size; an svg rasterised at the size; a bitmap decoded at twice the drawn size, a GIF held
  still). A picture that is gone, past 16 MB or will not decode is logged and the automatic icon drawn:
  no notice. The menu's list rows keep the target's own icon. (Tests `ShortcutIconsTests`.)
- **[S-1418]** Which icon a cell draws, best first (`ShortcutIcons.Choose`): the user's picture while it
  reads, the user's emoji, the icon a dropped `.url` named (`IconPath`, [S-1401]), then the target's own
  icon as Explorer draws it, Windows' own choices included: a `.lnk` kept as a link because it has an
  icon of its own ([S-1401]) shows that icon, not its target's (the shell item of the link itself, whose
  icon handler reads the link's icon location), and a folder with a `desktop.ini` `IconResource` shows
  that icon (the folder's shell item honours it). The Folder widget's items take their icon from the
  same shell item factory (`IFileIcons`, [S-1324]), so a link or folder there shows Windows' icon too;
  it has no icon of its own to choose.
- **[S-1419]** The own icon is checked as it is set and on load (`ShortcutIcons`, `Repair`): a picture
  must be a local, probe-able path ([S-1386]: never UNC) with one of the picture extensions; an emoji
  exactly one grapheme (`StringInfo`), trimmed, at most 32 UTF-16 units, not a control, format,
  separator, private-use or unassigned character. A refused one is dropped with a repair line, and an
  item holding both keeps its picture. Nothing is migrated: the fields are new ([S-1050]).
- **[S-1410]** Pressing an item and moving 5 px drags it (the selection, if it was on it); the list moves
  under the pointer; release commits exactly the preview (before the item under the pointer's first
  half, else after; empty space = end); an unchanged order writes nothing.
- **[S-1415]** Dragging items past the widget's edge hands their local files and folders to an OLE drag
  (Copy or Link only — never Move: a shortcut drag must not move the user's file). Dropped back on a
  Shortcuts widget it reorders (same widget) or moves (another; Ctrl copies; the receiver adds first,
  then the source removes — orig. S-35).
- **[S-4864]** The same Empty is on a Shortcuts item pointing at the Recycle Bin ([S-1389], issue
  #120): its item menu offers "Empty Recycle Bin…" after Open, off unless
  `RecycleBinState.CanEmpty` (items in the bin, no empty already out). One implementation,
  `RecycleBinStatus.EmptyAsync`, guards and empties for both; "not twice at once" is process-wide
  (`RecycleBinStatus.Emptying`), so the widget's button and every Shortcuts menu agree. Not on a
  multi-selection. (Tests `S4864_`, `S4863_`.)

Not ported: Ctrl+Z op log (S-36: one-step "Undo remove" instead); "Change file…" from the widget
(the picker needs an owner window the desktop layer cannot lend; a wrong target is removed and added again);
the dedicated "Add shortcut" window (S-66: the menu's Add carries the same affordances);
favicons are cached per run in memory, not on disk.
- **[S-11934]** View links ([S-11930]): an item whose target is an executable (`….exe`, a local path) is that
  app (`ShortcutRules.AppKey`, keyed by `LinkKeys.App`); a folder, a document, a site, a shell location or a
  layout is none. The pointer or the keyboard cursor on it publishes the app pointed at; an item that is the
  app pointed at elsewhere on the view is drawn as under the pointer (the desktop's hover), one that is the app
  picked takes the item layer's selected fill (`LinkLook.Row`: a tile, whose mark fills the cell, without the pill). A click keeps its meaning (select, open):
  shortcuts never pick.

Needs a live pass: drops from Explorer, Edge, Chrome and the Start menu, pointer reorder and the drag
out on the real surface, a full and an empty Recycle Bin item, real icons and site icons, a picture and an emoji chosen through the Icon submenu (the picker on
the desktop, the emoji panel in the ask box), a link given its own icon with Change Icon… and a folder
icon set in its Properties shown on Shortcuts and on the Folder widget, a layout
launched from an item, the Add submenu's pickers and typed address on the desktop.
