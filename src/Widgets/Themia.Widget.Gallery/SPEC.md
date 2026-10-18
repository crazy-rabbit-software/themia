# Gallery

The module `gallery` ([S-6911]): pictures from a folder, one picture file or Bing's photo of the day.
Its tags are this file's alone; the root `SPEC.md` holds the host's sections and what the gallery
shares, with their root tags: a file source, its empty states and the set up button ([S-761],
[S-1862]), the photo of the day and its credit ([S-762], [S-3112], the SDK's `BingPhoto`), the one
shared source per folder, the decoded pictures and each widget's place ([S-3410]..[S-3413], the
host's `GalleryService` behind `IWidgetHost.Gallery`, the SDK's `GalleryMemory` and `GalleryStates`),
the hourly rescan ([S-3792], `GallerySourceKey.RescanInterval`), the picture files ([S-303], [S-304],
the SDK's `ImageFiles`), the persisted settings ([S-305]), the slideshow keys ([S-1992], the SDK's
`ControlKeys`) and the sticker's turn and outline rules ([S-774], [S-775], the SDK's `StickerRules`
and `RotatedBox`).

Owning files: everything in this folder: `GalleryModule.cs` (descriptor, glyph, view factory, settings
rows, JSON context), `GalleryConfig.cs` (with its styles, fit and settings), `GalleryView.cs` (the
source through its host) with `.Layout.cs` (the frames, the decodes through `IWidgetHost.Gallery`, the
sticker) and `.Commands.cs` (the hover bar, the menu, the keys, opening through its launcher),
`GalleryArrangement.cs` (scatter, mosaic and grid), `GalleryPlan.cs` (the ladder), `StickerOutline.cs`
(the outline from a second decode), `GallerySettings.cs` (its page: the folder through the SDK's
`FolderBrowse`, the picture file through `SettingsHost.OwnPicker`), `Preview/` (Debug renders:
`--state sample|slideshow|grid|scatter|mosaic|file|sticker`, Windows' own wallpapers),
`Localization/Locales/` (its own `GalleryStr`, 30 languages, its style names included). Tests:
`Tests/GalleryLayoutTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's rules is
the module host's `GalleryHostTests` and `GalleryPersistenceTests`.

- **[S-760]** `Source` is a closed set: `Folder` (default), `File`, `Bing`. Folder uses `Path` +
  `Recursive`; File uses `FilePath`; Bing needs no setup. The settings page shows only the rows the
  chosen source (and, for the slide time and picture size, the layout) uses.
- **[S-764]** Scatter: square prints, one per cell of the grid shape for the count (spare cells left
  empty at random), centre in the middle half of its cell, side 1.05–1.4× the cell's short side
  (capped at 90% of the card's), tilt ±12° as in the original, stacking order shuffled. The
  original's "within 28% of the centre" heaped every print in the middle of a big card. About one
  print per 12,000 DIP².
- **[S-765]** Mosaic: the card cut recursively, the first half of the pictures getting a 35–65%
  share, across the longer side (the original alternated; same on a square, no slivers on a strip).
  Cells tile the card exactly with no gaps; always cropped to fill. About one cell per 9,000 DIP².
- **[S-6402]** The gallery's styles ([S-6000]) are its layouts, `GalleryConfig.Style`
  (`GalleryStyle`, `"style": "slideshow"`): **Slideshow**, **Grid**, **Scatter** ([S-764]) and
  **Mosaic** ([S-765]), in that order; the Layout row is gone. A folder's choice: one picture file
  and the photo of the day are drawn whole in any style ([S-762]). The slide time shows on the page
  for Slideshow only and the fit for every style but Mosaic; another style rebuilds the page.
- **[S-766]** Grid: about one 120-DIP tile per slot (max 24), shaped so tiles are closest to square
  with a small penalty per empty cell.
- **[S-767]** The ladder (`GalleryPlan`): any layout with room for only one picture is that picture
  full-bleed with nothing on it (XS). Slideshow: counter in the header and the hover bar from
  Regular width; the picture is the whole slot at every size (no "up next" thumbnails beside or
  under it, retired [S-666]; the L file-name line and the XL "N of M · folder" line are retired, [S-2760]). Grid / scatter / mosaic take more pictures as the card grows. Scatter and
  mosaic are dealt from the widget's own seed ([S-3412]), so a resize moves pictures rather than reshuffling them.
- **[S-768]** *(Retired: no "up next" thumbnails to tap, [S-767]. Home / End and the arrows still
  jump, [S-1992].)*
- **[S-769]** The hover bar (previous / pause-resume / next) shows only while the pointer is over
  the widget, in the slideshow with more than one picture. Pause is not persisted.
- **[S-770]** The slideshow's own timer runs only while it is a folder slideshow with more than one
  picture and not paused.
- **[S-771]** Double-click a picture opens it; right-click a picture: Open, Show in Explorer,
  Pause / Resume, Refresh images (no layout switch, [S-1746]).
- **[S-773]** The card's menu adds Pause / Resume and Refresh images (re-reads now and deals a new
  scatter / mosaic); the layout is a setting only ([S-1746]).
- **[S-2760]** No words on a picture (user rule, [S-666]): no title, credit or file name under or
  over any picture, at any size, in any layout or source (slideshow included), and no hover tooltip
  naming it (the photo of the day's credit tooltip, [S-3112], is the one exception); the freed height is the picture's. A single file's header keeps the catalogue label
  rather than the file's name; a folder's header still names the folder, the photo of the day's says
  "Photo of the day". The picture's name stays only as its screen-reader name. (`GalleryPlan`; tests
  `S2760_`.) Debug renders show real pictures with
  `--widget-png Gallery … --state slideshow|grid|scatter|mosaic|file` (Windows' own wallpapers).
- **[S-776]** The one main picture (slideshow, file, photo of the day) has **no backing fill**, so a
  PNG's transparent pixels and a letterbox show the card, or the desktop on a transparent surface
  ([S-32]): a cut-out picture on a transparent Gallery is a sticker on the wallpaper. Tiles (grid,
  scatter, mosaic) keep their placeholder fill while they decode.
- **[S-777]** The outline is worked out from a second decode of the file at the size shown (longest
  side rounded up to 64 px, at most 2048, never above the picture's own; files over 64 MB are shown
  without one), the distance pass on the thread pool, drawn as a bitmap under the picture. It is
  asked again only when the picture, colour, width, angle, scale or the frame's size (in 32-DIP
  steps) changes; a newer ask cancels the one in flight, and until it lands the picture is fitted by
  its own shape. Debug: `--widget-png Gallery … --state sticker` (the app's logo, −15°, white).
- **[S-811]** Pictures (Gallery widget) slideshow: Left / Right step, Space pauses (the original's S-29). Other
  layouts take no keys.
- **[S-1620]** Pictures (Gallery widget): a slide can be held for as little as **1 s** (`GalleryConfig.MinSlideSeconds`;
  the settings slider starts there), as the original's S-09/S-18. Stored values are clamped to
  1..3600 s.

Needs a manual pass: the widget on a live desktop ([S-769]..[S-773], [S-776], [S-777], [S-811]: a
folder of real photos in every style, a picture added and removed while shown, Bing's photo of the day
online and offline, a sticker turned and outlined, the hover bar, the keys through the lease, opening a
picture and its folder, the settings page's folder and file pickers), never in a render or a test.
