# Contributing to Themia

Thanks for helping make Themia better. Every contribution, from a bug report to a pull request,
earns a free Themia Pro licence: mention it in your issue or pull request, or email
support@themia.app with a link.

## Issues

Use the issue forms for bug reports, feature requests and widget ideas. Include your Themia and
Windows versions and what you did right before the problem. For a bug, the log file
(`%LOCALAPPDATA%\Themia\logs\themia.log`) is the best attachment; read it before you paste it.

Report security problems privately to themia@nathaniel-walser.com, not in a public issue.

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
build time (`THEMIA_CLIENT_SECRETS`, `google=<secret>`); a build without it refuses Google sign-in up front.

```
src/Themia.Core/      pure logic, no UI and no Windows dependency; all tests run against it
src/Themia.App/       WinUI 3 shell: Interop (Win32), Services (host, sync, samplers), Shell, Widgets
src/Themia.Localization.Generator/   build-time string tables (30 languages)
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
- **Time zones use a mapping table.** The app runs without ICU (`InvariantGlobalization`), so IANA city
  names are mapped to Windows zones from a frozen table; a zone Windows lacks falls back to local time.
- **Google sign-in** works only in builds made with Google's client secret (Microsoft needs none).
- **No browser engine.** The Video stream widget plays MJPEG, snapshots, MP4 and HLS through Windows'
  own media stack.
- **Out of scope on purpose:** a tray icon, an always-on-top pin, pop-out widget windows, an in-app
  updater (the Store updates it), telemetry, and importing data from the Tauri version.
- **Desktop only.** Widgets exist on the desktop layer; there is no taskbar flyout or lock-screen widget.

## Settings, data and logs

State lives in `%APPDATA%\Themia\` (`state.json`, `accounts.json` with every secret sealed by DPAPI for
your user, notes, local task lists, habits, screen-time history, weather and network caches). Machine-local data lives in `%LOCALAPPDATA%\Themia\` (logs,
saved copies of fetched data, the licence record). **Settings > About** exports and imports the state
document and can uninstall Themia, optionally deleting both folders. Packaged installs keep these under
the package's redirected AppData and remove them with the package.

Warnings and errors are logged to `%LOCALAPPDATA%\Themia\logs\themia.log` (rotated, 14 days) and to the
Windows Application event log; secrets are redacted before anything is written.

Builds you make yourself (F5, `dotnet build`, `scripts/publish.ps1`) are unpackaged developer builds
and unrestricted. The Store build is free for up to 20 widgets and 2 views; the one-time Pro add-on
lifts both limits. Packaging and the manual test plan are in [docs/STORE.md](docs/STORE.md).

## Pull requests

- Open an issue first for anything larger than a small fix, so the approach is agreed before you build it.
- Read the Engineering principles in [CLAUDE.md](CLAUDE.md). They apply to every change: no swallowed
  exceptions, validation at trust boundaries, atomic single-writer persistence, handles freed on every
  path, design tokens instead of literals, Native AOT rules, one implementation of anything.
- Behaviour is pinned in [SPEC.md](SPEC.md) with `[S-NN]` tags. Keep the spec and its tests in step
  with your change, and name tests after the tags.
- Build and test before you open the pull request:
  `dotnet build src/Themia.App/Themia.App.csproj -c Debug` (warnings are errors) and
  `dotnet test tests/Themia.Core.Tests/Themia.Core.Tests.csproj`.
- For UI changes, render the result with the Debug `--editbar-png`, `--widget-png` (and other) render flags described
  in CLAUDE.md and attach the PNG. Widgets must hold up at every size tier of the showcase views
  (`--add-showcase-views`), down to 120 by 120; attach a screenshot of the widget's showcase view.
- Put logic in `Themia.Core` and test it. A bug fix comes with a regression test that fails without the fix.
- Keep refactors and behaviour changes in separate commits.
- The app is unreleased, so there is no migration or legacy-format code: change the schema in place.

## Licensing of contributions

Themia is licensed under the [Elastic License 2.0](LICENSE). By submitting a contribution, you agree that:

1. you wrote it, or otherwise have the right to submit it under these terms;
2. it is licensed under the Elastic License 2.0; and
3. Lometics GmbH may also use, license and relicense it under other terms, including commercial ones.

If you can't agree to this, open an issue describing the change instead.
