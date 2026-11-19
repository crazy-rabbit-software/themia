# Fence (fence)

The module `fence` ([S-6911]). Its tags are [S-10900]..[S-10999], this file's alone; the root `SPEC.md` holds the
host's sections, the desktop's rules for fences among them (FENCES, [S-13000]..[S-13014]).

Stardock Fences' feature, not its look (the owner, 2026-11-19: "think about how we could integrate something like fences
... to create a group of icons with a header. it should also be build as a widget that can be created like fences does
it"): a box of real desktop items with a title bar of its own.

Owning files: everything in this folder. Tests: `Tests/FenceTests.cs` (run by `tests/Themia.Modules.Tests`); the rules
it stands on are Core's and the SDK's (`DesktopFencesTests`, `FenceRulesTests`, `DesktopFileUndoTests`).

- **[S-10900]** **What it remembers** (`FenceConfig`, the SDK's `IHoldsDesktopItems`): its title (`Name`, empty for the
  kind's word "Fence"), the desktop items it holds by their keys in its order (`Items`, [S-13000]), whether it is rolled
  up and the height it unrolls to (`RolledUp`, `OpenHeight`, [S-13012]), its standing sort (`Sort`, null for as placed,
  [S-13013]), its rules for new items (`Rules`, [S-13004]) and how its title bar rolls it up (`RollUpOnClick`,
  `HoverPeek`, both on in a new fence). A clone copies all of it, the lists anew. Tests `FenceTests.S10900_`.
- **[S-10901]** **Its size is the user's** (the kind's sizing, relaxed, [S-12411]): at least one cell of the desktop under
  its title bar, naturally the size that shows six items without scrolling (`FenceGrid.Around`), as large as the work
  area holds. Rolled up its height is its title bar's alone (a stepped axis the relax keeps), its width as it was. It joins
  no widget it touches (`WidgetTraits.NeverJoins`): its title bar is its own edge. The title bar is the card's top edge
  (`IWidgetTopEdge`): the card adds no inset above it. Its tile is in Add widget's Files, beside the desktop icon.
  Tests `FenceTests.S10901_`.
- **[S-10902]** **Its own small grid** (`FenceGrid`, the SDK's, tested in `FenceRulesTests.S10902_`): under the title bar
  ([S-10910]) the items stand in the desktop's one cell at the icon size in force (`DesktopCell.Grid`, [S-5593]), in
  reading order, as many to a row as the body's width holds (at least one), the row centred, 4 DIPs round them. More
  items than it shows scroll the body (no horizontal scroll), with the bottom fade every scrolling list has (`ScrollFade`,
  [S-1281]); "Drag desktop icons here" stands in an empty one.
- **[S-10903]** **An item looks as a desktop icon does**: the shell's icon with its overlay ([S-5512], from the host's
  cache at the display's pixels, [S-5563]) over its name in two lines in the desktop's cell, the highlight on the whole
  cell (hover, selected, inactive, the focus cue, a drop target), a cut or hidden item faded ([S-5545], [S-5547]). It
  stands on the card, not on the wallpaper, so its highlight is the item selection's look on a card (`SelectionLook.Rows`,
  as the folder widget's tiles and Explorer's own windows draw a selected icon: the desktop icon's palette is tuned for a
  wallpaper and vanished on a light card) and its name the card's text colour.
- **[S-10904]** **Every gesture on an item is the desktop's** ([S-13011]): a press selects in the one selection (Ctrl
  toggles, Shift the range, a press on a selected item narrows on release), a double-click opens the selection, a
  right-click opens the desktop's menu for it (Shift: the shell's classic menu), all keyed by the item. A press in the
  body never moves the fence; a press on the title bar is the card's (it moves the fence and what is selected with it,
  [S-2008]); in edit mode the card has every press.
- **[S-10905]** **The keys with the fence's lease are the desktop's** ([S-5541]..[S-5545]), sent from the fence: arrows,
  Enter, F2 (the rename box on the item, [S-5543]), Delete, Ctrl+C/X, Alt+Enter, F5, the Menu key, type-to-find; Ctrl+V
  and Ctrl+Shift+N land what they bring in this fence ([S-13005], [S-13101]). F2 with no item of it focused renames the
  fence ([S-10910]). Escape ends the lease and keeps the selection.
- **[S-10906]** **A drag from a fence** is the desktop's drag of the selection ([S-5549]), started by the fence once the
  press leaves the dead zone ([S-6624]): onto empty desktop the items leave the fence where they are let go ([S-13006]),
  onto another fence they move into it, onto Explorer, a folder, an app or the Recycle Bin the target does what it does.
  The picture is Explorer's ghost ([S-5564]) of this fence's icons: the held one in front, up to two more selected ones
  behind, the count in a badge.
