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

- **Foundations.** Geometry model; Rendering; Grid snap aid; Constraints; Defaults & placement; Views & displays; Window layering; Transparency; Ticking; Live widgets and resource use (R6-RESOURCES); Persistence; Failure rules; Clipboard & transfer between displays; No backwards compatibility.
- **Shell, windows and settings.** Displays (WP2); Desktop integration and suspend (WP3); Appearance engine (WP1); Notices, resilience and undo (WP4); Widget picker: search (WP5); Multi-select and the placement session (MULTISELECT-PLACEMENT); Context menus (R4-MENUS); Views and the view bar (WP8); The Displays and Views pages (R4-NAV, R5-PAGES); Icon picker and showcase views (R4-ICONS); Keyboard lease (WP11); Widget keys (WIDGET-KEYS); Keyboard in the shell (SHELL-KEYS); Opening a widget's app (R7-OPEN, retired); Shell hooks (PA); Removed widgets (R7-REMOVE); Shell skeleton and shared settings; Settings window: Windows 11 Settings; Settings navigation icons (NAV-ICONS); Logging (WP7); Network safety (WP6); Localization infrastructure (LOC-INFRA); Microsoft Store package and licensing; Gallery.
- **Look and layout.** Rows and the card edge; Looking like Windows; The widget header; Responsive layout (shared foundation); Adding a widget; Layout pass over the ported widgets; Widget polish 1 (POLISH-1); Widget polish 2: leftover parity and layout gaps (POLISH-2); The Set up button (R5-SETUP).
- **Accounts and sign-in.** Where account data comes from; Accounts and sign-in.
- **System widgets.** System data hub (SYS-HUB); System meters; Meter framework and memory; CPU; Disk and ping; Battery and network; Network and ping; System widgets: GPU, mixer and screen time; GPU and screen time: the whole tile (SYS-P4); System details (SYS-DETAILS); Mixer and Volume (SYS-P5); Power and Bluetooth; Device and location (DEVICE-AND-LOCATION).
- **Time, desk and launch widgets.** Clock; Countdown and pomodoro; Sun and moon (two widgets, SKY-SPLIT); Audio visualizer (VISUALIZER); Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ); Music and Pomodoro: parity and the whole tile (PF); Notes; Notes: note store and markdown editor; Clipboard and Countdown: the whole tile; Habits; Shortcuts A: display and items (PC); Folder: the file browser (FOLDER); About and the settings file.
- **Mail, calendar, tasks and online widgets.** Mail; Calendar and contacts; Tasks; Mail: the whole tile; Calendar and contacts: the whole tile; Tasks: using the whole tile, and the rest of the original; Weather: current conditions and forecast; Feed, stocks and the web widgets: parity and the whole tile (OFF-E); Web widgets: video stream; Web widgets: size ladder and overlay; Offline sync (SYNC).

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

## Live widgets and resource use (R6-RESOURCES)

Owning files: `Themia.Core/Widgets/WidgetLiveness.cs`, `Themia.Core/Widgets/FolderRealize.cs`,
`Themia.App/Shell/DesktopWindow.Layout.cs`, `Themia.App/Shell/WidgetCard*.cs`,
`Themia.App/Services/ReleaseSweep.cs`, `Themia.App/Shell/Gallery/SwitchCheck.cs`. Tests:
`WidgetLivenessTests`, `FolderRealizeTests`.

- **[S-1880]** Only a **live** widget is attached, ticks, polls or holds anything (system feeds linger 2 min after their last widget, [S-2505]): one in a view some
  display shows (`WidgetLiveness.ShownViews`, each display's own view or the active one, a view on two
  displays once) that runs ([S-1881]). A view no display shows costs its JSON and nothing else: no
  card, no ticker subscription, no service subscriber. Shared services start with their first
  subscriber and stop with their last, so a service whose widgets are all hidden does no work. The
  exceptions by design are the screen-time tracker ([S-459]) and the offline sync ([S-2608]), which
  keeps the account and feed widgets of every view synced so a view switch asks nothing.
