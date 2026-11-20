# Fence (fence)

The module `fence` ([S-6911]). Its tags are [S-10900]..[S-10999], this file's alone; the root `SPEC.md` holds the
host's sections, the desktop's rules for fences among them (FENCES, [S-13000]..[S-13014]).

Stardock Fences' feature, not its look (the owner, 2026-11-19: "think about how we could integrate something like fences
... to create a group of icons with a header. it should also be build as a widget that can be created like fences does
it"): a box of real desktop items with a title bar of its own.

Owning files: everything in this folder. Tests: `Tests/FenceTests.cs` (run by `tests/Themia.Modules.Tests`); the rules
it stands on are Core's and the SDK's (`DesktopFencesTests`, `FenceGridTests`, `FenceRulesTests`, `DesktopFileUndoTests`).

A fence is a region of the desktop icon grid (the owner, 2026-11-19: "the fence should basically just be a helper to align
the icon on the underlying engine without this transition in between", root [S-13040]..[S-13044]): its items are the
desktop's own icons, laid by the host on the cells of its body; the view draws its title bar and what is no desktop icon.

- **[S-10900]** **What it remembers** (`FenceConfig`, the SDK's `IHoldsDesktopItems`): its title (`Name`, empty for the
  kind's word "Fence"), the desktop items it holds by their keys in its order (`Items`, [S-13000]), whether it is rolled
  up and the height it unrolls to (`RolledUp`, `OpenHeight`, [S-13012]), the first row its body shows (`Scroll`, [S-13043]),
  its standing sort (`Sort`, null for as placed, [S-13013]), its rules for new items (`Rules`, [S-13004]) and how its title
  bar rolls it up (`RollUpOnClick`, `HoverPeek`, both on in a new fence), what it shows (`Source`, [S-13022]) and a search's
  query (`Query`: folder, recency, count, [S-13023]). A clone copies all of it, the lists anew. Tests `FenceTests.S10900_`.
- **[S-10901]** **Its size is the user's** (the kind's sizing, relaxed, [S-12411]): at least two icon boxes across and one
  under its title bar, naturally the size that shows six items without scrolling (`FenceGrid.Around`), both its title bar
  and its boxes on the grid's whole cells as a picture lays them (`FenceGrid.OnGrid`, [S-14500]: 160 x 130 and 240 x 230
  with the medium icons), as large as the work area holds, every cell between ([S-14501]); on the desktop the host lays
  it to whole boxes ([S-13040]). Rolled up its height is its title bar's alone
  (`FenceGrid.TitleDip`, which grid-fits to exactly its three rows on every display; a height the relax keeps,
  `WidgetSizing.KeepsHeight`), its
  width as it was. It joins no widget it touches (`WidgetTraits.NeverJoins`): its title bar is its own edge. The title bar
  is the card's top edge (`IWidgetTopEdge`): the card adds no inset above it. Its tile is in Add widget's Files, beside the
  desktop icon. Tests `FenceTests.S10901_`, `FenceTests.S10901_S14501_`.
- **[S-10902]** **Its body is cells of the desktop icon grid** (`FenceGrid.Layout`, the SDK's one layout, tested in
  `FenceRulesTests`): under the title bar (`IDesktopFences.Cells`: whole grid rows, and one desktop icon's box on the widget
  grid of the fence's display, [S-5593], [S-11875]) the items stand in reading order, as many to a row as the body holds,
  box against box from the body's corner, with nothing round them ([S-13019]). On the desktop they are the desktop's icons,
  laid there by the host ([S-13040]); the view draws only what is no desktop icon (a search's files, a picture's items) in
  the same cells. More items than it shows: whole rows from its scroll, the mouse wheel a row a notch, a thin thumb at the
  body's right edge ([S-13043]); "Drag desktop icons here" stands in an empty one.
- **[S-14264]** **An empty fence's sentence is its first row** (owner, 2026-11-19, WR-1: "there should not be a space at
  the top"; WR-5). "Drag desktop icons here" stands centred in the body's first row of boxes, edge to edge (one of its
  items, `ItemMark`), so the room of an empty fence stands under it as whole rows, as a list's empty sentence is its first
  row; before, it stood in the middle of the whole body. Renders `--widget-png fence --state empty --sizes range`.
- **[S-10903]** **An item is a desktop icon**: on the desktop each item is the desktop's own icon card ([S-13042]), so its
  icon, name, highlight, hover, focus cue, cut and hidden fades are a free icon's, exactly. What the fence draws itself (a
  search's files, a picture's items) looks the same: the shell's icon with its overlay ([S-5512], at the display's pixels,
  [S-5563]) over its name in two lines, the highlight on the whole box in the desktop icon's palette
  (`SelectionLook.DesktopIcon`), its white wash in black on a light card (`SelectionLook.DesktopIconOver`, [S-13019]), its
  name the card's text colour.
- **[S-10904]** **Every gesture on an item is a desktop icon's** ([S-13011]): its icon is the desktop's (press, Ctrl, Shift,
  double-click, right-click, F2, the move). A press in the body between its icons never moves the fence: a left press there
  starts the desktop's own band ([S-13020]); a press on the title bar is the card's (it moves the fence, its icons with it,
  and what is selected with it, [S-2008]); in edit mode the card has every press.
