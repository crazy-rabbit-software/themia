# Themia (WinUI)

A Windows desktop-widget app by Lometics GmbH: transparent widgets that live on the desktop layer,
with no main window and no tray icon. This branch (`winui`) is the rebuild on **WinUI 3 / .NET 10,
Native AOT** (Windows App SDK 1.8, self-contained). The original Tauri/SolidJS/Rust app is on
`main`; read it with `git show main:<path>` and never switch branches. Source-available under the
Elastic License 2.0 (`LICENSE`). Unreleased: nothing here has users yet.

## Build, test, publish

```powershell
dotnet build src/Themia.App/Themia.App.csproj -c Debug            # Debug builds include the render flags below
python scripts/test.py                 # the test classes a change touches (--changed [range], the default), each with why
python scripts/test.py --all           # Core.Tests (Core, the SDK's rules) and Modules.Tests (every module's Tests/, conformance) whole
python scripts/test.py --spec 1480     # or --filter Clock: tests named after a SPEC tag, or holding a name (both suites)
powershell -ExecutionPolicy Bypass -File scripts/new-module.ps1 -Kind widget -Name <Name> -Label "<English name>"   # or -Kind provider
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1      # Native AOT, unpackaged (-Run to start it)
powershell -ExecutionPolicy Bypass -File scripts/package.ps1      # unsigned Store .msixupload, see docs/STORE.md
```

- Several builds at once (agents, parallel work): pass `--artifacts-path <own folder>` so they do not
  fight over locks (`scripts/test.py --artifacts <own folder>`). `artifacts/` is git-ignored.
- The tests run in the repo's runner (`tools/TestRunner`, built by `scripts/test.py`: xUnit's engine in-process, the
  test collections in parallel, both suites in about 12 s), not `dotnet test`, whose test host talks over a loopback
  socket and hangs where loopback is blocked (this PC). Visual Studio's Test Explorer still works through the adapter.
- AOT publish needs the MSVC linker; `publish.ps1` puts `vswhere` on PATH for the ILCompiler. A build
  that passes in Debug can still fail or misbehave only when published (compiled XAML is not a
  publish item by default; the `PublishCompiledXaml` target in the csproj fixes that). Run
  `publish.ps1` after touching the csproj, XAML, interop or anything reflection-adjacent.
- Official builds read `THEMIA_CLIENT_SECRETS` at build time (`google=...;todoist=...`, one entry per
  provider whose sign-in needs a client secret, keyed by its `BuildSecret`); it is never in source.

### Looking at the UI without the desktop (Debug builds only)