- **[S-1881]** A widget that needs setting up ([S-1860], [S-1861]) does not run: its view is built but
  **parked** under the Set up button, never attached, so it polls nothing, ticks nothing and logs
  nothing. When the config or a newly signed-in account makes it useful a fresh view is attached in
  its place; a widget emptied again is detached and parked. A parked view hears no config, no size,
  no keys and offers no context actions. The one widget set up by using it, an empty Shortcuts widget,
  keeps running under the button so a file can be dropped onto it. A view acquires nothing in its
  constructor that only Detach releases (a timer's Tick roots the view from the native side): such
  subscriptions are made in Attach. A card detaches its view exactly once, whichever path asks first,
  and a view whose Attach throws half-way is still detached.
- **[S-1882]** A layout pass matches cards to widgets by the widget object (`WidgetLiveness.Reconcile`);
  every other card is **released before** any new one is built, so a view switch never holds the old
  view and the new one together. A pass that released cards asks for one background collection, at
  low priority after the new view is drawn (`ReleaseSweep`, one per burst): a card's native XAML tree
  is freed only when its managed wrappers are collected, and a widget layer allocates so little
  managed memory that, left alone, released views piled up (about +60 MB per pass over the showcase
  views, 412 MB to 820 MB private in eight passes). With the sweep the figure stays in one band.
- **[S-1883]** Debug render: `--switch-check <rounds> [<out.txt>] [--ordinary | --show <view>]
  [--per-view] [--dwell ms]` builds a real host and surface off-screen over a detached state holding
  the showcase views and an empty view, switches through every view `rounds` times and prints, after
  each round and a forced collection: working set, private bytes, managed heap, cards alive, views
  attached against the live widgets, ticker subscriptions, threads and handles, marking a breach LEAK.
  `--per-view` prints each view's cost while shown; `--show` measures one view and draws it, then
  with every list scrolled to its end.
- **[S-1884]** Debug builds keep a weak census of every card and attached view (`LiveCensus`) for the
  switch check: after a collection, a card of a view no display shows that is still alive is a leak.
- **[S-1885]** Baseline (switch check `--ordinary`, Debug build, one 1920x1080 stand-in display at
  100%): the empty surface is about 155 MB working set; eight everyday widgets (clock, CPU, memory,
  folder, shortcuts, notes, now playing, volume) add about 40 MB. Per view of seven showcase widgets,
  while shown: Folder about 13 MB
  (was about 175 MB before [S-1887]), everything else under 7 MB.
- **[S-1886]** "Not set up yet" is a state, not a fault: a fetch status change to it is logged at debug
  level, never as a warning (`FetchState.LogLevelFor`); real failures stay warnings, an answer after
  one is an info line.
- **[S-1887]** The folder widget builds only the items in view and one viewport below
  (`FolderRealize.Target`), in order and whole rows at a time; the rest is a spacer of exactly their
  height (`FolderRealize.SpacerHeight`), so the scroll range is right from the start. Scrolling,
  growing the tile, the keyboard cursor (End, Page down) and a rename build further; nothing built is
  taken down before the next full redraw. The "+N more" row comes once everything is built.

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
  dropped with a report, because this build cannot render it at all ([S-1900]).
- **[S-1900]** A widget whose kind is a name this build does not have (a removed widget, a hand edit)
  is taken out of the document **before** the typed read (`AppStateStore.Parse`, by the `kind`
  string), and each one is reported ("dropped widget 'id' of unknown kind 'Calculator'"); the rest
  of the document loads. The typed read would otherwise refuse the whole file over one widget and
  set it aside. A property the model no longer has (a widget's old `parentId`) is ignored like any
  unknown JSON, so a widget that sat in a removed container loads on the desktop at its own rect.
  Validation, not migration ([S-1050]).

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
- **[S-72]** A clipboard entry is a **snapshot** of the selection — every selected widget ([S-1588])
  — with configs and relative layout. It survives the originals being moved, reconfigured or deleted.
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

- **[S-89]** Mail, calendar, contacts and tasks are synced by Themia
  itself, from accounts it signs in to and stores on this PC ([S-500]..[S-539]). There is no external
  hub. The earlier design, which read them from a separate hub program ([S-90]..[S-99], [S-180]..[S-187]), is retired: a widget
  that depended on another program running and granting consent showed nothing whenever it was not,
  and the user wanted Themia to own its data. Nothing of that build's data is read ([S-1050]).

## Network and ping

- **[S-106]** Ping is sampled asynchronously and never blocked on from the tick. One probe is in
  flight at a time; a probe that overruns its interval delays the next reading rather than stacking
  up.
- **[S-107]** A late ping completion is discarded once its prober is closed ([S-2507]); a host change is another prober ([S-2508]).
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

## Notes

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
- **[S-169]** One lit-limb outline (`SkyMoonDisc`) draws both the Moon widget's disc and its row
  glyphs, so they cannot disagree. It is mirrored for a southern observer, keeps a hairline rim on
  the unlit part so a thin crescent still reads as a moon, and dims below the horizon. (It once sat
  beside the sun's graphic; the moon is its own widget now, [S-3100].)
- **[S-170]** The two display settings are independent, except that hiding the graphic forces the
  rows on: a widget showing nothing is never the answer. The graphic itself is never dropped — it is
  sized down to a 46px floor and keeps drawing. Only extras shed, cheapest-first by width: the
  countdown, then the label icons and the moon percentage, then the daylight label, then the number
  strip. (An earlier version of this line had the daylight label going before the icons. The code
  was right and the spec was wrong: "12h 6m" says more than two glyphs do.) This is the Sun widget's
  rule; the moon percentage it once shed is the Moon widget's ([S-3105]).
- **[S-171]** (Reason superseded by [S-1494]: the place is now picked by city search; the validation
  below still governs the exact-coordinate fields.) There is no geocoding service here and widgets make no network calls, so the settings
  take latitude, longitude and place name directly. Input that is not a number in range is
  **rejected** — the last good value stands and the row says why — never coerced to 0, which would
  silently move the user to the Gulf of Guinea.

### Two widgets: Sun and Moon (SKY-SPLIT)

Sun & moon is split in two (user rule: one widget, one thing). Core: `SkyConfigs` (`SkyPlaceConfig`,
`SunConfig`, `MoonConfig`), `MoonPhase.NextRiseSet`, `MoonLayout`, `SkyText.EventTime`; App:
`SunView`, `MoonView`, `SkyParts` (the shared row and moon drawing), `WidgetSettings.Sun/Moon/Sky`;
tests `SkyWidgetsTests` (`S310x_`).

- **[S-3100]** **Sun** (`WidgetKind.Sun`, 320×240, sun glyph) is the sun's day: the path graphic, the
  number strip and the rows sunrise, sunset, daylight (+ change), solar noon, dawn, dusk ([S-3102]).
  **Moon** (`WidgetKind.Moon`, 240×220, moon glyph) is the moon: the disc, its phase's name, the lit
  percentage, moonrise, moonset and the next full or new moon ([S-3105]). Both sit in the picker's
  Time section after Pomodoro, have a Top header titled with the place, never need setting up (they
  start at Zurich) and have no keys ([S-1996]). There is no combined kind: `SunMoon` (16) is gone and
  a state file holding it drops that widget at load like any unknown kind ([S-1900], [S-1050]).
- **[S-3101]** Both carry the observer's place (`SkyPlaceConfig`: latitude, longitude, place name)
  and share one settings block: city search ([S-1494], [S-2840]) and the validated exact
  coordinates ([S-171]). Sun adds the sun path mode and the Events switch; Moon has nothing else.
  `Repair` at load: a latitude or longitude that is not a number in range sends both, and the name,
  back to Zurich; a place name over 80 characters is cut; an unknown sun path mode becomes Numbers;
  each repair is logged.
- **[S-3102]** Sun ladder: [S-1492] without the moon — graphic (46 px floor, [S-170]) → strip →
  rows in priority sunrise, sunset, daylight (+change), solar noon (+altitude), dawn, dusk. One row
  is the next event. Rows beside the graphic on a strip; two to a line on E×E (sunrise | sunset,
  daylight | noon, dawn | dusk) with the graphic keeping 45% of the height.
- **[S-3104]** Moonrise and moonset: the next of each strictly after now, from the moon's altitude
  (the [S-165] sublunar point) stepped every 10 minutes over the next 50 hours and each horizon
  crossing bisected; a crossing is the centre at +0.125° geocentric (parallax less refraction and
  semi-diameter). Null when the moon does not cross in the window (it stays up or down near the
  poles) and the row then shows an em dash. Good to a few minutes (truncated lunar series).
- **[S-3105]** Moon ladder (`MoonLayout`, on one `Disclosure`): the disc (never dropped, 24 px floor)
  → the phase's name (measured at the language's longest name) → the lit percentage → whole rows
  moonrise, moonset, the next full or new moon ([S-1491]). The disc takes what the rungs leave, up
  to 192 px, so a larger tile only draws a larger moon. On a wide strip (Wide shape, width ≥
  Regular) the text and rows stand beside the disc. With one rise/set row it is the next of the two;
  the next one carries its countdown in the accent ([S-163]). The next full or new moon drops its
  weekday where the row cannot hold it ("Oct 26").
- **[S-3106]** A rise or set reads as the time alone when it falls today in the machine's zone, else
  with the short weekday before it ("Mon 9:55 AM"): the next one is often tomorrow.

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
  every row is the single clearest tell of a web table.
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

## About and the settings file

The settings window's About page. Owning files: `Shell/Config/ConfigPages.About.cs`,
`Shell/Gallery/SettingsRender.cs`, `Themia.Core/Storage/SettingsTransfer.cs`.

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

Not ported, on purpose: Karpo's "isn't running / Start" card — the settings window here runs
inside the app, so it is never open while the app is not.

## Web widgets: video stream

Ported from `widgets/online/video-stream` on `main`. Owning files: `Widgets/VideoStreamView.cs`,
`Widgets/RotatedBox.cs`, `Widgets/ShownWatch.cs`, `Widgets/IWidgetContextActions.cs`,
`Services/Web/StreamSession.cs`, `Shell/Config/WidgetSettings.Web.cs`,
`Themia.Core/Widgets/{VideoStreamRules,MjpegFrameReader}.cs`,
`Themia.Core/Model/Configs/WebWidgetConfigs.cs`. The original's web element widget is not part of
this app ([S-1724]).

The widget runs only while it can be seen. A paused camera, or one whose card is not in the tree or is
collapsed (`ShownWatch` follows the card's ancestors), holds no socket and no decoder; `Detach`
releases everything.

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
- **[S-1991]** Video stream: Space pauses and resumes (`ControlKeys.Stream`), as the overlay button
  does, and is persisted the same way. Only a tile with a URL takes the keyboard.
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

Themia's own accounts, the source of mail, calendar, contacts and tasks. Ported from the classic app's `widgets/office/email/src/lib.rs`
(storage, DPAPI, the four OAuth flows, IMAP/DAV verification, retry) and its Email SPEC [S-45],
[S-47], [S-51], [S-53]; code in `src/Themia.Core/Accounts/` (pure) and
`src/Themia.App/Services/Accounts/` + `Shell/Config/AccountsPage.cs`.

- **[S-500]** A closed `AccountProvider` set: Microsoft, Google, IMAP, CalDAV, CardDAV. A provider
  this build does not know is dropped on load and reported, never carried as a string ([S-1901]).
- **[S-501]** Providers are written under lower-case names (as the original wrote them), so the
  file stays readable and a hand edit is easy to validate.
- **[S-502]** What an account feeds (`AccountFeatures`) and how it proves itself (`AccountAuth`) follow
  from the provider alone. Widgets ask for a feature, never a provider name.
- **[S-503]** `accounts.json` lives beside `state.json` and carries a `schemaVersion`; a newer schema is
  set aside rather than half-read.
- **[S-504]** Every stored entry becomes a whole `Account` or nothing. A field that says *where* the
  account is (provider, IMAP host, DAV address) cannot be guessed and its entry is dropped; a field it
  can live without (nickname, IMAP port) is repaired; a missing secret is kept as "sign in again".
  Every drop and repair is logged. Duplicates keep the first.
- **[S-505]** Ids are built exactly as the classic app built them (`google:<sub>`, `imap:user@host`,
  `caldav:user@authority`, the Graph id for Microsoft), so signing in again replaces the account
  (keeping its nickname).
- **[S-506]** A secret is stored as `dpapi:v1:` + base64 of a DPAPI blob — the classic envelope byte
  for byte. Untagged text and a damaged payload are told apart from a good blob.
- **[S-507]** An access token within five minutes of expiry is treated as expired; a missing or absurd
  `expires_in` becomes an hour (ceiling 30 days).
- **[S-508]** PKCE S256 (RFC 7636 test vector pinned) with a 43-character unreserved verifier, and a
  random state compared in fixed time.
- **[S-509]** Microsoft signs in against `common` with the classic client id and the scopes the
  widgets need (`User.Read Mail.ReadWrite Calendars.Read Contacts.Read Tasks.ReadWrite
  offline_access`; no file scope, [S-1901]), by the browser flow of [S-1780] (was: device code);
  Google's consent URL asks for `access_type=offline` and `prompt=consent` so a refresh token always
  comes back.
- **[S-513]** The loopback redirect is read from the request line; the state is checked before anything
  else is believed (error included); malformed pairs are skipped; a request that is not a redirect
  (favicon) is answered 404 and the listener keeps waiting.
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
- **[S-535]** OAuth sign-ins verify who signed in (Graph `/me`, Google userinfo) before saving.
- **[S-536]** Google's Desktop client secret is never in source: it is compiled in from
  `THEMIA_GOOGLE_CLIENT_SECRET` (a generated constant, validated to look like a secret); without it,
  Google sign-in refuses before the browser opens and token refresh fails with the same sentence.
- **[S-539]** Settings → Accounts lists each account (provider icon, name, address, problem), renames
  in a flyout, signs out after a confirmation, and adds through one flow at a time drawn in place:
  Microsoft and Google waiting on the browser ([S-1783]); forms for IMAP, CalDAV and CardDAV. Errors
  appear on the page; leaving it cancels a flow.
- **[S-1901]** No GitHub or Claude account and no OneDrive access: they went with the widgets that
  used them ([S-1902]), with the GitHub device-code and token sign-ins, Claude's paste-back sign-in
  and Microsoft's `Files.Read` scope. A stored `github` or `claude` entry in `accounts.json` is an
  unknown provider: dropped on load and reported ([S-500]).
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
  `error`, else the HTTP status. Microsoft and Google exchanges share it.
- **[S-1783]** Settings → Accounts: Microsoft and Google share one browser card ("Your browser opens
  *provider*'s sign-in page…", a live status line, Cancel). There is no code to copy for Microsoft any
  more.
- **[S-1784]** The loopback listens on 127.0.0.1 and, on the same port, on ::1, so a `localhost`
  redirect lands whichever address the browser tries first; without ::1 (IPv6 off, port taken there)
  it carries on with 127.0.0.1 alone, which browsers fall back to.

Not ported: the anonymous endpoint probe and its preset health badges ([S-46], [S-48]) — the presets
here are a fill-in table and the add itself is the check; the two-page preset picker and brand icons
([S-47]) — one form with auto-fill instead; STARTTLS on port 143 — the classic app spoke implicit TLS
only; RSS "accounts" — feeds are a widget setting here; the `devOnly` gate on Google OAuth — a
release decision, not wired. Digest auth for DAV is not supported (basic only, as before).

## System widgets: GPU, mixer and screen time

Ported from `widgets/system/{gpu,mixer,screentime}` on `main`. Owning files:
`Themia.Core/Widgets/{GpuRules,MixerRules,AppNames,ScreenTimeTracker,ScreenTimeRules}.cs`,
`Themia.Core/Storage/ScreenTimeStore.cs`,
`Themia.App/Services/{GpuSampler,CoreAudio,ProcessNames,AppIcons,ScreenTimeService}.cs`,
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
  sampled on the pool, single-flight, read by the hub once per due tick ([S-2501], [S-2507]).
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
- **[S-447]** A muted row's slider sits at 0 but the row keeps its level; dragging a muted row above zero unmutes it.
  The mute button toggles a row (all of a group's sessions); right-click offers Mute/Unmute and
  Reset to 100%. Rows are reused by position so a snapshot never rebuilds the slider under the
  pointer.
- **[S-448]** No device: "No output device". No rows: "No app is playing audio". Failures are logged
  once, and again only when they change.
- **[S-449]** From 360 px a row is one line (name, slider, mute); narrower, the slider goes
  under the name; under 170 px a row is icon and mute ([S-2771]: no percent).

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
- **[S-1989]** Screen time keys: Up/Down, Page Up/Down, Home/End light one legend app the way
  hovering does (the rest dim, the headline names it); the Menu key opens its Hide menu (not on
  Other); Left / Right step the day as the header arrows do, Day period only and never past today;
  Escape lets the app go. Hiding is never a bare key: it has no undo.
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
  lines the box has and ends in an ellipsis. Every empty state is `Fluent.EmptyState`; the countdown
  view no longer draws a text block of its own. The panel never touches the glyph's
  `Visibility`, which several views own.
- **[S-643]** *(Superseded by [S-2501]: the hub is the only reader, once per tick.)* CPU load and network throughput are deltas against a process-wide baseline, so a read
  within 250ms of the last real one is answered from that one. Two CPU tiles used to show 31% and
  0% side by side: the first moved the baseline and the second measured a millisecond.
- **[S-644]** A short pomodoro tile keeps the countdown: a wide one moves the controls beside it, a
  narrow one drops the progress bar. Stacked, a 120px tile had no room left for the digits.
- **[S-645]** The clock insets its readout fully at the sides and by half above and below.
- **[S-646]** A reading's caption (meters, power, weather's condition) moves under the reading when
  it would be trimmed beside it, or when its own line lets the reading be drawn larger.
- **[S-647]** *(Retired: the power tile is gone, [S-1910].)*
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
the scrolling lists (feed, folder) show a partial last row at
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
  at once and waits in the account's queue until the provider has it ([S-2610], [S-2624]); a refusal
  is the header warning's tooltip and the next probe fetches the truth.
- **[S-545]** Graph `mailFolders/{f}/messages` (`$top`, `$select`, newest first); Gmail `messages`
  list then `format=metadata` per message, four at a time, the snippet's entities decoded, web link
  `?authuser=<address>#all/<thread>`; IMAP per [S-553]. One message shape for all three, carrying
  the account, provider, folder and the provider's own id.
- **[S-546]** One `MailService` per app (`AppServices.Mail`). Widgets open shared feeds keyed by
  accounts + folder + count, which are views over the stored folders ([S-2620]): opening one draws
  what is stored and asks nothing. Each folder a Mail widget in any view reads has its counts probed
  every 2 minutes (Graph folder, Gmail label, IMAP `STATUS`) and its list fetched only when they move
  or every 15 minutes [S-25, S-26]; Refresh fetches now. Failures back off on `Backoff` (429 far
  longer); a state change is logged once. The cadence rides the shared ticker, so it stops while the
  desktop is hidden, and ends when no widget in any view reads mail ([S-2608]).
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

## Tasks

Ported from `widgets/office/todo` on `main` and its Graph / Google
calls in `widgets/office/email/src/lib.rs`, off the earlier hub program and onto Themia's own accounts. Owning
files: `Themia.Core/Tasks/*` (model, due dates, list rules, wire, optimistic edits, Today fan-out,
local store), `Themia.Core/Model/Configs/TasksConfig.cs`,
`Themia.App/Services/Tasks/*`,
`Widgets/{TasksView,TasksView.Rows}.cs`, `Shell/Config/WidgetSettings.Tasks.cs`.
Tests: `TaskRulesTests`, `LocalTaskListsTests`, `TaskSyncTests`.

- **[S-590]** A Tasks widget reads Microsoft To Do (Graph) or Google Tasks through an account
  with the Tasks feature, or a list kept on this PC (`TaskSource`, a closed set). An empty account
  id means the first such account and an empty list id its default list, so a widget dropped by a
  signed-in user shows tasks without a trip to settings; a *named* account that has gone is never
  swapped for another — the widget says so. Every non-list state names itself (no account, sign in
  again, account gone, no lists, loading, offline / provider error, no list on this PC) and the ones
  Settings → Accounts can mend link there; a list on this PC needs no account at all.
- **[S-591]** One `TaskService` per app (`AppServices.Tasks`). Widgets on the same account share its
  stored tasks and one sync ([S-2650]); a widget's own tick asks nothing. A sync never stacks on one
  still out (a forced one is remembered and runs after); failures back off through `Backoff` (a 429
  far longer). Graph and Google are
  read through `GetGraphPagesAsync` / `GetGooglePagesAsync` (400 tasks a list at most); ids are
  escaped into the path; completed tasks are filtered for Graph here and by Google's own
  `showCompleted`; Google's hidden and deleted tasks are dropped; every Graph status but `completed`
  is open work. Refresh is configurable, 30–600 s (default 60 s).
- **[S-592]** The list picker offers the account's default list, Today, then its lists. The lists are
  stored per account ([S-2650]) and read again with every sync (single-flight; a failure with nothing
  stored is said).
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
  Writes to one account go out one at a time, in order, through its persisted queue ([S-2653]):
  offline or throttled a change stays on screen and waits; only a refusal takes it back. Every feed
  of the account (a list and Today) shows the same pending changes. The round marker is the one control that completes a task.
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

Not ported, deliberately: the pop-out window [S-39] (out of scope by user decision), the per-widget
header hide and label [S-02, S-20] (the card's own header does this for every widget), the 600 ms delay before a completed task leaves the list [S-26] (no wall-clock timing in the
list; it leaves at once), the one-shot `localTasks` config-bag migration [S-59] (the app is
unreleased: no compatibility code), and the single click on a title that renames with the caret at
the click point [S-50] (a click focuses the row for the keyboard; double-click or F2 renames).
Drag-to-reorder, keyboard navigation, Tab between editors, the scroll fade and the "!" badge are
ported in [S-781]..[S-788].

## Calendar and contacts

The classic Calendar and Contacts widgets, on Themia's own accounts ([S-529]) instead of the earlier hub
hub. Pure rules in `Themia.Core/Office` (tested), one shared source in `Services/Office`, the views in
`CalendarView*.cs` / `ContactsView.cs`, settings in `WidgetSettings.Calendar.cs` / `.Office.cs`.

- **[S-560]** Calendar shows events from Microsoft Graph (`calendarView`, `Prefer:
  outlook.timezone="UTC"`), Google Calendar (`events`, `singleEvents=true`) and CalDAV (`REPORT
  calendar-query`) accounts. An all-day event is held as two dates (first day, exclusive end) and is
  never converted between zones; a timed one as two instants. Clicking an event opens its provider
  page (CalDAV has none: tooltip only).
- **[S-561]** Config: `accountIds`, `calendars`, `calendarColors`, `layout` (month | week | day | year |
  agenda, default month, [S-2700]), `weekStart`, `showBirthdays` (on), `agendaCount` (1–100, 20). The
  context menu offers Refresh; the layout is chosen in the settings only ([S-1746]).
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
- **[S-568]** Ranges: month = whole weeks around the month; week; day; year = 1 Jan – 31 Dec;
  agenda = today and the next 30 days. Requested from local midnight to local midnight (the classic
  app used UTC midnight). The header title is the period ("October 2026", "28 Sep – 4 Oct",
  "Sun 4 Oct", "2026"); the agenda's is the widget's name.
- **[S-569]** Back / forward step a month (day clamped), week, day or year (29 Feb clamped); the
  agenda pages its rows ([S-2709]). A today button shows when today is off screen (the agenda: past
  its first page). The widget follows today across midnight until the user pages away.
- **[S-570]** A day lists birthdays, then all-day events, then timed ones by start; an all-day
  event covers [first, end); a timed one shows on every local day it overlaps.
- **[S-571]** Agenda: grouped by day ("Today", "Tomorrow", "Mon 5 Oct"), at most `agendaCount`
  entries; an event still running from before is filed under today; timed events already over are
  left out. Rows: time column (start over end, "All day", "Now"), title, location · organizer; on a
  tile under 260 px the time folds under the title.
- **[S-572]** Month cells: chips (title, coloured edge) as many as fit, the overflow as "+N" beside
  the day number; on smaller cells up to three dots (two in narrow cells) plus a muted one; on the
  smallest the number alone, strong when the day has something. Today is an accent disc.
- **[S-573]** Day: an all-day bar (all-day and multi-day events, birthdays) and a timeline of the
  hours that fit ([S-2705]), overlapping events in side-by-side lanes. Not scrolled.
- **[S-574]** *(Retired: a layout never falls back to another, [S-2700].)*
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
- **[S-588]** One shared source answers every widget, from the offline store ([S-2630], [S-2640]):
  the weeks around today are synced every 5 minutes and the whole year-each-way window every 6 hours,
  contacts every 15 minutes, calendar lists every 6 hours, birthdays daily; a sync already out is
  joined, a failure keeps the stored data and is held (an hour for sign-in / access refusals, 15
  minutes for a rate limit) unless the user refreshes. States: loading; no account (the Set up button, [S-1862]); account gone; sign-in or access needed (with the Accounts link);
  offline and service errors; nothing on. A failure of some accounts with data from others is a
  header warning that opens Accounts when the user can fix it.
- **[S-589]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

*(S-574's fallbacks are retired: a calendar layout never becomes another ([S-2700]); S-581's and
S-584's size rules are refined by [S-1270] and [S-1275]; a month fetches and draws only the weeks
holding its days ([S-1264]).)*

Not ported: the per-widget header hide (the card owns the header), the reauth button that signed
accounts out (the Accounts page is where sign-in is mended), the birthday-permission toast (there
is no toast pipeline yet; the header warning stays and opens Accounts), and birthdays from CardDAV
(the classic app had none either). Keyboard navigation goes through the shell's keyboard lease: see
[S-1269] / [S-1277] and [S-806] / [S-807].

## Responsive layout (shared foundation)

- **[S-660]** A widget's content area (the card less its header) is classified per axis into
  `SizeClass` Compact / Regular / Expanded / Huge (width breakpoints 180 / 400 / 720, height
  128 / 280 / 480 DIPs) plus a shape (Wide ≥ 2:1, Tall ≥ 1:1.6). The seven size tiers (`WidgetTiers`) land in
  seven distinct (width, height) pairs. An unmeasured area is Compact. (`WidgetArea`)
- **[S-661]** Optional parts are disclosed in priority order from one height budget: a part is shown
  only when it fits whole; once a part misses, no later part is shown (growing a tile only ever adds
  parts); a part too narrow is skipped without closing the ladder. (`Disclosure`) Which parts a
  ladder may have at all is [S-666].
- **[S-662]** Lists and line budgets count whole rows only, one arithmetic for all of them
  (`Disclosure.WholeRows` / `TakeRows`, 0.5 DIP rounding tolerance).
- **[S-663]** The card tells a view that implements `IWidgetSizeAware` its slot, once per layout pass
  that changed it, never unmeasured; views that do not implement it are unaffected.
- **[S-664]** Meters: the reading, then the history growing to fill the tile; a tiny tile is the
  reading alone (the ladder is [S-1762]). A wider chart spans more history (one sample per ~6 DIPs).
- **[S-665]** Clock: time → date (short "Sun 4 Oct" when narrow; with the offset ([S-1485])
  appended on a wide strip) → the offset line. No week, day of the year or time zone line at any
  size (retired, [S-666]). A tall column stacks hours over minutes when that draws them larger.
  Analog face unchanged.
- **[S-666]** **Concise first** (user rule, replaces "empty space at L/XL means a missing rung"):
  every rung is the widget's own content. Growing may show more of its own items (more rows or
  columns, a larger hero, a meter's history) or one closely related detail of them, never a new
  feature: no control that belongs to another widget or to Windows (the mixer's output-device
  picker), no secondary panel (detail, reading or Today panes, a note list), no counts or stats
  footer, nothing "because there is room". Room the core content cannot use stays empty. The
  rungs retired under this rule are marked *(Retired …)* in their sections ([S-1222], [S-1229],
  [S-1231], [S-1306], [S-1363], [S-1384], [S-1427], [S-1443], [S-1496], [S-1497], [S-1569],
  [S-1624], [S-723], [S-725], [S-763], [S-767], [S-768], [S-783], [S-1213], [S-1274], [S-1285],
  [S-665], [S-1571], [S-1323], [S-1284], [S-1469], [S-1475]; the stock
  facts and range chips, [S-2770]; the mixer's percent, [S-2771]).

## Shell skeleton and shared settings

Prep for the parallel shell and widget packages (S-700..S-719). Mostly file layout; the behaviour
rules are these.

- **[S-700]** The clock starts with its header **hidden**, as in the original
  (`WidgetInstance.DefaultHeader`); so do the power buttons ([S-1911]) and the audio visualizer
  ([S-3200]). Only creation reads the default; a loaded widget keeps the
  placement it was saved with.
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
- **[S-763]** *(Retired: the photo of the day's title and credit caption and its `ShowCaption`
  setting are gone, [S-2760].)*
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
  Regular width; the picture is the whole slot at every size (no "up next" thumbnails beside or
  under it, retired [S-666]; the L file-name line and the XL "N of M · folder" line are retired, [S-2760]). Grid / scatter / mosaic take more pictures as the card grows. Scatter and
  mosaic are dealt from one seed per scan, so a resize moves pictures rather than reshuffling them.
- **[S-768]** *(Retired: no "up next" thumbnails to tap, [S-767]. Home / End and the arrows still
  jump, [S-1992].)*
- **[S-1992]** Slideshow keys (`ControlKeys.Slideshow`) beyond [S-811]: Home / End jump to the first
  and last picture, Enter opens the shown one (local files only, as a double-click does). The other
  layouts take no keys, and a click on them no longer takes the keyboard (`WantsKeys`).
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
- **[S-2760]** No words on a picture (user rule, [S-666]): no title, credit or file name under or
  over any picture, at any size, in any layout or source (slideshow included), and no hover tooltip
  naming it (the photo of the day's credit tooltip, [S-3112], is the one exception); the freed height is the picture's. A single file's header keeps the catalogue label
  rather than the file's name; a folder's header still names the folder, the photo of the day's says
  "Photo of the day". The picture's name stays only as its screen-reader name. (`GalleryPlan`; tests
  `S2760_`.) Debug renders show real pictures with
  `--widget-png Gallery … --state slideshow|grid|scatter|mosaic|file` (Windows' own wallpapers).
- **[S-3112]** The photo of the day credits its picture: Bing's pictures are copyrighted and Bing
  shows a credit with each, so the picture's hover tooltip is that credit (e.g. "© Photographer/
  Agency"). Only a tooltip: nothing is drawn on the tile, no title, no setting, and the
  screen-reader name is unchanged ([S-2760]). `BingCredit` reads the reply's `copyright` field at
  the trust boundary: control and format characters stripped, white space collapsed and trimmed,
  only the "© …" part kept when there is one (Bing's field leads with the place), capped at 200
  characters without splitting a surrogate pair. A missing, blank or bare "©" field is no tooltip;
  the photo still shows. Local pictures (folder, file) have no tooltip. (Tests `S3112_`.)

## Weather: current conditions and forecast

Round 3 of `WeatherNow` and `WeatherForecast` (originals `weather-current`, `weather-forecast`).
Rules in `Themia.Core/Widgets` (`WeatherLayout`, `WeatherFacts`, `WeatherReport`, `WeatherData`) and
`Themia.Core/Storage/WeatherCacheStore`; tests in `WeatherLayoutTests`, `WeatherCacheStoreTests`,
`WeatherReportTests` (`S72x_`).

- **[S-720]** One open-meteo request per place carries the current block (temperature, feels-like,
  humidity, wind + direction + gusts, precipitation, pressure, cloud cover, UV), today and the next
  five days (high/low, sky, precipitation sum + max chance, max wind, max UV, sunrise, sunset),
  `timezone=auto`; no hourly block (nothing draws it). The forecast asks the same daily fields for
  1..14 days. Every value is range-checked; a missing daily block still yields a reading.
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
  at Huge both ways. No next-days strip and no next-24-hours chart at any size (retired, [S-666]:
  the hours and the days are the forecast widget's job); what the rungs leave is the reading's.
  "Show detail" off = the reading alone at every size.
- **[S-724]** Forecast layout setting `Layout` = Auto | Rows | Columns | Grid (default Auto: columns
  on a strip of width ≥ Regular, rows elsewhere); the user's choice always wins. Chosen in the settings
  only ([S-1746]).
- **[S-725]** Rows: stretched to fill the tile when every day fits at the row floor (40, or 32
  compact under a 200px list), capped at 80, else scrolling (hidden bar). Columns by width: icon +
  low ≥ 160, range bar ≥ 300, sky ≥ 380, droplet + amount ≥ 420, chance and max wind ≥ 500. A row
  ≥ 46 tall without a sky column puts the sky on a second line and the precipitation under the
  high. The rows are the widget at every size: no Today panel beside or above them (retired; it
  repeated the first row, concise widgets).
- **[S-726]** Columns: day + high always, then (by height) icon, low, a vertical range bar that takes
  the leftover, precipitation, sky (column ≥ 72), and at H ≥ Expanded the chance and the wind;
  scroll sideways below 56px a column. Grid: 1 card across ≤ 220, else 2..4; cards stretch to fill or
  scroll at 64; each card: day + icon, high/low, then bar, sky, precipitation "mm", chance · wind, sun.
- **[S-727]** Today is marked by an accent, semibold label and a quiet fill under its row, column or
  card. Precipitation is a droplet and the amount ("0.3"; "0.3 mm" in the grid), blank on a dry day.
- **[S-728]** `HideLocation` ("Location name" switch, both widgets): off leaves the header to the
  catalogue label; on, the title is the chosen place's city (first part of `LocationName`); before a
  pick there is none (the typed query is not kept, [S-2846]). A user label still wins.
- **[S-729]** The last good answers persist in the sealed `web` shelf of the offline store
  ([S-2660]; `schemaVersion` 1 inside, one writer, saved after each fetch that brought a new answer).
  At launch they are restored marked `Restored`: drawn at once with their real age in the caution
  colour ("Saved reading from 3 h ago; refreshing."), fresh only for that age ([S-2662]). A document
  of another schema or not JSON is refused whole (a damaged shelf is set aside, [S-2605]); entries
  older than 3 days, from the future, or without a temperature are dropped. A debug render never
  reads or writes it.
- **[S-730]** "Refresh" in both widgets' menus resets the back-off and bypasses cache freshness
  (`WebCache.GetAsync(force)`), still joining a request already in flight.
- **[S-731]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-732]** *(Retired by [S-2840]..[S-2846]: the two-row search, its separate results drop-down
  and the "Change" button that cleared the place are gone.)*
- **[S-2840]** The location is one settings row, `PlaceSearch` + `PlaceSearchBinding`, shared by both
  weather widgets (and Sun and Moon while they exist): title "Location", description the chosen place's
  label in full ("Zürich, Zurich, Switzerland") or "None chosen yet. Search for a city to set it.";
  the control is one search box (`AutoSuggestBox`, search glyph, "Search for a city", 300 wide). The
  box is only ever a question, never the choice: a pick stores coordinates + label, updates the
  description and empties the box. No clear and no Change mode (a weather widget without a place has
  nothing to show; a new one says so through its setup prompt). No "use my location": the app has
  no location source.
- **[S-2841]** Queries (`PlaceQuery`, Core): trimmed, runs of whitespace made one space, cut at 80.
  Typing searches 300 ms after the last keystroke once 2+ characters; Enter or the search glyph
  searches at once from 1 (津 is a city); text without a letter says "Type a place name" and asks
  nothing; emptying the box closes the list and cancels the search.
- **[S-2842]** Results: hits without a name, with a control character or overlong name, or without
  finite in-range coordinates are dropped (a bad region field is left out, not shown); the same
  place twice (name, region, county, country alike within 0.1°) is offered once; exact names first,
  then names starting with the query, then the geocoder's order (population); at most 6 of 10 asked.
  Each suggestion is the city over "region, country" (a region named like the city is not
  repeated); same-named places in one region add the county, and if that still does not tell them
  apart, " · lat, lon". The stored label is the city and that line.
- **[S-2843]** The list itself says the state, as one line that cannot be picked: "Searching…" with
  a ring; "No places match “x”. Check the spelling or try a nearby town."; offline (no network:
  "You're offline…", not an error); any other failure "The place search isn't answering…" in the
  critical colour, logged. Every fetch status maps to exactly one of these (`PlaceQuery.Outcome`).
- **[S-2844]** Keys: Down/Up move through the suggestions without committing or rewriting the box;
  Enter or a click picks the highlighted place; Enter with none highlighted picks the best match when
  the list answers what the box says, else searches now; Escape closes the list.
- **[S-2845]** One search at a time, a newer one cancels the older. Answers (places or no match) are
  kept for the session in a 32-entry LRU keyed by the query in any case and the app language;
  failures are not kept. The geocoder is asked in the app language (`language=`), through the
  guarded `WebFetch` and the source-generated JSON context only.
- **[S-2846]** The typed query is not stored: `WeatherConfig.Place` is gone (a half-typed word is not
  a place and must not title the widget).
- **[S-2847]** Debug renders: `--settings-png Widget:<Kind> … [--place-chosen] [--place-query text
  [--place-reply results|none|offline|error|searching]]` draws the row chosen or not, and the open
  list (`<out>-place.png`) against a fixture geocoder; nothing touches the network.
- **[S-733]** Reading glyphs, checked against the font: droplet EB42, thermometer E9CA, wind E7BC;
  layouts: list E8FD, columns E8C0, grid E8A9.

Not ported: wind in mph for °F (the original had none either); a gradient fill on the range bar
(solid accent, as every other bar in the app); the refresh stepper (fixed choices app-wide).

## Tasks: using the whole tile, and the rest of the original

- **[S-780]** Tasks ladder (`TaskLayout.Plan`, from the card's `WidgetArea`): one task always; the add
  row from Regular height; the list takes the rest (no summary strip, [S-783]).
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
- **[S-783]** *(Retired: no summary strip ("7 open · 2 overdue · …") on a panel; a larger tile shows
  more tasks, not counts of them. The header badge keeps the open count.)*
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
- **[S-1981]** Space ticks the focused task as Enter does, and Page Up / Page Down jump ten rows
  (`TaskLayout.Decide`, clamped; from no row, the first). All widget keys: Widget keys (WIDGET-KEYS).
- **[S-788]** Tab in a rename box commits and opens the next task's box (none past the last); in the
  add box it commits and keeps it open; Shift+Tab commits and closes (classic S-56).
- **[S-789]** Under Today on a panel each row names its list (the account's lists, cached).

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
`Shell/FluentLayout.cs`, `Shell/SettingRow.cs`, `Shell/ColorChoice.cs`, `Interop/ScreenColorPicker.cs`, `Shell/Config/ConfigWindow*.cs`,
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
- **[S-1556]** The eyedropper (`Interop/ScreenColorPicker.cs`, pure rules `Core/Settings/ScreenPick.cs`):
  wherever "Custom colour" is offered, an eyedropper button beside "View colours" closes the
  flyout, waits until the screen no longer shows it (the flyout's Closed, the next XAML frame, then
  `DwmFlush`; no timer), captures every display into one 32-bit DIB and covers them with a
  topmost popup showing that frozen picture. The picked pixel goes through the same path as a
  swatch, normalised to `#RRGGBB`. A failure to capture or show it is logged and raises an Error
  notice; nothing changes.
- **[S-1557]** The loupe: 11×11 pixels around the pointer, each drawn `Fluent.Space3` DIPs square,
  the centre pixel framed, its hex in Body Strong on a label filled with that colour (dark or
  light ink by luma), all framed dark-outside-light so it reads on any colour. It sits
  `Fluent.Space5` below-right of the pointer, flips left/up where it would leave the display, and
  is held inside the display under the pointer (never spilling onto the next one). Pixels outside
  the capture show black.
- **[S-1558]** Every display, mixed DPI: the process is per-monitor DPI aware (v2), so cursor,
  capture and overlay share physical pixels; the virtual screen is the union of the displays'
  bounds (left/above the primary included); a point in a gap belongs to the nearest display; the
  loupe is sized for the DPI of the display under the pointer (unknown DPI reads as 100 %). The
  overlay ignores `WM_DPICHANGED`. Arrow keys move the pointer one pixel (Shift: ten).
- **[S-1559]** A left click picks (on the release, so the click never reaches the window
  underneath); Enter or Space picks the pixel under the pointer; Escape, a right click (on release),
  another window taking the focus, `WM_CANCELMODE` or a display change cancels and keeps the old
  colour. One pick at a time. Every DC, bitmap, brush and font is a `SafeHandle`, released on every
  path; the window procedure catches, logs and ends the pick as a failure. Focus returns to the
  window that had it.
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
  clamps an out-of-range value and logs it. A new cadence is another hub series with its own history ([S-2504]), so the
  fixed-slot x-axis never mixes two spacings.
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

Power: one widget per action (R7-POWER), replacing the port of `main`'s Power tile
(`widgets/system/power`, [S-01]..[S-11]). Bluetooth: port of `main`'s Bluetooth
(`widgets/system/bluetooth`, [S-01]..[S-16]) plus the round-3 ladder. Core: `PowerActionRules`,
`PowerButton`, `PowerFace`, `BluetoothRules`, `BluetoothLayout`; App: `PowerButtonView`,
`Win32PowerActions`; tests `PowerActionTests`, `BluetoothLadderTests`.

- **[S-1910]** Each power action is its own widget kind and picker entry, with its own glyph, name and
  description: **Shut down** (`Shutdown`), **Restart**, **Sleep**, **Hibernate**, **Lock**, **Sign
  out** (`SignOut`) and **Turn off display** (`ScreenOff`), in the System section, default size
  120×120. The kind maps to its `PowerAction` in Core (`PowerActionRules.ForKind`/`KindOf`); one
  view (`PowerButtonView`) serves all seven. Each kind has an empty config of its own
  (`ShutdownConfig` … `ScreenOffConfig`, base `PowerButtonConfig`): nothing to set beyond the card's
  common settings, so the settings page shows only those, and a power widget never needs setting up.
  There is no combined power tile, no power-state strip (source, plan, battery saver, time left), no
  power plans or timeouts, and no power-state sampling. A state file with the old `power` kind fails
  load validation like any unknown kind ([S-1050]).
- **[S-1911]** The whole widget is the button. A new power widget has no header ([S-700]); the icon is
  centred with the name under it. Hover and pressed paint Windows 11's subtle fills
  (`SubtleFillColorSecondary`/`Tertiary`) across the whole content area, inside the card's own fill,
  border and corners; a click is press and release on the tile (sliding off cancels). It is not a
  `Button`: in edit mode the press is left to the card, which selects and moves the widget, so a
  click there never runs anything (`IWidgetSurface.EditMode`).
- **[S-1912]** Ladder (`PowerFace`): Compact width (XS) is the icon alone; from Regular width the name
  sits under it on a ramp step fitted to the width (Body to Title, about a sixth of the height). The
  icon's square (icon plus the arm ring) takes the rest, 24–192 DIPs, the icon two thirds of it. The
  label slot is planned for the taller of the name and the armed sentence, so arming moves nothing.
- **[S-1913]** Shut down, restart, sign out and hibernate arm on the first click: the tile turns to
  the critical background, the icon and a draining ring in the critical colour, and the name becomes
  "Click again to shut down" (wrapping to two Caption lines where one does not fit; the tooltip
  carries it at XS). The second click within 5 s runs it; a second click within 400 ms of arming is
  ignored (a double-click is one intent); the arm ends after 5 s, on pointer exit, on a config change
  or when the widget goes away. Sleep, lock and display-off run on one click. The window is checked by
  `PowerButton` against the press time; the ring's 50 ms repaint is cosmetic.
- **[S-1914]** Hibernate is offered only where the machine can (`IsPwrHibernateAllowed`): elsewhere
  the widget is drawn in the disabled text colour, takes no hover or press, and its tooltip says
  "Hibernation is turned off on this PC". The probe runs when the widget attaches and when the
  pointer comes onto it, so turning hibernation on shows up without polling.
- **[S-1915]** A failed action shows the warning glyph and "Could not <verb>: <reason>" in the
  critical colour in the label's place (tooltip at XS) until the next press, and logs a warning
  ([S-1246]).
- **[S-1916]** One run at a time per widget: a press while the action is still running (sleep runs
  until the machine resumes) does nothing.
- **[S-1917]** Debug: `--widget-png <Kind> <out.png> [w h] [--theme light|dark] [--state name]`
  draws one widget in a real card on a preview host ([S-1251]), off-screen, at the seven tiers of
  `WidgetTiers` (or once at `w h`), labelled. `--state` asks a view that implements
  `IWidgetPreviewState` for an interaction state a render cannot click into — for the power widgets
  `hover`, `pressed`, `armed`, `failed`, `unavailable`. States, not sample data ([S-1863]).
- **[S-1918]** The seven power widgets are their own picker section, Power, after System.
- **[S-1240]** *(Retired: one widget per action, [S-1910].)*
- **[S-1241]** *(Retired: `PowerAction` is no longer persisted; the kind is the action, [S-1910].)*
- **[S-1242]** *(Retired: no action list, [S-1910].)*
- **[S-1243]** Win32 only, no `shutdown.exe` (orig. [S-03]/[S-09]): `ExitWindowsEx`
  (`EWX_POWEROFF`/`EWX_REBOOT`/`EWX_LOGOFF` | `EWX_FORCEIFHUNG`, planned reason), `SetSuspendState(h, 0, 0)`,
  `LockWorkStation`, `SC_MONITORPOWER` broadcast via `SendMessageTimeout`. `SeShutdownPrivilege` is
  enabled per call for shut down, restart, sleep and hibernate; `ERROR_NOT_ALL_ASSIGNED` after a
  "successful" `AdjustTokenPrivileges` is "not allowed". The token is a `SafeHandle`. The plan per
  action is decided in Core (`PowerActionRules.Plan`); `Win32PowerActions` (an `IPowerBackend`) only
  executes it, on a worker thread (sleep blocks until resume, orig. [S-04]).
- **[S-1244]** *(Retired: the name shows by the ladder, [S-1912].)*
- **[S-1245]** *(Retired: which actions arm, and that they always do, is [S-1913].)*
- **[S-1246]** A failed action says "Could not <verb>: <reason>" on the widget in the critical colour
  ([S-1915]) and logs a warning (WinUI has no toast pipeline; orig. [S-07]).
- **[S-1247]** *(Retired: one widget, one arm, [S-1913].)*
- **[S-1248]** *(Retired: one button per widget, [S-1912].)*
- **[S-1249]** *(Retired: no status panel, [S-1910].)*
- **[S-1250]** *(Retired: no status strip, [S-1910].)*
- **[S-1251]** A debug render's cards are previews (`IWidgetSurface.IsPreview`, from the render's
  card host): a power widget's backend is `InertPowerActions` there, which logs and runs nothing (it
  still answers the hibernate probe truthfully). Only a card on a non-preview surface gets
  `Win32PowerActions` (orig. [S-08] demo mode).
- **[S-1252]** *(Retired: a power widget always has its action, [S-1910].)*
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
  and add row → Wide + run in words and a 7-period rate per row → L all of that → XL + best run
  per row. Rows are 40px (32 on a Compact side); the viewport holds whole rows and the rest scroll.
  The height a short list leaves stays empty: no summary strip or line ("3 of 5 done today · 71%",
  bar) and no completion chart fills it (user decision, concise widgets [S-666]; both retired).
- **[S-1428]** Keyboard (original S-17): a click in the widget takes the shell's keyboard lease
  ([S-809]) and a clicked row takes the focus:
  Up/Down clamp, Home/End, Tab/Shift+Tab wrap, Enter ticks, F2 renames, Delete deletes, Escape gives
  the keyboard back. In an editor Tab commits and opens the next row's editor, falling into the add
  row past the last. Not verified live: needs a manual pass on the desktop.
- **[S-1982]** Space ticks the focused habit as Enter does, and Page Up / Page Down jump ten rows
  (`HabitLayout.Decide`, clamped; from no row, the first).
- **[S-1429]** Settings: the list picker/editor (pick, rename as typed, habit count, add, delete
  — always confirmed, empty or not, original S-24), Resets, Interval (custom only), Show streaks,
  History. Renaming a list retitles every widget showing it.
- Not ported: original S-10 compact header default (no compact header mode here), S-12 equal-slice
  rows (Fluent fixed rows, S-213), S-13 drag reorder (above), S-21 legacy migration (no backwards
  compatibility).

## Calendar and contacts: the whole tile

The Calendar's layouts and the Contacts ladder ([S-660] API), the Outlook hand-off and the key maps.
Rules in `Themia.Core/Office/CalendarFit.cs`, `CalendarRules.cs`, `CalendarLinks.cs`, `ContactRules.cs`
(tests `CalendarFitTests`, `CalendarRulesTests`, `ContactsFitTests`); views `CalendarView*.cs` (one
partial per layout), `CalendarData.cs` (the data seam), `ContactsView.cs`.

- **[S-1260]** *(Retired: the size ladder that swapped layouts, [S-2700].)*
- **[S-1261]** *(Retired: the XS "next event" tile, [S-2700].)*
- **[S-1262]** *(Retired: the strip of days on a wide tile, [S-2700].)*
- **[S-1263]** *(Retired: the month with the agenda below or beside it, [S-2700].)*
- **[S-1264]** Month: weekday names ("Mon") from 64 px columns; chips lead with their start time from
  100 px cells (classic S-44); chips get a 20 px line in cells ≥90 tall; only the weeks holding a day
  of the month are drawn (4–6 rows) at every size.
- **[S-1265]** Week columns ≥64 px spell the weekday; ≥100 px a timed chip shows start–end and its
  location under the title.
- **[S-1266]** *(Retired: 32/40 px hours on a scrolled timeline, [S-2705].)* A timeline block of three
  caption lines adds a "location · organizer" line; one of a single line keeps the title only.
- **[S-1267]** Agenda: under 128 tall the first page's "Today" heading is dropped (any other day keeps
  its heading); at ≥480 px a row puts location and organizer in columns beside the title; at ≥720 px
  each page fills two columns, one after the other.
- **[S-1268]** Opening an event: a Microsoft account's event opens `ms-outlook://events/{id}` when
  Outlook's protocol is registered (one cached check shared with Mail, `ShellLauncher.OutlookInstalled`),
  else the provider's https page; the provider comes from the event's account, never the link's
  domain. Only https (no user info) and `ms-outlook://events/<percent-encoded>` pass
  `CalendarLinks.IsLaunchable`, re-checked by `ShellLauncher.OpenCalendarLink`.
- **[S-1269]** Keys (classic S-31..34) reach `CalendarView.HandleKey(WidgetKey)` through the lease
  ([S-806]); the map is [S-2710]. Up/Down move the cursor day a week in the month (outlined) and the
  week (its column head underlined); Escape hides it.
- **[S-1984]** *(Retired: Page Up / Page Down page every layout, [S-2710].)*
- **[S-2700]** The calendar draws only its configured `layout` (month, week, day, year or agenda),
  at every size, chosen in its settings. No size swaps it for another layout, adds an agenda beside a
  month, or shows a single "next event" instead: each layout sheds detail within itself
  (`CalendarFit`). Unmeasured, it draws the layout at its smallest.
- **[S-2701]** The view reads its data through one seam, `ICalendarData.LoadAsync(config, first,
  last)`, for exactly the range its layout covers ([S-568]): the live one wraps the shared office
  source; the render's fixture is another ([S-2711]).
- **[S-2702]** Month (`CalendarFit.Month`): weekday header as names (≥64 px columns), two-letter
  initials (≥22), one letter (≥10), and dropped when the cells would be shorter than a number's line
  under it; then cells of chips (≥40 × 34), dots (≥24 tall) or the number alone, with its dots beside it from 48 px columns (Wide). XS: numbers only, their line box shrunk to the row;
  S: initials over numbers; M: one chip line; Tall: dots; L: three chip lines; XL: four, with times.
- **[S-2703]** Bars (`CalendarFit.Bars`): in a month row every item is a one-line chip, and in the
  week's band every all-day or multi-day event and birthday; an event over several days is one bar
  across them, squared off where it runs past the row. Lines are packed from the first column, the
  longest first; what a day's lines cannot hold is its "+N". A timed event ending at midnight is
  one day.
- **[S-2704]** Week (`CalendarFit.Week`): columns under weekday and number (never without one letter
  of the weekday); a band of bars (up to two lines, three on the hour grid, always leaving a line for
  the body); then, from 64 px columns and 280 px tall (L, XL), an hour grid with each day's timed
  events in overlap lanes; else each column's timed events as chips (time over title from 64 px,
  title alone from 40 px), then "+N"; under 40 px one dot per event and a muted one for the rest.
- **[S-2705]** Hours (`CalendarFit.Hours`): the day wanted is 07–22, widened to every timed event of
  the day(s) drawn. When it fits at ≥20 px an hour it is drawn whole, a tall box widening it toward
  midnight rather than stretch an hour past 48 px; else as many whole hours as fit, starting an hour
  before the focus (today: the next event still to end, else now; another day: its first event, else 08:00). Hours fill
  the box exactly; hour labels read "07:00", or "07" in a day under 160 px. Today has an accent
  now-line. Events outside the window are on the day's hover.
- **[S-2706]** Day: the all-day bar shares its width in chips of at least 56 px, the rest "+N" (on
  hover); a day with nothing says "No events today".
- **[S-2707]** Year (`CalendarFit.Year`): twelve small months in the arrangement of 4×3, 3×4, 6×2,
  2×6, 12×1 or 1×12 with the largest day cell, preferring cells that hold numbers (a caption line)
  and then a weekday row; under 4 px it falls back to 6, 4, 3, 2 or 1 months around the current one
  (`FirstMonth`, inside the year). Number cells: today the accent disc, a day with something a strong
  number over a stripe of its first item's colour. Square cells: filled with that colour, today
  ringed. Every day's items on hover. XS shows three months, S six, the rest twelve.
- **[S-2708]** Month names in the year follow the small month's width: "September", "Sep", "S"
  (`LocaleFormat.MonthName`); the current month's in the accent.
- **[S-2709]** Agenda pages (`CalendarFit.AgendaPages`): whole rows of one height per render, a day's
  heading before its first row and again atop a column that starts mid-day; pages fill the columns in
  turn and always advance. Back / forward show only where there is a page; Home is the first.
- **[S-2710]** Key map (`CalendarFit.Key`), every layout: Left / Right and Page Up / Page Down step
  the layout's unit (month, week, day, year, agenda page); Up / Down the cursor day a week (month,
  week); Home today; Enter the cursor day's first event (the agenda page's first; none in the year).
  The header's arrows hide on a Compact-width tile (the keys still page).
- **[S-2711]** Debug renders: `--widget-png Calendar <out> --state month|week|day|year|agenda` draws
  that layout over a fixed sample (two calendars, an all-day event, a multi-day trip, an overnight
  event, overlaps, birthdays, a year of meetings) through a fixture `ICalendarData`; a card that
  needs setting up runs its view for the preview (`WidgetCard.RunForPreview`).- **[S-1270]** Contacts list rows say what the list's area allows (`ContactRules.ListDetail`, on the
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
- **[S-1274]** *(Retired: no detail pane beside the list on an XL tile. The list keeps the whole
  width; a person's actions (Send email, Call, Message, Copy …) are the row menu's, [S-1277].)*
- **[S-1275]** Search box: always from 220 px tall; from 128 a header search button opens it (it folds
  away again when left empty); below 128 none. A typed query keeps the box open.
- **[S-1276]** The People count is a number box (`FluentLayout.Stepper`): at least 5, steps of 5, no
  upper bound (S-583); text that is not a number is overwritten with the last good value.
- **[S-1277]** Keys (classic S-31/32), `ContactsView.HandleKey`: Up/Down move the selection through the
  people on screen (from none, Down picks the first, Up the last), Home/End jump, Escape clears, Enter
  writes (else calls). A click anywhere else — another widget, the desktop, another app — ends the
  keyboard lease the widget holds from the selecting click, which arrives as Escape and clears the
  selection (classic S-38; [S-807]).
- **[S-1983]** Page Up / Page Down move the selection ten people (`ContactRules.MoveSelection`), and
  type-ahead ([S-1980]) over the names on screen selects the person typed.
- **[S-1278]** vCard 2.1 quoted-printable: `ENCODING=QUOTED-PRINTABLE` (or the bare parameter) values
  are decoded in UTF-8 or Latin-1 (`CHARSET`), across `=` soft line breaks; bare 2.1 parameters join
  `TYPE` (so `TEL;CELL;PREF` is preferred). Lenient: a stray `=` stays.
- **[S-1279]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

## Mail: the whole tile

- **[S-1280]** Mail's ladder (`EmailView.Lay`, rules in `MailLayout`): the first row always; the row
  view's column titles from 140px of height; then as many whole rows as fit. Stacked rows shed account
  tag, preview, extra preview lines and time as before ([S-556]); a wide strip and a big tile go one
  line per message ([S-1283]); the envelope joins from 300px ([S-1282]); the largest tile adds the
  quick actions ([S-1284]). No provider name and no folder summary ([S-1285], retired).
- **[S-1281]** Bottom scroll fade, shared (`Widgets/ScrollFade` + Core `ListFade`): while the list
  continues below the viewport, rows within 48px of its bottom edge fade to 0.35 opacity (the Tasks
  depth and floor, [S-781]); scrolled to the end every row is whole. Opacity per row, never a
  gradient over the list (the card can be transparent). Event-driven: scroll, size, one layout pass
  after a rebuild. Used by Mail.
- **[S-1282]** Envelope: a closed envelope (accent) in front of unread mail, an open one (tertiary)
  in front of read mail, from 300px wide, in every view; the unread accent bar stays.
- **[S-1283]** One-line rows (list and compact views) from 640px wide, or from 420px on a strip under
  220px tall: sender column (24% of the line, 96-200px), subject with the preview after an en dash
  (preview from 440px), account from 560px, time. The row view is unchanged. A one-line row shows
  one preview line whatever `PreviewLines` says (that setting shapes the stacked rows).
- **[S-1284]** The account tag and the tooltip name the account alone at every size (the provider
  name, "Work (Microsoft)", is retired, [S-666]). On one-line rows from 720px the pointer swaps a
  row's time for quick actions: mark read or unread, archive (not in Junk), delete — only where the
  account allows changes ([S-544]).
- **[S-1285]** *(Retired: no folder summary ("4 unread of 1,661 in Inbox") under a large tile's
  list; the large tile shows more messages instead. The header badge keeps the unread count.)*
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
- **[S-1295]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported, deliberately: main's low-level keyboard hook for list focus (the lease replaces it).

## Clipboard and Countdown: the whole tile

Code: `Widgets/ClipboardView.cs`, `Widgets/CountdownView.cs`,
`Widgets/ItemMenu.cs` (submenus), `Shell/Config/WidgetSettings.{Clipboard,Countdown}.cs`,
Core `Widgets/ClipboardLayout.cs`, `ClipboardHistory.cs`,
`CountdownLayout.cs`, `ItemCursor.cs`,
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
  → L/XL 3 lines + detail; no count footer (concise: the entries only). A preview card never
  subscribes to the real clipboard.
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
- **[S-1456]** `ItemMenuEntry.Children` makes an entry a submenu (`MenuFlyoutSubItem`), separators
  collapse inside it as at the top level.
- **[S-1457]** `ItemCursor.Move`: the shared list cursor (Up/Left back, Down/Right forward, clamped;
  Home/End; Escape clears; no cursor + forward = first, + back = last).
- **[S-1985]** `ItemCursor.Move` also pages: Page Up / Page Down jump ten items (`ListKeys.PageStep`),
  clamped; with no cursor they land on the first. RSS and Screen time move with it.
- **[S-1986]** Countdown keys (`CountdownView : IWidgetKeys`, `ListKeys`): taken only while timers
  are listed under the hero. Up/Down, Page Up/Down, Home/End move a `RowFocus` ring over the listed
  timers (the hero is not a row), Delete removes the timer after the same confirmation as its menu,
  the Menu key opens its menu, Escape lets go. A removed timer hands the ring to the row that took
  its place. Enter has nothing to open.

Not ported, deliberately: clipboard pin (an optional extra the audit listed, not in the original);
Countdown C-61 keep-open (above). Countdown keys came later although the original had none ([S-1986]).

## CPU

- **[S-1140]** CPU: percent of all cores (`GetSystemTimes` delta, shared per tick [S-643]); the reading
  and its history only ([S-1762]). The clock, per-core strip, sensors, totals and top processes of the
  earlier ladder ([S-1141], [S-1142], [S-1146]..[S-1150]) are removed with their settings.
- **[S-1143]** Uptime wording (`CpuRules.Uptime`, used by the power tile), two largest units:
  `3d 4h`, `2h 5m`, `7m`.
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
  and each display's group has: Show Themia on this display, Shows (view) and Widget scale, then
  its view bar ([S-916]). The group is headed by the monitor's name ("(primary)" on the primary)
  over its resolution; the id is never shown. No Resolution row ([S-1709]).

Not ported, deliberately: the original's legacy ordinal-key migration ([MI-03]/[MI-04]; no
compatibility, by decision); its ~1 s work-area self-heal sweep ([DW-04]), which polls, replaced by
[S-848]'s messages plus [S-849]'s check on every pass; its adoption sequence counter ([MI-07]): the
pass runs synchronously on the UI thread, so there is no await for an older adoption to win across.

## Notes: note store and markdown editor

Code: Core `Notes/NoteStore.cs`, `Notes/NoteMarkdown.cs`, `Notes/NoteEditing.cs`,
`Notes/NoteHistory.cs`, `Notes/NoteText.cs`, `Notes/NotesLayout.cs`; App `Services/Notes/NotePool.cs`,
`Widgets/MarkdownEditor.cs`, `Widgets/NotesView.cs` (+ `.Picker.cs`), `Shell/Config/WidgetSettings.Notes.cs`,
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
  pick another (settings, or the header's picker); it is never handed another note, and a pending edit to
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
  height Expanded (with detail at width Expanded); the text takes the rest. The widget is the one
  note at every size: there is no note list beside the text, at any size (user decision; the
  header's picker switches notes).
- **[S-1364]** The header title is the note's title (the user's own widget label still wins). A
  chevron in the header opens the note picker (every note, New note, Rename…, Delete note…), except at
  XS (it would push the title out).
- **[S-1365]** The bar's buttons never take focus, so a command runs on the note's selection. Bold,
  italic, strikethrough, bulleted list and checklist are Segoe Fluent glyphs checked against the font;
  heading and numbered list are lettered ("H", "1.") because the font has neither.
- **[S-1366]** The editor admits no formatting the markdown does not hold: rich-text accelerators off,
  every Ctrl chord other than clipboard, selection and caret keys swallowed (Ctrl+Alt passes: AltGr),
  no selection flyout, its own context menu (undo, redo, cut, copy, paste, select all), copy as plain
  text. Keys are not intercepted while an IME composition is open.
- **[S-1367]** *(Retired: the widget never shows a note list beside the note, [S-1363]; the header
  picker of [S-1364] picks, creates, renames and deletes.)*
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
  themia-releases#49). The query is held while the hub's disk feed lives and closed with it ([S-2505]). Volumes and labels are re-walked at most every 30 s (original [S-21]).
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
  presses too) asks for it: a view implementing `IWidgetKeys`
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
- **[S-811]** Pictures (Gallery widget) slideshow: Left / Right step, Space pauses (the original's S-29). Other
  layouts take no keys.
- **[S-812]** Tasks and Habits with the lease but no row focused: a movement key lands on an end
  (Up/Down/Home/Tab the first, End/Shift+Tab the last); Enter, Delete and F2 do nothing.

Not wired here (lanes still running; see the WP11 report): Mail (`ListKey` handlers), Clipboard
(`HandleKey(WidgetKey)` ready), Notes' list and Network drives.
Not live-verified: focus taking, Alt+Tab, deactivation timing, IME (see the report).

Key routing was fixed after this section was written: the holder now gets its keys in the tunnelling
pass, before a `ScrollViewer` can take them ([S-860]).

## Widget keys (WIDGET-KEYS)

Every widget view's keys in one place. The maps are Core (`ListKeys`, `ItemCursor`, `TaskLayout`,
`HabitLayout`, `CalendarFit.Key`, `ContactRules.MoveSelection`, `AudioRules`, `ControlKeys`,
`TypeAhead`; tests `WidgetKeyMapTests` and the per-widget suites); the views implement
`IWidgetKeys` and only carry the answer out. Keys arrive through the lease ([S-800]..[S-812], [S-860]).

- **[S-1980]** Type-ahead (`TypeAhead`): characters typed within a second of each other build one
  prefix; the cursor goes to the first name starting with it, searching from the cursor itself and
  wrapping, ignoring case. One letter repeated ("ss") steps through the names starting with it. A
  control character never types; a space only inside a prefix (a lone Space is the widget's key). No
  match leaves the cursor, and the character is still used. Escape (every lease end) forgets it.
- **[S-1996]** Widgets deliberately without keys: Clock, Sun, Moon, Weather now and forecast, Stocks
  (nothing to navigate or act on), Scratchpad (only typed into: `RequestTextInput`), the seven power
  buttons (a single deliberate press; a key that shuts the PC down after a stray click is not
  calm), and the meters (their own lane, [S-1564]). A click on them never takes the keyboard.
- **[S-1997]** Characters reach the holder through `IWidgetKeys.HandleText(char)` (default: unused),
  from the surface's `CharacterReceived` outside edit mode, on the holder's surface only, never while
  a text box has focus and never for a Ctrl or Alt chord (AltGr, which is both, types). A letter that
  is a widget key (M) reaches `HandleKey` first, then `HandleText`.

| Widget | Keys |
|---|---|
| Mail | Up/Down, PgUp/PgDn, Home/End move; Enter opens; Delete (where allowed); Menu; Esc ([S-1288]) |
| Calendar | Left/Right and PgUp/PgDn page by the layout's unit (month, week, day, year, agenda page); Up/Down a week (month, week); Home today; Enter first event ([S-1269], [S-2710]) |
| Contacts | Up/Down, PgUp/PgDn, Home/End; Enter writes or calls; type-ahead ([S-1277], [S-1983]) |
| Tasks | Up/Down, PgUp/PgDn, Home/End; Enter/Space tick; Delete; F2; Esc ([S-787], [S-1981]) |
| Habits | as Tasks, plus Tab/Shift+Tab wrap ([S-1428], [S-1982]) |
| Folder | arrows (grid aware), PgUp/PgDn, Home/End, Shift extends; Enter; Space; Delete; F2; Menu; Ctrl+C/X/V/A; Backspace; type-ahead ([S-1327], [S-1993]) |
| Shortcuts | arrows, Home/End, PgUp/PgDn; Enter opens; Space toggles; Delete; F2; Menu; type-ahead ([S-1390], [S-1995]) |
| RSS | Up/Down/Left/Right, PgUp/PgDn, Home/End; Enter opens; Menu ([S-1306], [S-1985]) |
| Clipboard | Up/Down, PgUp/PgDn, Home/End; Enter copies; Delete; Menu ([S-1444]) |
| Countdown | Up/Down, PgUp/PgDn, Home/End; Delete (asks); Menu ([S-1986]) |
| Pictures (slideshow) | Left/Right; Home/End; Space pauses; Enter opens ([S-811], [S-1992]) |
| Mixer | Up/Down/Home/End rows; Left/Right, +/- step; PgUp/PgDn step 10; M/Space mute; Menu ([S-1225]) |
| Volume | arrows, +/- step; PgUp/PgDn step 10; Home/End 0/100; M/Space mute ([S-1226], [S-2733]) |
| Music | Space/Enter play-pause; Left/Right track; Shift+Left/Right seek 5 s ([S-1468]) |
| Pomodoro | Space/Enter start-pause; Right skip; Home reset ([S-1987]) |
| Screen time | Up/Down, PgUp/PgDn, Home/End light an app; Menu; Left/Right day ([S-1989]) |
| Quote | Right/Space/Enter next; Left previous ([S-1990]) |
| Video stream | Space pause ([S-1991]) |

## Keyboard in the shell (SHELL-KEYS)

Keys everywhere outside a widget's own view: edit mode on the desktop surface, the view bar, the
settings window. Rules in Core `EditKeys`, `KeyboardNudge`, `SelectionRules.Cycle`, `SettingsKeys`
and the widgets' `TypeAhead` ([S-1980], one implementation for lists and the navigation; tests `ShellKeyboardTests`); carried out by `DesktopWindow.Keys.cs`,
`ViewBar.cs` and `ConfigWindow.Keys.cs`. Widget views' keys are the lease's ([S-800]..[S-812]).
The surface keeps `WS_EX_NOACTIVATE` outside edit mode ([S-20]), so none of the surface keys exist
there: Themia never takes a key from the app the user is typing in.

| Where | Key | Does |
|---|---|---|
| Anywhere | Ctrl+Alt+W | Toggle edit mode ([S-65], global hotkey) |
| Edit mode | Arrows | Move the selection one grid cell ([S-1970]) |
| Edit mode | Ctrl+Arrows | Move ten cells ([S-1972]) |
| Edit mode | Alt+Arrows | Move one DIP, off the grid ([S-1972]) |
| Edit mode | Shift+Arrows (+Ctrl / +Alt) | Resize by the right / bottom edge, same steps ([S-1971]) |
| Edit mode | Tab / Shift+Tab | Select the next / previous widget ([S-1973]) |
| Edit mode | Enter | Open the selected widget's settings ([S-1974]) |
| Edit mode | Menu key / Shift+F10 | The selection's menu, or the surface's ([S-1974]) |
| Edit mode | F6 / Shift+F6 | Widgets → toolbar → view bar → notices ([S-1975]) |
| Edit mode | Delete | Delete the selection, undoable ([S-1592], [S-885]) |
| Edit mode | Ctrl+C / X / V / D | Copy, cut, paste, duplicate ([S-70]) |
| Edit mode | Ctrl+A | Select all on the display under the pointer ([S-1582]) |
| Edit mode | Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z | Undo / redo ([S-885]) |
| Edit mode | Ctrl+1..9 | Show the n-th view ([S-904]) |
| Edit mode | Escape | Placing, then the selection, then edit mode ([S-1583]) |
| View bar (edit mode) | Arrows, Home / End; Enter / Space | Move between chips; show the view ([S-1979]) |
| Settings | Ctrl+F / Ctrl+E | Focus "Find a setting" ([S-1977]) |
| Settings | Escape in the search box | Close the list, then empty the box ([S-1977]) |
| Settings | F6 / Shift+F6 | Title bar → navigation → page ([S-1977]) |
| Settings | Alt+Left / Alt+Right, mouse back / forward | Page history (existing) |
| Settings | Ctrl+W | Close the window ([S-1977]) |
| Settings navigation | Up / Down, Enter / Space | Move, open (NavigationView's own) |
| Settings navigation | Home / End, letters | First / last entry, type-ahead ([S-1978]) |
| Widget picker | Up / Down, Enter, Escape | Move the highlight, pick, close ([S-949]) |
| Menus, flyouts, dialogs | Arrows, Enter, Escape | WinUI's own: Escape closes the innermost |

- **[S-1970]** In edit mode an arrow moves **every selected widget on this display** one snap cell
  ([S-06b]) as one block, by the drag's rules ([S-1584]): the block's corner snapped when the grid
  is on, the block slid into the work area, never pulled apart at an edge. An off-grid widget lands
  on the next line in the arrow's direction, never back where it was. Nothing selected, or a ghost
  being placed: the arrows do nothing.
- **[S-1971]** **Shift** + arrow resizes instead, each selected widget on its own, always by its
  **right or bottom edge** (Right / Down grow, Left / Up shrink), so the top-left corner stays put.
  Only the moved edge snaps ([S-31a]); the [S-08] floor and the work-area edge stop it. Shift was
  chosen over a bigger step because one modifier per meaning (Ctrl = far, Alt = fine) combines.
- **[S-1972]** **Ctrl** makes the step ten cells, **Alt** one DIP with the grid suspended (as Alt
  suspends it for a drag, [S-06]). Ctrl+Alt is never a shortcut (AltGr). One press-and-hold is
  **one undo step**: the geometry is committed when the arrow comes up, before any other edit key,
  and when the surface loses activation.
- **[S-1973]** **Tab / Shift+Tab** selects the next / previous widget of the display in reading
  order (top edge, then left, then view order), wrapping, skipping cards that draw nothing; from no
  selection Tab lands on the first and Shift+Tab on the last. The card takes the keyboard focus, so
  the selection outline is the focus indicator. With focus on the toolbar, the view bar or a notice,
  Tab, the arrows, Enter and the Menu key are those controls' own.
- **[S-1974]** **Enter** opens the selected (primary) widget's settings page; the **Menu key** or
  **Shift+F10** opens the selection's card menu on its card ([S-1743]), or the surface menu
  ([S-1744]) at the work area's centre when nothing is selected.
- **[S-1975]** **F6 / Shift+F6** moves the keyboard round the surface's panes: the widgets (the
  selected card, else the surface), the toolbar's first button, the view bar (when it shows) and the
  notices (while one has a button). This is how the toolbar is reached without the mouse.
- **[S-1976]** One key map: `EditKeys.Decide` turns a key and its modifiers into a command and the
  surface only carries it out; the old per-key switch is gone. Ctrl+Shift letters, Shift+Delete,
  plain letters and F10 alone are not edit keys. While a text box has the focus (the picker's
  search) no key is an edit key: Delete or Ctrl+V there edits the text, not the desktop.
- **[S-1977]** Settings window: **Ctrl+F** (and Ctrl+E, as in Explorer) focuses "Find a setting";
  **Escape** there closes the suggestion list (the box's own), then empties the text; **F6 /
  Shift+F6** cycles the title bar (search), the navigation (the entry of the page on screen) and the
  page (its first control), skipping the navigation while it is folded away; **Ctrl+W** closes the
  window. Escape does **not** close the settings window: it is a window, not a dialog (as in main
  and in Windows Settings).
- **[S-1978]** Navigation type-ahead (main's `createTypeAhead` / `findByPrefix`): letters typed on
  a focused entry within a second of each other build a prefix, case-insensitive, and the focus
  moves to the first entry starting with it, searching from the focused one (a longer prefix keeps
  the entry while it still matches); one letter, or the same letter again, steps to the next entry
  with that initial, wrapping. Read from the typed character, so every layout works. Home / End go
  to the first / last entry. Focus moves, nothing opens: Enter opens, as NavigationView does.
- **[S-1979]** Focus and names: view-bar chips are focusable in edit mode with the system focus
  ring, the arrows rove between them, Enter or Space shows the view and the focus stays on its chip
  through the rebuild. Every icon-only button has an `AutomationProperties.Name` (the list commands
  of `FluentLayout.IconCommand` take their tooltip). No `AccessKey`s on the surface: Alt is the
  snap override there and would open key tips mid-drag.

Not built: placing a new widget from the keyboard (the placement ghost follows the pointer,
[S-1587]); the view bar outside edit mode (the surface has no keyboard then). Not live-verified:
focus after F6, focus rings, the foreground handling of edit mode, Narrator (see the report).

## Battery and network

Package SYS-P3. Both meters live in their own files (`Widgets/Meters/BatteryMeter.cs`,
`NetworkMeter.cs`); the rules are Core (`BatteryRules`, `NetworkRules`, tests in
`BatteryNetworkTests`). Like every meter they show the label, the reading and the history only
([S-1762]).

- **[S-1180]** Battery: charge from `GetSystemPowerStatus`; charging is the `BatteryFlag` charging bit,
  not the plug. The no-battery decision is `BatteryRules.Read`'s (bit 7 of a known flag with no
  usable percentage; 255 is "unknown", not "none"); a machine with no battery shows an empty state.
- **[S-1186]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1187]** The battery reading leads with the Segoe Fluent battery glyph filled to the nearest
  tenth, with a bolt while charging, in the pressure colour (`MeterPart.Badge`).
- **[S-1190]** Network throughput is per adapter (`ThroughputTracker`): a counter that falls is that
  adapter's reset (reads 0 once, re-baselines) and no longer zeroes every other adapter's traffic;
  a new adapter starts from a baseline. Read by the hub once per tick for every tile ([S-2501]).
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
  appears from the bottom: for a day, the week strip. Nothing else (the stats line and the daily
  limit bar are retired: the limit is the headline's "of 4h" already, [S-457]; concise widgets).
- **[S-1214]** Week strip (Day period, not style None): the seven days ending on the shown day, one
  bar each scaled to the busiest, the shown day in the accent, weekday under each; a click shows that
  day (today unpins) within the navigation's reach.
- **[S-1215]** *(Retired with the stats line, [S-1213].)*
- **[S-1216]** Screen time lays out from the card's `WidgetArea` (`IWidgetSizeAware`), not its own
  `SizeChanged`; a debug render never hooks or writes ([S-459]).
- Not ported / different: the original's GPU `visibleMetrics` order (fixed ladder order); per-engine
  history charts (one chart, the load).

## System data hub (SYS-HUB)

Every system widget — the meters (CPU, memory, disk, network, battery, ping), the GPU tile and the
detail widgets — reads through one hub that lives as long as the app. Owning files:
`Themia.App/Services/SystemData/*` (`SystemHub*.cs`, `HubSources.cs`, `HubSource.cs`, `WatchSlot.cs`),
Core `Themia.Core/SystemData/*` (`FeedKey`, `HubCadence`, `FeedTable`, `SampleRing`, `SeriesPoint`).
Tests `SystemDataHubTests` (`S25xx_`).

- **[S-2500]** One hub, `AppServices.System`, made by the host on the shared ticker and closed with it
  (a debug render makes its own). A view watches in Attach (a parked view watches nothing, [S-1881])
  and lets go in Detach; no view reads a counter, walks a list or sends a probe. The samplers
  (`SystemMetrics`, `MemoryCounters`, `PerformanceInfo`, `ProcessorInfo`, `ProcessList`, `DiskMetrics`,
  `NetworkThroughput`, `LocalAddresses`, `PublicAddress`, `BatteryDevice`, `GpuSampler`,
  `NetworkMetrics.PingAsync`) are read by the hub alone, so none keeps a same-tick cache of its own.
- **[S-2501]** A source (`SystemSource`; the ping source per host) is read **once per due tick**,
  however many widgets show it, and every delta (CPU load, throughput, process shares) has exactly one
  reader. Sources are read in `SystemSource` order, so whatever a widget combines comes first: CPU
  details' load and counts before the processor, network details' addresses before the throughput,
  the battery's charge before the pack.
- **[S-2502]** A feed is a source, a target (the ping host, trimmed and lower-cased; empty otherwise)
  and a cadence in whole seconds, at least one and not capped at a minute (`FeedKey`). Every widget
  asking for one key shares one feed.
- **[S-2503]** The hub holds **one** ticker subscription, at the greatest common divisor of every
  live feed's cadence, with a minute joined in when any is longer (the ticker wakes on minutes at the
  slowest), and a feed is read when its own period index moves on (`HubCadence`): a 2 s and a 3 s feed
  read at 0, 2, 3, 4, 6 s; a 90 s ping on a 30 s wake. A new feed is read at once.
- **[S-2504]** A charted series (a feed plus an instance: a drive root or `*`, an adapter's position)
  keeps its history in the hub, in a ring of `SeriesHistory.Capacity` (= `MeterTiming.MaxCapacity`,
  600) points, the longest chart and the widest ping window any config can ask for. A series lives as
  long as its feed. A view built again — a view switch, edit mode, a display change, a reopened
  settings window — loads the history and the newest reading at once; a longer span shows the longer
  history at once; a different cadence, drive or host is a different series with its own history
  ([S-1120]). The point is the chart's terms (`MeterSample.Of`): a fraction, or the value on a
  window-scaled chart; an unanswered probe plots on the baseline but is no answer for statistics.
- **[S-2505]** Feeds are subscription-counted (`FeedTable`). A feed nobody holds **lingers 2 minutes,
  still read**, so a view switch and back within that keeps its history unbroken; then it is dropped
  with its history, and a source no feed reads is closed: its PDH query, GPU session, process buffer
  or address lease given back, a probe out cancelled. A stopped series holds nothing.
- **[S-2506]** A watch never calls back from inside the call that makes it: the view reads the newest
  value and the history off the watch, and every later read arrives through its callback on the UI
  thread. A callback that throws is logged and reported to that widget's card ([S-891]), and the
  other widgets still get the reading; the hub's own ticker subscription is outside any card's fault
  scope (`Ticker.SubscribeUnscoped`).
- **[S-2507]** A source that takes a while (GPU on the pool, ping) has one read out at a time; a due
  tick while it is out is served by its answer, so a slow host updates more slowly instead of
  stacking probes, and an answer for a closed source is dropped.
- **[S-2508]** Ping: one prober per host, shared by every ping meter and ping statistics widget on it
  (each cadence a feed of it, its own history); its transitions are logged once per prober.
- **[S-2509]** Event-driven where Windows says so: a power event (battery status, power supply,
  charge) re-reads the battery feeds at once; an address change marks the local and public addresses
  stale and re-reads them and the throughput. The hub listens only while such a source is open.
  These reads update the reading and add no chart point.
- **[S-2510]** Refresh (ping, disk space) reads the source now for every feed of it and adds a point
  to the asking feed's history: a probe asked for is a probe made.
- **[S-2511]** The switch check ([S-1883]) prints the hub's feeds, lingering feeds and open sources;
  the hub's one ticker subscription does not count against the live widgets while it holds feeds.

## System details (SYS-DETAILS)

The details the original's meters showed, back as their own placeable widgets; the meters themselves
stay label, reading and history ([S-1762]). Eight kinds in the System section of the picker, right
after the meters: CPU details, Memory details, Top processes, Network details, Disk space, Battery
details, Ping statistics, GPU details. One view, `DetailView` (+ `.List.cs`), one `DetailPart` per kind
(`Widgets/Details/*Part.cs`); rules in Core (`DetailFace`, `DetailSheet`, `CpuDetailRules`,
`MemoryDetailRules`, `ProcessTable`, `NetworkDetailRules`, `DiskSpaceRules`, `BatteryDetailRules`,
`PingWindow`, `GpuDetailRules`; configs in `Model/Configs/DetailConfigs.cs`); settings
`WidgetSettings.Details.cs`. Tests `SystemDetailsTests` (`S24xx_`). Strings in `Locales/widgets-details`
(English only so far).

- **[S-2400]** A detail widget is a hero (its main figure), a caption saying what it is of, facts in
  priority order and, for some kinds, a list. No "which facts" settings: the tile shows more as it grows.
  Every source is Win32, PDH, `NtQuerySystemInformation`, DXGI, D3DKMT or a battery IOCTL: no WMI, no
  `PerformanceCounter`, no driver, no admin rights. Every figure is a hub feed ([S-2500]), watched in `Attach` and let go in
  `Detach` ([S-1881]); a parked widget holds none.
- **[S-2401]** `DetailConfig.IntervalSeconds`, 1–60 s (disk space and ping up to 300). Defaults: CPU,
  memory, processes, GPU 2 s; network 1 s; battery 15 s; ping 10 s; disk space 30 s. Load clamps it, loudly.
- **[S-2402]** The ladder (`DetailFace.Plan`): Compact both ways is the hero alone, filling the tile (a
  list kind: its rows). A Wide strip at least Expanded wide puts the hero in a left column (40 %) and the
  facts in lines beside it. Otherwise hero, caption and facts stack; when every fact fits, the richest
  fact style and the largest hero that fit are chosen together (lines, then tiles, then large tiles; a
  tile style whose single column would cut the longest value is skipped), and without a list the height
  the facts leave goes to the hero, scaled past Display up to 2.4×. When not all fit, facts are lines in
  as many whole rows as fit, most wanted first. A list takes everything under the facts, one whole row at
  least, or is not shown. Costs are line heights of the live ramp.
- **[S-2403]** A fact Windows does not report on this machine is left out, never "--"; a fact that can be
  read but is not available right now (a lost probe) is a dimmed "--". A thing that is not on the machine
  (no battery, no adapter) is the empty state ([S-82a]). Why the hero is dimmed is its tooltip.
- **[S-2404]** A list is Windows 11 rows in a scroller with the foot fade ([S-1281]) and a focus ring
  walked by the keyboard lease ([S-800], `ListKeys`): Up/Down, Page, Home/End, Escape; Enter opens where a
  row opens (a drive). The ring stays on its row's key across reads. Only a list on screen wants keys.
- **[S-2405]** CPU details: the effective clock as the hero, "23% utilization" (the CPU meter's own shared
  read) under it; base speed, cores (`GetLogicalProcessorInformationEx`), logical processors, processes,
  threads, handles (`GetPerformanceInfo`), up time, the thermal zone. The header names the processor from
  the registry, without vendor and generation noise ("Core i9-12900K").
- **[S-2406]** The clock is the base clock times `\Processor Information(_Total)\% Processor Performance`
  (Task Manager's figure, past 100 % under turbo); without the counter the reported clock stands in.
- **[S-2407]** Temperature is the hottest ACPI thermal zone (`\Thermal Zone Information(*)\Temperature`),
  labelled "Thermal zone" because it is the board's zone, not the package; 1–150 °C or left out.
- **[S-2408]** Memory details: in use as the hero, "41% of 64 GB"; available, committed (of the commit
  limit), cached, paged pool, non-paged pool (`GetPerformanceInfo`), page file.
- **[S-2409]** The page file is `NtQuerySystemInformation(SystemPageFileInformation)`, every file summed
  ("None" without one); the chain is walked defensively and never past the buffer.
- **[S-2410]** Top processes: one `NtQuerySystemInformation(SystemProcessInformation)` snapshot (no
  per-process handle, so elevated and protected processes are counted), parsed in Core; memory is the
  private working set (Task Manager's column). One hub read per due tick for every list ([S-2501]). The pinned buffer goes with the last list. The idle process is left out.
  A new widget: the removed process list ([S-1902]) stays removed.
- **[S-2411]** `SortBy`: processor (default) or memory; ties by the other figure, then name.
- **[S-2412]** `MaxRows`: 0 (default) lists every process the list can scroll to, 1–50 a ceiling.
- **[S-2413]** `GroupByName` (on): every process of one image name is one row, figures added, the count
  after the name ("brave (26)").
- **[S-2414]** A share is processor time over wall time over the logical processors (the CPU meter's
  scale). A process first seen reads 0; a pid with another start time is another process and starts over.
  From 280 px a row shows the other figure beside the ranked one.
- **[S-2415]** Network details: download as the hero, "Up 1.4 KB/s"; received and sent since Themia
  started, the busiest adapter's local address, the public address when asked for; from the network
  meter's own shared reader ([S-1190]), so the counted adapters are the meter's ([S-1192]).
- **[S-2416]** One list row per counted adapter, busiest first: name, "↓ x ↑ y", its address under it.
  No adapter is "No connection".
- **[S-2417]** `ShowPublicAddress` (off by default: it asks a third party) asks `api.ipify.org` through the
  guarded `WebFetch` client, only while such a widget lives.
- **[S-2418]** The answer is shown only when it is one IPv4 or IPv6 address; it is trusted 10 minutes and
  asked again at once after the machine's addresses change. A failure is logged without the body.
- **[S-2419]** An adapter's local address is its first IPv4 that is not link-local, else its first global
  IPv6; re-walked at most every minute, or when Windows says the addresses changed.
- **[S-2420]** Disk space: one row per drive, named as Explorer names it ([S-1161]), the share used,
  "844 GB free of 1.8 TB" (free to this user) and a bar coloured as a load (caution 70 %, critical 90 %).
  A few rows in a tall tile grow into their share of it (figure by ramp step, thicker bar). Enter on a row
  opens the drive.
- **[S-2421]** `Drives`: empty lists every drive the disk meter's walk lists ([S-1162], at most every
  30 s), by letter; otherwise those roots in order. Load keeps roots only, once. A chosen drive that is not
  connected keeps its row ("--", "Not connected").
- **[S-2422]** Space is read only for a drive the walk found ready, so no tick wakes a sleeping drive.
- **[S-2423]** Battery details: the charge and status from `GetSystemPowerStatus` (the meter's source);
  the rest from the battery class driver (`SetupDi` + `IOCTL_BATTERY_QUERY_TAG/_INFORMATION/_STATUS`),
  every battery summed, devices re-listed once a minute, one hub read per due tick; asked only when Windows
  says there is a battery.
- **[S-2424]** `BATTERY_INFORMATION` and `BATTERY_STATUS` are parsed in Core: all-ones and zero are
  unknown, the chemistry code becomes a word, the temperature level is tenths of a Kelvin (−40–120 °C).
- **[S-2425]** Status: Charging, Fully charged, Plugged in, not charging, On battery. Then time left
  (Windows' estimate, else remaining over the drain) or full in (while charging), health (full over
  design), full-charge and design capacity, cycles, charge or discharge rate, voltage, temperature,
  chemistry and maker, each only when reported; a relative-capacity battery shows no Wh or W.
- **[S-2426]** Ping statistics: the hub's prober for the host ([S-2508]), its window the prober's history, one at a time, a late
  answer dropped, a host change clears the window ([S-106], [S-107]); host and name rows as the meter's.
- **[S-2427]** `Window`: the latest 5–500 probes (offered 10, 30, 60, 120, 300; default 60).
- **[S-2428]** Over the window: min, average and max of the answers; jitter the mean absolute difference
  of consecutive answers (a loss between two does not break the pair; under two answers, none); loss the
  unanswered share of every probe.
- **[S-2429]** The latest latency is the hero, the status (Online, Offline, Checking…) the caption; the
  header names the host. A lost probe dims the hero, its tooltip naming the host and the status.
- **[S-2430]** GPU details: video memory in use (`\GPU Adapter Memory(*)\Dedicated Usage`, summed per
  LUID) as the hero, "of 7.8 GB video memory" (DXGI); load ([S-434]), shared memory, temperature, fan, GPU
  clock, memory clock. An integrated adapter leads with its shared memory. From the GPU meter's own shared
  sampler (the memory counters join its query); the header names the adapter ([S-430]).
- **[S-2431]** `Adapter`: a DXGI position, offered with two or more adapters; one that is gone shows the
  first.
- **[S-2432]** Sensors from the graphics kernel, any vendor's WDDM 2.4+ driver (`D3DKMTQueryAdapterInfo`
  adapter and node performance data and caps, one handle per adapter per sampler session): temperature
  1–150 °C, the fan only when the caps name one, clocks only when non-zero; a driver that answers none is
  asked no more.
- Not ported / dropped: CPU fan speed and package temperature (no Windows source without a driver or a
  third-party monitor; the original's hardware-monitor bridge stays out); GPU power draw and
  encode/decode (D3DKMT gives power only as an unlabelled share, NVML is NVIDIA-only); the per-core grid;
  IOPS and per-disk I/O (the disk meter is I/O); the Wi-Fi name (Windows 11 gates it behind location
  consent). Ping statistics and a ping meter on the same host each probe at their own cadence.

## Feed, stocks and the web widgets: parity and the whole tile (OFF-E)

Ported from `widgets/online/{rss,stocks,video-stream,web-element}` on main, renumbered; the rules
live in Core (`FeedAggregate`, `FeedLayout`, `NetworkGuard`, `MarketSearch`, `MarketQuote`,
`ExchangeTime`, `StockLayout`, `NetworkCacheStore`), tests in `FeedWidgetTests`/`StockWidgetTests`.

- **[S-1300]** Feeds and quotes survive a restart (classic S-23/S-29): the last good answers are
  saved to the sealed `web` shelf of the offline store ([S-2660]; never in an export;
  schema-versioned; one writer; saved after each fetch that brings a new answer). At launch they are
  read back before the windows are built and marked restored: drawn at once, with their real age in
  the header in the caution colour ("Saved 3h ago; refreshing"), and fresh only for that age
  ([S-2662]), so a fetch follows once the widget's interval has passed. Entries older than 7 days, from the future, or not a usable feed/quote are
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
  cache's freshness and the back-off, through the feed sync ([S-2661]). While a fetch for one of its
  feeds is out, a small spinner sits beside the age in the header (classic S-33), so Refresh is never
  a button with no visible effect.
- **[S-1304]** Site icons (classic S-26/S-27, the Google s2 service replaced): fetched from the site
  itself, `scheme://host[:port]/favicon.ico` of the item's link — no third-party service. Guarded
  ([S-1301]), 64 KB cap, the bytes must be ICO/PNG/GIF/JPEG/BMP (not HTML, not SVG), one request per
  origin per session shared by every row and widget, a missing icon retried after 6 h; decoded at
  2× the 16px drawn size. A globe stands in until it arrives or when there is none.
- **[S-1305]** Rss ladder (`FeedLayout.Plan`, rows shown whole, measured since rows are
  as tall as their headlines, the rest a scroll away with the shared fade [S-1281]): rung 0 the
  newest headline, two lines (one if even that does not fit); its age; its site icon from 180px of
  row width (inline at the start of the age line); the site's name on its own line from 240px; two
  columns from 400px wide (three from 720px on a short strip), read left to right; the summary where
  it costs no row, or where ≥3 rows still fit on a tile ≥280px high and not a tall column (1–2
  lines). By tier: XS one headline; S headline + icon + age; M two rows with names; Wide two
  columns; Tall 5+ headlines; L two columns with summaries; XL three columns with summaries.
- **[S-1306]** No reading pane at any size (retired: it read one item beside the list; user
  decision, the feed is its headlines). A click on a row focuses it; a double-click opens it
  everywhere, as every list on this desktop. Item menu (`ItemMenu`): Open, Copy link, Refresh. Keys through the shell's
  keyboard lease (`IWidgetKeys`, [S-800]; a click in the widget takes it): Up/Down/Left/Right step,
  Home/End, Enter opens, Escape lets go.
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
  and lowest bar, kept inside the chart; an axis price a marker's price would overlap is left out,
  the marker says the same price, `StockChartLabels.Place`); **Fill under the line** (`ShowAreaFill`, on — WinUI's
  look; the original defaulted off); **Rising/Falling colour** through the shared colour picker
  (`ColorPickerRow`, never hex; empty = the theme's green/red; a hand-edited non-colour is repaired
  to empty at load).
- **[S-1311]** Stocks ladder (`StockLayout.Plan`, replacing the 34%/120px chart band): rung 0 price +
  signed change (change under the price where it does not fit beside); currency and session glyph
  from 180px, the session's words from 300px; the extended line; the chart (≥24px, full-bleed) with
  start/end times from 128px high and the middle one from 400px wide, a price axis (high/mid/low) from
  400px wide and 120px of chart. A strip (≥400px, 2:1) puts the readout beside a full-height
  chart. The chart takes what is left; the price grows only to Title / TitleLarge / Display by the
  tile's smaller class. Pointer over the chart on L/XL reads the bar under it (time · price). The
  session badge and age re-evaluate every minute on the shared ticker (render only), so the badge
  flips at the bell whatever the refresh. **Refresh** in the card menu. Header title: name, else the
  service's own name (listed), else the symbol.
- **[S-2770]** Stocks is concise ([S-666], user decision): the name, the price, its change, the
  session badge, the extended-hours line ([S-1308], off by default, one caption under the price) and
  the chart, nothing else at any size. No facts (previous close, day and 52-week range, volume,
  exchange; `MarketQuote.Facts`/`Volume` are gone) and no range chips on the widget: the period is
  the **Timeframe** row in the widget's settings (`Range`, default 1 day). What they took goes to the
  chart. Those facts are not parsed or cached either (`QuoteStats` is gone; the previous close is
  read only to compute the change).

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
log into `Log`). Not done here: the original's pre-release gate on the Google sign-in provider (a launch decision, not a developer feature).

## Mixer and Volume (SYS-P5)

`Themia.App/Services/CoreAudio{,.Worker,.Native}.cs` (replaces `AudioSessions.cs` and the polled
`AudioEndpoint.cs`), `Themia.App/Widgets/{MixerView,MixerView.Row,VolumeView,AudioLook,AudioFeed}.cs`,
`Themia.Core/Widgets/{AudioRules,MixerRules,VolumeRules}.cs`, tests `AudioRulesTests`.

- **[S-1220]** One Core Audio worker serves both widgets (the [S-440] MTA thread, now named
  `CoreAudio`). The volume tile no longer polls: it is a subscriber that does not ask for sessions,
  and moves on the endpoint's `IAudioEndpointVolumeCallback`. `VolumeConfig.IntervalSeconds` and its
  settings row are gone. Each subscriber states what it wants (`AudioWant`: device, sessions, meters);
  the worker binds exactly the devices wanted and releases the rest, and publishes one
  snapshot per subscriber per wake.
- **[S-1221]** `DeviceId` (+ `DeviceName`, remembered for display) on both configs; null follows
  Windows' default output (console role). A chosen device that is not connected is "*name* is not
  connected", never silently the default. The worker re-enumerates active render endpoints (friendly
  name from `PKEY_Device_FriendlyName`) on device added/removed/state/rename notifications. A blank
  id is repaired to null at load.
- **[S-1222]** Device picker: settings ComboBox only ("Default output" + connected devices,
  enumerated off the UI thread), which binds the widget to a device. Neither widget offers a device
  picker of its own at any size (user decision: choosing the output is Windows' job, not the
  widget's); the device line, the volume caption's menu and the XL panel's device list are gone.
  Context action "Follow the default output" when a device is chosen. Inert in previews.
- **[S-1223]** Level meters via `IAudioMeterInformation` (per session; a group shows its loudest
  session). The only sampled thing: at most 10 Hz, only while some mixer has a meter on screen
  (loaded, ladder rung shown, `ShowPeaks`), silence sent once. The volume tile has no meter and never
  asks for peaks ([S-2730]). Meters are a scale
  transform on a fixed bar (no layout per sample); a muted row's meter is empty.
- **[S-1224]** *(Retired: neither audio widget shows the microphone; it is not their job. The
  `ShowMicrophone` settings are gone.)*
- **[S-1225]** Mixer keys (keyboard lease): Up/Down/Home/End move the row cursor (system focus rect on
  the row), Left/Right and +/− step the row 2 points, Page Up/Down 10, M or Space mute, Menu key opens
  the row menu, Escape lets go. With no row focused a row key only focuses the first row. `WidgetKey`
  gains `Plus` (VK_ADD, VK_OEM_PLUS), `Minus` (VK_SUBTRACT, VK_OEM_MINUS), `Mute` (M without Shift).
- **[S-1226]** Volume keys: arrows and +/− step 2 points, Page Up/Down 10, Home/End to 0/100
  ([S-2733]), M or Space mute. Ignored when `AllowControl` is off.
- **[S-1227]** Mixer rows: compact (< 170 px: icon, mute; 32 px), two lines (48 px), one line
  (≥ 360 px; 40 px) — fixed heights so the list takes whole rows (`Disclosure.TakeRows`); the rest
  scroll, never half a row. Supersedes [S-449]'s thresholds' wiring (values unchanged).
- **[S-1228]** Mixer rows by room: apps holding a session but silent are drawn dimmed; with
  `OnlyActive`, the silent apps it hid follow the list dimmed when all playing rows fit. The mixer is
  its rows and nothing else at any size: no device line (was: name, "N playing", chevron).
- **[S-1229]** *(Retired: the XL mixer has no side panel — no device name, master level beside the
  list, microphone or device list. The master stays a row of the list, as at every size.)*
- **[S-1230]** *(Retired: the volume tile has no ladder. The number, the level meter and its
  `ShowPeaks` setting, and the `ShowSlider` setting are gone; see [S-2730].)*
- **[S-1231]** *(Retired: the volume tile lists no apps; that is the mixer's job.)*
- **[S-1232]** *(Retired: no caption and no device name on the tile; muted reads from the speaker
  glyph and its tooltip. `VolumeRules.Caption`/`Readout` are gone.)*
- **[S-1233]** Previews never write (sliders, buttons, keys and device picks are inert); they read
  the machine.
- **[S-1234]** Stepping lands on the whole-percent grid and clamps (`AudioRules.Step`).
- **[S-2730]** The volume tile is the slider and nothing else, at every size (user decision): a
  speaker button at the slider's start (glyph by level, crossed out when muted; click mutes, tooltip
  Mute/Unmute) and the 0..100 slider. No percentage, no level meter, no device name. The card header
  follows the shared header rules. Settings: output device and "Allow changes"; read-only greys the
  controls rather than hiding them. Unavailable/missing device shows the empty state.
- **[S-2731]** The slider lies along the tile (`VolumeRules.Axis`): horizontal, centred vertically
  between side insets, on every shape but a column; a Tall area ([S-660]) stands it up, full
  height, with the speaker button under it. Growing a tile only lengthens the slider ([S-666]).
- **[S-2732]** The mouse wheel anywhere on the tile steps the level one arrow step (2 points) per
  notch (120); a touchpad's part-notches carry to the next event, turning back drops the carry
  (`AudioRules.Wheel`). Ignored when `AllowControl` is off; no keyboard lease needed.
- **[S-2733]** Home/End set the volume to 0 / 100 (`AudioRules.VolumeKey`); the mixer keeps
  Home/End for its row cursor.
- **[S-2771]** The mixer shows no volume number (user decision, [S-666]): a row is its icon, name
  (and session pill), slider, meter and mute button; the level is the slider's position and the mute
  glyph's band (crossed out when muted). No percent beside a row at any width, the master row
  included. The slider's own drag tooltip is the only number, while dragging.

The original Volume had no peak meter or device name; the audit added them and [S-2730] took them
out again.

## Widget polish 1 (POLISH-1)

Keyboard lease in the remaining lists, Accounts links, and four layout gaps.

- **[S-1560]** One key vocabulary: `ListKeys.Decide` takes the lease's `WidgetKey` (the separate
  `ListKey` enum and `RowFocus.Key(VirtualKey)` are gone). Keys a list has no use for (Left/Right,
  Tab, Space) decide `None` and go on to the surface; the Menu key (or Shift+F10) is
  `ListCommand.Menu`, the focused row's menu. Mail implements `IWidgetKeys` (no view
  `KeyDown`): Up/Down, Page Up/Down, Home/End, Enter opens, Delete deletes (Mail, where the account
  allows), Menu, Escape (also every lease end) clears the row focus.
- **[S-1561]** Clipboard takes the lease: Up/Down/PgUp/PgDn/Home/End move a cursor, Enter copies the
  entry back, Delete removes it from the history (cursor stays on the row that moved up), Menu opens
  its menu, Escape drops the cursor.
- **[S-1563]** *(Retired: no note list, [S-1363]; Notes takes no lease keys. The editor and the
  picker's rename box still request the keyboard for typing.)*
- **[S-1994]** *(Retired: no note list to type ahead in, [S-1363].)*
- **[S-1564]** `IWidgetKeys.WantsKeys` (default true): a view whose keys belong to a part shown only
  at some sizes says no while it is off screen, so the click does not take the keyboard. The card's
  `TakesKeys` honours it.
- **[S-1568]** RSS: the Menu key opens the focused item's menu.
- **[S-1569]** *(Retired: no Bluetooth detail panel. A short list on a large tile stays the list;
  the rows' second line and tooltip carry each device's facts.)*
- **[S-1988]** *(Retired with the detail panel: the Bluetooth list takes no lease keys.)*
- **[S-1571]** *(Retired: no summary line or strip at any size, [S-1427]; the header's "3/5" keeps
  the count.)*
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
  the height out when every row fits. Every view fills from the top-left of its box, row by row
  (a grid in the columns the width was shared into); room the items do not use stays at the right and
  bottom, never shared out to centre them (was: a grid whose items all fit sat centred as a group).
- **[S-1383]** Real icons: a file, folder, executable or `shell:` location gets Explorer's icon
  through `IShellItemImageFactory` (icon only, never a thumbnail) at twice the drawn size, one
  extraction at a time (concurrent first requests return E_PENDING; one immediate re-ask, no wait),
  cached per path, size and variant; a web address gets the site's own `/favicon.ico` through the
  shared guarded `FaviconCache` (no third-party service). The kind glyph shows until the icon
  arrives and stays when there is none. List and details rows: 32 / 40 / 48 high (one step smaller
  on a short tile), the target as a caption line on L/XL, two columns on XL and on a Wide strip.
- **[S-1384]** Details view: name; target column from Wide up; kind column (Web address / Windows
  location / File / Folder / Missing / Not allowed) on XL; column header when it has the target
  column and room for two rows under it. No status footer at any size (was: "N shortcuts", "N
  selected" at XL; retired, concise widgets).
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
- **[S-1995]** Type-ahead ([S-1980]) over the shortcut names selects the match alone and moves the
  cursor to it; not while a rename box or the search box is open.
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
  the public address) and the `AccountHttp` client (sign-in, token refresh, Microsoft Graph,
  Google, CalDAV/CardDAV) — and every
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
- **[S-965]** Account servers follow the switch. A user-typed CalDAV/CardDAV or IMAP server on this
  computer or the local network (a NAS) is refused while **Local network access** is off and
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

## Widget picker: search (WP5)

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
- **[S-957]** Debug builds render the open picker: `Themia.exe --picker-png <out.png> [--search text]`.

Not ported: picker row previews (`WidgetRowPreview`) and the group-by choice (`pickerGroups.ts`) — the
columns are the grouping; the pro lock (licensing is out of scope).

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

## Shell hooks (PA)

The card hooks widgets use. Rules in Core `LeaseKeyRouting`, `WidgetZOrder`, `CardFrame`,
`WidgetClipboard`/`WidgetTransfer`; carried out by `WidgetCard` and `DesktopWindow.Layout.cs` /
`.Input.cs`.

- **[S-860]** Lease keys reach the holder **first**: the surface listens to the tunnelling
  `PreviewKeyDown` and offers the key to the leased view before any control inside its card. A
  `ScrollViewer` handles arrows, Page Up/Down and Home/End in the bubbling pass, so the old
  bubble-only handler never saw them and list navigation was dead in every scrolling list. A key the
  view does not use carries on to the focused control. While a text editor has focus every key but
  Escape is still the box's ([S-804]); Escape ends the lease in the bubble, after a box that clears
  on Escape had it.
- **[S-872]** The hit region follows **paint** (`CardFrame.Hit`): a hidden widget ([S-873]) takes
  nothing; every other card takes its painted rect, inside the gap.
- **[S-873]** `IWidgetSurface.SetHidden(bool)`: the card stops drawing its fill, border and content
  and leaves the hit region; the view stays attached so it can show itself again. Edit mode always
  draws it, at half opacity. Not persisted. (For Music's hide-when-idle.)
- **[S-874]** Bring to front / Send to back on every widget's card menu (disabled when it already
  is). The order is the view's widget list, persisted with it, and it is the paint order, applied
  through `Canvas.ZIndex` — nothing is removed and re-inserted in the tree.
- **[S-877]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not live-verified: lease key routing in the real surface.

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
- **[S-1065]** Free tier: every widget type, at most **10 widgets** (all views) and **2 views**, as
  the original. Gates at every create: placement start and landing, paste (net of a cut's original;
  a selection pastes whole or not at all), duplicate and new view. A newly seen display creates nothing ([S-1705]), so it needs no gate. A refusal creates nothing and says why in a notice ([S-881]) whose "See plans"
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
  widget whose cell it touches is selected, live, as the band changes. With Ctrl or
  Shift held at the press it adds to the selection on that display; without, it starts from nothing
  (a click on empty surface clears the selection). A card that draws nothing is not picked.
- **[S-1582]** **Ctrl+A** selects every widget on the display under the pointer.
- **[S-1583]** Every selected card shows it (2-px outline, full-strength handles). A widget that is
  deleted or leaves the view leaves the selection with the change. Escape clears the selection
  before a second Escape leaves edit mode.
- **[S-1584]** Dragging a selected card moves **every selected widget on that display** by one
  translation: the block's bounding box is moved, its top-left snapped to the grid
  ([S-06], Alt suspends it) and the box slid into the work area ([S-841]) — never resized — and each
  rect is then kept on screen ([S-09]). Offsets between the widgets never change. Resizing is per
  card and narrows the selection to it.
- **[S-1585]** A ghost of several widgets shows **every widget's outline** at its offset in the
  block. An outline too small for the icon and name shows the frame alone.
- **[S-1586]** A payload lands with its bounding box centred on the pointer (a paste, Duplicate,
  Move), or with the held widget centred on it (a drag handed to another display), then snapped and
  clamped as a block on the display under the pointer. The ghost and the landing come from the same
  calculation, so the widgets land exactly where the ghost was.
- **[S-1587]** One **placement session** for everything that produces or moves widgets: Add widget
  (a new widget of one kind), Paste (Ctrl+V and menu), Duplicate (Ctrl+D and menu), Move (menu: to
  this display or any other). No command drops a widget at a computed spot
  any more. Click lands; Escape, a right-click or leaving edit mode cancels and changes nothing. The
  licence gate ([S-1065]) runs before the ghost appears (what the session would create: a copy of N
  widgets counts N, a move 0) and again on landing.
- **[S-1588]** A payload taken from the selection holds the selected widgets of that view, in view
  order, as a snapshot with original ids.
- **[S-1589]** A **copy** (paste of a copy, Duplicate) lands fresh widgets with fresh ids every
  time. The clipboard is not touched by Duplicate.
- **[S-1590]** A **move** (paste of a cut, Move) lands the **same** widgets: the originals are taken
  out of whatever view they are in now and the live instances — with any config change since the
  snapshot — take the planned rects. An original deleted meanwhile is put back from the snapshot.
  Until the click lands the
  originals stay where they are, faded; cancel leaves them and the cut as they were.
- **[S-1591]** Dragging a selection onto a display that shows another view hands **all** of it over
  in one landing ([S-76], [S-77]) and selects it there.
- **[S-1592]** Delete, Copy, Cut, Duplicate, Move, Bring to front and Send to back act on the whole
  selection; the card menu says how many ("Delete 3 widgets"). A right-click on a selected card keeps
  the selection; on any other card it selects that card alone first. Layering keeps the order the
  selected widgets had among themselves.
- **[S-1593]** A press on a card of a multi-selection keeps the selection so the drag that may follow
  carries all of it; released without moving (3 DIPs), it narrows the selection to that card.

Not done: a debug render of the multi-widget ghost (the outlines are pinned by
`S1585_*` instead). Not live-verified: everything pointer- and multi-monitor-driven here — the
rubber band, Ctrl+click, block drag clamping at the taskbar, hand-off of a selection across
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
  always shows the card; a preview never hides. While hidden, no position ticker.
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
- **[S-1467]** *(Retired: the XL "N sessions done" row (dots and count) and the session counter are
  gone; XL shows the ring, the phase, the next phase and the controls like L.)*
- **[S-1468]** Music keys via the keyboard lease (`IWidgetKeys`; taken only with a track shown):
  Space/Enter play/pause, Left/Right previous/next, Shift+Left/Right seek ∓5 s (new
  `WidgetKey.ShiftLeft/ShiftRight`; Shift+arrows no longer arrive as plain arrows in other views).
- **[S-1987]** Pomodoro keys (`ControlKeys.Pomodoro`): Space or Enter start / pause, Right skips to
  the next phase, Home resets: the three buttons in their order. Nothing after the card left.
- **[S-1469]** The large tiles split the subtitle into artist and album (album dropped when it
  repeats the artist). No "Playing · <app>" line (retired, [S-666]).
- **[S-1470]** *(Retired: the spectrum left Music for the audio visualizer, [S-3200]..[S-3201];
  `ShowSpectrum` and its settings row are gone.)*
- **[S-1471]** *(Retired: moved to [S-3202].)*
- **[S-1472]** *(Retired: moved to [S-3203].)*
- **[S-1473]** *(Retired: moved to [S-3204].)*
- **[S-1474]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1475]** Music ladder: XS title (+ artist) over play/pause; S/Wide lines with the transport
  beside (or under, or play only) and art on a strip, seek bar along the bottom (times beside it
  on a wide one); M art + lines + transport, seek bar with the times under it; Tall stacked like
  the media flyout (art, lines, seek, times, transport); L/XL art up to half the width beside
  title/artist/album/40px transport, seek row with times along the bottom. No spectrum ([S-3200]).
- Not ported: the compact-header default of the original's [S-21] (WinUI header modes differ);
  `pollInterval` (legacy, unused).
  Not live-verified: actual sound output of the cues and the preview, single-flight stop, seeking
  against real players (Spotify, browsers), the hidden card's hit-region on the desktop, and keys
  through the lease.

## Audio visualizer (VISUALIZER)

- **[S-3200]** `AudioVisualizer` (kind 70, `AudioVisualizerConfig`, discriminator
  `audioVisualizer`): the spectrum of whatever the PC plays, split out of Music, which no longer
  draws it. Listed under Media beside Music; glyph `Glyphs.Visualizer` (E7F3; Equalizer is the
  mixer's). Starts headerless ([S-700]). No settings of its own: the spectrum in Music was an on/off
  switch and nothing else, and placing the widget is that switch now; the page carries only the
  card's appearance rows. Accent fill (`AccentFillColorDefaultBrush`), never a typed colour.
- **[S-3201]** The analyser (was [S-1470]): WASAPI loopback of the default render endpoint
  (console role), mixed to mono, Hann-windowed 1024-point FFT, 24 log bands from bin 1 to Nyquist,
  sqrt compression ×3.5, clamped 0..1 — the original's. Float32/Int16/Int32 mix formats; anything
  else and silent packets count as zeros.
- **[S-3202]** Bars rise at once and fall 0.06 per frame (was [S-1471]). The worker drains every
  33 ms while sound arrives or a bar is up, and every 150 ms once all are down and nothing came
  (inside the 200 ms capture buffer); in silence it sends frames only until the bars are down,
  then nothing. Display cadence, not a correctness path: event-driven loopback capture is
  unreliable across drivers.
- **[S-3203]** One shared capture worker (`AudioLoopback`, `CaptureLease`; was [S-1472]): the first
  subscriber starts it, the last stops it (signalled, never joined on the UI thread); a remount that
  overlaps old and new never stops it; a worker that ended is restarted by the next subscriber. A
  tile subscribes only while it is attached, loaded and not a preview, so game mode (which detaches
  every widget, [S-920]) and a hidden view cost nothing. Frames are coalesced to one queued draw per
  tile.
- **[S-3204]** Self-healing (was [S-1473]): an `IMMNotificationClient` (immortal native object) flags
  a change of the default render device and the worker re-acquires at once; any capture error
  retries after 500 ms (logged once per distinct HRESULT). COM is MTA on the worker; every interface
  pointer and the mix format are released on every path; no exception leaves the thread or a
  callback. The worker reports whether it reached the device (`CaptureLease.Report`: unknown on a
  new worker, told to subscribers once per change, and at once to one joining a failed worker).
- **[S-3205]** Layout (`VisualizerLayout.Plan`): one part, the bars, filling the tile inside a 16 DIP
  inset (12 on XS). They run along the long side: standing on the floor of a tile or a strip, lying
  across a Tall column (growing from the start edge, bass at the bottom). The count is the largest
  of 24/12/8/6 (each a whole group of the 24 bands) whose bars stay ≥ 6 DIP thick; past 24 they
  only thicken, so a larger tile draws the same bands larger (XS/S 12, M and up 24). Gap = run/100,
  2..6 DIP. A bar is the loudest band of its group; bad levels clamp to 0..1.
- **[S-3206]** Idle is a flat baseline: every bar a 3 DIP stub, the same on every tile, no text.
  Only a device the capture cannot reach ([S-3204]) shows a sentence instead ("Audio output
  unavailable", the empty-state shape); the bars come back when the worker reaches it.
- **[S-3207]** Debug renders cannot capture sound: `--widget-png AudioVisualizer --state playing`
  draws a still synthetic spectrum, `--state unavailable` the sentence.
- Not live-verified: loopback capture against real output and device-switch healing, the
  unavailable sentence on a machine with no output device, the idle cadence's CPU cost, and the
  capture stopping under game mode.

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
  widget picker's translated + English search names. Polish plural keys have only
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
- **[S-1487]** Clock ladder: face → label → date (short when narrow; + offset on a wide strip)
  → offset; the face takes the rest. No week, day-of-year or zone line (retired, [S-665]). Not ported: an XL list of
  extra world-clock rows (each world clock is its own widget, as in the original).
- **[S-1488]** The analog dial at every tier, sized by the ladder (40 px floor, rebuilt only when the
  diameter changes): dial alone on XS, text under it on panels and columns, text beside it on a wide
  strip. The second hand still sweeps (S-51, deliberate).
- **[S-1489]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1490]** The Sun widget adds civil dawn/dusk (sun's centre at −6°; none in white nights), solar
  noon with the sun's altitude, and the day's change against yesterday ("−3m 22s"; none inside the
  polar circles). Pure in `SolarPosition` / `SkyText`.
- **[S-1491]** The next full or new moon, whichever comes first, from the same linear lunation as
  the phase, so the date agrees with the disc.
- **[S-1492]** *(Retired: Sun & moon is two widgets; the Sun's ladder is [S-3102], the Moon's
  [S-3105].)*
- **[S-1493]** *(Retired by [S-1746].)* The sun path's modes and the event rows are settings only;
  the widget has no menu entries of its own.
- **[S-1494]** The place is picked by city name through the shared `PlaceSearch` ([S-2840], Open-Meteo
  geocoder, resolved once at the pick); exact name/latitude/longitude stay editable and validated
  (S-171's refusal rule unchanged). This supersedes S-171's "no geocoding service" reason.
- **[S-1495]** Quote: the bundled list is the original's sixteen in its order, so a daily unshuffled
  rotation shows the original's quote for the day (`QUOTES[localDay % 16]`, regression-tested). The
  header button shows a random *other* quote (`QuoteRotation.RandomStep`); a tap steps to the next.
  Both stay ephemeral (original S-02).
- **[S-1990]** Quote keys (`ControlKeys.Quote`), only with two or more quotes: Right, Space or Enter
  the next quote (what a tap does), Left the one before. Ephemeral, like the tap.
- **[S-1496]** Quote ladder on `IWidgetSizeAware` + `Disclosure`: quote (Caption floor) → author;
  the author goes when the quote would not fit. Nothing else at any size: the rotation caption
  ("Quote of the day · 3 of 16") is retired, a larger tile sets the quote larger. Ceiling TitleLarge, Display on a Huge × Huge tile (replaces S-653's cap there).
- **[S-1497]** Scratchpad ink bar (original S-02 toolbar): six preset inks (`InkPalette`, shared
  with the settings picker) and the original's three nibs 2/4/8 px at the foot of the pad from
  Regular × Regular; nibs only when the width holds both; nothing more on a larger pad (the stroke
  count at W ≥ Expanded is retired, concise widgets). Picking an
  ink leaves the eraser. Header tools still shed per S-651. Not ported: export as PNG.


## Device and location (DEVICE-AND-LOCATION)

`Themia.App/Services/CoreAudio.Default.cs`, `Widgets/{AudioFeed,VolumeView,MixerView}.cs`;
Core `Widgets/AudioDefaultRules.cs`; tests `DeviceAndLocationTests`. The location half ([S-1610]..[S-1613],
[S-1619]: the Wi-Fi name, its consent prompt and the `location` capability) is removed with the
network meter's connection line ([S-1762]).

- **[S-1614]** *(The in-widget device menu and the XL mixer panel are gone, [S-1222]; only the
  "Follow the default output" context action reaches this path now.)* Picking a device made it
  Windows' default output and the widget kept following the default. "Follow the default output" and
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
  of every widget under fresh ids. Data a widget only refers to (a note, an
  account) stays shared, as in the original. The copy is shown on the display asked from; it counts
  against the licence as one view plus its widgets.
- **[S-901]** Reorder views by dragging a chip on the view bar (edit mode), by dragging a view's group
  on the Views page ([S-1703]), or Move up / Move down in a view's menu (Views page). By id, never by bar index, so a bar showing a filtered subset moves the right view.
  The order is the bar's, the Views page's and Ctrl+n's.
- **[S-902]** Deleting a view (never the last) repairs everything naming it in the same change:
  display assignments (the display then gets an existing view again, [S-1705]), the active view, the view for
  new displays (back to null) and every bar whitelist (one left empty means all views again).
- **[S-903]** A view's icon is picked in its group on the Views page from the shared glyph grid
  (`IconChoice`), whose default cell shows the view's initials — what a view without an icon shows.
  The icon is drawn by the view bar, the toolbar's View menu and the group's
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
  offers all (`ViewListRules`). On the display page the last view cannot be switched off, and
  switching every view on goes back to "all", so later views appear by themselves.
- **[S-909]** The bar sits at its anchor inside the display's work area, 16 DIP from its edges,
  never over the taskbar. In edit mode a bar that would overlap the edit toolbar (top centre) moves
  below it; one beside it stays. A bar larger than the area is pinned to its top-left.
- **[S-910]** The left and right middle anchors stack the chips vertically; the other six lay them
  in a row.
- **[S-911]** Sizes M, L and XL are the original's (Caption/Body/Body Large type, 14/16/20 icons,
  28/34/40 icon-only squares). A chip shows the view's icon or initials (the first letters of the
  first two words, or the first two letters of one word, upper-cased, counted in text elements; "?"
  for a blank name) and its name (bold for the
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
  View bar position (8 anchors), View bar size, Show view names, and "Views on the bar" (one
  drop-down button, [S-1708]). Its Shows drop-down assigns through `ShowViewOn`.

Not ported: the original's chip "drag-over" highlight (the chip itself moves instead); the free-tier view-limit
tooltip on the bar (licensing refuses through `AllowCreate`). Debug render:
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
- **[S-1708]** "Views on the bar" in a display's group stays one row tall however many views there
  are (`FluentLayout.MultiChoice`; was a column of check boxes on the row, one line per view). Its
  drop-down button says "All views", the one view's name, or "3 of 25 views"
  (`ViewBarRules.Offer`); its flyout has an "All views" box (indeterminate for a partial pick; a
  click on it then picks all, a click on a full one keeps only the view this display shows,
  `ViewBarRules.SetAllVisible`), a divider, then one check box per view in the user's order with
  its icon or initials, cut with an ellipsis, scrolling past 360 DIP. Each box follows [S-908]; the
  boxes and the button are read back from the stored whitelist after every change.
- **[S-1709]** The Displays page has no Resolution row: the resolution is the display group's
  subtitle already ([S-853]), and the work area the taskbar leaves is not something the reader acts on.
- **[S-1830]** The navigation pane lists pages only: General, Appearance, Accounts, then **Displays**
  and **Views** (in that order, with Windows Settings' screen and content-view colour icons,
  [S-1600]), and License and About at the foot. No entry per view, display or widget, so no entry
  menus and no dragging in the pane.
- **[S-1831]** A page target is a page plus, on Displays or Views, the item whose group to open
  (`ConfigTarget.App(Page, Item)`; `ConfigTarget.ForView`). Everything that used to open one view's
  page (a view bar chip's View settings, a widget page's Delete) opens
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
  page; `--open` opens one group (the first, or the view or display of that name) as a link does;
  `--views n` grows the demo profile to n views (long and short names, some with icons);
  `--flyout "Views on the bar"` draws that row's flyout panel under the group.

Needs a manual pass: dragging a view's group onto another on the Views page; each "…" menu action
(Show on this display, Move up/down, Duplicate, Delete) and the right-click menu; View settings from
a view bar chip landing on the opened group; Edit layout from a group; the
breadcrumb's crumbs and Back from a widget's page; the "Views on the bar" flyout opened live
(ticking, the "All views" box, scrolling, keyboard).

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
  Duplicate), Arrange (front/back), AddView, DeleteView, DuplicateView, ReorderView.
- **[S-885]** Ctrl+Z undoes, Ctrl+Y and Ctrl+Shift+Z redo, in edit mode only. Ctrl+Z during a
  placement cancels the placement. Each step shows a keyed "Undid …"/"Redid …" notice offering the
  opposite; with nothing to do, "Nothing to undo"/"Nothing to redo". Deleting widgets or a view
  shows "Deleted …" with Undo (Delete still asks nothing first). A notice's Undo/Redo acts only while
  the history's `Version` is the one it was offered at; otherwise it says the desktop changed and
  points to Ctrl+Z.
- **[S-886]** A step restores the arrangement (`LayoutShape`): views (id, name, icon, order) and every
  widget's kind, geometry, z-order, header, surface and title. Not content: a widget that
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
  recovery show), as the original's: two views, so switching views is discoverable, named Home and
  Work (translated, `shell.view.homeName`/`workName`); Home is active. Home is the original's less its
  guide: Clock (0.78, 0.04, 0.20×0.16) first, Folder on the user's Desktop folder (0.02, 0.04,
  0.24×0.50), Shortcuts (0.80, 0.80, 0.18×0.16) with Recycle Bin, This PC, Downloads, Documents,
  Pictures and Home (shell targets, labels empty so the names are the translated target names).
  Work has no folder: Clock (0.78, 0.04, 0.20×0.16) first, Calendar (0.02, 0.04, 0.50×0.72), Tasks
  (0.54, 0.04, 0.20×0.58), Notes (0.54, 0.64, 0.20×0.28); Calendar and Tasks show Set up until an
  account exists ([S-1860]). Every widget is inside the screen, none overlaps another, load
  validation changes nothing, and both views together stay inside the free tier ([S-1065]).
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
- **[S-1623]** *(Retired: the power tile and its side panel are gone, [S-1910].)*
- **[S-1624]** Mixer on an Expanded-height card: when every one-line app row fits, the rows share
  the height (`MixerFill.RowHeight`, at most 64 px) instead of stopping short, and a row of 56 px or
  more has a caption under the name (`MixerFill.Detail`: "Playing · 3 sessions", "Silent", "Muted",
  "Windows sounds"; the session count stays the pill). A list that does not all fit keeps the base
  height and scrolls. What a short list still leaves stays empty: no hint or link to Windows' mixer
  fills it (user decision: a widget shows its own content, not filler).
- **[S-1625]** Checked against the original and **not built, because the original never had it**:
  Sun & moon 7-day table (original: sunrise/sunset + phase only; moonrise/moonset came later with
  the Moon widget, [S-3104]); Scratchpad
  export as PNG (the original only persisted its canvas as a PNG data URL); Clock world-clock rows
  (one zone per clock); Video stream save-frame button and fps for MP4/HLS (the original was an
  `<img>` over MJPEG/snapshot URLs, no such controls); Tasks Overdue/Today group headings (the
  original's Today list was one list, oldest first); Clipboard pin (the original had no pin).
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
  (tail first, clickable), no status footer ("24 items · 2 selected", retired [S-666]); Huge width: grid tiles one size
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
- **[S-1993]** Type-ahead ([S-1980]) over the shown names: the match is selected alone and focused,
  realised if it is past the built rows, and scrolled into view. Not while renaming.
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
  the first, squeezed when the length has no room for that ([S-1760]). A card that draws nothing
  does not animate.
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
  paths, virtual files, links or shell items) is refused everywhere. The view hit is the card under
  the pointer.
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
  Duplicate, Settings and Delete, acting on the selection ([S-1592]); the list is the view's own
  commands, Bring to front / Send to back, Move, and Edit layout / Done editing.
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
  or the point of a right-click on the surface — and flips above only
  when there is no room below.

Needs a live pass (pointer and focus on the real desktop): the surface menu opening only on empty
surface and at the pointer, Add widget's picker at the click then placing, the right-click that
cancels a placement opening no menu, keyboard use of the menus (arrows across the icon row and into
the list, Menu key / Shift+F10 on rows), submenus opening beside their line, a click on a card
following an open settings window (and a double-click still leaving edit mode), and both menus on a
second display with another DPI.

## Icon picker and showcase views (R4-ICONS)

The icon a view is given, chosen from the whole Segoe Fluent Icons set, and a Debug tool
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
  overlap, on whole DIPs.
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
  timer; Quote with no quote left; Feed with no feed
  address; Stocks with no symbol; Weather (now and forecast) with no place; Video stream with no
  address; Shortcuts with no item; Pictures with no folder (folder source) or no
  file (file source). "Nothing right now" (no unread
  mail, no events today, an empty folder) is not setup, and neither is set-up-but-wrong (a symbol
  that is not one, a folder that is gone): those keep the widget's own sentence. Widgets that start
  with seeded content (quotes, shortcuts, the Sun's and the Moon's place, the folder
  widget's profile folder) never ask until it is emptied. Habits and a local task list are started
  from the widget itself and do not ask.
- **[S-1861]** An account widget needs setting up while no signed-in account offers its feature: Mail,
  Calendar, Contacts, Tasks (account source only). The features are the union of the signed-in providers' (`WidgetSetup.Offered`); an
  account that needs signing in again still counts (the widget says so itself, with the Accounts link).
- **[S-1862]** Such a widget's body is the Set up button and nothing else: the widget's glyph over one
  accent button ("Set up"; tooltip "Open this widget's settings"), centred, no sentence. The glyph is
  dropped on a body too short for both; the button stays at every size, XS included. A click opens
  the settings window on the widget's page (opening the window if it is closed), the same request as
  the card menu's Configure; the button takes no focus, so it works outside edit mode on the
  `WS_EX_NOACTIVATE` surface. The card owns it, not the view: the view underneath is parked, not
  running ([S-1881]), invisible and not hit-testable, and the button goes the moment the config (from
  settings, or a change the view saves itself such as a shortcut dropped onto it) or a newly
  signed-in account makes the widget useful. The views no longer carry their own "add one in settings" sentences.

- **[S-1863]** There is no widget gallery: no `--gallery` window, no `--gallery-png` render, no
  toolbar entry, and no `#if DEBUG` sample data inside widget views or services. A widget's layout at
  the seven size tiers (`WidgetTiers`) is checked on the showcase views ([S-1810]) on the live
  desktop, or off-screen with `--widget-png` (one card at every tier, default config, [S-1917]); the other debug render flags (`--settings-png`, `--picker-png`, `--viewbar-png`,
  `--notices-png`, `--menu-png`, `--iconpicker-png`, `--motion-png`, `--layout-editor-png`) stay and
  draw widgets with their default config. The tags that described the gallery's fixtures are retired.

Needs a manual pass: the button on the live desktop outside edit mode (opens Settings on the right
page, from a closed and from an open window), and the button going away when an account is added.

## Opening a widget's app (R7-OPEN, retired)

Removed again by the user's decision, with its table, launcher, hover affordance, strings and tests.
Outside edit mode a click on a widget's body or header does nothing at the card level; the widget's
own buttons, rows, links and controls keep their clicks, and a double-click enters edit mode at once
([S-65]). The tags are not reused.

- **[S-1920]** *(Retired: a click on a widget opens nothing.)*
- **[S-1921]** *(Retired: no click targets.)*
- **[S-1922]** *(Retired: no hand cursor, hover wash or tooltip on the card.)*
- **[S-1923]** *(Retired: views report nothing to open.)*
- **[S-1924]** *(Retired: no widget-click launcher or its gate.)*
- **[S-1925]** *(Retired: a click is not delayed by the double-click time; Enter under the lease goes to the view only.)*
- **[S-1926]** *(Retired: music opens no app.)*
- **[S-1927]** *(Retired: weather, stocks and the stream open no page.)*
- **[S-1928]** *(Retired: account widgets open no service.)*
- **[S-1929]** *(Retired: folder and pictures open no folder.)*
- **[S-1930]** *(Retired: a feed does not keep its site.)*

## Removed widgets (R7-REMOVE)

Taken out of the app by the user's decision, with everything that existed only for them (views,
settings pages, services, Core rules, glyphs, strings in all nine languages, tests). Their tags are
retired with them and are not reused.

- **[S-1902]** Removed: Themia (the mark), Help (getting started), Claude usage, GitHub (repository,
  issues, notifications, reviews), the view switcher (the view **bar** stays, [S-906]..[S-916]),
  Group, Stack and Tabs, the mood tracker, the colour swatch, web search, OneDrive, Apps, the
  calculator and the process list. `WidgetKind` keeps the numbers of the kinds that remain. Retired
  with them: [S-100]..[S-105], [S-639], [S-120]..[S-126], [S-131], [S-410]..[S-419], [S-480]..[S-492],
  [S-510]..[S-512], [S-514], [S-537], [S-606]..[S-609], [S-654], [S-790]..[S-796], [S-810],
  [S-861]..[S-871], [S-875], [S-876], [S-951]..[S-956], [S-1144], [S-1145], [S-1151]..[S-1155],
  [S-1290]..[S-1294], [S-1449]..[S-1455], [S-1562], [S-1565], [S-1566], [S-1570], [S-1573], and the
  sections on the developer widgets, the GitHub widgets, the calculator keypad and colour swatch
  modes (PK), Apps/Search/Help/Themia (PI-A) and the view switcher (PI-B).
- **[S-1903]** The model is flat: a widget has no parent, and nothing holds other widgets. There is no
  "Move into", no dropping a widget into another, no picker target but the desktop, and no side
  header (`HeaderPlacement` is Top, Bottom or Hidden). A selection is copied, moved, layered and
  deleted widget by widget.

## Offline sync (SYNC)

The account and web widgets draw from a store on this PC that background syncs keep filled, instead
of asking the network themselves. Owning files: `Themia.Core/Sync/*` (vault, sealing, shelves,
queue, schedules, merges, interests), `Themia.App/Services/Sync/SyncHost.cs` (key, network signal),
`Services/Mail/MailService.cs`, `Services/Office/AccountOfficeSource*.cs`, `Services/Tasks/TaskService.cs`,
`Services/Web/{AnswerSync,FeedSync,WebSync,NetworkCache,WeatherService,WebCache}.cs`. Tests:
`OfflineSyncTests`, `SyncHousekeepingTests` (`S26xx_`).

- **[S-2600]** One offline store per host (`SyncHost`, `AppServices.Sync`). Mail, calendar, contacts,
  tasks, feeds, weather and quotes have one sync service each, which is the only code that asks a
  provider for them; widgets read what is stored through the service's subscriptions
  (`IMailFeed.Updated`, `IOfficeSource.Changed`, `TaskFeed.Changed`, `FeedSync.Updated`,
  `WebSync.Updated`). A view switch, a card rebuild, a
  resize, a restart and a lost network all draw the stored copy at once and ask nothing.
- **[S-2601]** The store lives in `%LOCALAPPDATA%\Themia\sync\` for the app's own state, and in
  `sync\` beside any other state file (a portable or test profile takes it along, as it takes its
  accounts). A host over a detached state (every debug render) gets a detached store: nothing read,
  nothing written. It is never in an export or the settings file.
- **[S-2602]** Every shelf is sealed with AES-256-GCM (`SealedBlob`: `TSB1` · 12-byte random nonce ·
  16-byte tag · ciphertext) under one random key per install; the associated data is the shelf's
  `kind|owner`, so a shelf copied over another account's does not open. Chosen over DPAPI per shelf
  because shelves are rewritten after every sync and can be megabytes: one key in memory, no per-call
  derivation, and the format is tested in Core.
- **[S-2603]** The key is 32 random bytes sealed with DPAPI for the current Windows user in
  `sync\vault.key` (written temp + move). A key that does not open here (another user, another PC,
  damage) is set aside as `.corrupt` and replaced; the shelves it sealed then fail to open and resync
  ([S-2605]). A key that cannot be made leaves a detached store for the run, logged as an error;
  nothing is ever written unsealed.
- **[S-2604]** A shelf is one data kind (`mail`, `calendar`, `contacts`, `tasks`, `web`) for one owner
  (an account id, or the web caches), in `<kind>-<first 16 bytes of SHA-256(kind|owner), hex>.shelf`,
  so no address appears in a file name. Each document carries `schemaVersion` and `owner` and is
  source-generated JSON (`SyncJsonContext`).
- **[S-2605]** Load tells absent from unusable: no file is a fresh owner, synced on first need; a
  file over 64 MB, not sealed, not opening with this key and context, not JSON, of another schema or
  another owner is logged, moved aside as `.corrupt` (one copy kept) and the owner resyncs. A file
  that cannot be read (locked) is left in place and the owner starts empty for the run. Every shelf
  that opens is validated by its kind's rules ([S-2621], [S-2632], [S-2641], [S-2651]); what is
  dropped is counted in one log line.
- **[S-2606]** Writes are queued per shelf (the newest snapshot replaces a queued one) and drained by
  one background writer: serialise, seal, temp file, move over the shelf. A failed write keeps the
  previous shelf whole. The app flushes the queue on exit. Shelf objects handed to the writer are
  built fresh per save and never changed after.
- **[S-2607]** Signing an account out purges every kind of its shelf and their set-aside copies
  (`AccountService.SignedOut` → `SyncVault.Purge`), through the same queue, so a write already out
  lands first and none queued after resurrects it; a sync that finishes for an account no longer
  signed in does not save.
- **[S-2608]** What is synced is what every widget in every view needs, shown or not
  (`SyncInterests.From(state)`): each Mail widget's accounts, folder and count, each calendar's
  accounts and calendars (and birthdays), each contacts widget's accounts, each account task list
  (and Today), each feed address, each weather place and each Stocks symbol. The services hear only a real change of these (`Key()`), not every
  state write. A view the user switches to is already synced. This is the second exception to
  [S-1880] (after screen time): syncing continues for hidden views, on the shared ticker, which stops
  while the desktop is hidden; what no widget reads any more stays stored but is not synced.
- **[S-2609]** Each synced item has a `SyncSchedule`: at start it is due only when its stored copy is
  older than its interval (so a restart a minute after a sync asks nothing); after a success the
  interval; after a failure the shared `Backoff` (a 429 far longer, a refusal only the user can mend
  held an hour, [S-588]); when Windows reports a network again (`NetworkAvailabilityChanged`, or an
  address change on a live network) every wait that was only about the network ends; Refresh goes
  now unless the provider said "slow down". Showing a widget is not a trigger, except for an item
  never synced at all.
- **[S-2610]** Changes (mark read, move, delete a message; complete, reopen, rename, delete, add a
  task) show at once and wait in the owner's queue, persisted in its shelf (`PendingWrite`), until
  the provider has them. Strictly in order, head only, one at a time. Offline, unreachable, timed
  out, throttled, the server's trouble or a sign-in to mend: kept and retried after 30 s, 1, 2, 4 …
  30 min (or at once when the network returns); refused (gone, forbidden, malformed): taken back and
  the header says why. At most 500 queued; the next is refused in words. A queue restored at start
  is shown again and resent.
- **[S-2611]** A new change folds into what is still queued for the same thing: a second tick or read
  flag replaces the first, a rename replaces a rename, a delete swallows the task's queued tick and
  rename, and a task created and deleted before either reached the provider leaves nothing. The head
  is never folded while it is on the wire. A confirmed create re-addresses later changes to the
  provider's id.
- **[S-2612]** A stored copy the last sync could not improve on says when it is from: "Could not
  refresh Work: no network. Updated 10:42." (mail), "Work: No network · updated Mon 10:42"
  (contacts, calendar [S-2664]), "Last refresh failed: … Showing items from …" (tasks), the answer's
  age in the caution colour (Rss, weather, Stocks). Times through `LocaleFormat`: a clock time today, weekday and time this week, else a
  short date. Changes that had to wait say "3 changes waiting to sync" in the header; a change on its
  way out says nothing.
- **[S-2620]** Mail: a shelf per account with the newest messages of every folder a widget reads, at
  the largest count asked this run (at most 50 a folder, [S-540]): id, sender, subject, date, read
  flag, the 200-character preview and the web link — never a body — plus the folder's counts and the
  queue. The probe/fetch rule of [S-546] decides when the list is fetched again.
- **[S-2621]** A mail shelf on load: listed folders only, each once; messages need an id, appear once
  and stop at 50; strings are cut to their bounds (preview 400, subject 1000); negative counts are
  dropped; the queue keeps mail changes whose folder is a known name.
- **[S-2622]** What a folder shows is the stored list with confirmed changes kept on top until a
  fetch that started after them lands, and queued changes on top of that, in order: a read flag sets
  the flag, a move takes the message out of the folder it was in.
- **[S-2623]** Incremental where it is cheap: the counts probe skips unchanged folders (every
  provider), and Gmail asks metadata only for messages it does not hold yet — the read state of the
  rest comes from one list of the folder's newest unread ids. Graph and IMAP read only what changed
  when the counts moved or the list is old ([S-2687], [S-2689]).
- **[S-2624]** A mail change waits in the queue ([S-2610]); the first attempt of a change at the head
  answers the menu click (refused → the header warning), later outcomes reach the header.
- **[S-2630]** Calendar: a shelf per account with its calendar list, every synced calendar's window
  of events (id, subject, start, end, all-day, location, organizer, web link; at most 5000, nearest
  the fetch kept) and birthdays.
- **[S-2631]** A calendar is held from local midnight a year before today to local midnight after a
  year ahead (`CalendarSync.Window`), so every layout — month, week, day, agenda, a year — reads it
  without asking. The weeks around today (7 back, 42 ahead) are refreshed every 5 minutes, the whole
  window every 6 hours (and on Refresh); a near refresh replaces exactly the events it covers (and
  any it returns by id), keeps the rest and widens the window; one that does not touch the held
  window starts it again. A request inside the window is answered from the shelf, whatever it holds;
  a range outside it (paged more than a year away) is fetched on demand and held in memory only.
  Calendar lists are refreshed every 6 hours when the settings page asks, birthdays daily. A
  calendar whose provider reports changes ([S-2684]) is instead refreshed every 5 minutes as one
  round over the whole window; one never synced still draws the weeks around today first.
- **[S-2632]** A calendar shelf on load: calendars need an id (once, at most 200), colours are
  normalised; a span needs a non-empty window and appears once per calendar; events need an id,
  appear once, and an end before the start becomes the start; birthdays need a name and a real month
  and day.
- **[S-2640]** Contacts: a shelf per address book with up to 1000 people (the most the widget asks
  for, [S-583]): id, name, email, phone, job title, company. No photos: the widget draws initials
  ([S-586]). Refreshed every 15 minutes, by its changes where the provider reports them ([S-2685]).
- **[S-2641]** A contacts shelf on load: people need an id and a name, appear once, stop at 1000.
- **[S-2650]** Tasks: a shelf per account with its lists, the tasks of every list a widget reads (at
  most 400 a list, 50 lists) and the queue. One sync per account reads the lists, then each wanted
  list in turn, at the fastest refresh any of its widgets asks for (30–600 s); a list that fails
  while others answer keeps its stored copy. Every widget on the account — a list and Today — shares
  the account's tasks and pending changes. Each list is read by its changes ([S-2686]).
- **[S-2651]** A task shelf on load: lists need an id (once, at most 50); held lists need an id and
  appear once (never Today); tasks need an id, appear once per list, stop at 400; the queue keeps
  task changes with a list, and a rename or add with a title.
- **[S-2652]** The lists a sync reads: each list a widget shows (an empty id is the default list),
  every list once Today is shown (Today is built from all of them, [S-596]); finished tasks only for
  a list a widget shows them for, so finished ones cannot push open ones past the bound.
- **[S-2653]** A task change waits in the queue ([S-2610]) with its optimistic edit on screen; a
  restored queue is shown again on start. An added task keeps its temporary row (pinned at the
  bottom, [S-599]) until the provider confirms it.
- **Incremental sync.** Owning files: `Themia.Core/Sync/{DeltaSync,DeltaMerge}.cs`,
  `Core/Office/DavDelta.cs`, `Core/Mail/ImapDelta.cs`, `Services/Office/AccountOfficeSource.Delta.cs`,
  `AccountService.GetDeltaAsync`, `MailService.FetchGraphAsync`, `ImapMailClient.SyncAsync`,
  `AccountTaskRemote.SyncListAsync`. Tests: `IncrementalSyncTests` (`S268x_`).

  | Provider | Calendar (window) | Contacts | Mail (newest N a folder) | Tasks (a list) |
  |---|---|---|---|---|
  | Microsoft | `calendarView/delta` for the default calendar; a chosen calendar: full ([S-2631]) | default folder `contacts/delta` (book under 1000) | `messages/delta` per folder, bounded to the oldest held date | `tasks/delta` |
  | Google | events `syncToken` | People `syncToken` (book under 1000) | incremental metadata ([S-2623]), no history API | `updatedMin` |
  | CalDAV / CardDAV | `getctag` / `sync-token`, then `sync-collection` or an etag listing, then a multiget | same | — | — |
  | IMAP | — | — | CONDSTORE gate; flags of held UIDs + arrivals since UIDNEXT | — |

  Birthdays (daily), calendar lists (6 h), outside-window pages and a chosen Graph calendar are full reads.
- **[S-2680]** A provider's change cursor (`SyncCursor`: kind, value, scope, `Since` the full fetch
  the chain grew from, `At` its last move; DAV adds the sync-token and each resource's href, etag and
  item ids; IMAP the folder's UIDVALIDITY, UIDNEXT, EXISTS and HIGHESTMODSEQ) is kept in the owner's
  shelf beside what it describes — a calendar's span, a contacts book, a mail folder, a task list —
  and sealed with it ([S-2602]). It has its own format version. A read that did not reach its last
  page (the bound, the page ceiling, a next page on another host) leaves no cursor.
- **[S-2681]** On load a cursor of another format or kind, over its bounds, without the parts its
  kind needs, or (Graph) not an https address on `graph.microsoft.com` is dropped and counted with
  the shelf's other drops ([S-2605]); its data stays. A cursor is used only for its kind and scope (a
  calendar's window, a folder and its count, a list and whether finished tasks are held). Missing,
  of another scope, stale ([S-2682]) or refused by the provider (410 Gone; a 400 naming the token,
  as Google People's "Sync token is expired") means a full read within the bounds at once, which
  starts a new cursor; a refusal is logged once at Info, a scope change or the daily resync at Debug.
  A refusal is never the account's failure.
- **[S-2682]** However well deltas go, everything is read in full again once its chain is a day old
  (`SyncCursors.ResyncAfter`), as a safety net for a change a provider never reported; a chain
  started in the future (the clock moved back) is stale too. A day because the calendar window moves
  on at local midnight anyway (its scope changes) and every provider's token outlives it.
- **[S-2683]** Change pages are read through `DeltaPages` (the trust boundary): Graph `value` /
  `@removed` / `@odata.nextLink` / `@odata.deltaLink`, Google `items` or `connections` with a
  cancelled event, a `metadata.deleted` person or a deleted or hidden task read as removed, and
  `nextSyncToken`. A body that is not a JSON object is a bad answer; non-object items and removals
  without an id are skipped; a cursor beside a next page is not the end of the round.
- **[S-2684]** Calendar: a changed event replaces the one held under its id when it still overlaps
  the window and leaves when it moved out; a removed or cancelled one leaves; the window stays and
  both refresh stamps move on; at most 5000 events, and a window at the bound keeps no cursor.
- **[S-2685]** Contacts: changes upserted by id, removals out, name order, at most 1000; a book at
  the bound keeps no cursor, so a book larger than the widget can show is read in full each time.
  Graph lists the first 1000 by name as before and, when that is the whole book, starts the default
  folder's delta round, whose answer is what is held.
- **[S-2686]** Tasks: a changed task keeps its place, a new one joins at the end, a removed, deleted
  or hidden one leaves, and one finished elsewhere leaves a list held without finished tasks (and
  comes back reopened); at most 400, a list at the bound keeps no cursor. Graph reads every list as a
  delta round (one path: from the start, or from its link); Google reads the list in full with an
  `updatedMin` cursor five minutes before the request, then the tasks updated since (finished, hidden
  and deleted ones included). Queued changes still win on screen ([S-2653]): they are laid over what
  is held, never folded into it.
- **[S-2687]** Mail (Graph): the folder's delta is folded into the newest N (upsert, removals out,
  newest first, cut to N); a list left shorter than the folder could fill (its total from the probe)
  is read in full. In full, the newest N are listed and a delta round bounded to their oldest date
  (none when the folder holds fewer than N) starts the cursor. A change to a message older than the
  list does not push in. Queued and confirmed changes stay on top as before ([S-2622]).
- **[S-2688]** CalDAV / CardDAV: the collection's `getctag` (else its `sync-token`) unchanged means
  nothing is read. Changed: an RFC 6578 `sync-collection` from the stored token where both sides have
  one (a refused token — 400, 403, 409, 410, 412, 507 — falls back), else an etag listing over the
  window compared with the held etags; only new or changed resources are read, by multiget, a hundred
  at a time; a resource listed as gone or not returned takes its items with it, unless another
  resource yields the same id. A card without a UID is named by its href.
- **[S-2689]** IMAP: in one session, EXAMINE (with CONDSTORE where the server offers it) gives
  UIDVALIDITY, UIDNEXT, EXISTS and HIGHESTMODSEQ. No mark or another UIDVALIDITY (the UIDs held now
  name other messages) is the newest N in full; with CONDSTORE the same HIGHESTMODSEQ, UIDNEXT and
  EXISTS read nothing; otherwise the flags of the held UIDs are fetched (one that does not answer was
  expunged) and only messages at or above the stored UIDNEXT (newest N of them) are fetched whole. A
  removal that leaves the list short is read in full. QRESYNC is not used: the flag fetch of at most
  50 UIDs already finds expunges.
- **[S-2660]** Feeds, quotes and weather keep their documents ([S-1300], [S-729]: same validation,
  same ages) in the sealed `web` shelves of the store, no longer as plain `network_cache.json` /
  `weather_cache.json` files.
- **[S-2661]** Rss: `FeedSync` (`AppServices.Feeds`) keeps every feed of every Rss widget fresh on
  the shortest refresh any widget asks for, wakes with the network and goes now on Refresh; the
  widget draws what is held, hears the updates and asks nothing on attach (a feed address never
  fetched yet is the exception). The spinner shows only while a fetch for one of its feeds is out.
- **[S-2662]** A restored answer (feed, quote, weather) is fresh for its real age: a launch within a
  widget's refresh interval of the last fetch asks nothing; it is still drawn in the caution colour
  with its saved age until a fetch replaces it. The caches are restored before the web syncs start,
  and the syncs start only in the app (after the request guard reads the user's choice), never in a
  debug render.
- **[S-2663]** WeatherNow, WeatherForecast and Stocks: `WebSync` (`AppServices.WebSync`) keeps every
  reading, forecast and quote of every such widget in every view fresh (`SyncInterests.Weather`,
  `.Quotes`; a widget with no place or no usable symbol asks nothing), on the shortest refresh any
  widget on the same answer asks for (weather 5–240 min, quotes 1–120 min), backing off on the
  shared `Backoff`, waking with the network and going now on Refresh. Feeds and these share one
  scheduler (`AnswerSync`). The widget asks the sync for the answer of its own config
  (`SyncInterests.For`, the same rule), draws what is held, hears `WebSync.Updated`, and its tick only
  redraws (age, day labels); it never fetches. Offline or failing, the last answer stays with its
  age in the caution colour and the failure in the tooltip ([S-2612]). The quote and weather caches
  are unchanged.
- **[S-2664]** Calendar: a stored copy the last sync could not improve on says when it is from in the
  header's warning, as contacts do: "Work: No network · updated 10:42" (`SyncFreshness.Stamp`). The
  problems of one account's calendars fold to one line per account and problem, stamped with the
  oldest stored copy among them (`SyncFreshness.Fold`); a calendar with nothing stored adds no stamp.
- **[S-2665]** At start, after the accounts are loaded, shelves (and set-aside copies) of no account
  that exists are deleted (`SyncVault.Sweep`, a crash between a sign-out and its purge, [S-2607]):
  the vault hashes every existing account with every account kind and the web owners, and any
  `<kind>-<hash>.shelf[.corrupt]` beyond those names goes; the key, temp files and anything not named
  like a shelf are never touched. Through the write queue, owners read when it runs. One log line
  with counts per kind, no owner. The two web shelves are shared caches of every widget of their
  kind (bounded, aged out on load), not one per widget, so they are never swept.
- **[S-2666]** No sweep unless the account list loaded whole (`ShelfSweep.MayRun`): no accounts file
  (perhaps unreachable, not gone), a file set aside as unreadable or damaged, a read-only store, or
  a load that repaired the file (a dropped damaged account is not a signed-out one) sweeps nothing.
