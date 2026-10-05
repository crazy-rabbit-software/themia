# Themia

**Transparent widgets for your Windows desktop.** Clock, system meters, mail, calendar, tasks, notes,
weather and more, drawn on the desktop layer behind your windows. By [Lometics GmbH](https://lometics.com).

This branch is the native rebuild: **WinUI 3, .NET 10, compiled ahead of time (Native AOT)**. There is
no embedded browser and no JavaScript engine in the app itself, and nothing runs when nothing has
changed. The original Tauri version is on the `main` branch.

![the clock widget in edit mode](docs/edit-mode.png)

## What is in it

**53 widget types**, in these groups of the Add widget picker:

| Group | Widgets |
|---|---|
| Time | Clock, Countdown, Pomodoro, Sun & Moon |
| System | CPU, Memory, Disk, GPU, Network, Ping, Battery, Processes, Volume, Mixer, Power, Bluetooth, Screen time |
| Mail & calendar | Mail, Calendar, Tasks, Contacts (Microsoft, Google, IMAP, CalDAV/CardDAV, a local task list) |
| Information | Weather, Forecast, Stocks, Feed (RSS/Atom) |
| Media | Now playing, Gallery, Video stream |
| Launch | Apps, Shortcuts, Folder, OneDrive, Search |
| Desk | Notes, Calculator, Clipboard, Scratchpad, Colour, Quote |
| Trackers | Habits, Mood |
| Layout | Group, Stack, Tabs (containers), View switcher |
| Developer | GitHub, Assigned issues, Notifications, Review requests, Claude usage |
| Themia | Help, Themia |

Every widget fills the space it is given: it shows more as you make it bigger and sheds detail
gracefully as you make it smaller.

Around the widgets: views (named layouts, each display shows one), a desktop **view bar** for switching
them, per-display scale, a snap grid, copy / cut / paste and drag between displays, undo for deletes,
a Windows 11 style settings window with search, light / dark / system theme, accent and surface
colours, corner radius, borders, fonts, nine intro animations, game mode (widgets pause under
full-screen apps), start with Windows, and export / import of all settings.

## Using it

Themia has no main window and no notification-area icon. It is wallpaper with widgets on it.

- **Ctrl+Alt+W** toggles edit mode. Double-clicking a widget does the same, and launching Themia a
  second time hands the request to the copy that is already running, which is the way back in if
  another program owns the hotkey.
- **Add widget** (edit toolbar) opens the picker. Search it by name, synonym or even a typo ("calender").
  After you pick, the widget follows the mouse; click to drop it, **Escape** or right-click to cancel.
  Paste and duplicate use the same placement.
- **Select** one widget by clicking, several with **Ctrl+click**, a rubber band on empty desktop, or
  **Ctrl+A**. Drag to move the selection as one block, drag a handle to resize (eight handles; **Alt**
  bypasses snapping), **Ctrl+C / X / V / D** and **Delete** act on all of it.
- **Right-click** a widget for its menu, or open its settings. Items inside a widget have their own menus.
- **Views:** the toolbar's View menu and the view bar add, switch, duplicate, reorder and delete views.
  In edit mode **Ctrl+1..9** shows the n-th view. Dragging a widget onto another display moves it there.
- **Settings:** the toolbar's Settings button, or a widget's menu. Type in *Find a setting* in the title
  bar to jump to any row, including a widget's. Colours are always picked, never typed.
- **Containers:** drop widgets into a Group, Stack (rolled up to their headers) or Tabs from the picker's
  "Add to" choice.
- Keys inside a widget (arrows, Enter, F2, Delete in lists, the calendar, tasks and so on) work after you
  click it; **Escape** gives the keyboard back to the app you were in.

Exit is under the toolbar's **...** menu, away from **Done**.

## Settings and data

State lives in `%APPDATA%\Themia\` (`state.json`, `accounts.json` with every secret sealed by DPAPI for
your user, notes, local task lists, habits, screen-time history, weather and network caches). Machine-local data lives in `%LOCALAPPDATA%\Themia\` (logs,
saved copies of fetched data, the licence record). **Settings > About** exports and imports the state
document and can uninstall Themia, optionally deleting both folders. Packaged installs keep these under
the package's redirected AppData and remove them with the package.

Warnings and errors are logged to `%LOCALAPPDATA%\Themia\logs\themia.log` (rotated, 14 days) and to the
Windows Application event log; secrets are redacted before anything is written.

## Distribution and licence tiers

Themia ships through the **Microsoft Store** as an MSIX package. Builds you make yourself (F5,
`dotnet build`, `scripts/publish.ps1`) are unpackaged **developer builds** and unrestricted. The Store
build is free for up to 10 widgets and 2 views; the one-time **Pro** add-on lifts both limits. Nothing
you already have is ever deleted or hidden for being over a limit. Packaging and the manual test plan
are in [docs/STORE.md](docs/STORE.md).

## Building from source

Needs the .NET 10 SDK, Windows 10 1809 or later (Windows 11 recommended), and for the AOT build the
Visual Studio Build Tools with the C++ workload.

```powershell
dotnet build src/Themia.App/Themia.App.csproj -c Release
dotnet test tests/Themia.Core.Tests/Themia.Core.Tests.csproj
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1   # single native binary, unpackaged
powershell -ExecutionPolicy Bypass -File scripts/package.ps1   # unsigned Store package
```

Debug builds also carry render flags that write PNGs of the settings window, the picker and other
shell parts without touching the desktop, and `--add-showcase-views`, which puts every widget on the
desktop at seven fixed sizes. They are the way UI changes are checked, and are listed in
[CLAUDE.md](CLAUDE.md). Google sign-in needs a client secret at
build time (`THEMIA_GOOGLE_CLIENT_SECRET`); a build without it refuses Google sign-in up front.

```
src/Themia.Core/      pure logic, no UI and no Windows dependency; all tests run against it
src/Themia.App/       WinUI 3 shell: Interop (Win32), Services (host, sync, samplers), Shell, Widgets
src/Themia.Localization.Generator/   build-time string tables (nine languages)
tests/                xUnit over Themia.Core
docs/                 Store release notes and screenshots
```

The behaviour is pinned in [SPEC.md](SPEC.md), one tagged `[S-NN]` rule at a time; tests carry the same tags.

### Three ideas worth knowing

**Geometry is fractional.** A widget stores `x, y, w, h` as fractions of its monitor, never pixels, so a
layout opens proportionally on any screen. The snap grid is an aid during a drag, never storage.

**One window per display, not per widget.** Every widget on a screen shares one transparent surface,
clipped with `SetWindowRgn` to the union of the widget rects, so clicks anywhere else reach the desktop.
Cost stays flat as you add widgets: one window, one composition target and one render thread per display.

**It is a desktop layer, always.** The surface is never above another application, not even in edit
mode. The window procedure is subclassed and every z-order change is rewritten back to the desktop
anchor. The consequence is deliberate: **you can only edit a widget where the desktop is visible.**
Show Desktop is the one moment the surface is lifted, so the widgets do not vanish when you ask to see them.

## Deliberate limitations

- **Mixed-DPI multi-monitor is untested.** Placement uses each monitor's physical bounds and layout the
  window's own scale, so it should hold, but every test machine so far ran its displays at one scale.
- **Interface text is mostly English.** The string layer, the nine translation tables (en, de, es, fr, it,
  ja, pl, pt, zh) and the language picker are in; replacing the remaining literals is the open step.
- **Time zones use a mapping table.** The app runs without ICU (`InvariantGlobalization`), so IANA city
  names are mapped to Windows zones from a frozen table; a zone Windows lacks falls back to local time.
- **Google sign-in** works only in builds made with Google's client secret (Microsoft, GitHub and Claude need none).
- **No browser engine.** The Video stream widget plays MJPEG, snapshots, MP4 and HLS through Windows'
  own media stack.
- **Out of scope on purpose:** a tray icon, an always-on-top pin, pop-out widget windows, an in-app
  updater (the Store updates it), telemetry, and importing data from the Tauri version.
- **Desktop only.** Widgets exist on the desktop layer; there is no taskbar flyout or lock-screen widget.

## Contributing and licence

See [CONTRIBUTING.md](CONTRIBUTING.md) first. Themia's source is public under the
[Elastic License 2.0](LICENSE): you may read, build, change and share it, but not remove or bypass the
licence check or the features it unlocks, remove the notices, or offer Themia as a hosted service. That
makes it source available, not open source; the LICENSE file is the binding text. The name and logo are
not covered by the licence, so give a modified build its own name and icon. Third-party material is
listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Copyright (c) 2026 Lometics GmbH.
