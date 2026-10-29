# Desktop item (desktopItem)

The module `desktopItem` ([S-6911]). Its tags are [S-5600]..[S-5699], this file's alone; the root
`SPEC.md` holds the host's side, "The desktop layer (DESKTOP)" [S-5500]..[S-5599]: the choice, the
namespace, the reconcile that creates, rewrites and removes these widgets, and Add widget's list.

Owning files: everything in this folder. Tests: `Tests/DesktopItemTests.cs` (run by
`tests/Themia.Modules.Tests`); the host's rules in `tests/Themia.Core.Tests/DesktopLayerTests*.cs`.

- **[S-5600]** What it shows: one desktop item (`DesktopItemConfig.Item`, the item's key, [S-5510];
  an `IDesktopItemConfig` the host rewrites) as Explorer's desktop draws an icon: the shell's icon
  with its overlay and shortcut arrow ([S-5512]) over its name (Caption, centred, wrapped on whole
  words and cut with an ellipsis, [S-5606]), on no card: a widget the host creates is Transparent, and the kind
  has no header and no item layer (`WidgetTraits.None`). Nothing else, at any size (concise first,
  [S-666]). It has no properties-menu lines of its own; an unusable key is cleared at load, loudly,
  and the reconcile then removes the widget.
- **[S-5601]** One cell, 90 × 110 DIPs (the descriptor's default size, on the 10-DIP snap grid): the
  design's gap between widgets (8 by default), then an 8 inset (`FamilySpace.S2`; there is no card to
  stand off), a 48 icon (`FamilyShape.IconHuge`), a 4 gap and two 16 Caption lines. The ladder
  (`DesktopItemLayout.For`, measured on the size the view is really given): a 48 icon and two lines, one line,
  a 32 icon and one line, the 48 icon alone, the 32 alone, else a 16; a name needs 32 of width. A cell
  larger than one keeps the 48 icon and two lines at the top, centred; the room left is empty.
- **[S-5602]** The selection look is the SDK's one (`SelectionChrome`, [S-6627]) round the icon and the
  name, from the host's one desktop selection ([S-5540]): selected, the focus cue after keyboard use
  ([S-6628]), hovered under the pointer, a drag's drop target ([S-5549]), and drawn inactive while
  the desktop does not have the keyboard (`KeepsSelectionWhenLeft`, [S-6626]). A cut item and a
  hidden one have their icon faded (`SelectionLook.Faded`, [S-5545], [S-5547]).
- **[S-5603]** An item the listing does not hold (between a removal and the reconcile) draws the name
  from its key (`DesktopItemKeys.FallbackName`: the last part, a shortcut's `.lnk` dropped) and its
  type's icon.
- **[S-5604]** Outside edit mode every gesture is the desktop's, handed to `IWidgetHost.DesktopItems`
  ([S-5540]..[S-5550]): a left press (Ctrl, Shift) and its release, a move past the dead zone starting
  the drag (posted, never inside the pointer handler), a double-click opening the selection, a
  right-click the shell's menu (both the item's own, never the card's: no edit mode, no card menu),
  the keys and letters with the lease (Escape is the lease's), and drops onto it (forwarded to the
  item's own shell drop target, refused when the item is in the drag). The card still takes the press
  for its lease ([S-800]). A preview acts on nothing. In edit mode every press is the card's.
- **[S-5605]** Preview states (debug renders, `--state`): `document`, `folder`, `shortcut`, `thisPc`,
  `recycleBin`, `longName`, `selected`, `focused`, `renaming`, `dropTarget`, `cut`, `hidden`. This PC
  and the Recycle Bin are the shell's own; the files are names on no disk, drawn with their type's
  icon (and the arrow), the same on every PC; none reads the user's desktop. `--desktop-png <outDir>`
  draws them together on stand-in wallpapers with the band ([S-5544]).
- **[S-5606]** The name is a `WallpaperLabel` ([S-6733]): light in either theme over its shadow, as
  Windows' desktop draws its icons' names, so it reads on a light or busy wallpaper.
- **[S-5607]** While the host says this widget renames ([S-5543], `IDesktopItems.RenamingWidget`), the
  name gives way to a box (Caption, centred, wrapping, reaching into the cell's inset as Explorer's is
  wider than the name), focused with the host's selection range and taking the keyboard as text input
  ([S-801]); Enter and losing focus commit through the host, Escape reverts, and a sentence the host
  returns (an invalid or clashing name) stays as the box's tip while the box stays.

Needs a manual pass: the item widgets on the live desktop under Themia's items (the showcase views
do not hold them: they are made by the reconcile), at 100 % and at a scaled display, and every
gesture of [S-5604] there.
