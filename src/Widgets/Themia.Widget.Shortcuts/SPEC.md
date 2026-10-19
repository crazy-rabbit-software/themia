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
lines, JSON context), `ShortcutsConfig.cs` (the items, the styles, sizes and switches, the layout items),
`ShortcutsView.cs` (the items, health, search, the bin badge through `IWidgetHost.RecycleBin`) with
`.Cells.cs` (tiles, rows, icons through `AppIconSource`, site icons through `IWidgetHost.Favicons`),
`.Input.cs` (selection, keys, the item menu, opening through its launcher, layouts through
`IWidgetHost.AppLayouts`) and `.Drop.cs` (drops through `IWidgetDropTarget`, a Start-menu app through
`IWidgetHost.InstalledApps`, reorder, the drag out through `IWidgetHost.FileOps`), `ShortcutRules.cs`,
`ShortcutDrop.cs`, `ShortcutLayout.cs`, `ShortcutSelection.cs`, `ShortcutsMenu.cs` (its properties-menu lines: pickers
through `SettingsHost.OwnPicker`, the layout editor through `SettingsHost.AppLayouts`), `Preview/`
(Debug renders: `--state sample|grid3|grid7|grid20|list7|details7`), `Localization/Locales/` (its own
`ShortcutsStr`, 30 languages). Tests: `Tests/ShortcutsTests.cs`, `Tests/ShortcutDropTests.cs` (run by
`tests/Themia.Modules.Tests`); its config under the root's rules is the module host's
`ShortcutsHostTests`, `LauncherPersistenceTests` and `ListStylesHostTests`.

- **[S-1380]** Config (original S-21..S-23, S-33, S-46..S-48, S-71): `Style` Grid (default) / List /
  Details ([S-6401]), `GridSize` and `ListSize` Small / Medium / Large, `GridStretchHeight`, `GridIconOnly`,
  `ShowFileExtension`, `ClickMode` (shared `ItemClickMode`, Double by default). Enums are written as
  names; `Repair` brings unknown values back, drops null items and mends blank or repeated ids.
  `ShowTarget` and `OpenOnSingleClick` are gone (the details view and `ClickMode` replace them).
- **[S-6401]** Shortcuts' styles ([S-6000]) are its views, `ShortcutsConfig.Style`
  (`ShortcutsStyle`, `"style": "grid"`): **Tiles** (Grid), **List** and **Details**, in that order;
  the View expander is gone. Under Style the menu offers the tile size, icons only and stretch for
  Tiles (`ShortcutsConfig.ShownSettings`, [S-6070]), the row size for List and Details, and file
  extensions for all three.
- **[S-1381]** Display name: label, else host without `www.`, else the Windows location's name, else
  the file name without its extension unless `ShowFileExtension` (original S-05, S-18, S-48).
- **[S-1382]** Grid ladder (`ShortcutLayout.Grid`): tiles keep the chosen size and the columns share
  out the width; L and XL step the size up once; XL adds a caption line under the name (host, the
  folder it is in, or "Windows location"). Names go, then tiles shrink (down to a 16 px icon), only
  when keeping them would leave fewer than four items (or all of them) on screen: XS 3×2 icon-only,
  S icons, M 3 columns with names, Wide one row of five named tiles, Tall 2 columns. Stretch shares
  the height out when every row fits. Every view fills from the top-left of its box, row by row
  (a grid in the columns the width was shared into); room the items do not use stays at the right and
  bottom, never shared out to centre them (was: a grid whose items all fit sat centred as a group).
- **[S-1383]** Real icons: a file, folder, executable or `shell:` location gets Explorer's icon
  through `IShellItemImageFactory` (icon only, never a thumbnail) at twice the drawn size, one
  extraction at a time (concurrent first requests return E_PENDING; one immediate re-ask, no wait),
  cached per path, size and variant; a web address gets the site's own `/favicon.ico` through the
  shared guarded `FaviconCache` (no third-party service). The kind glyph shows until the icon
  arrives and stays when there is none. List and details rows: 32 / 40 / 48 high (one step smaller
  on a short tile), the target as a caption line on L/XL, two columns on XL and on a Wide strip.
- **[S-1384]** Details view: name; target column from Wide up; kind column (Web address / Windows
  location / File / Folder / Missing / Not allowed) on XL; column header when it has the target
  column and room for two rows under it. No status footer at any size (was: "N shortcuts", "N
  selected" at XL; retired, concise widgets).
- **[S-1385]** Search (original S-43): the header's search button (or the card menu) opens a box
  above the items; it narrows to items whose shown name or target holds the text, ignoring case.
  Escape closes it and clears the query; Enter opens the first match. Typing rides the keyboard lease.
- **[S-1386]** Broken targets (original S-44): a local path is checked on attach and every minute on
  the thread pool; a missing one is dimmed, struck through and its tooltip says "(target missing)".
  Web and shell items are never marked. A UNC path is never probed (no icon, no existence check):
  touching it connects to that server with the user's credentials, and the path came from a file
  anything can rewrite. It still opens when clicked. A refused target is dimmed with a warning glyph
  and its refusal in the tooltip, and cannot be opened.
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
  count badge ("99+" cap; a dot under a 24 px icon), none when empty or unknown; tooltip
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
  Undo remove. The view, sizes, icons only, stretch and file extensions are on the properties menu only ([S-1746], [S-1388]).
- **[S-1392]** Items enter the widget's own surface through one method (`AddItems`); every
  target-specific choice (icon, menu, open, health) switches on `LaunchKind`. The seams PD builds on.
- **[S-1393]** A new Shortcuts widget starts with the Recycle Bin and This PC (original S-20); the
  "under the clock on a new screen" placement belongs to the default layout and is not ported here.
- **[S-1400]** Files, folders, links and web addresses dropped onto the Shortcuts widget's content become
  items at the end, by reference (never copied). The drop arrives only through the desktop surface's one
  OLE target and `IWidgetDropTarget` (no XAML `AllowDrop`, no target of the widget's own). Every target
  goes through `LaunchTargets`; refused ones and ones the widget already holds are skipped (logged).
  Files win: URLs count only when no path came with the drop (orig. S-34). The widget shows an accent
  edge and the caption "Add to Shortcuts" while it would take the drop (orig. S-32).
- **[S-1401]** A `.url` becomes its http(s) address named after the file; a `.url` to any other scheme
  (`steam://`) is kept as the `.url` file itself with its `IconFile=` as the item's `IconPath` — the
  launch allow list is not widened (orig. S-29/S-73). A `.lnk` becomes its target named as the link,
  unless it passes arguments or cannot be resolved, then the link itself (orig. S-30). `IconPath` must
  be a local path (repair drops anything else).
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

Needs a live pass: drops from Explorer, Edge, Chrome and the Start menu, pointer reorder and the drag
out on the real surface, a full and an empty Recycle Bin item, real icons and site icons, a layout
launched from an item, the Add submenu's pickers and typed address on the desktop.
