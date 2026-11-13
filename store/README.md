# Themia on the Microsoft Store

The listing and its pictures. Every widget, desktop and menu on a picture is drawn by Themia itself,
off-screen, from a scene in `scenes.json`; only the headline, the monitor and the callouts around it are
added. The steps in Partner Center (reservation, identity, the Pro add-on, the package) are in
[docs/STORE.md](../docs/STORE.md), the answers to its questionnaires in
[docs/STORE-LISTING.md](../docs/STORE-LISTING.md).

## What's here

| File | What it is |
| --- | --- |
| `listing/<lang>.json` | The listing of one language: name, descriptions, features, keywords, each screenshot's label, headline, line and caption, its callouts, and the Pro add-on's words. English is the source. |
| `scenes.json` | The scenes the pictures are made of: stand-in desktops (a fetched wallpaper, Windows' taskbar, widgets) and sheets of widgets on nothing. A widget stands by its kind and its box in DIPs; a ready-made group by its id and its corner (`{ "readyMade": "thisPc", "at": [x, y] }`: the group the add page places). A widget given no state shows its sample; `links` lights what is pointed at. The display is 1536 x 864 DIPs: a full-HD screen at 125 %. `Themia.exe --scene-png` draws one. |
| `wallpapers.json`, `wallpapers.mjs` | The desktops' wallpapers: illustrations by other people, free to use, each listed with its source page, author and licence. The files are not in the repository: `node store/wallpapers.mjs` fetches them and fits each to 3840 x 2160 under `store/wallpapers/` (git-ignored). |
| `capture.ps1` | Builds the Debug app and draws every scene to `out/shots/<lang>/<scene>.png` (3840 x 2160), each with its layout beside it (`<scene>.layout.json`: where the app painted every widget). |
| `panorama.mjs` | The ten pages: which scene or widget stands where, the monitors, the callouts. Widgets are named by scene and id, their places read from the scenes' layouts, never typed. |
| `compose.mjs` | Makes the Store's pictures from the captures and the listings with headless Chrome: the screenshots per language, the hero art, the tile icon and the add-on's icon. |
| `fill-csv.mjs` | Fills a listing CSV exported from Partner Center with every language and lays out the folder to import. |
| `logos/` | The services' logos on the accounts page (Simple Icons, CC0). |
| `*.test.mjs` | `node --test store/listing.test.mjs store/csv.test.mjs store/panorama.test.mjs` |

Everything made lands in `store/out/`, which git ignores.

## Making the pictures

```powershell
node store/wallpapers.mjs                   # once: the wallpapers, to store/wallpapers
./store/capture.ps1                         # every scene in English, to store/out/shots/en
./store/capture.ps1 -Only desktop,pc        # or some
./store/capture.ps1 -Lang de                # Themia in another language
node store/compose.mjs en                   # the pages, to store/out/images/en
```

Nothing appears on the screen and nothing of the PC is read: a scene is drawn off-screen by a Debug
build, over the previews' sample day (one person, one place, 10:42) and a sample PC, and never touches a
running Themia or its data. So the pictures can be made at any hour, while the PC is in use, and come
out the same each time.

A language's pages show Themia in that language once it has captures of its own
(`out/shots/<lang>`); until then they show the English ones under that language's words.

## The ten pages

They are one picture, 10 x 1920 px wide, cut into the Store's pages: side by side in the Store's row
they are one scene. They say what Themia does, never how it is used (no add page, no edit bar, nothing
on how a panel is built):

1. **The promise.** A desktop with widgets on the wallpaper, Windows' taskbar under them.
2. **The office.** Your day (the next meeting, the week, mail, tasks); the accounts it comes from.
3. **The beautiful desktop.** Your PC with its live graphs; the weather and the time.
4. **Everything else.** A wall of the widgets no other page shows: more than a hundred.
5. **Yours.** From one clock to a full dashboard (four desktops); one desktop in four looks; three
   desktops of one PC, one per part of the day.
6. **What Themia stands for.** Its promise, a tile each (the listing's `points`).

The pages hang together: a widget lies across every cut, half on each page; the wall runs into the pages
beside it; one landscape (a sky, two ranges of hills, a ground the monitors stand on) runs behind them
all. Words and monitors stay whole on their own page.

They are drawn as the wallpapers are: flat fills, a handful of colours, no glow, no sheen, no gradient
(`palettes` in `compose.mjs`; `STORE_PALETTE=night` draws the same at night).

Rules the pictures keep (`panorama.test.mjs`, which needs the captures: it reads the layouts): every
desktop shows Windows' taskbar and one of the listed wallpapers; some desktops leave the wallpaper open
(the first covers less than half of it); neither the video stream nor the gallery widget is shown; no
widget leaves its display or lies on another.

## Changing something

- **Words:** `listing/en.json`, then the same field in the other languages. `listing.test.mjs` holds
  every language to Partner Center's limits and to English's shape, the number of widgets the listing
  names to the number of widget modules, and Google out of it while Themia offers no Google account
  (SPEC [S-3562]).
- **A desktop or a widget on it:** `scenes.json`. A scene may start from another (`like`) and change
  its look only: that is how the looks page shows one desktop four ways.
- **Where things stand on a page:** `panorama.mjs`. `panorama.test.mjs` keeps monitors whole on their
  pages, a callout for every bubble and widgets from lying on each other.
- **The Store's import:** export the listings from Partner Center (Store listings › Export listings),
  run `node store/fill-csv.mjs <exported.csv>`, import the folder `store/out/import/themia-store`.
