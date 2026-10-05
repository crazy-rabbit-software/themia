# Themia for WinUI: SPEC

Pins the behaviour the app implements. Tags are `[S-NN]`, and where a rule is carried
over from the original Themia (the Tauri app on `main`) the tag number is kept the same so the two can be
read side by side. Tags are never renumbered; a retired rule stays in place marked *(Retired.)*.

Mirror these tags in test names (e.g. `S09_clamps_a_widget_fully_on_screen`).

Owning files: `Themia.Core/Geometry/*`, `Themia.Core/Model/*`, `Themia.Core/Storage/*`,
`Themia.App/Shell/*`, `Themia.App/Interop/*`.

---

## Index

Sections, grouped by area (file order below; search for the heading).

- **Foundations.** Geometry model; Rendering; Grid snap aid; Constraints; Defaults & placement; Views & displays; Window layering; Transparency; Ticking; Persistence; Failure rules; Clipboard & transfer between displays; No backwards compatibility.
- **Shell, windows and settings.** Displays (WP2); Desktop integration and suspend (WP3); Appearance engine (WP1); Notices, resilience and undo (WP4); Widget picker: search and adding into containers (WP5); Multi-select and the placement session (MULTISELECT-PLACEMENT); Context menus (R4-MENUS); Views and the view bar (WP8); The Displays and Views pages (R4-NAV, R5-PAGES); Icon picker and showcase views (R4-ICONS); Keyboard lease (WP11); Containers; Containers and shell hooks (PA); Shell skeleton and shared settings; Settings window: Windows 11 Settings; Settings navigation icons (NAV-ICONS); Logging (WP7); Network safety (WP6); Localization infrastructure (LOC-INFRA); Microsoft Store package and licensing; Gallery.
- **Look and layout.** Rows and the card edge; Looking like Windows; The widget header; Responsive layout (shared foundation); Adding a widget; Layout pass over the ported widgets; Widget polish 1 (POLISH-1); Widget polish 2: leftover parity and layout gaps (POLISH-2); The Set up button (R5-SETUP).
- **Accounts and sign-in.** Where account data comes from; Accounts and sign-in.
- **System widgets.** System meters; Meter framework and memory; CPU and processes; Disk and ping; Battery and network; Processes, network and ping; System widgets: GPU, mixer and screen time; GPU and screen time: the whole tile (SYS-P4); Mixer and Volume (SYS-P5); Power and Bluetooth; Device and location (DEVICE-AND-LOCATION).
- **Time, desk and launch widgets.** Clock; Countdown and pomodoro; Sun and moon; Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ); Music and Pomodoro: parity and the whole tile (PF); Notes and calculator; Notes: note store and markdown editor; Calculator keypad and colour swatch modes (PK); Clipboard, Countdown and Mood: the whole tile; Habits; Apps, Search, Help and Themia: parity and the whole tile (PI-A); Shortcuts A: display and items (PC); Folder: the file browser (FOLDER); View switcher: item menu, keys and the whole tile (PI-B); App widgets and About.
- **Mail, calendar, tasks and online widgets.** Mail; Calendar and contacts; Tasks and OneDrive; Mail and OneDrive: the whole tile; Calendar and contacts: the whole tile; Tasks and Claude usage: using the whole tile, and the rest of the original; Developer widgets: GitHub and Claude usage; GitHub widgets: filling the tile, detail, saved copies; Weather: current conditions and forecast; Feed, stocks and the web widgets: parity and the whole tile (OFF-E); Web widgets: video stream; Web widgets: size ladder and overlay.

---

## Geometry model

- **[S-01]** A widget's position and size are stored as **fractions of its screen**: `x, y ∈ [0, 1]`
  is the top-left corner, `w, h ∈ (0, 1]` the size. `x`/`w` are fractions of screen **width**,
  `y`/`h` of screen **height** — independent axes, so aspect distortion across screens is accepted,
  not corrected. "The screen" is the **whole monitor**, taskbar included, never the work area
  ([S-840]).
- **[S-02]** Geometry is resolution-independent: the same layout opened on a larger screen renders
  every widget proportionally larger in both position and size. No stored pixel geometry, no stored
  grid.
- **[S-03]** The persisted document is `{ schemaVersion, views, activeViewId, monitorViews, design }`.
  A view is `{ id, name, widgets }`.

## Rendering

- **[S-04]** Each widget renders at `left = x·W`, `top = y·H`, inset on every side by
  `widgetGap / 2`, so neighbours show a gap without changing their logical rect. Logical edges are
  rounded to a shared whole-pixel boundary *before* the gap inset, so adjacent widgets still meet
  exactly.
- **[S-05]** The DIP space a fraction resolves into is the window's **real client rect divided by
  the window's real rasterization scale** — not `ActualWidth`. Changing display scale resizes the
  content without changing the client's pixel size, so no `SizeChanged` fires and `ActualWidth` can
  hold a stale pre-DPI value; trusting it rendered every widget 1.5x too far out on a 150% display.
- **[S-32]** A widget instance can drop its card: `WidgetSurface.Transparent` removes the fill and
  the border. `Translucent` keeps a tinted, partly see-through card. Neither is acrylic per widget —
  acrylic is a window backdrop, so per-widget acrylic would cost one window per widget.

## Grid snap aid

- **[S-06]** The grid is an **aid**, never storage. `design.gridSnap` (default on) rounds the rect
  chosen during drag, resize and placement to the nearest grid line. Holding **Alt** suspends it for
  the duration of the gesture. Storage is always the raw fraction.
- **[S-06b]** The cell target is **10 DIP**, and the minimum widget **32 DIP**, matching the
  original app. This only sets how many snap lines exist; the drawn pitch is screen/cols, so it
  follows the display. At that density the overlay stroke has to be very faint, or the mesh reads as
  a grey wash over the desktop rather than as a grid.
- **[S-06a]** The cell overlay is drawn only in edit mode, and only when `gridSnap` is on.

## Constraints

- **[S-08]** Minimum widget size is a **pixel floor** (`MinWidgetPx`, 64 DIP), converted to a
  fractional floor at the moment of resize, so it means the same physical size on every monitor.
- **[S-09]** Widgets are clamped fully on-screen (`0 ≤ x`, `0 ≤ y`, `x + w ≤ 1`, `y + h ≤ 1`) at
  load, and into the display's **work area** for drag, resize, placement, paste and drop ([S-841]).
  Clamping is **total**: a non-finite or inverted rect collapses to
  a valid one rather than propagating NaN into storage.
- **[S-31]** Edit mode draws **eight** handles: four corners and four sides. A corner drags one edge
  on each axis; a side handle drags **only its own edge**, leaving the opposite edge and the whole
  other axis untouched. An edge dragged past its opposite stops at the [S-08] floor instead of
  inverting the rect.
- **[S-31a]** Settling a resize snaps and clamps **only the edges the handle moves**: a moved edge
  rounds to the grid and stops at the work-area edge; an anchored edge never moves. Snapping the whole
  rect rounded the anchored edge too, and clamping the whole rect pushed a widget whose bottom edge
  was dragged into the taskbar upwards instead of stopping the edge at it.

## Defaults & placement

- **[S-10]** A widget definition declares its default size in **pixels**. At placement that is
  converted to a fraction of the current screen, so a newly dropped widget has a consistent physical
  size on any monitor, and scales per [S-02] thereafter.
- **[S-11]** Placement centres the widget on the drop point, then snaps ([S-06]) and clamps ([S-09])
  into the work area, so a widget dropped on the taskbar lands just above it.

## Views & displays

- **[S-12]** Views are switched as a whole. Each holds its own widget list. The last view cannot be
  deleted — a desktop with no view has nowhere to put a widget.
- **[S-13]** The per-widget settings page edits the widget's **live** config and saves as each field
  is committed — there is no Cancel. It does not mirror the config into local state and sync it
  back. Live, not a copy, because several widgets write their own config as they are used (a note's
  text, a habit's check-offs): a settings page holding an older copy would overwrite those on save.
- **[S-13a]** Because the page edits the object a view already holds, a view must never decide what
  changed by comparing that object with itself. A view that reacts to particular settings keeps its
  own snapshot of the settings it last acted on and compares against that. Comparing the live object
  was the bug: a new feed address, stock symbol, weather place or folder sort stayed unfetched until
  the next scheduled refresh.
- **[S-18]** `monitorViews` maps a display's **stable id** ([S-842]) to a view id; a display with no entry shows
  the active view until the display pass gives it one ([S-1705]). An entry pointing at a deleted view
  is dropped at load, not resolved.
- **[S-19]** Showing a view puts it on **one display**: that display's own assignment changes, and
  the active view follows so a display plugged in later starts on the last view chosen. Switching
  only the active view is not enough and was the bug: every display gets an assignment at startup
  ([S-18]) and an assignment always wins, so the View menu changed nothing on screen.

## Window layering

- **[S-20]** The widget surface is **never above another application**, in any mode. It is a desktop
  layer: widgets belong under the user's windows, and editing them does not change that. Outside
  edit mode it also carries `WS_EX_NOACTIVATE`, so clicking a widget neither focuses it nor raises it
  over the user's work. `WS_EX_TOOLWINDOW` keeps it out of the taskbar and Alt-Tab. The one exception
  is [S-33], where there is no other application on screen to be above.
- **[S-20a]** Edit mode drops `WS_EX_NOACTIVATE` so the surface can take keyboard focus for the
  shortcuts ([S-70]) — and an activatable window is raised when clicked, so the window procedure is
  subclassed and every `WM_WINDOWPOSCHANGING` is rewritten back to the anchor. Event-driven, so there
  is no timer re-asserting the z-order and no window in which the surface is visibly on top.
  - A consequence the user chose: **edit mode is only reachable where the desktop is visible.** If
    another window covers a widget, that widget cannot be clicked.
- **[S-20b]** The frame is stripped at the **Win32** level (`WS_POPUP`, clearing `WS_CAPTION` /
  `WS_THICKFRAME` / `WS_SYSMENU`), and re-applied whenever the styles are touched.
  `OverlappedPresenter.SetBorderAndTitleBar(false, false)` does **not** clear `WS_CAPTION`: the
  surface kept a caption and a 3px border the whole time, which the click-through region happened to
  clip away, so the title bar appeared the moment edit mode dropped the region.
- **[S-21]** The surface is clipped with `SetWindowRgn` to the union of the widget rects, so every
  click that is not on a widget lands on the desktop underneath. The region is rebuilt on every
  layout pass, in **physical** pixels, relative to the **window** — client and window coordinates
  differ by the frame, so the client origin is added or the whole mask sits shifted from the widgets
  it traces. It is always cut to the work area ([S-851]).
- **[S-22]** Only a genuine style change passes `SWP_FRAMECHANGED`. Sending it on an ordinary
  z-order reassert forces a non-client recalculation, which hands the borderless window its frame
  back.
- **[S-23]** The window is placed with `AppWindow.MoveAndResize` over the monitor's **physical**
  bounds — the whole monitor, not the work area ([S-840]). Display scale only applies inside the window, where XAML lays out in DIPs ([S-05]).
- **[S-23a]** The resting anchor is the **desktop window** (`Progman`), not `HWND_BOTTOM`: the
  surface is inserted against it so it travels with it, and falls back to the bottom only when
  Explorer is not up yet.
- **[S-33]** While Windows is genuinely showing the desktop, the surface is lifted to the top of the
  topmost band and dropped back the moment it is not. Show Desktop raises the desktop window *past*
  normal z-order, above anything anchored below it, so the widgets would otherwise vanish at the one
  moment the user asked to see their desktop. There is nothing else on screen for them to cover
  then, so this does not contradict [S-20]; it is the only way to honour it and still be visible.
- **[S-33a]** "Genuinely" is two signals: the desktop layer owns the foreground, **and** no ordinary
  application window is still on screen. Foreground alone is also true of a plain click on the
  wallpaper, and acting on it would throw the widgets in front of the user's work every time they
  clicked the desktop. There is deliberately no third "and something got hidden" signal: it would
  only matter when nothing was on screen to begin with, where lifting the widgets covers nothing,
  and both candidate signals measured unreliable — Show Desktop **minimises** on this build rather
  than cloaking, and the cloak count is permanently non-zero anyway because suspended UWP frames sit
  cloaked in the background all the time.
- **[S-33b]** A **topmost** window does not count as an application still on screen. Show Desktop
  cannot hide one, so its presence says nothing about whether Show Desktop is in effect. This is not
  hypothetical: a consent prompt from another program is exactly such a window, and on its own it kept
  the lift from ever firing.
- **[S-33c]** A transient shell surface (Start, Search, Task View, the taskbar) taking the foreground
  while the lift is held changes **nothing** — not even a re-raise. Dropping the lift there would
  bury the widgets under the still-elevated desktop layer, and dismissing Start hands the foreground
  to the taskbar rather than to the desktop, so they would never come back. Not re-raising is what
  lets Start, activated after us in the same band, stay on top. The decision is a pure function
  (`ZOrderPolicy.Decide`) so it can be tested without a window manager.
- **[S-33d]** `SetWindowPos(HWND_TOPMOST)` often reports success without `WS_EX_TOPMOST` actually
  taking, and a window that only thinks it is topmost stays under the elevated desktop layer. The
  lift therefore checks the bit and, when it is missing, sets it directly and re-applies the position
  with `SWP_FRAMECHANGED`.
  - Verified on two displays: during a real Show Desktop both surfaces read topmost and above the
    desktop window, and both return to the desktop layer when it ends. The original app documents a
    wry/WebView2 limitation where only the first window in a process can be promoted; that does not
    apply here.
- **[S-34]** The surface is kept **uncloaked**. A fullscreen exclusive application hides windows by
  asking DWM to cloak them — a path that goes through neither `SC_MINIMIZE` nor `WM_SHOWWINDOW`, so
  no window procedure sees it. A cloaked window is still `IsWindowVisible`, still not iconic, still
  at whatever z-order it was put at, and simply never composited: no amount of z-order work recovers
  it. `DWMWA_CLOAK` is cleared on every reconcile. Note this is **not** what fixes Show Desktop on
  this build of Windows, which minimises instead ([S-33a]); it is the guard for the fullscreen case.
- **[S-34a]** The cloak hook has a backstop behind it. An out-of-context `WinEvent` is silently
  dropped when the callback cannot be delivered inside the hook timeout — a fullscreen game is
  exactly that load — and a dropped cloak event would leave the surface invisible forever. A 1 s
  timer re-asserts the invariant: one attribute read per surface, a write only when it is broken.
- **[S-34b]** After any shell event the reconcile runs at 100 ms for a second before settling back to
  1 s. A secondary monitor's `WorkerW` can elevate into the topmost band *after* the surfaces are
  lifted, so the lift has to be re-asserted a few times to stick.
- **[S-28]** While the surface is pinned for Show Desktop, a **widget surface taking the foreground
  holds the pin**, exactly as Start or the taskbar does. Clicking a widget makes the surface the
  foreground, so the desktop no longer owns it and Show Desktop stops reading as active — but
  nothing has been restored and the desktop is still elevated. Dropping the pin there buried the
  widgets under it on the first click after Win+D.

## Transparency

- **[S-27]** The surface is genuinely transparent: no backdrop that paints, so the wallpaper (and
  whatever else is behind) shows everywhere the widgets do not.
  - The SDK ships only Mica and Desktop Acrylic, and a window with `SystemBackdrop = null` composes
    **opaque black** — which over a whole monitor is a sheet across the desktop. So a custom
    `SystemBackdrop` installs a transparent composition brush instead.
  - The brush must come from a **system** compositor: the projection types
    `ICompositionSupportsSystemBackdrop.SystemBackdrop` as `Windows.UI.Composition.CompositionBrush`,
    not the `Microsoft.UI.Composition` one.
  - That compositor needs a **system** `DispatcherQueue` on the UI thread, which is a different type
    from the `Microsoft.UI.Dispatching` one WinUI already provides. Without it, constructing the
    compositor fails with "The caller must initialize DispatcherQueue on this thread".
- **[S-27a]** Because the window paints nothing, a widget card must paint its **own** base. Fluent's
  `CardBackgroundFillColor` is a ~5% white overlay in dark theme, designed to sit on an opaque
  window; used alone it made the card invisible and left the clock unreadable over whatever was
  behind. `SolidBackgroundFillColorBase` is the surface that overlay expects, so it is the base here.
  The same applies to the snap grid, which uses a text brush rather than a control stroke.
- **[S-27b]** Fill and content are separate elements, so the surface-opacity setting fades the card
  without fading the text on it.
- **[S-27c]** The opaque base is painted **under** the card stroke, not only inside it. The stroke
  is a faint overlay; with the base stopping at its inner edge, every card had a nearly clear outer
  ring, and two cards at a 0px gap showed wallpaper between them.

## Ticking

- **[S-24]** Every widget shares **one** timer. Its cadence is the finest any subscriber asked for;
  it drops to once a minute when nothing needs seconds, and stops entirely when nothing is
  subscribed or the surface is hidden.
- **[S-25]** Each wake is re-aligned to the next real boundary plus a small bias, rather than using a
  repeating timer. Waking exactly on the boundary rounds down about half the time, showing the
  previous second for a whole tick; a late wake must not push the schedule permanently off.
- **[S-26]** A subscriber fires when its period index changes, so a once-a-minute widget sharing a
  one-second timer still only runs on the minute.

## Persistence

- **[S-40]** Writes are **atomic**: serialise to a sibling temp file, then move over the target, so a
  crash mid-write cannot leave a truncated document.
- **[S-41]** There is a **single writer**, and saves are coalesced — a drag that mutates geometry
  sixty times a second produces one write.
- **[S-42]** `FlushAsync` takes the write gate *before* reading the pending snapshot, so a flush that
  finds nothing queued still waits for an in-flight write to land. Reading the snapshot first let a
  shutdown flush return while a debounced save was mid-write, i.e. "flushed" without durability.
- **[S-43]** Load distinguishes **absent** from **corrupt**. A missing file is a fresh profile. A
  file that is empty, unparseable, or from a newer `schemaVersion` is corrupt: defaults are returned,
  loudly, and the original is **moved aside, never deleted**.
- **[S-44]** A loaded document is repaired back inside its invariants rather than rejected — dangling
  ids, duplicate ids, config that does not match its widget kind, out-of-range design values and
  off-screen geometry are all fixed, and every repair is reported.
- **[S-45]** Closed sets are enums, not strings (`WidgetKind`, `WidgetSurface`, `AppTheme`,
  `ClockFace`). Widget config is a discriminated union with the discriminator written into the JSON,
  so a config can never be silently read as the wrong widget's shape. An unknown widget kind is
  dropped with a report, because this build cannot render it at all.

## Clock

- **[S-50]** 24-hour time is zero-padded; 12-hour time is not, and carries AM/PM. Midnight and noon
  read as **12**, not 0.
- **[S-51]** Hands sweep rather than step: the hour hand advances with the minutes, the minute hand
  with the seconds.
- **[S-52]** A clock without seconds (and with a digital face) asks for a **1-minute** cadence. An
  analog face always needs seconds, because its second hand sweeps.
- **[S-53]** An unknown or malformed time-zone id degrades to the local zone rather than throwing —
  this runs on every tick.
- **[S-54]** Formatting is done explicitly, not with culture patterns: the app builds with
  `InvariantGlobalization`, so culture format strings would not honour the config.

## Failure rules

- **[S-60]** No Win32 callback may let a managed exception unwind into native code. The command
  window's `WndProc` and the monitor enumeration callback each wrap their whole body and fall
  through.
- **[S-61]** Every GDI handle is freed on all paths. `SetWindowRgn` takes ownership of the region on
  success, so the handle is deleted only while still owned and on every failure path.
- **[S-62]** No error is silent. Every catch either handles, logs, or is annotated with why ignoring
  it is correct.
- **[S-63]** One failing display, widget or tick subscriber must not cost the user the others:
  surface creation, tick dispatch and log sinks all isolate per-item failures.
- **[S-64]** Themia puts **nothing** in the notification area. It is wallpaper with widgets on it,
  not a program the user runs, and a permanent tray entry says otherwise. That removes the menu the
  app used to hang everything off, so what it was the only route to has to live somewhere else:
  edit mode on a global hotkey, and everything that menu offered on the edit toolbar.
- **[S-65]** **Ctrl+Alt+W** toggles edit mode, registered `MOD_NOREPEAT` so a held key does not
  toggle it dozens of times a second. Another program may already own the combination; that is
  logged as an error and leaves a working app, because double-clicking a widget and launching
  Themia again both still reach edit mode. The hotkey is unregistered on shutdown — one that
  outlives its window is a combination nothing can claim until the session ends, including the copy
  the user starts next.
- **[S-66]** Launching Themia while it is running **activates the running copy** instead of starting
  a second. Two processes drawing two widget layers over one wallpaper is indistinguishable from
  one, and with no tray entry there is nothing left to tell them apart by. The running copy is found
  by its command window's class name, which must therefore stay stable across versions.
- **[S-67]** Exit is offered in one place, the edit toolbar's overflow, and not beside **Done**: it
  is the only toolbar action that cannot be undone and **Done** is the one next to it the user
  presses constantly.

## Clipboard & transfer between displays

- **[S-70]** In edit mode the surface handles **Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+D / Ctrl+A**,
  **Delete** and **Escape**, each acting on the whole selection ([S-1592]). The cards are focusable,
  because with nothing focusable in the tree no `KeyDown` is raised at all and the shortcuts never
  fire. Clicking empty surface also takes focus, so the shortcuts keep working after clicking away
  from a widget. Escape backs out one step: placing, then the selection, then edit mode ([S-1583]).
- **[S-71]** The clipboard is **in-process**, not the system clipboard: the payload is a live widget
  configuration, and putting it on the system clipboard would clobber the user's actual clipboard.
- **[S-72]** A clipboard entry is a **snapshot** of the selection — every selected widget, a
  container with everything inside it ([S-876], [S-1588]) — with configs and relative layout. It
  survives the originals being moved, reconfigured or deleted.
- **[S-73]** A **cut is a mark**, not an immediate removal: the originals stay until a paste
  **lands**, so changing your mind — or cancelling the paste's placement — costs nothing. A cut is
  consumed by the one paste that lands (landing it twice would mean moving the same widgets to two
  places); a copy stays available for repeat pastes.
- **[S-74]** A paste never drops widgets at a computed spot: it starts a placement ([S-1587]) on the
  display under the pointer, and the clipboard's widgets follow the pointer as one block, centred on
  it, snapped and clamped as a block ([S-1584]) until a click lands them in the view of the display
  clicked on.
- **[S-75]** A landing removes what it moves and adds what it places in **one** mutation, so a cut
  across views can neither duplicate a widget nor lose it, and pasting a cut back into its own view
  leaves exactly one of each. A pasted cut is a **move**: the widgets keep their ids ([S-1590]).
- **[S-76]** A drag that ends with the pointer over another display hands **every dragged widget**
  to that display's surface, keeping their identity ([S-1591]). Only when the destination shows a
  **different** view — when both show the same view the widgets are already on both, so moving them
  would be a no-op that rewrote their positions against the wrong screen.
- **[S-77]** The drop **point**, not the dragged rects, decides the destination: those are clamped to
  their own screen ([S-09]) and so can never reach across the display boundary. The held widget's
  centre goes to the point, the others keep their offsets to it, and the block is clamped into the
  destination's work area as a whole: a point over the taskbar counts as that display and the block
  lands just above it ([S-841]).
- **[S-78]** While widgets are dragged, a **ghost** shows where they would land — one outline per
  widget — on the display under the pointer, only when that display would really receive them
  ([S-76]), with the cards on the screen they are leaving faded. Only a move hands widgets across; a
  resize that ends over another screen is still a resize.

## System meters

- **[S-80]** CPU, memory, disk and battery read straight from Win32 (`GetSystemTimes`,
  `GlobalMemoryStatusEx`, `GetDiskFreeSpaceEx`, `GetSystemPowerStatus`), not from
  `PerformanceCounter`: that pulls in the whole perf-counter stack, is slow to its first value and is
  not trim-safe, none of which a widget sampling one number a second can justify.
- **[S-81]** CPU load is a **delta** between two samples of the system's cumulative timers, so the
  first tick has nothing to subtract and reports 0 rather than inventing a figure. `GetSystemTimes`
  already counts idle inside its kernel total, so busy is `(kernel + user) - idle`.
- **[S-82]** A meter that cannot be read says so (`--`, dimmed) rather than showing 0%. A machine
  with no battery reports 255 for the charge, which is "unknown", not "empty".
- **[S-82a]** "Not present on this machine" and "present but unreadable right now" are different
  states and look different: the first is a proper empty state, the second a dimmed `--`. A desktop
  with no battery is not a battery meter that failed. The reading carries a flag saying which;
  matching on the caption text, as a first attempt did, made a display decision depend on the exact
  wording of a string in another file.
- **[S-83]** *(Superseded by S-1120/S-1126: the cadence is a setting with per-kind defaults, and
  the battery draws its history.)* Cadence follows what the number actually does: CPU and memory once a second, disk and
  battery once a minute. Battery draws no history - at a one-minute cadence a flat line reads as a
  broken graph rather than a stable charge.
- **[S-84]** Byte sizes use **binary** units, because that is what Windows reports; a widget
  disagreeing with File Explorer about the size of the same drive reads as a bug. A percentage never
  rounds a non-zero load down to 0% nor an incomplete one up to 100%, because both read as "nothing
  is happening" when something is.

## Where account data comes from

- **[S-89]** Mail, calendar, contacts, tasks, OneDrive, GitHub and Claude usage are synced by Themia
  itself, from accounts it signs in to and stores on this PC ([S-500]..[S-539]). There is no external
  hub. The earlier design, which read them from a separate hub program ([S-90]..[S-99], [S-180]..[S-187]), is retired: a widget
  that depended on another program running and granting consent showed nothing whenever it was not,
  and the user wanted Themia to own its data. Nothing of that build's data is read ([S-1050]).

## Processes, network and ping

- **[S-100]** Process CPU comes from one `NtQuerySystemInformation(SystemProcessInformation)`
  snapshot, not from per-process handles. Windows refuses a handle to anything elevated, protected
  or in another session — about a third of an ordinary desktop, including Defender, Search and the
  service hosts — so a handle-based list structurally cannot contain the actual CPU hogs. The handle
  path survives as the documented degradation when the call fails, and either way a row whose value
  is genuinely unknown says `--` rather than `0%`.
- **[S-101]** The snapshot is parsed defensively: every entry offset, thread-array span and chain
  link is bounds-checked against the buffer, and a chain that does not parse stops the walk rather
  than reading arbitrary memory as a process. A layout change therefore degrades to the fallback
  instead of misreporting.
- **[S-102]** The snapshot buffer is pinned and reused. The kernel writes image names *into* it and
  hands back pointers into it, so a moving collector would invalidate the names mid-read, and
  reallocating it per tick would be the most expensive thing the widget does.
- **[S-103]** An image name is materialised once per process, not once per sample: a pid whose start
  time still matches is the same process with the same name.
- **[S-104]** Process identity is (pid, start time as `FILETIME`) on both paths, so switching paths
  does not look like a recycled pid.
- **[S-105]** The list fills its tile with **correctly sized rows**, not stretched ones: the row
  height is Fluent's, and the configured count is clamped to how many of them the tile can actually
  draw. A bigger tile shows more rows, never taller ones — a list whose rows grow with the window is
  a web idea, and next to Explorer it reads as the wrong application. Row visuals are rewritten in
  place; rebuilding the list each sample made it blink twice a second.
- **[S-106]** Ping is sampled asynchronously and never blocked on from the tick. One probe is in
  flight at a time; a probe that overruns its interval delays the next reading rather than stacking
  up.
- **[S-107]** A late ping completion is discarded once the view is detached or the host has changed.
  Changing the host cancels the in-flight probe, clears the sparkline and re-probes at once.
- **[S-108]** A timeout or an unreachable host is a displayed state, not an error: logged once per
  transition, never once per tick, and the reading's tooltip names the host so `--` says what failed
  ([S-1762]: the tile carries no caption).
- **[S-109]** The ping host is validated at **both** boundaries — the settings box and the probe. An
  implausible value is never persisted, and an empty box means the default host, named in the panel,
  never "probe nothing".
- **[S-111a]** A sparkline's x-axis is a fixed window of slots, not "spread whatever samples are held
  across the full width". The newest sample is pinned to the right edge and each older one steps left
  by exactly one slot, so the trace grows in from the right and reaches the left edge only when the
  window is full. Spreading them instead makes the graph rescale on every sample for the first
  minute of a widget's life, which reads as the data moving when only the buffer is filling. The
  original app carries the same note against the same bug.
- **[S-111b]** The y-axis is the full range, never the range actually observed. A meter rescaled to
  its own recent minimum and maximum turns a steady 47% into a dramatic zigzag of noise, which is the
  opposite of what a glanceable readout is for.

## Notes and calculator

- **[S-120]** The calculator's arithmetic is a hand-written recursive-descent parser in
  `Themia.Core`. The original could not use `eval` because the webview CSP blocked it; here the
  reason differs and the conclusion is the same — a Native AOT binary has no scripting engine and
  must not grow one.
- **[S-121]** Grammar: `+ - * /`, `^` right-associative, unary sign, parentheses, decimals.
  Precedence low to high: `+ -`, `* /`, unary, `^`.
- **[S-122]** Input is sanitised to `0-9 + - * / ( ) . , % ^` and space; every comma is dropped as a
  thousands separator, so `1,5` is fifteen and never one-and-a-half, and `%` becomes `/100`.
- **[S-123]** Evaluation is **total**: any input returns empty, a finite value, or an error. Nesting
  is capped at 64, because parentheses and unary signs recurse and a `StackOverflowException` cannot
  be caught — it takes the process with it.
- **[S-124]** Numbers are parsed and written invariant. The app builds with `InvariantGlobalization`,
  and a result written with a decimal comma could not be typed back into the box it came from.
- **[S-125]** Calculator history is in-memory only, newest first, capped at 8. It is never persisted:
  a scratchpad is not a record, and typed text does not belong in the state file.
- **[S-126]** The decimal-places setting applies to the live preview and to every history row already
  on screen; trailing zeros are dropped and a value that rounds to zero is never written `-0`.
- **[S-128]** The note is handed to the note pool on a 400 ms debounce and flushed on blur, on
  switching note and on detach; the debounce is a write-rate limit, not a correctness path. A change
  from elsewhere never overwrites an unflushed local edit ([S-1348]).
- **[S-129]** The editor's chrome is removed through the text control's own resource keys (the
  `RichEditBox` shares the `TextBox`'s), not its `Background`: the stock template's visual states
  assign the border element directly and outrank a local value, so the field would light up solid
  on hover.
- **[S-130]** "Clear note" confirms in a dialog. The text is unrecoverable, so one click is not
  enough, and failing to show the dialog leaves the note alone. It empties the note's body (for every
  widget showing it); the note and its title stay.
- **[S-131]** The calculator drops its history under 120 DIPs of height, so a small tile shows the
  sum being typed rather than two clipped rows above it. The header rule that used to live here is
  the card's now ([S-402]) — every widget had reached the same conclusion with its own threshold.

## Countdown and pomodoro

- **[S-140]** A countdown row is a label, the absolute local target, and the duration. A target in
  the past is not hidden: it reads "… ago" in the theme's critical colour with the label stepped
  back, because how long ago a deadline went is the thing you want to know.
- **[S-141]** `auto` picks the coarsest unit that still says something: "in a moment", "N mins",
  "Nh Nm" (or "N hrs" on the hour), "Nd Nh" (or "N days" on the day), and past a week just days.
  Under a minute in the past is "just now", never "in a moment ago". The explicit formats keep their
  zero components, because a fixed shape is what makes a column of durations readable.
- **[S-142]** The one-second cadence is bought only by a timer that actually shows seconds;
  everything else re-letters twice a minute, and a widget with no timers does not wake at all.
- **[S-143]** Rows are built when the list changes and only re-lettered on a tick. Rebuilding them
  each tick drops the row under the pointer, which at one second is every second.
- **[S-144]** A cleared, impossible or non-existent (DST-skipped) target leaves the timer pointing
  where it did. Writing "now" is what the original did, and it silently retargeted a timer mid-edit.
- **[S-150]** Pomodoro run state — phase, seconds left, running — lives in the view instance and is
  never persisted. The same widget on two monitors is two independent timers, which is also why only
  a running instance can sound the cue.
- **[S-151]** At zero the phase flips and the new phase's full duration loads, still running. One
  second remaining is the last second, not a spare one.
- **[S-152]** The cue sounds on the flip alone and announces the phase being *entered*: ascending
  into focus, descending into a break. Nothing sounds on start, pause or reset.
- **[S-153]** The cue is synthesised in Core and played through `winmm!PlaySound` from memory, not
  through `MediaPlayer` or `AudioGraph`: those are whole media pipelines the AOT compiler roots in
  full in order to play two seconds of oscillators. `CuePlayer` copies the wave to native memory and
  plays it asynchronously, single-flight: the next cue stops the previous one (`PlaySound(NULL)`)
  before its buffer is freed, so at most one buffer is ever outstanding and nothing is GC-pinned.
  (The twelve ringtones and the volume: [S-1460]..[S-1463].)
- **[S-155]** The 1 Hz subscription exists only while the timer runs, and the callback the shared
  ticker makes at subscribe time is swallowed: that immediate fire is right for a view that renders
  the time and wrong for one that *is* the tick.

## Sun and moon

- **[S-160]** The astronomy is pure functions in `Themia.Core.Astronomy` taking an explicit instant,
  working in UTC throughout. `InvariantGlobalization` means IANA ids do not resolve, so display
  converts with the machine's local zone alone and no time-zone dependency exists.
- **[S-161]** Sunrise and sunset come from the standard sunrise equation, good to a minute or two,
  with no network. Polar day and polar night return null times plus a marker, and the tile shows an
  em dash with 24h or 0h of daylight.
- **[S-162]** The day reported is the **solar** day at the configured longitude, not the UTC day, so
  sunrise and sunset belong to the local day at every longitude and in both hemispheres.
- **[S-163]** The next event is the earliest one strictly after now, scanning the solar day before,
  of and after it; null inside the polar circles. The countdown therefore never sticks at `0m`.
- **[S-164]** Moon phase and illumination come from the synodic month off a known new moon, in eight
  buckets — a linear model, within 0.02 of a cycle of the real syzygies.
- **[S-165]** The sub-solar and sub-lunar points come from instantaneous right ascension and
  declination against GMST, never the rounded day index the sunrise equation uses, which would freeze
  them for a whole day.
- **[S-166]** Sky position gives which side of the horizon a body is on plus a progress running 0 at
  rise, 0.5 at the meridian and 1 at setting, normalised by that body's own semi-diurnal arc so the
  ends land exactly on rise and set.
- **[S-167]** The drawn curve is the sun's **real** path, not a semicircle: it peaks at the true noon
  altitude, crosses zero exactly at the ends of the semi-diurnal arc, flattens in winter and towers
  in summer. The axis tops out at the highest the sun ever reaches at that latitude.
- **[S-168]** The night stretch continues the same way round the sky — setting on the right, nadir,
  rising on the left — because a body sets on one side, passes underneath and comes back up the
  other. Its depth is compressed against the daylight scale on purpose: a true-to-scale winter
  midnight would be deeper than the whole widget, and nobody reads a number off it.
- **[S-169]** One lit-limb outline draws both the corner badge and the row glyph, so the two cannot
  disagree. It is mirrored for a southern observer, keeps a hairline rim on the unlit part so a thin
  crescent still reads as a moon, sits *beside* the graphic rather than on the solar arc, and dims
  below the horizon.
- **[S-170]** The two display settings are independent, except that hiding the graphic forces the
  rows on: a widget showing nothing is never the answer. The graphic itself is never dropped — it is
  sized down to a 46px floor and keeps drawing. Only extras shed, cheapest-first by width: the
  countdown, then the label icons and the moon percentage, then the daylight label, then the number
  strip. (An earlier version of this line had the daylight label going before the icons. The code
  was right and the spec was wrong: "12h 6m" says more than two glyphs do.)
- **[S-171]** (Reason superseded by [S-1494]: the place is now picked by city search; the validation
  below still governs the exact-coordinate fields.) There is no geocoding service here and widgets make no network calls, so the settings
  take latitude, longitude and place name directly. Input that is not a number in range is
  **rejected** — the last good value stands and the row says why — never coerced to 0, which would
  silently move the user to the Gulf of Guinea.

## Rows and the card edge

- **[S-190]** The card applies **no** content padding. Each view insets its own parts, and a list row
  applies the inset as its own *padding*, so the row — and the highlight it draws under the pointer —
  reaches both edges of the widget while its text stays exactly where it was. A row inset from the
  card edge reads as a box inside a box, and a highlight that stops short of the edge reads as a
  button rather than as a row.
- **[S-190a]** A full-bleed row therefore has no corner radius and no negative margin. The negative
  margins that used to cancel the card's padding are gone rather than adjusted: something pulling
  itself back out of a padding that no longer exists would now hang over the edge.
- **[S-190b]** A list has **no separators at all**. Windows 11 separates rows with space and with
  the fill under the pointer — Settings, Explorer and the Widgets board all do — and a rule between
  every row is the single clearest tell of a web table. The one rule that survives anywhere is the
  calculator's, which divides a scrolling region from a fixed command area the way a dialog does.
- **[S-191]** Headers, footers, empty states and readouts keep the inset — they are labels, not rows.
  A day heading in the calendar is a label and lines up with the header above it.

## Looking like Windows

- **[S-210]** Type comes from the **Fluent ramp**, never from a number computed off the tile. A widget
  still scales with its box — a resizable widget has to — but it scales by choosing a step, so every
  size on screen is one Windows itself uses. The web app's `clamp(min, min(Ncqw, Mcqh), max)` is what
  made the port read as a dashboard that happened to be running here.
- **[S-210a]** A hero readout on a very large tile may scale past Display, keeping Fluent's own
  line-height ratio, because the ramp stops at 68px and a clock filling a quarter of a screen
  legitimately does not.
- **[S-211]** Colour comes from **named brushes**, never from an opacity applied to a text block.
  `Opacity = 0.65` is not a colour; it is a colour the theme cannot change, and in light theme it is
  the wrong one. The single exception is the wash under a chart line, which is a translucent *area* —
  and even there the alpha belongs to the shape, never to the shared brush every other accent surface
  paints with.
- **[S-212]** Spacing is the 4px grid. No 6px, no 7px, no 3px.
- **[S-213]** A list row is a **fixed** Fluent height, standard or compact. A bigger tile shows more
  rows, never taller ones.
- **[S-214]** A card is a tint **on** something, not a flat fill: the opaque surface colour Fluent
  expects, with the real `CardBackgroundFillColor` overlay on top. That overlay is the lift that makes
  a Windows card look raised, and on its own — over a transparent window — it is nearly invisible.
- **[S-215]** *Superseded by [S-1824] for the meters:* a meter's chart sits inside the card's
  padding. Bled to the edges, its newest point and its fill were cut by the card's edge and rounded
  corner.
- **[S-215b]** The graph and the bar share one grid row rather than taking one each. They are
  alternatives — one shows exactly when the other does not — and as separate rows the grid's row
  spacing still sat between them, leaving a gap under a chart that is supposed to touch the
  bottom of the card. A collapsed row has no height; it still has a gap.
- **[S-215a]** The chart is a band of bounded height at the foot of the tile, and the **reading**
  takes the slack. Giving the graph the slack put a 350px band on a tall tile that a young trace had
  no way to fill, with the number stranded at the top.
- **[S-216]** A meter whose history moves slower than its window can fill draws no graph by default.
  Free space sampled once a minute is a bare sliver for most of an hour and a flat line for the rest;
  the bar says the same thing immediately. The setting stays, for a drive that really is filling up.

## The widget header

- **[S-400]** The header belongs to the **card**, not to the widget. One builder means one look; the
  alternative was thirty-odd widgets each with their own chance to disagree about its margin, its
  shedding threshold and its title — and nowhere to put a setting like "draw it at the bottom" that
  every one of them would have had to honour.
- **[S-400a]** The card knows the icon (the catalogue has it) and the title (the instance has it).
  The only thing it cannot know is what goes on the right — a badge, a count, a busy ring — so that
  is the whole of what a view contributes, through `IWidgetHeaderContent`. It is read **once**, when
  the header is built, so the element has to exist by then.
- **[S-401]** Header placement is per widget: top, bottom, or hidden. Switching it moves a row index,
  not a rebuild, so it costs nothing and cannot restart what the widget is doing.
- **[S-402]** The header sheds on a tile too short to carry it, **once**, in the card. Every widget
  had independently drawn that conclusion with a slightly different threshold.
- **[S-403]** The title is the user's own when they set one, otherwise the catalogue's. A widget has
  no say in it: a view that wants to label itself differently is a view that has drifted.

## Containers

- **[S-410]** Group, Stack and Tabs hold other widgets. Membership is a `ParentId` on the child, never
  a list of children on the container, so a widget cannot be in two containers at once and a
  container cannot name a child that has been deleted.
- **[S-411]** A **group** does not lay its children out. They keep the place on the grid the user
  chose and the group draws behind them, which is why dropping a widget into a group does not move
  it. A **stack** and a **tab strip** own their children's geometry outright.
- **[S-411a]** Neither ever writes back to a child's stored rect, so a widget taken out of one
  returns to exactly the place it had before it went in.
- **[S-412]** A container does not create its children — the surface does, exactly as it creates
  every other card. A widget inside a stack is the same object with the same lifetime as one on the
  desktop, so nothing has to be special-cased to keep it ticking, and nothing restarts when it moves.
- **[S-413]** A group collapses by **hiding** its children, never by rearranging them, so expanding
  puts every one back where it was.
- **[S-414]** A closed stack row and an unselected tab **collapse** their child rather than merely
  covering it: a widget nobody can see should not be measuring itself on every layout pass.
- **[S-415]** (Now by paint order, [S-874].) A group is moved to the back of the widget layer, because the layer paints in child
  order and a group added after its members would cover them.
- **[S-416]** "Move into" is built from what the tree will actually accept, not from every container
  on the desktop. An option that refuses itself when clicked is worse than no option.
- **[S-417]** Deleting a container **keeps** its children and hands them to whatever held it.
  Removing the box is not removing the things in it, and the alternative is a dozen widgets gone on
  one click on a surface with no undo.
- **[S-418]** A container cannot be put inside itself or inside its own descendant — that detaches a
  branch into a ring nothing can reach and no layout pass terminates on — and a branch is measured by
  its own height when it moves, because it moves as a whole. Nesting stops at a depth.
- **[S-419]** The state file can name a parent that is gone, a parent that is not a container, or a
  cycle: none of which the UI can produce, all of which a hand edit or a partial write can leave
  behind. Loading repairs each to a widget on the desktop — a state the layout can always draw — and
  says how many rather than repairing silently. Membership is repaired **last**, after ids are known
  unique, because the tree is walked by id.

## Adding a widget

- **[S-420]** Widgets are added from **Add widget** on the edit toolbar: a picker listing every
  widget in sections, the sections split into columns by `ColumnLayout` — reading order kept, no
  section torn from its heading, the tallest column as short as a contiguous split allows. There is
  no second way in from the settings window.
- **[S-421]** Picking a widget does **not** add it and does not open its settings. It starts the
  placement session ([S-1587]): a ghost of the widget at its default size follows the pointer across
  every display, the cards stop taking input so a click over one places the new widget rather than
  grabbing the old, and a line in place of the toolbar says what the click will do. Escape or a
  right-click cancels; leaving edit mode cancels. Paste, Duplicate and Move start the same session
  with one or many existing widgets.
- **[S-422]** A left click lands the payload on the display it is on, in that display's view, at the
  rects computed from the click itself — not from the last pointer move — centred as a block,
  snapped and clamped ([S-11], [S-1586]). Alt bypasses the snap, as when dragging. What landed is
  selected.
- **[S-423]** A ghost traces the **painted** cards, inside the gap, with the card's own corner
  radius — one outline per widget at its offset in the block ([S-1585]): it shows what the user will
  see once the widgets are there, not the cells they own.

## App widgets and About

Help, Themia and the view switcher, ported from `widgets/others/{help,themia,view-switcher}` on
`main`, and the settings window's About page. Owning files: `Widgets/HelpView.cs`,
`Widgets/ThemiaView.cs`, `Widgets/ViewSwitcherView.cs`, `Shell/SurfaceDisplay.cs`,
`Shell/Config/ConfigPages.About.cs`, `Shell/Gallery/SettingsRender.cs`,
`Themia.Core/Widgets/{HelpGuide,RichText,ViewSwitcherRules}.cs`,
`Themia.Core/Storage/SettingsTransfer.cs`.

- **[S-480]** Help has nothing to configure (was [S-13]). Its header is on, as on any widget; the
  open tab is not saved, because the guide is read, not set up.
- **[S-481]** Four tabs in order — Basics, Widgets, Views, Customize — on a `SelectorBar`, Basics
  first (was [S-06]/[S-07]). How they are drawn for the room there is — no tabs, icon tabs, or a
  page list beside the page — is [S-1531].
- **[S-482]** The guide is data (`HelpGuide`), not controls. Emphasis is `*strong*` / `_emphasis_`,
  split by `RichText` into plain runs — never parsed as markup; markers do not nest or cross a line,
  and an unpaired marker stays literal (was [S-14]). Every marker in the shipped guide is paired.
- **[S-483]** The guide describes **this** app, and so — unlike the original's [S-05] — it teaches the
  keys: Ctrl+Alt+W, double-clicking a widget and launching Themia again as the ways into edit mode,
  Add widget and click-to-place, Alt to bypass the snap, Ctrl+C/X/V, Delete, Esc, dragging between
  displays, per-display views. The original could point at a tray icon; this app has none, so a
  guide without the keys would leave no way to change anything. What else it describes is [S-1532].
- **[S-484]** Themia draws the mark (`Assets/themia-logo.png`, from `src-tauri/icons` on `main`), the
  name and `v` + the running version (was [S-01]/[S-03]; arranged by [S-1533]). The version is known before the
  first frame, so there is no empty "v". The mark is decoded logically at its drawn size, in 32-DIP
  steps; the version, then the name, give way on a tile too short for them.
- **[S-485]** Themia and the view switcher start with their header **hidden** (`WidgetInstance.
  DefaultHeader`; was the original's `noHeader` / `defaultHeaderMode: 'none'`). Only a default: the
  user can turn either header on.
- **[S-486]** Neither Help nor Themia ticks, polls or fetches.
- **[S-487]** The view switcher switches the view of **the display it is on**, never the app-wide one:
  the card host hands the widget an `IWidgetDisplay` for its display (`SurfaceDisplay`), and a click
  goes through `AppHost.ShowView` ([S-19]), the same mutation as the toolbar's View menu. Where there
  is no display it shows an empty state.
- **[S-488]** It offers the views in the user's view order, filtered by the widget's selection; an
  empty selection means every view, so views added later appear; a selection naming only deleted
  views also means every view. The shown view is the raised button with its initials in the accent.
- **[S-489]** A button falls back to initials: the first letters of the first two words, or the first
  two letters of a single word, upper-cased; "?" for a blank name. Counted in text elements, so a
  composed character is never split.
- **[S-490]** Settings: one switch per view and "Initials only". The last view cannot be switched off;
  switching every view on stores the empty "all" selection again.
- **[S-491]** The buttons fill the card as one row, or one per row — whichever makes each button
  closer to a 3:1 button shape (compared on a log scale), and a stack only when every button gets
  32px. The original only drew a row, which on anything but a strip made tall columns with clipped
  names. Names show when a button is at least 88px wide and "Initials only" is off; otherwise
  initials, with the name in the tooltip. Default size 320×56: one row with names. The ladder that
  replaced this rule — grids, faces, overflow — is [S-1653]..[S-1655].
- **[S-492]** *(Retired by [S-1746].)* "Initials only" / "Show names" is a setting only; the card
  menu no longer carries it.
- **[S-493]** About is the **last** item of the settings navigation, in its footer under everything
  else. The page follows karpo's About section for section: the name card (64-DIP mark, "Themia",
  "Version x.y.z", ".NET x · Windows App SDK y"), **Made by** Lometics (lometics.com) with its mark and a
  link, **Themia** with Quit Themia (`AppHost.RequestExit`), and **Settings file** with Export,
  Import and Open config file.
- **[S-494]** Export writes the state document — the same JSON as `state.json` — to a file picked
  with `Microsoft.Windows.Storage.Pickers` (window id, works unpackaged), atomically (temp + move).
- **[S-495]** Import reads the picked file through `AppStateStore.Parse`, the startup trust boundary,
  so an import can never accept what a restart would refuse. Empty, non-JSON, newer-schema and
  view-less documents (any other app's JSON would otherwise read as "one blank view") are refused
  with a reason and change nothing.
- **[S-496]** Before anything changes, a dialog says what will: views matched by id, then by name;
  renamed, added and removed views; per view the widgets added, removed and changed; every design
  value before → after. Repairs, displays named in the file that are not connected, and Start with
  Windows staying put are notes, not changes.
- **[S-497]** On confirm, what the user has now is written beside the state file as
  `state.before-import-<yyyyMMdd-HHmmss>.json` — the in-memory state, since a debounced save may not
  have landed — and if that backup cannot be written nothing is imported.
- **[S-498]** The import replaces the state **in place** in one mutation, then gives connected
  displays without an assignment a view as at startup. Start with Windows keeps this PC's value: it
  is a fact about the registry here, not a preference.
- **[S-499]** Debug builds render one settings page to PNG with `--settings-png <AppPage> <out.png>
  [waitMs]`, over a demo state and an `AppStateStore.Detached` store that accepts saves and writes
  nothing, so the render never touches `%APPDATA%`.

Not ported, on purpose: ignoring clicks in edit mode — no widget here does, and the buttons are not
where a drag starts. (The switcher's per-view menu and view icons are ported: [S-1650], [S-903].) Help's localisation ([S-14]'s
locale files) — the guide's text is still English; the string layer and the nine tables exist
([S-1100]). Karpo's "isn't running / Start" card — the settings
window here runs inside the app, so it is never open while the app is not.

## Web widgets: video stream

Ported from `widgets/online/video-stream` on `main`. Owning files: `Widgets/VideoStreamView.cs`,
`Widgets/RotatedBox.cs`, `Widgets/ShownWatch.cs`, `Widgets/IWidgetContextActions.cs`,
`Services/Web/StreamSession.cs`, `Shell/Config/WidgetSettings.Web.cs`,
`Themia.Core/Widgets/{VideoStreamRules,MjpegFrameReader}.cs`,
`Themia.Core/Model/Configs/WebWidgetConfigs.cs`. The original's web element widget is not part of
this app ([S-1724]).

The widget runs only while it can be seen. A paused camera, or one folded into a closed stack row,
an unselected tab or a collapsed group (`ShownWatch` follows the card's ancestors), holds no socket
and no decoder; `Detach` releases everything.

- **[S-460]** Video stream title: the user's label, else the stream's `host:port` (`Uri.Authority`,
  so a non-default port is kept), else the widget name (was [S-01]).
- **[S-464]** A failed, dropped, stalled or ended stream shows "Stream unavailable. Reconnecting…" and
  retries after 2 s, 4 s, 8 s … capped at 30 s (`VideoStreamRules.RetryDelay`; was [S-05]/[S-06]). The
  counter resets on the first decoded frame and on any restart. The delay is armed by the failure and
  cancelled with the session. A stream that sends no bytes for 30 s counts as dropped — a socket
  deadline, which closes the original's [U-02] without a frame-rate watchdog.
- **[S-465]** The camera's right-click menu has **Pause/Resume** and **Refresh** (was [S-11] and the
  original's `contextActions`); rotation is a setting (and an overlay button), not a menu entry ([S-1746]). Refresh reconnects at once with the backoff reset.
  Context actions are a card-level hook: a view implements `IWidgetContextActions` and the card lists
  them in the list under the icon row ([S-1743]), read fresh each time the menu opens.
- **[S-466]** Fit: whole picture (letterbox, default), fill and crop, stretch (was [S-16]). One
  `Stretch` mapping drives both the decoded-frame `Image` and the video player.
- **[S-467]** The stream address is checked by `WebInput.Url` on every read — http/https only, no
  credentials, no UNC, refused rather than repaired — and the settings box explains a refusal (was
  [S-09]). A refused address shows its reason in place of the picture; an empty one is the Set up button
  ([S-1862], was [S-02]).
- **[S-468]** Rotation 0/90/180/270, from settings or cycled by Rotate (was [S-08]). `RotatedBox` lays
  a sideways picture out against the swapped axes before turning it, so it fits instead of clipping.
  A value the enum does not name reads as none.
- **[S-469]** Paused is persisted, so a parked camera stays parked across restarts and uses no network;
  it shows "Paused" (was [S-07]).
- **[S-470]** `RefreshSeconds` 0–300, default 0 (was [S-17]). 0 holds the stream open at the camera's
  rate; any other value grabs one frame, closes the connection, waits, and grabs again — the only real
  throttle, since a camera pushes frames at its own rate. The cache-buster the original needed for the
  webview's memory cache is gone: `HttpClient` keeps no cache, and requests send
  `Cache-Control: no-cache` for any cache in between.
- **[S-471]** The reply's content type picks the transport (`VideoStreamRules.Classify`):
  `multipart/x-mixed-replace` → MJPEG, parsed here; `image/*` → a snapshot (live mode shows it once,
  interval mode polls it); `video/*` → Windows' `MediaPlayer`; an HLS playlist → `AdaptiveMediaSource`.
  Video is muted, looped, kept out of the system media flyout, and the player is created only for such
  a stream. Anything else — an HTML login page, a 401/403/404 — is "not a camera stream" and is **not**
  retried on a timer.
- **[S-472]** MJPEG frames come out of `MjpegFrameReader`: a part's `Content-Length` is trusted when
  present (a JPEG can contain the boundary bytes), otherwise the part runs to the next delimiter;
  quoted and `--`-prefixed boundaries, LF-only line ends, preambles, header-less parts and keep-alive
  empty parts are handled; with no boundary at all, frames are cut at the JPEG start/end markers. A
  frame past 8 MB, or a stream that never delimits, overflows and is reconnected rather than buffered.
  Frames are decoded by WIC at the size drawn; while one decodes, newer frames are dropped, not queued.
Not ported, on purpose: the `demoVideoFrame` demo fixture. The video stream stays
unguarded, as in the original. RTSP stays unsupported ([U-01]);
credentials in the address are refused ([U-03]).

## Accounts and sign-in

Themia's own accounts, the source of mail, calendar, contacts, tasks,
OneDrive, GitHub and Claude usage. Ported from the classic app's `widgets/office/email/src/lib.rs`
(storage, DPAPI, the four OAuth flows, IMAP/DAV verification, retry) and its Email SPEC [S-45],
[S-47], [S-51], [S-53]; code in `src/Themia.Core/Accounts/` (pure) and
`src/Themia.App/Services/Accounts/` + `Shell/Config/AccountsPage.cs`.

- **[S-500]** A closed `AccountProvider` set: Microsoft, Google, IMAP, CalDAV, CardDAV, GitHub, Claude.
  A provider this build does not know is dropped on load and reported, never carried as a string.
- **[S-501]** Providers are written under lower-case names (as the original wrote them), so the
  file stays readable and a hand edit is easy to validate.
- **[S-502]** What an account feeds (`AccountFeatures`) and how it proves itself (`AccountAuth`) follow
  from the provider alone. Widgets ask for a feature, never a provider name.
- **[S-503]** `accounts.json` lives beside `state.json` and carries a `schemaVersion`; a newer schema is
  set aside rather than half-read.
- **[S-504]** Every stored entry becomes a whole `Account` or nothing. A field that says *where* the
  account is (provider, IMAP host, DAV address, a non-https GitHub API root) cannot be guessed and its
  entry is dropped; a field it can live without (nickname, IMAP port, GitHub API root) is repaired; a
  missing secret is kept as "sign in again". Every drop and repair is logged. Duplicates keep the first.
- **[S-505]** Ids are built exactly as the classic app built them (`google:<sub>`, `imap:user@host`,
  `caldav:user@authority`, `github:login@apihost`, `claude:default`, the Graph id for Microsoft), so
  signing in again replaces the account (keeping its nickname).
- **[S-506]** A secret is stored as `dpapi:v1:` + base64 of a DPAPI blob — the classic envelope byte
  for byte. Untagged text and a damaged payload are told apart from a good blob.
- **[S-507]** An access token within five minutes of expiry is treated as expired; a missing or absurd
  `expires_in` becomes an hour (ceiling 30 days).
- **[S-508]** PKCE S256 (RFC 7636 test vector pinned) with a 43-character unreserved verifier, and a
  random state compared in fixed time.
- **[S-509]** Microsoft signs in against `common` with the classic client id and scopes (incl.
  `offline_access`), by the browser flow of [S-1780] (was: device code); Google's consent URL asks for
  `access_type=offline` and `prompt=consent` so a refresh token always comes back.
- **[S-510]** GitHub: one OAuth device flow for github.com (same scopes as the token path), and a
  personal-access-token form whose API root must be https — the only way in for GitHub Enterprise.
- **[S-511]** Claude: Themia's own authorization-code + PKCE grant on the CLI's client (the usage
  endpoint demands `user:profile`), a hosted `code#state` callback the user pastes back, and the CLI's
  user agent plus `anthropic-beta: oauth-2025-04-20` on every usage request.
- **[S-512]** The pasted Claude value splits into code and optional state; an echoed state must be the
  one issued; an empty or code-less paste says why.
- **[S-513]** The loopback redirect is read from the request line; the state is checked before anything
  else is believed (error included); malformed pairs are skipped; a request that is not a redirect
  (favicon) is answered 404 and the listener keeps waiting.
- **[S-514]** Device-code polling is a pure state machine: pending waits the server's interval,
  `slow_down` adds five seconds for good, expiry/denial/other errors end the flow with a sentence,
  and nothing is polled past the code's lifetime. Only GitHub uses it ([S-1780]).
- **[S-515]** DAV multistatus is read by local name (any prefix), properties under a non-2xx propstat
  are absent, a body that is not XML reads as nothing, DTDs are refused.
- **[S-516]** DAV hrefs resolve against the URL the answer came from after redirects; the `.well-known`
  address sits on the origin; a bare host typed by the user becomes https.
- **[S-517]** Adding a CalDAV/CardDAV account discovers the collection (RFC 6764): the typed URL, then
  `.well-known`; a collection is used verbatim, otherwise principal → home set → first usable
  collection (a calendar advertising components must include VEVENT). Credential failure on every
  candidate is reported as such; "signed in but nothing there" wins over a 404 from the fallback.
- **[S-518]** A 429/503 on a repeatable request is retried at most three times, waiting what
  `Retry-After` says (seconds or HTTP date) capped at 30 s, else 1/2/4 s. A POST is never retried.
- **[S-519]** Redirects are followed by hand (the framework drops `Authorization` and rewrites
  PROPFIND): never to a non-http scheme, never https→http, never from a public host to a loopback or
  private address. A bearer token only follows to the same origin; a DAV password may follow an
  https→https hop to another host (iCloud shards). A paging link is only followed on the first page's
  own https origin.
- **[S-520]** Failures are a typed `AccountException` (`AccountError`), carrying the provider's own
  words when its body has any of the known error shapes, and mapping onto the widget `FetchStatus`.
- **[S-521]** IMAP login uses quoted strings, or literals for values a quoted string cannot carry, and
  recognises greetings and tagged answers.
- **[S-522]** Typing an address fills the IMAP server (or DAV address) from a small provider table —
  exact domain or subdomain only, only into fields the user has not typed in, no Outlook.com entry.
- **[S-523]** *(Retired.)* There is no import from classic Themia. The user removed it: accounts
  are signed in afresh here, and the classic app's files are never read.
- **[S-524]** `accounts.json` is written atomically (temp + move) by one writer. A corrupt file is
  moved aside, never deleted; a file that exists but cannot be read makes the store read-only for the
  session so it is never overwritten with an empty list.
- **[S-525]** Secrets are sealed with DPAPI for the current Windows user (`CryptProtectData` via
  `[LibraryImport]`, output wiped and `LocalFree`d on every path). Sealing never falls back to plain
  text. A secret that will not open is reported on the account ("sign in again"), never dropped.
- **[S-526]** One HTTP client for every account request (pooled, user agent, 10 s connect, 30 s per
  request, 16 MB read cap) and one path through it.
- **[S-527]** The browser sign-ins (Google, Microsoft) come back to a raw listener on a random loopback
  port, answering one request; the browser wait gives up after five minutes ("did not come back within
  five minutes. Start again."). Google's redirect names `http://127.0.0.1:<port>`.
- **[S-528]** An IMAP account is saved only after a TLS connect, greeting and LOGIN succeed (then
  LOGOUT); nothing is selected or read.
- **[S-529]** `AccountService` (one, in `AppServices.Accounts`) lists, renames, signs out and adds;
  `AccountsChanged` is raised on the UI thread. A user action whose save fails changes nothing.
- **[S-530]** Token refresh is single-flight per account; whoever waited re-reads the account and uses
  a token another caller already fetched. Only the token fields of the account as it is *now* are
  written, and an account signed out mid-refresh is not resurrected. A revoked grant marks the account
  "sign in again"; nothing ever deletes it.
- **[S-531]** Every authorized request gets its provider's proof; a 401 on an OAuth account triggers
  exactly one refresh and one retry.
- **[S-532]** List helpers follow Graph `@odata.nextLink` and Google `nextPageToken`, item by item
  through a source-generated contract, up to a caller's maximum and 50 pages.
- **[S-533]** DAV requests go as the account with basic auth, `Depth`, and the method and body kept
  across redirects.
- **[S-534]** IMAP credentials are opened only when a session asks for them.
- **[S-535]** OAuth sign-ins verify who signed in (Graph `/me`, Google userinfo, GitHub `/user`) before
  saving.
- **[S-536]** Google's Desktop client secret is never in source: it is compiled in from
  `THEMIA_GOOGLE_CLIENT_SECRET` (a generated constant, validated to look like a secret); without it,
  Google sign-in refuses before the browser opens and token refresh fails with the same sentence.
- **[S-537]** A Claude sign-in is saved only after the fresh token has read the usage endpoint; 401 and
  403 get different guidance, 403 with Anthropic's own reason.
- **[S-539]** Settings → Accounts lists each account (provider icon, name, address, problem), renames
  in a flyout, signs out after a confirmation, and adds through one flow at a time drawn in place:
  GitHub's device code shown large with copy and open buttons and a live status line; Microsoft and
  Google waiting on the browser ([S-1783]); forms for IMAP, CalDAV, CardDAV and a GitHub token;
  Claude's open-then-paste. Errors appear on the page; leaving it cancels a flow.
- **[S-1780]** Microsoft signs in with the authorization-code grant + PKCE (S256) in the system
  browser: `…/common/oauth2/v2.0/authorize` with `response_type=code`, `response_mode=query`,
  `prompt=select_account`, a fresh state and challenge, and redirect `http://localhost:<port>/`; the
  code is exchanged at `…/token` with the verifier, the same redirect URI and the scopes, and no client
  secret (a public client). Microsoft and Google run this as one path; they differ in addresses,
  Google's secret ([S-536]) and the "who" call. The app registration must list `http://localhost`
  under "Mobile and desktop applications" (Microsoft ignores the port of a localhost redirect, but does
  not match `127.0.0.1` against it).
- **[S-1781]** Microsoft's callback is validated by the same reader as Google's ([S-513]): a wrong or
  missing state is refused before anything else is believed; `error` ends the sign-in with its
  `error_description` (`access_denied` reads "cancelled"); a missing or empty `code` is an error, never
  a sign-in. Codes, states and tokens are never logged ([S-983]).
- **[S-1782]** A token exchange answer is a grant only when it is a 2xx with a non-empty string
  `access_token`; `expires_in` is read as a number or numeric string; anything else fails with the
  provider's `error_description` cut to its first line (Microsoft appends trace ids), else its
  `error`, else the HTTP status. Microsoft, Google and Claude exchanges share it.
- **[S-1783]** Settings → Accounts: Microsoft and Google share one browser card ("Your browser opens
  *provider*'s sign-in page…", a live status line, Cancel). There is no code to copy for Microsoft any
  more.
- **[S-1784]** The loopback listens on 127.0.0.1 and, on the same port, on ::1, so a `localhost`
  redirect lands whichever address the browser tries first; without ::1 (IPv6 off, port taken there)
  it carries on with 127.0.0.1 alone, which browsers fall back to.

Not ported: the anonymous endpoint probe and its preset health badges ([S-46], [S-48]) — the presets
here are a fill-in table and the add itself is the check; the two-page preset picker and brand icons
([S-47]) — one form with auto-fill instead; STARTTLS on port 143 — the classic app spoke implicit TLS
only; RSS "accounts" — feeds are a widget setting here; the `devOnly` gate on Google and GitHub OAuth
— a release decision, not wired. Digest auth for DAV is not supported (basic only, as before).

## System widgets: GPU, mixer and screen time

Ported from `widgets/system/{gpu,mixer,screentime}` on `main`. Owning files:
`Themia.Core/Widgets/{GpuRules,MixerRules,AppNames,ScreenTimeTracker,ScreenTimeRules}.cs`,
`Themia.Core/Storage/ScreenTimeStore.cs`,
`Themia.App/Services/{GpuSampler,AudioSessions,ProcessNames,AppIcons,ScreenTimeService}.cs`,
`Themia.App/Widgets/{GpuView,MixerView,ScreenTimeView*,SparkBand,HugRow}.cs`,
`Themia.App/Shell/Config/WidgetSettings.SystemWidgets.cs`. Tests mirror the tags (`S433_...`).

### GPU

- **[S-430]** The GPU tile reads like the other meters ([S-1762]): the load as the number and the
  history as a `SparkBand` inside the card's padding ([S-1824]). One adapter names the card after itself, without vendor and
  trademark noise (`GpuRules.ShortName`: "RTX 4060 Ti"). Load reads `--` for the first sample: it is
  a rate and needs two readings.
- **[S-431]** Data comes from DXGI (which adapters exist: name, LUID) and the PDH GPU counters
  (`\GPU Engine(*)\Utilization Percentage`), all through `LibraryImport` and vtable calls, so
  AOT-clean. The original's registry enumeration is replaced: its numbered class subkeys include
  adapters that are gone and stop at the first gap, while DXGI hands over the LUID the counters are
  filed under. One sampler is shared by every GPU widget: opened by the first, closed by the last,
  sampled on the pool, single-flight, and a request within 500 ms of a sample reuses it.
- **[S-432]** Pressure colours the trace, never the number: high from 70% load, critical from 90%
  (theme caution and critical brushes).
- **[S-433]** `Adapters` is a list of DXGI positions, in drawing order. Negative and repeated entries
  are dropped; an empty selection draws the primary adapter. The settings picker shows the raw
  selection (no primary-adapter default) and is offered only with two or more adapters. Two or more
  selected adapters share the tile as panels, each with its own name, reading and history ([S-1207]).
- **[S-434]** A counter instance belongs to the adapter whose `luid_...` token it carries, found
  anywhere in the name (engine instances start with `pid_...`, which the original's "starts with"
  match missed). Load is the **busiest engine**: per-engine sums across processes, then the
  maximum, which is Task Manager's figure. The original summed the 3D engine only and read near zero
  during video decode or compute.
- **[S-435]** On NVIDIA cards the driver's management library (`nvml.dll`, loaded with
  `NativeLibrary`, called through function pointers) is asked for the load too, and it wins over the
  counters (a borderless full-screen game collapses the 3D counter). Matched to the DXGI adapter by
  name (the n-th of identical cards), never guessed among several. Loaded once per sampler session;
  any failure leaves the counters' figure.
- **[S-436]** Samples every `IntervalSeconds` (1 to 60, default 2). Changing the selection rebuilds
  the panels, so a new adapter's history starts fresh.

Not ported: the widget's own `label` (the card's label is the instance's); video memory, the
driver's sensors, the engines and the processes ([S-1762]).

### Mixer

- **[S-440]** Per-app volume for the default output device (render, console role), through Core
  Audio's session API. **No polling**: one MTA worker thread owns every COM call, registers for the
  default device changing (`IMMNotificationClient`), sessions appearing
  (`IAudioSessionNotification`), each session's volume, state and disconnect
  (`IAudioSessionEvents`) and the master volume (`IAudioEndpointVolumeCallback`), and sleeps until
  one of them or a write wakes it; a wake reads once and publishes one snapshot. The four callback
  objects are immortal native vtables of `[UnmanagedCallersOnly]` methods that only set a flag and
  wake the worker; no exception escapes them. Started by the first mixer, stopped (everything
  unregistered and released) by the last.
- **[S-441]** A row's name: the executable's description (cleaned per [S-458]), then its file name,
  then the session's display name unless it is an unexpanded `@...` resource. Names are read from
  disk once per path; icons are extracted once per path off the UI thread (`AppIcons`), with a glyph
  fallback (speaker for master, bell for system sounds, a window for an app).
- **[S-442]** `GroupByApp` (default on) makes every session of one executable one row: the loudest
  level, muted only when all are, active when any is, and a session-count pill. Off: one row per
  session, keyed by its instance identifier.
- **[S-443]** Rows are ordered by name, case-insensitively, system sounds last; never by activity, so
  a row never slides out from under a dragged slider.
- **[S-444]** `ShowMaster` (default on) pins the device's master level first, under the `__master__`
  key, which no session key can equal.
- **[S-445]** `ShowSystemSounds` (default off) adds the system-sounds row; `OnlyActive` (default off)
  hides apps that hold a session but are silent, never system sounds or the master row.
- **[S-446]** Writes go through a newest-value-per-key queue drained by the worker when it is free:
  single-flight, no throttle interval. A dragged level is held on screen until a snapshot reports it
  or its row is gone. A write whose key matches nothing is a no-op; a session failing
  mid-enumeration is skipped; expired sessions are dropped.
- **[S-447]** A muted row reads 0% but keeps its level; dragging a muted row above zero unmutes it.
  The mute button toggles a row (all of a group's sessions); right-click offers Mute/Unmute and
  Reset to 100%. Rows are reused by position so a snapshot never rebuilds the slider under the
  pointer.
- **[S-448]** No device: "No output device". No rows: "No app is playing audio". Failures are logged
  once, and again only when they change.
- **[S-449]** From 360 px a row is one line (name, slider, percent, mute); narrower, the slider goes
  under the name; under 170 px a row is icon, percent and mute.

Not ported: the poll `interval` setting (there is no poll).

### Screen time

- **[S-450]** The widget draws the app's one tracker ([S-458]) through `AppServices.ScreenTime`; it
  has no timer of its own and redraws when the tracker says something changed.
- **[S-451]** `Period` (Day, Week = last 7 days, Month = last 30 days; rolling windows ending today)
  and `Style` are separate. A day draws Doughnut, Bars or None; a window Bars, Heatmap or None. An
  impossible pair (or an unknown value) becomes the period's first style. Changing the period in
  settings writes both fields.
- **[S-452]** The ring's wedges are the busiest apps up to the cap plus one "Other" wedge, offsets
  from cumulative seconds; the total sits in the hole. A day of Bars is one bar split the same way.
  Durations read `3h 24m`, `12m`, `<1m`. How many apps the legend and the drawing name is
  [S-1211]/[S-1212] (`MaxApps` is a ceiling over what fits, `ShowList` hides the legend). Each app's colour is a stable hash of its key; a legend row
  carries the icon (or an initial chip in that colour), the name with the colour dot right after it,
  and the time as its last column. Hovering a wedge, segment or row lights it, dims the rest and
  names it in the readout.
- **[S-453]** `ShowAverage` (on): in a window, the per-day average (divided by the window length, so
  idle days count) beside the total and on each row. `ShowSwitches` (off): how often the foreground
  app changed. `ShowPercent` (off): each app's share of the shown total.
- **[S-454]** Day navigation in the header (back, label, forward, and a today button when away from
  today): today plus 89 days back. A pinned date stays that date across midnight, and returning to
  today unpins. A window shows "Last 7 days" or "Last 30 days" instead. Reading a day includes the
  interval still open, unless paused or older than a suspend leaves it; a range has one entry per
  calendar day, oldest first, gap days as zeros, clamped to retention. Executable paths are kept per
  app so past days have icons; at start, apps in the history with no path or name are filled from
  running processes.
- **[S-455]** Right-click "Hide from list" adds an app key to `HiddenApps`; hidden apps leave every
  view and the totals re-base. Settings list them with a way back.
- **[S-456]** A window of Bars: one bar per day, stacked by app in the legend's order, scaled to the
  busiest day, weekday labels for a week. Heatmap: seven columns, padded so the window ends on the
  last cell, shaded in quartiles of the busiest day (any use is at least level 1), today outlined.
- **[S-457]** Limits are daily, in seconds: `GoalSeconds` for the whole and `AppLimits` per app; zero
  or less is no limit. Shown as "of 4h" beside the figure (a window judges its daily average, "of
  4h/day"); over the limit, the figure turns critical. Edited in quarter hours.
- **[S-458]** Tracking: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (out of context, skipping
  Themia's own process) names the app (the lower-case executable stem), and a 30 s sampling tick of
  the service's own timer accrues it (not the shared widget ticker, which may pause while widgets are
  hidden). Session lock (`WTSRegisterSessionNotification` on a message-only window) pauses until
  unlock; input idle of 5 minutes (`GetLastInputInfo`) pauses until input returns; Themia's own
  foreground closes the interval. An interval across midnight is split; a gap longer than two ticks
  (suspend, clock jump) is never credited. A switch counts only between different apps while
  accruing. Display names come from the version resource, off the hook's thread, cut at the first
  control character, with an entry-point tail (`WhatsApp.Root`) stripped and the product name
  preferred when the description is identifier-shaped. A loaded history is merged into the live one.
- **[S-459]** The history lives in `usage_stats.json` beside the state file, never in a widget's
  config, so it is never copied, pasted or exported. `{schemaVersion, days, names, paths?,
  switches?}`, the optional maps omitted when empty (bytes pinned by a characterization test). Saved
  atomically (unique temp file and rename), one writer, in order, at most once a minute while dirty,
  on lock, and synchronously at exit; only the newest 90 dates are kept. A corrupt or
  version-mismatched file is moved to `.corrupt` and an empty history started; junk keys are dropped
  on load. The tracker is started by the app host only: a debug render never hooks the
  foreground or writes a file.

Not ported: the original's config migrations (`migrateMetrics`, `applyLegacyMetrics`, the pre-period
`displayStyle` values; [S-1050]); the retired `list` style; invisible
filler rows under a short legend, since the drawing here is sized from the tile rather than pushed
by the list.

## Layout pass over the ported widgets

Found by drawing every ported widget at every size tier (with the debug widget gallery, since
removed, [S-1863]). Sizing rules live in Core with tests in
`WidgetFitTests` (`S64x_`/`S65x_`).

- **[S-640]** A card header's title ends in an ellipsis. A Grid does not clip a child to its cell,
  so an untrimmed title was cut mid-word ("Countdow") or drawn under the trailing element.
- **[S-641]** A hero readout (clock, pomodoro, meters, power, volume) is a ramp step while the box is
  small and, once it holds TitleLarge, that step scaled to fill it, keeping its line-height ratio.
  It used to wait for Display to fit, so every box between the two drew a 40px readout.
- **[S-642]** An empty state sheds to fit: the glyph goes first, then the sentence is held to the
  lines the box has and ends in an ellipsis. Every empty state is `Fluent.EmptyState`; the stack, tabs
  and countdown views no longer draw a text block of their own. The panel never touches the glyph's
  `Visibility`, which several views own.
- **[S-643]** CPU load and network throughput are deltas against a process-wide baseline, so a read
  within 250ms of the last real one is answered from that one. Two CPU tiles used to show 31% and
  0% side by side: the first moved the baseline and the second measured a millisecond.
- **[S-644]** A short pomodoro tile keeps the countdown: a wide one moves the controls beside it, a
  narrow one drops the progress bar. Stacked, a 120px tile had no room left for the digits.
- **[S-645]** The clock insets its readout fully at the sides and by half above and below.
- **[S-646]** A reading's caption (meters, power, weather's condition) moves under the reading when
  it would be trimmed beside it, or when its own line lets the reading be drawn larger.
- **[S-647]** The power headline's Subtitle floor applies only where Subtitle fits across the tile;
  below that the widest step that fits, semibold, down to Body.
- **[S-648]** A process row shows both numbers only from 232px of content; below that only the
  ranked one, and on a narrow tile the columns stop holding open their widest value. The "by CPU"
  label leaves a tile too narrow for it and the title both.
- **[S-649]** The sun strip's icons and countdowns appear only where a strip column can hold them
  (250px and 280px tiles), not where the tile can.
- **[S-650]** Now playing stacks on a tall, narrow tile (art, lines, transport) and moves the
  transport under the lines when beside them it would leave the title under 120px.
- **[S-651]** The scratchpad's header tools shed eraser, then clear; undo stays. A hidden eraser is
  switched off.
- **[S-652]** Glyph code points are checked against the font's cmap. The weather set, Quote and
  Wrap were not in Segoe Fluent Icons (or MDL2) and drew as boxes; the forecast folded rain, snow
  and thunder into its own thermometer icon because of it. Weather now and the forecast share one
  condition→glyph map.
- **[S-653]** The quote may grow to TitleLarge, is held to the lines that fit with an ellipsis, and
  gives up its attribution line on a tile too small for the quote itself.
- **[S-654]** The colour swatch rebuilds when whether values fit changes, not only when the column
  count or density does; the first build before layout had hidden values for good on some sizes.
- **[S-655]** An informational header label ("Just now", "by CPU") gives the title at least 64px and
  is not drawn when less than 40px would be left for it. Through `MaxWidth`, never `Visibility`.
  Commands in the header are not touched; their views shed them.
- **[S-656]** The current-weather reading is sized against the tile less its detail line, not
  against its own height (which pinned the first step chosen); the condition icon goes first on a
  tile too narrow for it.
- **[S-657]** The stock price is fitted to its own text (at least seven characters), not the widest
  price any symbol could have, and the change, currency and session badge go under it when they do
  not fit beside it. The badge is a grid so its label ends in an ellipsis.
- **[S-658]** A forecast row narrower than 160px is the day and its high; the icon and the low go.

Known and left: sparklines grow in from the right for their first minute by design ([Sparkline]);
the scrolling lists (feed, folder, apps, colour swatch on a tall tile) show a partial last row at
the bottom edge as any scrolling list does.

## Mail

The Mail widget reads Themia's own accounts ([S-529]) — Microsoft (Graph), Google (Gmail) and IMAP —
instead of a separate hub program; [S-90]/[S-91] no longer describe it. Ported from the classic app's
`widgets/office/email` (its SPEC tags in brackets) and the mail half of its `lib.rs`.

- **[S-540]** `EmailConfig` keeps the classic wire names (`folder: inbox|sentitems|drafts|deleteditems|junkemail`,
  `viewMode: list|compact|row`, `sortOrder: desc|asc`, `listFields`, `rowColumns`, `accountIds`).
  Every closed set is read leniently: an unknown or mistyped value becomes the default and is
  logged, never a throw that would refuse the state file. `WidgetConfig.Repair()` runs at load from
  `AppStateStore.Validate`; for mail it clamps the count (1–50) and preview lines (1–5), keeps at
  least one row column. No hearth-era field is read ([S-1050]).
- **[S-541]** `accountIds` empty means every mail account, including ones added later (the classic
  widget showed "select an account" instead); a non-empty list reads those accounts that are still
  signed in. Settings lists the signed-in mail accounts as switches under "Every mail account" and
  links to the Accounts page; no account is ever signed in from the widget's page. Folders: Inbox,
  Sent, Drafts, Deleted, Junk [S-12].
- **[S-542]** List (sender + time, subject, preview), Compact (sender + time, subject) and Row (one
  line of user-ordered columns under a header row) views [S-14, S-37]; newest or oldest first over
  the same newest N [S-17]. The card title is the folder; the header carries the unread badge
  [S-02], a warning glyph naming an account that failed or needs signing in (tap → Accounts), and a
  busy ring only for a refresh the user asked for. The card menu offers Refresh and the five folders
  [S-20]; the view and the order are settings only ([S-1746]).
- **[S-543]** List fields Subject / Preview / Account, default all on; Preview only in the list view;
  the account tag shows whenever chosen [S-43]. Row columns From, Subject, Date, Preview, Account,
  reorderable, at least one on [S-16].
- **[S-544]** Message right-click: Open, Reply, Mark read/unread, Archive, Move to Junk, Copy subject,
  Delete [S-21]. Gmail is read through `gmail.readonly`, so its messages offer only Open, Reply and
  Copy [S-22]. Delete moves to Deleted (Graph move, IMAP UID MOVE); only a message already in Deleted
  is removed for good (Graph DELETE; IMAP `\Deleted` + `UID EXPUNGE`, UIDPLUS-scoped). A change shows
  at once and the next probe fetches the truth; a failure is the header warning's tooltip.
- **[S-545]** Graph `mailFolders/{f}/messages` (`$top`, `$select`, newest first); Gmail `messages`
  list then `format=metadata` per message, four at a time, the snippet's entities decoded, web link
  `?authuser=<address>#all/<thread>`; IMAP per [S-553]. One message shape for all three, carrying
  the account, provider, folder and the provider's own id.
- **[S-546]** One `MailService` per app (`AppServices.Mail`). Widgets open shared, ref-counted feeds
  keyed by accounts + folder + count. Each account's folder counts are probed every 2 minutes (Graph
  folder, Gmail label, IMAP `STATUS`) and the list is fetched only when they move or every 15
  minutes [S-25, S-26]; Refresh fetches now. Failures back off on `Backoff` (429 far longer); a state
  change is logged once. The cadence rides the shared ticker, so it stops while the desktop is
  hidden and ends with the last Mail widget.
- **[S-547]** Open: a Microsoft message deep-links to `ms-outlook://emails/<id>` when Outlook's
  protocol is registered, else its Graph web link; Gmail opens its thread; IMAP has no web view and
  opening is a logged no-op with the reason on the header [S-30]. Reply: Outlook's reply link, else
  `mailto:` with `Re:` [S-31]. Only those link shapes reach the shell, through
  `ShellLauncher.OpenMailLink` and `MailLinks.IsLaunchable`.
- **[S-548]** The merged list is the newest N across accounts (undated last); times read now / 5m /
  3h / 2d / Sep 20 in list and compact, a time today / weekday / date in the row view [S-09], and
  re-label every minute without a fetch.
- **[S-549]** Screens, each with its own sentence: no mail account (the Set up button, [S-1862]), chosen
  account gone (link), needs sign-in naming the account (link), loading, offline / unreachable /
  timed out / rate limited, empty folder ("No messages in Junk."), and a list with a warning when
  one account failed [S-04, S-05, S-06].
- **[S-550]** IMAP headers: folded fields unfold, the blank line ends the block, RFC 2047 words
  decode with their own charset (the word ends at the `?=` after the encoding, so a Q word starting
  `=C3` is one word), From splits name and address (including `addr (Name)`).
- **[S-551]** Dates apply their zone to a true instant [#45(d)]; the US zone names RFC 5322 defines are
  honoured (the classic app kept EST as wall time) and any other name reads as UTC; two-digit years
  are 19xx/20xx; an unparseable date is null, never a guess.
- **[S-552]** The IMAP preview walks MIME [S-52]: first text/plain, else text/html, attachments and
  non-text skipped, quoted-printable / base64 / charset decoded before the whitespace collapse, 200
  characters, bounded (6 levels, 32 parts, 16 KB a part). The body stays bytes (Latin-1 chars) until
  a part's charset decodes it, so 8-bit ISO-8859-1 bodies survive. HTML block tags leave a space.
- **[S-553]** IMAP over implicit TLS, one session per job: LOGIN (literal when a value needs one),
  LIST once for folder resolution, `EXAMINE` (read-only) and
  `FETCH n:m (UID FLAGS RFC822.HEADER BODY.PEEK[TEXT]<0.32768>)` — parenthesised [S-38/#45], never
  setting `\Seen`, and capped at 32 KB of text per message. Responses are framed by their literal
  lengths and parsed in Core. A refused LOGIN marks the account "sign in again" unless the server
  says `[UNAVAILABLE]` / `[INUSE]` / `[LIMIT]`.
- **[S-554]** Folder resolution: INBOX; else the SPECIAL-USE mailbox (RFC 6154, not `\Noselect`);
  else the first candidate name present (Gmail's `[Gmail]/…`, Outlook's `Sent Items`, `INBOX.Sent`);
  else the first candidate.
- **[S-555]** IMAP handles are `folder|uid|uidvalidity` (`folder|uid` when the server reported no
  validity); a bare `uid` or anything else is refused.
  Every change SELECTs the listed folder and refuses on a known UIDVALIDITY mismatch.
- **[S-556]** Fitting the tile: the account tag needs 260px, the preview 120px of height (one line
  under 240px), the time 110px, the header's warning glyph 160px; the row view keeps the most
  essential columns whose minimums fit (From, Subject, Date, Account, Preview — so a narrow tile keeps
  From and Date), in the user's order, never fewer than one; the column header needs 140px.
- **[S-557]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported, deliberately: the per-widget header hide and label [S-03, S-19] (the card's own header
placement and label do this for every widget), the "unread only" filter, and the composite
`account:id` message ids (the account travels separately). Keyboard navigation, the header sort
button, the scroll fade and the envelope icons are ported in [S-1281]..[S-1289].

## Tasks and OneDrive

Ported from `widgets/office/todo` and `widgets/office/onedrive` on `main` and their Graph / Google
calls in `widgets/office/email/src/lib.rs`, off the earlier hub program and onto Themia's own accounts. Owning
files: `Themia.Core/Tasks/*` (model, due dates, list rules, wire, optimistic edits, Today fan-out,
local store), `Themia.Core/Widgets/DriveRules.cs`, `Themia.Core/Model/Configs/{TasksConfig,OneDriveConfig}.cs`,
`Themia.App/Services/Tasks/*`, `Themia.App/Services/Files/DriveService.cs`,
`Widgets/{TasksView,TasksView.Rows,OneDriveView}.cs`, `Shell/Config/WidgetSettings.{Tasks,OneDrive}.cs`,
Tests: `TaskRulesTests`, `LocalTaskListsTests`, `TaskSyncTests`,
`DriveRulesTests`.

- **[S-590]** A Tasks widget reads Microsoft To Do (Graph) or Google Tasks through an account
  with the Tasks feature, or a list kept on this PC (`TaskSource`, a closed set). An empty account
  id means the first such account and an empty list id its default list, so a widget dropped by a
  signed-in user shows tasks without a trip to settings; a *named* account that has gone is never
  swapped for another — the widget says so. Every non-list state names itself (no account, sign in
  again, account gone, no lists, loading, offline / provider error, no list on this PC) and the ones
  Settings → Accounts can mend link there; a list on this PC needs no account at all.
- **[S-591]** One `TaskService` per app (`AppServices.Tasks`). Widgets on the same account, list and
  completed-filter share one feed and one request. A refresh never stacks on one still out (a forced
  one is remembered and runs after); a tick is skipped when another widget refreshed the feed within
  half the interval; failures back off through `Backoff` (a 429 far longer). Graph and Google are
  read through `GetGraphPagesAsync` / `GetGooglePagesAsync` (400 tasks a list at most); ids are
  escaped into the path; completed tasks are filtered for Graph here and by Google's own
  `showCompleted`; Google's hidden and deleted tasks are dropped; every Graph status but `completed`
  is open work. Refresh is configurable, 30–600 s (default 60 s).
- **[S-592]** The list picker offers the account's default list, Today, then its lists. The lists are
  cached per account (single-flight, failures not cached, cleared when the accounts change).
- **[S-593]** A due date is a calendar day: midnight of the intended day (Graph zoneless, Google UTC
  `Z`, a bare date, or midnight at any offset) is read verbatim; anything else is an instant —
  zoneless is UTC — resolved to the viewer's local day (the To Do mobile encoding, classic #101).
- **[S-594]** Labels: Overdue / Today / Tomorrow / short weekday (2–6 days) / `14 Oct`, counted in day
  numbers so a DST change cannot shift them. Overdue is said in words and in the critical colour, and
  never for a finished task.
- **[S-595]** Completed tasks are hidden unless "Show completed" is on; then they are struck through.
  Completing one with completed hidden makes it leave the list at once (no timed removal).
- **[S-596]** Today (`__today__`, the classic id) is every task due today or overdue across the
  account's lists (at most 50, read one at a time), oldest first, stable within a day; undated tasks
  are not in it. A failing list is skipped and logged; when every list fails the first error is shown
  rather than "nothing due". A task added from Today is created due today in the default (else first)
  list.
- **[S-597]** Completing, reopening, renaming and deleting are optimistic (`TaskEdits`): the change
  shows at once and a refusal takes it back (a deleted task returns to its place), with a warning in
  the header saying why. A confirmed change stays on top of any refresh that started before it was
  confirmed, so a racing poll cannot flash the old state; a change still in flight always stays.
  Writes to one account go out one at a time, in order; a confirmed write nudges that account's other
  feeds (a list and Today) to catch up. The round marker is the one control that completes a task.
- **[S-598]** Neither provider stores a hand-made order, so an account list's order is task ids in the
  widget's config (`ApplyOrder`: known ids first, unknown ones — new tasks — after; a gone id leaves no
  hole, a duplicate cannot double a row). A list on this PC is its own order, and a move leaves hidden
  completed tasks in their slots. Rows move with Move up / Move down in the row's menu.
- **[S-599]** A new task is added from the row at the foot of the list (Enter adds and keeps the box
  open for the next; leaving it adds and closes) and appears at the bottom: optimistically under a
  temporary id that is never written to, then under the provider's id, with the order on screen pinned
  into the config so the next refresh cannot re-sort it to the top.
- **[S-600]** Double-click a task (or Rename in its menu) to rename it in place. Enter or leaving the
  box commits, Escape cancels, a blank or unchanged title writes nothing. The request is a PATCH of
  the title alone, its body written with a JSON writer so a quote cannot break out.
- **[S-601]** A title wraps to "Title lines" (1–6, default 2; two at most on a compact row) and is
  then cut with an ellipsis; the tooltip carries the whole title, its state, due date and priority.
- **[S-602]** Layout: List puts the due date under the title, Compact beside it (under it anyway in a
  column under 240px wide). Rows are Fluent's fixed heights at minimum and grow only for a wrapped
  title; an important task's title is semibold. The size ladder is [S-780].
- **[S-603]** Lists on this PC are one pool in `tasks.json` beside the state (`schemaVersion` 1),
  shared by every widget: renaming a list retitles each widget showing it, and deleting a widget keeps
  its tasks. Load validates every entry (a list needs a non-empty id and a name, a task an id and a
  title; a repeated id keeps the first; a non-boolean `done` is false). Absent is a quiet empty pool;
  unparseable or newer is moved aside and reported; unreadable makes the pool read-only for the
  session. Saves are atomic, single-writer and coalesced, and flushed at exit.
- **[S-604]** The widget's settings: the source box (first account, each account, This PC); for an
  account the list box; for This PC the pool itself — pick a list, rename it as you type (a blank name
  is kept so the box can be cleared), add one (`New list`, `New list 2`…), delete one after a
  confirmation that names its task count. Switching to This PC lands on the list it had, else the
  first, else a new one — only the last creates anything. A deleted list leaves its widgets on "Pick
  or start a list", never on another list's tasks.
- **[S-605]** Retired (the app is unreleased: no hearth-era config is read). Nulls a hand edit leaves in
  a Tasks config's strings, an unknown source or layout, and a broken row order are repaired on load.
- **[S-606]** OneDrive (`WidgetKind.OneDrive = 47`, Launch) lists a Microsoft account's recent files or
  a folder: a kind icon (folder, picture, video, audio, spreadsheet, presentation, document, file —
  from the MIME type, then the extension), the name, and under it `size · 3h ago` for a file or the
  age alone for a folder (Just now / m / h / d ago, then the date). A double-click opens a file's
  OneDrive page through `ShellLauncher`'s allow list; nothing is downloaded.
- **[S-607]** It starts in Recent files or My files (`DriveStart`) and lists 5–50 items (default 15),
  newest first in a folder. One `DriveService` per app serves a listing younger than the caller's
  allowance from memory and joins a request already out for the same address.
- **[S-608]** It refreshes every five minutes on the shared ticker; a changed item count is fetched
  once the slider settles; an account change clears the cache.
- **[S-609]** A single click walks into a folder (only one on this drive — a shared folder from
  another drive opens on the web like a file); a back row above the list returns one level, and past
  the first folder to the configured start; a reply for a place the widget has since left is dropped.
  Changing the account or the start throws the trail away.

Not ported, deliberately: the pop-out window [S-39] (out of scope by user decision), the per-widget
header hide and label [S-02, S-20] and OneDrive's [S-15] (the card's own header does this for every
widget), the 600 ms delay before a completed task leaves the list [S-26] (no wall-clock timing in the
list; it leaves at once), the one-shot `localTasks` config-bag migration [S-59] (the app is
unreleased: no compatibility code), and the single click on a title that renames with the caret at
the click point [S-50] (a click focuses the row for the keyboard; double-click or F2 renames).
Drag-to-reorder, keyboard navigation, Tab between editors, the scroll fade and the "!" badge are
ported in [S-781]..[S-788]. OneDrive's scroll fade and single-click open are ported in [S-1281] and
[S-1291].

## Calendar and contacts

The classic Calendar and Contacts widgets, on Themia's own accounts ([S-529]) instead of the earlier hub
hub. Pure rules in `Themia.Core/Office` (tested), one shared source in `Services/Office`, the views in
`CalendarView*.cs` / `ContactsView.cs`, settings in `WidgetSettings.Calendar.cs` / `.Office.cs`.

- **[S-560]** Calendar shows events from Microsoft Graph (`calendarView`, `Prefer:
  outlook.timezone="UTC"`), Google Calendar (`events`, `singleEvents=true`) and CalDAV (`REPORT
  calendar-query`) accounts. An all-day event is held as two dates (first day, exclusive end) and is
  never converted between zones; a timed one as two instants. Clicking an event opens its provider
  page (CalDAV has none: tooltip only).
- **[S-561]** Config: `accountIds`, `calendars`, `calendarColors`, `view` (month | week | day | agenda,
  default month), `weekStart`, `showBirthdays` (on), `agendaCount` (1–100, 20). The context menu
  offers Refresh; the view is chosen in the settings only ([S-1746]).
- **[S-562]** An empty `accountIds` means every calendar account (later ones too — the Mail
  pattern); settings offer "Every calendar account" or a choice where the last cannot be turned off.
  Chosen accounts that were signed out are skipped; when none are left the widget says so with a
  link to Settings › Accounts.
- **[S-563]** Every value is read leniently: a wrong type or unknown name becomes the default
  instead of refusing the state. *(The hearth-era config fold-in that shared this tag is retired,
  [S-1050].)*
- **[S-564]** Week start Monday or Sunday drives the month grid, the week view and the weekday header.
- **[S-565]** Provider datetimes: a zone (`Z`, `±HH:MM`, `±HHMM`, negative offsets included) is kept;
  a zoneless one is UTC. Cancelled events (Graph `isCancelled`, Google `status: cancelled`, iCal
  `STATUS:CANCELLED`) are not listed.
- **[S-566]** Colours: a fixed six-colour palette by fetch-target index; a valid `#rrggbb` override
  per calendar (keyed by calendar id, or account id for a default calendar) wins; provider colours
  are normalised (`#rrggbbaa` → `#rrggbb`, `""`/`auto` → none). Chips carry colour when more than one
  calendar is shown or an override is set. Birthdays use the caution hue.
- **[S-567]** Several calendars per account: settings list each account's calendars (Graph
  `/me/calendars`, Google `calendarList`, CalDAV home-set listing) with a colour (Automatic or one of
  the six) and, when there is more than one, a switch. No selection means the default calendar;
  deselecting the last one goes back to it. A Google sign-in whose scope cannot list calendars
  (403) is offered its primary calendar.
- **[S-568]** Ranges: month = whole weeks around the month (42 cells); week; day; agenda = today and
  the next 30 days. Requested from local midnight to local midnight (the classic app used UTC
  midnight). The header title is the period ("October 2026", "28 Sep – 4 Oct", "Sun 4 Oct").
- **[S-569]** Back / forward step a month (day clamped), week or day; a today button shows when
  today is off screen. The widget follows today across midnight until the user pages away.
- **[S-570]** A day lists birthdays, then all-day events, then timed ones by start; an all-day
  event covers [first, end); a timed one shows on every local day it overlaps.
- **[S-571]** Agenda: grouped by day ("Today", "Tomorrow", "Mon 5 Oct"), at most `agendaCount`
  entries; an event still running from before is filed under today; timed events already over are
  left out. Rows: time column (start over end, "All day", "Now"), title, location · organizer; on a
  tile under 260 px the time folds under the title.
- **[S-572]** Month cells: chips (title, coloured edge) as many as fit, the overflow as "+N" beside
  the day number; on smaller cells up to three dots (two in narrow cells) plus a muted one; on the
  smallest the number alone, strong when the day has something. Today is an accent disc.
- **[S-573]** Day: an all-day bar and a 07:00–22:00 timeline, one list row per hour, overlapping
  events in side-by-side lanes; scrolled to the first event (or now).
- **[S-574]** A month narrower than 154 px or shorter than 112 px, a week narrower than 280 px or
  shorter than 96 px, and a day narrower than 140 px or shorter than 112 px fall back to the agenda.
- **[S-575]** Birthdays (when `showBirthdays`) come from Microsoft (`/me/contacts` `birthday`) and
  Google (People `birthdays`) accounts, held for a day. 29 February falls on the 28th in other
  years; the age is the one turned on that day, and none for a year before 1800 (Outlook's 1604).
  The same person in two accounts is one birthday. A refused address book is a header warning, not
  an error over the events.
- **[S-576]** iCalendar is unfolded and read by content line (quoted parameters, escapes, vCard
  groups); DURATION is honoured; nothing throws on bad input; organizer is its CN, else the address.
- **[S-577]** CalDAV asks the server to `expand` recurrences (instances come in UTC). A master the
  server did not expand is expanded here: DAILY/WEEKLY/MONTHLY/YEARLY with INTERVAL, COUNT, UNTIL,
  BYDAY (incl. ±n), BYMONTHDAY, BYMONTH, WKST, plus EXDATE, RDATE and RECURRENCE-ID overrides
  (moved or cancelled). Any other rule part shows the event once, at its start; at most 1000 instances.
- **[S-578]** A TZID resolves through the object's own VTIMEZONE (offsets and yearly rules), then a
  system zone of that id (Windows ids), then the local clock ([S-187]). A calendar URL from the config
  or a home set or principal from the server is only ever asked with the account's password when it
  is on the account's own origin (scheme, host, port).
- **[S-579]** vCards: FN (else N), the preferred EMAIL and TEL (`PREF=1` / `TYPE=pref`, else the
  first; `mailto:`/`tel:` values stripped), ORG's first part, TITLE, UID, BDAY (incl. `--MMDD`).
- **[S-580]** Contacts lists people from Microsoft (`/me/contacts`), Google (People connections) and
  CardDAV (`addressbook-query`) accounts in one list by name; the same person in two accounts once.
  Config: `accountIds` (empty = every contacts account), `layout`, `columns`, `count`, `defaultSearch`.
  Clicking selects (an accent pill), clicking the background clears.
- **[S-581]** Layouts: list (avatar, name, role, address/number), compact (small avatar, name, one
  field), details (table), grid (avatar cards). Details narrower than 88 px a column fall back to the
  list, a grid under two cards to compact. Under 160 px wide the avatars and the count are dropped.
- **[S-582]** Details columns stay in canonical order (Name, Email, Phone, Company, Title); the last
  one cannot be turned off.
- **[S-583]** `count` is at least 5 (steps of 5, no upper bound); each account is asked for
  clamp(count, 500, 1000) so a search spans the address book; search filters before the cap.
- **[S-584]** A search box (from 150 px tall) asks for the keyboard while focused (Esc clears) and
  starts with `defaultSearch`.
- **[S-585]** Search matches name, email, company or job title, case-insensitively.
- **[S-586]** Avatars: one or two initials on `hsl(hash(name), 38%, 34%)`, white text.
- **[S-587]** Right-click a person: their name and role, Send email / Copy email, Call / Message /
  Copy phone, Copy name, Refresh. `mailto:`, `tel:` and `sms:` are built only from a plain address or
  number (`ContactLinks`) and opened through `ShellLauncher.OpenContactLink`.
- **[S-588]** One shared source answers every widget: an answer is reused while fresh (events 4 min,
  contacts 10 min, calendar lists 30 min, birthdays 24 h), an identical request in flight is
  joined, a failure keeps the last good data and is held (an hour for sign-in / access refusals, 15
  minutes for a rate limit) unless the user refreshes. Calendar polls every 5 minutes, contacts every
  15. States: loading; no account (the Set up button, [S-1862]); account gone; sign-in or access needed (with the Accounts link);
  offline and service errors; nothing on. A failure of some accounts with data from others is a
  header warning that opens Accounts when the user can fix it.
- **[S-589]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

*(S-574's fallbacks are now part of the ladder in [S-1260]; S-581's and S-584's size rules are
refined by [S-1270] and [S-1275]; S-568's 42 cells are still fetched, [S-1264] only stops drawing a
week wholly in the next month on a large tile.)*

Not ported: the per-widget header hide (the card owns the header), the reauth button that signed
accounts out (the Accounts page is where sign-in is mended), the birthday-permission toast (there
is no toast pipeline yet; the header warning stays and opens Accounts), and birthdays from CardDAV
(the classic app had none either). Keyboard navigation goes through the shell's keyboard lease: see
[S-1269] / [S-1277] and [S-806] / [S-807].

## Developer widgets: GitHub and Claude usage

Five widgets in the picker's **Developer** section, on Themia's own accounts (Accounts page): the
repository widget (`GitHub`), assigned issues (`GitHubIssues`), notifications
(`GitHubNotifications`), review requests (`GitHubReviews`) and Claude usage (`ClaudeUsage`). Ported
from the classic `widgets/online/github*` (tags `[S-NN]`, `[A-NN]`, `[N-NN]`, `[R-NN]`) and
`widgets/online/claude-usage` plus the Rust `fetch_claude_usage`. Rules live in
`Themia.Core/Developer` with tests in `GitHubRulesTests`, `GitHubPollingTests`, `DevScreensTests`,
`ClaudeUsageTests` and `DeveloperConfigTests` (`S6xx_`).

- **[S-610]** The repository widget shows the repository's name in the card header (the user's own
  label still wins), the newest workflow run's verdict beside it (a coloured dot; the word —
  passing, failing, running, queued — from 300px), stars, forks and open issues (k/M), the latest
  release tag, language · last push · default branch, then Commits, Pull requests, Issues and
  Workflow runs as one-line rows (short hash; `#n` coloured open/merged/closed/draft; `#n`; a
  run's dot, name and branch). Under 180 wide the numbers stack; how the rest shares the tile is
  [S-741]..[S-745]. (classic S-04..S-11)
- **[S-611]** A repository is `owner/name`, a web address of one (github.com, Enterprise, `.git`,
  deeper paths) or `github.com/owner/name` without a scheme; anything else is refused in settings
  with the reason. The host of a pasted address is ignored: requests go to the account's own API
  root, so a token is only ever sent where the user signed in. (classic S-12, S-39)
- **[S-612]** Addresses as the classic app: `/repos/o/r`, `actions/runs?per_page=max(n,1)`,
  `commits?per_page=n`, `pulls?state=all&sort=created&direction=desc&per_page=min(3n+5,100)`,
  `issues?state=open&per_page=n+5` with pull requests dropped, `releases/latest` (a 404 there is
  "no release"). Counts 0–10 (commits 3, pulls 3, issues 3, runs 5); 0 hides a list. Only the
  repository itself is required: a failed side request is logged and drawn as absent, except a
  rate limit or a lost sign-in, which stop the lot. A 404 on the repository reads "not found, or
  this account cannot see it". (classic S-14..S-17, S-24..S-29)
- **[S-613]** Pull requests: filter Open / Closed (merged included) / All, order Newest / Oldest by
  creation, applied to the over-fetched list (classic issue #42, S-40, S-41).
- **[S-614]** No repository set: the Set up button ([S-1862]); an invalid one
  names itself. Account states come first. (classic S-01)
- **[S-615]** Assigned issues: `search/issues?q=is:open is:issue archived:false assignee:@me`
  (`(assignee:@me OR mentions:@me)` with mentions on), `sort=updated&order=desc&per_page=count`,
  per account; repository from `repository_url`; count 1–25, default 8. (A-15, A-17, A-18)
- **[S-616]** Review requests: `is:open is:pr review-requested:@me archived:false`; drafts dropped
  unless asked for (twice the count fetched then, so drafts do not starve the list), drawn muted
  with "draft" on the number. Rows add the author. (R-06, R-07, R-16, R-20)
- **[S-617]** Notifications: `notifications?all={showRead}&participating={participatingOnly}&per_page=count`
  per account, count 1–20 (default 5); each item keeps its account. Subject types read as words
  ("Pull request", "CI").
- **[S-618]** A row leads with its reason, a closed set: security critical; review, mention, team,
  assigned and approval caution; author accent; the rest neutral. Read threads are drawn in the
  secondary colour with a tertiary reason; the unread count is in the header. (N-06..N-08)
- **[S-619]** A thread opens its issue or pull request from `subject.url` (`api.github.com` →
  `github.com`, Enterprise `/api/v3` dropped, `pulls` → `pull`); any other shape, including a
  comment URL, opens the repository instead of a page that 404s. (N-22)
- **[S-620]** New: notifications are grouped under their repository (by web address, so the same
  name on two hosts is two groups), groups ordered by their newest thread, each heading with its
  unread count — when the list spans more than one repository and the body is at least 160px tall.
- **[S-621]** Right-click a thread: Open in browser, Mark as read (`PATCH notifications/threads/{id}`,
  disabled when read), Mark all in the repository as read (`PUT repos/{o}/{r}/notifications`),
  through the thread's own account. The change shows at once; a failure puts it back and says why
  in the header warning; success drops that account's cached notification validators and refetches.
  (N-17, N-27)
- **[S-622]** Several accounts merge newest first, cut to the count, the same issue seen by two
  accounts listed once. One account failing still shows the others (with a header warning); every
  account failing is a failure, never "nothing here". (A-17, R-03, R-18, N-21)
- **[S-623]** Accounts: list widgets read the chosen GitHub accounts, or every one when none is
  chosen ("Every GitHub account" in settings, so accounts added later join); the repository widget
  and Claude usage read one (the first when none is chosen). Only ids are stored. (A-07, R-08, N-09,
  S-13, S-37)
- **[S-624]** One `GitHubService` per app (`AppServices.GitHub`): widgets open shared feeds keyed by
  everything that changes the answer (never the token), so equal widgets cost one request; one
  cache of answers keyed by account and address serves every feed; the last widget to let go stops
  a feed. Polling rides the shared ticker. (S-35, A-21, R-23, N-25)
- **[S-625]** Polite polling: every repeat GET is conditional (`If-None-Match` with the last ETag,
  `If-Modified-Since` with the last `Last-Modified`), and a `304` — free against the budget — is
  answered from the cache. Notifications follow `X-Poll-Interval` when it asks for longer than the
  widget's cadence. A 403/429 with `X-RateLimit-Remaining: 0` or `Retry-After` is a rate limit, not
  a refusal (`RateLimitRules`); it, or a success that spent the last request, holds every request
  for that account until `Retry-After` / the reset (at most 2h), and the widget says when it will
  ask again. The account service gained `AccountReply.Headers` and `IsNotModified`, and
  `AccountException.ProviderMessage` / `RetryAt`, for this.
- **[S-626]** Refresh every 5, 15, 30 or 60 minutes (5 by default; Claude 15); a stored value outside
  5–60 is clamped on load. Each widget's right-click menu has Refresh. (S-20, S-23, A-10, A-14, N-13,
  N-18, R-11, R-15)
- **[S-627]** Double-click a row (or the release tag) to open it, through the launcher's allow list;
  the row's tooltip carries its whole title and repository. (S-33, A-19, N-23, R-21)
- **[S-628]** States, each its own sentence: no account at all is the Set up button ([S-1862]); a
  signed-out chosen account (and, for the anonymous repository widget, a chosen account that is
  gone: "Sign in to GitHub in Accounts.") offers an "Open Accounts settings" link (`AppServices.ShowAccounts`); an
  account the service found refused says who needs to sign in again; loading; offline; rate limited
  with the time; the provider's own reason for other refusals; and per-widget empty sentences.
- **[S-629]** Counts as GitHub writes them (999, 1.2k, 12k, 3.4M); ages now / 5m / 3h / 2d, then
  "4 Mar" (with the year when not this one).
- **[S-630]** Claude usage: a bar per window — "Session (5h)" and "Weekly (all models)" always,
  "Weekly (Opus)" / "Weekly (Sonnet)" when toggled (a null window is a 0% bar). Each bar has the
  rounded percentage and, at the loosest density, a "resets in …" line that keeps its slot. (S-04,
  S-05)
- **[S-631]** `utilization` is already a percentage: clamped to 0–100, never rescaled (classic U-01).
  Accent under 70%, caution 70–89%, critical from 90%. (S-06)
- **[S-632]** "resets in 3d 4h" / "3h 20m" / "45m"; nothing for a null, past or unreadable time; the
  prefix goes when the labels are shortened.
- **[S-633]** `GET api.anthropic.com/api/oauth/usage` through the Claude account (the account service
  renews the token, retries one 401, sends the CLI user agent and `anthropic-beta`). The body is
  parsed strictly for the four windows; unknown buckets are ignored; a non-object body is a loud
  error, never all zeros. (S-07, U-02)
- **[S-634]** A 401 the service could not mend is "sign in again" (the account is marked, never
  removed); a 403 is "lacks a permission: {Anthropic's reason}"; anything else names the status and
  Anthropic's words. (classic `map_usage_error`)
- **[S-635]** The widget never scrolls: rows draw at the loosest density that fits (label and % over
  bar over reset; without the reset line; one line; one line sharing the leftover height), and the
  labels shorten with the width ("Weekly (all models)" → "Weekly" → "7d"), estimated on the type
  ramp. One-line rows are centred on a wide, short tile. (S-17, S-18)
- **[S-636]** Settings: the Claude account (the first by default), the Opus and Sonnet toggles,
  refresh. (S-09..S-12)
- **[S-637]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-638]** Content stays through a failed refresh; the header shows a caution glyph whose tooltip
  says what failed (and opens Accounts when the fix is there). (S-36)
- **[S-639]** Lists show whole rows only: the scroll viewer is given the height of the rows that fit
  (never ending on a section heading); the rest is a scroll away.

Not ported: the legacy `config.token` migration (S-38; nothing is imported from the classic app, [S-523]),
the per-widget `label` and `hideHeader` (the card's own title and header placement cover both), demo
mode (S-32, S-15), and the single-click open (double-click, as every
list on this desktop). The cache across restarts and anonymous public access are ported in
[S-746]/[S-747] below.

## Responsive layout (shared foundation)

- **[S-660]** A widget's content area (the card less its header) is classified per axis into
  `SizeClass` Compact / Regular / Expanded / Huge (width breakpoints 180 / 400 / 720, height
  128 / 280 / 480 DIPs) plus a shape (Wide ≥ 2:1, Tall ≥ 1:1.6). The seven size tiers (`WidgetTiers`) land in
  seven distinct (width, height) pairs. An unmeasured area is Compact. (`WidgetArea`)
- **[S-661]** Optional parts are disclosed in priority order from one height budget: a part is shown
  only when it fits whole; once a part misses, no later part is shown (growing a tile only ever adds
  parts); a part too narrow is skipped without closing the ladder. (`Disclosure`)
- **[S-662]** Lists and line budgets count whole rows only, one arithmetic for all of them
  (`Disclosure.WholeRows` / `TakeRows`, 0.5 DIP rounding tolerance).
- **[S-663]** The card tells a view that implements `IWidgetSizeAware` its slot, once per layout pass
  that changed it, never unmeasured; views that do not implement it are unaffected.
- **[S-664]** Meters: the reading, then the history growing to fill the tile; a tiny tile is the
  reading alone (the ladder is [S-1762]). A wider chart spans more history (one sample per ~6 DIPs).
- **[S-665]** Clock: time → date (short "Sun 4 Oct" when narrow; with the ISO week and day of the
  year appended on a wide strip) → "Week 40 · Day 277" at Expanded height → time zone ("UTC+02:00 ·
  name") at Expanded × Expanded. A tall column stacks hours over minutes when that draws them larger.
  Analog face unchanged.

## Shell skeleton and shared settings

Prep for the parallel shell and widget packages (S-700..S-719). Mostly file layout; the behaviour
rules are these.

- **[S-700]** Calculator, Clock, Colour swatch and Web search start with their header **hidden**, as
  in the original (extends [S-485]; `WidgetInstance.DefaultHeader`). Only creation reads the default;
  a loaded widget keeps the placement it was saved with.
- **[S-701]** The app pages of the settings window come from one table, `ConfigPages.AppPages`
  (page, title, glyph, main or footer slot, builder). The navigation and the body both read it, so a
  page is added with one line plus its own `ConfigPages.<Page>.cs`. A reserved `AppPage` value with
  no table entry is not in the menu and, if asked for, logs a
  warning and shows an empty page. About stays the last footer entry ([S-493]).
- **[S-702]** Behaviour switches live in `AppState.Preferences` (`AppPreferences`), apart from the
  look (`DesignState`): language (`AppLanguage`, default follow Windows), hide in full-screen apps
  (off), desktop icons (on), local network access (off).
  Nothing reads them yet; each is wired by its package. Import brings them over and lists changes.
- **[S-703]** A view may carry an icon (`ViewState.Icon`, one glyph, null = initials). Anything but
  a single glyph is repaired to null at load.
- **[S-704]** Design gains the header fill (`HeaderBackgroundColor`, empty = the widget's fill;
  `HeaderOpacity` 0..1), the view-change and unlock animations with their own durations (0..3000 ms). `IntroAnimation` gains Sides, TopBottom, Flip, Blur, Hyperspace and
  Hacker. Out-of-range values are clamped, unknown animations reset to Scale, at load.
- **[S-705]** Per-display settings on `AppState`, keyed like `MonitorViews`: `DisabledMonitors`
  (blank and repeated ids dropped), `MonitorScales` (clamped to 0.5..2), and
  `DefaultViewForNewDisplays` (null = the active view; a deleted view is repaired to null).
- Not added: the updater's auto-check switch and an Updates page, since the updater is out of scope.
- **[S-1850]** One app icon everywhere: `Assets/themia.ico` (the original app's tile logo, 16..256 px) is
  the exe's resource icon (`ApplicationIcon`) and is set on every window through `AppIcon.Apply` —
  settings, layout editor, each display surface, the Debug render windows. A missing file is logged, not fatal.

## Gallery

Pictures from a folder, one picture file, or Bing's photo of the day (S-760..S-779; the earlier
[S-300]..[S-303] still hold for folders). Ported from the original's `widgets/others/gallery` SPEC.

- **[S-760]** `Source` is a closed set: `Folder` (default), `File`, `Bing`. Folder uses `Path` +
  `Recursive`; File uses `FilePath`; Bing needs no setup. The settings page shows only the rows the
  chosen source (and, for the slide time and picture size, the layout) uses.
- **[S-761]** File: one picture full-bleed. An empty box is the Set up button ([S-1862]), as is a folder source with no folder; a name that is not
  a picture is refused in settings and reported by the widget, and a missing file says so. The
  picker offers only the extensions the widget can draw.
- **[S-762]** Bing: the `HPImageArchive` JSON (today, en-US) through the shared HTTP client and a
  source-generated contract. Only a path on `www.bing.com` is accepted (protocol-relative, absolute,
  backslashed and non-rooted paths are refused). Offline and "could not load" are separate states.
  The picture is decoded at the size shown and cached by the system's HTTP cache; a failed load is
  dropped from the decoder cache so the next refresh asks again.
- **[S-763]** Bing shows its title and credit under the picture while `ShowCaption` (default on);
  the credit is shed before the title, and both below Regular width. File and folder never caption
  a single picture.
- **[S-764]** Scatter: square prints, one per cell of the grid shape for the count (spare cells left
  empty at random), centre in the middle half of its cell, side 1.05–1.4× the cell's short side
  (capped at 90% of the card's), tilt ±12° as in the original, stacking order shuffled. The
  original's "within 28% of the centre" heaped every print in the middle of a big card. About one
  print per 12,000 DIP².
- **[S-765]** Mosaic: the card cut recursively, the first half of the pictures getting a 35–65%
  share, across the longer side (the original alternated; same on a square, no slivers on a strip).
  Cells tile the card exactly with no gaps; always cropped to fill. About one cell per 9,000 DIP².
- **[S-766]** Grid: about one 120-DIP tile per slot (max 24), shaped so tiles are closest to square
  with a small penalty per empty cell.
- **[S-767]** The ladder (`GalleryPlan`): any layout with room for only one picture is that picture
  full-bleed with nothing on it (XS). Slideshow: counter in the header and the hover bar from
  Regular width; "up next" thumbnails beside it on a Wide strip (squares, the picture keeping ≥1.5:1)
  or in a row under it from Expanded height (3–8 thumbnails); the file name under it at L, and
  "N of M · folder" at XL. Grid / scatter / mosaic take more pictures as the card grows. Scatter and
  mosaic are dealt from one seed per scan, so a resize moves pictures rather than reshuffling them.
- **[S-768]** A tap on a thumbnail jumps the slideshow there and restarts its hold.
- **[S-769]** The hover bar (previous / pause-resume / next) shows only while the pointer is over
  the widget, in the slideshow with more than one picture. Pause is not persisted.
- **[S-770]** The slideshow's own timer runs only while it is a folder slideshow with more than one
  picture and not paused.
- **[S-771]** Double-click a picture opens it; right-click a picture: Open, Show in Explorer,
  Pause / Resume, Refresh images (no layout switch, [S-1746]).
- **[S-772]** `RefreshMinutes` is one of 5 / 15 / 30 / 60 (default 60, the original's); a corrupt
  value snaps to the nearest.
- **[S-773]** The card's menu adds Pause / Resume and Refresh images (re-reads now and deals a new
  scatter / mosaic); the layout is a setting only ([S-1746]).
- Left / Right / Space keys ([S-29] of the original) are ported through the keyboard lease
  ([S-811]). Not ported: a file or folder caption
  of date taken. The slide minimum is 1 s, as in the original ([S-1620]).

## GitHub widgets: filling the tile, detail, saved copies

Round 3 of the four GitHub widgets (`GitHub`, `GitHubIssues`, `GitHubNotifications`,
`GitHubReviews`). Rules in `Themia.Core/Developer` (`DevLayout`, `DevSavedCopy`, `GitHubRules`,
`DevScreens`, `GitHubPolling`), tests in `DevLayoutTests`, `DevSavedCopyTests`, `GitHubDetailTests`,
`DeveloperConfigTests` (`S74x_`).

- **[S-740]** "Fill the tile" (`FillTile`, on by default) on every GitHub widget: the feed asks for
  the most a list may hold (search 25, notifications 20, each repository list 10; a repository list
  at 0 stays hidden) and the view shows the whole rows that fit, so a bigger tile shows more. Off,
  the counts are exact. Settings: a toggle over the count, which hides while filling.
- **[S-741]** The repository widget's lists share their height by quota (`DevLayout.Quota`): every
  list that has something gets its heading and one row first (priority commits, pull requests, runs,
  issues), then rows are dealt one at a time; a heading over the only list shown gives its room to a
  row. A wide strip (Wide shape, at least 400 wide) puts the numbers in a 170 column and the newest of
  each list beside them, without headings.
- **[S-742]** From 520 wide the repository's lists sit in two columns (commits and runs | pull
  requests and issues), and grouped notifications put their repositories in two columns, a group
  starting the second column when it would not fit whole under the first (`DevColumns`, each column
  a whole-rows host).
- **[S-743]** Rows grow sideways: a two-line row adds its detail at the right of its second line from
  400 wide and label chips from 480 (one-line rows: 640 / 720). Issues and reviews: "author · N
  comments" and labels; notifications ungrouped: the subject type; repository rows at a Huge height
  are two-line with the commit author, the pull request's author and state word, the issue's author,
  comments and labels, the run's branch and "trigger · actor". Search lists go two-line from 168 of
  body (other lists 200). The tooltip carries everything a row trims.
- **[S-744]** New fields from payloads already fetched: commit author (name, else login), pull request
  author, issue author / comment count / labels (named only, at most five, colour kept only as six hex
  digits; chips draw a dot in that colour on the theme's subtle fill, two at most, then "+N"), run
  event ("pull request") and actor, search items' comments and labels.
- **[S-745]** The repository's description (first line) shows from an Expanded height (one or two
  lines by estimate), or fills the room a tile too short for any list has left. From 400 wide the
  release tag sits in the header beside the verdict (double-click opens it); at Huge × Huge the
  summary adds "tag · release name · released 3d".
- **[S-746]** Anonymous public access (classic S-13): with no GitHub account signed in and none
  chosen, the repository widget reads as `github:anonymous` — no token, github.com, its own
  rate-limit gate and cache — never faster than every 15 minutes (60 requests an hour per address;
  a repository costs six, a `304` none). A 401 there is a refusal, not a broken sign-in; a 404 says a
  private repository needs an account. A chosen account that left still says so. Settings call the
  account optional. List widgets still need an account.
- **[S-747]** Saved copies (classic S-34, A-22, R-24, N-26): each feed's last good answer per
  account is written atomically to `%LOCALAPPDATA%\Themia\Cache\GitHub\<hash>.json` (an
  envelope: `schemaVersion`, key, account, fetch time, value; the name a hash of key and account; no
  token). On start it is shown at once, flagged by the header caution "Showing what was saved 3 h ago;
  refreshing." until the first answer lands (a failed refresh's own warning outranks it). A file of
  another version, feed or account, older than a week, or that does not parse is ignored with a log
  line. A signed-out account's copies are deleted; copies untouched for a week are pruned.
- **[S-748]** Notifications: an accent dot before an unread title wherever read and unread are both
  listed (not colour alone); the widget menu's "Mark all as read" sends `PUT notifications` per
  account read, optimistic with rollback as [S-621].
- **[S-749]** Row menus: Open in browser, Copy link, Open repository (from the item's own page,
  `GitHubRules.RepoPageOf`); reviews add "Open changed files"; notifications keep their mark-read
  items. The repository widget's menu adds "Open repository".

Keyboard navigation of rows is ported through the keyboard lease ([S-810]). Not ported: the search total ("8 of 23") and grouping issues by repository (extras, not classic).

## Weather: current conditions and forecast

Round 3 of `WeatherNow` and `WeatherForecast` (originals `weather-current`, `weather-forecast`).
Rules in `Themia.Core/Widgets` (`WeatherLayout`, `WeatherFacts`, `WeatherReport`, `WeatherData`) and
`Themia.Core/Storage/WeatherCacheStore`; tests in `WeatherLayoutTests`, `WeatherCacheStoreTests`,
`WeatherReportTests` (`S72x_`).

- **[S-720]** One open-meteo request per place carries the current block (temperature, feels-like,
  humidity, wind + direction + gusts, precipitation, pressure, cloud cover, UV), today and the next
  five days (high/low, sky, precipitation sum + max chance, max wind, max UV, sunrise, sunset) and
  the next 24 hours (temperature, precipitation chance), `timezone=auto`. The forecast asks the same
  daily fields for 1..14 days. Every value is range-checked; a missing extra block still yields a
  reading. Hours carry the place's UTC offset; a reply without one has no hourly curve.
- **[S-721]** Day labels: "Today", then the culture's own abbreviated weekday (English while the app
  runs globalization-invariant).
- **[S-722]** The current readings, in order: Feels like, Humidity, Wind ("11 km/h NW"),
  Precipitation (the last hour's amount while it rains, else today's chance), Pressure, UV index,
  Cloud cover, Gusts, Sunrise, Sunset. A reading the service did not send is left out, not "--".
- **[S-723]** Current-conditions ladder (`WeatherNowLayout.Plan`): the temperature always (icon
  beside once it fits, condition beside or under, [S-656]); then — a strip: the readings as
  label/value lines in a column beside the reading; a tall column (H ≥ Expanded): today's high/low,
  then up to 8 edge-to-edge rows; otherwise a flat row of icon + label + value cells (no boxes,
  original S-07) once H ≥ Regular and three cells fit, else a single "Feels 17°C" token; today's
  "H 19°  L 11°" under the condition (Least ≥ Regular, or beside on a strip); a second row of cells
  and a five-day strip at Huge both ways; and the next 24 hours as a full-bleed area chart with
  precipitation-chance bars and six-hourly labels once H ≥ Expanded (not on strips or columns). With
  a chart the reading stops at Display and the chart takes the rest. "Show detail" off = the reading
  alone at every size.
- **[S-724]** Forecast layout setting `Layout` = Auto | Rows | Columns | Grid (default Auto: columns
  on a strip of width ≥ Regular, rows elsewhere); the user's choice always wins. Chosen in the settings
  only ([S-1746]).
- **[S-725]** Rows: stretched to fill the tile when every day fits at the row floor (40, or 32
  compact under a 200px list), capped at 80, else scrolling (hidden bar). Columns by width: icon +
  low ≥ 160, range bar ≥ 300, sky ≥ 380, droplet + amount ≥ 420, chance and max wind ≥ 500. A row
  ≥ 46 tall without a sky column puts the sky on a second line and the precipitation under the
  high. Huge both ways: a Today panel (sky, high/low, precipitation + chance, wind, UV, sunrise,
  sunset) beside the rows; a tall column with room for it and every row: the panel above them.
- **[S-726]** Columns: day + high always, then (by height) icon, low, a vertical range bar that takes
  the leftover, precipitation, sky (column ≥ 72), and at H ≥ Expanded the chance and the wind;
  scroll sideways below 56px a column. Grid: 1 card across ≤ 220, else 2..4; cards stretch to fill or
  scroll at 64; each card: day + icon, high/low, then bar, sky, precipitation "mm", chance · wind, sun.
- **[S-727]** Today is marked by an accent, semibold label and a quiet fill under its row, column or
  card. Precipitation is a droplet and the amount ("0.3"; "0.3 mm" in the grid), blank on a dry day.
- **[S-728]** `HideLocation` ("Location name" switch, both widgets): off leaves the header to the
  catalogue label; on, the title is the chosen place's city (first part of `LocationName`), the typed
  query only before a pick. A user label still wins.
- **[S-729]** The last good answers persist in `weather_cache.json` beside the state
  (`schemaVersion` 1, atomic temp + rename, one writer chained after each fetch that brought a new
  answer). At launch they are restored marked `Restored`: drawn at once with their real age in the
  caution colour ("Saved reading from 3 h ago; refreshing."), never treated as fresh, fetched again.
  A file of another schema or not JSON is set aside as `.corrupt`; entries older than 3 days, from
  the future, or without a temperature are dropped. A debug render never reads or writes it.
- **[S-730]** "Refresh" in both widgets' menus resets the back-off and bypasses cache freshness
  (`WebCache.GetAsync(force)`), still joining a request already in flight.
- **[S-731]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-732]** City search is one reusable piece, `PlaceSearch` + `PlaceSearchBinding` (Sun & Moon can
  bind its own config): search 400 ms after typing stops once 2+ characters, Enter at once; results
  open as a drop-down over the rows below; "Searching…", "No locations found." (not an error),
  "Search failed." (error, logged); malformed results dropped. A chosen place replaces the search with
  one "Location" row and a Change button, which clears the choice and brings the search back.
- **[S-733]** Reading glyphs, checked against the font: droplet EB42, thermometer E9CA, wind E7BC;
  layouts: list E8FD, columns E8C0, grid E8A9.

Not ported: wind in mph for °F (the original had none either); a gradient fill on the range bar
(solid accent, as every other bar in the app); the refresh stepper (fixed choices app-wide).

## Tasks and Claude usage: using the whole tile, and the rest of the original

- **[S-780]** Tasks ladder (`TaskLayout.Plan`, from the card's `WidgetArea`): one task always; the add
  row from Regular height; the summary strip on an Expanded × Expanded panel; the list takes the rest.
  Compact rows below Expanded height; one-line titles at Compact height, at most two below Expanded.
  Due text by width: none on an XS tile (both axes Compact), only Overdue/Today in a Compact-wide
  column, the short label otherwise, label plus date ("Overdue · 2 Oct", "Thu 8 Oct") on a panel;
  beside the title on a strip or panel (Expanded width) or in Compact layout, never in a column under
  240px. An open overdue task's marker rings in the critical colour at every size. On a Compact-wide
  tile the rows' side inset halves.
- **[S-781]** The list shows whole lines only (the viewport ends on the last line that fits, never on
  a section heading) and the lines near its bottom fade while more is below: row opacity, not a
  painted gradient, because the card may be transparent. Without an add row the list's foot padding
  goes to rows.
- **[S-782]** Two columns, filled top to bottom then left to right, the two cells of a line sharing
  its height — only on a Huge-wide tile and only when one column would not hold the list.
- **[S-783]** The summary strip: "7 open · 2 overdue · 1 due today · 3 done", open always, the rest
  when non-zero.
- **[S-784]** Completed tasks (when shown) follow the open ones, each part in its own order, under a
  "Completed · n" heading from Expanded height.
- **[S-785]** An open important task leads its title with a "!" in the critical colour (classic
  S-11) where its column is at least 160px wide; the weight and the tooltip say it everywhere.
- **[S-786]** Drag a row to reorder it (classic S-54): a press arms a drag after 5px (never on the
  marker, never while an editor is open); the lifted row follows the pointer and the rows it passes
  make way (into a slot over its middle half, within its own section); the list scrolls a little when
  the pointer nears an edge; letting go commits once through the Move up / Move down path; a lost
  capture puts the rows back. In edit mode the card captures the press and moves the widget instead.
  Redraws wait for the drop.
- **[S-787]** Keyboard (classic S-33..S-37, S-50 F2): a click in the widget takes the shell's
  keyboard lease ([S-808]) and a click on a row focuses it; Up/Down/Home/End move without wrapping,
  Enter ticks, Delete deletes, F2 renames, Escape gives the keyboard back; leaving the list drops
  the row focus; a redraw keeps the focus
  on the same task, or the row now in its place.
- **[S-788]** Tab in a rename box commits and opens the next task's box (none past the last); in the
  add box it commits and keeps it open; Shift+Tab commits and closes (classic S-56).
- **[S-789]** Under Today on a panel each row names its list (the account's lists, cached).
- **[S-790]** Claude usage spends the height stacked rows leave: thicker bar (6 → 12), then the
  percentage at Subtitle, then gaps up to 24; what is left centres the rows.
- **[S-791]** One-line rows on a strip at least 360 wide carry a reset column ("2h 9m"); labels
  shorten to make room for it.
- **[S-792]** An XS tile with one or two bars shows the session percentage as a hero in its severity
  colour over its bar, and "7d 73%" under it.
- **[S-793]** On an Expanded-wide tile each stacked row adds the reset's clock time and the pace
  ("resets in 3d 4h · Wed 16:00 · ahead of pace") and a tick on the bar at the share of the window
  gone; windows are 5 h and 7 d; within 5 points is "on pace".
- **[S-794]** A session gauge (270° arc, percentage and countdown inside, pace tick across the ring,
  label and pace under it): beside the weekly bars on a Huge-wide panel, or on any Expanded panel with
  one or two bars; above the week in a tall column with one or two bars.
- **[S-795]** "Updated 3m ago" under the rows from Regular height, with the account's name when the
  gauge is beside the bars.
- **[S-796]** Refresh is a 5–60 minute slider in steps of five (the classic stepper); a stored 10 or
  45 is kept, not snapped to a preset.

## No backwards compatibility

The app is unreleased, so the schema changes in place and no code reads an older shape. Validation of
corrupt, hand-edited or wrong-typed data at trust boundaries stays: that is robustness, rejected or
repaired to defaults and logged, never an upgrade. S-1050..S-1059.

- **[S-1050]** No migration, legacy-key reader or "old shape" repair exists, and none may be added.
  `WidgetConfig.Repair()` only clamps ranges and drops values a hand edit made unusable.
- **[S-1051]** Only `AppState.CurrentSchemaVersion` is read. A newer state file is refused
  (`NewerSchema`), an older one is refused as not supported (`NotState`); the file is set aside as
  `.corrupt`, an empty desktop starts, and the user is told. The same holds for the settings import.
- **[S-1052]** *(Retired.)* [S-540]/[S-563]'s hearth-era Mail, Calendar and Contacts config fold-in
  (`collectionId`, `unreadOnly`, `showPreview`, `maxCount`, `daysAhead`, `showAllDay`, `showLocation`,
  `showDetail`, `showAvatar`) and the `Legacy*` properties: removed. Those fields are unknown JSON now
  and are ignored like any other.
- **[S-1053]** *(Retired.)* [S-555]'s read of bare-`uid` IMAP handles: removed.
- **[S-1054]** *(Retired.)* `TokenExpiry.FromUnixSeconds` (the classic `expires_at`) and the
  "imported account" wording in [S-501], [S-505], [S-506], [S-509], [S-525]: removed. An untagged
  secret in `accounts.json` is still refused ([S-506]); that is validation, not a format to accept.

## Settings window: Windows 11 Settings

The settings window is drawn as Windows 11's Settings app (S-1540..S-1559). Owning files:
`Shell/FluentLayout.cs`, `Shell/SettingRow.cs`, `Shell/ColorChoice.cs`, `Shell/Config/ConfigWindow*.cs`,
`Shell/Config/SettingsPage.cs`, `Shell/Config/SettingsSearchIndex.cs`, `Shell/Config/ConfigPages.Uninstall.cs`,
`Services/DataPurge.cs`, `Core/Settings/*` (tests: `SettingsWindowTests`).

- **[S-1540]** Every row is the Community Toolkit `SettingsCard` (groups: `SettingsExpander`), built
  only through `FluentLayout` so every page — and every page added later — gets the look at once:
  header icon, title, secondary-text description, control on the right, card fill and stroke from
  the theme, 4 DIPs between cards, BodyStrong section headings 30 above / 6 below, switches with
  "On"/"Off" to their left, drop-downs at least 200 wide. A card that drills into another page
  (`NavRow`) carries the chevron; one that leaves the window (`ClickRow`) the open-elsewhere mark.
  Mica behind the whole window; the page sits on the same surface as the pane. There is **no status
  bar** (the old "Themia 1.0.0 · N widgets" line is gone). Brushes on settings surfaces come from
  App.xaml styles with ThemeResource, so they follow the window's own light/dark choice.
- **[S-1541]** The page title is a breadcrumb in the Title style above the scrolling cards, still
  while they scroll: a widget's page reads "View › Widget" and the view crumb opens the view. A page
  is a `SettingsPage` (title + optional description + rows); the window draws the title.
- **[S-1542]** "Find a setting" sits centred in the 48-DIP title bar. Its index is read off the pages
  themselves (`SettingRow` on every row's frame; section = the heading above it): every app page,
  view and display page, and one page per widget kind in use (built over a throwaway widget,
  offered for every widget of that kind as "View › Widget"). Built on first use, refreshed with the
  live page on every visit, dropped when the navigation changes shape. Pages are results too.
- **[S-1543]** Matching: lower-cased words, British spellings folded to American on both sides
  ("color" finds "Border colour"); every query word must land in the title, keywords (including a
  drop-down's options: "dark" finds Theme), section, page path or description.
- **[S-1544]** Ranking: field weight × match quality (exact, word, prefix, word prefix, substring,
  small typo — none for ≤3 letters, 1 up to 6, 2 beyond; a short word with a wrong first letter is
  no typo, typos only in title and keywords), plus a bonus when the whole query starts or equals the
  title; a page beats its own rows on its name.
- **[S-1545]** At most 8 suggestions; the same row reached twice is one suggestion.
- **[S-1546]** Choosing a suggestion (or Enter, which takes the best) opens its page; a row is
  brought a third of the way down the page (its expander opened first) and ringed in the accent for
  a moment. "No results" when nothing matches.
- **[S-1547]** Back in the title bar, Alt+Left/Alt+Right and the mouse's back/forward buttons step
  through the user's navigations (`NavHistory`): pushing from an older entry drops the forward
  tail, repeats coalesce, pages that disappear (a deleted widget's) leave the history.
- **[S-1548]** The debug render draws the real window: `--settings-png <Page|Widget:Kind> <out.png>
  [waitMs] [--theme light|dark] [--search text] [--land text] [--open first|name]` (Page includes
  Displays and Views, [S-1835]); the window is as tall as the page,
  Mica is painted as its fallback colour; `--search` also writes the open suggestion list to
  `<out>-search.png`.
- **[S-1549]** Colours are never typed. One control, `ColorChoice` / `FluentLayout.ColorPickerRow`:
  a swatch button naming the colour, opening a picker with "use the default" (where empty means
  follow the theme), the preset swatches in rows of 8 (Windows' 48 accent colours by default; a
  neutral row first for surfaces; a setting may give its own set), and "Custom colour" opening
  WinUI's `ColorPicker` in a dialog with no text entry. The chosen preset carries a check.
- **[S-1550]** Stored format stays `#RRGGBB` (empty = default). Replaced: Appearance's accent,
  widget background and border colour; the calendar colours (Automatic + the six + custom); the
  palette editor's entries (rgb()/hsl() entries open on their colour, are stored as hex once
  changed); the scratchpad ink (the six inks + custom).
- **[S-1551]** The navigation pane is open at 1008 DIPs and wider; narrower it folds away (no
  icon-only rail, as in Settings) behind a button in the title bar next to back. About stays the
  last footer entry ([S-493]).
- **[S-1552]** About ends with an "Uninstall" heading and a danger card. Packaged (has a package
  identity): Themia removes its own package with `PackageManager.RemovePackageAsync` — no capability
  is needed (full trust, medium integrity, own publisher); if that fails, Settings' page for the app
  (`ms-settings:appsfeatures-app`) opens. Not packaged: Installed apps (`ms-settings:appsfeatures`)
  opens and Themia quits.
- **[S-1553]** (Loose builds; packaged see [S-1070].) The confirmation offers, unticked, "Also delete my widgets, settings and accounts";
  ticked, it names the folders: `%APPDATA%\Themia` and `%LOCALAPPDATA%\Themia` (`AppStateStore.FolderName`),
  and nothing else — a missing/relative root or a name that is not a plain folder name deletes
  nothing. They go only after Themia has closed and saved: packaged, in-process just before the
  package is removed; loose, by `Themia.exe --purge-data <pid>`, which waits on the app's and its
  children's process handles, works the folders out again itself, and does nothing unless the id
  is a running copy of the same executable.
- **[S-1554]** The goodbye survey `https://themia.app/goodbye?v=<version>` (escaped; "unknown"
  without one) opens on a confirmed uninstall, as the original installer did on an interactive one.
- **[S-1555]** Nothing is removed or deleted without the dialog's Uninstall; no dialog, no
  uninstall; a debug settings render never uninstalls.
- **[S-1720]** The open page follows changes made elsewhere — snap to grid from the edit toolbar, a
  keyboard shortcut, another window. A page's controls are built from the state and then only
  write, so the window builds the page again when what it shows changed while the window was in the
  background (`SettingsPageSync.NeedsRebuild`): on every state change while the window is not the
  active one, queued and coalesced, the current page's signature is compared with the one it was
  built from (or caught up to when the window last lost the focus) and a difference rebuilds it in
  place, keeping the scroll position. A change while the window is active is the user's own, made
  with the page's controls, which already show it: the page is never rebuilt under the pointer or
  out from under a text box being typed in.
- **[S-1721]** What a page shows (`SettingsPageSync.Signature`): an app page, the design, the
  preferences, the default view for new displays and the views by name; the Views page, for every
  view its name, icon, widgets by kind and label and the displays showing it; the Displays page,
  the connected displays and for each its view, on/off, scale, view bar and the views it can pick from; a widget page, its config, label, surface,
  header and parent. Never where widgets sit, so a drag on the desktop rebuilds nothing. Changes
  that do not raise `StateChanged` (`Mutate(relayout: false)`) are not followed.
- **[S-1722]** The app pages: General, Appearance, Accounts, Displays and Views in the main slot
  ([S-1830]); License and About in the footer. There is no Layout page: its two rows (gap between widgets, snap to grid) are the
  Layout section at the end of Appearance's look, before the motion rows. General has no Data
  section (it held only "open the data folder") and no Developer section.
- **[S-1723]** Every font setting picks through one control, `FontChoice`: the presets, then the
  installed families ([S-827]), each entry drawn in the font it names (a preset in the family it
  maps to, [S-826]; the system entry in the UI's own). The closed box shows the name in the UI font.
- **[S-1724]** No developer mode and nothing behind one: no `DevMode` or `VerboseLogging`
  preference, no Logs page, no developer banner, no `--dev` render switch, and no Web element
  widget (the original's pre-release widget behind developer mode: its view, picker window,
  off-screen WebView2 renderer, saved renders, settings, keys and fixture are gone). The debug-only
  render flags (`--settings-png`, `--picker-png`, ...) are build tooling and stay.
- Not ported / different: karpo's status bar (removed by user request). The SDK's `TitleBar` and
  `BreadcrumbBar` controls are not used (the first crashed an earlier build; the second needs a WinRT
  collection) — equivalents are built from a grid and text blocks.

## Meter framework and memory

The six meters (CPU, memory, disk, battery, network, ping) share one view, `MeterView`, and differ
only in a `MeterPart` per kind (`Widgets/Meters/<Kind>Meter.cs`): where the number comes from, what
the chart plots and how it is coloured. Layout is Core `MeterFace` (tests `MeterFaceTests`).

- **[S-1120]** Every meter has a refresh interval (`GaugeConfig.IntervalSeconds`, 1–60 s; ping
  1–300 s). Defaults: CPU, memory, network 1 s; disk 5 s; battery 15 s; ping 10 s; GPU 2 s. Load
  clamps an out-of-range value and logs it. A new cadence restarts the history, so the fixed-slot
  x-axis never mixes two spacings.
- **[S-1121]** Every meter has a graph span (`GraphSpanSeconds`, one of 30 s, 1, 2, 3, 5, 10, 20,
  30 min, 1 h; load snaps anything else to the nearest). The chart holds span / interval samples,
  2–600 (`MeterTiming.Capacity`), and a chart too narrow for that many 2-DIP slots draws the
  newest it has room for (`MeterTiming.Window`). Defaults: CPU, memory, network, GPU 1 min; disk
  3 min; battery 5 min; ping 20 min (the original's).
- **[S-1122]** A meter's settings page ends with the shared rows: Show history, Chart span, Refresh.
  The kind's own rows (a drive, a host, adapters) come first; CPU, memory, battery and network
  have none.
- **[S-1123]** Pressure colours, as the original: usage accent < 70 %, caution 70–89 %, critical
  ≥ 90 % (CPU, memory); a battery success > 50 %, caution 21–50 %, critical ≤ 20 %,
  accent while charging. Compared in whole percent. They colour the chart (and the battery glyph) —
  never the reading, which stays primary text.
- **[S-1124]** A share is charted against 100 %. A rate (network) is charted against the peak of
  the samples on screen, floor 1 KB/s — not the session's all-time peak, which let one burst
  flatten the trace for the rest of the session. Disk throughput rounds that top up ([S-1821]).
- **[S-1125]** Performance counters go through one AOT-safe `PdhQuery` (English counter paths,
  `SafeHandle`-owned query, every counter closed with it). The GPU sampler uses it too.
- **[S-1126]** The chart (`SparkBand`) draws one or two series: the first filled, the second a
  thinner line (upload, writes). The battery draws its history when Show history is on (the first
  port ignored the setting).
- **[S-1130]** Memory: percent used, from `GlobalMemoryStatusEx` (total less available).
- **[S-1762]** **A system meter shows its label, its reading and its history — nothing else** (user
  requirement): the card's header is the label (a drive's or a host's name where the kind knows
  one), the reading is the number (the battery's glyph before it, [S-1187]), the history is the
  chart. No caption, detail lines, stat tiles, scale line, usage / composition bar, per-core strip
  or breakdown list; the settings that only switched those are gone with them, and so is the code
  that read their data (sensor polls, the process walk, page-file and cache counters, IOPS, the
  battery device, Wi-Fi name and public address). Why a reading is unavailable is its tooltip.
  The ladder (`MeterFace.Plan`, shared by `MeterView` and each GPU panel): a tile Compact both ways
  is the reading alone, filling it; a Wide strip at least Expanded wide puts the reading in a column
  on the left (35–50 % of the width) and the history the full height on the right; everything else
  stacks the reading over the history, which is promised half the height (never under 24 px), the
  reading growing by ramp step up to Display in what is left, and the history taking every pixel
  the reading does not — no empty space at L or XL. With Show history off the reading fills the tile.
- **[S-1823]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1824]** The history sits inside the card's padding: the content inset (16) from both sides
  and the bottom, its left edge in line with the reading's; beside the reading on a strip, the
  column's own inset separates them. `MeterFace.Plan` spends that bottom inset before the history.
  The trace is inset by half its stroke at both ends (`Sparkline.PlotX`), so the newest sample is
  never cut by the chart's edge (it was, at the card's edge and rounded corner, when the chart
  bled). Every meter and every GPU panel draws it this way.
- **[S-1825]** A window still filling stays right-aligned, as Task Manager draws it: the trace grows
  in from the right, one slot per sample, over a hairline baseline that spans the whole chart from
  the first frame, so a young meter reads as a chart with little history, not an empty hole. The
  shared ticker keeps whole-second cadences under a minute (`TickSchedule.Normalise`; it used to
  collapse them all to one second), so a meter set to 5 s samples every 5 s and its chart spans
  its configured span.
- Not ported / different: the original's `visibleMetrics` multi-select and everything it switched
  (removed by [S-1762]). The audit's `MeterLadder` was not built: `MeterFace` and `Disclosure` are
  the ladders.

## Power and Bluetooth

Port of `main`'s Power (`widgets/system/power`, [S-01]..[S-11]) and Bluetooth (`widgets/system/bluetooth`,
[S-01]..[S-16]) plus the round-3 ladder. Core: `PowerActionRules`, `PowerArm`, `PowerTileLayout`,
`PowerStrip`, `BluetoothRules`, `BluetoothLayout`; tests `PowerActionTests`, `BluetoothLadderTests`.

- **[S-1240]** Power is the original's **actions tile** (shut down, restart, sleep, hibernate, lock,
  sign out, display off), not a status readout. The former status facts live on as a strip
  ([S-1250]). One widget, no flag.
- **[S-1241]** `PowerAction` is a closed enum at every boundary, stored by name. On load
  `PowerConfig.Repair` drops undefined numbers and duplicates and says so; an unknown name fails
  the state load like every other enum.
- **[S-1242]** `Actions` is the ordered list of buttons; default shut down, restart, sleep, lock
  (orig. [S-02]). Settings list every action once — chosen ones first in tile order, each with a
  switch and move up/down.
- **[S-1243]** Win32 only, no `shutdown.exe` (orig. [S-03]/[S-09]): `ExitWindowsEx`
  (`EWX_POWEROFF`/`EWX_REBOOT`/`EWX_LOGOFF` | `EWX_FORCEIFHUNG`, planned reason), `SetSuspendState(h, 0, 0)`,
  `LockWorkStation`, `SC_MONITORPOWER` broadcast via `SendMessageTimeout`. `SeShutdownPrivilege` is
  enabled per call for shut down, restart, sleep and hibernate; `ERROR_NOT_ALL_ASSIGNED` after a
  "successful" `AdjustTokenPrivileges` is "not allowed". The token is a `SafeHandle`. The plan per
  action is decided in Core (`PowerActionRules.Plan`); `Win32PowerActions` only executes it, on a
  worker thread (sleep blocks until resume, orig. [S-04]); one action at a time.
- **[S-1244]** `ShowLabels` (default on) puts names under icons where the tile has room; set in the
  settings only ([S-1746]; the original's context toggle is gone).
- **[S-1245]** With `Confirm` on (default), shut down, restart and sign out arm on the first press
  ("Confirm" in the critical colour) and run on the second; sleep, hibernate, lock and display-off
  never arm (orig. [S-05]). `Confirm` off runs everything on one press.
- **[S-1246]** A failed action shows "Could not <verb>: <reason>" as the tile's first status line in
  the critical colour and logs a warning (WinUI has no toast pipeline; orig. [S-07]).
- **[S-1247]** One armed tile at a time; it disarms after 5 s, on pointer exit, on a config change,
  or when another arms (orig. [S-06]). The 5 s window is checked by `PowerArm` against the press
  time, so safety does not depend on the repaint timer. **New:** a second press within 400 ms of
  arming is ignored — an accidental double-click cannot end the session.
- **[S-1248]** Ladder (`PowerTileLayout`): every button labelled (cell ≥ 64×56), else every button as
  an icon (≥ 32×28), else the first ones that fit; the grid picks the squarest cells (cell height
  capped at 132). The status strip only takes room that keeps the buttons on the same rung. XS/S:
  icons only (2×2 for four). M/Tall/L: strip under the buttons. Wide: strip beside.
- **[S-1249]** Huge×Expanded (XL): buttons left, a side panel right with every status fact as a row
  and the machine's power plans (`PowerEnumerate`), the active one marked. Read-only.
- **[S-1250]** Status strip facts, in drop order: source (with charge on a laptop), plan
  (`ShowPlan`), time left while discharging (`ShowTimeRemaining`), battery saver (`ShowBatterySaver`,
  battery only). Unreadable power state is one line saying so.
- **[S-1251]** A debug render's cards are previews (`IWidgetSurface.IsPreview`, from the render's
  card host): the power view's backend is `InertPowerActions` there. The view starts inert and switches to
  the real backend only when bound to a non-preview surface (orig. [S-08] demo mode).
- **[S-1252]** No actions chosen is the Set up button ([S-1862], orig. [S-11]).
- **[S-1253]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1254]** Bluetooth ladder (`BluetoothLayout`): the list takes every whole row the tile has
  (snapped, the rest scrolls); `MaxCount` is a ceiling (1–30, default 20), not a target. Status word
  from a 260 px column; battery bar from a 400 px column (critical at ≤ 20%); "2 connected" in the
  header from Expanded width; two columns at Huge width when one cannot hold the list.
- **[S-1255]** Regular×Expanded and up: two-line rows — name, then kind · connection · battery ·
  signal (each only when enabled and reported; a wide row keeps battery and signal in columns).
- **[S-1256]** `ShowSignal` (default off): RSSI from the association endpoint's
  `System.Devices.Aep.SignalStrength`, plus a passive 3 s LE advertisement scan per refresh matched by
  device address. Shown as "-67 dBm"; no reading or an implausible one (≥ 0, < -127) is no column
  (orig. [S-16]).
- **[S-1257]** `ShowIcon` and `ShowConnection` (default on) are the original's `type` and
  `connection` metrics; the name is always shown (orig. [S-14]/[S-15]).
- **[S-1258]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1259]** Context menu "Refresh" enumerates now (orig. [S-09]).

Not ported: Bluetooth's 1–30 s refresh choices (kept 5–300 s: enumeration and GATT reads take
seconds); per-metric `visibleMetrics` ordering (fixed ladder order).

## Habits

Code: `Widgets/HabitTrackerView*.cs`, `Shell/Config/WidgetSettings.HabitTracker.cs`,
`Services/Habits/HabitPool.cs`, Core `Habits/HabitLists.cs`, `Habits/HabitListStore.cs`,
`Habits/HabitLayout.cs`, `Widgets/HabitPeriods.cs`, `Widgets/HabitRules.cs`. Tests:
`HabitRulesTests`, `HabitListsTests`, `HabitLayoutTests`, `TrackerPersistenceTests` (S1420/S1422).
Ports the original `widgets/others/habit-tracker/SPEC.md`.

- **[S-1420]** Habits are device-wide **lists** (original S-18), not per-widget data. The pool lives
  in `habits.json` beside the state file: `schemaVersion` envelope, every list/habit/key validated
  (malformed entries dropped and counted, duplicate ids keep the first), a corrupt or newer file set
  aside and never overwritten, an unreadable one leaves the pool read-only. Saves are atomic (temp +
  move), single-writer (one gate) and coalesced (one more write of the latest snapshot); the UI
  thread is the only mutator and every change goes through `HabitPool.Update`. A habit owns its
  log, so deleting it takes its history (original S-05/S-20 by construction). The widget config
  holds only `ListId` plus its own settings; with none chosen it takes the first list, creating
  one only when there is none; a deleted list is not replaced ("pick a list"). Two widgets on one
  list see each other's ticks at once (`HabitPool.Changed`). The header title is the list's name.
- **[S-1421]** Only the box ticks a habit (original S-02); name and run never do. The box is drawn
  (not a stock `CheckBox`, whose template clipped its glyph to a dash in both states): empty when
  not done, accent-filled tick when done. A tap reads the clock once and toggles that period;
  un-ticking removes every copy of the key.
- **[S-1422]** Reset period per widget (original S-07/S-08): hourly, daily, weekly (ISO), monthly,
  custom 1–1440 min in epoch-anchored windows. Keys: `YYYY-MM-DD`, `YYYY-MM-DD-Hhh`, `YYYY-Www`,
  `YYYY-MM`, `C{n}m-{window}`. Steps back are calendar steps for day/week/month; an hour steps from
  its own first instant, so a skipped spring hour is never visited and a repeated autumn hour is
  one period. The widget wakes every 15 s (hourly/custom) or 60 s and redraws only on a new period.
  The custom interval is typed and refused (not coerced) when out of range; the config is repaired
  to its ranges on load.
- **[S-1423]** Streak = consecutive done periods ending now; the current period not yet done does
  not break it; a gap does. Future, malformed and other-cadence keys are never counted.
- **[S-1424]** Best run = longest consecutive run anywhere in the log under the cadence, using the
  same step as the streak.
- **[S-1425]** Habits are added, renamed, moved and deleted **in the widget** (original S-13/14/16/22/23):
  an "Add a habit" row at the foot (always shown for an empty list; Enter keeps it open for the
  next habit); rename by double-clicking the name, F2 or the row menu (Enter/leaving commits, Escape
  cancels, blank or unchanged commits nothing, the id and log never change); the row menu (shared
  `ItemMenu`) has Done/Not done, Rename, Move up/Move down (disabled at the ends), Delete (danger,
  no dialog). Drag-to-reorder is ported in [S-1621]; Move up/down stay for the keyboard.
  The settings page holds no habit editor (original S-23).
- **[S-1426]** History dots (0–31, default 7): oldest left, the current period right, from the same
  step as the streak; each dot's tooltip names its period in the cadence's units ("Week of Monday
  28 Sep 2026", "14:00, …", "September 2026"). A narrow tile shows fewer, dropping the oldest; the
  name never yields to them.
- **[S-1427]** Ladder (`HabitLayout.Plan`): XS box + name, two rows → S + run (number) → M + dots
  and add row → Wide + run in words and a 7-period rate per row → Tall + summary strip (done this
  period, rate, bar) and a completion chart (one bar per past period, done/total) in the height the
  rows leave → L all of that → XL + best run per row. Rows are 40px (32 on a Compact side); the
  viewport holds whole rows and the rest scroll (no chart then). The header shows `done/total`.
- **[S-1428]** Keyboard (original S-17): a click in the widget takes the shell's keyboard lease
  ([S-809]) and a clicked row takes the focus:
  Up/Down clamp, Home/End, Tab/Shift+Tab wrap, Enter ticks, F2 renames, Delete deletes, Escape gives
  the keyboard back. In an editor Tab commits and opens the next row's editor, falling into the add
  row past the last. Not verified live: needs a manual pass on the desktop.
- **[S-1429]** Settings: the list picker/editor (pick, rename as typed, habit count, add, delete
  — always confirmed, empty or not, original S-24), Resets, Interval (custom only), Show streaks,
  History. Renaming a list retitles every widget showing it.
- Not ported: original S-10 compact header default (no compact header mode here), S-12 equal-slice
  rows (Fluent fixed rows, S-213), S-13 drag reorder (above), S-21 legacy migration (no backwards
  compatibility).

## Calendar and contacts: the whole tile

The Calendar and Contacts ladders ([S-660] API), the Outlook hand-off and the key maps. Rules in
`Themia.Core/Office/CalendarFit.cs`, `CalendarLinks.cs`, `ContactRules.cs` (tests `CalendarFitTests`,
`ContactsFitTests`, `S12xx_`); views `CalendarView.Fit.cs` (new), `CalendarView*.cs`, `ContactsView.cs`.

- **[S-1260]** The calendar's layout is chosen from the configured view and the slot
  (`CalendarFit.Layout`): a slot under 150 tall and at least 360 wide (and 56 tall) is a **strip**,
  whatever the view; else under 160 wide or 88 tall the **next** tile; a month under 154 × 112, a
  week under 280 × 96 and a day under 140 × 112 the **agenda** (S-574); a month at least 300 tall
  and 1.6× as tall as wide puts the **agenda below**; a month at least 720 × 400 the **agenda
  beside**. Unmeasured: the configured view. By tier: XS next, S agenda, M month (dots), Wide strip,
  Tall month over agenda, L month (chips), XL month beside agenda. Next and agenda do not page.
- **[S-1261]** Next (XS): the first agenda day's event under way or next (a timed one before an
  all-day one or a birthday), its time ("Now" in the accent, "All day", "Birthday"; prefixed with the
  day when not today), its title (a second line, or two caption lines, before anything else), then
  "+N more today". Nothing coming up: the weekday, the date and "Nothing coming up". Click opens it.
- **[S-1262]** Strip: 3–5 day columns of ≥112 px from the anchor (today while following it), or the
  configured week's 7; each has a heading ("Today" in the accent, "Tue 6") and one-line entries
  ("09:00 Standup", the time stacked above in columns under 100 px) as many as fit, then "+N more".
  It pages by its own width; title "4 Oct – 7 Oct".
- **[S-1263]** Month with agenda: below, the month's rows are square to the column (24–32 px, dots)
  and the list takes the rest; beside, the list is 280 px with a hairline. The list is the month's
  fetched events from today (from the 1st when paged away), so no extra fetch.
- **[S-1264]** Large month: weekday names ("Mon") from 64 px columns; month chips lead with their
  start time from 100 px cells (classic S-44); chips get a 20 px line in cells ≥90 tall; at height
  class Expanded or more only the weeks holding a day of the month are drawn (4–6).
- **[S-1265]** Week columns ≥64 px spell the weekday; ≥100 px a timed chip shows start–end and its
  location under the title.
- **[S-1266]** Day: 32 px hours when that shows 07–22 unscrolled (480–600 px), else 40; a block of
  ≥48 px adds a "location · organizer" line.
- **[S-1267]** Agenda: under 128 tall the first group's "Today" heading is dropped (any other day keeps
  its heading); at ≥480 px a row puts location and organizer in columns beside the title; at ≥720 px
  the days flow into two columns (whole days, balanced by rows).
- **[S-1268]** Opening an event: a Microsoft account's event opens `ms-outlook://events/{id}` when
  Outlook's protocol is registered (one cached check shared with Mail, `ShellLauncher.OutlookInstalled`),
  else the provider's https page; the provider comes from the event's account, never the link's
  domain. Only https (no user info) and `ms-outlook://events/<percent-encoded>` pass
  `CalendarLinks.IsLaunchable`, re-checked by `ShellLauncher.OpenCalendarLink`.
- **[S-1269]** Keys (classic S-31..34), `CalendarView.HandleKey(WidgetKey)`: Left/Right page, Up/Down
  move the cursor day a week (month and week layouts; the month outlines it), Home today, Enter opens
  the cursor day's first event (next event where nothing pages). Not yet reachable: the shell's
  keyboard lease does not exist; this is what it will call.
- **[S-1270]** Contacts list rows say what the list's area allows (`ContactRules.ListDetail`, on the
  tile less its search box): width Compact → the name only (32 px rows, no avatar); height Compact →
  the role, else the address/number; Regular → the role plus the address (else number) when three
  such rows fit, else the role; Expanded+ → role, address and number, each address/number with its
  glyph. Every row of a render is one height. Rows flow into columns of 240 px (up to 4), row-major;
  avatars from 180 px columns; the viewport stops at a whole row.
- **[S-1271]** Compact rows are one line: a 24 px avatar, the name, and the one field right-aligned
  (columns ≥240 px; the name keeps the larger share).
- **[S-1272]** Details: the Name cell carries a 20 px avatar when the table is ≥360 px wide.
- **[S-1273]** Grid cards are one height (88 px; whole rows only); at width and height class Expanded+
  they are 140 px wide with a 56 px avatar and the address as a second line.
- **[S-1274]** XL (width Huge, height Expanded+) with someone selected: a 300 px detail pane beside
  the list (not the grid) — 64 px avatar, name, role, then Send email (the address), Copy email, Call
  (the number), Message, Copy phone, Copy name. Selecting re-lays the list around it.
- **[S-1275]** Search box: always from 220 px tall; from 128 a header search button opens it (it folds
  away again when left empty); below 128 none. A typed query keeps the box open.
- **[S-1276]** The People count is a number box (`FluentLayout.Stepper`): at least 5, steps of 5, no
  upper bound (S-583); text that is not a number is overwritten with the last good value.
- **[S-1277]** Keys (classic S-31/32), `ContactsView.HandleKey`: Up/Down move the selection through the
  people on screen (from none, Down picks the first, Up the last), Home/End jump, Escape clears, Enter
  writes (else calls). A click anywhere else — another widget, the desktop, another app — ends the
  keyboard lease the widget holds from the selecting click, which arrives as Escape and clears the
  selection (classic S-38; [S-807]).
- **[S-1278]** vCard 2.1 quoted-printable: `ENCODING=QUOTED-PRINTABLE` (or the bare parameter) values
  are decoded in UTF-8 or Latin-1 (`CHARSET`), across `=` soft line breaks; bare 2.1 parameters join
  `TYPE` (so `TEL;CELL;PREF` is preferred). Lenient: a stray `=` stays.
- **[S-1279]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

## Mail and OneDrive: the whole tile

- **[S-1280]** Mail's ladder (`EmailView.Lay`, rules in `MailLayout`): the first row always; the row
  view's column titles from 140px of height; then as many whole rows as fit. Stacked rows shed account
  tag, preview, extra preview lines and time as before ([S-556]); a wide strip and a big tile go one
  line per message ([S-1283]); the envelope joins from 300px ([S-1282]); the largest tile adds the
  provider, quick actions and the folder summary ([S-1284], [S-1285]).
- **[S-1281]** Bottom scroll fade, shared (`Widgets/ScrollFade` + Core `ListFade`): while the list
  continues below the viewport, rows within 48px of its bottom edge fade to 0.35 opacity (the Tasks
  depth and floor, [S-781]); scrolled to the end every row is whole. Opacity per row, never a
  gradient over the list (the card can be transparent). Event-driven: scroll, size, one layout pass
  after a rebuild. Used by Mail and OneDrive.
- **[S-1282]** Envelope: a closed envelope (accent) in front of unread mail, an open one (tertiary)
  in front of read mail, from 300px wide, in every view; the unread accent bar stays.
- **[S-1283]** One-line rows (list and compact views) from 640px wide, or from 420px on a strip under
  220px tall: sender column (24% of the line, 96-200px), subject with the preview after an en dash
  (preview from 440px), account from 560px, time. The row view is unchanged. A one-line row shows
  one preview line whatever `PreviewLines` says (that setting shapes the stacked rows).
- **[S-1284]** From 720px the account tag names its provider ("Work (Microsoft)"); the tooltip always
  does. On one-line rows at that width the pointer swaps a row's time for quick actions: mark read
  or unread, archive (not in Junk), delete — only where the account allows changes ([S-544]).
- **[S-1285]** Folder summary under the list on a tile ≥ 480px of content height: "4 unread of 1,661
  in Inbox" (or "… in Inbox, all read"), summed over the accounts from the counts each probe reports
  (`MailAccountOutcome.Folder`). Hidden while any answering account has not reported its counts — a
  total missing one account would be wrong, not smaller. Not a ladder rung: it never moves the
  fetch count.
- **[S-1286]** The count setting is a floor: the feed asks for `max(MaxCount, rows that fit)`, at most
  50, so a big tile is never half empty. Resizing refetches only when that number changes.
- **[S-1287]** Row view from 700px: when the user's columns leave Subject as the only flexible column
  and it would take more than 40% of the line, Preview joins after Subject. A chosen Preview keeps
  the user's order; without Subject nothing is added.
- **[S-1288]** List keyboard navigation, shared (Core `ListKeys`, App `RowFocus`): a click focuses a
  row (an inset 1.5px accent ring every row reserves, so nothing moves); Up/Down step, Page Up/Down
  jump 10, Home/End go to the ends, Enter opens, Delete deletes (Mail, where the account allows),
  F2 renames (lists that rename), Escape lets go. With no row focused a movement key lands on an end.
  The focus follows its item across redraws and moves to the row that took its place when the item
  left. Keys reach a view only once the shell's keyboard lease (WP11) makes the surface take the
  keyboard; the views' `KeyDown` handlers are in place for it. Clearing the focus on a click outside
  or on app blur is the lease's job.
- **[S-1289]** Mail's header carries a sort button (from 200px wide) that turns the list round and
  saves `SortOrder`, like the menu's toggle; its tooltip says the current order.
- **[S-1290]** OneDrive's ladder (`OneDriveView.Lay`, rules in `DriveLayout`): the back row inside a
  folder; then, where the text has 216px, one line per item with detail columns beside the name —
  Modified, then Size (a folder's item count), then Modified by, then Location (recent files only) —
  each added while the name keeps 120px, a column that misses closing the ladder, and shown in
  Explorer's order with Size last at the right edge; column titles from
  280px of height; a narrow tile with height keeps the details as a second line; a narrow short one
  shows names only. Then as many whole rows as fit.
- **[S-1291]** `ClickMode` (`ItemClickMode`, shared with the file explorer): files open on a double
  click (default) or a single click; folders always open on a single click. In double-click mode a
  single click focuses the row. Item menu: Open / Open in browser, Copy link.
- **[S-1292]** OneDrive's item count is a floor too: `max(FileCount, rows that fit)`, 5 to 50.
- **[S-1293]** Graph `$select` adds `lastModifiedBy` and `parentReference`: who changed an item, the
  folder it is in (`/drive/root:/Work/Q%203` reads "Work › Q 3", the top "My files", any other path
  nothing) and a folder's child count ("1 item", "12 items").
- **[S-1294]** On a tile ≥ 480px of content height the drive's quota sits under the list ("12 GB of
  1.0 TB used", `GET /me/drive?$select=quota`), cached 30 min per account and single-flight in
  `DriveService`; a failure is logged and means no footer, never an error on the widget.
- **[S-1295]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported, deliberately: OneDrive image thumbnails (an XL-only extra, not in the original; every
row would cost a download). Main's low-level keyboard hook for list focus (the lease replaces it).

## Clipboard, Countdown and Mood: the whole tile

Code: `Widgets/ClipboardView.cs`, `Widgets/CountdownView.cs`, `Widgets/MoodTrackerView*.cs`,
`Widgets/ItemMenu.cs` (submenus), `Shell/Config/WidgetSettings.{Clipboard,Countdown}.cs`,
Core `Widgets/ClipboardLayout.cs`, `ClipboardHistory.cs`,
`CountdownLayout.cs`, `MoodLayout.cs`, `MoodInsights.cs`, `ItemCursor.cs`,
`Model/Configs/CountdownConfig.cs`. Tests: `TrackersLayoutTests` (S1440..S1457).

- **[S-1440]** Clipboard keeps 1–200 entries, default 50 (original `maxItems`); the slider steps by 1.
- **[S-1441]** A row's preview is up to the plan's lines: the entry's own line breaks kept, blank
  lines dropped, whitespace collapsed inside a line, " …" when more lines follow; wrapped and
  trimmed by the row. One line is exactly the old single-line preview.
- **[S-1442]** Each row is marked by what it looks like (`ClipboardLayout.Classify`): link (http/https/
  ftp), address, number (leading digit/sign), colour (`#hex`, `rgb()`, `hsl()` → a swatch of it),
  several lines, else text; the current entry keeps the accent tick. Display only, never opened.
- **[S-1443]** Clipboard ladder (`ClipboardLayout.Plan`): XS/S one line, compact rows → M + time
  column → Wide 2-line previews + time → Tall 2 lines + detail line ("4m · 32 characters")
  → L/XL 3 lines + detail + "N of capacity" footer. A preview card never subscribes to the
  real clipboard.
- **[S-1444]** Clipboard row menu (shared `ItemMenu`): Copy, Remove from history, Clear history…
  (confirmed through `FluentLayout.ConfirmAsync`, like the header bin). `HandleKey`: Up/Down/Home/End
  cursor, Enter copies, Escape drops it — through the shell's keyboard lease ([S-800]..[S-812]),
  `ClipboardView : IWidgetKeys`; needs a live pass.
- **[S-1445]** Countdown timer menu on every row and on the hero (original C-60): Remove… (confirmed).
  The display format is chosen per timer in the settings only ([S-1746]); original C-61 is moot.
- **[S-1446]** The featured timer is the soonest still ahead, else the most recently passed; the
  "Next: <label> in <auto>" line names the soonest other upcoming one. The featured timer is not
  listed again; when it passes, the next tick re-lays the tile.
- **[S-1447]** Countdown ladder (`CountdownLayout.Plan`): XS/S the hero alone (+ next line); one timer
  is the hero at any size; Wide hero left, list right; M/Tall/L/XL hero on top, the others as rows
  under it. The hero readout takes the height the rows leave (`FluentType.Hero`, sized for the
  format's widest text); when every row fits they sit at the foot and the hero is centred above.
  Short or narrow tiles use compact rows; a compact list ≥260 wide shows the date as a column.
- **[S-1448]** `CountdownTimer.Created` (set when added in settings): L/XL draw a progress bar under
  the hero, XL under each row too. Unknown (unset) or not before the target: no bar.
- **[S-1449]** Mood faces (original S-01): five drawn faces (circle, eyes, mouth from frown to smile)
  on the heatmap's wash; the number and name are the tooltip and accessible name.
- **[S-1450]** A day's tooltip is its sentence plus one line per entry, oldest first, with the
  thought ("21:40 · Good: long walk") — the original S-12 hover.
- **[S-1451]** Right-click a logged day: its date, then each entry, with "Delete this entry" one
  submenu down (original S-12); an empty day opens no menu (the card's opens).
- **[S-1452]** Mood stacked ladder (`MoodLayout.Plan`, width < Expanded): faces always → one map row
  → thought box (H ≥ Regular) → footer (H ≥ Regular) → the map takes the rest, preferring three weeks
  to bigger squares (fixes M never showing the thought box); labels and digits from 18px squares.
- **[S-1453]** Wide tiles (W ≥ Expanded) split: a left pane (faces, thought, footer, insights) and the
  map beside it with weeks as columns, weekdays as rows, weekday names down the left and month names
  on top; weeks = what fits, capped by the setting; the left pane takes the rest of the width. A
  month span is always shown whole (squares shrink; [S-1573]).
- **[S-1454]** Insights (split, H ≥ Regular): "How it went" (count per level) and "By weekday"
  (average per weekday), both over the map's window, then (when the pane still has room, XL)
  "In this window": days logged of days elapsed, best run, thoughts written; L/XL list recent
  thoughts under the map
  ("Tue 29 Sep · Good: …"), as many whole rows as fit.
- **[S-1455]** Mood `HandleKey`: Left/Right/Home/End move a cursor over the faces (focus stroke),
  Enter logs that face, Escape drops it. For the keyboard lease; not live-verified.
- **[S-1456]** `ItemMenuEntry.Children` makes an entry a submenu (`MenuFlyoutSubItem`), separators
  collapse inside it as at the top level.
- **[S-1457]** `ItemCursor.Move`: the shared list cursor (Up/Left back, Down/Right forward, clamped;
  Home/End; Escape clears; no cursor + forward = first, + back = last).

Not ported, deliberately: clipboard pin (an optional extra the audit listed, not in the original);
Countdown keyboard (the original had none); Countdown C-61 keep-open (above). Mood `storageMode` stays
gone (one source). A Month span on an XS/S tile can need more height than it has (six rows at the
minimum square); it then clips at the bottom.

## CPU and processes

- **[S-1140]** CPU: percent of all cores (`GetSystemTimes` delta, shared per tick [S-643]); the reading
  and its history only ([S-1762]). The clock, per-core strip, sensors, totals and top processes of the
  earlier ladder ([S-1141], [S-1142], [S-1145]..[S-1150]) are removed with their settings.
- **[S-1143]** Uptime wording (`CpuRules.Uptime`, used by the power tile), two largest units:
  `3d 4h`, `2h 5m`, `7m`.
- **[S-1144]** Process and thread totals (`CpuRules.Totals`), the process list's footer:
  "312 processes · 4,210 threads".
- **[S-1145]** Process names in the process list are friendly (`ProcessLabels`: the version-info
  description, resolved off the UI thread once per pid + image; the image name until then or when
  there is none). One process walk per 2 s is shared by every reader (`ProcessList.Recent`).
- **[S-1151]** Processes: whole rows (`Disclosure.TakeRows`) up to a ceiling 1–30 (default 20), so a bigger
  tile shows more. Laid out from `IWidgetSizeAware`, no private `SizeChanged`.
- **[S-1152]** Processes columns (`ProcessTable.Columns`): the ranked figure always, then the other figure,
  threads, pid, each while the name keeps 96 px; a step that does not fit ends the ladder. Keeps [S-648]'s
  232 px boundary for the second figure; under 160 px figures drop their reserved width and the "by CPU" label goes.
- **[S-1153]** Processes at H ≥ Expanded: column headers (ranked one stronger) and a footer
  "312 processes · 4,210 threads" (ShowTotals); at Expanded × Expanded a hairline share bar per row
  (CPU share, or working set over physical memory).
- **[S-1154]** Re-rank in place: clicking the CPU / Memory heading (or the setting); persisted
  through `RequestSave`. No card-menu entry ([S-1746]).
- **[S-1155]** Processes settings: Rank by, Most processes (1–30), Columns: Memory, Threads, Process ID, Totals.
- Not ported: the original's per-core grid ([S-1762]: the meter is its reading and history).

## Displays (WP2)

The display watcher, stable monitor identity, the per-display switch and scale, and the geometry
model that keeps widgets still when the taskbar changes (S-840..S-859).

- **[S-840]** The grid and every stored fraction are laid out on the **whole monitor**, taskbar
  included. The surface window covers the monitor's bounds exactly ([S-23]); `Metrics` is its client
  rect in DIPs; the snap grid overlay spans it. Resizing, moving or auto-hiding the taskbar therefore
  never shifts or rescales a widget: rendering ignores the work area entirely. Was: the window and
  the fractions followed the work area, so every taskbar change moved every widget.
- **[S-841]** The **work area** is the region *new* geometry may use: `ScreenMetrics.Allowed`, the
  work area as a fraction of the monitor (`GridGeometry.AllowedArea`). Placement, drag, resize, paste,
  drop and duplicate settle into it, so nothing is put under the taskbar; a rect that does not fit
  shrinks to it, one outside slides in keeping its size, and one dragged against the taskbar stops
  flush with it. A widget the taskbar later grows over **stays where it is**: load validation clamps
  to the whole screen only, and nothing re-clamps on a display change. An unusable work area
  (non-finite, empty, off the monitor) allows the whole screen. An auto-hidden taskbar reserves
  nothing, so the whole screen is allowed.
- **[S-842]** A display's id is its monitor **device interface path** (from
  `DISPLAYCONFIG_TARGET_DEVICE_NAME.monitorDevicePath`, lower-cased), never `\\.\DISPLAYn`, which is an
  ordinal Windows re-deals on every topology change. Every per-display setting (`MonitorViews`,
  `DisabledMonitors`, `MonitorScales`, `ViewBars`) is keyed by it. The DISPLAYCONFIG structs match the
  SDK byte for byte (a `LUID` is two 32-bit halves, never a `ulong`, which pads the header to 24 bytes
  and gets every lookup refused with `ERROR_INVALID_PARAMETER`); their sizes are checked and a
  mismatch, a per-path lookup failure or a failed query is logged with its Win32 code.
  `ERROR_INSUFFICIENT_BUFFER` (topology changed between sizing and querying) re-sizes and retries.
- **[S-843]** A monitor with no device path (a virtual or remote display, or a panel still waking)
  falls back to its GDI name, flagged unresolved and logged. An unresolved id is never *given* a view
  assignment: it shows the active view until it resolves, so a waking panel does not store a binding
  under a key the next display change abandons. One that already has a view (a permanently path-less
  display) keeps it. When the path appears, the same pass re-keys the fallback to the real id before
  planning (`DisplayPlan.Rekeys` / `AdoptResolvedIds` / `SurfaceRekeys`): whatever is stored under the
  ordinal (view, on/off, scale, view bar) moves to the path, replacing the path's own entry (it is what
  the screen showed), an ordinal with nothing stored moves nothing, and the surface is re-keyed in
  place, so the screen keeps its view and its widgets are not restarted.
- **[S-844]** One display pass (`AppHost.ReconcileDisplays`, plan in `DisplayPlan.Reconcile`) builds,
  closes and updates surfaces, matched by stable id, so a surface follows its panel whatever order
  Windows enumerates them in, and a surviving surface keeps its cards (a taskbar change restarts no
  widget). A surface whose window died is closed and rebuilt. Startup, display messages, the
  per-display switch and an import all go through it.
- **[S-845]** Per-display switch: a display in `DisabledMonitors` gets no surface, so its whole screen
  passes through to the desktop. The last display Themia is on cannot be switched off (the toggle is
  disabled and the host refuses). If every connected display is off anyway (the only one on was
  unplugged), the primary is shown without changing the setting.
- **[S-846]** An empty monitor enumeration builds nothing and closes nothing: it is a driver
  mid-change, and the next display message runs the pass again. No phantom fallback surface.
- **[S-847]** `DefaultViewForNewDisplays` (General page, "New displays show"): a display without a
  view shows that view, shared rather than claimed. Unset ("The last view shown"), it shows the
  active view: the last one the user showed anywhere ([S-19]). Was: unset gave each new display an
  empty view of its own.
- **[S-848]** The watcher is the command window: `WM_DISPLAYCHANGE`, `WM_SETTINGCHANGE` with
  `SPI_SETWORKAREA`, `WM_DPICHANGED` and Explorer's `TaskbarCreated` each request a pass. Requests are
  **coalesced, not timed**: the first queues one pass at low dispatcher priority, behind the rest of
  the burst; a later message queues the next. It is subscribed before the first surface is built, so
  a change during startup is heard.
- **[S-849]** Each pass also checks every surface's window rect against its monitor and re-places one
  that moved. A display-scale change resizes windows to a rect Windows scales from the old one; the
  surface's rasterization-scale change requests a pass so it is put back over its monitor.
- **[S-850]** Per-display **widget scale** (`MonitorScales`, 50–200 %, 100 % stored as no entry)
  magnifies widget content only: the card's cell stays where it is and the same size, the content is
  laid out at cell ÷ scale and render-scaled back up, so a view discloses less at a bigger scale
  ([S-663]). The app-wide widget scale (`Design.UiScale`) multiplies in through the same path.
- **[S-851]** The hit region never covers the taskbar: it is cut to the work area in normal mode,
  and is the work area itself (not the whole window) in edit mode. A widget the taskbar has grown
  over is clipped there (`SetWindowRgn` clips painting as well as input) rather than moved. This
  matters while Show Desktop lifts the surface above the topmost band ([S-33]), when the region is
  all that keeps the taskbar clickable.
- **[S-852]** The edit toolbar and the placement hint sit inside the work area (margins from
  `GridGeometry.AllowedPixels`), so a taskbar on the top edge does not cover them.
- **[S-853]** The Displays page ([S-1832]) lists **every connected display**, switched off or not,
  and each display's group has: Show Themia on this display, Shows (view), Widget scale, and
  Resolution (with the work area the taskbar leaves), then its view bar ([S-916]). The group is
  headed by the monitor's name ("(primary)" on the primary) over its resolution; the id is never shown.

Not ported, deliberately: the original's legacy ordinal-key migration ([MI-03]/[MI-04]; no
compatibility, by decision); its ~1 s work-area self-heal sweep ([DW-04]), which polls, replaced by
[S-848]'s messages plus [S-849]'s check on every pass; its adoption sequence counter ([MI-07]): the
pass runs synchronously on the UI thread, so there is no await for an older adoption to win across.

## Notes: note store and markdown editor

Code: Core `Notes/NoteStore.cs`, `Notes/NoteMarkdown.cs`, `Notes/NoteEditing.cs`,
`Notes/NoteHistory.cs`, `Notes/NoteText.cs`, `Notes/NotesLayout.cs`; App `Services/Notes/NotePool.cs`,
`Widgets/MarkdownEditor.cs`, `Widgets/NotesView.cs` (+ `.List.cs`), `Shell/Config/WidgetSettings.Notes.cs`,
Tests: `NoteStoreTests`, `NoteMarkdownTests`, `NoteEditingTests`.
Ports the original `widgets/others/notes` (SPEC S-01..S-38, `src/lib.rs`, `markdown.ts`, `editing.ts`,
`history.ts` and their tests). Replaces the old one-string-in-config note directly (no migration).

- **[S-1340]** A note is a document the app owns: `notes\<id>.md` (the markdown, byte for byte) plus
  `notes\index.json` (`schemaVersion` + `{id, title, updatedAt}` rows) beside the state file
  (original data layer). The widget config holds only `NoteId`, so a note outlives its widget, view
  and layout, and two widgets can show one note.
- **[S-1341]** Atomic and single-writer: every file is written to a unique temp sibling and moved
  over the target; every index read-modify-write runs under one lock; the index is read once and kept
  in memory (one process, one writer). A body is written before the index stamp, so a failed index
  write never advertises a save that did not land (original S-30).
- **[S-1342]** Ids are exactly 32 lowercase hex digits (`Guid "n"`), checked by shape before any path
  is built, so a hand-edited config can never name a file outside the folder. A write, rename or
  delete of a note not in the index does nothing (`NotFound`): no resurrection (original S-31).
- **[S-1343]** Index trust boundary (`NoteStore.ParseIndex`): absent → bodies adopted silently (first
  run); not JSON / no or another `schemaVersion` → set aside as `index.json.corrupt-<stamp>` and every
  `<id>.md` adopted with title "Untitled", ids (so every widget's link) and text kept; malformed rows
  dropped and counted; rows whose body is gone pruned (original S-33).
- **[S-1344]** Titles are one line (control characters → spaces), trimmed, at most 200 characters,
  blank → "Untitled". List order is title (case-insensitive) then id, never date. A new note is named
  uniquely ("Note", "Note 2", …).
- **[S-1345]** Reading tells "missing" from "empty" (`NoteReadStatus`), so emptiness is never saved
  over a note that is gone.
- **[S-1346]** A desktop widget with no `NoteId` creates a note of its own on first show (named after
  the widget's label, else "Note") and saves the id. A preview card (a debug render) creates nothing: it
  shows an empty draft that is never saved (original S-32).
- **[S-1347]** A widget whose note was deleted (anywhere) says "This note was deleted" and where to
  pick another (settings, or the list at XL); it is never handed another note, and a pending edit to
  it is dropped (original S-36).
- **[S-1348]** One `NotePool` per process: a change lands in memory at once and raises `Changed(id)`
  for a body, `Changed(null)` for the list; widgets re-read only for their id or null. Disk work runs
  off the UI thread, one operation at a time, in order; a queued body write is coalesced, never
  stacked. An unflushed local edit outranks an incoming body (original S-09/S-34). The pool is flushed
  on exit after the surfaces close.
- **[S-1349]** Settings: one card per note (pick dot; title committed on Enter or blur, not per
  keystroke; last-written age; delete behind a confirm), "New note" (creates and picks), and a line
  when the shown note was deleted. The cards follow the pool while the page is on screen (original
  S-38).
- **[S-1350]** Block syntax, one line each: `#`..`###` (a space after the hashes), `- ` `* ` `+ `
  bullets, `1. ` / `1) `, `> `, `---` `***` `___` rules, and fences (every line between two fence
  lines is code). Indent depth: two spaces or a tab a level, capped at 6 (original S-15).
- **[S-1351]** Inline: bold `**`, italic `*` and `_` (word boundary only, so `snake_case` stays
  plain), strike `~~`, code (backticks, never nests) (original S-16).
- **[S-1352]** The spans of a line concatenate to exactly the source line; an unpartnered delimiter is
  literal text. The editor formats spans at their offsets and never rewrites text (original S-14/S-18).
- **[S-1353]** Every intercepted key and command is a pure `(text, selection) → (text, selection)` in
  `NoteEditing`; the control only applies the smallest replacement and places the caret.
- **[S-1354]** Enter continues `- `, `- [ ] ` (unchecked) and `1. ` (numbered on) when the caret is in
  the item's content; on an item holding only its marker it clears the marker. Shift+Enter is a plain
  newline (original S-19).
- **[S-1355]** Tab / Shift+Tab indent / outdent every touched line by two spaces; a tab outdents as one
  unit (original S-20).
- **[S-1356]** Ctrl+B / Ctrl+I wrap in `**` / `*`, unwrap when already wrapped (inside or just outside
  the selection), or insert an empty pair with the caret between (original S-21).
- **[S-1357]** Paste is plain text only, line endings normalised to `\n`. Native drag-and-drop is kept
  (RichEdit moves text correctly); whatever formatting a drop brings is overwritten when the touched
  lines are redrawn (original S-23, adapted).
- **[S-1358]** Tasks: `- [ ] ` / `- [x] ` (`X` too); a done item is struck and dimmed; a click that
  leaves the caret on the box toggles it, length-preserving. The box is drawn in the accent colour
  rather than as an icon over the text — a `RichEditBox` cannot host one (original S-17, simplified).
- **[S-1359]** Toolbar commands: heading cycles H1 → H2 → H3 → body (level from the first touched
  line); bullet / numbered / task / quote apply to every touched line, replace any other block marker,
  and strip when every line already has them; buttons light for the marks at the caret (a checked
  task's strike does not light Strike) (original S-27).
- **[S-1360]** Undo/redo is the editor's own (`NoteHistory`: Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y, the menu):
  runs of single-character typing coalesce, Enter and pastes are steps of their own, no timers, 200
  steps. The control's own stack is off — it would also hold every formatting pass (original S-22).
- **[S-1361]** Only the lines an edit touched, plus lines whose parse changed (a fence opened or
  closed), are redrawn (`NoteText.LinesToRedraw`); replacements never split a surrogate pair.
- **[S-1362]** Status line: "N words" (runs holding a letter or digit, so markers are not words); at
  wide sizes also "N characters · Saved / Saving… / Not saved", driven by the pool's save events.
- **[S-1363]** Ladder (`NotesLayout.Plan`): the text always (at least two lines; headings at body size
  on a compact width) → formatting bar from Regular × Regular or on a Wide strip, buttons shed by rank
  as it narrows (bold, italic, task, bullet, heading, strike, code, numbered, quote) → status line at
  height Expanded (with detail at width Expanded) → note list beside the text at width Huge.
- **[S-1364]** The header title is the note's title (the user's own widget label still wins). A
  chevron in the header opens the note picker (every note, New note, Rename…, Delete note…), except at
  XS (it would push the title out) and XL (the list does its job).
- **[S-1365]** The bar's buttons never take focus, so a command runs on the note's selection. Bold,
  italic, strikethrough, bulleted list and checklist are Segoe Fluent glyphs checked against the font;
  heading and numbered list are lettered ("H", "1.") because the font has neither.
- **[S-1366]** The editor admits no formatting the markdown does not hold: rich-text accelerators off,
  every Ctrl chord other than clipboard, selection and caret keys swallowed (Ctrl+Alt passes: AltGr),
  no selection flyout, its own context menu (undo, redo, cut, copy, paste, select all), copy as plain
  text. Keys are not intercepted while an IME composition is open.
- **[S-1367]** XL note list: title and last-written age per row, the shown note lit, "New note" at the
  bottom; click opens, double-click / F2 / menu renames in place, the menu or Delete deletes after a
  confirm; Up/Down/Home/End/Page keys, Enter and Escape via `ListKeys`.
- **[S-1368]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported, deliberately: the pop-out window (original S-11..S-13, user decision); the selection
bubble (S-26: the bar and Ctrl+B/I cover it); header-mode placement of the bar (S-29: the card owns the
header and a view cannot know its mode, so the bar is a row the ladder sheds instead); the "On this
device" source row (S-35: there is no other source); a comfortable-measure cap at L (a 560-wide card
is about 80 characters already, and a cap would leave the empty band the ladder exists to avoid).

## Disk and ping

Package P2 of the system audit. Both stay `MeterPart`s on the shared `MeterView`; like every meter
they show the label, the reading and the history only ([S-1762]).

- **[S-1160]** Disk reports on one `Drive` (a root, `C:\`; empty = the system drive). Load repairs it
  to its root and drops anything that is not a drive letter, logged. Changing the drive clears the
  chart (original [S-22]). The settings page offers "System drive (C:)" and every drive Windows lists;
  a chosen drive that has been removed stays offered as "not connected".
- **[S-1161]** The header title names the drive as Explorer does ("System (C:)", "Local Disk (D:)",
  "USB Drive (E:)"), "C:" for a drive the walk did not list, "All drives" for every drive.
- **[S-1162]** Read and write rates are per drive from one shared PDH query
  (`\LogicalDisk(*)\Disk Read|Write Bytes/sec`, matched by letter ignoring case; `_Total` and folder
  mounts left out). A drive the counters do not list has no rate, never the system total (original
  themia-releases#49). The query is held while a disk widget lives and closed when the last one
  lets go. Volumes and labels are re-walked at most every 30 s (original [S-21]).
- **[S-1163]** **The chart is disk I/O, never space** (user requirement). All drives is the sum of
  the letter instances, never `_Total` too. The history is on by default.
- **[S-1820]** **The disk reading is throughput, never space** (user requirement: the disk widget
  shows disk I/O, not the storage space): reads and writes together per second
  (`DiskRates.Total`), formatted as a rate ("12 MB/s", `Formatting.Rate`), and the chart is that
  same total, one series. Space used, free space and a drive pressure colour are neither read nor
  shown. A sample is a rate, a wait (the counters' first collection lists nothing: a dimmed "--"
  and no point on the chart, never a 0), a missing drive (dimmed "--", "C: unavailable" on hover)
  or no counters ("unavailable") — `DiskRules.Read`.
- **[S-1821]** The throughput chart's top is the window's peak rounded up to a doubling of 1 MB/s
  (`ChartScale.Doubling`, `DiskRules.Scale`): background writes of a few hundred KB/s stay low on
  the chart instead of filling it, a copy keeps headroom, and the top only moves when the peak
  crosses a doubling.
- **[S-1822]** One setting says whose throughput: `Scope` = one drive (`Drive`, empty = the system
  drive) or all drives. Changing it clears the chart. Load repairs an unknown scope to one drive,
  loudly. The separate chart scope (`IoScope` / `IoDrive`) is gone.
- **[S-1166]** The card's "Open C: in Explorer" opens the drive through `ShellLauncher`; all drives
  offers no such command.
- **[S-1167]** Framework hooks for a kind (`MeterView.Part.cs`): a header title
  (`IMeterHost.SetTitle`), right-click commands (`MeterPart.ContextActions`), a header trailing
  element, and `IMeterHost.SampleNow`. A reading that carries a fraction is plotted even when it is
  not available (a missed ping); every other unavailable reading carries none.
- **[S-1168]** Disk settings: Drive (system drive / all drives / each drive Windows lists, a removed
  one offered as "not connected"), then the shared meter rows.
- **[S-1169]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1170]** Ping status: Checking… until the first probe returns, then Online / Offline from the
  last probe. A dot at the right of the header carries it in the success / critical / tertiary
  colour; its tooltip says it in words, and so does a dimmed reading's.
- **[S-1172]** The ping chart is latency against the window's slowest answer (floor 1 ms); an
  unanswered probe is a dimmed "--" that plots on the baseline. Online colours the chart success,
  offline critical.
- **[S-1173]** `HostLabel` (≤ 64 characters, stored as typed, trimmed when shown): the header title is
  the label, else the host.
- **[S-1174]** Ping "Refresh" on the card's menu probes now; a probe already out is the answer
  coming, so it is not doubled ([S-106]).
- **[S-1175]** Ping settings: Host, Name, then the shared meter rows.

Not ported / different: several ping hosts (the original had one); several drives in one tile, the
drives list, IOPS, the volume line and ping's jitter / loss / range ([S-1762]); a coloured latency
number (S-432: pressure never colours the reading).

## Keyboard lease (WP11)

The desktop surface is `WS_EX_NOACTIVATE`; a widget that is typed into or navigated with keys gets
the keyboard through one lease. Rules in Core `KeyboardLease<T>` / `WidgetKeys` (tests
`KeyboardLeaseTests`), carried out by `AppHost.Keyboard.cs` and `DesktopWindow.Input.cs`.

- **[S-800]** One lease, app-wide, one holder. Outside edit mode a press anywhere in a card (handled
  presses too; the innermost card of a container) asks for it: a view implementing `IWidgetKeys`
  takes it, any other press ends the current holder's lease, on any display. The holder's surface
  drops `WS_EX_NOACTIVATE` and becomes the foreground window; the z-order controller keeps it on the
  desktop. If Windows refuses the foreground the lease is dropped at once (logged), so the surface is
  never left activatable for nothing. Preview cards never take it.
- **[S-801]** Text boxes ride the same lease: `IWidgetSurface.RequestTextInput(true)` on focus grants
  it to the box's widget, `false` on blur ends it — except for a widget with keys, which keeps the
  arrows after its editor closes. Focus moving between two widgets' boxes hands over in either event
  order; a late blur from a previous holder changes nothing.
- **[S-802]** The lease ends on Escape (which also hands the keyboard back to the window that was
  foreground when the lease began, if the surface is still foreground), on a press on any other card,
  on the surface losing activation (a click on the desktop or another app, Alt+Tab — the "click
  outside" signal, no global mouse hook), when the holder's card leaves its surface, on entering edit
  mode and when the surface closes. Only Escape gives the keyboard back: after a click elsewhere the
  click already put it where the user wants, and a launched app must not be raced. Every end sends
  the old holder's view `WidgetKey.Escape`, the one place a view drops its cursor or selection, and
  moves XAML focus out of its card so no caret is left blinking.
- **[S-803]** Keys: arrows, Page Up/Down, Home/End, Tab / Shift+Tab, Enter, Space, Delete, F2
  (Rename), the Menu key / Shift+F10 (Menu), Escape. Ctrl and Alt chords are never widget keys;
  Shift+Delete, Shift+Space and Shift+F2 are not either. A view returns whether it used the key; an
  unused key carries on (Tab still moves focus).
- **[S-804]** While a text box on the surface has focus, every key but Escape is the box's. Escape
  reaches the lease only when the box did not handle it (a search box that clears on Escape keeps it).
- **[S-805]** The keyboard is never taken without the user asking: only a press in a card or a text
  box getting focus can grant the lease.
- **[S-806]** Calendar (`HandleKey`, [S-1269]): Escape hides the cursor day.
- **[S-807]** Contacts ([S-1277]): the surface-wide press hook is gone; a click outside ends the
  lease and its Escape clears the selection, which now also covers a click in another app.
- **[S-808]** Tasks ([S-787]) and **[S-809]** Habits ([S-1428]): keys arrive through `HandleKey`
  (the per-row `KeyDown` / `PreviewKeyDown` handlers are gone); `UpdateKeyboard` asks only for an
  open editor. The lease ending while an editor is open forgets the row; the box's blur commits it.
- **[S-810]** GitHub (repository, issues, reviews, notifications; `RowCursor`): a click on a row
  focuses it (XAML focus, system focus rect: no layout cost), Up/Down/Home/End move (clamped,
  `ItemCursor`), Enter opens the row's page, the Menu key opens its menu, Escape lets go; the focus
  follows its row's link across redraws and is dropped when the row leaves.
- **[S-811]** Pictures (Gallery widget) slideshow: Left / Right step, Space pauses (the original's S-29). Other
  layouts take no keys.
- **[S-812]** Tasks and Habits with the lease but no row focused: a movement key lands on an end
  (Up/Down/Home/Tab the first, End/Shift+Tab the last); Enter, Delete and F2 do nothing.

Not wired here (lanes still running; see the WP11 report): Mail, OneDrive (`ListKey` handlers),
Clipboard and Mood (`HandleKey(WidgetKey)` ready), Notes' list, CPU/Processes and Network drives.
Not live-verified: focus taking, Alt+Tab, deactivation timing, IME (see the report).

Key routing was fixed after this section was written: the holder now gets its keys in the tunnelling
pass, before a `ScrollViewer` can take them ([S-860]).

## Battery and network

Package SYS-P3. Both meters live in their own files (`Widgets/Meters/BatteryMeter.cs`,
`NetworkMeter.cs`); the rules are Core (`BatteryRules`, `NetworkRules`, tests in
`BatteryNetworkTests`). Like every meter they show the label, the reading and the history only
([S-1762]).

- **[S-1180]** Battery: charge from `GetSystemPowerStatus`; charging is the `BatteryFlag` charging bit,
  not the plug. The no-battery decision is `PowerRules.Read`'s, so the meter and the power tile
  agree; a machine with no battery shows an empty state.
- **[S-1186]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1187]** The battery reading leads with the Segoe Fluent battery glyph filled to the nearest
  tenth, with a bolt while charging, in the pressure colour (`MeterPart.Badge`).
- **[S-1190]** Network throughput is per adapter (`ThroughputTracker`): a counter that falls is that
  adapter's reset (reads 0 once, re-baselines) and no longer zeroes every other adapter's traffic;
  a new adapter starts from a baseline. One shared reader; a second tile in the same tick reuses the
  sample.
- **[S-1191]** Download is the reading; the chart's two series (download filled, upload the thin
  line) share one top: the window's peak of both, floor 1 KB/s (`RateWindow`), never the session's
  all-time peak.
- **[S-1192]** Counted adapters: up, not loopback/tunnel, not a packet-capture mirror (name or
  description contains "pcap packet driver"; the standalone Npcap Loopback Adapter is kept), and not
  an NDIS filter binding listed as its own adapter ("WiFi-QoS Packet Scheduler-0000": another
  adapter's name, "-", a filter, "-NNNN"), each of which re-reports its adapter's counters — on the
  dev machine seven of them multiplied the reading sevenfold, a bug the first port had too.
- Not ported / different: the battery's status line, health, rate, cycles, capacity list and the
  network's connection, addresses, public IP, session totals and adapters list ([S-1762]); RX green /
  TX accent colours (the chart's two-series styling is the framework's).

## GPU and screen time: the whole tile (SYS-P4)

Code: `Widgets/GpuView.cs` + `GpuView.Panel.cs`, `Services/GpuSampler.cs`, Core `GpuLayout`, `GpuRules`,
`MeterFace`; `Widgets/ScreenTimeView*.cs` (+ `.Footer.cs`), Core `ScreenTimeRules`; settings
`WidgetSettings.Gpu.cs`, `WidgetSettings.SystemWidgets.cs`. Tests `GpuLayoutTests`, `ScreenTimeRulesTests` (`S12xx_`).

- **[S-1200]** The GPU chart is the meters' `SparkBand` holding `GraphSpanSeconds / IntervalSeconds`
  samples (`GaugeConfig.GraphCapacity`, default 60 s at 2 s); the page is the adapter picker and the
  shared history rows (show history, chart span, refresh). Supersedes the fixed 60-sample band of [S-430].
- **[S-1201]** Each adapter panel is a meter face ([S-1762], `MeterFace.Plan`): the load alone on a
  tiny panel, else the history under it (beside it on a strip) filling the panel. With several
  adapters each panel keeps its name beside the load at Subtitle and gives the rest to its history.
- **[S-1207]** Several adapters share the tile with no scrolling (`GpuLayout.Grid`): the column count
  whose panels come closest to 240 × 90 in their tighter direction (ties: fewer columns), so a
  strip puts two side by side and a column stacks them.
- **[S-1210]** The ring's total takes the largest step that fits its hole; when even Caption cannot
  hold `4h 37m` it shows the compact form (`4.6h`, `13h` from ten hours, `37m`, `<1m`). Never clipped.
- **[S-1211]** `MaxApps` is a ceiling over what fits: 0 (the default) lists as many apps as the tile has
  whole rows for, 1–50 at most that many; an "Other" row joins when apps are left over. `ShowList`
  (on) switches the legend off. Supersedes the fixed default of 7 and "0 lists none" in [S-452].
- **[S-1212]** The drawing names the apps the legend lists: all of them when they fit (no "Other"),
  else all but the last row, which is "Other" — but never fewer than 7 unless `MaxApps` says fewer.
- **[S-1213]** On a tile Expanded both ways, with the drawing keeping at least 200 px, a footer
  appears from the bottom: the stats line, the daily limit as a bar ("Daily limit · 2h 10m of 4h",
  critical past the limit; a window judges its daily average), and for a day the week strip.
- **[S-1214]** Week strip (Day period, not style None): the seven days ending on the shown day, one
  bar each scaled to the busiest, the shown day in the accent, weekday under each; a click shows that
  day (today unpins) within the navigation's reach.
- **[S-1215]** Stats line: under a day "7-day average 1h 34m · busiest Tue 5h 0m"; under a window
  "busiest Tue 29 Sep 5h 0m · 9 apps". Hidden apps are left out of every figure.
- **[S-1216]** Screen time lays out from the card's `WidgetArea` (`IWidgetSizeAware`), not its own
  `SizeChanged`; a debug render never hooks or writes ([S-459]).
- Not ported / different: the original's GPU `visibleMetrics` order (fixed ladder order); per-engine
  history charts (one chart, the load).

## Feed, stocks and the web widgets: parity and the whole tile (OFF-E)

Ported from `widgets/online/{rss,stocks,video-stream,web-element}` on main, renumbered; the rules
live in Core (`FeedAggregate`, `FeedLayout`, `NetworkGuard`, `MarketSearch`, `MarketQuote`,
`ExchangeTime`, `StockLayout`, `NetworkCacheStore`), tests in `FeedWidgetTests`/`StockWidgetTests`.

- **[S-1300]** Feeds and quotes survive a restart (classic S-23/S-29): the last good answers are
  saved to `network_cache.json` beside the state (own file, never in an export; schema-versioned;
  atomic temp + rename; one writer; saved after each fetch that brings a new answer). At launch they
  are read back before the windows are built and marked restored: drawn at once, with their real age
  in the header in the caution colour ("Saved 3h ago; refreshing"), never treated as fresh, so a
  fetch follows at once. Entries older than 7 days, from the future, or not a usable feed/quote are
  dropped; feed links are re-checked; a corrupt file is set aside as `.corrupt`. Only the app wires
  it; a debug render never reads or writes the user's file. This reverses the earlier "in memory, so a
  cold start never shows old data as current" note: the age and colour say it is old.
- **[S-1301]** Network guard (classic `netguard::check_url`, S-08b): an address that comes from a
  user or a stranger and is then fetched by the app — every feed, every feed item's site icon, a
  pinned web page in the renderer and the picker — is refused when its host is, or resolves to
  (every address, not the first), loopback, private (10/8, 172.16/12, 192.168/16), link-local
  (incl. 169.254.169.254), CGNAT 100.64/10, 0/8, 240/4, a documentation range, ::1, ::, fe80::/10,
  fc00::/7 or 2001:db8::/32 (IPv4-mapped judged as IPv4). Feed requests follow redirects themselves
  and check every hop; https→http is refused. An unresolvable name is refused unless a proxy will
  resolve it (classic NG-07), but an IP literal is checked even then. One switch for the whole app,
  Settings › General › **Local network access** (`AppPreferences.AllowPrivateNetwork`, off), read on
  every check. A refused feed says so in the widget ("x is on the local network. Turn on local
  network access…"). The camera stream stays unguarded, as in the original; every other request,
  the fixed public services included, now connects through the guard ([S-960]), and the loopback
  hardware monitor is reached as "this machine only" ([S-967]).
- **[S-1302]** Rss reads several feeds (`RssConfig.Feeds`, up to 12, a list editor in settings, each
  address checked on commit), fetched in parallel through the shared cache and merged
  (`FeedAggregate.Merge`, classic S-30): newest first, undated last in arrival order, capped at
  `MaxItems` across all feeds. Repeated and blank addresses fold; refused ones are not fetched. A
  feed that fails while others answer is a partial refresh: "Name: reason" in the header tooltip and
  the caution colour (classic S-34/S-35); all failing shows every reason. Header title (S-04): the
  user's, else the single feed's `<title>`, else the feeds' names (title or host) joined with ", ",
  else the catalogue name. Item age (S-06): "Just now"/"5m ago"/"3h ago"/"6d ago", then "Mar 4"
  ("Aug 30, 2025" in another year). `FeedUrl` is gone (no compatibility).
- **[S-1303]** Rss **Refresh** (card menu and item menu, classic S-15): the network now, past the
  cache's freshness and the back-off. While any fetch is out, a small spinner sits beside the age in
  the header (classic S-33), so Refresh is never a button with no visible effect.
- **[S-1304]** Site icons (classic S-26/S-27, the Google s2 service replaced): fetched from the site
  itself, `scheme://host[:port]/favicon.ico` of the item's link — no third-party service. Guarded
  ([S-1301]), 64 KB cap, the bytes must be ICO/PNG/GIF/JPEG/BMP (not HTML, not SVG), one request per
  origin per session shared by every row and widget, a missing icon retried after 6 h; decoded at
  2× the 16px drawn size. A globe stands in until it arrives or when there is none.
- **[S-1305]** Rss ladder (`FeedLayout.Plan`, rows shown whole per [S-639], measured since rows are
  as tall as their headlines, the rest a scroll away with the shared fade [S-1281]): rung 0 the
  newest headline, two lines (one if even that does not fit); its age; its site icon from 180px of
  row width (inline at the start of the age line); the site's name on its own line from 240px; two
  columns from 400px wide (three from 720px on a short strip), read left to right; the summary where
  it costs no row, or where ≥3 rows still fit on a tile ≥280px high and not a tall column (1–2
  lines). By tier: XS one headline; S headline + icon + age; M two rows with names; Wide two
  columns; Tall 5+ headlines; L two columns with summaries; XL the reading pane.
- **[S-1306]** From 720px wide and 128px high a reading pane sits beside the list (list 40%,
  240–360px; pane ≥240px): the selected item's icon and site, whole headline, date ("Sat 4 Oct
  2026, 14:30 · 2h ago"), whole summary (the parser now keeps 600 characters, was 300) and **Open
  article**. A click on a row selects (and focuses) it; a double-click opens it everywhere, as every
  list on this desktop. Item menu (`ItemMenu`): Open, Copy link, Refresh. Keys through the shell's
  keyboard lease (`IWidgetKeys`, [S-800]; a click in the widget takes it): Up/Down/Left/Right step, Home/End, Enter opens, Escape lets go;
  in the reading layout the cursor is the selection.
- **[S-1307]** Stocks symbol search in settings (classic `SymbolSearch.tsx`, S-38): an
  `AutoSuggestBox` asks Yahoo `v1/finance/search` and CoinGecko `search` together after a 300 ms
  typing pause (the previous search abandoned), keeps only chartable kinds (EQUITY, ETF, MUTUALFUND,
  INDEX, CURRENCY, FUTURE), at most 8 per service, alternating, 12 in all; each line says name,
  symbol, exchange and class. A pick sets symbol, market and name; typed text committed with Enter
  is a raw symbol under the current market, through the old check. The chosen symbol's class
  ("Index on SNP") shows under the box. A failing service contributes nothing (logged).
- **[S-1308]** Extended hours (classic S-34/S-35, `ShowExtendedHours`, off): listed symbols on the
  1-day and 5-day charts ask `includePrePost=true` (part of the cache key). The last pre-market /
  after-hours bar against the regular close is shown under the price ("After hours 412.10 −0.36% ·
  16:42", in its direction's colour): pre-market before the bell, after hours after it, the latest of
  the two while closed. The badge turns accent in an extended session with data; bars outside the
  regular session draw dashed at 55% opacity. Timestamps are kept paired with closes (nulls drop
  both halves; out-of-order series are sorted).
- **[S-1309]** Times on the chart are the exchange's (classic S-36): Yahoo's `gmtoffset` and
  `timezone` ("EDT") — not an IANA lookup, which needs ICU the app does not ship — "09:30" for 1 day,
  "Tue 15:00" for 5 days, "Mar 4" beyond, with the year from 2 years; local time when the reply has
  no offset. The badge and the time labels say which zone in their tooltip.
- **[S-1310]** Chart options: **High and low** markers (dot and price at the period's first highest
  and lowest bar, kept inside the chart); **Fill under the line** (`ShowAreaFill`, on — WinUI's
  look; the original defaulted off); **Rising/Falling colour** through the shared colour picker
  (`ColorPickerRow`, never hex; empty = the theme's green/red; a hand-edited non-colour is repaired
  to empty at load).
- **[S-1311]** Stocks ladder (`StockLayout.Plan`, replacing the 34%/120px chart band): rung 0 price +
  signed change (change under the price where it does not fit beside); currency and session glyph
  from 180px, the session's words from 300px; the extended line; the chart (≥24px, full-bleed) with
  start/end times from 128px high and the middle one from 400px wide, a price axis (high/mid/low) from
  400px wide and 120px of chart; facts (`MarketQuote.Facts`: previous close, day range, 52-week
  range, volume "18.7M", exchange — only what the reply carried): a stack under the chart on a tall
  column, a row of tiles on a large tile, a column beside the chart on the largest; range chips
  (1D…5Y, saving `Range`) on the largest. A strip (≥400px, 2:1) puts the readout beside a full-height
  chart. The chart takes what is left; the price grows only to Title / TitleLarge / Display by the
  tile's smaller class. Pointer over the chart on L/XL reads the bar under it (time · price). The
  session badge and age re-evaluate every minute on the shared ticker (render only), so the badge
  flips at the bell whatever the refresh. **Refresh** in the card menu. Header title: name, else the
  service's own name (listed), else the symbol.

Not ported, on purpose: RSS feeds as shared *connections* and the classic "Manage connections" page
(S-08/S-29 — feeds stay a widget setting, recorded earlier); per-widget `label`/`hideHeader` (card
title and header placement); demo mode; single-click open of a feed item
(double-click, recorded); the original's IANA time-zone names in labels (offset + short name
instead, see [S-1309]).

## Web widgets: size ladder and overlay

Round 3 parity and layout for the video stream widget (package OFF-E). Owning files:
`Widgets/VideoStreamView.cs` + `VideoStreamView.Overlay.cs`, `Services/Web/StreamSession.cs`
(`FrameFacts`), `Shell/Config/WidgetSettings.Web.cs`, `Themia.Core/Widgets/VideoStreamRules.cs`
(`StreamChrome`, `StreamChromeRules`, `FrameRateMeter`), `Themia.Core/Model/Configs/WebWidgetConfigs.cs`.
Tests: `StreamChromeTests`.

- **[S-1312]** Video stream chrome ladder (`StreamChromeRules.Chrome`, `IWidgetSizeAware`). The
  picture is the widget at every size; nothing else ever shows on XS, S or a Wide strip (height
  under Regular). From M (Regular × Regular, so Tall too), with the pointer on the picture, a
  bottom strip shows a state dot, the state and the frame's age. From L (Expanded × Expanded) the
  strip adds Pause/Resume, Rotate and Refresh (the same handlers as the context menu's) and
  the camera's resolution and measured frame rate. At XL (Huge × Huge) a persistent top-left chip
  shows the state and transport. Two settings: `HoverControls` (default **on**: invisible until
  hovered, and never on the small sizes) gates every hover part; `ShowStatus` (default **off**,
  because the original had no chrome) gates the chip. The view's own `SizeChanged` is gone: the
  decode size, the message's shedding and the chrome all follow the card's area.
- **[S-1313]** What the chrome says (`StreamChromeRules`): "LIVE" for an open stream, "every 10 s"
  in interval mode, "connecting" before the first frame, "reconnecting n" after the n-th failure in
  a row (reset by a frame), "paused"; frame age "now" / "4 s ago" / "2 min ago" / "3 h ago";
  resolution "1280×720" from the frame's own size before the decode scaled it (`FrameFacts`
  through `IStreamSink.OnFrame`; for MP4/HLS the player's natural video size); frame rate over the
  last 5 s of arrivals (`FrameRateMeter`, monotonic timestamps, "12 fps" / "2.5 fps"), shown only
  for an open MJPEG stream; transport MJPEG / Snapshot / Video / HLS. The age ticks through the
  shared ticker at 1 s only while the strip is on screen and the stream is live.

Not ported, on purpose: a compact header for the video stream — the original's
`defaultHeaderMode: 'compact'` has no counterpart (this app has Top/Bottom/Hidden only; see the
S-10 note), so the camera keeps the Top default; camera credentials ([U-03]) — the original had
none, so basic-auth streams stay unsupported and an address with a user name or password is
refused (the settings note says so); a save-current-frame button at XL (an audit suggestion, not
the original's); frame rate for MP4/HLS (the player exposes no per-frame arrivals); the hover strip
in a debug render (a render cannot hover).

## Appearance engine (WP1)

Every Appearance setting takes effect live, on every widget and, where it applies, on the settings
window. One apply path: `DesignApplier.Apply(design)` (app-wide: theme, accent, font, text size)
runs first on every state change (`AppHost` subscribes it before any surface), then each card's
`WidgetCard.ApplyDesign` (fill, border, header fill, radius, font scope). Nothing is rebuilt: no
card is re-created, no tick restarts. Pure rules live in Core (`DesignLook`, `DesignPresets`,
`FluentType.StepsAt`), tested in `DesignEngineTests`.

- **[S-820]** Accent override. Empty = the Windows accent (`UISettings` Accent, AccentDark1..3,
  AccentLight1..3). A colour of the user's own is walked into seven shades (HSL lightness, hue and
  saturation kept; `DesignLook.Shades`). The framework's accent brushes are found once, by colour,
  before anything is recoloured (every `AccentFill…`/`AccentTextFill…`/`SystemControl…Accent…`
  brush is one of the seven shades at some alpha) and recoloured in place, alpha kept; the
  `SystemAccentColor*` resources are set for later lookups. So template `{ThemeResource}` uses
  follow, in widgets and in the settings window.
- **[S-821]** Text size (`FontScale`, 0.5..2) scales the whole `FluentType` ramp (size and line
  height together), so `Fit`/`Hero` still pick real steps and a layout sheds rather than
  overflows. Live: before a card's view re-lays out on the state change, text already on the card
  whose size was set on the element is rescaled by the ratio; text built afterwards comes from the
  scaled ramp. Icons keep their size (widget scale, [S-850], scales everything).
- **[S-822]** Theme. "Use system setting" is resolved by the engine (Windows background colour) and
  every window root gets the explicit Light/Dark. `FluentLayout.Themed` hands out one engine-owned
  brush per key, coloured from the effective theme's dictionary and recoloured in place on a theme
  or accent change — a brush looked up from code used to be the app's start-up theme, so code-made
  text stayed white in a light card. A Windows theme or accent change (`ColorValuesChanged`) re-runs
  the apply path.
- **[S-823]** Widget background and opacity. A colour of the user's own replaces the Fluent base and
  its tint (exactly that colour); empty keeps the Fluent base + card tint. Opacity 0.2..1; a
  translucent widget is 60% of it (min 0.15), a transparent one has no fill.
- **[S-824]** Border: style None/Solid/Dashed (3-on-2 stroke widths)/Dotted (round dots two widths
  apart), width 0..8, colour (empty = the theme's card stroke), opacity 0..1. Drawn as a shape over
  the content and inset with the gap; none on a transparent widget.
- **[S-825]** Corner radius 0..32 on the fill, tint, border, content clip and edit outline.
- **[S-826]** Font: `system` (Segoe UI Variable), `sans` (Arial), `serif` (Georgia), `mono` (Cascadia
  Mono, Consolas), or any installed family name; a value that is not a plausible family name is the
  system font. Applied to the card content through a presenter, so text inherits it; icon glyphs
  name their own font and are untouched. Text boxes keep the control font.
- **[S-827]** The font picker lists every installed family (GDI `EnumFontFamiliesExW`, off the UI
  thread, once per process): vertical `@` faces dropped, duplicates folded, sorted. Each entry is
  drawn in its own font ([S-1723]).
- **[S-828]** Header background (empty = shares the widget's fill) and header opacity (0..1,
  [S-1761]); the strip reaches the card's edges and follows its corners.
- **[S-1761]** The header opacity always fades the header strip (it did nothing while the header had
  no colour of its own, which is the default — the widget's fill was painted under the header too, so
  a header sharing it could not look any different). The card's fill now paints the body only, in
  the content grid beside the header (`WidgetCard.ApplyBodyFill`), and the strip has its own fill:
  a header colour at the header opacity, or the widget's fill and Fluent lift at the widget's opacity
  times the header opacity (`DesignLook.HeaderFillOpacity`). 100 % looks as before; 0 % leaves the
  strip clear over the desktop. The body's base reaches 1 DIP under the strip, so no anti-aliased seam
  opens between the two where their edge falls between pixels (widget scale, an intro, fractional DPI).
- **[S-829]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-830]** Design presets (the original's six): Default, Midnight, Daylight, Glass, Terminal, Paper.
  Each owns 13 fields (theme, accent, font, widget fill + opacity, header fill + opacity, border
  style/width/colour/opacity, corner radius, widget gap) and never widget scale, text size or
  motion.
- **[S-831]** Default is read from a fresh `DesignState`, so a fresh install reads as Default.
- **[S-832]** Applying a preset writes its fields in one mutation and leaves every other field alone.
- **[S-833]** The picker shows the matching preset, or Custom; only owned fields are compared, colours
  case-insensitively, numbers within 0.001.
- **[S-834]** Appearance page: Style (presets + Custom), theme, accent, widget background + opacity,
  header background + opacity, corner radius, border group, font (presets + installed), text size,
  widget scale, then Layout ([S-1722]: gap between widgets, snap to grid). Picking a preset rebuilds
  the page's rows in place; no other row of the page rebuilds anything (a change made elsewhere
  is caught up by the window, [S-1720]).

Not ported, on purpose: the original's preset preview tiles (a drop-down lists them, through the
shared `FluentLayout` row like every setting); the blur that tracked opacity (there is no blur layer
behind a card here); the eyedropper (optional in the audit). Motion settings are WP9's.

## Logging (WP7)

Ported from the original's logging pipeline (`src-tauri/SPEC.md` [LOG-01..09]). Pure rules in Core
(`Diagnostics/LogFileRules`, `LogPolicy`, `LogRedactor`, `LogFormat`), tested in `LoggingTests`; the
sinks in `Services/LogFiles.cs` and `Services/EventLogSink.cs`. There is no settings page and no
switch for it ([S-1724]).

- **[S-980]** Bounded, rotating log folder: `%LOCALAPPDATA%\Themia\logs` (machine-local, not
  roaming; covered by the uninstall's data purge). The current file is `themia.log`; before a write
  would take it past 2 MB it is renamed to `themia-<UTC yyyyMMdd-HHmmss-fff>[-n].log` and a fresh
  one starts. An empty file is never rotated (one oversized record cannot chain empty archives).
  One writer: a lock around one open stream (shared for read and delete), flushed per record. A
  write that fails with an I/O error turns the file sink off for the session instead of throwing
  on every record. Only the real app writes files: the debug renders get the
  debugger and session-buffer sinks only, and a second copy that hands its request to the running
  one never opens the file.
- **[S-981]** Level policy: warnings and errors are always recorded; info and debug only in a debug
  build. A record under the floor is dropped in `Log` before it is redacted or formatted. The
  original kept only errors by default; warnings are kept here too because they are rare in this app
  and are what explains most "it stopped updating" reports.
- **[S-983]** Redaction, central: `Log` passes every message and every exception's full text
  through `LogRedactor` before any sink sees it, so no sink — file, event log, debugger
  — can carry a secret. Removed: `Bearer`/`Basic` credentials; secret-named parameters
  (`access_token`, `refresh_token`, `id_token`, `client_secret`, `password`, `api_key`, `cookie`,
  `authorization`, …) in `=`, `:` and JSON form; OAuth query parameters (`code`, `state`, `token`,
  `code_verifier`, `sig`, `key`, …); JWTs; GitHub/Anthropic/Slack/Google token prefixes; runs of 40+
  base64 characters mixing upper case, lower case and digits; URL user info. Email addresses keep
  their first character and domain (`j***@example.com`). Audit of the existing call sites: none
  logs a token or password directly; account ids (`imap:<user>@<host>`, `caldav:…`, `saved
  <account>`) and exception messages carrying addresses were the leaks, and the central pass covers
  them.
- **[S-984]** Record format: `yyyy-MM-dd HH:mm:ss.fff zzz LEVEL scope: message`, the exception
  (inner exceptions and stack) indented four spaces under it. Line breaks inside a message become
  " ⏎ " so a message cannot forge another record. A record is cut at 32 KiB.
- **[S-985]** Retention: at most 9 archives beside the current file (≤ 20 MB in all), and none older
  than 14 days. Swept at start (before the first write) and after every rotation. Only names
  `[S-980]` produces are ever deleted; anything else in the folder is left alone. Best-effort: a
  folder that cannot be listed or a file that cannot be deleted is skipped, never fatal.
- **[S-986]** Errors are mirrored into the Windows Application event log, source `Themia`, event
  id 1000 (`RegisterEventSourceW`/`ReportEventW`, handle held in a `SafeHandle` for the process).
  25 per session, then one notice that the rest are suppressed, then nothing. Text cut at 2,000
  characters without splitting a surrogate pair. The source is registered by the installer (HKLM,
  later package); without it Windows still files the event, wrapped in its "description cannot be
  found" preamble. A failure to register or report is silent: the caller is the logging pipeline.

Not ported, on purpose: the original's Developer section and Logs page — developer mode, verbose
logging, the session list of records ([S-1724]); DevTools (no web view), demo mode and dev views (the
showcase views, [S-1810], play that role), the per-window webview log forwarding gate and console-noise
filter ([LOG-02], [LOG-03]; no webview), the dependency DEBUG cap ([LOG-06]; .NET libraries do not
log into `Log`). Not done here: the original's pre-release gate on the Google and GitHub sign-in
providers (a launch decision, not a developer feature).

## Mixer and Volume (SYS-P5)

`Themia.App/Services/CoreAudio{,.Worker,.Native}.cs` (replaces `AudioSessions.cs` and the polled
`AudioEndpoint.cs`), `Themia.App/Widgets/{MixerView,MixerView.Row,MixerView.Panel,VolumeView,LevelRow,AudioFeed}.cs`,
`Themia.Core/Widgets/{AudioRules,MixerRules,VolumeRules}.cs`, tests `AudioRulesTests`.

- **[S-1220]** One Core Audio worker serves both widgets (the [S-440] MTA thread, now named
  `CoreAudio`). The volume tile no longer polls: it is a subscriber that does not ask for sessions,
  and moves on the endpoint's `IAudioEndpointVolumeCallback`. `VolumeConfig.IntervalSeconds` and its
  settings row are gone. Each subscriber states what it wants (`AudioWant`: device, sessions, meters,
  microphone); the worker binds exactly the devices wanted and releases the rest, and publishes one
  snapshot per subscriber per wake.
- **[S-1221]** `DeviceId` (+ `DeviceName`, remembered for display) on both configs; null follows
  Windows' default output (console role). A chosen device that is not connected is "*name* is not
  connected", never silently the default. The worker re-enumerates active render endpoints (friendly
  name from `PKEY_Device_FriendlyName`) on device added/removed/state/rename notifications. A blank
  id is repaired to null at load.
- **[S-1222]** Device picker: settings ComboBox ("Default output" + connected devices, enumerated off
  the UI thread); in the widget, the mixer's device line (from a Regular-height card with room) and
  the volume tile's caption on large tiles open a radio menu; the XL mixer panel lists the devices.
  Picking in the widget (menu, XL panel) makes the device **Windows' default output** ([S-1614]; was:
  bound the widget); the settings ComboBox still binds the widget to a device. Context action "Follow
  the default output" when a device is chosen. Inert in previews.
- **[S-1223]** Level meters via `IAudioMeterInformation` (endpoint, microphone, per session; a group
  shows its loudest session). The only sampled thing: at most 10 Hz, only while some widget has a
  meter on screen (loaded, ladder rung shown, `ShowPeaks`), silence sent once. Meters are a scale
  transform on a fixed bar (no layout per sample); a muted row's meter is empty.
- **[S-1224]** Microphone: the default capture endpoint's level and mute (`MicrophoneKey` write key),
  shown in the XL mixer panel and as the volume tile's row on large tiles (`ShowMicrophone`).
- **[S-1225]** Mixer keys (keyboard lease): Up/Down/Home/End move the row cursor (system focus rect on
  the row), Left/Right and +/− step the row 2 points, Page Up/Down 10, M or Space mute, Menu key opens
  the row menu, Escape lets go. With no row focused a row key only focuses the first row. `WidgetKey`
  gains `Plus` (VK_ADD, VK_OEM_PLUS), `Minus` (VK_SUBTRACT, VK_OEM_MINUS), `Mute` (M without Shift).
- **[S-1226]** Volume keys: arrows and +/− step 2 points, Page Up/Down 10, M or Space mute. Ignored when
  `AllowControl` is off.
- **[S-1227]** Mixer rows: compact (< 170 px: icon, percent, mute; 32 px), two lines (48 px), one line
  (≥ 360 px; 40 px) — fixed heights so the list takes whole rows (`Disclosure.TakeRows`); the rest
  scroll, never half a row. Supersedes [S-449]'s thresholds' wiring (values unchanged).
- **[S-1228]** Mixer extras by room: the device line (name, "N playing", chevron) always on an
  Expanded-height card, else only when every row still fits; apps holding a session but silent are
  drawn dimmed; with `OnlyActive`, the silent apps it hid follow the list dimmed when all playing
  rows fit.
- **[S-1229]** XL mixer (Huge width, Expanded+ height): a side panel (≤ 320 px) with the device name,
  its master level (the master row leaves the list), the microphone and every output device.
- **[S-1230]** Volume ladder: number + slider band at every size (a short tile keeps an 8 px foot so XS
  keeps its slider); meter from Regular×Regular (or along a wide strip); microphone row and the
  device switcher on large tiles (Expanded×Regular or Regular×Expanded); the rest goes to the number
  (≤ Display).
- **[S-1231]** Volume apps rung on large tiles with room: top 3 (6 at Huge height) apps on the
  device as read-only level bars, playing ones first in name order; silent ones in the quiet fill.
- **[S-1232]** The caption beside the number is the device's name (was "Output level"), still "Muted" /
  "Turned down" when those apply.
- **[S-1233]** Previews never write (sliders, buttons, keys and device picks are inert); they read
  the machine.
- **[S-1234]** Stepping lands on the whole-percent grid and clamps (`AudioRules.Step`).

The original Volume had no peak meter, device name or microphone — these are audit additions.

## Widget polish 1 (POLISH-1)

Keyboard lease in the remaining lists, Accounts links, and four layout gaps.

- **[S-1560]** One key vocabulary: `ListKeys.Decide` takes the lease's `WidgetKey` (the separate
  `ListKey` enum and `RowFocus.Key(VirtualKey)` are gone). Keys a list has no use for (Left/Right,
  Tab, Space) decide `None` and go on to the surface; the Menu key (or Shift+F10) is
  `ListCommand.Menu`, the focused row's menu. Mail and OneDrive implement `IWidgetKeys` (no view
  `KeyDown`): Up/Down, Page Up/Down, Home/End, Enter opens, Delete deletes (Mail, where the account
  allows), Menu, Escape (also every lease end) clears the row focus.
- **[S-1561]** Clipboard takes the lease: Up/Down/PgUp/PgDn/Home/End move a cursor, Enter copies the
  entry back, Delete removes it from the history (cursor stays on the row that moved up), Menu opens
  its menu, Escape drops the cursor.
- **[S-1562]** Mood takes the lease: arrows/Home/End over the five faces, Enter logs the face.
- **[S-1563]** Notes: the XL note list answers the lease (Up/Down/PgUp/PgDn/Home/End, Enter opens,
  F2 renames, Delete deletes after asking, Menu, Escape). Only the editor and a rename box request
  the keyboard for typing; a click on a list row parks focus on the list host (outside the
  scroller, which would take the arrows) so the keys reach the list.
- **[S-1564]** `IWidgetKeys.WantsKeys` (default true): a view whose keys belong to a part shown only
  at some sizes says no while it is off screen, so the click does not take the keyboard. The card's
  `TakesKeys` honours it.
- **[S-1565]** `KeyedCursor`: a cursor over a list that re-sorts under it remembers its item (a
  pid), not its row; Up/Down clamp, Home/End jump, Escape clears, Left/Right/Enter/PgUp/PgDn are
  not its keys. An item that left the list marks no row.
- **[S-1566]** Processes: Up/Down/Home/End move a ring over the processes shown; it follows its
  process through re-rankings. Rows carry the inset ring; the column headings step in by as much.
- **[S-1568]** RSS: the Menu key opens the focused item's menu.
- **[S-1569]** Bluetooth detail panel: on a tile Regular+ wide and Expanded+ tall with one column of
  rows that leaves at least 128 DIPs (`BluetoothLayout.DetailMinHeight`), the height under the rows
  shows one device: icon and name large, kind and connection, battery (with a level bar, or
  "Not reported"), signal while its scan is on ("Not heard" when none), address
  (`BluetoothRules.AddressText`, "A4:C3:…"). The device is the last clicked row while it is listed,
  else the first (`BluetoothRules.Selected`); its row stays lit. Narrow columns stack the facts and
  let the name wrap to two lines.
- **[S-1570]** (superseded by S-1573) Mood Month on a short tile used to shed weeks of the month.
- **[S-1573]** Mood Month is whole or it is the recent weeks, never a partial month and never clipped.
  Invariant (`MoodLayout.Plan`, test `S1453_a_month_view_is_whole_or_...`): `plan.Recent == false`
  means every week of the month is drawn; `Recent == true` means the month was replaced by a
  rolling map of fewer weeks. Stacked, the month's floor (all its weeks at the smallest legible
  square, 9 DIPs) is reserved right after the faces, before the thought box and the footer; so on a
  short tile the box, the footer and the weekday labels (squares < 18) give way first, and squares
  shrink toward 9. If even the floor does not fit (XS, S: 4 to 6 weeks need 49 to 74 DIPs under
  the faces), the map shows the weeks that fit at 9 DIPs ending with this one (`MoodGrid.Rolling`,
  1 to month-weeks minus 1) and the header says so (`MoodLayout.Caption`): "This week" / "Last N
  weeks"; a whole month is captioned with its name. The caption is also the map's tooltip and
  accessible name, because a header too narrow for a label (XS, `HeaderFit`) draws none. The footer's
  average covers the weeks shown. Split tiles (width at Expanded) always hold the whole month
  (squares shrink to 9). `MoodGrid.Window` is gone.
- **[S-1571]** Habits one height class down (Regular, e.g. M): the height no whole row can use
  carries the summary's line ("3 of 5 done today · 71%") without its bar; the full strip with bar
  stays at Expanded height.
- **[S-1380]** Config (original S-21..S-23, S-33, S-46..S-48, S-71): `View` Grid (default) / List /
  Details, `GridSize` and `ListSize` Small / Medium / Large, `GridStretchHeight`, `GridIconOnly`,
  `ShowFileExtension`, `ClickMode` (shared `ItemClickMode`, Double by default). Enums are written as
  names; `Repair` brings unknown values back, drops null items and mends blank or repeated ids.
  `ShowTarget` and `OpenOnSingleClick` are gone (the details view and `ClickMode` replace them).
- **[S-1381]** Display name: label, else host without `www.`, else the Windows location's name, else
  the file name without its extension unless `ShowFileExtension` (original S-05, S-18, S-48).
- **[S-1382]** Grid ladder (`ShortcutLayout.Grid`): tiles keep the chosen size and the columns share
  out the width; L and XL step the size up once; XL adds a caption line under the name (host, the
  folder it is in, or "Windows location"). Names go, then tiles shrink (down to a 16 px icon), only
  when keeping them would leave fewer than four items (or all of them) on screen: XS 3×2 icon-only,
  S icons, M 3 columns with names, Wide one row of five named tiles, Tall 2 columns. Stretch shares
  the height out when every row fits. A grid whose items all fit sits centred as a group.
- **[S-1383]** Real icons: a file, folder, executable or `shell:` location gets Explorer's icon
  through `IShellItemImageFactory` (icon only, never a thumbnail) at twice the drawn size, one
  extraction at a time (concurrent first requests return E_PENDING; one immediate re-ask, no wait),
  cached per path, size and variant; a web address gets the site's own `/favicon.ico` through the
  shared guarded `FaviconCache` (no third-party service). The kind glyph shows until the icon
  arrives and stays when there is none. List and details rows: 32 / 40 / 48 high (one step smaller
  on a short tile), the target as a caption line on L/XL, two columns on XL and on a Wide strip.
- **[S-1384]** Details view: name; target column from Wide up; kind column (Web address / Windows
  location / File / Folder / Missing / Not allowed) on XL; column header when it has the target
  column and room for two rows under it. XL (all views) adds a footer: "N shortcuts", "N of M shown"
  under a search, "N selected", "N missing".
- **[S-1385]** Search (original S-43): the header's search button (or the card menu) opens a box
  above the items; it narrows to items whose shown name or target holds the text, ignoring case.
  Escape closes it and clears the query; Enter opens the first match. Typing rides the keyboard lease.
- **[S-1386]** Broken targets (original S-44): a local path is checked on attach and every minute on
  the thread pool; a missing one is dimmed, struck through and its tooltip says "(target missing)".
  Web and shell items are never marked. A UNC path is never probed (no icon, no existence check):
  touching it connects to that server with the user's credentials, and the path came from a file
  anything can rewrite. It still opens when clicked. A refused target is dimmed with a warning glyph
  and its refusal in the tooltip, and cannot be opened.
- **[S-1387]** Item menu (original S-26): Open (disabled when refused) or "Open N" on a right-click
  inside a multi-selection; web: Copy URL; file: Open file location, Copy path; shell: Copy path;
  Move to start / up / down; Rename (off for several); Remove / Remove N (danger). Remove keeps one
  step of "Undo remove" on the card menu. Rename edits in place (Enter / losing focus commits,
  Escape cancels); a name equal to the derived one clears the label.
- **[S-1388]** Settings (original S-16, S-45, S-61): Display first, then Add (file / folder pickers
  and one switch per Windows location with its real icon; on adds it, off removes every item
  pointing there; the switches follow the list), the list editor last.
- **[S-1389]** Recycle Bin (original S-75): an item pointing at `shell:RecycleBinFolder` shows a
  count badge ("99+" cap; a dot under a 24 px icon), none when empty or unknown; tooltip
  "name — N items (size)" / "name — empty". `RecycleBinStatus` is process-wide, single-flight, reuses
  an answer younger than 5 s, is asked every 15 s on the shared ticker only by widgets holding the
  bin, keeps the last answer on failure, and the icon is fetched per state (empty / full).
- **[S-1390]** Selection (original S-40..S-42, S-62, S-63): click selects one, Ctrl toggles, Shift
  selects the range from the anchor (Ctrl+Shift adds it); right-click outside the selection moves it;
  a background press past 5 px draws a rubber band (accent fill at 20 %) that selects live, Ctrl adds
  to the selection; a background click clears it. Keys while the widget holds the lease (`IWidgetKeys`,
  [S-800]): arrows (Up/Down by a row of the grid), Home/End, PageUp/PageDown, Space toggles,
  Enter opens the selection, Delete removes it, F2 renames, the Menu key opens the item menu,
  Escape cancels rename, then closes search, then clears the selection. Ctrl+A is "Select all" on the
  card menu (the lease never maps Ctrl chords, [S-803]).
- **[S-1391]** Card menu (original S-27, S-49): Search, Open on single / double click, Select all,
  Undo remove. The view, sizes, icons only, stretch and file extensions are settings only ([S-1746]).
- **[S-1392]** Items enter the widget's own surface through one method (`AddItems`); every
  target-specific choice (icon, menu, open, health) switches on `LaunchKind`. The seams PD builds on.
- **[S-1393]** A new Shortcuts widget starts with the Recycle Bin and This PC (original S-20); the
  "under the clock on a new screen" placement belongs to the default layout and is not ported here.

Not ported: Ctrl+Z op log (S-36: one-step "Undo remove" instead); "Change file…" from the widget
(the picker needs an owner window the desktop layer cannot lend; the target is edited in settings);
the dedicated "Add shortcut" window (S-66: the settings page carries the same affordances);
drop, drag-to-reorder, cross-widget drag and app layouts (S-28..S-38, S-50..S-74: package PD);
favicons are cached per run in memory, not on disk.

## Network safety (WP6)

Classic `widgets/netguard` (NG-01..07) and `widgets/netproxy` (PX-01..10), built on the guard of
[S-1301]. Owning files: `src/Themia.Core/Widgets/NetworkGuard.cs` (address classes, verdicts),
`src/Themia.Core/Net/ConnectRules.cs` (proxy hop, routes, CONNECT), `src/Themia.App/Services/Web/NetGuard.cs`
(resolve, judge, connect, the switch), `src/Themia.App/Services/Net/GuardedHttp.cs` (the one HTTP
client factory, connect callback, per-request check), `src/Themia.App/Services/Net/GuardedTcp.cs`
(raw TLS for IMAP, tunnel). Tests: `NetworkSafetyTests` (`S96x_`).

- **[S-960]** One guard, one connect path. Every HTTP client is built by `GuardedHttp.Create` — the
  shared `WebFetch` client (feeds, favicons, weather, quotes and symbol search, the photo of the day,
  the public address) and the `AccountHttp` client (sign-in, token refresh, Microsoft Graph incl.
  OneDrive, Google, GitHub incl. a user-typed server, Claude usage, CalDAV/CardDAV) — and every
  connection it opens goes through one `ConnectCallback`. IMAP (account check and every sync) opens
  through `GuardedTcp.OpenTlsAsync`. Both end in `NetGuard.OpenSocketAsync`. A refusal is a
  `NetRefusedException` carrying the sentence ("x is on the local network. Turn on local network
  access in Settings › General to reach it"): `FetchStatus.Refused` in a widget,
  `AccountError.Refused` for an account. Not guarded, as in the original: the camera stream (a LAN
  camera is its point).
- **[S-961]** Address classes (NG-01), one table for the whole app (`NetworkGuard.IsBlocked`):
  [S-1301]'s list plus site-local fec0::/10, and an IPv6 address carrying an IPv4 one — mapped
  ::ffff:a.b.c.d, IPv4-compatible ::a.b.c.d, NAT64 64:ff9b::/96 — is judged as that IPv4 address.
- **[S-962]** DNS-rebinding resistance: a direct connection resolves the name once, judges every
  address (one bad address refuses the connection), and connects only to the addresses it judged,
  so the checked address is the connected address — on every new connection, not when a feed or an
  account was added (the classic app's "re-validate the stored host before reconnecting"). A name
  that resolves to nothing fails as HostNotFound. An IP literal is judged as written.
- **[S-963]** Proxy as the original had it: Windows is the only source (`HttpClient.DefaultProxy`:
  manual box, automatic WPAD / setup script, the bypass list incl. `<local>`); a failure to read it
  is direct (PX-05). The proxy's own address is never judged (PX-07, proxies live on 10.x). The
  destination behind a proxy is checked with NG-07: a literal or a name that resolves here is
  judged, a name only the proxy can resolve is allowed. Checked per request (`GuardedHandler`,
  because a proxied plain-http request rides the proxy's pooled connection) and again in the connect
  callback for a tunnel. A connection to another endpoint than the request's own authority is the
  proxy's (`ConnectRules.IsProxyHop`).
- **[S-964]** IMAP through a proxy (PX-08): the service is put to Windows as `https://host:port/`; an
  HTTP proxy gets `CONNECT host:port HTTP/1.1` with Host and the app's user agent, the reply is read
  one byte at a time to the blank line (16 KB cap), and only a 2xx opens the tunnel. A 407 says the
  proxy requires authentication; another status is shown with the proxy's own status line; a SOCKS
  proxy is refused out loud rather than bypassed. Bounded by the IMAP job's deadline.
- **[S-965]** Account servers follow the switch. A user-typed CalDAV/CardDAV, IMAP or GitHub server
  on this computer or the local network (a NAS) is refused while **Local network access** is off and
  reached when it is on, as in the classic app (NG-04). This reverses the earlier "a NAS is always
  legitimate" exemption. The settings row says so.
- **[S-966]** Redirects: both clients follow them by hand, never the stack, so every hop is a new
  request through the guard. One rule for which hops to follow (`RedirectRules.MayFollow`, [S-519]):
  http(s) only, never https→http, never from a public host into local space; at most 5 hops.
  `RedirectRules.IsLocal` uses the guard's address classes (CGNAT, documentation and embedded IPv4
  now count as local).
- **[S-967]** "This machine only" (`NetReach.ThisMachine`): the loopback hardware monitor's
  `localhost:8085` is fetched by a second client from the same factory whose connections admit
  loopback addresses only, whatever the switch says, and are never proxied.
- **[S-968]** One user agent for every HTTP and CONNECT request (PX-10); TLS 1.2 or newer for HTTP
  and IMAP; timeouts and caps: web 8 s connect / 20 s request / 4 MB body, accounts 10 s connect /
  30 s per request / 16 MB body, IMAP 30 s per job, 1 MB line, literal cap; favicons 64 KB.
- **[S-969]** The switch is read on every connection (`NetGuard.AllowPrivate`, wired from
  `AppPreferences.AllowPrivateNetwork`), default off, so nothing reaches the LAN before the app has
  read its settings (NG-04).

Not ported: proxy authentication (classic known limit; Windows stores no proxy credentials) and
proxy failover; the classic per-scheme+host proxy cache and its invalidation (PX-04) — .NET's
`DefaultProxy` owns caching and notices setting changes itself; the updater path (PX-09; no updater).

## Widget picker: search and adding into containers (WP5)

Search is `Core/Widgets/WidgetSearch.cs` + `WidgetKeywords.cs` (ported from `widgetSearch.ts`,
`searchKeywords.ts`, WidgetPicker.SPEC S-01..S-11; tests `WidgetSearchTests`, the original's cases);
the picker body is `Shell/WidgetPicker.cs`, opened from `DesktopWindow.Picker.cs`.

- **[S-940]** A query matches six weighted fields: label 1.0, curated keywords 0.9, the kind's name
  split on camel case 0.8, category title 0.55, account providers 0.55, description 0.45 (S-01).
- **[S-941]** Both sides are lower-cased, accents folded (`Grüße` ≡ `grusse`, ß → ss) and split on
  anything that is not a letter or digit; unsegmented scripts stay one token (S-02). Accents fold
  through a Latin table, not `string.Normalize`: the app runs with invariant globalization, where
  decomposition is a no-op and the accent would never come off.
- **[S-942]** A multi-word query is an AND: every word must land in some field (S-03).
- **[S-943]** Every kind has curated English synonyms, plus providers for account widgets (S-04);
  a test keeps the list complete as kinds are added.
- **[S-944]** Typo budget 0 / 1 / 2 edits for ≤3 / 4–6 / ≥7 characters, Damerau-Levenshtein, a
  mistyped prefix matches, short tokens must keep their first letter (S-07, S-08).
- **[S-945]** Descriptions match literally only, never fuzzily (S-11).
- **[S-946]** Results are best first, ties by label; a score of 0 drops the widget (S-05, S-10).
- **[S-947]** Exact > word > prefix > word prefix > substring > typo, plus a bonus when the whole
  query is a prefix of the label (S-09).
- **[S-948]** The picker opens on a search box above the category columns. Typing replaces the columns
  with one flat ranked list (name, description, category) in the columns' place and size, so the
  flyout does not jump; clearing the box brings the columns back (S-05). No match says so.
- **[S-949]** Keyboard: the box has focus when the picker opens, so typing searches; Up / Down move a
  highlight through what is shown (the columns in reading order, or the results); a search starts on
  the best match so Enter takes it; Enter picks; Escape closes. The highlight is drawn, not focused,
  so the caret stays in the box.
- **[S-951]** A widget can be created directly inside a container: the target must exist on this
  display's view, be a container, and leave room under `WidgetTree.MaxDepth`. The rule is the same
  for every kind, containers included (the original allowed no nesting; this app allows it to the
  depth limit, as "Move into" does).
- **[S-952]** In a group it gets its default size (converted against this display), inset 3% from the
  area's edges below a 40 DIP allowance for the group's header, stepped 6% down and right per member
  already there (restarting after five) and always held inside the area (`areaChildRect`, S-36).
- **[S-953]** In a stack or a tab strip its stored rect is the container's own (where it returns to if
  taken out, S-33) and it goes last. One write with the parent already set, never add-then-reparent.
- **[S-955]** The picker is rebuilt on every open. When this display has containers that can take a
  widget, an "Add to" choice sits beside the search: Desktop, then each container by its title
  (duplicates numbered). A container that is selected when the picker opens is preselected.
- **[S-956]** Picking with Desktop starts click-to-place ([S-421]); picking with a container adds the
  widget at once and selects it. The settings window is not opened (the original opened it; [S-421]
  keeps adding and configuring as two decisions).
- **[S-957]** Debug builds render the open picker: `Themia.exe --picker-png <out.png> [--search text] [--into]`.

Not ported: picker row previews (`WidgetRowPreview`) and the group-by choice (`pickerGroups.ts`) — the
columns are the grouping; the pro lock (licensing is out of scope). Pending other lanes: an "Add
widget inside…" entry on a container's own menu and empty state, which needs a hook in
`WidgetCard`'s menu or the container views (PA/WP1 files) to open this picker preset to that
container; drop-to-join, child reorder by drag, group header side and members in the clipboard
(B13/B14) live in `WidgetCard`/`DesktopWindow` drag code and the group files (PA).

## Desktop integration and suspend (WP3)

Game mode, Windows' desktop icons, the click clean-up a non-activating surface needs, and start with
Windows for both installs. Rules in `Themia.Core/Shell/{GameMode,DesktopIconRules,AutostartRules,
ShellClickRules}.cs` (tests `DesktopIntegrationTests`); Win32 in `Interop/{UserNotificationState,
FullscreenWatch,DesktopIcons,DesktopFolderView,ShellClicks}.cs`; host in `Services/AppHost.Suspend.cs`,
`Services/Autostart.cs`; surface in `Shell/DesktopWindow.Integration.cs`; rows in
`Shell/Config/ConfigPages.General.Desktop.cs`.

- **[S-920]** Game mode (General › "Pause widgets during full-screen apps", off by default, original
  `game_mode.rs`/`gameMode.ts`): the widgets are suspended while `SHQueryUserNotificationState`
  reports an exclusive Direct3D app, a full-screen window (`QUNS_BUSY`: borderless games, full-screen
  video) or presentation settings, and resumed when it no longer does. One pure decision
  (`GameMode.Decide(enabled, state, foreground, suspended)`) from a fresh query on every check. The
  row shows "Paused now" while it holds the widgets.
- **[S-921]** Switching it off resumes at once; off, no hook, window or check exists at all.
- **[S-922]** Never suspended while a window of this process has the foreground: an edit-mode surface
  spans its monitor and Windows calls that "busy".
- **[S-923]** Fail open: a failed query or an unknown state never suspends.
- **[S-924]** Hold, don't churn: Start/Search/taskbar in front, no foreground window, or the lock
  screen keep whatever is in effect, so opening Start over a game does not rebuild every widget.
- **[S-925]** Presentation settings suspend whatever is in front.
- **[S-926]** Otherwise only an application in front counts; the desktop in front (Show Desktop over
  a game) resumes.
- **[S-927]** Event-driven, no polling (the original polled every 2 s): a hidden top-level window
  hears the shell hook (`HSHELL_WINDOWACTIVATED`/`RUDEAPPACTIVATED`/`MONITORCHANGED`/`WINDOWDESTROYED`,
  which is how a window going full-screen in place is heard) and `WM_DISPLAYCHANGE`, plus an
  out-of-context `EVENT_SYSTEM_FOREGROUND` hook. Each event only queues one coalesced low-priority
  check. The first check runs synchronously before the first surface is built, so an autostart under
  a running game never flashes the widgets.
- **[S-928]** Suspending = leave edit mode, then every surface detaches all its widgets (each widget
  already stops its timers, samplers, feeds and watchers on detach, and the shared services go idle
  with their last subscriber), leaves the z-order controller and hides; the controller's backstop
  timer stops and the shared ticker pauses. A surface the display pass builds while suspended starts
  suspended. Screen time keeps recording on purpose ([S-459]: the game is what it counts).
- **[S-929]** Resuming re-tracks and shows each surface, rebuilds its widgets from the state (each
  fires at once, so nothing shows pre-game data), unpauses the ticker and requests a display pass for
  any mode or monitor change the game made.
- **[S-930]** "Show desktop icons" (original DI-01..05): posted as the shell's own command
  (`WM_COMMAND 0x7402` to `SHELLDLL_DefView`), so Explorer persists it and the desktop menu's check
  mark agrees; Themia never re-asserts or restores it (it survives quitting Themia and reboots). The
  shell windows are looked up on every use (Progman, or a WorkerW in between), never cached.
- **[S-931]** The command is a toggle: posted only when the live state (`IsWindowVisible` of the icon
  list) is known and differs; an unreadable shell posts nothing.
- **[S-932]** Windows owns the state; `Preferences.DesktopIconsVisible` is a first-paint cache. The
  switch adopts the live state on every visit and, while on screen, on the icon list's own
  `EVENT_OBJECT_SHOW/HIDE` (a WinEvent hook scoped to that one Explorer thread) — event-driven,
  replacing the original's 1 s poll. Adopting writes only on a known disagreement.
- **[S-933]** Start with Windows, picked by package identity: unpackaged → `HKCU\…\Run\Themia`;
  packaged (MSIX) → the manifest's startup task, id `ThemiaStartup` (`Package.appxmanifest` declares
  `desktop:StartupTask TaskId="ThemiaStartup" Enabled="false"`). One switch, same states and advice.
- **[S-934]** The Run value is `"<exe>" --autostart`; a startup-task activation counts the same.
  `Autostart.LaunchSource` tells a sign-in launch from a manual one (read once in `App.OnLaunched`).
- **[S-935]** A change that did not take says what to do (original AS-04/05): an inline note under
  the row (Windows switched it off → Settings › Apps › Startup; policy; access denied; other), with the
  raw error in small print under the advice, never alone. The switch stays clickable and shows what
  was reached.
- **[S-936]** "On" means it will actually run (original AS-07): the Run value must launch this
  executable (a stale path is off) and Windows' own switch (`Explorer\StartupApproved\Run`, low bit
  of the first byte = disabled) must allow it. Turning it on from Themia removes that record, the
  user's newest choice winning. Read fresh on every visit; `Design.RunAtStartup` is a first-paint cache.
- **[S-937]** Packaged states map from `StartupTaskState`; "disabled by user" can only be undone in
  Windows (advice says where), policy states are reported as such, unknown values read as unknown.
- **[S-938]** Click clean-up (original `dismiss_start_menu_if_open`, `dismiss_context_menu_if_open`,
  `clear_desktop_selection`): every press on a surface (handled or not), outside edit mode, sends one
  Escape when Start/Search (a `CoreWindow` of StartMenuExperienceHost/SearchHost/SearchApp) has the
  foreground or a shell context menu (`#32768`, or the Windows 11 menu's popup owned by
  explorer.exe) is open; in edit mode the press activates the surface and Windows closes them itself.
  Every press also clears the desktop's icon selection, off the UI thread and single-flight: a
  bounded `LVM_GETSELECTEDCOUNT` first, then — only if something is selected — the desktop's
  `IShellView::SelectItem(null, SVSI_DESELECTOTHERS)` through `IShellWindows` (not the original's
  `WriteProcessMemory` into Explorer, which security software flags as injection).

Not ported: the original's corner "paused" pill (the surfaces are hidden while suspended; the
General row shows "Paused now"); the Scheduled-Task autostart and its probe (AS-01/06: by decision
the unpackaged build keeps the Run entry, the packaged build uses its startup task); the pre-0.15
desktop-icon repair (DI-06, no backwards compatibility); suspending on session lock (not game mode).

## Settings navigation icons (NAV-ICONS)

The settings window's navigation pane draws its entries with full-colour icons, the way Windows 11
Settings does (System, Bluetooth, Personalization, Accounts...). Code: `Shell/Config/NavIcons.cs`,
`ConfigWindow.Navigation.cs`, the `Icon` column of `ConfigPages.AppPages`; tests `NavIconAssetTests`.

- **[S-1600]** The icons are Microsoft's Fluent UI System Icons "Color" set (the family Windows uses),
  bundled unmodified as SVG in `src/Themia.App/Assets/NavIcons/` (content files, no package). One
  per entry, echoing the Windows Settings equivalent where there is one: General = gear (`settings`),
  Appearance = Personalization's brush (`paint_brush`), Accounts = Accounts' person (`person`),
  About = `question_circle` (the set has no
  info icon), Views = `content_view`, Displays = System's screen (`laptop`; the set has
  no desktop monitor). A new app page names its icon in its `AppPages` line.
- **[S-1601]** Each icon is drawn by `SvgImageSource` in a 20-DIP icon box (NavigationView's default
  16 is sized for font glyphs; the colour icons carry their own padding and read too small there),
  from the icon's 20-DIP design, rasterised at exactly the pixels the box covers on the window's
  display (20 px at 100 %, 40 px at 200 %) and re-rasterised when the window moves to a display
  with another scale. Colours are the icon's own in light and dark, as in Windows Settings.
- **[S-1602]** Only the pane's entries change (it lists pages only, [S-1830]); settings-card row icons,
  group headers and "Find a setting" suggestions stay monochrome glyphs, as in Windows Settings.
- **[S-1603]** The MIT notice for the icons is in `THIRD-PARTY-NOTICES.md` at the repository root,
  listing every bundled file, and ships beside `Themia.exe` in every build.
- **[S-1604]** A bundled icon uses only shapes, groups and gradients (what Direct2D's SVG renderer
  draws) and the 20-DIP view box; a test rejects one with filters, masks or other features that
  would silently drop out.

## Containers and shell hooks (PA)

Group, stack and tabs brought to the original, plus the card hooks other widgets use. Rules in Core
`LeaseKeyRouting`, `HeaderRules`, `ContainerRules`, `WidgetZOrder`, `CardFrame`, `GroupRules`,
`ContainerLayout`, `WidgetClipboard`/`WidgetTransfer` (tests `ContainersShellTests`); carried out by
`WidgetCard`, `DesktopWindow.Containers.cs` / `.Layout.cs` / `.Input.cs`, the three views and
`ContainerChildrenRows`.

- **[S-860]** Lease keys reach the holder **first**: the surface listens to the tunnelling
  `PreviewKeyDown` and offers the key to the leased view before any control inside its card. A
  `ScrollViewer` handles arrows, Page Up/Down and Home/End in the bubbling pass, so the old
  bubble-only handler never saw them and list navigation was dead in every scrolling list. A key the
  view does not use carries on to the focused control. While a text editor has focus every key but
  Escape is still the box's ([S-804]); Escape ends the lease in the bubble, after a box that clears
  on Escape had it.
- **[S-861]** A group's title bar toggles it: the whole band at rest, the chevron button in both
  modes (edit mode is where the user needs hidden members back). The chevron points at the space the
  area opens into while closed and back at the bar while open.
- **[S-862]** `HeaderPlacement.Left` / `Right`: the card's one header turned a quarter to run up the
  side edge (reads bottom to top, icon at the bottom, `SidewaysPanel`). Groups only (`HeaderRules`);
  a side header on another widget is repaired to Top on load. The settings offer it only for groups.
- **[S-863]** A group paints its **area** itself: a faint wash (`SubtleFillColorSecondary`), or the
  user's fill at its opacity; Background "None" clears the wash. The card paints its surface behind
  the bar only (`IWidgetArea`). Settings: icon (`IconChoice`), fill (colour picker, default = the
  wash), fill opacity, then the member list. No member count (the original's group [S-05]).
  Collapse is not mirrored in the settings — the bar owns it.
- **[S-864]** A stack has at most one child open and **every child starts closed**; a row click
  opens that child and closes the open one, a click on the open row closes it. A stale open id is
  "nothing open" and is not rewritten.
- **[S-865]** Stack ladder (`ContainerLayout.Plan`): rows of 40 — name only at XS, icon + name from
  S; the open child takes what the rows leave; under 100 DIPs on a wide tile the rows become a strip
  along the top; under 40 the open child is not drawn. Rows that do not fit are clipped only below
  the open child. Keys (lease): Up/Down/Home/End move the row cursor, Enter/Space toggle, Menu opens
  the row menu.
- **[S-866]** Container children: a row's (tab's) menu has Configure, Move up/down (left/right;
  disabled at the ends) and Take out (back on the desktop at the rect it had, [S-411a]); in edit mode
  rows and tabs reorder by drag (`ChildReorder`, slot = `ContainerLayout.DropSlot`, one
  `MoveChildTo`). The settings page lists the members (`ContainerChildrenRows`): settings, move
  (stack/tabs only — a group's members have places, not an order), "Remove from group"/"Take out",
  then "Add a widget inside". Order is the view's widget list ([S-410]); a move swaps neighbours in it.
- **[S-867]** Stack and Tabs start headerless (their rows and tabs are their chrome). A stack's open
  child is drawn without its own header (the stack row names it); a tab strip's shown child keeps its
  own live header, as in the original.
- **[S-868]** Tabs: the selected child, else the first; a stale id is ignored, not rewritten. Taps
  are inert in edit mode. Keys: Left/Right/Home/End switch tabs.
- **[S-869]** Tabs ladder: icon + name at W ≥ Expanded when all fit, names when they fit, icons alone
  otherwise (the strip scrolls if even those do not fit).
- **[S-870]** An empty container shows a dashed placeholder with **Add widget** in both modes and
  "or drag widgets in here" in edit mode. Add widget — there, in the container's card menu ("Add
  widget inside…", first) and on its settings page — opens the widget picker with the container
  chosen in its "Add to" box ([S-955]).
- **[S-871]** Collapsing a group shrinks the **card** to its header bar on the header's edge
  (`CardFrame.Collapsed`, bar = `WidgetCard.BarThickness` + gap), in both modes; the stored rect is
  untouched, so expanding restores it exactly. A collapsed group with a hidden header shows a top
  bar. The header does not shed on a collapsed card.
- **[S-872]** The hit region follows **paint** (`CardFrame.Hit`): a hidden widget takes nothing; a
  group whose area paints nothing (Background None, or a fill at 0 opacity) and has members takes only
  its bar; an empty one keeps its rect (the placeholder is the way to fill it). A child a stack or tab
  strip draws is never masked at its own stored rect (it used to leave a dead patch of desktop there),
  and members of a collapsed group are not masked.
- **[S-873]** `IWidgetSurface.SetHidden(bool)`: the card stops drawing its fill, border and content
  and leaves the hit region; the view stays attached so it can show itself again. Edit mode always
  draws it, at half opacity. Not persisted. (For Music's hide-when-idle.)
- **[S-874]** Bring to front / Send to back on every grid-placed widget's card menu (disabled when it
  already is). The order is the view's widget list, persisted with it; a container moves with
  everything inside it. Paint order (`WidgetZOrder.PaintOrder`) is view order with each group directly
  under its lowest member ([S-415], nested groups outermost first), applied through `Canvas.ZIndex`
  — nothing is removed and re-inserted in the tree any more.
- **[S-875]** Drop to join: a move in edit mode that ends with the pointer over a container puts the
  widget in the topmost one that will take it (not itself or its own descendant, not a collapsed
  group); dropped outside the group it was in, it leaves that group. Membership only — the widget
  stays where it was dropped. A widget a stack or tab strip lays out does not drag on its own: the
  press goes to the container's card.
- **[S-876]** A container is copied, cut and pasted **with its members** (a copy: fresh ids per
  paste, parents remapped; every rect shifted by the block's one translation, [S-1584]); a pasted cut
  moves the original with everything inside it, ids kept ([S-1590]). Dragging a container to another display's view takes its members too; the
  moved widget leaves a container that stays behind.
- **[S-877]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported: the group's open/close animation (original group [S-16]); a container drop highlight
while dragging (the drop works, the target is not lit); pop-out of a child (out of scope). Not
live-verified: lease key routing in the real surface, drag-reorder and drop-to-join with the pointer,
the collapsed bar's hit region, the picker opening from a card menu outside edit mode.

## Microsoft Store package and licensing

Themia is sold and updated through the Microsoft Store only (no Lemon Squeezy, no updater, no other
installer). Release steps and the manual sideload test: `docs/STORE.md`.

- **[S-1060]** One code base, two shapes. Every build is unpackaged (F5, `dotnet build`,
  `scripts/publish.ps1`) except the Store package, which `scripts/package.ps1` builds as a
  single-project MSIX (`-p:ThemiaPackage=true` switches `WindowsPackageType` to MSIX for that build)
  with Native AOT and the Windows App SDK self-contained, **unsigned** (the Store signs it), into
  `artifacts\store\Themia_<version>_x64.msixupload` (+ `.msix`). The version lives only in
  `Package.appxmanifest` `Identity/@Version` (`Major.Minor.Build.0`) and is stamped into the exe.
  Identity name and publisher are marked placeholders until Partner Center reserves them; the script
  warns while they remain. The script checks the package holds `Themia.exe`, `resources.pri` and
  compiled XAML, no managed Themia assembly and no signature. No certificate is made, imported or
  trusted by any script. Logos are drawn at every requested size from the Themia mark by
  `scripts/package-logos.py` into `Assets/Package/`.
- **[S-1061]** Capabilities: `runFullTrust`, plus `radios` and `bluetooth` for the Bluetooth widget.
  Not declared: `location` (nothing reads the Wi-Fi name since [S-1762]), `internetClient` (not
  enforced for full trust), and every broad capability.
- **[S-1062]** The manifest declares the `windows.startupTask` `ThemiaStartup` (disabled by default)
  that the packaged autostart ([S-937]) asks for. No event-log source is declared: MSIX can only
  register one with a message file, and Themia ships none; `EventLogSink` already writes under the
  unregistered source (Application log, "description cannot be found" preamble) and is best-effort.
- **[S-1063]** `LicenseService` (one, owned by the host) asks `StoreContext.GetAppLicenseAsync` at
  start for the durable **Pro** add-on (`StoreIds.ProAddOn`, a marked placeholder until Partner
  Center issues the id), single-flight; re-asks on `OfflineLicensesChanged`, after a purchase and on
  "Restore purchases". Purchases use `RequestPurchaseAsync`, with the settings window handed to the
  Store through `IInitializeWithWindow`. `Changed` fires on the UI thread. Without package identity
  the Store is never touched.
- **[S-1064]** Tier: no package identity: **Unrestricted** ("Developer build"); the Store's "owned":
  Pro; "not owned": Free; no answer: Pro only inside the offline grace, else Free. An unknown or
  corrupt state never unlocks Pro.
- **[S-1065]** Free tier: every widget type, at most **10 widgets** (all views, containers and their
  members each count) and **2 views**, as the original. Gates at every create: placement start and
  landing, paste (net of a cut's original; a group pastes whole or not at all), duplicate, add into a
  container (picker) and new view. A newly seen display creates nothing ([S-1705]), so it needs no gate. A refusal creates nothing and says why in a notice ([S-881]) whose "See plans"
  opens Settings › License, which repeats it once. Nothing
  is ever deleted or hidden for being over the limit; moving, editing and deleting are never gated.
  Import is a replacement, not a create, and is not gated.
- **[S-1066]** Offline grace: 7 days from the last Store "owned" on this PC, when the Store cannot be
  asked (also before its first answer, so a Pro user never starts limited). A confirmation dated
  more than 10 minutes in the future gives none.
- **[S-1067]** Add-on ownership: an active licence whose SKU id is `<productId>/<sku>`. A product id
  that is not 12 upper-case letters/digits (the placeholder) is never owned and is not offered.
- **[S-1068]** Settings › License (footer, above About): status (title and explanation for every
  basis), on the free tier the widget and view allowance as "n of limit" bars, "Themia Pro" with
  Upgrade (the Store's price once known; disabled with "not on sale yet" while the id is the
  placeholder) and "Restore purchases". A refusal that opened the page shows once as a warning bar.
  Redraws live on `Changed`. Debug renders: `--settings-png License out.png --license free|refused|pro|grace|offline`.
- **[S-1069]** The grace record is `%LOCALAPPDATA%\Themia\license.dat`, `themia-license:v1:<unix
  seconds>` sealed with DPAPI (current user), written atomically on "owned", deleted on "not owned",
  untouched when the Store is unreachable. Absent, unreadable, plain text or malformed: no grace.
- **[S-1070]** Uninstall, packaged: the data cannot be kept (Windows deletes the package's
  redirected AppData with it), so the dialog offers no "also delete" box and says the data goes; the
  folders are deleted in-process before the package is removed. Loose builds keep the choice ([S-1553]).
- **[S-1071]** Uninstall steps after the app has closed, in order: autostart off
  (`Autostart.SetAsync(false)`), then packaged: delete data, `RemovePackageAsync` on the own package
  (fallback `ms-settings:appsfeatures-app`); loose: start the `--purge-data` helper (only when
  there are folders), open Installed apps.
- Not ported: Lemon Squeezy keys, activation counting, masked-key reveal, the newsletter row and the
  `THEMIA-DEV-PRO` bypass (Store only, by decision); per-device limits (the Store ties Pro to the
  Microsoft account). Not verified here: anything needing a published Store listing (purchase
  dialog, price, licence events, offline grace with a real Store), and the sideloaded package itself
  (no certificate may be made on this machine) — see `docs/STORE.md`.

## Calculator keypad and colour swatch modes (PK)

- **[S-1500]** The colour swatch is one kind with `Mode {Solid, Gradient, Palette}`, default **Solid**
  — the original widget: the whole tile in one colour. Palette is the copy-from list the port had
  built. It starts headerless ([S-700]) at 120×120, as the original's 110×110 block.
- **[S-1501]** Solid: `Color` + `Alpha` (0..1). Gradient: `StartColor`/`StartAlpha`,
  `EndColor`/`EndAlpha` (default `#34D399`), `Angle` (default 135). Colours are `#RRGGBB` from the
  shared colour picker ([S-1549]), never typed; empty means the accent (the original's default was
  the app accent). Opacities are sliders in percent.
- **[S-1502]** Load repair: an unknown mode is Solid, an unreadable colour its slot's default,
  opacities clamp to 0..1 (NaN = opaque), the angle is brought into 0..360 — each logged.
- **[S-1503]** The gradient follows CSS `linear-gradient(<angle>deg)`: 0° up, 90° right, clockwise;
  the axis runs through the centre and is long enough that the two corners furthest along it sit
  exactly on the stops, on any box shape (`GradientAxis.For`, absolute mapping recomputed per size —
  XAML's relative mapping skews non-right angles on a non-square box).
- **[S-1504]** The block fills the whole slot with no inset; the card clips its corners. A solid
  default paints the theme's accent brush itself (follows an accent change in place), with the
  opacity on the element, not the shared brush.
- **[S-1505]** The original's "layer: in front / behind" is the shell's Bring to front / Send to back
  ([S-415] family) on every widget; the swatch's settings point there instead of duplicating it. Not
  ported: "edit mode ignores the layer" (the shell's order is the same in and out of edit mode).
- **[S-1506]** Palette ladder (`SwatchLayout.Plan`): under 180 wide a centred grid of chips sized to
  show every colour (min 16, then it scrolls); otherwise rows (chip, name, value) in up to 4 columns
  of ≥210, growing from 40 to 56 tall to fill the height; once even 56-tall rows would leave over a
  fifth empty, a grid of big tiles (block over name and value), column count chosen for the largest
  block (≥64).
- **[S-1507]** Palette keys through the lease: arrows move a focus ring, Enter copies, Escape lets go;
  a click copies (with "Copied" in place of the value until the pointer leaves).
- **[S-1510]** The calculator has an optional keypad (`ShowKeypad`, on): C ( ) ÷ / 7 8 9 × / 4 5 6 −
  / 1 2 3 + / ⌫ 0 . =, plus an Ans % xʸ x² row when tall. Keys are not focusable: they edit the line
  at the caret (replacing a selection) without taking the keyboard, and = files it through the same
  commit as Enter ([S-10]).
- **[S-1511]** A binary operator or x² on an empty line continues from the newest result; Ans types
  it; a negative answer is bracketed so a power squares the answer, not its magnitude.
- **[S-1512]** One decimal point per number; a point that starts a number gets its leading zero.
- **[S-1513]** In the line, Up/Down recall history expressions newest first (a focus ring marks the
  row); Down past the newest restores the draft. Escape clears a non-empty line and keeps the
  keyboard; on an empty line it ends the lease. Typing is through the text box's lease ([S-801]).
- **[S-1514]** Calculator ladder (`CalculatorLayout.Plan`): the line and its result always; then the
  newest history row; a keypad under the line where it fits (tall tiles, ≥172 wide); more whole
  rows; leftover grows the keys (to 56) then the live result (Body → Display, 12-character budget).
  A Wide strip puts the history beside the line; Expanded width with room for 5 keys puts a
  full-height keypad on the right half (6 rows when keys stay ≥44).
- **[S-1515]** Calculator defaults as the original: 4 decimal places, header hidden ([S-700]).
- **[S-1517]** With nothing typed, the result line shows the newest answer (`= 42`, tertiary) — what
  an operator or Ans continues from — so the display is never a blank box once a sum was done.
- **[S-1516]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

## Multi-select and the placement session (MULTISELECT-PLACEMENT)

Owning files: `Themia.Core/Model/WidgetSelection.cs` (`WidgetSelection`, `SelectionRules`),
`Themia.Core/Model/PlacementPayload.cs` (`PlacementPayload`, `PastePlan`, `GhostOutline`),
`Themia.Core/Geometry/BlockGeometry.cs`, `WidgetClipboard.cs`, `WidgetTransfer.ApplyPlacement`;
`AppHost.{Placement,Clipboard}.cs`, `DesktopWindow.{Selection,Picker,Input}.cs`, `WidgetCard.cs`.
Tests: `MultiSelectPlacementTests`, `WidgetClipboardTests`, `WidgetTransferTests`.

- **[S-1580]** Edit mode has a **selection** of one or many widgets, always from one view. A click
  on a card selects it alone; **Ctrl+click** adds it or takes it out without starting a drag; picking
  on a display that shows another view starts a new selection there. The last widget picked is the
  primary one, which single-widget commands (the picker's "Add to") mean.
- **[S-1581]** A left-button drag on empty surface draws a **rubber band** on that display; every
  widget the grid places whose cell it touches is selected, live, as the band changes. With Ctrl or
  Shift held at the press it adds to the selection on that display; without, it starts from nothing
  (a click on empty surface clears the selection). A widget a stack or tab strip lays out, and a
  collapsed group's hidden members, are not picked.
- **[S-1582]** **Ctrl+A** selects every widget the grid places on the display under the pointer.
- **[S-1583]** Every selected card shows it (2-px outline, full-strength handles). A widget that is
  deleted or leaves the view leaves the selection with the change. Escape clears the selection
  before a second Escape leaves edit mode.
- **[S-1584]** Dragging a selected card moves **every selected widget the grid places on that
  display** by one translation: the block's bounding box is moved, its top-left snapped to the grid
  ([S-06], Alt suspends it) and the box slid into the work area ([S-841]) — never resized — and each
  rect is then kept on screen ([S-09]). Offsets between the widgets never change. A group does not
  carry its members ([S-411]); members selected with it move in their own right. Resizing is per
  card and narrows the selection to it.
- **[S-1585]** A ghost of several widgets shows **every widget's outline** at its offset in the
  block; a stack's or tab strip's children are drawn by their container and get no outline. An
  outline too small for the icon and name shows the frame alone.
- **[S-1586]** A payload lands with its bounding box centred on the pointer (a paste, Duplicate,
  Move), or with the held widget centred on it (a drag handed to another display), then snapped and
  clamped as a block on the display under the pointer. The ghost and the landing come from the same
  calculation, so the widgets land exactly where the ghost was.
- **[S-1587]** One **placement session** for everything that produces or moves widgets: Add widget
  (a new widget of one kind), Paste (Ctrl+V and menu), Duplicate (Ctrl+D and menu), Move (menu: to
  this display or any other), and container copy/paste. No command drops a widget at a computed spot
  any more. Click lands; Escape, a right-click or leaving edit mode cancels and changes nothing. The
  licence gate ([S-1065]) runs before the ghost appears (what the session would create: a copy of N
  widgets counts N, a move 0) and again on landing.
- **[S-1588]** A payload taken from the selection holds the selected widgets with no selected
  ancestor, each with everything it holds ([S-876]), as a snapshot with original ids.
- **[S-1589]** A **copy** (paste of a copy, Duplicate) lands fresh widgets with fresh ids every time,
  members re-parented to their container's copy; the picked widgets land on the desktop (out of the
  group they were in). The clipboard is not touched by Duplicate.
- **[S-1590]** A **move** (paste of a cut, Move) lands the **same** widgets: the originals are taken
  out of whatever view they are in now and the live instances — with any config change since the
  snapshot — take the planned rects. An original deleted meanwhile is put back from the snapshot; a
  member added to a moved container after the snapshot goes along. Until the click lands the
  originals stay where they are, faded; cancel leaves them and the cut as they were.
- **[S-1591]** Dragging a selection onto a display that shows another view hands **all** of it over
  in one landing ([S-76], [S-77]) and selects it there.
- **[S-1592]** Delete, Copy, Cut, Duplicate, Move, Bring to front and Send to back act on the whole
  selection; the card menu says how many ("Delete 3 widgets"). A right-click on a selected card keeps
  the selection; on any other card it selects that card alone first. Layering keeps the order the
  selected widgets had among themselves.
- **[S-1593]** A press on a card of a multi-selection keeps the selection so the drag that may follow
  carries all of it; released without moving (3 DIPs), it narrows the selection to that card.

Not done: dropping a placement or a dragged block **into** a container (only a single dragged widget
joins one, [S-875]); a debug render of the multi-widget ghost (the outlines are pinned by
`S1585_*` instead). Not live-verified: everything pointer- and multi-monitor-driven here — the
rubber band, Ctrl+click, group drag clamping at the taskbar, hand-off of a selection across
displays, the ghost following the pointer across displays, Ctrl+A/Ctrl+D/Escape order.

## Music and Pomodoro: parity and the whole tile (PF)

- **[S-1460]** Pomodoro `Sound` (`AlarmSound`: off + the original's twelve — chime (default),
  marimba, crystal, pluck, droplet, birdsong, zen, bell, gong, arcade, digital, buzzer; listed in
  that order, gentle → insistent) and `Volume` 0..100 (default 70, row hidden while off). Unknown
  names read as chime (lenient converter); `Repair` clamps the volume. Changing either previews the
  focus cue. The old `Alarm` bool is gone (no compatibility).
- **[S-1461]** `PomodoroSounds` ports `alarmSound.ts` note for note: one note sequence plus a
  timbre per sound; the focus cue is the sequence (it rises), the break cue the same notes
  reversed. Off is the empty plan and plays nothing.
- **[S-1462]** Sounds are synthesised, not bundled: the original shipped no audio files (Web Audio
  oscillators and band-passed noise). `SoundPlan` renders the same score — sine/square/sawtooth/
  triangle (PolyBLEP-band-limited) and seeded noise through an RBJ band-pass (Q 0.8), piecewise-
  linear pitch and gain envelopes, 250 ms tail — to a 16-bit mono 44.1 kHz WAVE at volume/100.
  Deterministic; starts and ends silent. Played by `CuePlayer` ([S-153], single-flight).
- **[S-1463]** Every sound is levelled to one peak bound (0.9, the largest sum of concurrent voice
  gains, exact at envelope breakpoints); no sound clips at volume 100.
- **[S-1464]** Music `HideWhenIdle` {Never (default), NoSession, NotPlaying} (lenient → Never).
  NoSession hides while Windows reports no session (and before the first snapshot); NotPlaying
  hides any state but Playing. The view calls `IWidgetSurface.SetHidden` ([S-873]); edit mode
  always shows the card; a preview never hides. While hidden, no position ticker and no spectrum.
- **[S-1465]** The progress line is a seek bar (`MusicSeekBar`): click jumps, drag scrubs (the
  elapsed label follows), release commits through `TryChangePlaybackPositionAsync` in the
  session's own frame (StartTime + position). Only when the session reports
  `IsPlaybackPositionEnabled` and a duration (`CanSeek`); otherwise a plain line with no input.
  Capture lost mid-drag cancels. A refused seek re-reads the session.
- **[S-1466]** Pomodoro ladder: XS digits over start/pause; short strips (S, Wide) phase + digits
  left, controls right (+ skip and "Next: Break 5:00" on a wide strip), the bar along the bottom;
  M phase, digits, bar, start/reset/skip, next line; Tall/L/XL a progress ring with the digits
  inside (stacked on Tall; a side column with phase, next, controls on L/XL). Skip loads the other
  phase in full, silently, keeping the running state.
- **[S-1467]** XL also shows the focus sessions finished (dots + "N sessions done"): a flip out of
  focus counts one; skip and reset do not. In memory only, like all run state ([S-150]).
- **[S-1468]** Music keys via the keyboard lease (`IWidgetKeys`; taken only with a track shown):
  Space/Enter play/pause, Left/Right previous/next, Shift+Left/Right seek ∓5 s (new
  `WidgetKey.ShiftLeft/ShiftRight`; Shift+arrows no longer arrive as plain arrows in other views).
- **[S-1469]** The large tiles split the subtitle into artist and album (album dropped when it
  repeats the artist) and add "Playing · <app>" from the session's app id (`MusicRules.SourceName`).
- **[S-1470]** Spectrum (`ShowSpectrum`, default off): WASAPI loopback of the default render
  endpoint (console role), mixed to mono, Hann-windowed 1024-point FFT, 24 log bands from bin 1 to
  Nyquist, sqrt compression ×3.5, clamped 0..1 — the original's analyser. Float32/Int16/Int32 mix
  formats; anything else and silent packets count as zeros.
- **[S-1471]** Bars rise at once and fall 0.06 per frame (~30 fps); drawn as 24 rectangles scaled
  by render transform (no layout pass per frame), floor 3%. In silence the worker sends frames only
  until the bars are down, then nothing.
- **[S-1472]** One shared capture worker (`AudioLoopback`, `CaptureLease`): the first subscriber
  starts it, the last stops it (signalled, never joined on the UI thread); a remount that overlaps
  old and new never stops it; a worker that ended is restarted by the next subscriber. A tile
  subscribes only while the bars are on screen: shown by its ladder, playing, card not hidden, view
  loaded, not a preview. Frames are coalesced to one queued draw per tile.
- **[S-1473]** Self-healing: an `IMMNotificationClient` (immortal native object) flags a change of
  the default render device and the worker re-acquires at once; any capture error retries after
  500 ms (logged once per distinct HRESULT). COM is MTA on the worker; every interface pointer and
  the mix format are released on every path; no exception leaves the thread or a callback.
- **[S-1474]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1475]** Music ladder: XS title (+ artist) over play/pause; S/Wide lines with the transport
  beside (or under, or play only) and art on a strip, seek bar along the bottom (times beside it
  on a wide one); M art + lines + transport, seek bar with the times under it, bars between when
  there is room; Tall stacked like the media flyout (art, lines, bars, seek, times, transport);
  L/XL art up to half the width beside title/artist/album/source/40px transport/bars, seek row with
  times along the bottom.
- Not ported: the compact-header default of the original's [S-21] (WinUI header modes differ);
  `pollInterval` (legacy, unused). The spectrum frame pacing is a 33 ms wait on the worker (display
  cadence, not a correctness path); event-driven loopback capture is unreliable across drivers.
  Not live-verified: actual sound output of the cues and the preview, single-flight stop, seeking
  against real players (Spotify, browsers), loopback capture and device-switch healing, the hidden
  card's hit-region on the desktop, keys through the lease, and CPU cost while playing.

## Localization infrastructure (LOC-INFRA)

- **[S-1100]** One string layer in Core (`Themia.Core.Localization`): every UI text is a member of the
  generated `Str` enum (key `settings.language.label` → `Str.Settings_Language_Label`, summary = its
  English text), read with `Str.X.Text()`, `Str.X.Format(("name", v))` or `Str.X.Plural(n)` in
  `Strings.Current`, or `Strings.Get(locale, id)` for an explicit language. Lookup is an array read;
  no `ResourceManager`, no reflection, nothing loaded at run time (Native AOT clean).
- **[S-1101]** Source: one JSON file per language, `src/Themia.Core/Localization/Locales/<code>.json`
  (en, de, es, fr, it, ja, pl, pt, zh), flat `"dotted.key": "text"` or a plural object. en.json is the
  master (its keys, in order, are the enum). `Themia.Localization.Generator` (Roslyn incremental
  generator, build-time only) compiles them; a malformed file, a bad key, a duplicate, a key en.json
  lacks, an unknown locale file or two keys clashing on one member name is a build error
  (`THEMIALOC001`..`009`) with the file position. Each language's table is built on first use.
- **[S-1102]** Placeholders are `{name}` (ASCII letters, digits, `_`), filled in one pass; a value is
  never re-scanned; a placeholder without an argument stays visible; number arguments are formatted
  for the language (`1,500` / `1.500`).
- **[S-1103]** Plurals are CLDR cardinal categories for whole numbers (`zero one two few many other`):
  en/de/es/it one = 1; fr/pt one = 0 and 1; pl one / few (2–4, not 12–14) / many; ja/zh other only.
  `Plural(n)` fills `{count}`; a missing category uses the language's `other`, then English.
- **[S-1104]** A string the language lacks falls back to English, logged once per key and language at
  debug level; an id or key nobody has returns the key text. Never an exception, never empty.
- **[S-1105]** Completeness test (`LocalizationTests.S1105_*`): every key is in every locale or is
  listed under `"$englishOnly"` in en.json (a listed key translated everywhere fails as stale); every
  form's placeholders equal English's; plural keys are plural in every language and use only that
  language's categories; no text is blank.
- **[S-1106]** Formatting without `CultureInfo` (`LocaleFormat`, data in `LocaleData`, transcribed
  from CLDR per main region — en-US, pt-BR, zh-CN, es-ES, the rest their home country): month and
  weekday names (wide/short/narrow; Polish genitive inside dates), 8 `DateStyle`s, times on the
  language's default clock or forced 12/24 h (with seconds), integers and decimals with the
  language's grouping/decimal marks (es and pl leave 4-digit numbers ungrouped), percent, first day
  of the week. Pure functions; overloads without a locale use `Strings.Current`.
- **[S-1107]** Relative time (`LocaleFormat.Relative`): under 1 min "now", then whole minutes, hours,
  days, weeks (from 7 d), 30-day months (from 30 d), 365-day years (from 365 d), truncated; past
  "… ago", future "in …"; long and short styles, plural-correct.
- **[S-1108]** "Follow Windows": `GetUserPreferredUILanguages` (read once; changing it needs a
  sign-out), first tag whose primary subtag Themia has (`zh-CN`, `zh-TW` → zh as in the original,
  `pt-BR` → pt), else English. Walks the whole list, not only its head.
- **[S-1109]** Settings › General › Language (first row): System + the nine, each named in its own
  language. The host resolves the setting before any window is built and again on every state
  change (an import or reset), and `SetLanguage` applies it at once.
- **[S-1110]** Live switch: `Strings.Changed(Locale)` fires on the UI thread after the language
  changed (not when it is set to the same one). The settings window redraws its navigation, search
  index and open page on it (queued behind the event that caused it). Cards, widgets and the
  surfaces subscribe in the extraction round; every subscriber unsubscribes on Detach/close.
- **[S-1111]** Area fragments: a language may also be split into `Locales/<area>/<code>.json`
  (`<area>` lowercase, `-` between parts, e.g. `shell`, `widgets-a`; one folder level only), so
  parallel authors never edit the same file. All of a language's files merge into one table; English
  key (enum) order is the root file, then areas by name. A key is defined once per language: a second
  definition is `THEMIALOC008` naming both files. A fragment may only hold keys the `en.json` of its
  own area has (`THEMIALOC006`, which also covers an area with no `en.json`), so a stray or
  misplaced key fails the build; an unknown-language fragment is `THEMIALOC003`, a bad area folder
  name `THEMIALOC009`. `$englishOnly` lists keys of its own file. The root `en.json` is still required;
  the root files stay valid. The S1105 completeness tests run on the merged result; the generator
  rules are tested in-process by `LocaleFragmentTests`.
- Ported from `main`: 1,673 of the original's 1,748 keys with all nine translations (key names
  kept; `folder.showHidden_`/`showExt_` → `showHiddenMenu`/`showExtMenu`; `shortcut.appCountOne/Other`
  and `binCountOne/Other` → plural keys; `<br/>` → newline; `settings.language.system` takes the
  translations of `settings.theme.system`). Dropped as out of scope: tray, updater, telemetry consent,
  Lemon Squeezy key activation, newsletter, pop-out, always-on-top, YouTube. Not built yet (with the
  extraction round): replacing the literals, rich text (`tm`), the literal scanner (`i18nScan`), the
  widget picker's translated + English search names, Help localisation. Polish plural keys have only
  one/other until translated (few/many fall back to other). The CLDR tables want a native-speaker
  review in the translation wave. Not verified live: the language switch in the running app (no app
  run here); the General row was checked with `--settings-png General`.

## Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ)

Tests: `tests/Themia.Core.Tests/DeskParityTests.cs`.

- **[S-1480]** Time zones are IANA ids (`Asia/Tokyo`) resolved through `ZoneCatalog`: an explicit
  IANA → Windows table frozen from Windows' own `timezoneMapping.xml` (CLDR), because under
  `InvariantGlobalization` .NET cannot convert IANA ids (no ICU) while Windows ids always resolve.
  Unknown or missing zones resolve to null and the clock falls back to local [S-53]. A test checks
  every Windows id in the table resolves on the machine.
- **[S-1481]** The zone picker is a search over every listed city (one per place: old names such as
  `Asia/Calcutta` and bare `Etc/GMT±N` resolve but are not offered), cities starting with the query
  first, with "Current: <places>" (unset = local) always first. Enter takes the best match.
- **[S-1482]** Label over the time (original S-07/S-18/S-19): the custom label, else the city of a
  chosen zone, else none. Deliberate difference: choosing any city labels the clock, even one in the
  machine's own Windows zone (a Windows zone covers many cities). Labels are capped at 40 characters.
- **[S-1483]** Style Digital / Analog / Stacked (original S-10..S-13). Stacked always stacks hours
  over minutes; seconds and AM/PM ride a step down beside the minutes. A digital face still stacks on
  its own in a tall column when that draws the digits larger.
- **[S-1484]** Alignment Left / Centre / Right for the whole column (original S-25/S-26). Beside an
  analog dial the text stays left-aligned to the dial.
- **[S-1485]** Offset line None / From UTC ("UTC+2", "UTC+5:30", "UTC−3") / From local ("+8h local",
  "same as local"), minus as U+2212 (original S-08/S-16/S-23). On a wide digital strip it joins the
  date line.
- **[S-1486]** `ClockConfig.Repair`: unknown enum values, unknown zone ids and over-long labels are
  repaired to defaults on load, each logged.
- **[S-1487]** Clock ladder: face → label → date (short when narrow; + week + offset on a wide strip)
  → offset → week (H ≥ Expanded) → zone (E×E); the face takes the rest. Not ported: an XL list of
  extra world-clock rows (each world clock is its own widget, as in the original).
- **[S-1488]** The analog dial at every tier, sized by the ladder (40 px floor, rebuilt only when the
  diameter changes): dial alone on XS, text under it on panels and columns, text beside it on a wide
  strip. The second hand still sweeps (S-51, deliberate).
- **[S-1489]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1490]** Sun & moon adds civil dawn/dusk (sun's centre at −6°; none in white nights), solar
  noon with the sun's altitude, and the day's change against yesterday ("−3m 22s"; none inside the
  polar circles). Pure in `SolarPosition` / `SkyText`.
- **[S-1491]** The next full or new moon, whichever comes first, from the same linear lunation as
  the phase, so the date agrees with the disc.
- **[S-1492]** Sun & moon ladder (`IWidgetSizeAware` + `Disclosure`, replacing its own SizeChanged
  thresholds): graphic (46 px floor, S-170) → strip → rows in priority sunrise, sunset, daylight
  (+change), moon, solar noon, dawn, dusk, next full/new moon. One row = the next event. Rows stand
  beside the graphic on a strip, and go two to a line on E×E with the graphic keeping 45% of the
  height. XS and S stay graphic (+strip): a 32 px row does not fit under the 46 px floor there.
- **[S-1493]** *(Retired by [S-1746].)* The sun path's modes and the event rows are settings only;
  the widget has no menu entries of its own.
- **[S-1494]** The place is picked by city name through the shared `PlaceSearch` (Open-Meteo
  geocoder, resolved once at the pick); exact name/latitude/longitude stay editable and validated
  (S-171's refusal rule unchanged). This supersedes S-171's "no geocoding service" reason.
- **[S-1495]** Quote: the bundled list is the original's sixteen in its order, so a daily unshuffled
  rotation shows the original's quote for the day (`QUOTES[localDay % 16]`, regression-tested). The
  header button shows a random *other* quote (`QuoteRotation.RandomStep`); a tap steps to the next.
  Both stay ephemeral (original S-02).
- **[S-1496]** Quote ladder on `IWidgetSizeAware` + `Disclosure`: quote (Caption floor) → author →
  caption "Quote of the day · 3 of 16" (H ≥ Expanded); decor goes first, then the author, when the
  quote would not fit. Ceiling TitleLarge, Display on a Huge × Huge tile (replaces S-653's cap there).
- **[S-1497]** Scratchpad ink bar (original S-02 toolbar): six preset inks (`InkPalette`, shared
  with the settings picker) and the original's three nibs 2/4/8 px at the foot of the pad from
  Regular × Regular; nibs only when the width holds both; stroke count at W ≥ Expanded. Picking an
  ink leaves the eraser. Header tools still shed per S-651. Not ported: export as PNG.


## Device and location (DEVICE-AND-LOCATION)

`Themia.App/Services/CoreAudio.Default.cs`, `Widgets/{AudioFeed,VolumeView,MixerView,MixerView.Panel}.cs`;
Core `Widgets/AudioDefaultRules.cs`; tests `DeviceAndLocationTests`. The location half ([S-1610]..[S-1613],
[S-1619]: the Wi-Fi name, its consent prompt and the `location` capability) is removed with the
network meter's connection line ([S-1762]).

- **[S-1614]** In the Volume and Mixer widgets (device menu, XL mixer panel), picking a device makes it
  Windows' default output and the widget keeps following the default. "Follow the default output" and
  the device that already is the default change nothing in Windows (a bound widget just unbinds).
  The menu checks the device the widget shows (the default while following) and offers "Follow the
  default output" only when bound. The settings page's device ComboBox still binds the widget.
- **[S-1615]** The switch is `IPolicyConfig::SetDefaultEndpoint` (undocumented, CLSID
  `870AF99C-…`, IID `F8679F50-…`, vtable slot 13) on the Core Audio worker thread, for console,
  multimedia and communications in that order; a refused console role stops the switch, a later role
  that fails is logged. One `PolicyConfigClient` per pick, released on every path. Picks queued while
  the worker stops are answered "failed", never left waiting. One pick at a time per widget.
- **[S-1616]** Results: Switched (follow the default), NotSupported (class/interface missing),
  AccessDenied, DeviceGone (left as it was), Failed. Anything but Switched/DeviceGone binds the widget
  to the picked device ([S-1221]) and a notice says why ([S-880]).
- **[S-1617]** The call sits behind `IDefaultOutputSwitch`; all decisions are Core
  (`AudioDefaultRules.Plan/Classify/Continue/Outcome/After/PickAsync`) and tested against a fake. No
  test or preview card ever switches a device: previews never reach the seam ([S-1233]).
- **[S-1618]** Device tooltips: "(Windows default)" on the default, "Make … the default output" on
  the others.

Needs live verification: the default switch on real hardware (all three roles in Sound settings; refusal path on a locked-down
account).

## Views and the view bar (WP8)

Duplicate, reorder and icons for views, the original's desktop view bar, Ctrl+1..9 and the
toolbar's snap switch. Rules in Core `ViewOps` and `ViewBarRules` (tests `ViewsTests`); the bar is
`Shell/ViewBar.cs`, wired by `DesktopWindow.ViewBar.cs`. Every switch goes through
`AppHost.ShowView` → `AppState.ShowViewOn` ([S-19]).

- **[S-900]** Duplicate a view (its group on the Views page and that group's menu, the toolbar's View menu, a view bar chip's menu in edit
  mode): the copy goes right after the source, named "<name> (Copy)", with its icon and a deep copy
  of every widget under fresh ids. Container members point at their container's copy, and a stack's
  open child / a tab strip's selected tab name the copy. Data a widget only refers to (a note, an
  account) stays shared, as in the original. The copy is shown on the display asked from; it counts
  against the licence as one view plus its widgets.
- **[S-901]** Reorder views by dragging a chip on the view bar (edit mode), by dragging a view's group
  on the Views page ([S-1703]), or Move up / Move down in a view's menu (Views page, switcher). By id, never by bar index, so a bar showing a filtered subset moves the right view.
  The order is the bar's, the switchers', the Views page's and Ctrl+n's.
- **[S-902]** Deleting a view (never the last) repairs everything naming it in the same change:
  display assignments (the display then gets an existing view again, [S-1705]), the active view, the view for
  new displays (back to null) and every bar whitelist (one left empty means all views again).
- **[S-903]** A view's icon is picked in its group on the Views page from the shared glyph grid
  (`IconChoice`), whose default cell shows the view's initials — what a view without an icon shows.
  The icon is drawn by the view bar, the switcher widget, the toolbar's View menu and the group's
  header on the Views page (a view without one shows the shared view glyph there).
- **[S-904]** In edit mode only, Ctrl+1..Ctrl+9 (and the keypad digits) show the n-th view on the
  display under the pointer (the one a paste lands on, [S-74]), or this one. Outside edit mode
  Themia takes no digits: there is no global hotkey (audit B6).
- **[S-905]** The edit toolbar has a Snap toggle for `Design.GridSnap`, kept in step with Settings.
- **[S-906]** Each display has its own view bar settings (`AppState.ViewBars`, keyed like
  `MonitorViews`): anchor (default bottom left), size M/L/XL, names or icons only, hidden, and a
  whitelist of views. A display without an entry uses the defaults and nothing is stored until it
  is changed. At load, blank keys and null entries are dropped, unknown anchors/sizes reset, and
  each whitelist keeps only existing views, once each. Import carries them over.
- **[S-907]** Outside edit mode the bar is drawn unless hidden, and only when it offers more than one
  view or the user picked the views on purpose (a deliberate single-view whitelist still shows). In
  edit mode it is always drawn, with a drag handle; a hidden one is drawn faded with a dashed edge.
- **[S-908]** The whitelist filters in the user's view order; empty, or naming no view that exists,
  offers all. On the display page the last view cannot be switched off, and switching every view
  on goes back to "all", so later views appear by themselves (same rules as [S-490]).
- **[S-909]** The bar sits at its anchor inside the display's work area, 16 DIP from its edges,
  never over the taskbar. In edit mode a bar that would overlap the edit toolbar (top centre) moves
  below it; one beside it stays. A bar larger than the area is pinned to its top-left.
- **[S-910]** The left and right middle anchors stack the chips vertically; the other six lay them
  in a row.
- **[S-911]** Sizes M, L and XL are the original's (Caption/Body/Body Large type, 14/16/20 icons,
  28/34/40 icon-only squares). A chip shows the view's icon or initials and its name (bold for the
  view new displays start on); the shown view is a raised control, the rest subtle until hovered.
  The bar's fill and stroke are the widget card's (colour, opacity, border colour).
- **[S-912]** Outside edit mode the bar's box is added to the surface's hit region, cut to the work
  area; everything around it stays the desktop. It is an element on the surface, not a window, so it
  is never above other apps.
- **[S-913]** In edit mode the bar is dragged by its handle; while dragging it snaps live to the
  anchor under the pointer (thirds of the work area; the middle band of the side thirds are the
  vertical anchors; the dead centre goes to top or bottom middle by half), and the drop saves that
  display's anchor. A 4-DIP threshold separates a click from a drag.
- **[S-914]** In edit mode a chip dragged along the bar slides into the slot whose centre is nearest
  the pointer (also past the ends); release commits the move ([S-901]); a lost capture undoes it.
- **[S-915]** In edit mode a chip's right-click menu offers Duplicate view and View settings; outside
  edit mode it has none. A click always shows the chip's view on this display.
- **[S-916]** A display's group on the Displays page ends with its view bar rows: Show the view bar,
  View bar position (8 anchors), View bar size, Show view names, and "Views on the bar" (one check
  box per view). Its Shows drop-down assigns through `ShowViewOn`.

Not ported: the original's chip "drag-over" highlight (the chip itself moves instead); the free-tier view-limit
tooltip on the bar (licensing refuses through `AllowCreate`). The help guide now describes the bar
([S-1532]). Debug render:
`Themia.exe --viewbar-png <outDir>` draws the real bar, placed by the real rule, at nine
anchor/size/mode combinations on a stand-in display with a taskbar strip.

## The Displays and Views pages (R4-NAV, R5-PAGES)

Every display's and every view's settings, each on one page, and how a display is given a view.
Owning files: `Shell/Config/ConfigPages.Displays.cs`, `ConfigPages.Views.cs`,
`ConfigWindow.Navigation.cs`, `ConfigTarget.cs`, `AppHost.Displays.cs` (`SettingsSurface`,
`EditView`); rules in Core `DisplayPlan.AssignViews`, `ViewOps.Rename`, `SettingsExpansion`,
`SettingsPageSync.Shape` (tests `DisplayPlanTests`, `ViewPerDisplayTests`, `ViewsTests`,
`SettingsListPagesTests`).

- **[S-1700]** A view's group on the Views page: Name, Icon, the view's widgets (each a card drilling
  into its page), Add a widget (Edit layout, [S-1701]), then Duplicate this view and Delete this view
  (its button in the critical colour; off for the last view). No "Active view" row (which display
  shows a view is chosen in the display's group or from the group's menu) and no order row.
- **[S-1701]** Edit layout from Settings (a view's Add a widget row) first shows that view on the
  settings display ([S-1702]), then enters edit mode. Edit mode alone left the user arranging
  whatever view that screen happened to show.
- **[S-1702]** "This display", for a command given in Settings (Show on this display, Edit layout,
  Duplicate), is the display holding the centre of the settings window; when the window is hidden,
  minimised or off every display, the one under the pointer; else the first surface.
- **[S-1703]** A view's group can be dragged (a mouse press on its header travelling 4 DIPs, not on
  the "…" button or inside its rows) onto another view's group: it takes that view's place in the
  order through `AppHost.ReorderView` (the view bar's own path, one undo step), so the page, the bar,
  the switchers and Ctrl+n follow. Only a drag that began on the page is accepted; the move runs
  after the drop has finished, since it rebuilds the page.
- **[S-1704]** A view's group has a "…" button on its header (and the same menu on a right-click),
  through the shared `ItemMenu`: Show on this display (off when it already shows there); Move up,
  Move down (off at the ends); Duplicate view (shown on this display, [S-900]), Delete view (danger,
  off for the last view; the undo notice is the way back). A display's group has no menu; its rows
  hold all it offers. The navigation pane's entries have no menus.
- **[S-1705]** A display is never given a new view. The display pass (and import, delete and undo,
  which re-run it) gives every display without a valid assignment an **existing** view: the one
  chosen for new displays ([S-847]), else the active view. It is stored, so switching another display
  later does not drag this one along. A display that has a view keeps it. Views are only ever made by
  the user (Add view, New view, Duplicate). Was: each new display got an empty "Display n" view of its own.
- **[S-1706]** A display's "Shows" drop-down lists the existing views and nothing else: no "new
  view", no "follow the active view" (every display holds an assignment, so that choice lasted only
  until the next display pass). It shows the display's assignment, or the active view before it has one.
- **[S-1707]** Renaming a view (its Name box, committed when it loses the focus) goes through
  `ViewOps.Rename`: the name is trimmed; blank, unchanged or an unknown view changes nothing. The
  group's header follows in place.
- **[S-1830]** The navigation pane lists pages only: General, Appearance, Accounts, then **Displays**
  and **Views** (in that order, with Windows Settings' screen and content-view colour icons,
  [S-1600]), and License and About at the foot. No entry per view, display or widget, so no entry
  menus and no dragging in the pane.
- **[S-1831]** A page target is a page plus, on Displays or Views, the item whose group to open
  (`ConfigTarget.App(Page, Item)`; `ConfigTarget.ForView`). Everything that used to open one view's
  page (a view bar chip's View settings, the view switcher's menu, a widget page's Delete) opens
  the Views page with that group opened and scrolled to the top; the reader's other groups stay as
  they were. While a widget's page is open the Views entry stays selected, and the title is the
  breadcrumb "Views › Desktop › Clock": Views opens the page, the view's crumb opens it on that
  view's group. Clicking an entry (not a selection change) navigates, so Views opens its page even
  while it is already selected under a widget's page; clicking the entry of the page on screen
  leaves it as it is.
- **[S-1832]** The Displays page has one `FluentLayout.Expander` group per connected display ([S-853];
  "· Themia is off here" after the resolution of one switched off); the Views page has Add view at
  the top (a new, empty view at the end of the order, shown nowhere yet; within the licence,
  [S-1065]; its group opens), then one group per view in the user's order, headed by its icon and
  name with its widget count. Search offers a row inside a group as "Views › Desktop" (or the
  display's name) and lands in that group, opening it; the same row in several groups is one
  suggestion per group.
- **[S-1833]** Which groups are open is kept by the window while it is open (`SettingsExpansion`), so
  rebuilding a page (to catch up with a change made elsewhere, [S-1720]; after a view was added,
  moved or deleted; on coming back from a widget's page) opens the same groups. A group nobody
  opened or closed is closed, unless it is the only one on its page. A gone item is forgotten.
- **[S-1834]** A list page whose shape changed (`SettingsPageSync.Shape`: the views in order; the
  connected displays and which are on) is rebuilt at once, keeping the scroll position, even while
  the window is active: the change came from the page itself (Add view, Duplicate, Delete, a move,
  switching a display off) and it has to show it. A rename or a new icon keeps the shape, so the box
  being typed in is never torn down; the group's header follows in place.
- **[S-1835]** Debug render: `--settings-png Displays|Views <out.png> [--open first|name]` draws the
  page; `--open` opens one group (the first, or the view or display of that name) as a link does.

Needs a manual pass: dragging a view's group onto another on the Views page; each "…" menu action
(Show on this display, Move up/down, Duplicate, Delete) and the right-click menu; View settings from
a view bar chip and the view switcher landing on the opened group; Edit layout from a group; the
breadcrumb's crumbs and Back from a widget's page.

## Apps, Search, Help and Themia: parity and the whole tile (PI-A)

Owning files: `Widgets/AppsView.cs` (+ `.Cells.cs`), `Widgets/WebSearchView.cs`, `Widgets/HelpView.cs`,
`Widgets/ThemiaView.cs`, `Widgets/AppActions.cs`, `Shell/Config/WidgetSettings.Apps.cs`,
Core `Model/Configs/AppsConfig.cs`,
`Widgets/{InstalledAppRules,AppsLayout,SectionCursor,RecentSearches,WebSearchLayout,HelpLayout,ThemiaLayout,HelpGuide}.cs`.
Tests: `AppWidgetsTests` (S1520..S1533), `HelpGuideTests`, `LauncherPersistenceTests`.

- **[S-1520]** Apps draws as **tiles** (default; icon over name), **rows** (icon, name) or **compact
  rows** (name only, no grouping) — original S-04/S-10. Tiles and rows come from the Shortcuts
  planner (`ShortcutLayout.Grid/Rows`, [S-1382]/[S-1383]) at Medium, so a wide tile shows more
  columns, L/XL step the tiles up once and the widest list runs in two columns (a grouped list from
  an Expanded width, its rows the plain 40 DIPs since the headings carry the folder).
- **[S-1521]** Grouped by Start-menu folder (`GroupByFolder`, default on): named folders A–Z, then
  the top-level apps as "Other"; headings only with more than one section, from a Regular height,
  never in compact rows (original S-01/S-05). A large ungrouped list says the folder as a caption
  line; compact rows (and ungrouped one-column lists without one) show it as a right column from a
  320-DIP slot.
- **[S-1522]** Every app shows the icon Explorer shows for its shortcut (`AppIcons.GetAsync`, at
  twice the drawn DIPs, extracted one at a time and cached app-wide; never for a share). Until it
  arrives, or when there is none, a tile of its initials (first letter of up to two words) on a
  dark colour hashed from the name exactly as the original did (`h*31+c`, `|h| % 360`), so an app
  keeps its colour (original S-06/S-07/S-17).
- **[S-1523]** `HiddenApps` (shortcut paths) and `HiddenCategories` (folder names; empty is the top
  level) are left out **before** the filter and the folder chips, so a search never brings a hidden
  app back (original S-22/S-23). The item menu's "Hide from list" / "Hide the folder …" add to them
  and save (S-24). Load repairs null, blank and doubled entries and unknown enums, loudly.
- **[S-1524]** Settings: view, group by folder, filter box, count, open with (double/single, the
  shared `ItemClickMode`); **Folders** — one switch per folder found by a Start-menu scan made off the
  UI thread when the page opens (plus hidden folders the scan no longer finds); **Hidden apps** —
  each with "Show again" (original S-25).
- **[S-1525]** Opening is a double click by default (desktop-layer safety, as Shortcuts and
  OneDrive); single click is the option. Every open goes through `ShellLauncher`; preview cards (a
  debug render) never open anything.
- **[S-1526]** Keys ([S-800] lease): a click puts the cursor on an app; Left/Right step one,
  Up/Down one visual row keeping the column (a section's short last row lands on its last app),
  PgUp/PgDn a screenful, Home/End the ends (`SectionCursor`); Enter opens, the Menu key opens the
  app's menu, Escape (and every lease end) drops the cursor. Enter in the filter box opens the
  cursor's app or the first match. A widget with nothing listed does not take the keyboard.
- **[S-1527]** Ladder (`AppsLayout`): apps always (XS: a row of icon tiles); filter box from a
  Regular height (M, Tall, L, XL; never a strip); "N apps · N hidden" line at L/XL; at XL a row of
  folder chips (All + each folder) narrows the list to one folder (not saved). The filter only
  narrows while its box is on screen.
- **[S-1528]** Item menu (right-click, or the Menu key): Open, Open file location, Copy path, Hide
  from list, Hide the folder (original S-15/S-24). Card menu: Refresh (reads the Start menu now,
  S-13) and open on single / double click; the view and grouping are settings only ([S-1746]).
- **[S-1529]** Search ladder (`WebSearchLayout`): the box always (one ramp step larger at XL);
  engine chips (Google … Ecosia, Custom only when its template is usable) from an Expanded width —
  a click switches this widget's engine and saves it; the "Enter to search with …" hint when a line
  is left; recent searches as whole rows of the rest (5; 8 from an Expanded height; 10 at XL, titled
  "Recent searches" from Expanded, in 40-DIP rows at XL). With nothing
  under it the box is centred. Default size 320×56, header hidden ([S-485] list), as the original.
- **[S-1530]** Recent searches are this session's only, shared by every Search widget, never
  persisted (`RecentSearches`: trimmed, blank ignored, a repeat moves to the front, at most 10). A
  click puts one back in the box, a double-click runs it again, its menu has Search again / Copy /
  Remove; the card menu has "Clear recent searches". Up/Down in the box walk them like a shell
  history. With none yet, a tile with room says where they will appear.
- **[S-1531]** Help ladder (`HelpLayout`): page only below a Regular width; icon tabs (`SelectorBar`)
  from Regular; from Expanded a labelled page list beside the page (Settings' shape, accent pill on
  the open page; 28-DIP rows on a short strip), which keeps lines readable on wide tiles. L adds
  "Edit widgets" and "Settings" under the list (`AppServices.EditLayout/ShowSettings`); XL adds a
  card of the keys (`HelpGuide.Keys`). Prose is Body where the page keeps 240 DIPs, else Caption.
- **[S-1532]** The guide describes the app as it is: edit mode (Ctrl+Alt+W, double-click, "Edit
  layout", starting Themia again), the keyboard a clicked widget borrows and Esc giving it back, the
  picker's search, new/pasted/duplicated/moved widgets following the pointer until a click,
  Ctrl+click / rubber band / Ctrl+A selection, bring to front / send to back, containers (drop to
  join, Move into, Add widget inside, delete keeps members), the view bar (per display, drag,
  reorder, chip menu in edit mode, display-page settings), Ctrl+1–9, view icons, and Find a setting.
- **[S-1533]** Themia ladder (`ThemiaLayout`): the mark keeps a 56-DIP floor; then name, version;
  from an Expanded height (Tall, L, XL) a tagline and "Made by Lometics"; at L/XL "Edit widgets" /
  "Settings"; at XL links to release notes,
  feedback and the source (all through `ShellLauncher`, nothing fetched). The mark takes what is
  left, up to 160 DIPs. A wide strip and the XL tile put the mark beside the words (up to 240 at
  XL, with the name a step larger), so XL is a mark and a column, not a stack with empty sides.
- **[S-1534]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported: the original Apps' max-6-parallel icon queue (the shell answers E_PENDING to parallel
first requests, so `AppIcons` extracts one at a time) and the search bar's own ✕ button (the stock
box has one). Not live-verified: launching from a tile/row, the keyboard lease on Apps, Enter in the
filter, the Help/Themia buttons entering edit mode and opening Settings, the links opening the browser.

## Notices, resilience and undo (WP4)

- **[S-880]** One notice API (`Services/Notices`: `Show`, `Info`, `Warning`, `Error`) for every
  transient message; callable from any thread, always logged at the matching level. The host draws
  each notice on the display under the pointer (else the first surface). Before the host starts and
  in a debug render a notice is only logged. The licence refusal ([S-1065]) and the audio "default not
  changed" ([S-1616]) are notices now (the flyout and the jump to Settings are gone).
- **[S-880a]** Host (`DesktopWindow.Notices`): a layer on the surface, mounted only while a notice
  shows and removed when the last goes; bottom-right of the work area, 16 DIP in from its edges,
  fades in where it lands (`EntranceThemeTransition` without offset, as the region clips painting;
  the others reposition). The surface stays
  non-activating; the notices' box joins the hit region ([S-872]) so they paint and take clicks
  outside edit mode too.
- **[S-881]** A notice (`Core/Shell/Notice`) has a kind (Info, Success, Warning, Error), a title, an
  optional message, an optional single action, a key and a sticky flag. Drawn by `Shell/NoticeCard`
  as Windows 11 draws a notification: severity glyph in the system severity colour, bold title,
  secondary message (4 lines max), the action as a button, a close button; 364 DIP wide (never
  wider than the work area), opaque flyout fill and stroke. Its buttons never take focus.
- **[S-882]** `NoticeStack`: at most 3 at once, newest at the bottom; a fourth pushes out the oldest
  non-sticky one; a notice with the key of one showing replaces it and becomes the newest ("undo"
  notices replace each other, "license" refusals too).
- **[S-883]** Lifetime: Info/Success 4 s, Warning 6 s, Error 8 s, +3 s with an action; sticky means
  until closed or acted on. The timer stops while the pointer is over the notice and restarts in
  full when it leaves. Acting on a notice or closing it takes it down.
- **[S-884]** Undo history (`Core/Model/UndoLog<T>`, `LayoutHistory`): bounded at 100 steps, newest
  first; recording forgets the redo side; undo moves the current state to the redo side. `Mutate`,
  the single write path, commits every change to it with an `UndoAction`; the end of a drag (`Move`,
  a whole multi-selection as one step) and of a resize (`Resize`) commit through `CommitGeometry`.
  A commit records a step only when the arrangement changed, so design, widget settings and view
  switches record nothing. Steps: Change (settings-page header/surface/title, view name/icon), Add,
  Delete, Move (drag, cut-paste, Move, hand-off to another display), Resize, Paste (copies,
  Duplicate), Arrange (front/back), Container (reparent, drop into/out of, child order, take out),
  AddView, DeleteView, DuplicateView, ReorderView.
- **[S-885]** Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, in edit mode only. Ctrl+Z during a
  placement cancels the placement. Each step shows a keyed "Undid …"/"Redid …" notice offering the
  opposite; with nothing to do, "Nothing to undo"/"Nothing to redo". Deleting widgets or a view
  shows "Deleted …" with Undo (Delete still asks nothing first). A notice's Undo/Redo acts only while
  the history's `Version` is the one it was offered at; otherwise it says the desktop changed and
  points to Ctrl+Z.
- **[S-886]** A step restores the arrangement (`LayoutShape`): views (id, name, icon, order) and every
  widget's kind, geometry, z-order, container, header, surface and title. Not content: a widget that
  survives keeps its current config (text typed after a move is not taken back) and its instance
  (its card stays). A widget brought back gets the config it had (held by reference, never copied).
  A view brought back returns to the displays that showed it; every reference to a view that goes is
  repaired as a delete repairs it ([S-902]); a display left without a view is given an existing one
  ([S-1705]; not a step).
- **[S-887]** Drift (a layout change made without a commit) is folded into the next step, or recorded
  as its own Change before an undo; never lost under a restore. The view the system assigns to a new
  display ([S-1705]) is adopted without becoming a step. Import and reset clear the history.
- **[S-888]** Not undone: widget content and settings-page config, appearance, preferences, display
  switches and scales, view-bar settings, which view a display shows, and a view bar's visible-view
  list after a view delete is undone (the view returns hidden from bars that listed it).
- **[S-890]** Crash-loop guard (`Core/Storage/BootGuard`, `boot_attempts` beside the state file):
  bumped atomically before the surfaces are built, deleted once every surface has loaded and laid out
  its cards. The 3rd start in a row that never got that far is safe: every card shows "Paused:
  Themia started in safe mode." with Start, and a sticky notice offers "Start widgets" (all of them;
  the start then counts as good) or a reset in Settings › About. Fails open: an unreadable counter
  is a normal start; a corrupt one starts over (logged).
- **[S-891]** Widget error boundary (`WidgetCard.Fault`): a view that throws while created, bound,
  attached, given new settings, or on a tick is detached and replaced by an error card. The ticker
  routes the faults of subscriptions made inside a card's `CatchFaults` scope (Attach, ApplyConfig)
  to that card; a fault on the first tick during Attach counts as an attach failure, a later one
  swaps on the next UI-queue pass. The card keeps its header (the view's trailing part goes) and
  shows "This widget stopped working.", Retry, and Details (a flyout with the exception type and
  message). Logged with the widget id and kind. Retry builds the view again in place. Below 160 DIP
  high the glyph goes; below 150×110 DIP the Details link goes too and the line shortens to
  "Stopped working." (Retry always stays).
- **[S-892]** Settings › About › Reset all settings (confirm): the current state is written to
  `state.before-reset-<stamp>.json` beside the state file (if that fails nothing changes and an error
  notice says why), then views, widgets, appearance and preferences become a fresh profile's (Start
  with Windows is kept, as on import); the undo history is cleared; a Success notice. Accounts,
  notes, tasks and habits are separate files and are kept.
- **[S-893]** First-run desktop (`AppState.CreateDefault`, also what a reset and a corrupt-state
  recovery show), as the original's: Clock (0.78, 0.04, 0.20×0.16) first, Help (0.02, 0.04,
  0.24×0.34), Folder on the user's Desktop folder (0.02, 0.42, 0.24×0.50), Shortcuts (0.80, 0.80,
  0.18×0.16) with Recycle Bin, This PC, Downloads, Documents, Pictures and Home (shell targets,
  labels empty so the names are the translated target names).
- Not ported: the original's pending-operation framework (an operation that resolves later; nothing
  here records one yet, and file operations will add their own step when they need it), and
  per-window boot counters (one process here, so one counter).

## Widget polish 2: leftover parity and layout gaps (POLISH-2)

Items earlier packages left "not done", each checked against the original on `main` first: what
the original had is ported, what it never had is recorded here and not built.

- **[S-1620]** Pictures (Gallery widget): a slide can be held for as little as **1 s** (`GalleryConfig.MinSlideSeconds`;
  the settings slider starts there), as the original's S-09/S-18. Stored values are clamped to
  1..3600 s.
- **[S-1621]** Habits are **reordered by dragging a row** in the widget (original S-13), beside the
  row menu's Move up / Move down. A press arms nothing until the pointer has moved 5 px
  (`HabitDrag.Threshold`), so a click still focuses and a double-click still renames; a press on the
  tick box or in the rename box never arms. The lifted row follows the pointer (clamped to the list),
  the rows it passes step one pitch aside (`HabitDrag.Shift`), the landing slot is the one the row is
  more than half over (`HabitDrag.Target`), and the list scrolls near its edges. Release commits one
  `MoveHabit` through the pool (a drag that comes home writes nothing); capture lost, a rebuild under
  the drag or Detach puts the rows back. Rows are only translated while captured, never reparented.
  In edit mode the card takes the press and the widget moves instead.
- **[S-1622]** `Disclosure.Fits` / `Disclosure.TakeFirst`: a rung offered in several sizes takes the
  richest that fits, and only a total miss closes the ladder.
- **[S-1623]** Power at XL fills its side panel: on a tile Huge in height the panel rows are the
  standard 40-px list height, the facts end with **"Up for 6d 11h"** (the CPU meter's uptime form),
  and after the power plans a **"Screen and sleep"** section lists the active plan's
  "Screen off after", "Sleep after" and "Hibernate after" for the power source in use
  (`PowerStatus.Timeouts`: `PowerReadACValueIndex`/`PowerReadDCValueIndex` on the active scheme;
  `PowerTimeoutRules.Duration`: "Never", "45 s", "5 min", "1 h 30 min"). An unreadable value has no
  row (never shown as "Never"). Read with the plan list, only while the panel shows. Read-only.
- **[S-1624]** Mixer at XL: when every app row fits, the rows share the list's height
  (`MixerFill.RowHeight`, at most 64 px) instead of stopping a third of the way down, and a row of
  56 px or more has a caption under the name (`MixerFill.Detail`: "Playing · 3 sessions", "Silent",
  "Muted", "Windows sounds"; the session count stays the pill). A list that does not all fit keeps
  the base height and scrolls. Where a short list still leaves 128 px or more (XL beside the panel,
  L, Tall), a centred hint fills it: "Other apps appear here when they play sound" and a link to
  Windows' volume mixer (`ms-settings:apps-volume`, through `ShellLauncher.OpenWindowsSettings`;
  inert in previews).
- **[S-1625]** Checked against the original and **not built, because the original never had it**:
  Sun & moon 7-day table and moonrise/moonset (original: sunrise/sunset + phase only); Scratchpad
  export as PNG (the original only persisted its canvas as a PNG data URL); Clock world-clock rows
  (one zone per clock); Video stream save-frame button and fps for MP4/HLS (the original was an
  `<img>` over MJPEG/snapshot URLs, no such controls); Colour swatch hex caption (the original drew
  the colour only); Tasks Overdue/Today group headings (the original's Today list was one list,
  oldest first); GitHub "8 of 23" header total (the original showed the shown count, as here);
  Clipboard pin (the original had no pin).
- **[S-1626]** Item menus: the original's menu kept itself open for `keepOpen` items (Countdown's
  format choices, C-61; Folder's and Shortcuts' item-size choices), so formats could be tried in a row.
  WinUI's `MenuFlyout` closes on every pick; this stays a known difference (the choice is still one
  right-click away and the checked item shows the current one).

## Folder: the file browser (FOLDER)

Original: `widgets/office/folder` on `main` (SPEC S-01..S-67). View: `Widgets/FolderView{,.Render,.Input,.Commands}.cs`;
file interop services (reusable by Shortcuts and the shell): `Services/Files/{DragDropInterop,FileClipboard,ShellFileOps,
ShellContextMenu,ShellNewTemplates,FolderWatch,FileIcons,GitStatus}.cs`; rules in Core `Widgets/Folder{Columns,Filter,
Selection,Gesture,Layout,History,Badges}.cs` and `Files/{FileDrops,DropFiles,FileDescriptors,FileNames}.cs`; tests
`FolderBrowserTests` (+ `FolderRulesTests`, `FolderFormattingTests`, `FolderPathTests`).

- **[S-1320]** Settings (orig. S-14..S-24, S-58): `Path` (empty = profile), `View` List/Details/Grid/Icons, `ItemSize`
  ExtraSmall/Small/Medium/Large (rows 24/28/32/40, icons 12/14/16/20), `TileSize` Small/Medium/Large (80x90/110x120/150x160,
  icons 32/48/64), `StretchTiles`, `Columns` (ordered; default Size, Modified, Type; `Repair` drops unknown/repeats loudly),
  `Navigation` Explorer/Navigate, `ClickMode` (`ItemClickMode`, default Double), `Filter`, `Sort`/`Descending`/`FoldersFirst`,
  `ShowHidden`, `ShowExtensions` (default off). `ShowDetail`/`MaxCount` are gone (no compatibility).
- **[S-1321]** Comma-separated filter and the header search (orig. S-21, S-50): OR of keywords, case-insensitive substring;
  both must pass. Names hide the extension unless `ShowExtensions` (folders and dot-files keep theirs). Sort, filter and
  search re-draw what is in hand; only the path re-reads the disk.
- **[S-1322]** Details columns: Size, Modified, Type (shell type name, `SHGetFileInfo`, cached per extension), Cloud, Git,
  Items (folder entry count, counted off-thread up to 1000, shown "999+"). Shown in the user's order while each fits beside
  a 120 DIP name; the first that does not fit ends the row. Header click sorts Name/Size/Modified (again = reverse, saved);
  the columns are chosen in the settings only ([S-1746]; the header has no menu of its own).
- **[S-1323]** Ladder (`FolderLayout.Plan`): one row / one tile row always; an open search box; the details column header;
  list view adds unlabelled Modified (W >= Regular), Size (>= Expanded), Type (>= Huge); at Expanded x Expanded a breadcrumb
  (tail first, clickable) and an Explorer-style footer ("24 items · 2 selected · 3.1 MB"); Huge width: grid tiles one size
  up. Grid columns = floor(width / tile), stretched to fill; Icons view packs tiles to icon + 24. Stretch shares the height
  only when every tile fits. Over 1000 items: the first 1000 and a "N more in Explorer…" row.
- **[S-1324]** Real shell icons (`IShellItemImageFactory`, shared per extension except exe/lnk/ico/url…, customised folders
  per path) and image thumbnails in Grid/Icons (`SIIGBF_THUMBNAILONLY`; never for cloud placeholders). Caches bounded and
  shared; extraction serialised (orig. S-64). Cloud badge from attributes (cloud-only / always kept / available) over the
  icon and in the Cloud column; "Always keep on this device" / "Free up space" set the pin bits.
- **[S-1325]** Git: only when a `.git` exists up the tree; `git --no-optional-locks status --porcelain=v1 --branch -z`
  off-thread, single-flight per repository; no git = no badge. Header badge "main ~2 ?3" (List/Details, by width) and the
  Git column (file code; "•" for a folder with changes beneath).
- **[S-1326]** Header (trailing, shed by width): Up, Back, Search, Sort (not in Details), Forward, git badge, Home.
  Navigate mode keeps a 50-deep history restoring each place's scroll offset; Backspace and mouse X1/X2 go back/forward;
  a `.lnk` to a folder opens inside the widget. Explorer mode opens folders in Explorer.
- **[S-1327]** Selection as Explorer: click selects, Ctrl toggles, Shift ranges from the anchor (Ctrl+Shift adds), a press
  on a selected item narrows on release (a multi-item drag keeps it), background click clears, right-click on an unselected
  item selects it. Keys via the lease (`IWidgetKeys`): arrows/Home/End/PgUp/PgDn (grid-aware, Shift extends), Enter opens,
  Space selects (Ctrl+Space toggles), Delete, F2, Menu, Ctrl+C/X/V/A (new `WidgetKey.Copy/Cut/Paste/SelectAll`, Ctrl
  alone; other chords still never reach widgets), Backspace (`WidgetKey.Back`); Escape (and lease end) clears selection and
  cursor. Double-click mode opens on double-click; Single opens on a plain click (Ctrl/Shift only select).
- **[S-1328]** Press -> click | rubber band | drag is the pure `FolderGesture` (orig. `gesture.ts`, S-65/#58): 5 DIP dead
  zone, every exit resets, each press wipes prior state, a generation token cancels a deferred drag. Background drags draw
  a band selecting every overlapped item (Ctrl keeps the earlier selection).
- **[S-1329]** The file clipboard is the real Windows one: the shell's data object (CF_HDROP, shell ID lists) plus
  `Preferred DropEffect` (Move for cut), `OleSetClipboard` + `OleFlushClipboard`. Paste reads CF_HDROP and the effect from any
  source (Explorer, other widgets); a cut-paste moves, then empties the clipboard. Cut items are dimmed while the clipboard
  sequence number is still the cut's.
- **[S-1330]** Shell operations through `IFileOperation` on one STA worker (undoable in Explorer; the shell's progress and
  conflict UI): copy, move, rename, and delete = **Recycle Bin only** (`FOFX_RECYCLEONDELETE|FOF_ALLOWUNDO|FOF_WANTNUKEWARNING`)
  after a confirmation flyout at the widget. Item menu: Open, Open with…, Show in Explorer, Cut, Copy, Paste (into a
  right-clicked folder), Copy path (quoted), Rename, Delete, cloud pin, Properties, Show more options (the shell's own
  `IContextMenu`; the owner window is subclassed for the menu's life so Send to / Open with fill in).
- **[S-1331]** Background menu: Paste, Select all, Copy path, New > Folder + ShellNew templates (NullFile/FileName/Data;
  Command entries skipped), Up, Open in Explorer, Properties, Refresh, Show more options. New items get the next free
  "New folder (2)" name and start an in-place rename; rename edits the stem when extensions are hidden, validates Windows
  name rules and keeps the item selected.
- **[S-1332]** Drag out: an OLE drag of the shell data object with our `IDropSource` (`SHDoDragDrop`), posted after the
  pointer handler. Drop in: through the surface's OLE target ([S-1020]..[S-1024], `IWidgetDropTarget`): plain drop moves,
  Ctrl copies, into the folder row under the pointer (accent ring) or the folder on screen, carried out by that folder's
  shell drop target; a drag whose every item is already there or is the destination's ancestor is refused
  (`FileDrops.Pastable`, orig. `dropTarget.ts`). Virtual files (`FileGroupDescriptorW`, SDK layout 592 bytes / name at 72)
  are saved on paste, and on drop by the shell's target.
- **[S-1333]** `FolderWatch`: a `FileSystemWatcher` plus every in-app file operation's notice; events coalesce into one posted
  reload (no timers); a reload in flight is marked dirty and followed by exactly one more. No watcher (some shares): 5 s
  timer, else the 30 s backstop. Disposed exactly once (orig. S-63).
- **[S-1334]** Every rowless state says which it is (status messages; "Nothing here matches." under a filter).
- **[S-1335]** Card menu: Open in Explorer, Refresh. The views, row or tile sizes, hidden items and file extensions
  are settings only ([S-1746]).
- **[S-1336]** Tests use made-up paths and byte buffers only; no test touches a real file.
- **[S-1339]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported: header double-click opens Explorer (orig. S-07; the card owns the header, so "Open in Explorer" is in the card
menu); image-item inline toggles (orig. S-29). Needs live verification: drag in/out with
Explorer and Outlook, Show more options submenus, clipboard interop both ways, Recycle Bin and the nuke warning on a share,
OneDrive pin states, lease delivery of Ctrl chords, multi-monitor.

## View switcher: item menu, keys and the whole tile (PI-B)

Owning files: `Widgets/ViewSwitcherView.cs`, Core `Widgets/ViewSwitcherLayout.cs` (+ `ViewSwitcherRules`),
`Shell/SurfaceDisplay.cs`; `IWidgetDisplay` (in
`WidgetCatalog.cs`) gained the per-view commands. Tests `ViewSwitcherLayoutTests`, `ViewSwitcherRulesTests`.

- **[S-1650]** Each view button has its own right-click menu (an item menu, so the card's own menu
  still opens on the card): Show on this display (off for the shown view), Move earlier / Move later
  (off at the ends; [S-901]), Duplicate view (the copy is shown on this display; [S-900]), View
  settings (name and icon; [S-903]), Delete view (danger, off when only one view is left; no
  question, the undo notice is the way back, [S-881]/[S-902]). Every command goes through the
  `AppHost` view mutations via `IWidgetDisplay`; switching is always `AppHost.ShowView` →
  `AppState.ShowViewOn` for the widget's own display ([S-19], [S-487]).
- **[S-1651]** A view's icon ([S-903]) stands where its initials would on every face, at the face's
  size, and in the "+N" menu.
- **[S-1652]** Keys (`IWidgetKeys`, the WP11 lease): a click takes the keyboard. The first arrow lands
  on the shown view's cell; Left/Right step in reading order, Up/Down a row in a grid (one cell in a
  single row or column, into the shorter last row's last cell), all clamped; Home/End jump; Enter or
  Space shows the view under the cursor (or opens the "+N" menu); the Menu key opens its menu;
  Escape, or the lease ending, drops the cursor. The cursor is the button's own keyboard focus and
  follows its view across rebuilds. Rules in `ViewSwitcherLayout.Step`.
- **[S-1653]** The ladder (`ViewSwitcherLayout.Plan`, from the view's `WidgetArea` via
  `IWidgetSizeAware`): faces Mark (icon/initials, cell ≥ 32×32) → Named (≥ 88 wide) → Detailed (name
  over "N widgets", ≥ 120 × two lines + padding) → Card (sketch + name + count; sketch over the text
  in cells ≥ 140 × (Detailed + 64-DIP sketch), beside it in cells ≥ 240 × (64 + padding)). Every grid
  from one row to one column is tried; the richest face every row can carry wins, then the cells
  closest to that face's shape (log scale: 1:1 marks, 3:1 buttons, 1.3:1 or 3:1 cards). The last row
  holds what is left and its cells widen, so no cell is left empty. "Initials only" caps the face at
  Mark. With three views: XS/S a named column, M a detailed column, Wide a row of cards, Tall a column
  of cards, L/XL two cards over one wide one.
- **[S-1654]** The shown view is the raised button with its mark in the accent; Detailed and Card
  faces add the Windows 11 selection pill on the left edge, and a card's sketch draws its widgets in
  the accent.
- **[S-1655]** When not even 32-DIP cells fit every view, the cells that fit (`Disclosure.WholeRows`)
  hold the first views in order and the last is "+N": a menu of the rest, the shown one ticked. A box
  smaller than one cell still has the "+N" cell.
- **[S-1656]** A card's sketch is the display (its whole-monitor shape, [S-840]; 16:9 when unknown) as
  large as fits, with each top-level widget's stored rect inside it, cut to the screen; off-screen,
  empty and non-finite rects are left out. Drawn from state only (`IWidgetDisplay.Tiles`), never by
  rendering the view; a rebuild happens only when a sketch's rects or the display shape change.
- **[S-1657]** The count line reads "No widgets", "1 widget", "N widgets" (every widget in the view,
  container members included, as the licence counts them).
- **[S-1658]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

## Motion: intros, hyperspace, hacker (WP9-MOTION)

Ported from the original's `src/SPEC.md` [S-15], [S-16], [S-23], [S-24], [S-26], [S-27], [S-28]
(`introSheets.ts`, `hyperspace.ts`, `hacker.ts`, `WidgetGrid.css`). Rules in
`Themia.Core/Motion/*`, tests in `MotionTests` and `ShellSettingsTests`; the surface side is
`Shell/DesktopWindow.Motion.cs` and `Shell/Motion/*`.

- **[S-1000]** Three moments, each with its own style and length in `DesignState`: **Startup**
  (`IntroAnimation`/`IntroAnimationMs`), **View change** (`TransitionAnimation`/`…Ms`) and **Unlock**
  (`UnlockAnimation`/`…Ms`); default Scale / 500 ms each. One resolver (`IntroMotion.StyleFor`/`MsFor`)
  maps a moment to its pair. Styles: None, Fade, Scale, Slide, Sides, Top and bottom, Flip, Blur,
  Hyperspace, Hacker. Lengths are 100..5000 ms (the original's stepper); load clamps out-of-range
  values into it and a non-finite one to 500. A length is the **whole** show's ([S-1760]).
- **[S-1760]** **The length is the animation's whole wall time** (user requirement: it used to run far
  longer): the overlay, the wait for its jump, the cascade across every widget and the last widget's
  own animation all fit inside it, and the last widget comes to rest exactly on it
  (`IntroSchedule.For`, tested for every style, moment, length and 1..200 widgets). The cascade keeps
  its 45 ms step and 540 ms cap while there is room, but takes at most 40 % of the widgets' time; each
  widget animates for what the cascade leaves. Hyperspace's overlay timeline (static, accel, cruise,
  jump, decel, fade; 4.5 s natural) is played faster or slower as one piece to the length, so the
  widgets wait for the scaled jump, the shake and the veil follow the same clock, and the overlay's
  fade ends with the show. Before, each widget took the whole length after its cascade (20 widgets
  at 500 ms took 1040 ms) and hyperspace added 4.1 s to whatever was set.
- **[S-1001]** Intros are **armed one-shots** per display (`IntroArming`): a surface's first layout
  that the user can see (loaded, usable size) plays Startup; a layout of a different view than the last
  plays View change. Every other rebuild is silent: a rename, a widget placed, pasted, restored or
  synced in, a settings change, a game-mode resume. A widget added to a live desktop never animates.
- **[S-1002]** Widgets cascade in the view's widget order: widget *i* starts `min(i·45, 540)` ms after
  the first, squeezed when the length has no room for that ([S-1760]). Only grid-placed widgets animate; a stack's or tab strip's children move with their
  container, a collapsed group's members draw nothing.
- **[S-1003]** The keyframe styles are the original's keyframes as data (`IntroTracks`), with its
  curve `cubic-bezier(0.22, 1, 0.36, 1)`: Fade (opacity), Scale (opacity, 0.94→1, 16 DIP rise), Slide
  (opacity, 40 DIP rise), Flip (−32° about the top edge with a 900-DIP perspective), Blur (opacity and
  1.04→1). Every track ends on the identity. **Not ported:** Blur's 12-px blur itself — a live XAML
  element cannot be blurred without re-rendering it into an effect surface every frame; the fade and
  settle carry the style.
- **[S-1004]** Sides and Top and bottom slide the view in from two opposite edges as two **rigid
  sheets** (`IntroSheets`): a widget joins the near sheet when its centre is in the first half of the
  axis, otherwise the far one; each sheet has one offset, sized by its deepest member plus 24 DIPs, so
  the sheet arrives whole; no cascade. Opaque by 12% of the trip.
- **[S-1005]** Hyperspace at **Startup or Unlock** (never on a view change) plays a full-screen
  overlay under the widgets: a black veil and a 100-star field around a core at 68% of the height.
  The warp (`Hyperspace.Warp`): static 900 ms, a tease to 0.06 over 1220 ms, a 280-ms smoothstep snap
  to full warp, cruise to the **jump at 3300 ms**, a cubic drop over 900 ms. The veil holds black to
  the jump and lifts across the deceleration; the overlay fades out over the last 300 ms. These are
  the natural times, scaled to the length ([S-1760]). Stars are compositor sprites bound by expressions to two animated
  numbers, the warp level and its travel (closed form `Hyperspace.Distance`/`Angle`: speed grows with
  depth; past the edge a star is reborn at the core on its next ray, turned by the golden angle);
  streaks are cut at the core. While it plays the surface's hit region is the work area, so the veil is
  seen edge to edge; it shrinks back when the show ends. The overlay never takes a click itself.
- **[S-1006]** Under the overlay each widget waits for the jump, then flies in from the core at 0.02
  scale on a steep ease-in (`cubic-bezier(0.55, 0, 1, 0.45)`), hits its slot over-scaled (1.05 at 38%)
  and judders to rest. The widget layer takes the original's screen shake (`Hyperspace.Shake`, 3300 ms
  after the static hold, hardest at the jump); the stars never shake. On a view change Hyperspace is
  the fly-in alone, with no wait.
- **[S-1007]** Hacker covers each widget with its own **tile** (`HackerTile`, through
  `WidgetCard.SetMotionCover`): opaque green phosphor from the first frame, one of five panels by
  index (console log, molecule, CCTV, hex dump, radar, oscilloscope; neighbours never repeat), then a
  fade that reveals the widget. Cover 65% of the length, fade the rest, the cascade added to the
  reveal, all inside the length: the last tile is gone exactly at it ([S-1760]). Panels move by compositor loops, not a
  redrawn canvas. The palette is the style's own, deliberately not the accent.
- **[S-1010]** Nothing plays — no intro, no overlay — while Windows' **Animation effects**
  is off (`UISettings.AnimationsEnabled`, read when an intro would start), the desktop's
  `prefers-reduced-motion`. The Motion settings say so when it is off.
- **[S-1011]** Unlocking the session (`WTS_SESSION_UNLOCK` only, heard by the command window)
  replays the Unlock intro on every display, overlay included for Hyperspace. Unlock = None turns it off.
- **[S-1012]** A replay (unlock, preview) is ignored on a display whose intro is still playing; a view
  change replaces a show still running.
- **[S-1013]** Purely cosmetic: intros move the cards' composition visuals, never their layout, so the
  hit region, drag geometry and saved rects never see a half-animated box, and every widget takes clicks
  from the first frame. The show ends on its animations' completion (one scoped batch), not a timer.
- **[S-1014]** Settings › Appearance › Motion: one expander per moment (Startup, Unlock, View change)
  with the style, the length of the whole animation (100..5000 ms) and a ▶ replay. Any change replays
  that moment on the desktop. The startup sound ([S-1008], [S-1009]) is removed.
- **[S-1015]** Every intro runs on the compositor (`MotionKit`): keyframe and expression animations,
  no per-frame managed timer, no layout pass, no redraw of a widget.
- **[S-1016]** Debug: `Themia.exe --motion-png <outDir>` freezes the intros mid-flight on a stand-in
  screen, posed from the same Core data (keyframe styles at 250 ms of 500, Sides at 60 ms, Hyperspace
  at 600/2300/2900/3450/3800 ms of its natural 4.5 s, Hacker tiles through the real hook). It proves the choreography, not the
  compositor's playback.

## Desktop surface drops (WP10-SURFACE)

Original: `src-tauri/src/drag_drop/drop_target.rs` (one OLE target per window, drops off any widget
zone copied or moved to the Desktop folder), `src/nativeDragDrop.ts` (zone router), folder S-13/S-53/S-54,
shortcut S-28..S-34.

- **[S-1020]** Who takes a drop (`SurfaceDrops.Route`): over a widget whose view implements
  `IWidgetDropTarget` (Folder, Shortcuts), that view, **outside edit mode only**; over any other widget or
  the surface's own controls (toolbar, view bar, notices), nobody: the no-drop cursor, never a hand-on
  to the desktop behind the widget. Bare surface is the desktop. A drag carrying nothing droppable (no
  paths, virtual files, links or shell items) is refused everywhere. The view hit is the innermost card
  under the pointer (a stack's or tab strip's child, not the container).
- **[S-1021]** Where a drop can land follows the hit region ([S-851]). Outside edit mode the surface
  exists only on widgets, the view bar and notices, so a drag between widgets is over Explorer's own
  desktop and Explorer takes it; in edit mode the whole work area is the surface. One OLE drop target
  per surface, registered on the window **and every child window** (WinUI's content bridge and input
  site), replacing any target already there, so XAML's drag events never compete: it is the one drop
  path. Revoked before the window closes; a drag in flight is left first.
- **[S-1022]** Drops into a folder go through **that folder's own shell drop target**
  (`IShellItem::BindToHandler(BHID_SFUIObject)`), entered afresh whenever the folder under the pointer
  changes: the shell copies, moves or links with Explorer's conflict and progress UI, the source's
  optimized-move handshake (so Explorer never deletes what was moved), virtual files (Outlook
  attachments), links (`.url`), Start-menu apps, and a right-button drag's Move here / Copy here menu.
  Bare surface forwards to the Desktop folder (wherever it is redirected) with the user's own keys, so
  it does exactly what dropping on the desktop would (move on one volume, copy across) — deliberately
  not the original's always-move, so edit mode and normal mode agree. The drag (`WidgetDrag`) holds
  the data object for its life and reads it once on entry: paths, virtual-file names, links (only
  when no file came with it), shell items.
- **[S-1023]** A view can force an effect: `SurfaceDrops.KeysFor` turns Move/Copy/Link into Explorer's
  Shift / Ctrl / Ctrl+Shift for the shell target, keeping the mouse buttons and dropping Alt. Folder
  forces its own rule ([S-1332]: plain drop moves, Ctrl copies, virtual files copy) and answers None
  when every item is already there or is the destination's ancestor (`FileDrops.Pastable`).
- **[S-1024]** Feedback: the effect the taker answers is the cursor (a set shows its strongest:
  Move, Copy, Link); the shell's drag-image helper (`IDropTargetHelper`) keeps the source's thumbnail
  drawn over the surface; the caption beside it is a `DropDescription` in the shell's words — "Move
  to %1", "Copy to %1", "Create link in %1" with the place inserted (`WidgetDrag.Place`, set by the
  view; the Desktop's name on bare surface). Cleared when the drag leaves, is refused or drops, so it
  never lingers over another app. Folder rows also show the accent ring on the folder a drop enters.
- **[S-1025]** Untrusted text payloads: a Chromium app's `file:///` lines (VS Code offers CF_HDROP
  and fails to render it) become paths with percent escapes decoded as UTF-8 bytes; only absolute
  local or UNC paths, each once. Wide text stops at its NUL.
- **[S-1026]** Debug: `Themia.exe --drop-check <outDir>` exercises the plumbing off-screen with no
  input: the target on every window of a WinUI window, a shell data object read back as a drag, a
  forwarded hover, a folder switch and a drop into a scratch folder under `outDir` (the copy must
  arrive), the drag-image helper and the caption.

Not ported: the original's own copy/rename to the Desktop folder (replaced by the shell's target,
above); the original's "copy/move" effect over widgets without drops (it showed a move cursor and did
nothing; now refused). Needs live verification: real drags from Explorer, Outlook, a browser, VS Code
and the Start menu onto Folder, Shortcuts and bare edit-mode surface; the drag image and caption;
a right-button drag's menu; Escape mid-drag; multi-monitor and mixed DPI hit-testing.

## Shortcuts B: drop, reorder and app layouts (PD)

Original `widgets/others/shortcut` S-28..S-38, S-50..S-60, S-65, S-68..S-74. Core: `ShortcutDrop`,
`LayoutTree`, `LayoutPlan`, `LayoutWindows`, `Model/Configs/AppLayoutModel.cs`; App: `ShortcutsView.Drop.cs`,
`Services/AppLayoutLauncher.cs`, `Shell/LayoutEditorWindow.cs`. Tests: `ShortcutsDropLayoutTests`.

- [S-1400] Files, folders, links and web addresses dropped onto the Shortcuts widget's content become
  items at the end, by reference (never copied). The drop arrives only through the desktop surface's one
  OLE target and `IWidgetDropTarget` (no XAML `AllowDrop`, no target of the widget's own). Every target
  goes through `LaunchTargets`; refused ones and ones the widget already holds are skipped (logged).
  Files win: URLs count only when no path came with the drop (orig. S-34). The widget shows an accent
  edge and the caption "Add to Shortcuts" while it would take the drop (orig. S-32).
- [S-1401] A `.url` becomes its http(s) address named after the file; a `.url` to any other scheme
  (`steam://`) is kept as the `.url` file itself with its `IconFile=` as the item's `IconPath` — the
  launch allow list is not widened (orig. S-29/S-73). A `.lnk` becomes its target named as the link,
  unless it passes arguments or cannot be resolved, then the link itself (orig. S-30). `IconPath` must
  be a local path (repair drops anything else).
- [S-1402] Browser links: `UniformResourceLocatorW`, else the http(s) lines of the text. A Start menu app
  (shell items, no paths) is found by name among the Start menu's shortcuts and added as that `.lnk`; a
  Store-only app with no shortcut is not added (logged).
- [S-1403] App layout tree: a pane or a split (side by side / stacked, first-child share 0.05..0.95).
  Split keeps the pane as the first half plus a new empty pane; remove gives the sibling the space and
  never leaves zero panes; depth ≤ 6.
- [S-1404] A pane opens an App (Start menu `.lnk`/`.exe`), a Web address, a File or a Folder; a blank
  target clears it; switching the source in the editor clears it. "Move it if it is already open"
  (orig. reuseIfRunning, default on) applies to App and File only.
- [S-1405] A layout item has a `Layout` and an empty target; named by its label, else "App layout"; its
  detail is "No apps yet / 1 app / N apps"; its icon stacks up to 4 pane icons (layout glyph when none).
  Item menu: Open layout (off with no apps), Edit layout…, moves, Rename, Remove.
- [S-1406] Repair: a missing/odd tree, a split missing a side, NaN or out-of-range ratios, broken
  assignments, repeated ids and over-deep nesting are mended loudly; a layout's target is blanked.
- [S-1407] Opening walks the tree against the work area of the display the widget is on (orig. S-52:
  50/50 side by side = x 0..50 and 50..100); an empty layout does nothing (S-53); the same target twice
  opens once; panes in pixels are never smaller than 100 × 80. Each pane launches through
  `ShellLauncher.Open(target, out pid)` (allow-listed, never arguments — orig. per-pane args not ported).
- [S-1408] A window is restored, moved without activation or z-order change, widened by its invisible
  DWM border, and put back (≤ 3 times) when it drifts more than 8 px / 24 px right after.
- [S-1409] Window matching is event-driven (WinEvent show / rename / foreground / location; no polling,
  no sleeps; a 10 s give-up that takes a splash fallback). Folder panes take only a new or activated
  `CabinetWClass` window; others take the started process's window or a new/activated window of the
  pane's exe (link target, file association, default https browser). Never an `explorer.exe` window that
  is not a folder window, never Themia's own. An app already running is moved instead of started when
  reuse is on (never for URLs or folders).
- [S-1410] Pressing an item and moving 5 px drags it (the selection, if it was on it); the list moves
  under the pointer; release commits exactly the preview (before the item under the pointer's first
  half, else after; empty space = end); an unchanged order writes nothing.
- [S-1411] "Edit layout…" (item menu, settings row) and "New layout" (settings, then straight into the
  editor) open one editor window for the app, retargeted when asked again; Escape and Done close it. It
  holds only the item id and writes through `AppHost.Mutate`; if the item disappears it says so.
- [S-1412] The editor draws the panes in the primary display's shape (≤ 440 DIP tall); dividers drag
  live and save once on release.
- [S-1413] Each pane: split side by side / top and bottom / remove (not on the only pane), and centred
  the icon and name when assigned, the source, then a searchable Start menu app box, a URL box, or a
  path box with Browse (checked by `LaunchTargets`, the reason shown), reuse, Clear. Shrinks with the pane.
- [S-1414] Debug: `--layout-editor-png <out.png> [waitMs] [--empty]` renders the editor off-screen over
  a demo state; nothing is launched.
- [S-1415] Dragging items past the widget's edge hands their local files and folders to an OLE drag
  (Copy or Link only — never Move: a shortcut drag must not move the user's file). Dropped back on a
  Shortcuts widget it reorders (same widget) or moves (another; Ctrl copies; the receiver adds first,
  then the source removes — orig. S-35).

Not ported: per-pane command-line arguments (allow-list rule: nothing is ever a command line); drag-out
of web, Windows-location and layout items (no file to hand over); Ctrl+Z op log (as PC). Needs live
verification: drops from Explorer / Edge / Chrome / the Start menu, pointer reorder and drag-out on the
real surface, layout launch with Word/Outlook/Code/browser/Explorer windows on multiple monitors and
mixed DPI, the editor's divider drag, pickers and app search.

## Context menus (R4-MENUS)

Every right-click menu in the app — a widget's card, an item inside a widget, empty surface in edit
mode, a view chip, the settings window — and how a click on a card in edit mode reaches the settings.
Owning files: `Themia.Core/Shell/ContextMenus.cs` (`ContextMenuLayout`, `ShellMenus`, `CardGestures`);
`Widgets/ItemMenu.cs` (the one builder), `WidgetCard.cs` (card menu, click), `DesktopWindow.Menu.cs`
(surface menu), `Gallery/MenuRender.cs`. Tests: `ContextMenuTests`.

- **[S-1740]** One menu, Windows 11 File Explorer style: a `CommandBarFlyout`, always expanded, with
  the common commands as a **row of icons** on top and the labelled list below. The row's commands
  and order: Cut, Copy, Paste, Rename, Duplicate, Settings, Delete. Each icon has its label as tooltip
  (with the shortcut, "Copy (Ctrl+C)") and as its accessible name. A disabled command stays in its
  place, greyed, so the row never shifts. A second entry claiming the same command, a toggle and a
  submenu stay in the list. `ItemMenu` is the only code that builds a context menu; no view builds a
  `MenuFlyout` for one by hand.
- **[S-1741]** With fewer than two row commands there is no row: a lone icon reads as a stray button,
  so it stays a labelled line in the list, and the menu is the list alone (a `MenuFlyout`: a command
  bar flyout with no row still draws an empty bar above its list).
- **[S-1742]** The list keeps its callers' order; separators collapse (none leading, trailing or
  doubled), including where moving commands to the row emptied a group.
- **[S-1743]** A widget card's menu: the row is Cut, Copy, Paste (disabled with nothing to paste),
  Duplicate, Settings and Delete, acting on the selection ([S-1592]); the list is Add widget inside
  (containers), the view's own commands, Move into (submenu, when there is a target), Bring to front /
  Send to back (grid-placed widgets only), Move, and Edit layout / Done editing.
- **[S-1744]** In edit mode a right-click on **empty surface** (not a card, the toolbar or the view
  bar) opens the surface menu: Paste and Settings in the row; Add widget (opens the picker at the
  click, whose choice then follows the pointer to be placed, [S-1587]), Select all ([S-1582], off
  with no widget), Snap to grid (checked when on, [S-06]) and Done editing in the list. Outside edit
  mode empty surface is click-through; while placing, the right-click cancels the placement and opens
  nothing.
- **[S-1745]** In edit mode a plain click on a card — pressed and released on it, not on a resize
  handle, without Ctrl, travelling less than 3 DIPs — shows that widget's page in the settings window
  **if the window is already open**, without bringing it to the front (the desktop keeps the
  keyboard) and leaving it alone when it already shows that page. A closed window is never opened by a
  click; a drag, a resize, Ctrl+click and a gesture lost to capture are not clicks.
- **[S-1746]** A widget's menus never change how it looks: no view, layout, sort, size, label,
  column, format or "show X" switches on the card menu or an item menu. Those are settings and live on
  the widget's settings page only (each one already has a row there). The menus keep commands the user
  runs while looking at the widget: refresh, pause, open, which folder, the click mode, search.
- **[S-1747]** Debug: `--menu-png <outDir>` opens the card menu (edit mode, one
  widget; desktop, three widgets), the surface menu and two sample item menus for real, off-screen,
  and writes each open popup to a PNG.
- **[S-1748]** A menu opened by the pointer opens down and to the right of it (top-left corner at the
  pointer), as Explorer's does, and flips only to stay on screen. A menu opened from the keyboard sits
  under its anchor's left edge.
- **[S-1749]** The Add-widget picker opens centred under what opened it — the toolbar's Add button,
  the point of a right-click on the surface, or the container it adds into — and flips above only
  when there is no room below.

Needs a live pass (pointer and focus on the real desktop): the surface menu opening only on empty
surface and at the pointer, Add widget's picker at the click then placing, the right-click that
cancels a placement opening no menu, keyboard use of the menus (arrows across the icon row and into
the list, Menu key / Shift+F10 on rows), submenus opening beside their line, a click on a card
following an open settings window (and a double-click still leaving edit mode), and both menus on a
second display with another DPI.

## Icon picker and showcase views (R4-ICONS)

The icon a view or a group is given, chosen from the whole Segoe Fluent Icons set, and a Debug tool
that puts every widget on the real desktop at every size tier. Owning files: Core
`Icons/FluentIcons.cs` (+ the generated `FluentIconData.g.cs`, from `scripts/fluent-icons.py`),
`Icons/IconSearch.cs`, `Widgets/WidgetTiers.cs`, `Model/ShowcaseViews.cs`; App `Shell/IconChoice.cs`,
`Shell/IconPicker.cs`, `Shell/Gallery/IconPickerRender.cs`, `Shell/Gallery/ShowcaseCommand.cs`. Tests
`IconSearchTests`, `ShowcaseViewsTests`.

- **[S-1800]** The picker offers every glyph of the WinUI Gallery's icon list (about 1,500: name, code
  point, tags; MIT, `THIRD-PARTY-NOTICES.md`), compiled into a C# table: no file and no JSON is read
  at run time. A choice is stored as the glyph itself, as before ([S-703]); a tooltip names a glyph
  by its Gallery name with the words spaced ("Global Nav Button").
- **[S-1801]** Thirty-two suggested icons (home, work, games, music, …: the former curated set) come
  first when nothing is typed, carry a name in every language, are found by that name and by the
  English one ("Zuhause", "home"), and win a tie in the ranking.
- **[S-1802]** Search (`IconSearch`): case- and accent-insensitive; every word of the query must
  match the name, a tag, a suggested icon's names, or the four-digit code point ("e80f"). Best
  first: code or whole name, name prefix, a whole word of the name, a word prefix of the name, a whole
  tag, a word of a tag, a tag prefix, then a substring of the name, then of a tag; ties go to a
  suggested icon, the shorter name, the lower code point. An empty query lists every icon. A query of
  punctuation only ("$") is matched whole against the tags.
- **[S-1803]** The picker (a flyout from the icon button) is built when it opens: the default cell (initials for a view, [S-903]) beside a search
  box, over a virtualised grid of 8 columns of 40-DIP cells, 8 rows high, scrolling. Typing filters at
  once; Enter in the box takes the best match, Down moves into the grid; arrows, Home/End and Page
  keys move in the grid and Enter or Space picks. The current icon is selected and scrolled to. No
  match says so in the grid's place, which keeps its size. Debug render:
  `Themia.exe --iconpicker-png <out.png> [--search text] [--theme light|dark] [--initials]`.
- **[S-1810]** Showcase views: one view per widget kind in catalogue order, named after the widget
  and with its glyph as the view icon, holding seven default instances of it, one per size tier
  (`WidgetTiers`: XS 120×120, S 200×150, M 320×240, Wide 480×160, Tall 200×400, L 560×420, XL 800×560
  DIPs). Each cell is the tier times the display's content
  scale ([S-850]) plus the widget gap, so the content lays out at exactly the tier.
  The cells are packed tallest first, lowest-then-leftmost, inside the work area less 16 DIP, without
  overlap, on whole DIPs. Containers are empty instances.
- **[S-1811]** Ids are derived, not random: `showcase-<kind>` for a view, `showcase-<kind>-<tier>` for
  a widget, so a rerun produces the same ids.
- **[S-1812]** When the seven do not fit one work area (1920×1080 at 125 %, for one), the rest go on
  further views of the same kind ("Clock (1)", "Clock (2)", ids `showcase-clock`, `showcase-clock-2`).
  A tier larger than the whole work area is shrunk to it.
- **[S-1813]** A rerun replaces the showcase rather than adding a second: the fresh views go to the
  end, an old one whose id comes back is replaced (a display showing it keeps it), one that does not is
  deleted with its references repaired ([S-902]). Widgets the user added to a showcase view go with it.
- **[S-1814]** Only through a Debug-build flag, never the app's UI:
  `Themia.exe --add-showcase-views [<statePath>]` (default: the app's own state file) lays the views out
  for the primary display, writes the file atomically through the store and exits. Exit 0 written,
  2 no display, 3 refused because a copy of Themia is running and the target is the app's own state
  file (that copy owns it and would overwrite the change), 4 the file is not a state document (left
  untouched), 5 the write did not land. A missing file starts from the default layout. The free tier's
  limits ([S-1065]) are not involved: they gate creating in the running app, a loose build is
  unrestricted ([S-1064]), and nothing over a limit is ever hidden or deleted.

Needs a manual pass: the picker's keyboard path inside the real flyout (Down from the box, Enter,
Escape), and the showcase views on a real desktop (each tier's size at
the display's scale, the second page on a small or scaled display, the refusal while the app runs).

## The Set up button (R5-SETUP)

Owning files: `Themia.Core/Widgets/WidgetSetup.cs`, `Themia.App/Shell/WidgetCard.Setup.cs`,
`Themia.App/Widgets/SetupPrompt.cs`. The section also records the removal of the debug widget
gallery ([S-1863]).

- **[S-1860]** A widget that can show nothing useful until it is configured needs setting up, decided
  for every kind by one pure rule, `WidgetSetup.Needed(config, signed-in features)`: Countdown with no
  timer; Quote with no quote left; Colour swatch in palette mode with no colour; Feed with no feed
  address; Stocks with no symbol; Weather (now and forecast) with no place; Video stream with no
  address; Shortcuts with no item; Power with no action; Pictures with no folder (folder source) or no
  file (file source); the GitHub repository widget with no repository. "Nothing right now" (no unread
  mail, no events today, an empty folder) is not setup, and neither is set-up-but-wrong (a symbol
  that is not one, a folder that is gone): those keep the widget's own sentence. Widgets that start
  with seeded content (quotes, colours, shortcuts, power actions, Sun & moon's place, the folder
  widget's profile folder) never ask until it is emptied. Habits and a local task list are started
  from the widget itself and do not ask.
- **[S-1861]** An account widget needs setting up while no signed-in account offers its feature: Mail,
  Calendar, Contacts, Tasks (account source only), OneDrive (files), the three GitHub list widgets,
  Claude usage. The features are the union of the signed-in providers' (`WidgetSetup.Offered`); an
  account that needs signing in again still counts (the widget says so itself, with the Accounts link).
- **[S-1862]** Such a widget's body is the Set up button and nothing else: the widget's glyph over one
  accent button ("Set up"; tooltip "Open this widget's settings"), centred, no sentence. The glyph is
  dropped on a body too short for both; the button stays at every size, XS included. A click opens
  the settings window on the widget's page (opening the window if it is closed), the same request as
  the card menu's Configure; the button takes no focus, so it works outside edit mode on the
  `WS_EX_NOACTIVATE` surface. The card owns it, not the view: the view keeps running underneath,
  invisible and not hit-testable, and the button goes the moment the config (from settings, or a
  change the view saves itself such as a shortcut dropped onto it) or a newly signed-in account makes
  the widget useful. The views no longer carry their own "add one in settings" sentences.

- **[S-1863]** There is no widget gallery: no `--gallery` window, no `--gallery-png` render, no
  toolbar entry, and no `#if DEBUG` sample data inside widget views or services. A widget's layout at
  the seven size tiers (`WidgetTiers`) is checked on the showcase views ([S-1810]) on the live
  desktop; the other debug render flags (`--settings-png`, `--picker-png`, `--viewbar-png`,
  `--notices-png`, `--menu-png`, `--iconpicker-png`, `--motion-png`, `--layout-editor-png`) stay and
  draw widgets with their default config. The tags that described the gallery's fixtures are retired.

Needs a manual pass: the button on the live desktop outside edit mode (opens Settings on the right
page, from a closed and from an open window), and the button going away when an account is added.