- **[S-10907]** **Drops on a fence** ([S-13005]): the desktop's own drag moves its desktop items in, before the item the
  thin accent mark between cells shows (at the end while a standing sort orders them); files from outside go through the
  shell into the desktop folder and arrive in the fence; over a folder item of the fence the drop is that folder's own
  shell target ([S-5587]), the folder drawn as the drop target. The caption names the fence ("Move to Work"). Never in
  edit mode.
- **[S-10910]** **The title bar**: the fence's name (its title, else "Fence"), cut with an ellipsis, then how many items
  it holds ("6 items") and a chevron (up while open, down while rolled up), a hairline under it while open. A
  double-click on it, F2 with no item focused, or the menu's Rename fence turns the name into a box: Enter or a click
  elsewhere keeps it (trimmed, no control character, at most 80 characters; the kind's own word or nothing clears it),
  Escape takes it back. It is the fence's own title, never the Header widget ([S-12480]).
- **[S-10911]** **The card's menu** (its context actions, [S-1746]): Paste and New folder into the fence ([S-13005]),
  Rename fence, Roll up or Unroll, Remove fence. Remove fence never deletes a file: a fence with items asks first, beside
  its title bar, "Put the 3 items back on the desktop?" with Put back (the default) and Cancel; Put back removes it and
  its items arrive where it stood ([S-13009]), one undo step that brings it back with them. An empty fence goes at once.
- **[S-10912]** **A click on the title rolls it up**, as Fences' roll-up ([S-13012]): the fence shrinks to its title bar;
  a click again unrolls it to the height it had. The page's switch "Roll up with a click on the title" turns that off.
  A double-click renames instead: its first click's roll-up is taken back.
- **[S-10913]** **Hover peek**: while the pointer rests on a rolled-up fence it opens, and rolls up again when the pointer
  leaves (not while its rename box or a drag holds it); a click while it peeks keeps it open. The page's switch "Open
  while the pointer is over it" turns that off.
- **[S-10914]** **Its page** ([S-2340]): Fence name (asked), Sort by (As placed, Name, Item type, Date modified, Size:
  the fence's standing order, the desktop's own sort rule, [S-13013]), the two roll-up switches, and New items › its rules
  ([S-13004]): a switch per file type (Images, Documents, Installers, Archives, Videos, Music, Shortcuts, Folders), Other
  extensions (asked, "pdf, docx"), Name contains (asked), under a note saying new desktop items that match arrive here.
- **[S-10915]** **Pictures** ([S-11850]): the sample is a fence named "Work" with six of the sample desk's items (names on
  no disk, so the shell draws their type's icons, the same on every machine). Debug renders' states (`--widget-png Fence
  --state …`): `sample`, `empty`, `selected`, `inactive`, `renaming`, `title`, `rolledUp`, `many` (it scrolls), `cut`,
  `sorted`.
- **[S-10916]** **Stored whole**: the config round-trips through the state file (the sort and the file types by name);
  a new one is as placed, rolls up on a click, peeks, and has no rule. Tests `FenceTests.S10916_`.
- **[S-10917]** **Load repair**: a null item list or rule set is empty and the title is cleaned and cut, each said; the
  items' keys, a repeat across fences and a free twin are the desktop's load rules ([S-13002]). Tests `FenceTests.S10917_`.
- **[S-10918]** **Which items stand in a fence is the arrangement** ([S-13010]): moving items in or out is one undo step
  that puts every icon back where it stood, free or fenced, and its redo does it again. Tests `FenceTests.S10918_`.
- **[S-10919]** **A removed fence's undo** brings it back with its items and takes their free icons away again
  ([S-13009]). Tests `FenceTests.S10919_`.
- **[S-10920]** **No item is both free and in a fence on one view** after load ([S-13001], [S-13002]). Tests
  `FenceTests.S10920_`.

Needs a manual pass on the live desktop: its min, natural and max on the showcase views ([S-1810]); a press on an item
selecting with the free icons, the band reaching into a fence, the arrows crossing between a fence and the free desktop;
F2 in a fence; a drag out onto empty desktop, onto another fence and onto Explorer; a drop from Explorer onto it (and a
right-drag with the shell's menu); a folder item taking a drop; roll-up by a click and hover peek with the pointer;
the scroll fade; Remove fence's question; Narrator over its items (they are not in the desktop list's UI Automation
order, [S-5576]: left).
