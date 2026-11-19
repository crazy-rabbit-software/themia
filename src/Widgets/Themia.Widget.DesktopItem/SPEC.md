# Desktop icon (desktopItem)

The module `desktopItem` ([S-6911]): the one kind for everything drawn and used as Explorer's desktop
draws an icon. Its tags are [S-5600]..[S-5699] and [S-9500]..[S-9515], this file's alone, with
[S-11204] of the sizing and [S-11934], [S-11949] of the view links. The desktop's icon size is the
host's one setting ([S-5593], root `SPEC.md`); this file says what the icon does with it. The second
block was the Shortcut module's: the kind `shortcut` was merged into this one on 2026-11-14, and a tag
of it is kept where an own icon still behaves so. The root `SPEC.md` holds the host's side: "The desktop
layer (DESKTOP)" [S-5500]..[S-5599] (the setting, the namespace, the reconcile, the one selection, the
commands on a selection of both sources, [S-5580]..[S-5587], the drags and Add widget's lists) and
"Own desktop icons: drops and app layouts (PD)" (the app layouts and the host's side of a drop).

Owning files: everything in this folder: `DesktopItemModule.cs`, `DesktopItemConfig.cs`,
`DesktopItemView.cs` (the lifecycle, the gestures, the drawing, the rename box) with `.Own.cs` (an own
icon's checks and tooltip), `.Icon.cs`, `.Drop.cs` and `.Links.cs`, `DesktopItemAutomation.cs`,
`OwnIconRules.cs`, `OwnIcons.cs`, `OwnIconDrop.cs`, `OwnIconMenu.cs` (an own icon's page lines),
`Samples/`, `Preview/`; the cell and the tip are the SDK's (`Themia.Sdk/Widgets/DesktopCell.cs`,
`Themia.Sdk/Files/DesktopTip.cs`). Tests: `Tests/DesktopItemTests.cs` and `Tests/OwnIcon*Tests.cs` (`OwnIconTests`,
`OwnIconDropTests`, `OwnIconIconsTests`, `OwnIconLinksTests`, `OwnIconMenuTests`), run by
`tests/Themia.Modules.Tests`; the host's rules in `tests/Themia.Core.Tests`: `DesktopCommandsTests`,
`DragGhostTests` (the drag's ghost, [S-5564], [S-5565]), `DesktopLayerTests*.cs`, `DesktopViewTests` (the cell at each size, the tip).

## One kind, two sources

- **[S-5600]** [S-9500] What it shows: one desktop icon as Explorer's desktop draws one: its icon over
  its name (Caption, centred, [S-5606]) in the desktop's one cell ([S-5601]), on no card: a new one is
  Transparent (`WidgetTraits.StartsTransparent`, the default every new widget of the kind gets), and the
  kind has no header and no item layer. Nothing else, at any size (concise first, [S-666]). A widget of
  the kind is one of two sources, told apart by its config alone ([S-9501]):
  - a **desktop item**: a file, folder, link or shell item of the desktop, by its key ([S-5510]). The
    host creates these, rewrites the key on a rename and removes the widget when the item is gone
    ([S-5520]), through `IDesktopItemConfig`. Its icon is the shell's with its overlay and shortcut arrow
    ([S-5512]), its name the one Explorer shows.
  - an **own icon**: no key, and the one thing the user points it at: a file, a folder, an app, a web
    address, a Windows location, a Windows Settings page or an app layout ([S-1405]), with the user's
    name and icon for it and how it opens. The reconcile never touches it. Add widget's tile of the kind
    ("Shortcut") makes one that waits for its target ([S-9511], [S-5523]).

  One view (`DesktopItemView`) draws both, and one behaviour holds for both: the view has no selection,
  no menu and no command of its own ([S-5604]).
- **[S-5611]** **Its name.** The kind is a **Desktop icon** (`desktopItem.label`, in every language: the
  forty translations the desktop item's label had; the English was "Desktop item"): its page's title, the
  selection's entry ("Desktop icon", "2 × Desktop icon"), the placing bar, the assistants' list. Only its
  **tile on the add page**, which makes an own icon, is called **Shortcut** (`WidgetDescriptor.AddLabel`,
  [S-11709]; the search finds the tile under both names), and an own icon that opens nothing yet is drawn
  under that name ([S-9502]). *(Until 2026-11-14 the kind's label was "Shortcut", so a plain desktop file
  selected in edit mode was titled "Shortcut".)* Test `DesktopItemTests.S5611_`.
- **[S-9501]** The config (`DesktopItemConfig`: an `IDesktopItemConfig`, `ISetupRule`,
  `IHoldsAppLayouts` and `IAppLayoutItem`). `Item` is a desktop item's key and empty for an own icon;
  `ItemId` is an own icon's stable id (a new one for every new widget and every copy, `Clone`), which
  makes its selection key ([S-5604]) and is what the layout editor holds ([S-1411]). An own icon also
  holds `Label`, `Target`, `IconPath` (the icon a dropped `.url` named, [S-9508]), `IconPicture`,
  `IconEmoji` ([S-9503]), `Layout` (an app layout's pane tree) and `ClickMode` (Double by default,
  [S-9505]); `LabelLines` (1 or 2, [S-11204]) is both sources'. Stored in camelCase: an own icon is
  `"item": ""` with its `itemId`, `label`, `target`, `labelLines` and `clickMode`; a desktop item its
  `item` key with the rest empty. The target is untrusted (a text box, a drop, a JSON file anything
  running as the user can rewrite): `LaunchTargets` classifies it every time it is used, never once on
  the way in.
  **Load repair**, loud: a key that is no key (invalid, or an own icon's selection key) is cleared, and
  the widget is then an own icon that waits for its target (the reconcile no longer removes it); a null
  target or name is emptied; an id that is not 1 to 64 letters, digits, `-` or `_` is replaced by a new
  one; an unknown click mode becomes Double; the name lines are clamped to 1 or 2; a desktop item that
  also holds a target, a name, a layout or an icon of an own icon has those dropped (one source, never
  both); an own icon's layout tree is mended ([S-1406]) and a layout's target blanked; an `IconPath`
  that is no local path, a picture that is no local picture file and an emoji that is not one emoji go,
  and of a picture and an emoji the picture stays.
  **No migration** ([S-1050]): a stored widget of the kind `shortcut` is an unknown kind and is dropped
  at load, logged.
- **[S-5601]** One cell, **Explorer's own**, at the desktop's icon size ([S-5593]: Large 96, Medium 48,
  Small 32, one setting for every icon, never an icon's own). **At medium it is measured** (its desktop
  list's item spacing, read from a Windows 11 desktop: 114 × 150 pixels at 150 %, so 76 × 100), **laid out inside as Explorer lays its own**
  (lossless captures of Explorer's desktop at 150 %, 2026-11-14): the highlight starts 3 under the cell's top
  and is the cell's whole width; inside it the 48 icon (`FamilyShape.IconHuge`) stands 3 down, the name 2
  under it on its 16 Caption lines, centred, to 2 from each side (a name has 72), 1 under its last line; the
  cell keeps 11 clear under a two-line name's highlight. They are Explorer's numbers, not the family's
  spacing tokens. The design's gap between
  widgets is the host's to add ([S-2444]), not the cell's. **Large and Small are derived, not measured**:
  Explorer's icon view keeps the room round the icon and grows the cell by what the icon grows, while the
  name stays 9-point text on two 16-DIP lines, so the cell is the icon plus 28 across (76 − 48) and plus 52
  down (100 − 48): **124 × 148 at Large, 60 × 84 at Small** (a name has 120 and 56), every inset above
  unchanged. A capture of Explorer at those two sizes should confirm or replace them. The geometry is the
  SDK's `DesktopCell`, in one place, every number of it a function of the size.
- **[S-11204]** Converted to the sizing ([S-2438]): **one fixed size per its Name lines** (1 or 2, a new one
  2; the one page line both sources share) **at the desktop's icon size and layout** (`DesktopCell.Current` and
  `DesktopCell.CurrentLayout`, which the host puts in force, [S-5593], [S-13970]; text beside and icons only, root
  `SPEC.md` [S-13971]); text below: at medium 76 × 100 with two, 76 × 84 with one; at large 124 × 148 and
  124 × 132; at small 60 × 84 and 60 × 68 (`DesktopCell.Size`), no handle. When the size changes the host
  refits every icon in that same change ([S-2447]) and the view draws its icon's square and highlight at
  the new size and asks the icon again at its pixels ([S-5609]). It was 90 × 110, which counted the old 8 gap between widgets (now 0 and the host's) and 2 to
  spare. The ladder that shed lines and shrank the icon in a smaller box is gone. *([S-9512], the
  Shortcut's own name lines, retired 2026-11-14 into this line.)*

- **[S-5613]** **The icon layout** ([S-13970]..[S-13975], root `SPEC.md`): the view draws its icon and name in the layout
  in force, never its own (`DesktopItemView.Layout.cs`: `Relook`, through the SDK's `DesktopCellLayout`); a change of
  layout is drawn at once, a rename box open moving with the name. In icons only the name is on the view's overhang
  (`IWidgetOverhang`), shown while pointed at, keyboard-focused or renamed, and the tip always names the item. Renders
  `--desktop-png --icon-layout below|beside|icononly [--icon-size …]`.

## The look

- **[S-5602]** The highlight is one mark **behind the icon and the name** (`SelectionChrome.Wrap(null,
  SelectionLook.DesktopIcon)`), **the icon widget's whole card box** ([S-5612]); it takes nothing of the cell's room.
- **[S-5612]** **The highlight is where the corners are** (owner, 2026-11-19: "the hover and highlight of a desktop
  icon should always be the same area that the widget itself has (where the corners are)"): hovered, selected,
  highlighted or a drop target, the fill covers exactly the icon's card box, the rect its corner marks trace (its
  whole grid cells, [S-11875]), never Explorer's smaller mark inside the cell (it started 3 under the cell's top and
  was as tall as the icon and the name's lines, `DesktopCell.HighlightHeight`, which now only counts the cell's
  height); its corners are `SelectionLook.DesktopIcon`'s radius, the card never clipping them ([S-11880]). The rule
  is `DesktopCell.Highlight` (the card's DIPs in, the box out), placed by the view on every size change. Test
  `DesktopItemTests.S5612_` (every icon size, 100 % to 200 %); renders `--desktop-png`, `--drag-png`. Its numbers are `SelectionLook.DesktopIcon`, a
  `SelectionPalette` of its own (hover, selected, selected under the pointer, the inactive selection,
  the focus stroke, the radius and the border), the one place the desktop icon's highlight is tuned
  against Explorer's, and **measured there** (2026-11-14, inside against just outside the item's edge): a
  wash of white (`SelectionTone.Light`), never the highlight colour: 13 % under the pointer, 27 % selected,
  13 % selected while another window has the keyboard, the edge the fill's own, corners of 2. Selected
  under the pointer (33 %) and an inactive selection under it (18 %) are estimates: they could not be
  captured cleanly. The file widgets' look (`SelectionLook.Rows`, [S-6627]) is not changed by it. What is drawn comes from the host's one desktop selection ([S-5540]): selected, the
  focus cue after keyboard use ([S-6628]), hovered under the pointer, a drag's drop target ([S-5549]),
  and inactive while the desktop does not have the keyboard (`KeepsSelectionWhenLeft`, [S-6626]). A cut
  desktop item and a hidden one have their icon faded (`SelectionLook.Faded`, [S-5545], [S-5547]).
- **[S-5606]** The name is a `WallpaperLabel` ([S-6733]): light in either theme over its shadow, as
  Windows' desktop draws its icons' names, so it reads on a light or busy wallpaper. It **wraps on whole
  words onto its lines** (1 or 2, [S-11204]; 68 wide) and is only then cut with an ellipsis: "Mission
  Planner" is two lines, not "Mission". The view owns its top edge (`IWidgetTopEdge`, [S-5700]): the
  cell is the whole card from its top, because the inset a card gives a view with no header pushed the
  cell down and the name's second line out of it.
- **[S-5603]** A desktop item the listing does not hold (between a removal and the reconcile) draws the
  name from its key (`DesktopItemKeys.FallbackName`: the last part, a shortcut's `.lnk` dropped) and its
  type's icon.
- **[S-5609]** A desktop item's icon is the shell's from the host's cache (the cached one laid in at
  once), asked for at the pixels of the display the view is on for the icon size in force
  (`DesktopIconPixels.For(DesktopCell.Icon(size), scale)`, [S-5563], [S-5593]: a large icon is 96 pixels at
  100 %, 144 at 150 %, 192 at 200 %, from the shell's jumbo list), and again when that display's scale or
  the icon size changes (`XamlRoot.Changed`, the host's `Changed`). An own icon's is asked at the same
  pixels ([S-9503]).
- **[S-9502]** An own icon's name: the user's label, else the target's own (a site's host without
  `www.`, a Windows location's or a Settings page's name, a file's name without its extension), else
  "App layout" for a layout nobody named, and its tile's name ("Shortcut", [S-5611]) while it opens nothing
  yet (`OwnIconRules.Name`, `DerivedName`).
- **[S-9503]** An own icon's icon, best first (`OwnIcons.Choose`): the user's **picture** while its file
  reads (png, jpg, bmp, gif, ico or svg; a local file of at most 16 MB; an `.ico` through the shell, an
  `.svg` rasterised at the size, an animated GIF held still), the user's **emoji** (exactly one grapheme,
  filling the icon's square), the icon a dropped `.url` **named** (`IconFile=`, [S-9508]), then the
  **automatic** one: Explorer's icon for a file, a folder, an app or a Windows location (never for a
  network path), the site's own favicon for a web address (the host's cache, no third-party service),
  and for an app layout up to four of its panes' icons ([S-1405]). The glyph of what it is (a link while
  it waits, a globe, a file, a folder, the Settings gear, the layout glyph, a warning for a refused
  target) stands in until the icon arrives and stays when there is none. A picture and an emoji are
  never both kept; a picture that is gone, too big or will not decode is logged and the automatic icon
  drawn, with no notice.
- **[S-9514]** A Windows Settings page has no icon of its own: the Settings app's is drawn.
- **[S-9515]** **An icon is no card** (owner, 2026-11-15: "the desktop icons have two rounded and two normal
  corners, this should not be"): the kind states `WidgetTraits.NeverJoins` ([S-11880]), so an icon never joins
  the icons it touches on the widget grid (no group panel, no glass body), and on no card its card's surface is
  not clipped to a card's corners (`CardJoins.ClipRadius`): its highlight keeps all four of
  `SelectionLook.DesktopIcon`'s corners, the top two no longer the card's 8. Tests `S9515_`.
- **[S-9504]** An own icon's target health. A local path that may be looked at is checked on the thread
  pool once when the icon is drawn and then once a minute (the shared ticker), never per frame and
  **never a network path**; a web address, a Windows location and a Settings page are never looked at.
  A target that is **missing** draws the icon and the name faded (`SelectionLook.Faded`) and the name
  struck through, and its tooltip says so. A target the launch rule **refuses** is drawn too, faded,
  with a warning glyph, its tooltip saying why: an icon that changed under the user is the one worth
  seeing. The target is classified again by the host's launcher at the moment it is opened.
- **[S-9510]** An own icon on the Recycle Bin holds the host's bin answer while it is the bin: its
  tooltip says how much the bin holds (`RecycleBinState.Tooltip`) and its icon is the shell's empty or
  full one, asked again when that changes. Emptying it is the desktop menu's line ([S-5582]) and a drop
  onto it goes to the bin ([S-5587]).
- **[S-5610]** **The tip under the pointer is Explorer's kind of tip** (`DesktopTip`, SDK, pure; user
  report 2026-11-14: an own icon on a Windows location showed its raw target, `shell:Personal`, under its
  name): only where there is something to say. **A desktop item**: a file its type's name as the shell
  writes it, its size and when it was modified ("Type: Text Document", "Size: 1.2 KB", "Date modified: …");
  a folder when it was made ("Date created: …"; no size: counting a folder is not cheap); a shortcut file
  that resolves "Location: " and where it points, as Explorer writes it, "name (folder)"; a shell item
  (This PC, the Recycle Bin) nothing. The facts are the host's (`IDesktopItems.FactsAsync`: the size and
  dates read on the thread pool, the type's name by `SHGetFileInfo` and a link's target by `IShellLink` on
  the UI thread as their other callers do), asked when the pointer arrives on the icon, once per arrival,
  never per frame, never for a network path, never in a preview; until they arrive the tip says what it
  can. The shell's own infotip (`IQueryInfo`) is not asked: it would need the item bound on a COM worker
  for every hover, and for files it says these same three facts. **An own icon**: "Location: " and its
  target's place (`DesktopTip.Place`): a local path as "name (folder)", a web address as it is, a Windows
  location and a Settings page by their names (Documents, Display), **never the `shell:` or
  `ms-settings:` string**; a Windows location Themia has no name for says nothing, and so does an icon
  whose name is its place already (Documents on Documents). **A name cut short** under the icon leads the
  tip in full. **With the name shown whole and nothing more to say there is no tip at all.** Kept as they
  were: an app layout says its apps ("No apps yet", "1 app", "N apps"), the Recycle Bin how much it holds
  ([S-9510]), a missing target that it is missing and a refused one why ([S-9504], with the target as
  stored: that string is what is wrong). An icon that waits has none. Tests `DesktopViewTests.S5610_`.

## Gestures

- **[S-5604]** Outside edit mode every gesture, of both sources, is the desktop's, handed to
  `IWidgetHost.DesktopItems` ([S-5540]..[S-5550], [S-5580]..[S-5587]) under the icon's **selection
  key** (`DesktopItemKeys.Of`): a desktop item's own key, an own icon's `*icon:<ItemId>`
  (`DesktopItemKeys.Own`). An asterisk is in no path and no shell name, so an own icon's key is never a
  desktop item's, and a key stored as an item that starts so is cleared at load ([S-9501]). The
  gestures: a left press (Ctrl, Shift) and its release, a move past the dead zone starting the drag of
  the selection (XAML's own drag, started inside that pointer handler with its pointer:
  `IDesktopItems.BeginDrag`, `ItemDrag.StartAsync`, `EndDrag` on every way out, [S-5549]; its ghost composed at
  once from the picture each icon keeps, `DragIcon`: the shell's bitmap read, anything else rendered when the icon
  changes, [S-5564]; a drag loop
  started after the handler never saw the mouse until the button was let go), a double-click opening the selection, a
  right-click the desktop's menu for the selection (Windows 11's, [S-5582]; with Shift held the shell's
  classic one at once, [S-5545]; both the icon's own, never the card's: no edit mode, no card menu), the keys and letters with the lease (Escape is the lease's own end, which keeps the
  selection), and drops onto it ([S-9508]). The card still takes the press for its lease ([S-800]). A
  preview acts on nothing. In edit mode every press is the card's.
- **[S-9505]** Opening an own icon. **Open with** on its page is Double click (the default, as Windows'
  desktop: the icon sits in the pointer's path and a stray click opening something is a hazard) or
  Single click. With Single a plain click opens it, never one with Ctrl or Shift held, which only
  selects, and a double-click opens nothing more; with Double a double-click opens the selection, as a
  desktop item's does. What opening does is the desktop's rule ([S-5581]). *(Retired 2026-11-14: the
  Shortcut's own selection of one, and Escape clearing it. An own icon is in the desktop's one
  selection, [S-5540], and Escape ends the lease and keeps it.)*
- **[S-9506]** *(Retired 2026-11-14: the Shortcut's own menu through `ItemMenu`. A right-click on an
  own icon opens the desktop's menu for the selection, [S-5582].)*
- **[S-5607]** While the host says this widget renames ([S-5543], [S-5584], `IDesktopItems.RenamingWidget`),
  the name gives way to a box (Caption, centred, wrapping, reaching into the cell's inset as Explorer's
  is wider than the name), focused and taking the keyboard as text input ([S-801]). A desktop item's
  box holds its name with the host's selection range (the part before a shown extension); an own icon's
  holds its name, all of it selected, over the name its target gives as the placeholder. Enter and
  losing focus commit through the host, Escape reverts (it is the box's, the lease does not end), and a
  sentence the host returns (an invalid or clashing file name) stays as the box's tip while the box stays.
- **[S-9507]** Renaming an own icon sets its label, never a file: a name that is empty, or the one the
  target gives, clears the label, so the icon follows its target again (`OwnIconRules.Rename`,
  `IDesktopItemConfig.RenameOwn`). F2's box, the desktop menu's Rename, a screen reader's value and the
  page's Name line all go through it.
- **[S-9508]** Drops onto the icon ([S-5587]), outside edit mode, through the surface's one drop path
  (`IWidgetDropTarget`; the view registers no OLE target and never throws into the caller). An own icon
  that **waits for its target** takes the first dropped thing the rule accepts as what it opens
  (`OwnIconDrop`): a file or a folder by its path; a `.lnk` by what it points at (the link itself when
  it cannot be resolved, passes arguments or has its own icon); a `.url` by its address when that is a
  web address or a Settings page, else the `.url` file itself with the icon it names; a browser's link
  by its URL when no file came with it; a Start menu app by the Start menu's shortcut of that name, else
  a packaged app's Apps-folder item ([S-1402]). It is made by reference, never by copying the file (the
  effect is Link where the source allows it, else Copy), it takes the dropped thing's name unless the
  user already gave it one, and it saves itself. A drop that holds nothing it may open is logged and it
  still waits. A **set-up** own icon and a desktop item hand the drop to their target's own shell drop
  target ([S-5587]), drawn as the drop target while it accepts ([S-5549]); every other icon refuses,
  and an icon in the drag takes no drop of itself.
- **[S-9511]** An own icon waits for setup while it opens nothing (no target and no layout); a desktop
  item never does. Its setup is in place ([S-1881]): it runs while it waits, drawn with the kind's glyph
  and name, and a drop onto it sets it up ([S-9508]). Its Set up sentence is "Choose what it opens", and
  the Set up button opens its page on the Target line ([S-1862]).

## The page

- **[S-9509]** The page lines ([S-3719], `DesktopItemModule.MenuLines`). A desktop item is what the
  desktop says it is: its one line is **Name lines** ([S-11204]). An own icon has every option there
  (`OwnIconMenu`): **Target** (the line the Set up button opens; it ends with what kind of thing it
  opens now), a submenu of File… and Folder… (Windows' pickers), Web address or path… (typed and checked
  by `LaunchTargets` as it is typed; a refused one cannot be set and says why under the box; nothing is
  corrected: a bare word is refused, never turned into `https://word`), Windows locations (by name, each
  with Explorer's icon, the current one marked) and App layout… (made, then straight into its editor;
  Edit layout… on one that is a layout, [S-1411]; off while the editor cannot open); **Name** (asked,
  [S-9507]); **Icon** (Picture…, Emoji…, Automatic icon; the line ends with the emoji or the picture's
  file name, [S-9503]); then **Name lines** and **Open with** (Double click, Single click, [S-9505]).
  Setting a target clears a layout and the icon a dropped `.url` named; the name and the user's own
  icon stay.

## Previews, automation and view links

- **[S-5605]** [S-9513] Preview states (debug renders, `--state`). `sample` is the sample, the add
  page's picture of the tile: an own icon on Windows' Documents location, which every PC has. Desktop
  items: `document`, `folder`, `link` (a shortcut file, with the arrow), `thisPc`, `recycleBin`,
  `twoWords` (a name of two words on its two lines, [S-5606]), `longName` (cut at two lines), `hover`,
  `selected`, `selectedHover`, `inactive`, `focus`, `editing` (the rename box), `dropTarget`, `cut`,
  `hidden`. Own icons: `web` (its globe: no favicon is fetched in a render), `ownFolder`, `app`,
  `settings`, `layout` (three panes), `emoji`, `ownTwoWords`, `ownLongName`, `missing`, `refused`,
  `waiting`, `ownSelected`, `ownInactive`, `ownFocus`, `ownEditing`, `ownDropTarget`. The family's
  render walk skips the interaction ones ([S-6731]). This PC and the Recycle Bin are the shell's own;
  the files are names on no disk, drawn with their type's icon (and the arrow), the same on every PC;
  none reads the user's desktop, and a fixture is applied, never saved. `--desktop-png <outDir>` draws
  icons of both sources together on stand-in wallpapers (hover, selected, inactive, focus, a drop
  target, cut, hidden, a two-line name, a long name, the rename box) with the band ([S-5544]); with
  `--icon-size small|medium|large` at that icon size ([S-5593], the files `desktop-<size>-…`), and under
  the icons the tips of four stand-in icons as the rule words them ([S-5610]: a tip is a popup, which a
  render cannot catch, so they are drawn as boxes).
- **[S-5608]** To a screen reader the view is one desktop icon, whichever its source ([S-5570],
  `DesktopItemAutomationPeer`): a list item named as shown, its place in the desktop's order, the
  selection item, invoke and value patterns forwarded to the host's desktop (nothing in a preview or in
  edit mode), and on each paint a selection that changed and a focus that arrived announced. The value
  is the name: a desktop item's is set by F2's rules and is read-only for a shell item, an own icon's
  sets its label ([S-9507]).
- View links ([S-11930], [S-11945]): the kind publishes `HoveredApp` and `HoveredFile` and follows
  `HoveredApp`, `SelectedApp` and `HoveredFile`. A desktop item is its file, folder or link by its path
  (This PC, the Recycle Bin and every other shell item none). The pointer on the icon publishes what it
  is, and the whole icon is drawn as under the pointer (the desktop's hover, over what its own selection
  draws) while it is pointed at, or its app picked, elsewhere on the view. A click keeps its meaning
  (select, open): a desktop icon never picks.
- **[S-11934]** The app an own icon is on its view's links (`OwnIconRules.AppKey`): a target that is a
  local executable (`….exe`) is that app, keyed as screen time keys it (`LinkKeys.App`); a folder, a
  document, a web address, a Windows location, a Settings page, an app layout and a refused target are
  none.
- **[S-11949]** The file an own icon is on its view's links (`OwnIconRules.FileKey`): a target that is
  a local path and no executable (a folder, a document, a `.lnk` or `.url` by its own path: the widget
  never reads a link's target), keyed as every file widget keys it (`LinkKeys.LocalFileLink`), by its
  stored target; an executable is its app ([S-11934]), anything else none.

Needs a manual pass: none of this kind's gestures can be exercised without a live desktop, and none
was in the change of 2026-11-14 that merged the two kinds. On the live desktop (the showcase views do not hold desktop items: they are made by the reconcile), at 100 % and at a
scaled display, with icons of both sources: every gesture of [S-5604]; dragging a selection (the
ghost under the pointer from the drag's first frame, held where the icon was pressed, one icon or a stack with its badge, with no mark and no caption while they would only move, [S-5564]; that the drag follows the pointer from the press on, [S-5549]); moving
it on empty desktop and across displays; dropping it onto a folder, an app and a Recycle Bin icon of
either source; dragging it out to Explorer; a drag of only own icons that are no file being refused
everywhere outside the desktop; the Windows 11 menu ([S-5582]) for a file, a folder, a link, a shell item, one
own icon, several and a mixed selection, each of its lines, its Show more options, the Menu key, Shift+F10 and
Shift+right-click; Delete on a mixed selection with the undo notice; F2 on an own icon; Ctrl+C with
an own icon on a path; Properties… entering edit mode on the icon's page; an own icon opening on a
single click; Narrator over both sources; the three icon
sizes beside Explorer's own desktop at each (the derived cells of Large and Small, [S-5601]) and the icons
refitting as the size changes; the tip of a file, a folder, a shortcut file and own icons of each kind
appearing under the pointer and staying away where there is nothing to say ([S-5610]).