Render flags draw off-screen to PNG and exit. Look at the PNGs; they are how UI work is verified. One
check exists in every build, Release and the Store's included ([S-11854]): `Themia.exe --add-pictures-check
<out.png>` draws the add page's Start tab through the shipped path and says how many pictures it drew.

```
Themia.exe --widget-png <Kind> <out.png> [w h] [--theme light|dark] [--state name] [--style name] [--sizes WxH,..|range] [--keys k1,k2|text:abc]
           [--place-cleared]   # a widget about a place with its place cleared [S-2848]
           [--transparent]     # no page behind the cards: the PNG keeps their alpha, for compositing
           [--background-image <full path>]   # the cards' own background image [S-6590]
           [--machine sample]  # the system widgets read the sample laptop, not this PC [S-1931]; any render: SampleMachine.Install(ticker, dispatcher)
           [--config <json>]   # a config fragment merged over the kind's default ({"adapter":"{GUID}"}: a network card on one adapter, [S-14962])
           [--wallpaper <image path>]   # the page is a screen with that wallpaper; the cards frost over it [S-2119]
           # a kind at its range (min, natural, max), side by side (or w×h);
           # a wrong --state prints the kind's states (Stocks: sample|falling)
           [--design border=dashed,borderWidth=4,borderOpacity=0.5,borderColor=#FF0000,radius=4,preset=Midnight,...]
           # and every look token by its key: vibrancy=1,frostSaturation=1.6,innerSurface=vibrant,rowDividers=inset,... [S-2142]
           # --style: any styled kind in one of its styles [S-6006] (Clock: digital|analog|stacked|flip|word|minimal; --state sample fixes 10:42)
           [--chart area|line|bars|candles] [--detail simple|detailed]   # a graph kind in a chart look [S-6752]
           [--trend none|arrow|line] [--span n]   # a single metric drawing that trend, its graph over that span [S-14712]
           [--layout-json <out.json>]   # every card's item boxes, grid pitch and the ink outside its items [S-12831]
           [--hover fx,fy] [--pressed fx,fy]   # the item or control at that point of every card (fractions) as under the pointer [S-12831]
           # scripts/item-check.py and scripts/gap-check.py judge the kinds against docs/WIDGET-RULES.md (exit 1 on a finding)
Themia.exe --widget-batch <jobs.json> [--theme dark] [--timeout s]   # many --widget-png renders in one process [S-14340]:
           # {"jobs": [{"id", "args": [<what follows --widget-png>]}]}; the checks' renders go through it (scripts/renders.py)
Themia.exe --catalog-json <out.json>   # every kind of the build: id, project, traits, styles, states [S-14342]
Themia.exe --picker-png <out.png> [--tab Id] [--search t] [--width w] [--height h]   # the add page under the bar [S-11712]
Themia.exe --iconpicker-png <out.png> [--search t] [--initials]
Themia.exe --viewbar-png | --notices-png | --menu-png | --motion-png <outDir>   # --notices-png takes --theme: every notice shape [S-881a]
Themia.exe --desktop-png <outDir> [--icon-size small|medium|large] [--icon-layout below|beside|icononly]   # a stand-in desktop
           # of icons of both sources, their states, the band and the tips [S-5605]; the desktop's icon size [S-5593] and layout [S-13970]
Themia.exe --drag-png <outDir>   # the locked desktop: a move with its local grid, icons on the widget grid at each icon
           # size, a mixed lasso selection (icons and cards), several cards selected and moved (the frame's corners,
           # drag-group-*, drag-groupmove-*, [S-14310]), both themes [S-11874]..[S-11879]; --icon-layout below|beside|icononly
           # instead: that layout's icons and fences at medium and large [S-13970]..[S-13976]
Themia.exe --desk-select-check <out.txt> [--locked] [--live] [--twice] [--glass] [--icon-layout below|beside|icononly]   # the locked
           # desktop's one selection through a real host and surface: --live drives it with real input (SendInput, over every
           # window), PASS or FAIL [S-12954]; host-level it also moves an icon into the fence and one on the free desktop, drawn
           # mid-move to <out>.move-fence.png / .move-free.png [S-13046], and two icons on the free desktop and into the fence,
           # the held icon alone under the pointer (<out>.move-several-free.png / .move-several-fence.png, [S-13048])
Themia.exe --first-drag-check <out.txt> [--locked] [--kinds a,b]   # every kind's first press on a row, a button, a track,
           # a thumb, a text box, a chart, an empty spot, dragged through the card's own steps off-screen, PASS or FAIL [S-13060]
Themia.exe --drag-bench <out.txt> [--frames n] [--events n]   # what each stage of a move's pointer step costs on a real
           # surface of 21 widgets, locked and edit mode, solid and Liquid glass [S-11890]
Themia.exe --editbar-png <outDir> [--theme light|dark] [--only bar|pages|appearance|select|marks|unplaced|follow]   # the edit bar (editbar-*: nothing,
           # one, several selected, problems, an entry open, narrow), the add page (-add*), placing; every page's flyout with
           # its pickers open (page-*-general*, -accounts*, -display*, -views*, -appearance*, -unplaced*); the selection's page
           # beside real cards (select-*) [S-2328]; the edit marks (marks-*) and a block resized by the selection's frame
           # (marks-*-block*, [S-2460]); a flyout following its page as a card opens and closes (-follow-*, PASS or
           # FAIL, [S-2350]). --picker-png takes --theme too. There is no settings window [S-3585]
Themia.exe --displays-check <outDir>   # one view on two stand-in displays, the taskbar left on one and right on the other,
           # through a real host and surfaces: every card clear of both taskbars, placement at the far edges, a widget dragged
           # over each taskbar staying there (the visible minimum kept), a view shown on the other display, PASS or FAIL [S-855]
Themia.exe --layout-editor-png <out.png> [waitMs] [--empty]
Themia.exe --add-showcase-views [<statePath>]   # one view per widget kind at its min, natural and max; quit Themia first
Themia.exe --scene-png <scenes.json> <scene> <out.png> [--scale <n>]   # a store scene: a whole desktop of widgets
           # to one PNG of exactly screen × scale px (default 2) [S-1074]; store/capture.ps1 draws every scene of store/scenes.json
           # a scene widget may carry "selected": true; a "readyMade" entry too (all its widgets) [S-11860]
Themia.exe --recipe-png <id> <out.png> [--states kind=state,..] [--scale <n>] [--theme light|dark] [--edit]   # a ready-made
           # group placed, as a scene; --edit: edit mode, the placed widgets selected; no state: its "sample" preview [S-2475]
Themia.exe --inline-check <out.txt>   # every widget's inline box (add row, rename, title, search) open over its rest
           # state at min, natural and max: a finding per part that moved, grew or went; exit 1 on any [S-13950]
Themia.exe --gallery-png <outDir> [--theme light|dark] [--category Id] [--width w --height h] [--raster 100]   # the widget gallery:
           # every screen, one widget picked (its panel, its styles), several, a drag under way [S-14917]
Themia.exe --gallery-check <out.txt>   # its drag driven: picked, dragged (no jump, whole cells), dropped, the gallery back; PASS or FAIL
Any render: [--lang <code>]   # any of the forty (en, de, … zh-Hant, … my)
           [--icon-census]   # beside each PNG <png>.icons.json: every icon drawn and its box; then
           # python scripts/icon-clip-check.py <dir> names each icon cut by its box, measured on the PNG [S-2377]
--editbar-png, --iconpicker-png: [--raster 100]   # drawn at 100 % whatever the display's scale [S-2377]
```

Build output (`artifacts/`, `bin/`, `obj/`) cannot be read by agents; write render PNGs outside it.

Compare render sets with `python scripts/pngdiff.py <before> <after> [--noise <dir>]` (colour and
alpha; never diff with `getbbox()` on an RGBA difference, which sees alpha only and calls every opaque
render equal). Render the "before" set twice with the same build and pass the second run as
`--noise`: changes inside what moved between those runs (live readings, the clock, async loads,
animation frames) are reported as noise, anything else as `pixels`.

**Never start `Themia.exe` without a render flag as the first argument.** A bare start hands off to
the copy that is already running and toggles its edit mode. Do not drive the user's running
windows, and do not touch `%APPDATA%\Themia` or `%LOCALAPPDATA%\Themia`: the renders use a detached
state store.

## Layout

```
src/Themia.Sdk/    the widget SDK, pure (docs/WIDGET-KIT.md 4.2): no assembly of ours; Core builds on it.
  Localization/ (+ Locales/*.json: the host's Str; StringTable, LocalText) Diagnostics/ (Log) Links/ Time/ (CalendarDays) Shell/ (Glyphs)
  Kit/ (the widget kit's pure parts: KitItem, KitAction/KitMenu, KitKeys, KitSpec/KitPlanner (rich, lined, table, schedule, check looks), KitPages/KitDays, KitChecks, KitScreens, KitFreshness, IKitConfig, IKitSource)
  Services/ (the service layer: ServiceDescriptor and its strategy sets, sign-in members, RateLimitRules, ServiceFailures, paging, GraphQL/form envelopes)
  Model/ (WidgetConfig, FillChoice, RefreshPreferences, ContentAlign; Configs/: the shared config bases QuickToggle-,
  PowerButton-, Weather-, SkyPlace-, Detail-, GaugeConfig and the config contracts ISetupRule, IPlaced, IContentAligned)
  SystemData/ (the hub's data: FeedKey, SourceId, Sources and their snapshots, MeterReading, series, SystemInterests
  with IReadsSystem, ISystemFeeds, WatchSlot) and the system rules in Widgets/ (meters, details, drives, audio, music)
  and the local stores' shared data and rules (clipboard history, alarms, focus, notifications, Notes/NoteMeta,
  Habits/HabitLists with the periods and streaks, screen time, the gallery's sources, scans and Bing photo)
  and the file widgets' shared data (FolderBadges, RecentFileRules, DesktopCell (the cell at each icon size), DesktopTip, the app layouts: AppLayoutModel, LayoutTree,
  LayoutPlan, IHoldsAppLayouts; VideoStreamRules, HlsVariants, the stream's facts; Files/: FileNames, FileDrops,
  SurfaceDrops, DesktopItem with DesktopItemKeys, DesktopCommands (what a command does to a selection of
  desktop icons), DragGhost (Explorer's drag picture; a desktop icons' move shows the held icon alone, `OfMove`), TypeIconName, FenceRules (a fence's rules), DesktopSortBy and the
  records the file contracts carry; Widgets/FenceGrid, a fence's cells (the one layout of its body); Model/Configs/IHoldsDesktopItems, a fence's config contract)
  Widgets/ (WidgetArea, LayoutMath, keys, fits, styles, IShownSettings, Formatting, RingArc, item roles,
  HexColor, PlaceLookup, WebInput) Settings/ (ColorPresets, ColourPicker, ScreenPick, ReorderKeys) Shell/ (Glyphs, KeyChord, MenuArt,
  the item menu's line layout, Notice) Sync/ (UpdateCadence, HostCadences) Files/ (DropEffect) Modules/ (the ids,
  WidgetDescriptor, ModuleCatalog, IProviderModule, ProviderCatalog, FeatureSet, CadenceRow, WidgetTraits, AddPlacement)
src/Themia.Core/   pure logic, no UI types, no Windows dependency. The host's rules, and the widgets not yet moved.
  Geometry/ Model/ (state, Configs/* with each in-Core widget's descriptor) Storage/ (atomic store, load validation, StateJson)
  Widgets/ (per-widget rules) Accounts/ Mail/ Office/ Tasks/ Notes/ (NoteStore) Habits/ (HabitListStore)
  Settings/ (the settings as lines: SettingsMenu*.cs, one builder per section, ViewsMenu, DisplayMenu, AccountsMenu,
  AccountForm, UninstallPlan; SettingsPages: the edit bar's five pages over them; SettingsRows: the row vocabulary)
  Shell/ (EditBarLayout, SelectionPage) Motion/ Licensing/ Net/ Sync/   Accounts/HostProviders.cs (the five account providers as modules)
  Desktop/ (the desktop layer's rules: DesktopReconcile, DesktopViews (which items each view shows: All, Chosen, None), DesktopGrid (icons on the widget grid: their box, arrivals,
  auto arrange, a drop back), DesktopSelection with DesktopPicks (the locked desktop's one selection of icons and
  cards), DesktopDragOut, DesktopSort, DesktopBackground, DesktopFences (which items stand in a fence: membership,
  routing by rules, making and removing one, laying its icons on the icon grid, the one drop cell; FenceCells, its rect on the grid), DesktopDeleteUndo and DesktopFileUndo (Explorer's undo of the desktop's file steps))
  Services/ (the service sync's engine: ServiceClient, ServiceMemory, ServiceEntries, FixtureReplay; Sync/ServiceShelf)
  Model/FirstRunDesktop.cs (the first-run seed as data: ids and config fragments)
  Model/ReadyMadeGroups.cs (the add page's ready-made groups as data: kinds, boxes in DIPs, config fragments, starters)
  Model/WidgetSelection.cs (the edit-mode selection of widgets; SelectionRules: lasso, Tab, PressTarget)
src/Themia.Sdk.WinUI/  the widget SDK, WinUI: IWidgetModule, IWidgetHost (+ IShellLauncher), WidgetMenuLines, view
                   Kit/ (KitList, KitStatus, KitStatePanel, KitBinding, KitMenuLines: the kit's UI parts),
                   contracts (IWidgetView, IWidgetSurface, IWidgetKeys, IWidgetDropTarget, ...), Ticker, Fluent,
                   ThemeBrush, ItemFill, ItemMenu, RowFocus, WidgetItems, ConfirmFlyout, RingPart,
                   UiLanguage, ScrollFade, Notices, the meters' and details' shared views (MeterView,
                   DetailView, SparkBand, CoreGraphGrid), AudioFeed, the screen-time widgets' shared parts, AddFooterRow,
                   RotatedBox; Modules/: the host's contracts (IWidgetHost's members, among them the file shell's:
                   FileIcons, FileClipboard, Folders, FileOps, ExplorerMenus, RecentFiles, AppLayouts, Streams; DesktopItems with its
                   Fences, IDesktopFences;
                   SettingsHost for a module's menu lines); Shell/: FluentLayout (the layout editor's rows), ColorChoice, PlaceSearch,
                   MenuArtIcons, FolderBrowse, ScreenTimeMenuLines, SkyPlaceRows. References Themia.Sdk only, never Core. What
                   needs Win32 (eyedropper, file icons, geocoder, the OLE drop target, the shell's file operations and menus)
                   is the host's, behind those contracts or installed at start (App()).
                   Files moved into the SDKs keep their Themia.Core.* / Themia.App.* namespaces.
src/Themia.App/    WinUI 3 shell, native code, widget views
  Interop/         Win32 ([LibraryImport]), desktop layering, hotkey window
  Services/        AppHost*.cs (state, displays, placement, clipboard, keyboard lease, suspend, license),
                   shared samplers and sync services (accounts, mail, tasks, files, web; every sync an adapter over
                   Core's Sync/SyncEngine and WriteDrain, every request through Net/HttpCore);
                   Kit/ServiceSync.cs (the one service sync, IWidgetHost.Services)
  Shell/           DesktopWindow*.cs (one surface per display; .BarMenus: the edit bar's flyouts, .Properties: the
                   selection's entry and page, .SettingsMenu: the settings pages' hosts and deep links), EditBar,
                   WidgetCard, picker, view bar, CardWidgetHost (a card's IWidgetHost), Gallery/ (render flags),
                   Pages/ (PageFlyout, PageView: every page drawn as Windows' settings cards; PageRows, SelectionRows)
  Widgets/         WidgetCatalog (views from the registry), SetupPrompt, WidgetDrag, ItemPalette: no widget
                   of its own, the app hosts modules only (THEMIAMOD005 if it declares one)
src/Widgets/Themia.Widget.<Name>/     a widget module project (every widget, 109): module, config, view, Samples/
                   (its sample, ships), Preview/ (render-only states, Debug), Localization/Locales/ (its own
                   <Name>Str), SPEC.md (its section), Tests/
src/Providers/Themia.Provider.<Name>/ a provider module project (none yet; plain net10.0 over Themia.Sdk)
src/Themia.Localization.Generator/   build-time generator: Locales/*.json -> the `Str` enum
src/Themia.Modules.Generator/        build-time generator: [assembly: ThemiaWidget] -> the static ModuleRegistry
tests/Themia.Core.Tests/             xUnit over Core and the SDK, named after SPEC tags
tests/Themia.Modules.Tests/          one host for every module (globs them, compiles their Tests/), conformance
docs/STORE.md      Microsoft Store release       scripts/   publish.ps1, package.ps1, package-logos.py
SPEC.md            the behaviour spec (tags [S-NN])
```

One window per display, not per widget: every widget on a screen shares one transparent
surface that is the desktop itself ([S-5501]), clipped with `SetWindowRgn` to the work area. Geometry is stored as fractions of the whole monitor.

Adding a widget or a provider is creating its folder: run `scripts/new-module.ps1` (it writes the
project, the module class with its descriptor, the config, a view stub, the 40 locale files, the
`SPEC.md` with the next free tag block, a first test, and one line in `Themia.slnx`). The glob
references, the registry generator (THEMIAMOD001..005: the id is the folder's name in camelCase)
and the module test host pick it up; nothing in the app, Core or the SDK is edited. A **family** of small
widgets that share their code (a widget a reading, split from one that showed many: the weather facts, [S-13900])
is one project marked `[assembly: ThemiaWidgetFamily]` that declares each kind with its own id, module and
config ([S-6901]); its kinds share one add-page family (`AddFolderId`, `AddTree`). Every one-reading widget is a
**single metric** on one base (SPEC METRICS, [S-14600]..[S-14607]): its kinds as data (`MetricKind`), a `MetricModule`
each, the one face `MetricView` (one row at its least, every cell taller too, the face fitting its box by one rule:
`MetricRules.Look`, the row or the readout over the graph, [S-14700]; the trend None/Arrow/Line, the graph in the chart look
`IChartLooks` sets, [S-14703]; one item that lights and opens its kind's source, `MetricKind.Opens`, [S-14706]), a
system family's facts through `SystemMetricPart` on `SystemMetricView`; never a widget of picked facts again. Every widget whose face is a line of text (a song's
title, the rain's sentence) is a **text kind** on one base the same way (SPEC TEXTS, [S-14930]..[S-14936]): `TextKind` data,
a `TextModule` each, the one face `TextView` (one block scaled as one at the content alignment, its line cut at the card's
edge, `TextRules`; the item that lights and opens its source, `TextKind.Opens`, `ItemLight`). A module builds
on `Themia.Sdk`, `Themia.Sdk.WinUI` and provider projects only: something it needs from the host is
an SDK change first, in its own commit (docs/WIDGET-KIT.md 4.1). A view asks the host through the
`IWidgetHost` its module's `CreateView(host)` is handed ([S-6913]) for the ticker, the launcher,
the Accounts page, the Recycle Bin, the power backend, the quick-setting toggles' sources, the web
answers the sync keeps fresh (`Weather`, `Quotes`, `Rates` over `Fetched<T>`, `Feeds`) and the system widgets'
sources (`System`: the hub's `Watch`/`WatchSeries` over `Sources`; `Drives`, `Audio`, `Brightness`,
`Media`, `Loopback`) and the local stores (`ClipboardHistory` with `CopyText`, `Alarms`, `Focus`,
`Notifications`, `Notes`, `Habits`, `ScreenTime`, `Gallery`), never `ShellLauncher`; a cue sounds
through `SoundCue`; a module's menu lines have no host, so what they read of one is installed at start: its
lists (`DriveList`, `AudioDevices`, `GpuAdapters`, `SymbolLookup`) and `SettingsHost` (the notes, the
habit lists, the screen-time history, the launcher, the picker's owner window); a config's sync wants go
through the SDK's `IInterests`/`InterestsBuilder.Of<T>()`, its hub wants through `IReadsSystem`, an
alarm through `IAlarm` and the clipboard history through `IKeepsClipboardHistory`; the account
widgets read `Office` (calendars, contacts, meetings), `Mail` and `Tasks` (`ITaskSource`, its feeds and
`ILocalTaskLists`) and a kit widget `Services`, while the sync services behind them stay host code;
their menu lines read `SettingsHost.Office`, `Tasks` and `AccountsWith(feature)` and choose accounts with
`KitMenuLines.Accounts` (or the SDK's `OfficeMenuLines`, [S-7063]). The stores' files (`notes/`, `habits.json`,
`usage_stats.json`) stay host code with their validation; `LocalStoreFixtureTests` pins their shapes. A host test that needs a real widget (the seed, placement, the
add menu, styles over every kind) lives in `tests/Themia.Modules.Tests`, which has every widget and
Core's internals; Core's own tests run over a registry of no widget. The app's settings are the edit
bar's pages ([S-2300]); there is no settings window ([S-3585]) and a widget has no settings page ([S-6536]).

## Engineering principles (non-negotiable)

- **No swallowed exceptions.** No empty `catch {}`. Every error is handled, or logged through
  `Themia.Core.Diagnostics.Log` (and surfaced as a notice where the user must know), or annotated
  `// ignore: <reason>`. Tell "absent" from "corrupt". Never delete user data on a recoverable
  error (a 401 means refresh, not remove the account).
- **No exception may escape a native callback.** Win32 hooks, `WndProc`s, `[UnmanagedCallersOnly]`
  methods and COM callbacks catch, log and return.
- **Validate at every trust boundary:** files, network JSON, clipboard, drag data, user input. Parse
  into typed models; a bad document is rejected or repaired to defaults, loudly, never half-built.
  Closed sets (kinds, providers, actions) are `enum`s, not strings.
- **Persisted writes are atomic** (temp file + rename) and single-writer. Token refresh and every
  read-modify-write are single-flight.
- **RAII for every Win32/COM/GDI/clipboard handle** (`SafeHandle`, `using`); free on every path.
  Subscribe in `Attach`, unsubscribe in `Detach`.
- **No new wall-clock timing in correctness paths.** No "sleep until it is probably ready"; be event-driven.
- **Design tokens, not literals:** `Fluent.*` spacing and type, theme brushes via
  `ThemeBrush.Of(...)` (Themia.Sdk.WinUI), never a hard-coded colour or font size.
- **One path, never two.** No flag keeping the old implementation beside the new one, no parallel code
  doing the same job. Replace cleanly and de-risk with tests.
- **No backwards-compatibility code.** The app is unreleased: change the schema in place. No
  migrations, legacy-key readers, "old shape" repairs, or tests for loading older files ([S-1050]).
  Corrupt or hand-edited files are still rejected or repaired and logged: that is validation.
- **Native AOT** (`PublishAot`, `IlcTreatWarningsAsErrors`): no reflection JSON (use the source-generated
  `AppStateJsonContext`), no `dynamic`, no WMI / `System.Management`, no `PerformanceCounter`, no
  `Marshal.GetActiveObject`, no culture data (`InvariantGlobalization`: use `LocaleFormat`, not
  `ToString("MMM")`). COM through function-pointer vtables as in `CoreAudio*.cs`. A new NuGet
  package only if unavoidable and AOT-clean.
- **Secrets** never in source, logs or output. `Log` redacts centrally ([S-983]); keep it that way.
- **Resource-conserving:** share services between widget instances, never poll faster than the data
  changes, ask the shared ticker for the slowest cadence that is correct.
- **Tests for logic.** Parsers, formats, rules, state machines, merge/sync, invariants: put the logic in
  Core and test it. Skip pure UI wiring. Every bug fix ships a regression test that failed first.

## Spec and tests

- Read the subsystem's section in `SPEC.md` before changing it. Behaviours are tagged `[S-NN]`; the
  index of sections is at the top. New behaviour gets a tagged line in the right section.
- Mirror tags in test names: `S512_rejects_a_secret_without_its_envelope`.
- Separate refactor commits from behaviour changes. A refactor leaves tests green without editing
  them; a behaviour change edits the test first.
- Write-ups belong in `SPEC.md` or a code comment, and
  new `.md` files are only for docs a user or contributor reads (README, CONTRIBUTING, docs/).

## Working conventions

- **Widget sizing, one path** (SPEC "Widget sizing (SIZE)", [S-2438]..[S-2449], [S-11800];
  docs/WIDGET-KIT.md 17): **content decides a widget's size; size never decides its content.** A kind
  states `WidgetDescriptor.Sizing` (a `WidgetSizing`: Min, Natural, Max, always bounded, no steps)
  from its config; the host adds it at Natural, stops resizes at Min/Max (every grid cell between: a widget
  resizes by one cell on each axis, [S-14500]), paints the
  stored rect fitted into the sizing and refits on a config change; the grid is always on ([S-06]: no
  setting, no Alt, no free path) and it fits every sizing, a desktop icon's too, to whole grid cells first
  (`GridFit`, [S-11807], [S-11875]; a ready-made group, or touching widgets in a picture, as one block, [S-11808], [S-11809]:
  `ReadyMadeGroups.Laid`, never member by member),
  so a kind states content's DIPs, never grid sizes; a widget resizes by one grid cell ([S-14500], owner 2026-11-20:
  "the widgets should not have the size increments anymore but should be incrementable in steps of one"; no
  `SizeSteps`, `OnePathSizingTests.S14500_`). Repeated items have **one fixed size** (their natural in whole cells,
  `ItemSize.Optimal`) at every card size: they stand from the content's top, touching, as many as the card reaches,
  and the last is cut by its foot (its right edge for columns); a shorter list leaves the room under its last empty
  ([S-14810], owner 2026-11-20: "the widget items should actually have a fixed size. then when you change the height
  of a widget the items should not grow or shrink to fit all the way from the top to the bottom, they should just keep
  their size and the last item will be cutoff to some extent then."; no `ItemStretch`, `OnePathSizingTests.S14810_`).
  Inside its range a kind shows the
  same parts: what it shows is config (rows, lines, facts, what the items are, a chart's span). **Size and item
  counts follow the card** ([S-14410], owner 2026-11-20: "the size should not be settable, it is set by the size of
  the widget"; "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows the card's
  size. What the items are (which facts, which span of time a chart covers) stays a setting."): no widget has a Size
  line ([S-2449] retired; a face scales with its card), and no count line for identical items (days, hours, laps,
  columns, tiles) and no stored count: the card shows what its box holds, a resize stores the rect alone ([S-14502]).
  A list's **Rows** stays a setting (it decides the natural height; a changed Rows puts the card there, [S-14503]). A kit list states its
  rows (`IKitRows`, `KitSizing`). A view may measure its box to lay out those fixed parts
  (`IWidgetSizeAware.ApplySize(WidgetArea)`: a graph's length, where a text wraps, a grid's cells;
  `LayoutMath` for whole lines), never to choose them; costs are line heights, worst-case text lengths.
  There is no legacy sizing, no tier, no ladder, no size class: `OnePathSizingTests` fail on their
  names. Clock, Memory and Tasks are the references. Check a kind at its min, natural and max (renders,
  the showcase views; "max" is the large check, twice the natural, `SizeStretch.Large`). **Relaxed, items of one size**
  ([S-12410], [S-12411], [S-14810], docs/WIDGET-KIT.md 17.4): the descriptor relaxes the stated range to no practical
  maximum (`SizeStretch.Relax`: Max = `SizeStretch.Ceiling` on every axis; the work
  area is the bound; the user decides how large); in a larger box a repeated item keeps its size and more of them
  show (`ItemSize`: `Rows`, `RowsAbove` with an add row under them, `Lay` for a stack, `RowReach.Span` a list's
  viewport; `SpreadStack` lays a family row, `IInked.IsRow`, at its optimal); rows and fills run to the card's edge (no
  content inset; text keeps the item's own); items have no spacing between them (owner, 2026-11-19); what fills the box
  is a one-readout block, which keeps its parts together and scales up as one until its ink fills the height or the
  width stops it (`ReadingFit`/`InkFit`, `BlockFit`, `LineInk`/`PartInk`, [S-12808], [S-12804]), a graph's plot, a
  single metric's one row (its whole face, [S-14705]) and the month, a grid of the card, not a list, whose cells fill and
  whose type grows (`ScaleBox`, [S-12809], WR-9). Never a per-widget stretch of its own.
- **No card header** ([S-12480], [S-12481]; owner 2026-11-16): a card draws its view and nothing over it,
  no title row, at any size. A title is the **Header** widget (`src/Widgets/Themia.Widget.Header`, [S-9600]),
  placed like any widget; every ready-made group starts with one ([S-12485]). What a header
  carried besides a name lives in the widget's own content: the sources' state as `KitStatus` over the
  content's top end, tools as a `ToolLine` (the content's first row, counted in the sizing), whole-widget
  commands on the card menu. Never add a header-like row, title or `SetHeaderTitle`-style hook back.
  **No context in a widget, no extra band** ([S-12800]..[S-12803]; owner 2026-11-17): a widget never names
  what a Header over its group says once (place, account, device or host, a list's or feed's name, its own kind's
  name, "Last 7 days"; the Header holds the shared place, `showsPlace`); what a reading is, its kind's glyph
  leading it says (meters, graphs). Content stands one inset from every edge and reaches it: the card gives
  the top, the view the sides and foot. **A join is chrome, never layout** ([S-12483]; owner, 2026-11-17): a
  widget's content is laid out the same joined or alone; no view hears of its joins (no join hook, `CardContent`).
  A one-readout face (clock, date, the timers) is planned on the family's one-readout step (no Size setting, [S-2449]
  retired) and scaled up and down as one to fill its card ([S-12804], [S-12833]);
  the date is the Date widget's, never the clock's ([S-12805]); a quick toggle has no real minimum ([S-12806]).
- **Concise first** ([S-666], user rule): a setting may only add more of the widget's own items (rows,
  columns, a larger number, a meter's history) or one closely related detail of them — never a control,
  panel or fact that is another widget's or Windows' job (a device picker, a detail/reading pane, a note
  list, a counts footer, the next days on "weather now").
- **One family** (SPEC "FAMILY", [S-6700]..[S-6732]; docs/WIDGET-KIT.md 16): a widget is drawn from the
  family's tokens (`FamilyType`, `FamilySpace`, `FamilyShape`, `ColourRole`; `Family.*` in WinUI) and its
  seven blocks (Header (the Header widget's only), Row, Readout, Chart, Status, Tile, Controls in `Themia.Sdk.WinUI.Kit`; a
  tool line `ToolLine`); the design's look tokens
  (`DesignState.Tokens`, `LookTokens`, [S-2130]..[S-2143]: frost, glass, text levels, weights, tracking, surfaces, row
  dividers, rendering mode) reach a widget only through them (`FamilyLook`, `Family.Brush`'s widget keys, the item
  layer), never per widget; the
  guards (`FamilyGuardTests`, `scripts/family-check.ps1`) hold shrink-only lists of today's offenders.
- **List widgets are built on the kit** (`Themia.Sdk.Kit` pure, `Themia.Sdk.WinUI.Kit` UI; SPEC
  "The widget kit (KIT)", [S-7000]..[S-7071]; design and as-built in docs/WIDGET-KIT.md 5, 6 and 11.2;
  RSS, `src/Widgets/Themia.Widget.Rss`, is the reference). A list widget writes: its config as an
  `IKitConfig` (Needs, Configured, AccountIds, Shows: its setup rule follows); a spec file with its
  `KitSpec` (row look, optional `KitShow` fields, columns, time, untitled and empty sentences), the
  mapping of its data to `KitItem`s (an id that is stable or a position on purpose, plain text, a
  `Uri` link) and its `KitAction`s per item (Open, OpenTo "<verb> on <Service>", CopyLink, CopyText,
  Refresh; no view or sort switch exists); an `IKitSource` over its host contract (`Read` gives items,
  one `KitOutcome` per source, the title; `Changed`; `Busy`; `Show(refresh)`); a view of a few lines
  that owns a `KitBinding` and forwards to it (`Attach`, `ApplyConfig` to `Apply`, `ApplySize` to
  `Size`, `HandleKey`, `ContextActions` to `CardActions`, `Detach` to `Dispose`; children `List`, `State`
  and last `Status`, `Status.Place(this)`); its page lines from `KitMenuLines`
  (`Accounts`, `Count`, `Shows`; a free input is the menu's ask or list, [S-3719]). The kit decides the screen (`KitScreens`), the plan
  (`KitPlanner`: the rows, lines and fields the config states), the keys (`KitKeys`: click focuses, double-click/Enter opens, Ctrl+C copies), the menu
  (`KitMenu`, through `ItemMenu`), the stale rule (`KitFreshness`: a problem glyph, the age on the
  Refresh line, never on the widget) and opening (through `IWidgetHost.Launcher` only). A kit view
  builds no scroll viewer, row focus, menu entry, ring or empty state of its own, and the kit's
  shared code names no service, header or error code (`KitConformanceTests`). Something the kit lacks
  (a row look, page overflow, a lead) is a kit change first, in its own commit with tests.
- **Online services are data plus rules** (SPEC "The service layer (SERVICES)", [S-9900]..[S-9931];
  design docs/WIDGET-KIT.md 5.7 and 13, as built in 11.2). A service is a provider project
  (`src/Providers/Themia.Provider.<Name>`) whose module returns a `ServiceDescriptor` (`Service`),
  its sign-in members (`SignIn`: `OAuthPkceLoopback`, `OAuthDeviceCode`, `PastedToken`), a
  `BuildSecret` key only when a browser member's secret is `BuildTime` (`THEMIA_CLIENT_SECRETS`), and
  its cadence row; its rules are `IServiceQuery<TWire>`s (or a `ServiceReplica<,>` and its views) and an
  `IServiceIdentity`, over its own source-generated wire types. Its widgets add a `ServiceInterest`
  from their config's `IInterests` and read `IWidgetHost.Services.Open(interest)` as their kit
  source. Nothing outside the provider names the service: no host branch, header name or error code
  (the conformance checks). Failures are the one vocabulary (`FetchStatus`: `SignInRequired`,
  `Forbidden`, `NotFound` included), reached only through `ServiceFailures.Classify`. Previews replay
  the provider's `Fixtures/<state>.json` (scrubbed: [S-7070]). Kit services are read-only: a write is
  an `OpenTo` web page.
- **Keyboard lease** ([S-800..812]): outside edit mode the surface is `WS_EX_NOACTIVATE`, so no widget
  receives keys by default. A widget that navigates with keys implements `IWidgetKeys.HandleKey(WidgetKey)`
  and gets the lease on a click; a widget with a text box calls `IWidgetSurface.RequestTextInput`
  from GotFocus/LostFocus. The lease ends on Escape, a click elsewhere or deactivation. One key
  vocabulary: Core `WidgetKey`.
- **Widget styles** ([S-6000]..[S-6010]): a look of the same data. The kind's config implements
  `IStyled` over its own `<Kind>Style` enum (`[JsonConverter(typeof(StyleJsonConverter<…>))]`, property
  `Style`, default first, copied in `Clone`); its labels go in a `Locales/styles-<kind>/` area. The
  registry, the strip's Style choice, load repair and `--style` find it on their own:
  no shared line to edit. An existing look option becomes the style, never a second setting.
- **One colour picker, Karpo's** ([S-1549], [S-1574]..[S-1577]). A colour is a menu's one colour row
  (`menu.Colour`, a Core `SettingsColour`; `ColorChoice` behind it): Default and the recent colours, two
  lines of presets, the mixer (spectrum, hue, a fill's opacity), the hex box (a colour may be typed there,
  as in Karpo's) and the eyedropper; layout and rules in `ColourPicker`. Stored as `#RRGGBB`.
- **The edit bar** ([S-2320]..[S-2329], Karpo's command bar, `karpo-app` docs/widgets.md §575): one 48-DIP row
  per display, one width, at the top, never moved or dragged ([S-2329]; a selected widget under its page is
  seen through it instead, [S-2344]): the pages' entries (General, Accounts, Display, Views, Appearance;
  `SettingsPages.Entries`), the divider and the selection's entry in its kept room, no toggle (the grid is always on,
  no Snap to grid, [S-06]; widgets always go over the screen edge, no Over the edge, [S-2330]; widgets may always
  overlap, no Keep widgets from overlapping, [S-11959] retired), the problems' room, the Widget gallery's button
  ([S-14910]), Add widget, Unplaced icons right after Add (its icon alone and a count badge, [S-13700], [S-13701]), Done. Each entry opens
  its own flyout hung from it by a beak (580 wide, at most 70 % of the work area, Windows' ground and cards,
  never the widgets' look), one at a time; the Unplaced icons page alone has no light dismiss and takes desktop icons moved
  onto it off the view, the view's All/Chosen kept (`SettingsPages.StaysOpen`, `UnplacedItems.Unplace`, [S-13713], [S-13715]). No trail, no Undo/Redo buttons (the keys stay). Layout rules are Core's (`EditBarLayout`, `FlyoutPlacement`), metrics
  `ShellMetrics.Bar`, `.Flyout`, `.Page`, `.Inline`. **One setting, one level** ([S-3590]..[S-3592]): a setting
  is a widget's (its page), a view's (Views), a display's (Display) or Themia's (General, Accounts,
  Appearance), and that page alone sets it; a lower level that follows it just follows it: no copy and no
  link row ([S-3749]: the only link rows lead where the user must go to do something, an account to add or
  manage, `WidgetMenuLines.AccountsLine`), and no widget config holds a format, cadence, theme or language of
  its own. The guard tests (`S3592_`, `S2306_`) fail on a setting set twice, a widget's link to a setting it
  follows, or a setting without a page.
- **The app's settings are the edit bar's pages** ([S-2300]..[S-2309]), never a window. A page is Core
  lines (`SettingsMenu.<Section>.cs`: `SettingsToggle`, `SettingsChoice`, `SettingsSlider`, `SettingsColour`,
  `SettingsSubmenu`, `SettingsCommand`, `SettingsNote`, `SettingsField`, ...) keyed by paths that are the same
  in every language, grouped by one rule (`SettingsPages.Sectioned`) and tested in Core (`SettingsMenuTests`);
  the app draws them as Karpo's/Windows' settings cards (`Shell/Pages/PageView`, Karpo's components one to one:
  the table in [S-2317]; a row that acts a card with a button of its words, one with nothing to say its button
  alone, [S-2318]): a row 48 tall (60 with a caption), at most two levels (a card and one level opened inside it; deeper is a sub-page behind a back
  head), every choice, colour, font, icon and lookup an inline picker in its card, never a popup. **No page of its own
  for one input, anywhere** ([S-2351]; owner, 2026-11-20: "this type of settings component should not exist anywhere in
  themia settings"): a line of text asked or an "are you sure?" is a section in its own card (`SettingsRowKind.Ask`,
  Core's `AskInPlace`, `PageView.Asks`), its row its words, what it holds and the expand chevron; a sub-page holds many
  lines, never one input. Every way
  into settings lands on its row (`RequestSettings(key)`, [S-3534]). The pages follow Karpo's (`karpo-app`
  origin/master): every view, display and account is a **thing**, a card that opens in place
  (`SettingsSubmenu.Card`, its `Caption`, its head's `Tools`, dragged by its grip, [S-2310]); what opens is scrolled
  into sight as little as it takes, gliding, as Karpo's (`PageReveal`, [S-2347]); and no line acts
  on "the selected" view, display or account; the Appearance page opens on a live preview of real cards
  ([S-2311]); the styles are Karpo's palettes plus Liquid glass, a card material drawn from Core's values
  (`DesignPresets`, `LiquidGlass`, [S-2122], [S-2123]). `FluentLayout` (`SettingsCard` rows) is
  only the app-layout editor's, Themia's one window.
- **View links** (SPEC "VIEW-LINKS", [S-3300]..[S-3311], [S-11900]..[S-11958]; docs/WIDGET-KIT.md 19):
  what is pointed at in one widget lights the same thing in the others of the view. A kind declares what it
  publishes and follows in its descriptor (`WidgetDescriptor.Links`, a `LinkUse`; its code names exactly
  those `LinkTopics`, `S11906_`), keys through `LinkKeys` only, joins through `WidgetLinks` and draws the one
  look through `LinkLights`/`LinkLook`/`LinkCard` (hover fill; selected fill and accent pill; others at
  0.6 or 0.3). SPEC's topic table is made from the descriptors (`S11907_`).
- **Item menus** through `ItemMenu`; icons through `Glyphs`; lists keep `RowFocus`/`ScrollFade`. One leading column
  ([S-12952]): a toggle or choice line shows its check or dot in the icon slot (`MenuMarks.Slot`), never a
  Toggle/RadioMenuFlyoutItem or AppBarToggleButton (`S12952_` guard, no file excepted; the view bar's select menu too).
- **Widget selection, no groups** ([S-1580]..[S-1597], [S-2450]..[S-2457]; owner, 2026-11-17: "actually remove
  the group feature"): there are no groups, no `WidgetGroups`, no view `Groups`, no widget `group`, no Ctrl+G, no
  group page; a state file that still names them loads with those keys dropped loudly (`StateDocument.DropUnknownKeys`).
  Edit mode selects widgets (`WidgetSelection`: a press the widget alone or keeps a selection that holds it, a click
  narrows, Ctrl/Shift+click toggles one, the lasso what it touches, Tab one by one, Escape clears); several selected
  move as one block (snapped, whole device pixels, [S-11873]; over any widget, nothing keeps them apart), resize as one block by the
  selection's frame round them or Shift+arrows (`BlockResize`: scaled about the opposite edge, every edge on a grid line,
  on a common step, each member in its own sizing, [S-2460]..[S-2465]) and are set together on the page
  of several ("For all of them" and Appearance, [S-11754], [S-11755]). One selected card shows its marks; **two or more
  show none of their own** ([S-14310], owner 2026-11-20: "when mutliple widgets are selected the individual widgets
  corners in the group should not be highlighted / show up"): the selection's frame alone marks them (edit mode: its
  hairline, markers and grips, the members' grips inert; the locked desktop: its four corners round the display's
  selected cards, never an icon, moving with a locked move), counted by one rule (`SelectionRules.Framed`, the marks by
  `SelectionRules.MarksShown(…, framed)`, re-applied by `DesktopWindow.SyncFrame`); a selected
  card's grips and move area are lifted over every card (`WidgetCard.MarkerLift` on `EditMarkerLayer.Lifts`,
  `SelectionRules.PressTarget`, [S-1597]; a member of several its move area alone) so one under another keeps all its
  handles; the drawing order stays.
  **Any two cards whose cells meet join** (`CardJoins.Paint`: neighbours by cells, [S-2240], [S-2248], [S-2249]):
  square inner corners, one outline with no line at the seam ([S-11870], `CardOutline.Seams`), in Liquid glass one
  body (`CardJoins.Bodies`, [S-2123]); a desktop icon never joins (`WidgetTraits.NeverJoins`, [S-11880]); there is
  no gap between widgets, every card paints its whole cell ([S-11884]); a view whose fill reaches the card's edge
  takes the card's corners (`IWidgetCorners`, [S-11883]). The cards a drag carries join only among themselves
  ([S-2457]). Everything that produces or moves widgets is the one placement session (`PlacementPayload`: a new
  widget, a ready-made group, a copy, a move); a ready-made group lands as loose widgets laid as one block
  (`ReadyMadeGroups.Laid`) and selected ([S-2471], [S-2475]).
- **The add page** ([S-13610]..[S-13617], [S-11701]..[S-11712], [S-3510], [S-3511], [S-2473]; Karpo's §593; owner,
  2026-11-19: the library will hold hundreds of kinds): Add opens a flyout as wide as the bar, flush under it: a
  **rail** (the search box, then Start, each subject and More with its count, Karpo's side list) and the chosen
  subject's content (its ready-made groups, its own widgets, then each **family** of two or more as a **stack**: a
  head with name, count and chevron over one row of tiles ending in "+N"; the head or "+N" opens it in place).
  Search is flat over every widget and group. Compact tiles of at least 200 (icon, name, caption: Karpo's) and
  beside them a preview pane of the tile or family head pointed at or focused ([S-13600]..[S-13604]): a picture of
  the thing itself (`AddPictures`: real cards on sample data, in every build; a glyph until drawn), drawn lazily,
  the previewed first. The model is Core's (`AddTree` the subjects and families, `AddRail`, `AddPage` the
  sections, stacks and search, `AddPageKeys`, `AddPageLayout`), drawn by `Shell/Pages/AddPageView`; a widget
  places itself in the tree through its descriptor; a recipe is data only. Picking a tile starts the placement
  session. **The widget gallery** ([S-14910]..[S-14917]; owner, 2026-11-21) is a second page beside it (its own bar button):
  every widget as real sample cards on the display's own grid over the work area, grouped by subject and family in blocks
  (`WidgetGallery`, Core, tested), screens per category; picking is edit mode's selection; a drag is the one placement session
  (`PlacementPayload.ForGallery`, held where the pointer held it: no jump) and the gallery comes back after it.
- **Samples and previews** ([S-11850]..[S-11854]; docs/WIDGET-KIT.md 6.8): **a widget's sample ships, its
  render-only states do not, by folder.** The sample (the add page's picture, on the sample day's data in
  `Themia.Sdk/Samples`) is `IWidgetSample.ShowSampleAsync` in the module's `Samples/<Name>View.Sample.cs`,
  compiled in every configuration; error, loading, empty, hovered and edge-case states are
  `IWidgetPreviewState` in `Preview/`, removed outside Debug by `src/Widgets/Directory.Build.targets`, and
  their `sample` state calls the sample. No `#if` in either. A sample holds its data in the view alone, never
  writes a store, the clipboard or anything another widget reads; a view whose host already hands it
  sample data (the meters over the sample PC) has none. The sample PC and a gallery service reach a
  picture's cards only through their host (`SampleWorld`, `PreviewCardHost.Samples`), never a static.
- **The locked desktop** ([S-2008], [S-11874]..[S-11879]): outside edit mode a widget pressed and dragged moves
  (with the selection it is in, [S-11879]) on the first press whatever part is under it ([S-13060]: the card arms on
  every press, handled ones too; parts act on the release, never the press), snapped, the local
  grid round where it lands (`LocalGrid`: a faint rectangle fading out of the box, [S-11886]) with the moving and nearby cards' corner
  marks ([S-11881]; several selected moving: the selection's frame's corners instead of theirs, [S-14310]); edit mode never turns
  on by a drag; **Lock widgets** ([S-12950], [S-12951]; its home General › Desktop icons, the menus' line under Edit
  mode flips it) keeps every widget where it is outside edit mode, icons alone move (`WidgetLock`: a mixed selection
  moves its icons; edit mode moves everything); a move may land over any widget (no keep-apart rule, [S-11959]
  retired). Edit mode is entered and left by one checked menu line, **Edit mode** ([S-65]). Desktop icons are widgets for placement (`DesktopGrid`: whole cells, arrivals in reading order);
  an icon's move becomes the shell's drag only off the desktop or over a drop target (`DesktopDragOut`); under the pointer it
  shows the held icon alone however many move, never a stack or a count, and the shell's drag carries that picture
  ([S-13046]..[S-13048], `DragGhost.OfMove`). One
  selection of icons and cards (`DesktopSelection` + `DesktopPicks`), one lasso; `--drag-png` draws them. A
  selected icon shows its highlight alone; its corners only in edit mode and a move ([S-11889]). Ctrl+Z / Ctrl+Y
  on the desktop step the one history; a desktop delete's step puts its items back from the Recycle Bin
  (`DesktopDeleteStep`, an `IUndoEffect` the undo log carries both ways, [S-5597]), and a paste, a drop from outside,
  New ›, Ctrl+Shift+N, Paste shortcut and a rename are steps too (`DesktopFileStep`, `DesktopFileUndo`, recorded when a
  read of the desktop sees what the command's landing named, [S-13102]; the background menu's "Undo …" line, [S-13103]).
  Explorer's own undo is out of reach (its process): never claim it, carry the step. Parity with Windows' desktop is the
  bar (owner, 2026-11-19: "without noticing a difference to normal windows"): SPEC PARITY lists what is left and why.
- **Fences** (SPEC FENCES, [S-13000]..[S-13014], [S-13040]..[S-13044]; the module `src/Widgets/Themia.Widget.Fence`,
  [S-10900]..[S-10920]; owner, 2026-11-19: "the fence should basically just be a helper to align the icon on the
  underlying engine without this transition in between"): a fence is a widget whose config holds real desktop items by key
  (`IHoldsDesktopItems`), a **region of the desktop icon grid**: its title bar is grid rows, its body whole icon boxes, and
  each item it holds is the desktop's own icon (its item widget on that view) laid on a cell of the body by
  `DesktopFences.Lay` in every `Mutate` (`AppHost.LayFences`). It draws no item of its own (only a search's files and a
  picture's items, in the same cells, `FenceGrid.Layout`). An icon moved into, inside or out of a fence is the desktop's one
  move; where it lands is `DesktopFences.DropCell` for the free desktop and a fence alike, and `DesktopFences.Land` makes it
  the fence's or free on the drop: no hand-off, preview or glide of a fence's own. Never write a second layout or drag for
  fence items. On its view its items are never free icons (`DesktopViews.Free` leaves them out; `Shown` = `Free` +
  `DesktopFences.Held`); a view's All/Chosen/None is the free desktop's alone; auto arrange, Sort by and the settle lay the
  free icons only (`DesktopFences.FreeIcons`). What is the fence's (roll up, scroll by whole rows, remove, drops of files,
  paste, new folder) is asked of `IDesktopItems.Fences`, carried out by Core's `DesktopFences` through `Mutate`;
  `AppHost.PlaceInFence` puts items into one from elsewhere. Membership is the arrangement: the undo snapshot copies it
  (`WidgetShape.Held`). Removing a fence never deletes a file (its icons stay where it showed them). Made by a right-drag rectangle on empty
  desktop ("Create fence here"), "Create fence from selection" in the icon menu, or Add widget. Rules route new items
  (`FenceRules`; first matching fence of each view).
- **Item selection** (files: anything selected like desktop icons) through the SDK's one rule set
  `ItemSelection` (+ `SelectionBand`) and one look `SelectionChrome` ([S-6620]..[S-6629]); never a selection of its own.
  A desktop icon is one kind, `desktopItem`, with two sources ([S-5600]): a desktop item (a key, the
  reconcile's) or an own icon (what the Shortcut widget was). Both are in the host's one desktop selection
  ([S-5540], keys by `DesktopItemKeys.Of`), drawn in the SDK's one cell (`DesktopCell`, at the desktop's one
  icon size, [S-5593]: a preference the host puts in force, never an icon's own) with the highlight
  behind the whole cell in its own palette (`SelectionLook.DesktopIcon`); what a command does to a selection
  of both is the SDK's `DesktopCommands` ([S-5580]..[S-5587]). Which desktop items a view shows is that view's setting,
  **Desktop icons** (All, Chosen, None; in its card on the Views page), and its rules are Core's `DesktopViews`
  ([S-5525]..[S-5529]): a new item arrives on every All view, never on a Chosen or None one. The kind states
  `WidgetTraits.StartsTransparent` to start on no card.
- **The selection's page** ([S-2340]..[S-2343]): selecting widgets adds their entry to the bar ("Clock",
  "2 × Clock", "3 widgets") and opens its flyout once per selection, when the pointer lets go of the widget
  (a click, the end of a drag or resize; never over a widget still moving): a header card (the kind's tile, the
  name, Cut / Copy / Remove), then two tabs ([S-2345], `SelectionPage.Tabs`, the one tab component
  `PageTabButton`): Settings (What it shows) and Appearance. Every option a widget has lives
  there; there is no widget settings page ([S-6536]): its module's `MenuLines(WidgetMenuLines, config)` adds
  `Lines` and `StyleLines` (under the Style picker, what `config.Offers` says the look draws) as
  `ItemMenuEntry`s (`Toggle`, `Choice`, `Number`, `Slider`, `Colour`, `Fill`, `Submenu`, `Search`, `Command`,
  `Ask` (`WidgetMenuLines.Ask`: the line carries its `WidgetTextAsk`), `Confirm`, `Note`, `List` (its items dragged or Ctrl+Up/Down to reorder),
  `PickFolder`, a link row `LinkTo` (to the accounts only, [S-3749])), separators between its groups, and marks the line the Set up button
  opens with `WidgetMenuLines.SetsUp` ([S-1862]); `PageRows` turns each into the page's row of its kind. An
  Ask or a Confirm is asked in place, in its own card ([S-2351]): never a page of its own. The desktop icon (an own icon's lines, `OwnIconMenu`) and Clock are the references; docs/WIDGET-KIT.md 15 is
  the guide. A setting several kinds hold (a place, accounts, a drive, a host, a span, Rows) is an
  entry of the SDK's `SharedSettings` over a config contract, and its standard line is marked with
  `WidgetMenuLines.Shared` ([S-11750]..[S-11753]); a new kind that holds one implements the contract and
  uses (or marks) that line. **The page of several** ([S-11754], [S-11755], `SelectionPage.Of` in Core,
  `SelectionRows` in the app, `SharedRowLines` in Sdk.WinUI): For all of them (each shared setting once: the first holder's
  own line over a scratch copy, every write set on every holder, Mixed while they disagree) and Appearance,
  never a widget one by one ([S-11755]).
- **Text** goes through the `Str` layer (`Str.X.Text()`); a new key goes into `en.json` and its
  `$englishOnly` list. Dates and numbers through `LocaleFormat`.
- **Out of scope by decision:** tray icon, always-on-top pin, pop-out windows, Lemon Squeezy, updater,
  telemetry. The old hub is gone. Distribution is the Microsoft Store (MSIX) only.
- Match the surrounding style: `sealed partial` views, file-scoped namespaces, `///` comments that say
  why. No AI attribution lines in commits or PRs. Do not create branches unless told to.

## Definition of done

1. `dotnet build` (Debug) is green with warnings as errors, and the tests pass at the tier below.
2. New behaviour has a test; a fixed bug has a regression test.
3. UI work: rendered through `--editbar-png`, `--widget-png` (and the other render flags) and looked at. Widget layout is
   checked at its min, natural and max on the showcase views (`--add-showcase-views`) on the live desktop: a manual pass, say so.
   **Any change to a widget, the kit, widget layout or the gap check ends with the widget audit** (the saved workflow
   `.claude/workflows/widget-audit.js`, args `{repo, out, exe?, kinds?}`): every kind it audits × every owner rule in
   `docs/WIDGET-RULES.md` gets a verdict from a reviewer who did not build the change, failures are re-measured by a
   skeptic, and a critic lists exemptions the owner never granted. Not done until it returns `pass: true`; report its
   verdict, not "looked fine". The rules file is the owner's words only; a check script or SPEC entry that allows less is the bug.
4. **Check tiers** (owner, 2026-11-20: "you dont have to test everything everytime"; [S-14344]). The checks
   (`scripts/item-check.py`, `scripts/gap-check.py`, the audit) share one cached render pass a build ([S-14341]: a second
   check on the same build draws nothing) and take `--changed [range]` (the default: the branch against winui, uncommitted
   files included), `--kinds a,b` or `--all`; each kind picked is printed with why (`scripts/changes.py`, [S-14342]).
   Always pass `--exe` (your own build) and `--artifacts`/`--artifacts-path` your own folder.
   - **While working:** `python scripts/test.py` (the tests the change touches); for widget work
     `python scripts/item-check.py --exe <exe>` and `python scripts/gap-check.py --exe <exe>` (the kinds it touches); the
     audit with its default kinds.
   - **Before an install or a merge:** `python scripts/test.py --all`, and both checks with `--changed since-sweep`
     (every kind changed since this checkout's last full sweep; no sweep recorded: `--all`).
   - **A full sweep (`--all`)** only when shared layout code or the check rules change: `--changed` then picks every kind
     itself and says why (the card, the render path, the scripts, `docs/WIDGET-RULES.md`, the shared build); the audit
     with `kinds: 'all'`. A full sweep is recorded (`TestResults/last-full-sweep.txt`) for `since-sweep`.
5. Touched interop, focus, z-order, displays, autostart, Store or sign-in code that cannot run in CI:
   say exactly what still needs a manual pass (multi-monitor, mixed DPI, a live account, the
   packaged build). Never imply it was verified.
6. `SPEC.md` matches the behaviour.
