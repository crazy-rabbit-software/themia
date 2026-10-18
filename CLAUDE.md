# Themia (WinUI)

A Windows desktop-widget app by Lometics GmbH: transparent widgets that live on the desktop layer,
with no main window and no tray icon. This branch (`winui`) is the rebuild on **WinUI 3 / .NET 10,
Native AOT** (Windows App SDK 1.8, self-contained). The original Tauri/SolidJS/Rust app is on
`main`; read it with `git show main:<path>` and never switch branches. Source-available under the
Elastic License 2.0 (`LICENSE`). Unreleased: nothing here has users yet.

## Build, test, publish

```powershell
dotnet build src/Themia.App/Themia.App.csproj -c Debug            # Debug builds include the render flags below
dotnet test tests/Themia.Core.Tests/Themia.Core.Tests.csproj      # Core and the SDK's rules (xUnit)
dotnet test tests/Themia.Core.Tests/Themia.Core.Tests.csproj --filter "FullyQualifiedName~Clock"   # scoped run
dotnet test tests/Themia.Modules.Tests/Themia.Modules.Tests.csproj   # every module's own Tests/ + conformance + the state fixture
powershell -ExecutionPolicy Bypass -File scripts/new-module.ps1 -Kind widget -Name <Name> -Label "<English name>"   # or -Kind provider
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1      # Native AOT, unpackaged (-Run to start it)
powershell -ExecutionPolicy Bypass -File scripts/package.ps1      # unsigned Store .msixupload, see docs/STORE.md
```

- Several builds at once (agents, parallel work): pass `--artifacts-path <own folder>` so they do not
  fight over locks. `artifacts/` is git-ignored.
- AOT publish needs the MSVC linker; `publish.ps1` puts `vswhere` on PATH for the ILCompiler. A build
  that passes in Debug can still fail or misbehave only when published (compiled XAML is not a
  publish item by default; the `PublishCompiledXaml` target in the csproj fixes that). Run
  `publish.ps1` after touching the csproj, XAML, interop or anything reflection-adjacent.
- Official builds read `THEMIA_GOOGLE_CLIENT_SECRET` at build time; it is never in source.

### Looking at the UI without the desktop (Debug builds only)

Render flags draw off-screen to PNG and exit. Look at the PNGs; they are how UI work is verified.

```
Themia.exe --settings-png <Page|Widget:<Kind>> <out.png> [waitMs] [--theme light|dark] [--search t] [--land t]
           [--flyout row] [--width dips] [--open first|name] [--views n]      # Page: General..About, Displays, Views
           [--place-chosen|--place-cleared] [--place-query t [--place-reply results|none|offline|error|searching]]   # place row [S-2847]; --place-cleared on --widget-png too
Themia.exe --widget-png <Kind> <out.png> [w h] [--theme light|dark] [--state name] [--style name] [--sizes WxH,..|tiers] [--keys k1,k2|text:abc]
           # the 7 tiers (or w×h); a wrong --state prints the kind's states (Stocks: quote|falling)
           [--design border=dashed,borderWidth=4,borderOpacity=0.5,borderColor=#FF0000,radius=4,preset=Midnight,...]
           # --style: any styled kind in one of its styles [S-6006] (Clock: digital|analog|stacked|flip|word|minimal; --state sample fixes 10:42)
Themia.exe --picker-png <out.png> [--search t]       Themia.exe --iconpicker-png <out.png> [--search t] [--initials]
Themia.exe --viewbar-png | --notices-png | --menu-png | --motion-png <outDir>
Themia.exe --editbar-png <outDir> [--theme light|dark]   # edit bar: snap on/off, add menu open, placing; --picker-png takes --theme too
Themia.exe --layout-editor-png <out.png> [waitMs] [--empty]
Themia.exe --add-showcase-views [<statePath>]   # one view per widget kind at the 7 sizes; quit Themia first
Any render: [--lang en|de|es|fr|it|ja|pl|pt|zh]
```

Build output (`artifacts/`, `bin/`, `obj/`) cannot be read by agents; write render PNGs outside it.