- **[S-10905]** **The keys with the fence's lease are the desktop's** ([S-5541]..[S-5545]), sent from the fence: arrows,
  Enter, F2, Delete, Ctrl+C/X, Alt+Enter, F5, the Menu key, type-to-find; Ctrl+V and Ctrl+Shift+N land what they bring in
  this fence ([S-13005], [S-13101]), from one of its icons too. F2 with no item of it focused renames the fence ([S-10910]).
  Escape ends the lease and keeps the selection.
- **[S-10906]** **A move of its items** is the desktop's one move of icons ([S-13041]): within the fence to another place,
  onto empty desktop of any display (a free icon where it lands), onto another fence (into it); dragged off the desktop
  (Explorer, a folder, an app, the Recycle Bin) it is the shell's drag of the selection, as any icon's ([S-11878]).
- **[S-10907]** **Drops on a fence** ([S-13005]): the desktop's own icons land on its cells by the move ([S-13041]), never
  through it; files from outside go through the shell into the desktop folder and arrive in the fence; over one of its
  folder icons the drop is that icon's own ([S-5587]). The caption names the fence ("Move to Work"). Never in edit mode,
  never on a search fence.
- **[S-10910]** **The title bar**: the fence's name (its title, else "Fence"), cut with an ellipsis, then how many items
  it holds ("6 items") and a chevron (up while open, down while rolled up), a hairline under it while open. A
  double-click on it, F2 with no item focused, or the menu's Rename fence turns the name into a box: Enter or a click
  elsewhere keeps it (trimmed, no control character, at most 80 characters; the kind's own word or nothing clears it),
  Escape takes it back. It is the fence's own title, never the Header widget ([S-12480]).
- **[S-10911]** **The card's menu** (its context actions, [S-1746]): Paste and New folder into the fence ([S-13005]),
  Rename fence, Roll up or Unroll, Remove fence. Remove fence never deletes a file: a fence with items asks first, beside
  its title bar, "Put the 3 items back on the desktop?" with Put back (the default) and Cancel; Put back removes it and
  its items stay where it showed them ([S-13009]), one undo step that brings it back with them. An empty fence goes at once.
- **[S-10912]** **A click on the title rolls it up**, as Fences' roll-up ([S-13012]): the fence shrinks to its title bar, gliding ([S-13028]);
  a click again unrolls it to the height it had. The page's switch "Roll up with a click on the title" turns that off.
  A double-click renames instead: its first click's roll-up is taken back.
- **[S-10913]** **Hover peek**: while the pointer rests on a rolled-up fence it opens, and rolls up again when the pointer
  leaves the fence (its icons are the fence's: the pointer over one has not left; not while its rename box or a drag holds
  it); a click while it peeks keeps it open. The page's switch "Open while the pointer is over it" turns that off.
- **[S-10914]** **Its page** ([S-2340]): Fence name (asked), Shows (What I put in it, Desktop items that match, Files
  that match from a folder or drive, [S-13022], [S-13023]; for a search: Look in, Changed, At most), Sort by (As placed, Name, Item type, Date modified, Size:
  the fence's standing order, the desktop's own sort rule, [S-13013]), the two roll-up switches, and New items › its rules
  ([S-13004]): a switch per file type (Images, Documents, Installers, Archives, Videos, Music, Shortcuts, Folders), Other
  extensions (asked, "pdf, docx"), Name contains (asked), under a note saying new desktop items that match arrive here (a
  collecting fence: that every match stands here; a search fence: that every matching file of its place is shown, the
  files staying where they are; the page is then "Which items").
- **[S-10915]** **Pictures** ([S-11850]): the sample is a fence named "Work" with six of the sample desk's items (names on
  no disk, so the shell draws their type's icons, the same on every machine), drawn in its cells as the desktop's icons are.
  Debug renders' states (`--widget-png Fence --state …`): `sample`, `empty`, `selected`, `inactive`, `title`, `rolledUp`,
  `many` (its thumb), `cut`, `sorted`. The fence on the desktop beside free icons, its icons laid by Core over the grid:
  `--drag-png` (`drag-fences-*`).
- **[S-10916]** **Stored whole**: the config round-trips through the state file (the sort and the file types by name);
  a new one is as placed, rolls up on a click, peeks, and has no rule. Tests `FenceTests.S10916_`.
- **[S-10917]** **Load repair**: a null item list or rule set is empty and the title is cleaned and cut, each said; the
  items' keys, a repeat across fences and a free twin are the desktop's load rules ([S-13002]). Tests `FenceTests.S10917_`.
- **[S-10918]** **Which items stand in a fence is the arrangement** ([S-13010]): moving items in or out is one undo step
  that puts every icon back where it stood, free or on its fence's cell, and its redo does it again. Tests `FenceTests.S10918_`.
- **[S-10919]** **A removed fence's undo** brings it back with its items on their cells ([S-13009]). Tests `FenceTests.S10919_`.
- **[S-10920]** **No item is both free and in a fence on one view**: its icon is the fence's; a fence stored before fences
  laid icons gets them at load ([S-13001], [S-13044]). Tests `FenceTests.S10920_S13044_`.

Needs a manual pass on the live desktop: its min, natural and max on the showcase views ([S-1810]); an icon moved into a
fence, inside it and out of it with no jump at the edge; the fence moved with its icons; the band reaching into a fence;
F2 in a fence; a drag off the desktop onto Explorer; a drop from Explorer onto it (and a right-drag with the shell's
menu); a folder icon taking a drop; roll-up by a click and hover peek with the pointer over its icons; the wheel's scroll
and its thumb; a resize snapping to whole boxes; Remove fence's question; two displays of different scale; the light
theme's labels over a light card.