**Never start `Themia.exe` without a render flag as the first argument.** A bare start hands off to
the copy that is already running and toggles its edit mode. Do not drive the user's running
windows, and do not touch `%APPDATA%\Themia` or `%LOCALAPPDATA%\Themia`: the renders use a detached
state store.

## Layout

```
src/Themia.Sdk/    the widget SDK, pure (docs/WIDGET-KIT.md 4.2): no assembly of ours; Core builds on it.
  Localization/ (+ Locales/*.json: the host's Str; StringTable, LocalText) Diagnostics/ (Log) Links/ Time/ (CalendarDays) Shell/ (Glyphs)
  Model/ (WidgetConfig, FillChoice, RefreshPreferences, ContentAlign; Configs/: the shared config bases QuickToggle-,
  PowerButton-, Weather-, SkyPlace-, Detail-, GaugeConfig and the config contracts ISetupRule, IPlaced, IContentAligned)
  SystemData/ (the hub's data: FeedKey, SourceId, Sources and their snapshots, MeterReading, series, SystemInterests
  with IReadsSystem, ISystemFeeds, WatchSlot) and the system rules in Widgets/ (meters, details, drives, audio, music)
  and the local stores' shared data and rules (clipboard history, alarms, focus, notifications, Notes/NoteMeta,
  Habits/HabitLists with the periods and streaks, screen time, the gallery's sources, scans and Bing photo)
  Widgets/ (WidgetArea, Disclosure, tiers, keys, fits, styles, IShownSettings, Formatting, RingArc, DotGrid, item roles,
  HexColor, PlaceLookup, WebInput) Settings/ (ColorPresets, ScreenPick, SettingsSearch) Shell/ (Glyphs, KeyChord, MenuArt,
  the item menu's line layout, Notice) Sync/ (UpdateCadence, HostCadences) Files/ (DropEffect) Modules/ (the ids,
  WidgetDescriptor, ModuleCatalog, IProviderModule, ProviderCatalog, FeatureSet, CadenceRow, WidgetTraits, AddPlacement)
src/Themia.Core/   pure logic, no UI types, no Windows dependency. The host's rules, and the widgets not yet moved.
  Geometry/ Model/ (state, Configs/* with each in-Core widget's descriptor) Storage/ (atomic store, load validation, StateJson)
  Widgets/ (per-widget rules + the responsive API) Accounts/ Mail/ Office/ Tasks/ Notes/ (NoteStore) Habits/ (HabitListStore)
  Settings/ Shell/ Motion/ Licensing/ Net/ Sync/   Accounts/HostProviders.cs (the five account providers as modules)
  Model/FirstRunDesktop.cs (the first-run seed as data: ids and config fragments)
src/Themia.Sdk.WinUI/  the widget SDK, WinUI: IWidgetModule, IWidgetHost (+ IShellLauncher), WidgetSettingsRows, view
                   contracts (IWidgetView, IWidgetSurface, IWidgetKeys, IWidgetDropTarget, ...), Ticker, Fluent,
                   WidgetLayout, ThemeBrush, ItemFill, ItemMenu, RowFocus, WidgetItems, ConfirmFlyout, RingPart,
                   UiLanguage, ScrollFade, EmptyStatePanel, Notices, the meters' and details' shared views (MeterView,
                   DetailView, SparkBand, CoreGraphGrid), AudioFeed, the screen-time widgets' shared parts, AddFooterRow,
                   RotatedBox; Modules/: the host's contracts (IWidgetHost's members, SettingsHost for a settings
                   page); Shell/: FluentLayout (settings rows), ColorChoice, RowReorder, SettingRow, PlaceSearch,
                   MenuArtIcons, FolderBrowse, ScreenTimeSettingsRows. References Themia.Sdk only, never Core. What
                   needs Win32 (eyedropper, file icons, geocoder, OLE drag) is the host's, installed at start (App()).
                   Files moved into the SDKs keep their Themia.Core.* / Themia.App.* namespaces.
src/Themia.App/    WinUI 3 shell, native code, widget views
  Interop/         Win32 ([LibraryImport]), desktop layering, hotkey window
  Services/        AppHost*.cs (state, displays, placement, clipboard, keyboard lease, suspend, license),
                   shared samplers and sync services (accounts, mail, tasks, files, web)
  Shell/           DesktopWindow*.cs (one surface per display), WidgetCard, picker, view bar,
                   CardWidgetHost (a card's IWidgetHost), Config/ (settings window), Gallery/ (render flags)
  Widgets/         Modules/<Name>Module.cs (each in-app widget's module) + one *View.cs per widget
src/Widgets/Themia.Widget.<Name>/     a widget module project (68 so far: year progress, M4 part B groups 1-4 and 6): module, config, view, Preview/,
                   Localization/Locales/ (its own <Name>Str), SPEC.md (its section), Tests/
src/Providers/Themia.Provider.<Name>/ a provider module project (none yet; plain net10.0 over Themia.Sdk)
src/Themia.Localization.Generator/   build-time generator: Locales/*.json -> the `Str` enum
src/Themia.Modules.Generator/        build-time generator: [assembly: ThemiaWidget] -> the static ModuleRegistry
tests/Themia.Core.Tests/             xUnit over Core and the SDK, named after SPEC tags
tests/Themia.Modules.Tests/          one host for every module (globs them, compiles their Tests/), conformance
docs/STORE.md      Microsoft Store release       scripts/   publish.ps1, package.ps1, package-logos.py
SPEC.md            the behaviour spec (tags [S-NN])
```

One window per display, not per widget: every widget on a screen shares one transparent,
click-through surface clipped with `SetWindowRgn`. Geometry is stored as fractions of the whole monitor.

Adding a widget or a provider is creating its folder: run `scripts/new-module.ps1` (it writes the
project, the module class with its descriptor, the config, a view stub, the 30 locale files, the
`SPEC.md` with the next free tag block, a first test, and one line in `Themia.slnx`). The glob
references, the registry generator (THEMIAMOD001..004: the id is the folder's name in camelCase)
and the module test host pick it up; nothing in the app, Core or the SDK is edited. A module builds
on `Themia.Sdk`, `Themia.Sdk.WinUI` and provider projects only: something it needs from the host is
an SDK change first, in its own commit (docs/WIDGET-KIT.md 4.1). A view asks the host through the
`IWidgetHost` its module's `CreateView(host)` is handed ([S-6913]) for the ticker, the launcher,
Settings › Accounts, the Recycle Bin, the power backend, the quick-setting toggles' sources, the web
answers the sync keeps fresh (`Weather`, `Quotes`, `Rates` over `Fetched<T>`) and the system widgets'
sources (`System`: the hub's `Watch`/`WatchSeries` over `Sources`; `Drives`, `Audio`, `Brightness`,
`Media`, `Loopback`) and the local stores (`ClipboardHistory` with `CopyText`, `Alarms`, `Focus`,
`Notifications`, `Notes`, `Habits`, `ScreenTime`, `Gallery`), never `ShellLauncher`; a cue sounds
through `SoundCue`; a settings page has no host, so what it reads of one is installed at start: its
lists (`DriveList`, `AudioDevices`, `GpuAdapters`, `SymbolLookup`) and `SettingsHost` (the notes, the
habit lists, the screen-time history, the launcher, the picker's owner window); a config's sync wants go
through the SDK's `IInterests`/`InterestsBuilder.Of<T>()`, its hub wants through `IReadsSystem`, an
alarm through `IAlarm` and the clipboard history through `IKeepsClipboardHistory`; the other shared data
services (`AppServices.Feeds`, the mail, office and task sources, the folder listing and file icons)
are host types until each gets an SDK contract (part B). The stores' files (`notes/`, `habits.json`,
`usage_stats.json`) stay host code with their validation; `LocalStoreFixtureTests` pins their shapes. A host test that needs a real widget (the seed, placement, the
add menu, styles over every kind) lives in `tests/Themia.Modules.Tests`, which has every widget and
Core's internals. The widgets not moved yet (M4 part B, docs/WIDGET-KIT.md 11.1) keep their
descriptor on their config in Core and their module in `Widgets/Modules/<Name>Module.cs`. Settings pages register one line in `ConfigPages.AppPages`.

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

- **Responsive widgets** (`Themia.Core/Widgets`, S-660..S-666): a widget adapts as it grows, by a
  ladder of parts, not by scaling one layout. Implement `IWidgetSizeAware.ApplySize(WidgetArea)`;
  classify on the `WidgetArea` (`WidthClass`/`HeightClass`/`Shape`, breakpoints 180/400/720 and
  128/280/480 DIPs), measure on the deflated box, spend height through one `Disclosure`
  (`Reserve` for the must-show part, `Take` for optional rungs in priority order, `TakeRows` for the
  list). Costs are line heights, not font sizes; use worst-case text lengths. **Concise first**
  ([S-666], user rule): a rung may only add more of the widget's own items (rows, columns, a bigger
  hero, a meter's graph) or one closely related detail of them — never a control, panel or fact that
  is another widget's or Windows' job (a device picker, a detail/reading pane, a note list, a
  counts footer, the next days on "weather now"). Room left at L/XL goes to the core content (a
  larger ramp step, more rows); if that is used up, empty space is fine. Clock and the meters are the
  reference. Check every size tier on the showcase views.
- **Keyboard lease** ([S-800..812]): outside edit mode the surface is `WS_EX_NOACTIVATE`, so no widget
  receives keys by default. A widget that navigates with keys implements `IWidgetKeys.HandleKey(WidgetKey)`
  and gets the lease on a click; a widget with a text box calls `IWidgetSurface.RequestTextInput`
  from GotFocus/LostFocus. The lease ends on Escape, a click elsewhere or deactivation. One key
  vocabulary: Core `WidgetKey`.
- **Widget styles** ([S-6000]..[S-6010]): a look of the same data. The kind's config implements
  `IStyled` over its own `<Kind>Style` enum (`[JsonConverter(typeof(StyleJsonConverter<…>))]`, property
  `Style`, default first, copied in `Clone`); its labels go in a `Locales/styles-<kind>/` area. The
  registry, the strip's Style choice, the settings row, load repair and `--style` find it on their own:
  no shared line to edit. An existing look option becomes the style, never a second setting.
- **Colours are never typed.** Use `FluentLayout.ColorPickerRow` / `ColorChoice` (swatch button, accent
  presets, custom `ColorPicker`). Stored as `#RRGGBB`.
- **Settings rows** come only from `FluentLayout` (`SettingsCard`/`SettingsExpander`), so a restyle
  reaches every page. Never build a card by hand. Each row's frame carries a `SettingRow` for search.
- **Item menus** through `ItemMenu`; icons through `Glyphs`; lists keep `RowFocus`/`ScrollFade`.
- **Text** goes through the `Str` layer (`Str.X.Text()`); a new key goes into `en.json` and its
  `$englishOnly` list. Dates and numbers through `LocaleFormat`.
- **Out of scope by decision:** tray icon, always-on-top pin, pop-out windows, Lemon Squeezy, updater,
  telemetry. The old hub is gone. Distribution is the Microsoft Store (MSIX) only.
- Match the surrounding style: `sealed partial` views, file-scoped namespaces, `///` comments that say
  why. No AI attribution lines in commits or PRs. Do not create branches unless told to.

## Definition of done

1. `dotnet build` (Debug) is green with warnings as errors, and the scoped tests pass.
2. New behaviour has a test; a fixed bug has a regression test.
3. UI work: rendered through `--settings-png` (and the other render flags) and looked at. Widget layout is
   checked on the showcase views (`--add-showcase-views`) on the live desktop: a manual pass, say so.
4. Touched interop, focus, z-order, displays, autostart, Store or sign-in code that cannot run in CI:
   say exactly what still needs a manual pass (multi-monitor, mixed DPI, a live account, the
   packaged build). Never imply it was verified.
5. `SPEC.md` matches the behaviour.
