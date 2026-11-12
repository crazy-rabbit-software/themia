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

- **Foundations.** Geometry model; Rendering; Grid snap aid; Constraints; Defaults & placement; Views & displays; Window layering; Transparency; Ticking; Live widgets and resource use (R6-RESOURCES); Memory; Persistence; Failure rules; Not on this machine (UNAVAILABLE); Clipboard & transfer between displays; No backwards compatibility; Widget modules and the registry (MODULES); The widget kit (KIT); The service layer (SERVICES).
- **Shell, windows and settings.** Displays (WP2); The edit bar and its pages (EDITBAR-PAGES); Desktop integration and suspend (WP3); Appearance engine (WP1); Notices, resilience and undo (WP4); Widget picker: search (WP5); Multi-select and the placement session (MULTISELECT-PLACEMENT); Widget styles (STYLES); Context menus (R4-MENUS); Views and the view bar (WP8); The Displays and Views pages (R4-NAV, R5-PAGES); Icon picker and showcase views (R4-ICONS); Keyboard lease (WP11); Widget keys (WIDGET-KEYS); Item selection (ITEM-SELECTION); View links (VIEW-LINKS); Keyboard in the shell (SHELL-KEYS); Motion: intros, hyperspace, hacker (WP9-MOTION); Desktop surface drops (WP10-SURFACE); The desktop layer (DESKTOP); Opening a widget's app (R7-OPEN, retired); Opening what a widget is about (CARD-OPEN); Shell hooks (PA); Removed widgets (R7-REMOVE); Shell skeleton and shared settings; Groups and ready-made groups (GROUPS); Shared widget settings: region and updates (SETTINGS-AUDIT); Colours, fonts and uninstall (the settings window, retired); Settings navigation icons (NAV-ICONS, retired); Logging (WP7); Network safety (WP6); The assistants' server (MCP); Localization infrastructure (LOC-INFRA); Microsoft Store package and licensing; Gallery.
- **Look and layout.** Rows and the card edge; Looking like Windows; The widget header; The widget family: tokens and blocks (FAMILY); Widget sizing (SIZE); Responsive layout (shared foundation); Adding a widget; Layout pass over the ported widgets; Widget polish 1 (POLISH-1); Widget polish 2: leftover parity and layout gaps (POLISH-2); The Set up button (R5-SETUP); One sample day (SAMPLE-DAY).
- **Accounts and sign-in.** Where account data comes from; Accounts and sign-in.
- **System widgets.** (The seven meters, the seven detail widgets, Drive, Removable drives, Brightness, Mixer, Volume, Music, the Audio visualizer, the three screen-time widgets, Notifications and the Focus session are modules: their sections are their projects' `SPEC.md`; the sections named here keep the hub, the samplers and the rules they share, the SDK's.) System data hub (SYS-HUB); System meters; Meter framework and memory; CPU; Disk and ping; Battery and network; Network and ping; System widgets: GPU, mixer and screen time; Screen time: chart, donut and apps (SCREENTIME-SPLIT); GPU and screen time: the whole tile (SYS-P4); System details (SYS-DETAILS); Mixer and Volume (SYS-P5); Power and Bluetooth; Peripheral batteries (DEVICE-BATTERIES); Device and location (DEVICE-AND-LOCATION); Quick-setting toggles (QUICK-TOGGLES); Windows' panels: notifications, focus session, brightness (WINDOWS-PANELS).
- **Time, desk and launch widgets.** (Clock, Countdown, Stopwatch, Pomodoro, Quote, Sun, Moon, Scratchpad, Recycle Bin, Alarm, Notes, Clipboard, the Habit tracker, the Gallery, Recent files, Shortcuts and the Folder are modules: their sections are their projects' `SPEC.md`, `src/Widgets/Themia.Widget.<Name>/`; so are Bluetooth, the seven power buttons and the thirteen quick-setting toggles, under System widgets, whose shared rules stay here.) Clock; Countdown and pomodoro; Stopwatch, alarm and drives (KARPO-2); Sun and moon; Audio visualizer (VISUALIZER); Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ); Music and Pomodoro: parity and the whole tile (PF); Notes; Notes: note store and markdown editor; Clipboard and Countdown: the whole tile; Recent files (RECENT-FILES, the host's list); Recycle Bin (RECYCLE-BIN); Date (DATE); Habits; Shortcuts B: drop, reorder and app layouts (PD); Folder: the file browser (FOLDER, the host's file services); About and the settings file.
- **Mail, calendar, tasks and online widgets.** (Every widget here is a module: Email, Calendar, Contacts, Next meeting, Week planner, Tasks, RSS, Weather now, Weather forecast, Rain next hour, Air quality, Exchange rates, Stocks and the Video stream; their own behaviour is their projects' `SPEC.md`, and the sections named here keep what they share with the host: the mail, office and task services, the offline sync and the SDK's rules.) Mail; Calendar and contacts; Tasks; Mail: the whole tile; Calendar and contacts: the whole tile; Calendar: five styles and shared dates (CAL-SPLIT); Tasks: using the whole tile, and the rest of the original; Weather: current conditions and forecast; Feed, stocks and the web widgets: parity and the whole tile (OFF-E); Web widgets: video stream; Web widgets: size ladder and overlay; Offline sync (SYNC); Next meeting (NEXT-MEETING); Week planner (WEEK-PLANNER); Air quality, rain and exchange rates (ONLINE-2).

---

## Geometry model

- **[S-01]** A widget's position and size are stored as **fractions of its screen**: `x, y` is the
  top-left corner (below 0 or near 1 for a widget hanging over an edge, [S-6608]), `w, h ∈ (0, 1]`
  the size. `x`/`w` are fractions of screen **width**,
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
- **[S-32]** *(Retired by [S-6560]/[S-6563]: the per-widget surface enum (Card, Translucent, None) is
  gone; a widget's background is a fill choice, and Transparent drops the card and its border. Still
  true: nothing is acrylic per widget — acrylic is a window backdrop, so per-widget acrylic would cost
  one window per widget.)*

## Grid snap aid

- **[S-06]** The grid is an **aid**, never storage. `design.gridSnap` (default on) rounds the rect
  chosen during drag, resize and placement to the nearest grid line. Holding **Alt** suspends it for
  the duration of the gesture. Storage is always the raw fraction.
- **[S-06b]** The cell target is **10 DIP**, and the minimum widget **32 DIP**, matching the
  original app. This only sets how many snap lines exist; the drawn pitch is screen/cols, so it
  follows the display. At that density the overlay stroke has to be very faint, or the mesh reads as
  a grey wash over the desktop rather than as a grid.
- **[S-06a]** The cell overlay is drawn only in edit mode, and only when `gridSnap` is on.
- **[S-06c]** Every **5th** line (`GridGeometry.MajorEvery`), counted from the monitor's top-left
  edge (the origin the snap rounds from), is a **major** line: wider (`LineWidthsPx`: minor about
  1 DIP, major half as wide again, at least one physical pixel more) and a little stronger (0.26
  against 0.18 opacity), in the same theme brush. Purely visual: snapping stays on every line. Each
  line is placed on whole physical pixels (`CrispLine`) so it stays sharp at any DPI. The overlay is
  two geometry paths, rebuilt only on layout. `--grid-png <outDir>` renders it in both themes.

## Constraints

- **[S-08]** Minimum widget size is a **pixel floor** (`MinWidgetPx`, 32 DIP, [S-06b]), converted to a
  fractional floor at the moment of resize, so it means the same physical size on every monitor. It
  is every widget's last floor; a legacy kind has no other, a converted kind stops at its sizing's Min
  first ([S-2445]).
- **[S-09]** Widgets are kept **in reach** (`GridGeometry.Clamp`), not inside: at load against the
  whole screen, and against the display's **work area** for drag, resize, placement, paste, drop and
  the arrow keys ([S-841]), each by the one rule of [S-6608]. No widget is ever bigger than that area.
  Clamping is **total**: a non-finite or inverted rect collapses to a valid one rather than
  propagating NaN into storage. A converted kind is also held to its sizing: painted fitted into it
  ([S-2446]), resized within it ([S-2445]); its Max may be larger than the work area allows, and the
  work area wins.
- **[S-6608]** A widget may hang over **any edge** of its display, but always keeps at least
  `MinVisiblePx` (**32 DIP**, the [S-08] floor) of itself on the allowed area on each axis: a
  floor-sized widget is never cut at all, a bigger one always leaves a whole minimum widget's worth
  to see, grab and drag back. Pushed further, it slides back just that far, keeping its size; a
  resize stops the moved edge there. The minimum is kept on the **work area**, so nothing can be lost
  under the taskbar either; an area narrower than it keeps what it can hold. It is DIPs, so the
  same at any display scale. The surface clips the overhang at the work area ([S-851]): the hit
  region, the markers and the painting stop there. One surface per display ([S-21], [S-23]) means a widget
  hanging over the edge **towards a neighbouring monitor is clipped at its own monitor's edge** and not
  shown on the neighbour; moving it there is the hand-off of [S-76]. The rule holds at the moment of
  the gesture and at load (against a 1920 × 1080 reference): stored fractions scale with the display
  ([S-02]) and nothing re-settles on a display change ([S-841]), so on a much smaller display the
  overhang scales with it. (Tests `S6608_`.)
- **[S-6609]** While the snap aid is on ([S-06], Alt suspends it), the work area's edges are a
  **magnet**: a moved rect, block or resized edge within `EdgeMagnetPx` (**8 DIP**, under one grid
  cell) of one lands flush on it, judged on the raw position before the grid rounds it; past that the
  drag goes on over the edge. It catches an edge the grid misses (a taskbar's top is rarely on a grid
  line), now that the edge no longer stops a drag by itself. (Tests `S6609_`.)
- **[S-6610]** The properties panel ([S-3708]) anchors to the **part of the selection that shows**
  (its box cut to the work area), so it stands beside the visible part of a widget hanging over an
  edge and never beside an edge that is off the screen. (Tests `S6610_`.)
- **[S-31]** Edit mode draws **eight** handles: four corners and four sides. A corner drags one edge
  on each axis; a side handle drags **only its own edge**, leaving the opposite edge and the whole
  other axis untouched. An edge dragged past its opposite stops at the [S-08] floor instead of
  inverting the rect.
- **[S-31a]** Settling a resize snaps and limits **only the edges the handle moves**: a moved edge
  rounds to the grid (or lands on a work-area edge within the magnet, [S-6609]), may pass the
  work-area edge, and stops where the widget would outgrow the work area or keep less than the visible
  minimum on it ([S-6608]); an anchored edge never moves. A converted kind's moved edge also stops at its
  sizing's Min and Max and lands on its steps, a fixed axis has no handle ([S-2444], [S-2445]). Snapping the whole rect rounded the anchored
  edge too, and clamping the whole rect pushed the widget about instead of stopping the moved edge.
- **[S-31b]** Edit mode draws no outline round a widget. The eight handles are its markers
  (`EditMarkers`): a **square-cornered L-bracket** at each corner (arms 14 DIPs, never rounded, whatever
  the card's radius) and a **16-DIP bar** at the centre of each edge, about 3 DIPs thick and
  **centred on the cell's edge** (half outside, half inside, in whole device pixels), each over a
  1-DIP contrast halo (`SolidBackgroundFillColorBase`) so it reads on any wallpaper. Two widgets that
  meet on an edge or a corner draw their marks there on the same pixels: one line, a T or a cross,
  never two marks with a seam. So the surface draws every card's ink on one layer above all cards
  (`EditMarkerLayer`: every halo, then every bar; the grips stay on the cards), a selected widget's
  bars over an unselected neighbour's, and the neutral bars in the strong stroke as it shows over the
  halo, made opaque (`EditMarkers.Over`), so overlapping bars do not brighten. A side too short for two arms, a bar and
  4-DIP gaps shrinks the arms and bar alike (never below half the thickness). Every edge lands on a device pixel at the window's scale. A handle's grip is
  its ink plus the old 10-DIP square on its corner or edge midpoint; grips never overlap. Cursors and
  the keyboard resize are unchanged. (Tests `S31b_`.)
- **[S-31c]** Every widget in edit mode shows its markers: a selected one in the accent
  (`AccentFillColorDefault`), every other one in the neutral strong stroke (`ControlStrongFillColorDefault`).
  Each widget of a multi-selection shows accent markers. `--editbar-png` writes `marks-<theme>-r4.png`,
  `-r16.png` and `-multi.png`.

## Defaults & placement

- **[S-10]** A widget kind states its sizes ([S-2438]); a new widget lands at its **Natural** size in
  DIPs (`WidgetDescriptor.Natural`, the default config's; `DefaultWidth`/`DefaultHeight` are gone). At
  placement that is converted to a fraction of the current screen, so a newly dropped widget has a
  consistent physical size on any monitor, and scales per [S-02] thereafter (a converted kind then
  painted fitted into its sizing, [S-2446]).
- **[S-11]** Placement centres the widget on the drop point, then snaps ([S-06], [S-6609]) and keeps
  it in reach ([S-09]): one dropped near an edge or on the taskbar may hang over it, but keeps the
  visible minimum on the work area ([S-6608]). The ghost shows exactly that.

## Views & displays

- **[S-12]** Views are switched as a whole. Each holds its own widget list. The last view cannot be
  deleted — a desktop with no view has nowhere to put a widget.
- **[S-13]** A widget's properties menu ([S-3719]) edits the widget's **live** config and saves as
  each pick is made — there is no Cancel. It does not mirror the config into local state and sync it
  back. Live, not a copy, because several widgets write their own config as they are used (a note's
  text, a habit's check-offs): a menu holding an older copy would overwrite those on save.
- **[S-13a]** Because the menu edits the object a view already holds, a view must never decide what
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
- **[S-33e]** Both holds ([S-33c], [S-28]) last only while the user's windows are still away. Once an
  ordinary application window is on screen again (same test as [S-33a]) the pin drops, whoever has
  the foreground. Show Desktop can end with the taskbar, Task View or a widget still in front (the
  taskbar's Show Desktop button, a second Win+D, a three-finger swipe up): the windows came back,
  nothing handed them the foreground, and the held pin drew the widgets over them until a click
  (original issues #132, #167). `ZOrderPolicy.Decide(..., applicationOnScreen)`.
- **[S-34]** The surface is kept **uncloaked**. A fullscreen exclusive application hides windows by
  asking DWM to cloak them — a path that goes through neither `SC_MINIMIZE` nor `WM_SHOWWINDOW`, so
  no window procedure sees it. A cloaked window is still `IsWindowVisible`, still not iconic, still
  at whatever z-order it was put at, and simply never composited: no amount of z-order work recovers
  it. `DWMWA_CLOAKED` is read on every check and `DWMWA_CLOAK` cleared when it is set (logged with
  the cloak's reason). Note this is **not** what fixes Show Desktop on this build of Windows, which
  minimises instead ([S-33a]); it is the guard for the fullscreen case.
- **[S-34a]** The controller is **event-driven, with no timer** (it had a 1 s backstop and a 100 ms
  burst cadence). Every input of the decision changes only through a window-manager operation that
  announces itself once it is done, and each announcement only queues one coalesced check on the UI
  thread (`DesktopZOrder`); the check reads everything fresh ([S-33a]), so a burst costs one check
  and an event that lands mid-transition is followed by the one that ends it. Which events count is
  pure (`ZOrderEvents.Classify`):
  - `EVENT_SYSTEM_FOREGROUND` (other processes): Show Desktop starting or ending, a game quitting,
    Start opening. The one WinEvent that refills the repairs ([S-34d]).
  - `EVENT_SYSTEM_MINIMIZESTART`/`END` (other processes, top-level windows): the user's windows
    leaving or coming back, which decides the lift ([S-33a]) and its end ([S-33e]).
  - `EVENT_OBJECT_REORDER` on the desktop window (other processes): any change to the top-level
    z-order — the desktop raised by Show Desktop, a late `WorkerW` raise ([S-34b]), an application
    sent to the bottom ([S-34c]). The same event from a list view or toolbar is ignored.
  - `EVENT_OBJECT_CLOAKED`/`UNCLOAKED` (every process, our own included, top-level windows): our
    surface cloaked ([S-34]), or the windows hidden or brought back by cloaking on builds that cloak.
  - `EVENT_OBJECT_SHOW` (other processes, top-level windows), **only while lifted**: an application
    shown without taking the foreground is back on screen ([S-33e]). It also fires for carets and
    cursors, so it is not heard the rest of the time.
  - From the command window, not WinEvents: `WM_DISPLAYCHANGE`, the work area, `WM_DPICHANGED`,
    `TaskbarCreated` (an Explorer restart), the session unlock and resume from sleep
    (`DesktopZOrder.Refresh`, which also refills). The host's own changes (a surface shown, edit mode
    switching its activation) check at once.
  - Out-of-context WinEvents are documented as queued to the hooking thread and delivered in order;
    the old claim that one is silently dropped under load (a fullscreen game) was never measured (the
    original's own test calls that path impossible to simulate); the timeout Windows documents is the
    low-level input hooks'. Should one be lost anyway, nothing is decided from it alone: the next
    event of any kind repeats the whole check, uncloak included.
- **[S-34b]** A secondary monitor's `WorkerW` can rise into the topmost band *after* the surfaces are
  lifted. That raise is a top-level z-order change, heard as a reorder ([S-34a]); while lifted, a
  check re-raises a surface only when a desktop-layer window (`Progman`, a `WorkerW`) on screen is
  above it or its topmost bit is gone (`ZOrderPolicy.PinHolds`). Start, the taskbar or another
  program's topmost window above it is not a reason ([S-33c]).
- **[S-34c]** At rest a surface is re-anchored only when its place is broken: it is topmost, or a
  window on screen that is not one of ours or the desktop layer sits between it and the desktop window
  (`ZOrderPolicy.AnchorHolds`). A write that always happened would be heard back as a reorder and
  checked again, so the controller would never go quiet; our own writes are also skipped at the hook
  (`WINEVENT_SKIPOWNPROCESS`). A surface's own moves never need a check: [S-20a] rewrites each one.
- **[S-34d]** Repairs (a re-anchor, a re-raise, an uncloak) are bounded: 16 between two user actions
  (`ZOrderRepairs`), refilled by a foreground change, a display change, an Explorer restart, an
  unlock or a resume. Another program that insists on the same place (a second desktop-widget app, the
  shell re-raising its layer) answers every write with its own, and with no clock nothing else ends
  that exchange. The first refusal is logged as a warning; the lift and its drop are transitions, not
  repairs, and are never refused.
- **[S-34e]** What no event covers, and needs a live pass: a cloak or a window under a surface that
  arrives with no window-manager event at all after it (not seen; the next foreground change repairs
  it); a display powering on without a mode change (`GUID_CONSOLE_DISPLAY_STATE`) is not heard, only a
  mode or monitor change is ([S-848]). Verified only by reasoning and the pure tests: Win+D and back,
  Win+M, the taskbar's Show Desktop button, a fullscreen exclusive game, an Explorer restart,
  sleep/resume, lock/unlock, a display unplugged and replugged, two displays, clicking widgets in edit
  mode during Show Desktop.
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

- **[S-1880]** Only a **live** widget is attached, ticks, polls or holds anything (the system hub reads what every view's widgets need, [S-2513]; gallery sources linger 2 min after their last widget, [S-3413]): one in a view some
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
  with every list scrolled to its end. `--busy` is the long-running desktop on two displays ([S-6601]).
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

## Memory

What a desktop left running for hours holds, and what bounds it. Most of the process is native: XAML
elements and text layouts on the process heap, decoded pictures and video frames in the graphics
driver's buffers; the managed heap is a few MB of live objects. Figures are from the switch check's
busy desktop ([S-6601]), Debug, private bytes.

- **[S-6600]** What is built is bounded by what can be seen. Only the views the displays show run
  ([S-1880]); a list builds the rows in view and one viewport below ([S-1887], [S-6603]); a picture is
  decoded at the size drawn ([S-3411]); a video plays the rendition the tile needs ([S-6602]); a cache
  of what nobody sees has a byte or entry cap (pictures [S-6605], program icons 512, file icons,
  favicons, program names). The
  release sweep ([S-1882]) is the only forced collection.
- **[S-6601]** Debug render: `--switch-check <rounds> [<out.txt>] --busy <pictures> [--stream <url>]
  [--soak s]` is the long-running desktop: two 2560x1440 stand-in displays, one with seven galleries
  (every style) over a picture folder, one with a video stream, the visualizer, top processes, the
  meters, CPU details, the screen-time chart and a clock; measured after 5 s and every 30 s for
  `--soak` seconds, then the showcase views switched through on the first display `rounds` times
  with the second keeping its view, then both emptied. Every measurement of the switch check also
  prints the GC's committed bytes, the icons held, the process heaps (committed and in use,
  `HeapSummary`) and the private memory in 32 MB blocks (the driver's picture and video buffers).
  `--state <path>` takes the views from a copy of a state file, never written back.
- **[S-6602]** HLS video is capped at the rendition the tile needs. The master playlist is read once
  for its renditions (`HlsVariants.Parse`, at most 256 K characters; anything else has none) and the
  adaptive source's `DesiredMaxBitrate` set to the smallest rendition that fills the tile at its
  physical size (`HlsVariants.CapFor`: one side when letterboxed, both when cropped or stretched, the
  sides swapped when turned), again on every resize; it starts at the highest bitrate under the cap.
  No cap when no rendition is that large or any does not declare its size. The 1080p test stream in
  a 1000 px tile plays 720p: about 160 MB less after three minutes.
- **[S-6603]** A detail list (top processes, drives, adapters) builds only the rows in view and one
  viewport below, with a spacer for the rest (`FolderRealize`, as the folder widget); a scroll, the
  keyboard cursor or a taller tile builds more, nothing built is taken down. Top processes had a row
  per process in every list: seven tiles cost 327 MB private, now 203 MB.
- **[S-6604]** The process runs on the segment heap (`app.manifest`, `heapType`), as packaged UWP
  apps do. The default heap kept what it once committed: with both displays empty again it held
  about 230 MB committed for 80 MB in use; the segment heap gives the pages back (about 110 MB).
- **[S-6605]** Decoded pictures nobody shows are kept up to half as much again as those on screen,
  at least 32 MB (one full-bleed 4K picture) and at most 96 MB (was as much again, 48 to 192 MB).
  Each one also costs its copy in the driver's buffers.
- **[S-6606]** Not done, on purpose: no `EmptyWorkingSet` or `SetProcessWorkingSetSize` trim (it moves
  pages to the standby list, the commit stays, and the next frame faults them back in), no periodic
  `GC.Collect` (the managed heap is not where the memory is), no `HeapCompact` (measured: it releases
  nothing on top).
- **[S-6607]** Busy desktop ([S-6601], 300 s soak, two showcase rounds, the Mux 1080p test stream,
  200 pictures of 2 to 24 MP), before and after [S-6602]..[S-6605]: 642 MB private after the soak, now
  395 MB; 769 MB after the showcase rounds, now 443 MB; 539 MB with both displays empty, now 329 MB.
  Working set at the end of the rounds 873 MB, now 560 MB.
- **[S-6611]** The GC's gen0 budget is 4 MB (`GCgen0size`, compiled into the Native AOT binary as a
  runtime option, `Themia.App.csproj`), so its ceiling is 6 MB instead of about 15 MB: the managed heap
  holds a few MB of live objects, and the default budget kept 15 to 25 MB committed for garbage not yet
  collected. The switch check's first line prints the ceiling in force. Ordinary view
  ([S-1885]), Debug AOT, with the view emptied again: GC committed 18.7 MB, now 9.4 MB; private
  141.6 MB, now 132.8 MB. The price is more gen0 collections, each over a heap of a few MB. A JIT
  build (`dotnet build`) keeps the default.
- **[S-6612]** At start (after the single-instance hand-off) the process lowers its memory priority to
  below normal (`SetProcessInformation`, `ProcessMemoryPriority`, `MemoryPriority.Lower`), so under
  memory pressure Windows repurposes Themia's trimmed pages before those of the apps the user works
  in. Not low: the surface is on screen and redraws every second (the clock, the meters), and pages
  taken from it before a background indexer's would fault back in on the next tick. A refusal is
  logged, never fatal. It changes nothing while memory is plentiful; the switch check applies it as
  the app does and prints the priority in force. Not done, on purpose: EcoQoS
  (`ProcessPowerThrottling`). It is the whole process or nothing, and the same process plays a
  video stream, the audio visualizer, the intros and every drag of edit mode; on the efficiency
  cores at a lower clock those frames come late, and an idle Themia already sleeps between ticks.

## Persistence

- **[S-40]** Writes are **atomic**: serialise to a sibling temp file, then move over the target, so a
  crash mid-write cannot leave a truncated document.
- **[S-49]** The move over the target (`AtomicFile.Replace`, every store's atomic write: state,
  notes, accounts, tasks, habits, screen time, sync vault, settings export, boot guard) is retried
  on an access-denied or sharing/lock-violation error only, at most 5 attempts with a doubling pause
  from 10 ms (150 ms in all), each refusal logged: an indexer or a virus scanner holding the fresh
  file without delete sharing refuses the rename for a moment (seen as a flaky
  `Access to the path is denied` in the note store's concurrent-writers test). Any other error, and
  the last attempt's, is handled by the store as before.
- **[S-41]** There is a **single writer**, and saves are coalesced — a drag that mutates geometry
  sixty times a second produces one write.
- **[S-42]** `FlushAsync` takes the write gate *before* reading the pending snapshot, so a flush that
  finds nothing queued still waits for an in-flight write to land. Reading the snapshot first let a
  shutdown flush return while a debounced save was mid-write, i.e. "flushed" without durability.
- **[S-43]** Load distinguishes **absent** from **corrupt**. A missing file is a fresh profile. A
  file that is empty, unparseable, or from a newer `schemaVersion` is corrupt: defaults are returned,
  loudly, and the original is **moved aside, never deleted**.
- **[S-44]** A loaded document is repaired back inside its invariants rather than rejected — dangling
  ids, duplicate ids, a config's own out-of-range values, out-of-range design values and
  off-screen geometry are all fixed, and every repair is reported. A config cannot disagree with its
  widget's kind any more: the config's discriminator is the kind ([S-6908]).
- **[S-45]** Closed sets are enums, not strings (`FillKind`, `AppTheme`, `ClockFace`), except the
  sets other assemblies extend: widget, provider, feature and cadence ids are validated ids, closed by
  the registry compiled in ([S-6900], [S-6902]). Widget config is a discriminated union with the discriminator written into the JSON,
  so a config can never be silently read as the wrong widget's shape. An unknown widget kind is
  dropped with a report, because this build cannot render it at all ([S-1900]).
- **[S-1900]** A widget whose kind is a name this build does not have (a removed widget, a hand edit)
  is taken out of the document **before** the typed read (`AppStateStore.Parse`, by its config's `kind`
  string), and each one is reported ("dropped widget 'calc' of unknown kind 'calculator' from view 'Desk'"); the rest
  of the document loads. The typed read would otherwise refuse the whole file over one widget and
  set it aside. A property the model no longer has (a widget's old `parentId`) is ignored like any
  unknown JSON, so a widget that sat in a removed container loads on the desktop at its own rect.
  Validation, not migration ([S-1050]).
- **[S-6908]** A widget is known by its config's `kind` (its module id, [S-6900]). A widget whose
  id no module in this build declares (a settings file from a build with a module this one lacks, a
  hand edit), or whose id is not an id at all, is dropped on load like an unknown kind ([S-1900]):
  taken out element by element before the typed read (System.Text.Json would refuse the whole
  document over one unknown discriminator), named in the load's log line with its id and its view
  ("dropped widget 'w3' of unknown kind 'hello' from view 'Desk'"), and gone from the next save. No
  placeholder card, no raw JSON kept. A deliberate exception to "never delete user data on a
  recoverable error": nothing this build can do brings a widget without its code back (Q15). So is a
  widget without a config (it has no kind). A widget's own `kind` beside its config's, which files
  from before ids carry, is not read and no longer written. A known id with a broken config is
  repaired by its own rules ([S-44]).

## Clock

- [S-50], [S-51], [S-52], [S-53], [S-54], [S-6011], [S-6012], [S-6013], [S-6014], [S-6015], [S-6020], [S-6021], [S-6022], [S-6023], [S-6024]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).

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
  toggle it dozens of times a second. Edit mode is reached by this hotkey and by launching Themia
  again ([S-66]); a widget's right-click menu's Edit layout ([S-1758]) leads there too. A
  double-click on a widget does **not** enter edit mode (user decision 2026-11-07, issue 75): a click
  on a widget opens what it is about ([S-2000]), and waiting out the double-click time before every
  such click was the reason the first attempt was retired. Another program may already own the
  combination; that is logged as an error and leaves a working app, because launching Themia again
  still reaches edit mode. The hotkey is unregistered on shutdown — one that
  outlives its window is a combination nothing can claim until the session ends, including the copy
  the user starts next.
- **[S-66]** Launching Themia while it is running **activates the running copy** instead of starting
  a second. Two processes drawing two widget layers over one wallpaper is indistinguishable from
  one, and with no tray entry there is nothing left to tell them apart by. The running copy is found
  by its command window's class name, which must therefore stay stable across versions. Started
  again **by hand** (Start, a shortcut, `Themia.exe` with no argument), the running copy enters edit
  mode and opens the edit bar's **settings menu** ([S-3520]) at its first level under ⚙ on the main
  display (the primary monitor's), the keyboard in it: Themia has no window, so that is what
  starting it again opens, as Karpo's every way to its settings opens the edit bar's menus. Edit mode
  already on, it stays on and ⚙ opens all the same (already open, it goes back to its own rows); another of
  the bar's menus open there closes, and a placement in progress is dropped. Started again **at
  sign-in** (the Run entry's flag or the package's startup task, [S-934]) it does nothing: a second
  autostart entry never throws edit mode up over the desktop. The copy that stands down says which
  in the activation message (`SecondStart.Encode`); any other value is logged and ignored
  (`SecondStart.Decide`, a trust boundary); a start by hand hands the foreground on
  (`AllowSetForegroundWindow`) so the menu gets the keys. Tests `SecondStartTests`.
- **[S-67]** Quitting is offered in one place, the last line of the edit bar's settings menu (**Quit
  Themia**, [S-3532]), and never as a button beside **Done**: it is the only action there that cannot
  be undone and **Done** is the one the user presses constantly.
- **[S-68]** **A flag Themia does not know starts nothing.** The start's first argument is classified
  before anything else (`StartArguments.Classify`): no argument or a value is the app, as is the Run
  entry's `--autostart` ([S-934]); a render or check flag this build has (Debug only,
  `Gallery.RenderFlags`) runs that render and exits; any other argument starting with a dash prints
  an error naming it (stderr and the log) and exits with code 2, never starting the app and never
  reaching the hand-off of [S-66], which would toggle the running copy's edit mode. A render flag no
  render took is refused the same way. A Release build knows no render flag. Tests `StartArgumentsTests`.

## Not on this machine (UNAVAILABLE)

User request 2026-10-19: "if there is no brightness to set it should still show the slider just
greyed out … do the same for the battery widget". Owning files: `Themia.Sdk/Widgets/Unavailable.cs`
(the figures), `Themia.Sdk.WinUI/Widgets/UnavailableLook.cs` (the look). Tests `UnavailableTests`.

- **[S-1940]** A widget whose subject is not on this machine (no battery, no display brightness, no
  output device, no graphics adapter, an output the capture cannot reach, a quick setting Windows does
  not offer here) draws its own content, the same ladder and parts as when the subject is there, in
  the family's disabled role (`UnavailableLook`: `ColourRole.TextDisabled` for text, glyphs and a
  level's fill, [S-6706]; its controls `IsEnabled = false`). Nothing on it takes a click, the wheel, a key or a menu;
  the reason is the tile's tooltip and help text. No empty state replaces it. Not this rule: a widget
  waiting for setup ([S-1862]), an online widget's states (the kit's), a list whose items are data that
  is simply not there (Bluetooth devices, notifications, removable drives, a drive not connected:
  their own states stay), and the properties menu (its options stay hidden where they change nothing).
  As built: Brightness [S-4445], Volume, Mixer [S-448], Battery [S-6272], Battery details and GPU
  details ([S-1942]), GPU [S-433], Audio visualizer [S-3206], the quick-setting toggles ([S-3904]: their
  own item in its disabled state) and the power buttons (already so).
- **[S-1941]** What the content stands at (`Unavailable`): a level part (a slider's thumb, a battery's
  fill, a ring's sweep, a gauge, a meter's history) at the middle, `Share` 0.5 (`Percent` 50 on a
  slider), claiming neither empty nor full; a number is never made up: the figure reads "--" ([S-82]).
  Only a thing not on the machine stands at the middle; one that is there but unreadable right now is
  [S-82]'s dimmed "--" with an empty part.
- **[S-1942]** A detail widget's sheet for a thing not on this machine (`Unavailable.Sheet`): the hero
  "--" and the facts it leads with when the thing is there, each "--", labels and figures dimmed, the
  reason the tile's tooltip (battery: time left, health, full charge and design capacity, cycle count,
  voltage; GPU: load, temperature, GPU clock, memory clock). `--state unavailable` renders it.

## Clipboard & transfer between displays

- **[S-70]** In edit mode the surface handles **Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+A**,
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
- **[S-77]** The drop **point**, not the dragged rects, decides the destination: those are kept in
  reach of their own screen ([S-09]) and are clipped at its edge ([S-6608]), so they never show on
  the next display. The held widget's centre goes to the point, the others keep their offsets to it,
  and the block is kept in reach of the destination's work area as a whole: a point over the taskbar
  counts as that display and the block keeps the visible minimum above it ([S-841]).
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
  states and look different: the first is the meter drawn in the disabled look at the middle
  ([S-1940], [S-1941]; it was an empty state until 2026-10-19), the second a dimmed `--`. A desktop
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
- **[S-109]** The ping host is validated at **both** boundaries — the box it is typed in and the probe.
  An implausible value is never persisted (the properties menu's Host question refuses it as typed,
  `PingHostLine.Refuse`, 2026-10-19), and an empty box means the default host, named at the end of
  the Host line, never "probe nothing".
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

- [S-128], [S-129], [S-3962], [S-130]: moved with the Notes widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Notes/`).

## Countdown and pomodoro

- [S-140], [S-141], [S-142], [S-143], [S-144], [S-6030], [S-6031], [S-6032], [S-6036], [S-6037]: moved with the Countdown widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Countdown/`).
- [S-150], [S-151], [S-152], [S-155], [S-6035]: moved with the Pomodoro widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Pomodoro/`).
- **[S-153]** The cue is synthesised in Core and played through `winmm!PlaySound` from memory, not
  through `MediaPlayer` or `AudioGraph`: those are whole media pipelines the AOT compiler roots in
  full in order to play two seconds of oscillators. `CuePlayer` copies the wave to native memory and
  plays it asynchronously, single-flight: the next cue stops the previous one (`PlaySound(NULL)`)
  before its buffer is freed, so at most one buffer is ever outstanding and nothing is GC-pinned.
  A widget (the pomodoro, a menu line's preview) reaches it through the SDK's `SoundCue`, the
  seam the host installs at start; the host's own alarm service calls `CuePlayer` itself.
  (The twelve ringtones and the volume: [S-1460]..[S-1463].)

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
- [S-170]: moved with the Sun widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Sun/`).
- **[S-171]** (Reason superseded by [S-1494]: the place is now picked by city search; the validation
  below still governs the exact-coordinate fields.) There is no geocoding service here and widgets make no network calls, so the settings
  take latitude, longitude and place name directly. Input that is not a number in range is
  **rejected** — the last good value stands and the ask says why ([S-3721]) — never coerced to 0, which would
  silently move the user to the Gulf of Guinea.

### Two widgets: Sun and Moon (SKY-SPLIT)

Sun & moon is split in two (user rule: one widget, one thing). Each is a module of its own
(`src/Widgets/Themia.Widget.Sun`, `src/Widgets/Themia.Widget.Moon`, their SPEC.md files). The SDK:
`SkyPlaceConfig`, the astronomy (`MoonPhase.NextRiseSet`, `SkyText.EventTime`), `SkyParts` (the
shared row and moon drawing) and `SkyPlaceRows` (the shared place line on their properties menus); tests `SkyWidgetsTests`
(`S310x_`, the astronomy), the pair's in the module host's `SkyWidgetsHostTests`.

- **[S-3100]** **Sun** (`sun`, 320×240, sun glyph) is the sun's day: the path graphic, the
  number strip and the rows sunrise, sunset, daylight (+ change), solar noon, dawn, dusk ([S-3102]).
  **Moon** (`moon`, 240×220, moon glyph) is the moon: the disc, its phase's name, the lit
  percentage, moonrise, moonset and the next full or new moon ([S-3105]). Both sit in the add menu's
  Weather & sky subject ([S-3511]), carry the Header like every data widget (G3, 2026-10-29: titled with the place, the widget's name while it is cleared), start at Zurich, so they
  need setting up only once their place is cleared ([S-2848]), and have no keys ([S-1996]). There is no combined kind: `SunMoon` (16) is gone and
  a state file holding it drops that widget at load like any unknown kind ([S-1900], [S-1050]).
- **[S-3101]** Both carry the observer's place (`SkyPlaceConfig`: latitude, longitude, place name)
  and share one line on their properties menus ([S-3719], user request 2026-10-19; no settings
  page): **Use my location** ([S-2850]) and, while it is off, **Location** (`SkyPlaceRows.Lines`), the city search ([S-1494], [S-2840], [S-3722])
  with, under its answers, Clear location ([S-2848]), Latitude and Longitude, each asked for as
  text ([S-3721]) and validated ([S-171]). The place's name is what the search found (cut at 80): it
  is the header's title unless one is typed, and the Header row's Title is the one way to change it
  ([S-3590]; the Place name line that set it a second time is gone). Sun adds the Events switch under Style ([S-3107]); Moon has nothing else ([S-3108]; how
  each draws itself is its style, [S-6045], [S-6050]). `Repair` at load: a latitude or longitude that is not a number
  in range sends both, and the name, back to Zurich (a `null` one is a cleared place, [S-2848], and
  is left alone); a place name over 80 characters is cut; each
  repair is logged.
- [S-3102], [S-6045], [S-6046]: moved with the Sun widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Sun/`).
- **[S-3104]** Moonrise and moonset: the next of each strictly after now, from the moon's altitude
  (the [S-165] sublunar point) stepped every 10 minutes over the next 50 hours and each horizon
  crossing bisected; a crossing is the centre at +0.125° geocentric (parallax less refraction and
  semi-diameter). Null when the moon does not cross in the window (it stays up or down near the
  poles) and the row then shows an em dash. Good to a few minutes (truncated lunar series).
- [S-3105], [S-6050], [S-6051]: moved with the Moon widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Moon/`).
- **[S-3106]** A rise or set reads as the time alone when it falls today in the machine's zone, else
  with the short weekday before it ("Mon 9:55 AM"): the next one is often tomorrow.
- **[S-6735]** The pair's shared parts are on the family's blocks (`SkyParts`, G3 2026-10-29): a row
  (`SkyEventRow`) is the fact row ([S-6711]): a 16 lead (a glyph, or the moon drawn at 16), the name
  secondary, the time at the end primary; the next event's name is BodyStrong, never in the accent,
  and its countdown, or a plain row's note ("civil", "35°", "−3m 20s"), is the row's second line where
  the tier gives rows two lines ([S-6712]). Its name takes its short form ("Rise" for "Moonrise") where
  the long one and the value do not fit side by side in its room, the row's width less the insets, the
  lead and its gap and the gap before the value (`SkyRows`, the ramp's width estimate). The moon's
  drawing (`SkyMoonDisc`, [S-169]) is a lead: its lit part in the secondary role, the unlit rim a
  hairline in the tertiary one, a role lower each below the horizon.

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
- **[S-215]** A chart is the card's bottom band, edge to edge with no padding at its sides or under
  it ([S-1824] for the meters, [S-1311] for Stocks; user decision 2026-10-11, reversing the inset
  of 2026-10-05). The card's rounded corners clip it; only the text above keeps the content inset.
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
- **[S-401]** Header placement is per widget: top, bottom, left, right ([S-404]) or hidden, set on the properties strip
  ([S-3707]). Switching it moves a grid cell, not a rebuild, so it costs nothing and cannot restart
  what the widget is doing. Hidden takes the whole header away, its icon, title and trailing part
  with it, at every size: the header's row is sized to its content and collapses to nothing, so the
  widget's content gets the whole card, and no other part keeps room for a title ([S-3712]). A kind
  that can never have a header ([S-3711]) is drawn hidden whatever the file says.
- **[S-5700]** **A card with no header above its view gives the view's top the inset** (owner decision
  2026-11-12, so more kinds can start without one, [S-2590]). Every view keeps its own side and foot
  insets (a Row carries the side inset to reach both edges with its hover, [S-6711]; a list, a face, a
  block pads its foot), and under a header the header's 12, 20 and 4 are its top. With the header
  hidden, shed ([S-402]), at the foot or down a side, the card starts the view's cell 12 down
  (`HeaderStrip.ViewTop`, the cell's margin, `WidgetCard.ApplyHeader`), so the content stands at the
  inset on all four sides. A view that owns its top edge says so (`IWidgetTopEdge`, re-read whenever the
  card places its header) and gets its whole box as before: one drawn to the edge on purpose (Gallery's
  pictures, Video stream, the audio visualizer's spectrum, now playing's Cover style) or padded on all
  four sides by itself (the clock, the quick toggles, the power buttons), or centred in the whole card
  (the alarm's time, [S-2590]). The Set up row, the waiting
  row and the error card follow the same rule as a view that does not own the edge (they each carried
  a copy of it before). The sizing counts it ([S-2444]). Tests `HeaderStripTests.S5700_`,
  `SizeFitTests.S5700_`.
- **[S-402]** The header sheds on a tile too short to carry it, **once**, in the card. Every widget
  had independently drawn that conclusion with a slightly different threshold.
- **[S-403]** The title is the user's own when they set one (on the properties strip, [S-3704]), otherwise the catalogue's. A widget has
  no say in it: a view that wants to label itself differently is a view that has drifted.
- **[S-404]** **Left** and **Right** stand the header as a strip down that edge of the card, as thick
  as a top header is high (it is the same header, measured against the card's height). The card's
  content is a 3 × 3 grid (auto, star, auto both ways, `HeaderStrip`): the view always sits in the
  middle cell, a top or bottom strip takes the whole outer row, a side strip the whole outer column,
  so the content area is the card less the strip's width and the size ladder ([S-660]) classifies on
  that box. A side strip sheds ([S-402]) on a card too narrow for it (`HeaderStrip.Sheds`: the axis
  the strip eats), a top or bottom one on a card too short. Debug: `--widget-png <Kind> <out.png>
  --header top|bottom|left|right|hidden` draws the cards with that header; `--menu-png` writes
  `props-header.png`, the properties panel's Header menu opened from its drop-down, Left ticked.
- **[S-405]** On the left the header is turned a quarter **counter-clockwise** (it reads bottom to
  top, its icon at the bottom, as a book's spine and a vertical tab on the left read); on the right a
  quarter **clockwise** (top to bottom, icon at the top). Either way the tops of the letters face out
  of the card (`HeaderStrip.Turn`, `Rotation`). WinUI has no layout transform, so `TurnedHeader`
  measures the header against the strip's length, takes its height as the strip's width, and turns
  it into place with a render transform, which turns its hit-testing with it.
- **[S-406]** Left and Right are **physical** edges in every language: in a right-to-left language a
  left header is still on the left (the card's flow mirrors, so it is the flow's end there,
  `HeaderStrip.Edge`), and the turn is the same. Turned, the header is laid out left to right in
  every language (icon first, so at the bottom on the left edge and the top on the right); the
  title's own text still runs in its script's direction. A flat header flows with the card as before.
- **[S-407]** A side strip has the card's two corners on its edge (`StripCorners`); a header colour
  of its own follows the rule of a top one ([S-828], [S-2103]), rounded on those corners. Without
  one the strip draws nothing: the card's one fill lies under it.
- **[S-408]** A side strip is the **icon and the title** only (concise first): the header's trailing
  part (a badge, a caption, a picker, Notes' formatting bar) is sized by its view for a header as
  wide as the content, and along a strip it would be measured against the card's height and read
  sideways. It stays with the view and comes back with a top or bottom header.
- **[S-409]** A side strip sheds on a card under **160 DIPs** wide (`HeaderStrip.SideShedsBelow`; a
  top or bottom one under 96 high, `FlatShedsBelow`). Derived: the strip eats about 40 DIPs (a
  header's height), and the content beside it must stay at least as wide as the narrowest card every
  widget is laid out for, the XS tier's 120 ([S-1810]). Under 96 (the old threshold) an XS card kept
  about 76 DIPs of content: Notes' empty text wrapped per letter and Mail's Set up button clipped.

## The widget family: tokens and blocks (FAMILY)

Every widget is built from the same few sizes, colours and parts, so the desktop reads as one family
(user decisions 2026-10-29). Owning files: `Themia.Sdk/Kit/Family/*` (the tokens and the blocks'
rules), `Themia.Sdk.WinUI/Kit/Family/*` (the tokens as WinUI parts and the seven blocks),
`Themia.Core/Widgets/FamilyCheck.cs` (the render walk's rules), `Themia.App/Shell/Gallery/Family*.cs`
and `TreeDump.cs` (the renders); tests `FamilyTokensTests`, `FamilyBlockRulesTests`,
`FamilyCheckTests` (Core), `FamilyGuardTests` (module host); `scripts/family-check.ps1`. The
module author's guide is docs/WIDGET-KIT.md 16.

- **[S-6700]** Text is drawn in **nine sizes and two weights**: Caption 12/16, Body 14/20 (BodyStrong
  at 600), Title 20/28 (600), the Readout steps Big 28/36, 40/52 (600), and the Hero steps 68/92,
  96/128, 136/180, 192/252 (600) that only a one-readout widget (Clock, Alarm, Countdown, Stopwatch,
  Pomodoro, Focus session, the quick toggles, the power buttons) may use. Weights are 400 and 600,
  nothing else. Every step is the base size times the user's text size ([S-821]). A text's line is the
  step's exactly (`LineStackingStrategy.BlockLineHeight`).
- **[S-6748]** The family's type is the app's **only** type: the shell draws on its steps too, and the
  old Fluent ramp is folded into `FamilyType` (the text size, `TextScale`/`SetTextScale`/
  `ClampTextScale`, the width estimate `EstimateWidth`, `StepsAt(scale)` for tests); `FluentType` and
  its off-family steps (Body Large, the 28/40/68 "Title" steps, `Ramp`, `Strong`) are gone (shell
  sweep, 2026-10-30).
- **[S-6701]** A value's **unit** and the **line under** it follow from its step: the unit (400,
  secondary) is 14 beside 28, 20 beside 40, 28 beside 68, 40 beside 96 and 136, 68 beside 192; the
  line under is Caption under 28, Body under 40 and 68, Title (400) under 96, 136 and 192.
- **[S-6702]** *(Legacy until converted: a converted kind's number is 28, or its Size's step for a
  one-readout widget, whatever the box, [S-2449].)* The **Readout**'s step is chosen, never computed: the largest whose line (and the line
  under it) fits the box and whose lead glyph, value and unit fit its width (`ReadoutRules.Step`,
  widths by `HeroFit.Ems`), never below 28. A data widget's number has two sizes ([S-2220]): 28, and
  40 once the view area is expanded both ways (L, XL), never 68 (`ReadoutRules.Ceiling`). Room left
  goes to more of the widget's own content, never a bigger number ([S-666]). A one-readout widget
  climbs the Big steps and then the Hero steps (68, 96, 136, 192) as far as they fit, on fixed steps,
  never filling the box. A box too short for the line under the floor step sheds that line, never the
  value.
- **[S-6703]** Space is one 4-based scale, **4 · 8 · 12 · 16 · 24**, and its sums: the card inset 12
  on every side at every tier; the header one 20 line at 12, then 4, so the content starts at 36; a
  glyph 8 from its text; a name 12 from its value and a column 12 from the next; the text edge 36
  with a 16 lead (12 without); a 24 header button stands 2 over the header's line (`FamilySpace`).
- **[S-6704]** Shapes: rows 28 · 40 · 56 (one, two, three lines) at every tier; icons 16 (header,
  row, status, inline), 32 (an avatar, an app or file icon, a cell), 48 (a large cell); one radius,
  4, for every inner rectangle, a pill, dot, ring or track round (half its short side); one stroke,
  2, with a 16 % wash under a chart line; one track, 4; a hairline 1. Buttons: a text button 28 tall
  (a one-line row) with 12 either side and Body text, an icon button 28 × 28, a header button 24 × 24,
  each with a 16 glyph (`FamilyShape`). The card's own radius is the design's setting.
- **[S-2220]** **The dense family** (owner's decision 2026-11-12: the widgets were too airy and too
  unlike each other): the card's inset is 12 and the header's gap 4, so the content starts at 36 and a
  led row's text at 36 ([S-6703]); rows are 28 · 40 · 56, one Body line with 4 over and under it, two
  or three lines with 2 ([S-6704], [S-6712]); a text button and a content icon button are 28, as tall
  as a one-line row; a header button stays 24, a button's sides 12 and the icons 16, 32, 48. A data
  widget's number is 28, or 40 once expanded both ways, never 68 ([S-6702]): 68 is the first Hero step,
  a one-readout widget's alone. The tier sizes, the breakpoints, the type's nine sizes, the card's
  radius and the gap between cards do not change. Tests `FamilyTokensTests.S2220_`,
  `FamilyBlockRulesTests.S2220_`.
- **[S-2221]** **The shell keeps its sizes** when the family gets denser: the edit bar, its menus, the
  properties menu's 320 × 440 panel and its 32 rows, the picker, the notices and the view bar draw on
  `ShellMetrics`, never on a widget's row, button or inset token; the widget picker's rows (32) and
  results (48) and the layout editor's icon commands (32) are `ShellMetrics.Picker.Row`, `.Result` and
  `ShellMetrics.Window.IconButton`. Test `ShellTokensTests.S2221_`; the shell's renders are pixel-equal
  before and after the density pass.
- **[S-6705]** Colours are **roles**, never values (`ColourRole`, its one table `ColourRoles`): text
  primary, secondary, tertiary and disabled; the accent with text on it (primary, secondary); Caution
  and Critical; Track (a rail), Solid (a pill over an image), Subtle (a count pill, a hover), Clear,
  Control (a standard button) and its stroke. The item layer's fills stay `ItemFill`'s. A data colour
  (a calendar's, an app's, a pen, a photo's) comes from the data through one entry point,
  `Family.Data(hex)`, as a 2 px bar, a dot or a series only.
- **[S-6706]** The accent is only for **on or selected** (a toggle that is on, today's circle, a
  checked box), the **one main action** (Set up, Join, Start, Play) and **charts and meters**, never
  for text on the card. There is **no success green**: a healthy reading is the accent, a rising
  price secondary. A **count badge is neutral**: Caption in the primary role on the subtle fill.
  `UnavailableLook` stays: a part missing on this PC is drawn in the disabled role ([S-1940]).
- **[S-6707]** A Readout's **delta** (a price's or a rate's change, Body, in its role) stands on the
  value's line where the value, the unit and it fit; else the unit gives way and the delta stays
  beside the value; else the delta is the line under the value, in its own role (the unit back where
  the value and it fit), and the widget's own line under gives way. Measured on the delta's widest case,
  so the fold does not change as the reading moves (`ReadoutRules.Fold`).
- **[S-6710]** **Header**: [glyph 16] 8 [name, BodyStrong, secondary, ellipsis] … [trailing], one 20
  line at the inset (`HeaderBlock`, the card's header). Its trailing parts, from the start: the
  problem glyph (caution), the busy ring, an informing label (Caption, secondary), at most two header
  buttons, the count pill (`HeaderParts`). On a narrow card they give way before the name is cut:
  the label, then the buttons from the last, then the count; the problem glyph and the ring stay
  (`HeaderRules`). Parts stand 4 apart, buttons side by side, a last button's glyph on the inset. A
  part with nothing to say now (a problem glyph while there is none) is not wanted and takes no room
  (`HeaderParts.Want`); the fold decides among the wanted ones.
- **[S-2260]** **A header names the widget; the place or the device trails.** The name is what the
  widget is (its module's name: "Sun", "Moon", "Weather", "Forecast", "Rain next hour", "Air quality",
  "GPU", "GPU details", "CPU details", "Ping", "Ping statistics"); where or which it is about (the place,
  the adapter, the processor, the host) is the header's informing label (`HeaderParts.SetAbout`,
  `HeaderRules.About`: trimmed, none when blank), so the label gives way first on a narrow card and four
  widgets about one place no longer share one name. No place (not set, or cleared, [S-2848]) is no
  label; a name the user typed still replaces the module's ([S-728]) and the label stays. A part added
  once there is something to say stands in the header's order (`HeaderRules.InsertAt`). Headers named
  after the user's own source (a task list, a mailbox, a drive, a folder, a calendar's period, a stock,
  a camera) keep it as their name. Tests `HeaderAboutTests` (module host), `OnlineHostTests.S728_`.
- **[S-6711]** **Row**: [lead] 8 [name, Body] 12 [inline meter] 12 [value, Body, end-aligned]; a
  second line Caption secondary, a third Caption tertiary; it carries the side inset itself, so it
  and its hover reach both edges; no divider (`RowBlock`). **What is read is primary**: a fact's
  value (Humidity 71 %) is primary and its name secondary; a list item's name (a task, a mail, a
  file) is primary and its value (a time, a size) secondary (`RowRules.Roles`). A widget's rows all
  lead or none do, and lead only when the glyph tells rows apart: where every row would show the same
  glyph by construction (an envelope on every mail) none leads and the name starts at the inset
  ([S-2270]); a list that tells unread from read then does it by the name alone, unread BodyStrong
  primary, read Body secondary (`KitSpec.Unread`, `RowBlock.Quiet`). The one emphasised row of a list (today, unread) is BodyStrong. A meter's own rows
  (an engine's load, reads and writes) let their meter take all the room between the name and the
  value (`RowBlock.Meter` with no width); a table's row puts its columns in the name's place
  (`RowBlock.Cells`, [S-7034]). A row of a part missing on this PC is all disabled
  (`RowBlock.Available`). The keyboard's ring on a row is a stroke-2 outline over its whole width in
  the accent, clear otherwise, so focusing it moves nothing (`RowBlock.Focused`, [S-1288]).
- **[S-6712]** A row's **lines come from the tier**, its pitch from its lines: one while the view
  area is compact either way (XS, S, Wide), two from regular both ways (M, Tall), three from expanded
  both ways (L, XL); a row of one line is 28, of two 40, of three 56 ([S-2220]); only whole rows are
  shown (`RowRules.Lines`, `Fit`).
- **[S-6713]** **Row.Cell** (`RowCell`): a grid's item (Shortcuts', a folder's icons), its lead (32,
  48 from an expanded width) over its name in Caption, primary, centred and cut with its ellipsis at
  the cell's edge, 4 apart; cells 12 apart, their marks 4 around them, so a grid's marks start at the
  inset less 4 (`RowRules.CellMarkEdge`, [S-2220]) and the first cell's lead and name at the inset.
- **[S-6714]** **Status** (empty, problem, set up): one 28 row where the content's first row would
  be, under the header: [state glyph 16] 8 [one sentence, Body, secondary, ellipsis, its whole text
  in the tooltip] 12 [its one action] (`StatusBlock`). Empty: the widget's glyph, tertiary. Problem:
  the caution warning; Try again or Open Accounts as a standard button. Set up: the widget's glyph and
  the accent Set up button ([S-1862]). A row with an action and less than 96 left for the sentence
  drops it and keeps the glyph and the action (`StatusRules`); without an action the sentence stays. Tile widgets show their state in the tile.
- **[S-6715]** **Tile** (`TileBlock`), a one-readout widget's body: the glyph centred on the largest
  Readout or Hero step that fits above its label (Body, 8 under it) and its state line (Caption) and
  takes at most 0.6 of the width (`TileRules.GlyphStep`), in the card's inset of 12 at every size. On,
  the **whole tile** is the accent with the text and glyph on it; Round puts the glyph in a circle
  (the accent when on, the item layer's tile otherwise) at the largest family glyph size within half
  of it; Switch is a row with a switch as its value. Not on, the tile is an item of the card's item
  layer ([S-6513]). Missing on this PC, every part is disabled. A power button's arm ring is the
  Tile's ring ([S-6736]).
- **[S-6736]** A Tile says its state by its roles (`TileRules.Roles`, `TileState`): **on** the accent
  (the tile, or the round look's circle); **off** the primary text on the item layer; **not known yet**
  every part secondary; **unavailable** every part disabled; **critical** (a power button armed, or
  its last run failed) the glyph, the text and the ring in Critical, nothing filled: the tile shows
  its state itself, never a Status row (DECISIONS 4). The lines are planned whether or not they say
  anything now (a reading not answered has an empty state line), so a change of state moves nothing.
  The owner keeps the whole tile as its hit target and tells the block the pointer (`Face`): the item
  layer's hover and press, or over the accent the subtle fills laid on it. A **ringed** tile keeps
  its glyph inside its ring, at the largest family glyph size within three quarters of the step
  (`TileRules.RingGlyph`), so the arm ring shows without moving anything; the ring is the Chart's
  (`ChartRing`, stroke 2 over its track) and drains on the compositor (`ChartRing.Drain`: one linear
  animation of the arc's end over the time left, nothing on the UI thread, [S-1913]). The **switch**
  look is a row: [glyph 16] 8 [name over state] 12 [Windows' toggle switch, 40 wide, drawn and
  inert]; the name and state need 96 beside the switch, so on a narrow tile the glyph gives way
  first, then the lines, and the glyph and the switch stay (`TileRules.Switch`).
- **[S-6737]** A **one-readout** widget (DECISIONS 2: the clock and the timers, the quick toggles and
  the power buttons) says so in its module's descriptor (`WidgetTraits.OneReadout`); it alone may climb
  the Hero steps (`ReadoutRules.OneReadout`, `ReadoutRules.Steps`), and the render walk ([S-6731])
  reads the same trait, so no list of kinds is kept anywhere else.
- **[S-6716]** **Controls** (`ControlsBlock`): what a person does to a widget, one row of 28 controls
  8 apart at the card's bottom inset: at most one main action in the accent (a text button, or a 32
  icon button for a transport's Play: `MainIcon`), standard buttons, icon buttons, a slider (its rail
  Track, its value the accent) taking the room left and an on/off switch (Windows' toggle switch, 40
  wide, its name its tooltip: an alarm that is set; `Switch`, set back without running its handler by
  `Set`). On a narrow card the icon buttons give way from the last, then the buttons, then the slider;
  the main action and the switch stay (`ControlsRules`). A button that does nothing now (a stopwatch's
  Lap before it runs) is drawn in the disabled role, its glyph with it. No control takes focus ([S-800]).
- **[S-6717]** **Chart**: one series in the accent at 2 with a 16 % wash, a second in tertiary,
  Caution or Critical only while the reading is over its threshold (`ChartRules.Role`); a meter's bar
  is a 4 round track with its fill (`ChartBar`; a column is the same track standing up, filled from
  its foot or between two ends; a fill moves along its track by a transform, never a margin), a ring a stroke-2 arc over its track from twelve, clockwise (`ChartRing`), a gauge the
  ring's upper half from nine o'clock (`ChartRing` half, `ChartRules.GaugeSweep`, `GaugeHeight`); a band
  (`ChartSeries`) runs edge to edge at the card's bottom ([S-215]), its points spread evenly or laid out
  by its owner on fixed slots (a meter's history, `ChartRules.Y`). A part's role, fraction and points
  are set again only when they change ([S-6330]).
- **[S-6739]** **A timer's face** (Alarm, Countdown, Stopwatch, Pomodoro, Focus session; `TimerFace`):
  a one-readout widget ([S-6737]), so its Readout climbs the Hero steps as far as they fit, on fixed
  steps, with its line under. Its Controls row stands at the bottom inset, 8 under the readout's box,
  and only where the floor step's line, that gap and the row fit; a tile too short for both keeps the
  readout, which is the widget. A ring face draws the Chart block's ring as large as the box's short
  side and sets the readout in its hole, three quarters of the hole wide and half of it tall
  (`ChartRules.Hole`); a ring under 48, or whose hole cannot hold the floor step's value and line,
  is not drawn and the readout stands alone.
- **[S-6740]** **Split-flap cards** (the clock's Flip style, the Countdown's; `FlipCard`, `FlipRules`):
  one card per part of the time, 8 apart, each its figures' widest case on a family step with 12
  either side and the step's line tall, an item of the card's item layer in the one radius with a
  hairline split in the Track role across its middle. The step is chosen as a Readout's: the largest
  the widget may take on which the cards, their gaps and what stands beside the row fit the width and
  the line and its line under fit the height, never below 28.
- **[S-6745]** **The shell's measurements** (`ShellMetrics`, `Themia.Sdk/Kit/Family/ShellTokens.cs`): the
  app's own chrome is drawn from the family's tokens like a widget, its own spacings on the scale; what
  Windows or Karpo measure for a piece of chrome and the family has no token for is named there, each
  with where it comes from, and nowhere else: Windows' overlay radius 8 and its menu chevron 12, a
  flyout's shadow (elevation 32, 8 down, blurred 32, 18 % light / 42 % dark), Karpo's menu (320 × 440,
  rows 32 apart with a 28 fill inset 5, its switch 40 × 20 with a 12 knob, the top row's 40 squares,
  [S-3734]), Karpo's edit bar (48 tall, 36 buttons in a 6 padding, [S-3500]), the view bar's squares
  ([S-911]), Windows' toast width 364 ([S-881]), the error details' 320 × 240 ([S-891]), the layout
  editor's window (a 48 title bar, a page at most 1000 wide inset 36, sections 30 under and 6 over,
  Windows' settings page) and the pickers' cells. Tests `ShellTokensTests` pin them: the menu's
  rectangle and rows must not change.
- **[S-6746]** **The shell's colours** are roles too (`ShellRole`, its one table `ShellRoles`; drawn
  with `ShellBrush.Of`): a flyout's solid sheet and its stroke, the edit bar's acrylic, a
  raised sheet (a notice), a window's layer, the card's own base, fill and stroke, accent words on the
  chrome, the accent pointed at, pressed and disabled, a control pointed at, a drawn switch's track and
  rim, a strong fill (the resize marks). None is a widget's colour role ([S-6705]).
- **[S-6747]** **The guard's shell rules** ([S-6730]): a module or one of the family's blocks
  (`Themia.Sdk.WinUI/Kit`) that names `ShellMetrics`, `ShellRole`(`s`), `ShellBrush` or `ShellColours` fails it. A
  colour is made from numbers in two files only: `HexBrush` (a data colour, the user's design, a
  stored colour's swatch and the ink drawn on it, a role's 16 % wash under its stroke: the rubber band,
  a placing outline) and `ShellColours` (the colours that are no role and no data: a shadow's black,
  a see-through backdrop's clear, Windows' default accent where the custom colour dialog starts, the
  motion effects' own palettes: the hacker tiles' phosphor, the warp's black space and its stars).
- **[S-6720]** The blocks replace the SDK's older parts as each widget group moves onto them, in
  place, never beside them (the order is the family's phase plan): the card's header is the Header
  block now and `Fluent.Header` is gone; the Set up row is the Status block now. `KitHeader`,
  `KitStatePanel`, `EmptyStatePanel`/`Fluent.EmptyState`, `Fluent.Row`/`Fluent.Badge`,
  `AddFooterRow`, `HeroFit`'s fill rules, `FluentType.Hero`/`Fit`/`FitWidth`/`Scaled` and the
  off-family ramp steps and `RingPart` stayed until the group that migrated their callers rebuilt them
  on the blocks; each then went. The meters and the details are on the blocks (G1, 2026-10-29):
  `MeterView` and `DetailView` are built from the Readout, Row and Chart blocks, `SparkBand` draws on
  `ChartSeries`, the GPU meter is a `MeterView` like the others, and `MeterFace`, `MeterStyleFace`,
  `MeterLookParts` (`BarList`, `TileGrid`), `MeterBrushes`, `CoreGraphGrid`, `PictureFace`,
  `BatteryShape`, `MeterNumbers`, `LoadTint`, `BarLayout`, `CoreGraphLayout`, `HeroLayout.Split` and
  the GPU's own view are gone. The lists and the calendar are on the blocks (G2, 2026-10-29): the
  kit's `KitHeader` is the Header block's parts (`HeaderParts`, at most two 24 buttons, the neutral
  count), `KitStatePanel` the Status block's row at the top, every `KitList` look (rich, message,
  table, schedule, check) the Row block of 28, 40 or 56 by the tier, `AddFooterRow` a Row block of
  32; `HeaderTrail`, `Fluent.Badge`, `KitHeaderParts` (folded into `HeaderRules.Wants`),
  `KitList.Lined` and the old ladders' measures are gone. `Fluent.Row`, `SetDensity`, `RowHeight`,
  `CompactBelow` and `EmptyStatePanel`, `EmptyStateFit`, `Fluent.EmptyState` stay for the groups
  whose widgets still call them (G5, G6, the card's fault and setup code). The weather, sky, finance
  and text widgets are on the blocks (G3, 2026-10-29): weather now, air quality, stocks and the rates
  are a Readout (28/40/68 then; 28/40 since [S-2220]) and fact rows laid out as the details are (`DetailFace`), a falling price or
  rate Critical (its delta and its line, [S-6707]); the forecast's days are Row blocks with a range
  `ChartBar`, its columns, curve and grid from tokens; rain's bars are `ChartBar` columns; the air
  quality's gauge is `ChartRing` half; the sun and the moon have the Header and their shared rows and
  disc are on the blocks ([S-6735]); the quote is set on the Big ramp and notes on Title and Body with
  their marks tertiary ([S-6738]); an online widget without an answer says so in the Status row
  ([S-6734]). `HeroReadout`, `WidgetParts` (`StatLine`, `StatCell`, `Figure`, the runs), `StatPart`,
  `FactStyle`, `PartMetrics`, `OnlineLayouts`, `GaugeDial`, `GaugeLayout` and `HeroLayout`'s glyph,
  token, growth, fill and tier steps are gone; the forecast's now mark, the air quality's band strip,
  the sun's number strip, Stocks' own rising and falling colours and the note's own font size went
  with them. The quick toggles and the power buttons are on the blocks (G4, 2026-10-29): both views
  are the Tile block in its three looks ([S-6715], [S-6736]), the power buttons' ringed with the arm
  ring as the Tile's `ChartRing`, drained on the compositor; `RoundButtonPart`, `ArmRing`,
  `ToggleFace`, `SwitchFace`, `RoundButtonFace`, `PowerFace` and `ButtonInset` (the 8 inset under a
  short side of 128) are gone, and with them the toggles' wide-strip row, the switch's own drawn track
  and its levels, the round button's 0.85 circle and Subtitle name, the power buttons' critical tile
  fill, the armed sentence's two Caption lines and the always-accent round power button. The files,
  stores and special widgets are on the blocks (G6, 2026-10-29): the folder's and Shortcuts' lists
  and tables are Row blocks (Details on `RowBlock.Cells`), their grids `RowCell`s laid out by
  `RowRules.Cells` (32, 48 from an expanded width, 12 apart), their selection `SelectionChrome`'s
  marks on the family's tokens, Shortcuts' Recycle Bin count the neutral pill; drive has the Header,
  the used share as a Readout over its `ChartBar` with fact rows, or a `ChartRing`; removable drives
  are Row blocks with an inline `ChartBar`; the Recycle Bin's size is a Readout with Empty the
  Controls row's one main action; a habit is a Row block with its days as the inline meter, the
  heatmap's cells filled from their foot; the scratchpad's tools are two header buttons and a bar of
  32 tools, its inks data colours; the screen-time widgets' day is the header's label and buttons,
  their apps the accent's ramp by rank ([S-2272], once data colours), the chart's total a Readout, the donut's wedges
  stroke 4; the gallery and the video stream say a missing picture in the Status row and draw their
  overlays on the Solid role; the visualizer's styles are the Chart's tokens; `UnavailableLook` is
  the disabled role ([S-6706]). `RingFace`, `HeroLayout`, `ScreenTimeLayout` and
  `ScreenTimeRules.RingText` are gone, and with them the drive's picture and its four-figure tile, the
  folder's cloud badge, the removable drives' Eject buttons, the Recycle Bin's icon, the habits'
  rate and best columns and the heatmap's shades, the scratchpad's header eraser, the screen-time
  chart's weekday labels, the calendar's shades and the donut's thick lifted ring, the Folder's and
  Shortcuts' own row and tile sizes. `Fluent.Row`, `Fluent.RowHeight`, `CompactBelow`,
  `Fluent.EmptyState` (`EmptyStatePanel`, `EmptyStateFit`), `RingPart`, `AudioFeed`, `AudioLook`,
  `Fluent.Space5`, `Fluent.OverlayRadius` and the off-family `FluentType` steps are left to G5's
  widgets and the app's shell alone. The time and media widgets are on the blocks (G5, 2026-10-29):
  the clock's seven styles are one block each in the card's inset on the Hero steps, its flip the
  split-flap cards of [S-6740], its dial and its words from the tokens; the timers share one face
  ([S-6739]): a centred Hero readout, its ring the Chart's, the Controls row centred at the foot with
  one main action (Start, Dismiss) and its icon buttons and switch ([S-6716]); the music widget is a
  Title line, its secondary line, a `ChartBar` progress and a transport whose Play is the main icon
  action; volume and brightness are the Controls row's slider; the mixer's sessions and the Bluetooth
  devices are Row blocks, a slider in the mixer row's meter place. `HeroFit`'s fill rules (`Row`,
  `Stacked`, `Figures`, `BaselineDrop`, `ClockRow`, the solid leading), `FluentType.Hero`, `Fit`,
  `FitWidth` and `Scaled`, `RingPart` and `RingArc`, `Fluent.SetDensity` and `CompactBelow` are gone
  (`HeroFit.Ems` stays: the measure every step is chosen on); so are the clock's own date size, the
  pomodoro's and the focus session's item layers, the mixer's three row styles and count pill and the
  Bluetooth status and signal columns. `Fluent.RowHeight` (the picker), `Fluent.EmptyState` with
  `EmptyStatePanel` and `EmptyStateFit` (the card's fault and waiting states), `Fluent.Space5`,
  `Fluent.OverlayRadius`, `FluentType.BodyLarge` and `Strong` (the view bar) are the shell's alone.
  The app's chrome is on the tokens (the shell sweep, 2026-10-30): the menus, the edit bar, the view
  bar, the notices, the pickers, the layout editor, the card's own fill, markers, fault and waiting
  states draw from the family's tokens, the shell's measurements and colour roles ([S-6745],
  [S-6746]); `Fluent.RowHeight`, `Fluent.Row`, `Fluent.EmptyState`, `EmptyStatePanel`,
  `EmptyStateFit`, `Fluent.Space5`, `Fluent.OverlayRadius`, `Fluent.ListPadding`,
  `WidgetLayout.ListInset`, `ReadoutLayout` and `FluentType` (folded into `FamilyType`, [S-6748])
  are gone. No old helper is left.
- **[S-6730]** The **source guard** (`FamilyGuardTests.S6730_no_literal_outside_the_family_tokens`)
  reads every module, the SDK's UI parts and the app's Shell and Widgets (not `Tests/`, `Fixtures/`,
  `Gallery/`) and fails on a type size or line, a weight other than 400/600, a spacing, thickness,
  radius, row size, stroke or opacity given as a number (0 and a hairline 1 aside; the motion code
  and `ScrollFade` may fade), a second ramp or an old helper (`FluentType`, `HeroLayout`, `HeroFit` but
  its `Ems`, `Fluent.Space5`, `OverlayRadius`, `RowHeight`, `EmptyState`), a colour made from numbers
  (but in `HexBrush` and `ShellColours`, [S-6747]), a `Colors.*` that is not a fallback, a `#RRGGBB`
  string, a theme brush outside the roles, or the shell's tokens in a module or a block ([S-6747]).
  It started (baseline 2026-10-29: 505 entries, 723 offences in 132 files) with a list of the day's
  offenders that only shrank; the shell sweep (2026-10-30) emptied it and the list is gone: **any**
  offence fails the test.
- **[S-6731]** The **render walk** (`Themia.exe --family-check <out.json> [--theme] [--kinds]`, Debug):
  every kind at the seven tiers in its default, its preview states and its styles, each card's header,
  view and Set up row walked element by element (a control's template skipped, its content walked)
  and judged by `FamilyCheck` (a glyph is judged as a glyph: the text inside its `FontIcon` is not
  walked): a text's size, line and weight, a glyph's size (a Hero step only in a one-readout kind,
  [S-6737]), every padding, margin
  and spacing on the scale, radii 0, 4 or round, strokes 0, 1, 2 or 4, every solid colour a role (or
  the card's, the item layer's, a data colour, or clear), no text or glyph in the accent.
  `scripts/family-check.ps1` runs it for both themes and fails on any finding (kind, element, rule;
  the values stripped, `FamilyCheck.Rule`). It needs the Debug app, so it is a definition-of-done
  step, not a test. Baseline 2026-10-29: 298 lines over all 78 kinds (about 47 700 elements in the two
  themes); every kind walks clean since group G5, and the shell sweep (2026-10-30) removed the list.
- **[S-6732]** `Themia.exe --family-blocks-png <outDir> [--theme] [--only]` draws each block in every
  variant (32 since G5) at the seven tiers with its visual tree beside it (`TreeDump`; `--widget-png … --tree`
  writes the same for a widget). The family's mockups draw the same variants over the same data, and
  their texts and glyphs are paired with the render's and held to ±1 DIP in position and size with the
  same colour role (DECISIONS 11).
- **[S-6733]** **A name over the wallpaper** (a widget with no card, the desktop item's, [S-5606]) is
  drawn in the family's two wallpaper roles, not the card's text roles: `OnWallpaper`, light in either
  theme as Windows' desktop draws its icons' names, over `OnWallpaperShadow`, the same text cast
  `FamilyShape.TextShadowOffset` (1 DIP) down and to the right, so it reads on a light or a busy
  wallpaper too (`WallpaperLabel`, Themia.Sdk.WinUI.Kit). Two texts, not a composition shadow, so the
  renders show what the desktop shows. Only a widget that draws on no card uses them.
- **[S-6734]** An **online widget with no answer** to draw (weather now, the forecast, rain, air quality,
  stocks, the rates) says why in the Status block's one row at the top of its content ([S-6714]), never
  a centred panel: still waiting, offline or an answer with nothing in it is a plain state (the widget's
  glyph, tertiary, no action); every other failure is a problem (the caution warning), with Retry as a
  standard button where asking again can help, never after a 429 nor for a refusal only an edit mends
  (`FetchState.Look`, `OnlineParts.ShowState`). An answer kept through a failure stays on screen and the
  header's problem glyph says so ([S-7042]); not set up is the card's Set up row ([S-1862]).
- **[S-6738]** A control that draws a text colour **without its alpha** (the note editor's RichEdit) is
  given the role's opaque equivalent on the card instead: the role over the card's fill over its base, as
  the theme and the user's design have them now (`HexBrush.OnCard`, `HexColor.Over`; asked again when the
  theme changes), so its secondary and tertiary text stay secondary and tertiary. Only a role, never a colour
  of its own.

Needs a manual pass: the showcase views on the live desktop (the blocks change no widget yet, but
the card's header and the Set up row are theirs now).

## Widget modules and the registry (MODULES)

Every widget is a module: an id, a descriptor and a view factory, found at build time (design:
`docs/WIDGET-KIT.md` section 4). Owning files: `Themia.Sdk/Modules/*`, `Themia.Sdk.WinUI/Modules/*`,
`Themia.Modules.Generator/*`, `Themia.App/Widgets/Modules/*`, `src/Widgets/Directory.Build.*`,
`src/Providers/Directory.Build.*`, `scripts/new-module.ps1`. Tests: `ModuleIdsTests`,
`ModuleRegistryGeneratorTests`, `WidgetRegistryTests`, `LocaleModuleTests`, and in
`tests/Themia.Modules.Tests` `ModuleConformanceTests`, `StateFixtureTests`, `UnknownWidgetTests`.
Module sections live in `src/Widgets/*/SPEC.md` and `src/Providers/*/SPEC.md` ([S-6912]).

- **[S-6900]** A module id (widget, provider, feature, cadence) is camelCase: a lowercase ASCII
  letter, then ASCII letters and digits, at most 64 characters (`weatherNow`, `airplaneModeToggle`).
  A widget's id is its config's discriminator in `state.json`, so the files keep the shape they had
  before ids. An id is checked wherever one is made or read; a malformed one in a document is a
  corrupt value, never a new widget.
- **[S-6901]** The registry is generated at build time (`Themia.Modules.Generator`): every
  `[assembly: ThemiaWidget(id, typeof(M))]` and `[assembly: ThemiaProvider(id, typeof(M))]` in an
  assembly the app references becomes one `new M()` in `ModuleRegistry`, in id order; the app
  declaring one itself fails the build (THEMIAMOD005). No reflection and no run-time loading, so
  Native AOT keeps every module. A malformed id fails the build (THEMIAMOD001), so does an id declared twice (THEMIAMOD002), a type that is not
  a public, non-abstract module class with a public parameterless constructor (THEMIAMOD003), and a
  module project (`Themia.Widget.<Name>`, `Themia.Provider.<Name>`) declaring anything but the one
  module its name says, `<name>` in camelCase (THEMIAMOD004).
- **[S-6902]** Each widget has one `WidgetDescriptor`, the pure half of its module: what the host
  knows of it without its UI. The host installs the registry into `ModuleCatalog` before it reads
  any widget (the app first thing in its constructor), once; a registry with a missing
  descriptor, an empty id, or an id or kind given twice is refused at that install, and so is a
  module whose descriptor's id is not the id its attribute declares. Every per-widget question the
  host asks is answered from the catalogue, not from a table of its own. Every widget is a module
  project ([S-6911]) that declares its descriptor on its module class; Core and the app declare no
  widget, no widget config and no widget page, and Core's tests run over a registry of none.
- **[S-6903]** A state document is read and written with one set of options (`StateJson`): the
  host's own metadata, then each installed widget's source-generated resolver, and a contract
  modifier that adds every widget's config type to the `WidgetConfig` union with its id as the
  `kind` discriminator. No attribute or list in the host names a widget's config; the union holds
  exactly the registry's configs, and every widget's resolver uses the host's options (camelCase,
  indented, nulls left out), so a module's config is written as a built-in one is. A host with no
  widget installed reads and writes a state with none (the load drops every widget of an unknown
  kind first).
- **[S-6904]** *(Retired 2026-10-19: `IWidgetModule.SettingsRows` and `WidgetSettingsRows` are gone
  with the widget pages. A widget's options are its module's `MenuLines` over a `WidgetMenuLines`
  ([S-3719]), ⚙ › Accounts the one place its accounts are added ([S-3745], [S-3558]); there is still no
  dispatch over config types.)*
- **[S-6905]** What a widget's card offers is stated by its descriptor's `Traits`, in full (the
  member is required, so no widget takes another's by omission): `HasItems` ([S-6570], checked
  against what its view paints), `CanHaveHeader` ([S-3711]) and `StartsHeaderless` ([S-700]; only
  for a widget that may have a header at all).
- **[S-6906]** What the host asks a widget's state is asked of its config, never by a switch over
  config types: whether it waits for setup and whether it runs while it waits (`ISetupRule`,
  [S-1860], [S-1881]), whether and where its content aligns (`IContentAligned`, [S-6581]), what the
  background sync keeps fresh for it (`IInterests`, [S-2608]; an SDK contract over the SDK's
  `InterestsBuilder`, one typed list per interest kind, `Of<T>()`, so a module's config adds its
  wants without the SDK naming the host's sources). A widget that reads an account (its
  descriptor names providers) has a setup rule and interests. Which quick setting or power action a toggle or
  power widget stands for is its config's (`QuickToggleConfig.Toggle`, `PowerButtonConfig.Action`;
  `QuickToggleRules`/`PowerActionRules` read them off the catalogue), and whether a place widget
  reads the weather feed and for how many days (`WeatherConfig.ReadsWeather`, `ForecastDays`).
- **[S-6907]** A widget places itself in the add menu ([S-3510]) through its descriptor (`Add`): the
  folder it is at home in, the run of rows it is in (a line parts two runs) and its place in the
  run, and its place among Popular's rows when it is one of them. The folders (the subjects under
  Browse and the families inside them, with their own places) are the host's and name no widget; a
  folder's row wears its first widget's icon, and a family named after its reading (Processor,
  Memory, Graphics, Network, Ping, Battery) takes its first widget's name as its title. No two widgets share a place, and none is at home in
  Popular, Browse or More. A folder no widget of the build is at home in (every one of its modules
  absent from the build) is not in the tree, as a folder left with none is not in the menu ([S-3511]).
- **[S-6909]** Strings per module: a project that sets `ThemiaStringsEnum` (`ClockStr`, a
  name ending in `Str`, else THEMIALOC010) gets its own enum, tables and `Text()`/`Format()`/
  `Plural()`/`Local()` extensions from its own `Localization/Locales`, in its own namespace, under the
  same file rules as the host's ([S-1101], [S-1111]); the SDK sets none and gets `Str`. Every table,
  the host's included, looks up through one `StringTable` ([S-1104]: the language, else English
  noted once, else the key). A descriptor hands the host its texts as `LocalText` (a table and an
  id, or a fixed brand name such as "Microsoft"), read in the current language each time.

- **[S-6910]** Providers are modules too (`IProviderModule`, `[assembly: ThemiaProvider(id, typeof(M))]`):
  each states its id, its name (`LocalText`), the features its accounts offer, how they sign in
  (`AccountAuth`), its place in ⚙ › Accounts › Add account and whether it is offered there
  ([S-3562]), and its own Updates row or none
  (its accounts follow the host's Accounts row, [S-3780]). The host installs them into
  `ProviderCatalog` with the widgets, checking each id against its attribute; every question about a
  provider is answered there. A widget's descriptor names the providers whose accounts it reads by id
  (`Providers`), each a registered provider; their ids are what search matches ([S-943]). The five account providers of today are declared in Core
  (`HostProviders`) until their sign-in runs on descriptors (docs/WIDGET-KIT.md section 11.2, step 11).
  `accounts.json` keeps its shape: `AccountsFixtureTests` loads a file of every provider, generated
  with the build from before provider ids, without a repair and writes it back byte for byte.

- **[S-6911]** A module is a project one folder under `src/Widgets` (`Themia.Widget.<Name>`, a WinUI
  library) or `src/Providers` (`Themia.Provider.<Name>`, plain `net10.0`), and adding the folder is
  the whole registration: the app and the module test host reference `..\Widgets\*\*.csproj` and
  `..\Providers\*\*.csproj`, the registry generator finds its declaration. The folder's
  `Directory.Build.props`/`.targets` give every module its framework, the SDK reference
  (`Themia.Sdk.WinUI` for a widget, `Themia.Sdk` for a provider), its strings generator
  (`<Name>Str`, [S-6909]), and keep its `Tests/` out of its build (they compile into
  `tests/Themia.Modules.Tests`), its `Preview/` and `Fixtures/` in Debug only; its csproj is
  `<Project Sdk="Microsoft.NET.Sdk" />` plus the providers it reads. A module builds on the SDK and
  providers only, never on Core, the app or another widget; code two widgets share goes into the
  SDK. The module test host checks every module the registry holds (`ModuleConformanceTests`): its
  folder is its id, its name and description are its own table's and said in English, its strings
  are whole in every language or `$englishOnly` with matching placeholders, its default config
  round-trips through the state under its id with the host's options, it references nothing it may
  not, and its `Traits.HasItems` matches whether its view paints items ([S-6570]). The host
  installs into it every widget of the build (Core's descriptors and the modules'), so the state
  fixture of every kind ([S-44]) and the unknown-id rules ([S-6908]) are tested there.
- **[S-6912]** Every module carries its own `SPEC.md`: its section, the tags only it defines and
  what needs a manual pass. The root `SPEC.md` keeps the host's sections and points at a moved one.
  A tag is defined once across all the SPEC files, and a module's tests name only its own tags.
  `scripts/new-module.ps1` scaffolds a module folder with all of the above.
- **[S-6913]** A widget's view asks the host for what it needs through the `IWidgetHost` its
  module's `CreateView` is handed, never through the host's statics: the shared ticker and the UI
  thread's dispatcher, the one launcher (`IShellLauncher`: every target classified on the literal
  string, each link kind on its own allow list, never a command line), whether its card is a
  preview ([S-1251]), ⚙ › Accounts (false where there is none, a debug render), and the
  host's services a widget reads: the Recycle Bin (`IRecycleBin`: the one answer how full it is and
  Windows' own empty, [S-4863]) and what runs a power action (`IPowerBackend`: the machine's calls,
  inert on a preview, [S-1243], [S-1251]) and the quick settings (`IQuickToggles`: one
  shared `IToggleSource` per toggle, [S-3907]) and the web answers the background sync keeps
  fresh ([S-2663]: `IWeatherAnswers` for the conditions, forecast, air and rain, `IQuoteAnswers` for
  the quotes, `IRateAnswers` for the ECB table, `IFeedAnswers` for the Rss widgets' feeds ([S-2661]); each a `Fetched<T>` per want, the key it is synced
  under, `Show`/Refresh and `Updated(key)`; where the sync is not running, a debug render, what the
  shared caches hold), and the system widgets' sources: the system figures (`ISystemFeeds`: the one
  hub's `Watch` over a typed `SourceId<T>` and `WatchSeries`, null before the host has started it,
  [S-2500]), the drives (`IDrives`: the one walk, [S-4655]), the output devices (`IAudioOutputs`: the
  one Core Audio worker's subscriptions, levels, mutes and default-output switch, [S-1220]), every
  display's brightness (`IBrightness`, [S-4441]), Windows' media session (`IMediaSessions`) and what the
  speakers play (`IAudioCapture`: the one loopback capture, [S-3203]); and the local stores: the
  clipboard history (`IClipboardHistory`, null where none is kept, with `CopyText` for putting text on
  the clipboard, [S-1458]), every alarm widget's alarm (`IAlarms`, [S-4623]), the one focus session
  (`IFocusSession`, do not disturb held host-side, [S-4421]), Windows' notification list
  (`INotifications`, [S-4408]), the notes (`INotes`, [S-1348]), the habit lists (`IHabitLists`,
  [S-1420]), the screen-time history (`IScreenTimeSource`, [S-458]) and the gallery's sources and
  decodes (`IGalleries`, [S-3410]), each the host's one, its files and their validation host code; and
  the file shell: a file's icon, thumbnail, type and count (`IFileIcons`, [S-1324]), files on the real
  clipboard (`IFileClipboard`, [S-1329]), a folder's listing, watch, repository, New file types and a
  ZIP (`IFolders`, [S-1321], [S-1333]), the shell's own operations with its dialogs and undo, delete
  always recycling, and a drag out (`IShellFileOps`, [S-1330]), File Explorer's own menus and Open with
  (`IExplorerMenus`, [S-6416]), Recent Items (`IRecentItems`, [S-4841]), the app-layout editor and
  launch (`IAppLayouts`, [S-1411]; the editor finds an item through `IHoldsAppLayouts` on a config),
  camera connections (`IVideoStreams`, [S-471]), the Start menu's apps (`IInstalledApps`) and a site's
  icon (`IFavicons`, [S-1304]); a drag's shell items are `IWidgetDrag.ReadShellItems`. A module's
  menu lines have no card: their host lists are seams installed at start (`DriveList`, `AudioDevices`,
  `GpuAdapters`), and `SettingsHost` holds the stores a menu edits (the notes, the habit lists, the
  screen-time history), the launcher, the owner window of a menu's file picker and the app-layout
  editor (`AppLayouts`); so are a process's
  executable and name (`ProcessPaths`) and a row's shell icon (`AppIconSource`). The host makes one per card, every member shared by all of them. What a widget shares with the host's
  own UI is in `Themia.Sdk.WinUI` too: `FluentLayout` and `ColorChoice`, the item menu (`ItemMenu`), `Notices`, a drop target's contract
  (`IWidgetDropTarget` over `IWidgetDrag`); the parts that need Win32 (the eyedropper, a file's
  shell icon, the OLE drag) stay host code behind a seam the host installs at start, as do the
  place lookup and the Stocks settings' symbol search (`SymbolLookup`, [S-1307]).

## The widget kit (KIT)

The shared parts a list widget is built from, and the rules they keep (design and recipe:
`docs/WIDGET-KIT.md`). Pure parts in `src/Themia.Sdk/Kit/` (namespace `Themia.Sdk.Kit`), tested in
`tests/Themia.Core.Tests/KitTests.cs`. The kit knows no service: a source hands it items and outcomes.

- **[S-7000]** `KitItem`: one row's data (id, title, subtitle, preview, lead, when, tone, emphasis,
  badges, link, group), plain text only. `KitLead` (none, icon, initials, the site's own icon, a
  status dot), `KitBadge`, `KitTone` (neutral, accent, success, caution, critical; brushes in the
  App), `KitGroup`, `KitSummary`, and `KitIcon`, the closed set of glyphs an item or action names.
- **[S-7001]** `KitWhen` is an instant or a day, never both: a due day does not move with the zone.
- **[S-7002]** Every item passes `KitItems.Clean` on its way to a list: no id, dropped; an id seen
  before, dropped (the first wins); strings cut to `KitBounds` (never through a character); a link
  kept only when it is an absolute web address without a user name or password (http or https for a
  feed's links; https on the source's own hosts when it names them), else the row stays and opens
  nothing; a summary's fraction clamped to 0..1, dropped when it is not a number.
- **[S-7003]** `KitText`: a count as 999, 1.2k, 12k, 3.4M in the language's digits, never rounded up;
  an item's time per the spec (`KitTime`): an age ([S-1302]: the shared freshness for a week, then
  the date), a clock time or a date; a day is written as its date.
- **[S-7010]** `KitAction`: what an item offers, a closed set (`KitActionKind`: Open, OpenTo,
  CopyLink, CopyText, Toggle, Rename, Delete, Refresh). None changes how a widget looks ([S-1746])
  and none writes to a service: a change the user wants made is an OpenTo to the service's page.
- **[S-7011]** `KitMenu.Lines`: one order for every kit item menu: the item's group (Open, a check,
  the web pages in the spec's order, the copies), then Rename, then Delete, then Refresh, each group
  after a separator, an empty group left out. One Open, check, Rename, Delete and Refresh per item.
  A line whose kind lacks what it needs (an Open without an address, a copy without text) is shown
  disabled. The first copy, Rename and Delete are the icon row's ([S-1740], [S-1741]). A line whose
  key the list takes shows it ([S-1753]): Enter on Open, Ctrl+C on the first copy, F2, Delete. The
  primary action (double-click, Enter) is the item's Open when it can run; an item with no Open at all
  opens for editing, its Rename (a task, [S-616]). A toggle whose label says what it does ("Mark as
  done") is a plain line, not a check.
- **[S-7013]** A kit service is read-only; the host's account widgets (mail, tasks) keep the writes
  their own sources make: `Change` (mark read, archive, move to junk: several per item, in the item's
  group after the web pages, carried out by the view's handler by id), Toggle, Rename and Delete. An
  Open or OpenTo with an id and no address is opened by the view itself (mail opens where the user
  reads it, a reply in the user's mail program); with neither it is shown disabled.
- **[S-7012]** `KitIcons`: one map from every `KitIcon` and action kind to its glyph. Open is the
  open glyph, as every widget's Open line (fixed: a feed's Open drew Explorer's "Show more options"
  picture, its glyph being the code point that art is matched to, [S-1754]).
- **[S-7020]** `KitKeys.Decide`: one key map for every kit list over `ListKeys`, in reading order
  (Left and Right step like Up and Down, across columns): with nothing focused a movement key lands
  on the first item, End on the last ([S-812]); Page Up/Down jump ten; Enter opens, Space toggles,
  Ctrl+C runs the first copy, F2 renames, Delete deletes, the Menu key opens the menu, Escape lets go
  ([S-813]); Alt+Shift+Up / Down move the focused item a place, never past an end ([S-3802]).
- **[S-7030]** `KitSpec`: a kit widget's shape (its row look, the optional fields it offers
  (`KitShow`: lead, subtitle, preview, when), its time, its overflow, its column cap, its untitled
  sentence); the user's own fields are bounded by it.
- **[S-7031]** *(Legacy until converted, [S-2443]: a kit list that states its rows plans by them,
  [S-2440].)* `KitLadder.Plan`: the kit's one disclosure ladder ([S-660]..[S-666]) on the family's
  rows ([S-6711], [S-6712]): every look is the Row block, one row of 28, 40 or 56 for one, two or
  three lines, never taller for a long title (one line, ending in an ellipsis). The lines grow with
  the tile both ways (`RowRules.Lines`: one while the view's area is compact either way, two from
  regular, three from expanded), up to what the row has to say (the title; the subtitle with the badge
  after it; the preview, on the second line when there is no subtitle); the title always, the lead
  (a 16 glyph or the site's icon) and the time (the value, secondary) from a column of 180, the badge
  from 260. From 400 wide a spec may go to two columns (three from 720) while a column keeps 320,
  never more than the spec allows. The list shows the rows that fit whole in the view's area less the
  card's bottom inset (`KitLadder.Fit`), the rest a scroll away under the fade ([S-639], [S-1281]).
  The rich look (the feed's, [S-1305]): the title, its age, the source on the second line, the
  summary on the third.
- **[S-7032]** The message looks (Mail's, [S-556], as the kit's): `TwoLine` rows are the sender (the
  name, BodyStrong while unread), the time as the value, the subject with the account's badge after
  it ("Quarterly numbers · Work") on the second line, the preview on the third; the lead the
  message's glyph, secondary. `Line` holds the same row to one line at every tier (a compact style:
  `KitChoices.OneLine`). An item's tone colours its time (Caution, Critical) and its lead; the accent
  never colours a text, and no bar marks a row. Times are re-labelled without rebuilding a row.
- **[S-7035]** `KitOverflow.Page`: pages of the rows that fit whole, never scrolled ([S-2709]): a
  page is the plan's columns filled one after the other, each group's heading (one row of 28,
  BodyStrong secondary; a day's: "Today", "Tomorrow" with the date where the line holds it whole,
  else the date, [S-2716]; primary for the selected day, [S-3622]) over its first row and again over
  a column that starts mid-group; on a tile under 128px a first group that is today drops its
  heading ([S-1267]); at least one row per column, so paging always moves. The widget turns the page
  (`KitList.Page`); the cursor takes the page with it.
- **[S-2280]** An item in no group (`KitItem.Group` null) has no heading over it, on a page's first
  row or a column's: its rows are the column's whole height (the week's chosen day, under its
  strip, which names the day already).
- **[S-7036]** The `Schedule` look (the agenda's): a Row block per item, its calendar's colour as a
  2 px bar at the lead's start (a data colour, [S-6705]; the item's lead glyph when it has no
  calendar), the title as the name, the start as the value ("All day"; "Now" BodyStrong while under
  way), the span, where and who (or the item's note) on the second line where the plan gives two.
  Two columns from 720px; rows of two lines from regular both ways, one on a compact tile; headings
  one row of 28. A view may hook its rows (`KitList.Decorate`: the calendar's links across widgets).
- **[S-7037]** The `Check` look (Tasks' ladder, [S-780]..[S-784], as the kit's, `KitChecks`): a Row
  block per item, its lead the item's check (`KitLead.Check`: a 16 ring of stroke 2, secondary or
  the item's tone on a late one; the accent's filled circle with the tick when done; a click on it
  toggles the item, drawn at once, then the widget's write); the title as the name, BodyStrong when
  emphasised and open, struck through and tertiary when done; the due as the value (`KitDue`, the
  source's words, in the item's tone while open): only urgent on a Compact-wide tile, none on a
  Compact × Compact one, long from Expanded × Expanded; the subtitle (a list's name) on a second
  line from regular both ways, never with the compact style (`KitChoices.OneLine`). Rows of one
  height in one or two columns (two on a Huge-wide tile when one would not hold them), filled column
  by column, whole lines only, never ending on a heading; the bottom lines fade while there is more
  below ([S-1281]); the done group under its heading (one row of 28, BodyStrong secondary) from
  Expanded height. The keyboard's ring is the Row block's.
- **[S-7038]** The check look's editing: a row renamed in place (the title's column becomes a box with
  the whole title; Enter or leaving it commits, Escape cancels, Tab commits and opens the next row's
  box, none past the last; Shift+Tab commits and closes); the add row under the list ([S-3850], the
  spec's words; Enter adds and stays open for the next item); a row dragged past 5px is lifted, the
  rows it passes in its group make way, the drop raises the new order once; Alt+Shift+Up / Down move
  the focused row within its group. While a row is lifted or a row's box is open no show redraws
  under it (the held show is drawn when they let go); a box whose item has gone goes with it. The
  widget carries every change out (`Renamed`, `Added`, `Reordered`) and asks its surface for the
  keyboard while a box is open (`TextInput`, [S-808]).
- **[S-7033]** Retired (G2, 2026-10-29): the inline actions a `Line` row showed under the pointer on
  a Huge-wide tile. A row is the Row block, a control is not one of its rungs ([S-666]); the menu
  and the keys carry every action.
- **[S-7034]** The `Table` look: one row of 28 of the user's columns (`KitColumn`: title, subtitle,
  preview, time, badge) in the user's order, in the Row block's place for the name
  (`RowBlock.Cells`), 12 apart; title, time and badge have fixed widths, subtitle and preview share
  the rest; the most essential columns are kept while their minimums fit, the first always; from
  700px a lone subtitle taking over 40% of the line is followed by the preview; the title primary
  (BodyStrong while unread), the rest secondary, the time end-aligned; the columns' titles (the
  spec's words, Caption secondary, one line and 8 under it) show over the rows from 140px of height;
  the time column is written as a stamp (the time today, the weekday this week, then the date).
- **[S-7040]** `KitScreens.Decide`: the one state machine. A widget that reads accounts: none at all
  is the card's Set up; its accounts all gone, all needing a sign-in, or all refused, each its
  sentence with the Accounts link. Then items held: the list, one header line per failed source
  ("World News: HTTP 404 Not Found"); a source not answered yet: loading; every source answering
  with nothing: the spec's empty sentence (a filter on: its no-match one); else every failed
  source's line in the sources' order, as one sentence: the one source's line, or with several
  "World News and 2 more could not be loaded", every line in its details (the state row's tooltip).
  A source's line uses its own words when it has them, else the shared sentence ([S-62]).
- **[S-7041]** The accounts a kit widget reads: every account offering its need when it names none,
  else the named ones still signed in.
- **[S-7063]** `KitMenuLines`: a kit widget's standard lines on its properties menu ([S-3719];
  user request 2026-10-19: they were `KitRows`, a settings page's rows), built only from the menu's
  vocabulary: the accounts it reads, a count (`Count`, a number, [S-3726]), its optional fields
  (`Shows`, switches); a free input is the menu's ask or list, checked by the widget's own validator
  as it is typed ([S-3721], [S-3729]). One Accounts submenu: "Every <kind> account" (on by default,
  accounts added later join), then a switch per signed-in account, its provider or problem at the
  end (turning Every off starts from every account there is; the last chosen one is greyed), then
  Manage accounts, which opens ⚙ › Accounts ([S-3745]); with no account the widget's sentence
  and Add an account. Every widget that chooses among accounts builds it with
  `KitMenuLines.Accounts` (directly, or through the SDK's `OfficeMenuLines`: mail, calendars,
  contacts, next meeting, week planner).
- **[S-7042]** `KitFreshness`, the one stale rule's words: one line per source and problem, folded,
  stamped with the oldest copy among them ("Work: No network · updated Mon 10:42", [S-2664]); the
  Refresh line with the age of the oldest copy on screen ("Refresh · updated 4 min ago").
- **[S-7064]** `KitHeader`: the header extras of every widget that shows a source's data, the
  Header block's trailing parts ([S-6710], `HeaderParts`, so the header folds them), leading to
  trailing: the problem glyph (only while a copy is on screen that the last attempt of some source
  could not improve on, its tooltip `KitFreshness`' lines, a click opening ⚙ › Accounts where only
  the user can mend it), the busy ring (only while a fetch of its own sources is out), an informing
  label the widget keeps (the exchange rates' day), at most two of the widget's own header buttons
  (24 with 16 glyphs: Mail's compose and sort, the calendar's back and forward, the task list's
  picker; an action, or choices asked in a menu under it) and the neutral count pill ([S-6706]).
  Which have something to say is `HeaderRules.Wants`; where they show is the header's fold (the
  label first, then the buttons from the last, then the count). The age is never drawn in a header.
  With nothing to show the extras are collapsed: they take no room and no gap.
- **[S-7039]** `KitStatePanel` is the Status block's one row at the top of the content ([S-6714],
  where the first row would be): the widget's glyph (the caution warning for a problem), one
  sentence (its lines in the tooltip) and its one way out, a standard button: Accounts ("Open
  Accounts", "Accounts" under 200px) where only the user can mend it there, else the page that widens
  what the account may read ([S-9919]), else the widget's own (its words and what it does: Tasks'
  "Try again", "Start a list"). Too narrow for both, the sentence goes and the glyph and the button
  stay.
- **[S-7062]** One launcher: a kit item's address opens through the host's one launcher
  (`IWidgetHost.Launcher`, which classifies it again on the literal string it hands the shell), never
  a process of the widget's own; a preview opens nothing. Nothing in the source starts a process but
  the launcher and a short list of host files with their reasons (`KitConformanceTests`).
- **[S-7065]** One open gesture (decided, Q1/Q2): in every list of items a click focuses a row and a
  double-click or Enter opens it: the kit's list (Mail, the calendar's agenda, [S-3652]), Recent files and Notifications. The card makes a
  widget inert in edit mode ([S-1751]); the kit's list and Recent files check no edit mode of their own.
- The kit's conformance checks over source (`KitConformanceTests`), each from an explicit list of
  today's offenders that may only shrink:
  - **[S-7066]** The kit's parts name no colour at all.
  - **[S-7067]** No view sets a literal font size or family beyond today's four.
  - **[S-7068]** No view or kit part shows a literal sentence (an example web address in a
    placeholder and a preview's sample data aside; none today).
  - **[S-7069]** A kit widget's view builds no list plumbing of its own, and only the kit's list
    turns actions into menu entries.
  - **[S-7070]** Every fixture parses and holds no token's shape, bearer header or address outside
    the reserved example names.
  - **[S-7071]** The kit's shared code names no service, provider, service header or error code;
    outside the provider projects nothing names a kit service or reads a service's own header beyond
    today's two (the rate-limit rules' `X-RateLimit-*`, the calendar's iCalendar `X-` properties).
- **[S-7072]** A row's value may be a reading instead of a time (`KitItem.Value`: a battery's
  level, "45%"), drawn where the time would be, in the item's tone (the Critical role for a low
  battery), cut to 24 characters, an empty one none; the ticker never rewrites it. `KitIcon` names
  a peripheral by kind for a row's lead (headphones, mouse, keyboard, phone, controller, pen, watch,
  any other device).
- **[S-7073]** A spec whose value is a reading (`KitSpec.Readings`) keeps it at every width: it is
  the item's point, where a time gives way below a column of 180; the title ellipsises first.
- **[S-7050]** `IKitConfig`: a kit widget's config states what it needs (a feature, or none for a
  web source), whether it is configured, the accounts it reads and the fields the user has on; its
  setup rule follows ([S-1860], [S-1861]): it waits while it has nothing to read, or while no
  signed-in account offers what it needs.

## The service layer (SERVICES)

How the host reads an online service for kit widgets, and the vocabulary every account and service
failure is said in (design: `docs/WIDGET-KIT.md` section 5.7 and 6.5). Pure parts in
`src/Themia.Sdk/Services/` (namespace `Themia.Sdk.Services`), tested in
`tests/Themia.Core.Tests/Service*Tests.cs`; host parts in `src/Themia.App/Services/Kit/`. The
service layer knows strategies, never services: a service is a descriptor that picks them.

- **[S-9900]** One failure vocabulary: `FetchStatus` names what went wrong, and an account's failure
  reaches it without being folded away. A sign-in only the user can renew (a 401 after the one
  renewal, a revoked grant) is `SignInRequired`, a refusal that is not a throttle `Forbidden`, a
  missing item `NotFound`, each with its own sentence in every language; an account that is gone
  is "not set up"; a server failure and a body that did not parse are "unexpected". None of the
  three is retried on the cadence: they wait the back-off's ceiling ([S-2609]) until the user acts
  or a Refresh asks. A sign-in problem never removes the account ([S-530]).
- **[S-9901]** A throttle's own retry time is kept: an account failure carrying `RetryAt` (the
  provider's `Retry-After` or rate-limit reset, `RateLimitRules`) holds its schedule until then, never shorter
  than the back-off, and the user's Refresh does not jump it ([S-2609]).
- **[S-9902]** A kit service is a `ServiceDescriptor`, data its provider module hands the host
  (`IProviderModule.Service`): its provider and feature, its Updates row, its transport (`RestJson`,
  `GraphQL`, `FormPost`), its https API root (fixed or the account's own), its sign-in members, the
  headers every request carries (never a credential), its rate-limit and error schemes, its
  freshness, whether public data may be read without an account, its page and item bounds, the
  hosts its links may name and the shortest cadence its budget allows. No host code branches on
  which service it is.
- **[S-9903]** What a service's rules give the kit is a query (`IServiceQuery`): a key naming
  everything that changes the answer (never the token), its paging and freshness, at most three
  earlier steps whose answers it needs (`IServiceStep`, a table of named values, fetched and held
  like any entry), the request for a page (`ServiceRequest`: a read, its body, the rate-limit bucket
  it counts against, its key) and the answer of a page, parsed through the service's
  source-generated wire type (`IServiceQuery<TWire>`): a body that is not that shape is a bad
  response, named by the first path that failed, never half-built.
- **[S-9904]** A page of a query is a `KitChunk` (items before the item rules, the next cursor, the
  summary line, the ids a delta removed, the sentence for an empty list the rules know the reason
  of); a widget's list over held data is a `KitPage`.
- **[S-9905]** Freshness: `ETag` sends the held entity tag and a 304 reuses the held body (a 304
  with nothing held is a bad response); `None` asks anew each time and lets the cadence keep it
  cheap; a service's own sync token keeps a replica ([S-9907]). An "updated since" filter is never
  used: an item that leaves a filtered set would never be seen to go.
- **[S-9906]** Paging is one loop (`ServicePager`) for every member (`None`, `LinkHeader`,
  `BodyCursor`, `PageToken`, `OffsetLimit`): it stops at the widget's count, at the descriptor's
  page and item bounds, on an empty page, on the service's own end, and on a cursor or address seen
  before (a loop is a bad response, never an endless fetch). A `Link: rel="next"` address is
  followed only on the first page's https origin, without user info; an address with a comma is
  read whole.
- **[S-9907]** A service read through its sync token keeps a typed replica per account
  (`IServiceReplica`: merge a delta onto what is held, validate on load, the token to send), shared
  by every widget of the provider; a widget's list is a view of it (`IReplicaView`), recomputed on
  every merge and at the day's rollover, never a request of its own.
- **[S-9908]** An entry is next asked at its fetch plus the user's cadence, never sooner than the
  service's idle floor (and, read anonymously, its anonymous floor), and never before a block on its
  bucket ends (`ServicePolling.NextPoll`).
- **[S-9909]** What several accounts answer merges into one list (`KitMerge`): newest first (an
  instant, else a day, else the source's order), an item seen through two accounts once (the first
  account's copy), cut to the widget's count.
- **[S-9910]** How a provider's accounts sign in is a closed set of members (`ServiceAuth`), listed
  in the order ⚙ › Accounts offers them (`IProviderModule.SignIn`): the browser flow with PKCE
  and a loopback (`OAuthPkceLoopback`), the device code (`OAuthDeviceCode`), a pasted token
  (`PastedToken`), and none (`AnonymousAuth`, where public reads are allowed). Scopes are joined by
  a space or a comma as the provider wants. A provider the host adds through a password form (IMAP,
  CalDAV, CardDAV) lists none.
- **[S-9911]** A member says how its token goes on a request: `Authorization: Bearer <token>`, or the
  token bare for a service whose personal keys are not Bearer tokens.
- **[S-9912]** Without an account a service is read only where its descriptor allows public reads,
  and never more often than their floor.
- **[S-9913]** A loopback redirect is written as its rule says (`localhost` or `127.0.0.1`, the path,
  the port): any free port where the provider ignores the port of a registered loopback redirect,
  else the listed ports tried in order.
- **[S-9914]** A provider's refresh tokens are stable or rotate; a rotating one is written before the
  new access token is used, inside the one refresh ([S-530]), and the old one is never sent again
  past the provider's reuse grace: when the write fails, the new pair is kept in memory and is what
  the next refresh sends (the failure is logged).
- **[S-9915]** A pasted token is tried only when it has its member's shape (length, alphabet, an
  optional prefix), trimmed; then the provider's identity request checks it, and only a token that
  passes is sealed and kept.
- **[S-9916]** Who signed in, for a provider whose identity is one GET with the new token, is data
  (`IdentityEndpoint`): the address, the id field, the fields that may hold the address (the first
  one given, else the id), the name, and the id's prefix, so an account signed in again keeps its id
  ([S-505]).
- **[S-9917]** Client secrets a provider insists on are build environment, never source: one
  variable, `THEMIA_CLIENT_SECRETS` (`google=...;todoist=...`), keyed by the provider's `BuildSecret`,
  parsed by one rule (`ClientSecretRules`: a key is a module id, a secret 8 to 256 letters, digits,
  `_ - .`; any broken entry fails the whole value, a key given twice too). The log names the keys a
  build holds, never a value.
- **[S-9919]** The device code and the pasted token, run by the host for any provider that offers
  them. A device-code sign-in asks the provider for a code (every field present, the page https),
  shows it large with Copy and the provider's page, polls at the interval the provider names (a
  missing or absurd one is five seconds; five more after every `slow_down`, for good) until the user
  allows Themia, declines, or the code's lifetime ends (fifteen minutes when unnamed); a pasted token
  is checked by its shape and then by the provider's identity request. Either then saves the account
  under `<provider>:<the id its identity named>`, with the kind of member that signed it in
  (`signIn` in `accounts.json`: `browser`, `deviceCode`, `pastedToken`; absent for a provider's only
  flow, a kind the provider does not offer dropped and logged). A pasted token never renews
  (`AccountAuth.Token`): a 401 is "sign in again". ⚙ › Accounts lists every provider with
  sign-in members in its Add menu, opens its first member's panel and links to the others under it;
  an account whose provider grants access per resource has a row to its grant page ("Choose what it
  may read"). The panels' words are English first (`sign-in` strings area).
- **[S-9924]** An account may name its own API root where the descriptor allows it (an Enterprise
  server): https on a public name, without user info. A request the rules built on the descriptor's
  root moves onto the account's; one that is not under it is refused.
- **[S-9925]** One service sync for the app (`ServiceSync`, `IWidgetHost.Services`), the only code
  that asks a kit service for anything ([S-2600]). Each kit widget's config adds a `ServiceInterest`
  (its provider, its query or its view of the provider's replica, its accounts, its count); the
  interests of every view, shown or not ([S-2608]), and the widgets on screen say which entries exist:
  one per provider, account (every account of the provider when the widget names none, [S-7041];
  anonymous only where public reads are allowed) and query, shared by every widget that reads it, at
  the largest count any of them shows. An entry is asked on its provider's Updates row (never below
  the service's floor, [S-9908]), backs off and wakes as every synced thing does ([S-2609]), goes at
  once on Refresh unless its provider said "slow down", is not asked for an account only the user
  can mend, and is let go when no widget reads it. A widget reads its accounts' items merged
  ([S-9909]) with one outcome per account (named when it reads more than one), the accounts with how
  each stands (a sign-in to mend, a refusal), and its service's grant page; it never asks the
  network itself. One log line per change of an entry's state, never one per attempt, with no
  token and no item's text in it.
- **[S-9926]** What an account's entries hold is kept in its sealed `service` shelf ([S-2602]): each
  query's items (with their times, groups and badges) and summary, each replica with its token and
  last full read. On load it is read only for its own account and provider; the items pass the item
  rules again (ids once, bounds, links on the service's hosts, [S-7002]) and a replica the
  provider's validation; an entry is due at once only when older than its cadence. A sign-out purges
  it with the account's other shelves ([S-2607]); the sweep of shelves of accounts no longer signed in
  knows the kind ([S-2665]).
- **[S-9927]** One client for every kit service (`ServiceClient`, no subclass and no branch per
  service): the query's steps first (each a held, conditional, throttled request of its own, its last
  answer used while a block or a blip stops it), then its pages: each request built by the query,
  refused before it is sent while its account's bucket is blocked, sent with the held entity tag
  where the query's freshness allows, classified through `ServiceFailures.Classify` (a throttle, or a
  success that spent the budget, blocks the account's bucket until its time; a code the scheme does
  not know is logged), a 304 answered from the held body, a GraphQL answer unwrapped for the rules,
  mapped by the query, and paged by the one loop ([S-9906]). The items pass the item rules and the
  descriptor's bound. A transport failure is its own state; no account without public reads asks
  nothing.
- **[S-9928]** The client's memory is in memory only: conditional bodies by account and request (at
  most 256, the least recently used out), the blocks by account and bucket, the steps' answers; a
  sign-out forgets the account's.
- **[S-9929]** A preview card's kit widget (a debug render) reads its provider's recorded answers
  through the real rules and sync (`FixtureReplay` over `Fixtures/<state>.json`, copied into Debug
  builds only under the provider's own folder): `items`, `many`, `empty`, `nomatch`, `forbidden`,
  `ratelimited`, `problem` are recordings; `accounts` reads one through two stand-in accounts,
  `stale` answers then finds the network gone, `signin` and `gone` change the stand-in account, and
  `loading` never answers. A recording matches a request by its address and answers in order (an
  entity tag, then a 304). Nothing reaches the network or an account.
- The service layer's conformance checks, over the registry (`tests/Themia.Modules.Tests/DescriptorConformanceTests.cs`)
  and over source (`KitConformanceTests`):
  - **[S-9930]** Descriptors: every sign-in member names https addresses (no user info) and a
    client; a loopback's fixed ports are user ports; a pasted token's shape is sound; a provider asks
    the build for a secret exactly when a browser member needs one, under a key the one rule accepts;
    no two providers share an Updates row or a secret key; a kit service is its provider's (one
    feature, the provider's id, offered by its accounts), https at its root, with its members, its
    hosts (names, not addresses), no credential among its pinned headers, a 429 among its throttle
    statuses, sane bounds and floors, a cadence default among its choices, and a GraphQL service
    reads its errors array.
  - **[S-9931]** Polling: the account service's service path is named only where it is defined and
    by the desktop's transport, the transport is asked only by the one client, and a reply is
    classified only by the client and a new sign-in's identity request; the kit's shared-code rule
    ([S-7071]: no service, provider, service header or error code) covers the service layer in the
    SDK, Core and the app.
- **[S-9920]** A throttle is read through the descriptor's `RateLimitScheme`: its budgets' headers
  (several budgets: a spent one wins), the reset's unit (epoch seconds or milliseconds, or seconds
  from now), the header naming the bucket, the statuses that are a throttle when a budget is spent or
  a wait is named (a 429 always is), and the statuses whose `Retry-After` is not a wait (a 401 that
  wants a new token). The wait is `Retry-After`, else a wait the body names, else the reset, else a
  minute, never past two hours; a success that spent the last request waits for the reset too.
  Unreadable values are absent, never zero. The host's own providers name no budget: a 429, or a 403
  with `Retry-After` (`RateLimitScheme.RetryAfterOnly`).
- **[S-9921]** A reply reaches the one vocabulary through one function (`ServiceFailures.Classify`),
  in this order: a throttle the headers show; a throttle the body shows (a code or a message the
  error scheme maps, a body wait); the body's code; the status (401 sign in again, but a refusal when
  the request carried no credential; 403 not allowed; 404 and 410 not found; anything else
  unexpected). A code the scheme does not know is unexpected and named, so a new code is seen in the
  log. A body that is not JSON carries no code.
- **[S-9922]** The GraphQL transport writes `{"query", "variables"}` through the service's own
  context and reads an answer's envelope: `data` for the rules, `errors` (message, code, path) for the
  error scheme. A 200 with errors is used when the list's own path is present, its errors the detail;
  without it, it is classified by its codes like any failure.
- **[S-9923]** The form-post transport sends its fields as `application/x-www-form-urlencoded`, each
  escaped, in order.

## Adding a widget

- **[S-420]** *(Retired: the picker whose sections were never split across columns (`ColumnLayout`)
  is the edit bar's add menu, [S-3501]; it is a tree of subjects, [S-3510].)* There
  is no second way in: there is no settings window ([S-3585]).
- **[S-421]** Picking a widget does **not** add it and does not open its settings. It starts the
  placement session ([S-1587]): a ghost of the widget at its default size follows the pointer across
  every display, the cards stop taking input so a click over one places the new widget rather than
  grabbing the old, and a line in place of the toolbar says what the click will do. Escape or a
  right-click cancels; leaving edit mode cancels. Paste starts the same session
  with one or many existing widgets.
- **[S-422]** A left click lands the payload on the display it is on, in that display's view, at the
  rects computed from the click itself — not from the last pointer move — centred as a block,
  snapped and clamped ([S-11], [S-1586]). Alt bypasses the snap, as when dragging. What landed is
  selected.
- **[S-423]** A ghost traces the **painted** cards, inside the gap, with the card's own corner
  radius — one outline per widget at its offset in the block ([S-1585]): it shows what the user will
  see once the widgets are there, not the cells they own.

### The edit bar and its pages (EDITBAR-PAGES)

The edit bar is Karpo's command bar (user request 2026-11-12: "the top edit bar should be designed in
the same way that karpo has it"; `karpo-app`, `docs/widgets.md` §575, §578, §584, §593, §594, §596,
§608, §670–§679): a row of page entries, each opening its own flyout hung from it by a beak, the
selection's entry and its page, Add and Done. It replaces the trail, the drop-down menus (⚙, Display ▾,
View ▾) and the properties menu beside the selection, which are gone with their engine. Owning files:
Core `Settings/SettingsPages.cs` (the pages), `Settings/SettingsRows.cs` (the row vocabulary, the
cards), `Settings/NumberEntry.cs`, `Shell/EditBarLayout.cs`, `Shell/SelectionPage.cs`,
`Geometry/FlyoutPlacement.cs`; the metrics `ShellMetrics.Bar`, `.Flyout`, `.Page`, `.Inline`; App
`Shell/EditBar.cs`, `Shell/Pages/` (`PageFlyout`, `PageView*`, `PageRows`, `SelectionRows`),
`Shell/DesktopWindow.BarMenus.cs` (opening, closing and placing the flyouts, one mechanism),
`.Properties.cs` (the selection's entry and page), `.Toolbar.cs`, `.SettingsMenu.cs`. Tests
`SettingsMenuTests` (`S23*`), `EditBarLayoutTests`, `NumberEntryTests`, `ShellTokensTests`.

- **[S-2300]** The bar's **pages**, in its order, each an entry (its glyph and its name) and the flyout
  it opens: **General** (the gear: General's own lines with Snap to grid, then Language & region,
  Online updates, Motion, AI assistants where a server runs, Back up & reset, Themia Pro, About Themia,
  and Quit Themia last), **Accounts** (every account, a page of its own, then Add account), **Display**
  (the display this bar stands on: the view it shows, its scale, its view bar, then what every display
  shares), **Views** (the view shown here: its name, icon and colour, Duplicate, Delete; then every
  view as the user's own things) and **Appearance** (the style, theme and colours, item background, the
  cards, text and size). The lines are the settings' own (`SettingsMenu.*`, `DisplayMenu.Root`,
  `ViewsMenu.Root`); a page only groups them (`SettingsPages.General` … `.Appearance`).
- **[S-2301]** One rule makes a page of lines (`SettingsPages.Sectioned`): a heading line starts a
  section under its words; a submenu that is not a page of its own is a section of its own (its words
  the heading, its lines the section's); every other line joins the section it follows. No section is
  empty; a section's key is the line's it came from, so a deep link finds it in every language.
- **[S-2302]** The **row vocabulary** (`SettingsRowKind`, decided once by `SettingsRows.KindOf` for the
  settings' lines and by `PageRows` for the selection's entries, so a row that does the same is drawn
  the same): a **toggle** (On / Off before Windows' switch, the whole row the switch); a **number** (a
  240 × 32 box showing the value as its line writes it, − and + inside, Up and Down step it, a typed
  value read in the box's units and held to the range and grain, [S-2309]); a **text** box (240 × 32;
  Enter or leaving keeps it, Escape restores it; why a text cannot be kept under the name in the
  critical colour; a form's field hands on every letter; a password's never shows what it holds); an
  **action** (the whole row a button, a danger's in the critical colour, a chevron where it asks first);
  a **link** (a chevron, its tooltip "Opens …"); an **item** (the user's own thing: its icon buttons at
  its end, their keys F2 and Delete while it has the keyboard, dragged or Ctrl+Up / Down to reorder); a
  **note** (words; a reading with its value at the end); the **pickers** (choice, lookup, colour, font,
  icon, [S-2308]); an **expander**; a **sub-page**.
- **[S-2303]** At most **two levels** (Karpo's §594): a card, and one level opened inside it (an
  expander, a picker). A submenu marked a page of its own (`SettingsSubmenu.Page`: an account, Add
  account) or one whose lines open further is a **sub-page**: it stands in the page's place behind a
  32-DIP back head (the back arrow and its title in BodyStrong; the whole head goes back). Inside an
  expander a picker stands open under its name. A line of text asked on its own (a view's Rename tool,
  a widget's Ask) and an "are you sure?" (a preset over one's own look, deleting a view, an import, a
  widget's Confirm) are pages of their own too, the question their head; only their button does it.
- **[S-2304]** **Cards** (`SettingsRows.Cards`), Windows' settings cards: each row a card of its own,
  with the notes under it (what holds against it, how a try went); options of a choice standing open,
  and the things of one list, share one card; a heading's card holds the rows under it to the next
  break. Cards are Windows' card fill and stroke, radius 4, 4 apart.
- **[S-2305]** A page as drawn (`PageView`): 16 in from the flyout's sides and top, 16 under the last
  card; a section's heading in BodyStrong 24 after the previous group and 8 over its cards (the first
  section's heading where the page has one: General's); a row 48 tall, 60 with a caption, its icon at
  16, its name at 48, its control ending 16 from the card's edge. The page is drawn again on every change
  of the state; what is open, the sub-page, the scroll and the keyboard's place (a box's caret too) are
  kept by the rows' keys.
- **[S-2306]** Every setting has **exactly one page** (`SettingsMenuTests.S2306_`): every setting line of
  the section builders, Display ▾'s and View ▾'s lines stands once on one page, and the ownership guard
  ([S-3592]) walks the pages, each section a level of its own. A deep link names a row by its key
  (`SettingsPages.PathTo`: its section, the rows down to it).
- **[S-2307]** A sub-page's **back head**: Escape or the head goes back to the page it stands on.
- **[S-2308]** The **inline pickers** (Karpo's §670–§676): every choice of one, every colour, every font,
  every icon and every lookup is a card that opens, never a popup or a drop-down: its head shows the
  icon, the name, what holds (a colour's swatch and words, a choice's words) and a chevron (up while
  open); opened, the picker stands inside the card under a hairline on Windows' secondary card fill, 12
  in. One card is open at a time, scrolled to the top of the flyout as it opens. A **choice**: radio rows
  36 tall, more than six in up to three columns of at least 180. A **lookup** (the language, a place, a
  time zone): a search box over at most eight results and a status line ("3 found", "No results",
  "Searching…"), the card the same height whatever is typed; its own options filtered as typed
  (`SettingsMenu.Matches`), a search that asks as typing settles; Enter picks the first. A **colour**: 36
  swatches 4 apart (Default first, a fill's Transparent next), the one that holds ringed in the accent;
  a fill's opacity as a number; then the custom picker inline (Windows' spectrum, its hue, a fill's
  opacity, the hex box: letting go of a drag keeps the colour, as leaving the hex box does) and the
  eyedropper. A **font**: the font picker and Add fonts. An **icon**: the icon picker.
- **[S-2309]** A number typed into a number box is read in the units the box shows (`NumberEntry`: the
  digits, one decimal mark of either kind, a minus; "42 %" is 0.42 of a fraction, "1.5 s" 1500 ms), then
  held to the range and the grain (`MenuSlider.Snap`); nothing that reads as a number restores the box.
- **[S-2320]** The **bar** (`EditBar`, laid out by `EditBarLayout.Lay`): one row 48 DIPs tall (8, a 32
  control, 8) at the top centre of each display's work area, 8 under its top, Windows' ground with the
  flyout's rim, radius 8 and a flyout's shadow. Left to right: the pages' entries 4 apart (each 12, its
  16 icon, 8, its words in Body, 12; no fill or rim until pointed at); 8, a 1-DIP hairline 20 tall, 8
  (drawn only while something is selected, its room always kept); the **selection's room**, 256 DIPs
  always kept ([S-2324]); a 16 gutter; the **problems' room** (a 40 icon button while there are
  problems; Themia keeps no standing list of them yet, so the room stays empty); 4; **Add widget**
  (Windows' standard button, its "+" in the accent, [S-3501]); 8; **Done** (the one accent button, at
  least 80, ending 8 from the bar's end). One width whatever is selected and however many problems there
  are. There is no grip (the bar does not move), no Undo or Redo (Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z still
  step the history, [S-885]), no search, no fold and no close button.
- **[S-2321]** The entry whose flyout is open is lit: the subtle fill and the accent's 16 × 3 pill at
  its foot. Each entry's words are its tooltip and its name.
- **[S-2322]** On a display too narrow for the bar the pages' entries show their **icon alone** (40
  wide, their words the tooltip) first; then the selection's room narrows, never under 40.
- **[S-2323]** The divider stands between the pages and the selection only while something is selected.
- **[S-2324]** The selection's entry in its room (`EditBarLayout.Fit`): its natural width, cut short with
  an ellipsis where it does not fit but never under 12 + 16 + 8 + 40 + 12, else its icon alone.
- **[S-2325]** A page's **flyout** (`PageFlyout`, placed by `FlyoutPlacement.Place`): 580 DIPs wide (a 16
  gutter either side of 548 of cards), as tall as its page up to 70 % of the work area (then it scrolls:
  the wheel, Page Up / Down, Home and End), centred on its entry 12 under the bar, pushed in to stay 12
  from the work area's sides and its foot; radius 8, Windows' own colours always (the ground
  `SolidBackgroundFillColorBase`, the cards `CardBackgroundFillColorDefault` with `CardStrokeColorDefault`),
  never the widgets' look. No title: the entry names the page.
- **[S-2326]** Its **beak**: 8 tall and 16 across at its top, its tip over the entry's middle, held to the
  top's straight (the corner's radius and half the beak in from either end), so a flyout pushed in at a
  screen edge still points at its entry; the beak and the panel one outline with one rim and one shadow.
- **[S-2327]** **One flyout at a time**, nothing open as edit mode starts. A page's entry opens its
  flyout, or closes it again; another entry swaps it. It closes on Escape (after going back out of a
  sub-page or an opened card first), on a press anywhere that is not the bar or the flyout (which then
  does what it does: a press on a widget selects it), when the add menu opens, when a placement starts,
  and as edit mode ends; Escape with no flyout open backs out of placing, the selection, then edit mode
  ([S-1583]). Keys: Tab and Shift+Tab walk the bar then the flyout; Left and Right walk the bar (mirrored
  right to left); Enter or Space opens an entry; Down from the bar goes into the open flyout; F6 reaches
  it ([S-1975]); while the keyboard is in a flyout no edit key reaches the selection but Undo, Redo and
  F6. What the General and Accounts pages follow of Windows and the accounts is followed while one is
  open ([S-3525], [S-3559]).
- **[S-2328]** Debug renders: `--editbar-png <outDir> [--theme light|dark] [--lang xx] [--only
  bar|pages|select]` draws the real bar on a stand-in display (`editbar-<theme>.png`, nothing selected;
  `-widget`, one widget, its flyout open; `-many`, two of a kind; `-mixed`, three of several; `-errors`,
  the problems' button; `-open`, General's entry lit; `-narrow` and `-narrowest`, 900- and 640-wide
  displays; `-add`, the add menu; `-placing`), every page's flyout hung from its entry over a busy
  stand-in wallpaper, opened at a row as a deep link opens it, its picker open (`page-<theme>-general`,
  `-general-end`, `-general-motion`, `-general-language`, `-general-language-find`, `-general-advice`,
  `-general-link-temperature`, `-general-assistants*`, `-general-pro-*`, `-general-backup*`,
  `-general-about`, `-general-uninstall-*`; `-appearance`, `-appearance-style`, `-theme`, `-accent`,
  `-background`, `-border`, `-font`, `-replace`; `-accounts`, `-accounts-account`, `-accounts-add`,
  `-accounts-imap*`, `-accounts-caldav`; `-views`, `-views-icon`, `-colour`, `-rename`, `-delete`,
  `-many`, `-many-end`; `-display`, `-display-type`, `-position`, `-bar-views`, `-new`, `-end`), and the
  selection's page beside real cards in edit mode (`select-<theme>-one`, `-style`, `-header`,
  `-background`, `-appearance-end`, `-two`, `-many`, `-toggle`, `-weather`, `-weather-location`,
  `-setup-weather`, `-shortcuts`, `-vocabulary`, `-vocabulary-colour`, `-vocabulary-feeds`,
  `-vocabulary-confirm`), over stand-in hosts that reach neither Windows nor the user's state; then the
  edit markers ([S-31b]). `--editbar-png` writes no `settings-*`, `views-*`, `display-*` or `props-*`
  pictures any more.
- **[S-2340]** The **selection's page** (`SelectionRows`, after Karpo's widget page §529): a header card
  80 tall (a 48 tile with the kind's glyph, the widget's name in Title over a Caption line: its kind
  under a title of its own, what the kind is under the kind's name, how many of what for several; Cut,
  Copy and Remove as icon buttons at its end, their words and keys the tooltip; Remove reads "Remove
  from this view" for desktop items, [S-5524]), then its sections ([S-2342]). It is where a widget's
  options are: there is no widget settings page ([S-6536]).
- **[S-2341]** The **selection's entry** on the bar of the display the selection is on
  (`SelectionPage.Words`): the kind's glyph and name for one widget; "2 × Clock" for several of one kind
  (`QuickSetOffer.CommonKind`); "3 widgets" for several kinds.
- **[S-2342]** Its sections (`SelectionPage.Sections`): **What it shows** (the style first as an inline
  picker, only for one kind with styles ([S-6007]), what that look draws under it, then the widget's own
  lines, [S-3719]; for one widget); **Header** (the one widget's title as a text box, the kind's name its
  hint, [S-3704]; where the header stands, an inline picker; not where a widget selected can never have
  a header, [S-3711]); **Appearance** (Alignment ([S-6584]), Background, Item background, Item text
  ([S-6566], [S-6571], [S-6574]), the background image ([S-6593]), Scale ([S-6531])). Several selected:
  the count in the header card and the shared sections only ([S-3702]); a mixed value reads as a dash.
- **[S-2343]** Selecting a widget adds its entry and **opens its flyout, once per selection**: closed by
  the user it stays closed for that selection; a click on a selected widget opens it again; nothing
  selected removes the entry and closes it. Nothing opens while a drag or a resize runs ([S-3710]) or
  while placing. The card's Set up button selects the widget alone and opens its page at the row its
  module names ([S-1862]). Enter from the selection opens its flyout with the keyboard in it ([S-1974]).

Needs a live pass: the bar and its flyouts on every display at mixed DPI and with a top taskbar (the bar
8 under the work area, a flyout pushed in at a screen edge with its beak over its entry, 70 % of a short
display); the pointer (hover fills, a press outside closing a flyout and selecting a widget, an item
dragged to a new place, a colour's spectrum dragged); the keyboard (Tab and the arrows along the bar,
Down into a flyout, Escape out of a sub-page then the flyout then edit mode, F6, Ctrl+Up / Down on an
item, F2 and Delete on a view); a screen reader on the entries, the cards and the pickers; the selection's
flyout opening once per selection and again on a click; a language switch with a flyout open.

### The edit bar and its add menu (EDITBAR)

The bar itself is EDITBAR-PAGES' ([S-2320]); what stays here is its add menu. Owning
files: `Shell/WidgetPicker.cs`,
`Shell/DesktopWindow.Picker.cs`; rules in Core `Widgets/AddMenu.cs` (tests `AddMenuTests`).

- **[S-3500]** *(Retired by [S-2320] (2026-11-12): the bar is Karpo's command bar of page entries; the trail, Undo, Redo, Snap and ⚙ are gone from it.)*
- **[S-3507]** *(Retired by [S-2320], [S-2341]: there is no trail; the selection has an entry of its own, Display and View are pages.)*
- **[S-3508]** *(Retired by [S-2343]: the selection's entry opens its page; there is no properties menu to put away.)*
- **[S-3509]** *(Retired by [S-2320]: the bar has no Undo or Redo; Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z step the history ([S-885]).)*
- **[S-3516]** **Snap to grid** (grid snap while moving and resizing, [S-06]) is a switch in the General
  page's first section ([S-2300], key `snap`), set there and nowhere else ([S-3590]): not on the bar,
  not on the Appearance page, not in the surface menu.
- **[S-3501]** **Add widget** opens the **add menu** under it ([S-1749]; the surface menu's Add widget
  opens the same menu at the click, [S-1744]): a search box over every widget kind in the catalogue
  (the module registry, `ModuleCatalog.Widgets` [S-6902], so a new widget is listed with no other change), then the tree of [S-3510],
  each row the widget's icon and name on a 32-DIP line, its description the tooltip. No previews.
  Choosing one starts the placement session of [S-421] unchanged.
- **[S-3502]** *(Retired: the catalogue's sections no longer flow through columns as a newspaper's;
  the menu is a tree of subjects, [S-3510].)*
- **[S-3503]** *(Retired with it: two top columns fit every work area; the cascade scrolls sideways
  past 75 % of its width, [S-3512].)*
- **[S-3504]** *(Retired: a search lists its matches flat, [S-3515].)*
- **[S-3505]** *(Retired: the keys walk the tree, [S-3514].)*
- **[S-3510]** The menu is a **tree of subjects**, after Karpo's add list (`karpo-app`,
  docs/widgets.md §7b, §136): it answers the one question a person adding a widget has (what do I
  want?) by subject. Its folders are declared once in Core (`Widgets/AddTree.cs`: `AddFolderId`,
  a closed set, each with its words; its row wears its first widget's icon), and each widget places
  itself in one ([S-6907]); lines part the runs. Three top columns under their headings: **Ready-made**
  (the ready-made groups, [S-2473]), **Popular** and **Browse**; Browse is a row per
  subject; inside a subject a **family** (one widget in several layouts or parts) is one row
  opening its own submenu. Never deeper than that. The catalogue has no sections of its own any more.
- **[S-3511]** What is where. **Ready-made**: This PC, My day, Weather ([S-2474]). **Popular** (each again from its home): Clock, Weather, Forecast,
  Calendar, Mail, Tasks, Notes, Now playing, CPU, Folder, Shortcuts. **Browse**, in
  order: **Time** (clock, date, alarm, countdown, stopwatch | pomodoro, focus session);
  **Calendar & mail** (calendar, next meeting, week planner | mail, tasks,
  contacts | notifications); **Weather & sky** (weather, forecast, rain next hour, air quality | sun,
  moon); **News & markets** (stocks, exchange rates | feed); **Music & media** (now playing,
  audio visualizer, volume, mixer | gallery, video stream); **Notes & tools** (notes,
  scratchpad, clipboard | habits, quote); **Files & apps** (folder,
  shortcuts, recent files, recycle bin); **System** (CPU › meter, details, top processes; Memory ›,
  GPU ›, Network ›, Ping ›, each a meter and its details; the disk meter | Screen
  time › chart, donut, apps); **Devices & power** (Battery › meter and details, Power buttons › the
  seven | brightness, Bluetooth | drive, removable drives); **Quick settings** (the thirteen
  toggles: the radios | the sound | the screen | the PC). Every kind has exactly one home; only
  Popular shows rows again. The calendar, the audio visualizer and the folder are one widget each,
  their former variants its styles ([S-3640], [S-3216], [S-4123]): a row of their subject, and found
  by the variants' old names too ([S-943]). `AddTree.On` resolves the tree against the catalogue's kinds: a folder
  left with one row is that row, with none it is gone, a line stands only between two rows, and a
  kind the tree does not know stands in **More** at Browse's end, never missing, while
  `AddMenuTests` fail until it has a home.
- **[S-3512]** Layout: 240-DIP columns 8 DIPs apart; a heading line, then 32-DIP rows; a line is a
  hairline with 4 DIPs either side. A folder's row ends in a chevron (mirrored right to left). An
  open submenu is **not part of the menu** (user requirement, after Karpo's add list, whose
  submenus are windows of their own): it is a card of its own in a popup, on the flyout's own
  chrome (acrylic, stroke, 8-DIP corners, shadow), with no heading — the row that opened it stays
  lit and names it. It stands beside that row, its first row level with it, lapping the column 4
  DIPs; it opens forward (right, left to right) while it fits in the work area, else backward, and a
  cascade that turned keeps that way; it never leaves the work area (`AddCascade`, tests
  `AddMenuTests`). Opening or closing one moves and resizes nothing else. The popups are the
  picker's children, so the flyout's one light dismiss closes all of it. The menu itself is its two
  top columns, as tall as the taller one (at most 70 % of the work area). A folder holds at most 12
  rows, a line counted, except Quick settings (every toggle).
- **[S-3513]** Search lists each widget once, at its home, and matches it on its name, keywords,
  kind, description and **the names of the folders it is in** (subject and family: [S-940]'s
  category field), so "calendar" finds the week view and "system" the meters.
- **[S-3514]** Keys and pointer (`AddMenuKeys`): the search box keeps the focus, so typing searches
  at any time, and the highlight is drawn, not focused. Down from the box goes to Popular's first
  row, Up from a top column's first row back to the box; Up and Down skip lines and stay in their
  column, and a move in a column closes the submenus it had opened; Home and End go to its ends.
  Right on Popular goes to the nearest row of Browse, Left back. **Right, Enter or Space on a folder
  opens it** with the highlight on its first row; **Left closes a submenu** back to its row; Escape
  closes the innermost submenu, and with none open closes the menu. Enter or Space on a widget adds
  it. In the box, Left, Right, Home and End move the caret and Space is typed. Right to left the
  arrows swap, as the cascade opens to the left. The pointer opens a folder by a click at once, or
  by resting on its row for Windows' menu delay (`SPI_GETMENUSHOWDELAY`); resting on another row of
  a column closes what that column had opened.
- **[S-3515]** Typing lists the matches **flat, in place of the tree**, best first: one column of
  rows, each its icon, its name and under it where it lives ("System › CPU", "Quick settings"). The
  menu keeps its size; nothing matching says so. Down from the box walks the results, Up from the
  first goes back; Enter adds the highlighted one, else the best match (drawn highlighted); Escape
  empties the box, back to the tree with nothing open, and an empty box's Escape is the tree's.
- **[S-3506]** *(Retired by [S-2328]: the bar's pictures are `editbar-<theme>*.png` of the new bar; `--picker-png` is unchanged.)*

Needs a live pass: the bar on every display at mixed DPI and with a top taskbar (the trail's one
width, a narrow display's crumbs shrinking, the bar 8 DIPs off its sides), the widget crumb coming
and going with a selection and putting the properties menu away and back, Undo and Redo greying and
lighting as changes, drags and steps happen, Snap in step with the grid, the menus built again after
Done and opened at once; the menu opening under Add widget and at a right-click (and growing sideways as a submenu opens, near a display's
edge too), the hover delay, the keys in the menu, and a pick starting the placement.

### The edit bar's settings menu (SETTINGS-MENU)

Since 2026-11-12 these sections are the edit bar's General, Accounts and Appearance pages
(EDITBAR-PAGES, [S-2300]); the ⚙ drop-down and its menu engine are gone, and "⚙ › X" below names
the section X of its page. The app's settings moved out of the settings window into the edit bar, as Karpo's did (`karpo-app`,
`docs/widgets.md` §335–§341; user request 2026-10-25), one part at a time: General, Appearance,
Accounts (SETTINGS-ACCOUNTS, [S-3550]), Language & region, the online update intervals, and Themia
Pro, Back up & reset and About (SETTINGS-ABOUT, [S-3570]) are here; then the window, empty, went
([S-3585]): every way into settings opens the edit bar's menus ([S-3534]). The Displays and Views pages are the bar's
Display and View menus (BAR-MENUS, [S-3540]). Owning files: Core `Settings/SettingsMenu.cs` (the lines' vocabulary, the
first level) and one builder per section (`SettingsMenu.General.cs`, `.Appearance.cs`, `.Motion.cs`,
`.Accounts.cs`, `.Language.cs`, `.Online.cs`, `.Pro.cs`, `.Backup.cs`, `.About.cs`), `Settings/SettingsPages.cs` (the pages); App `Shell/Pages/` (the pages
drawn, [S-2302]), `Shell/DesktopWindow.BarMenus.cs` (opening, closing and placing every flyout, one
mechanism), `Shell/DesktopWindow.SettingsMenu.cs`, `Services/AppHost.SettingsMenu.cs`. Tests `SettingsMenuTests`.

- **[S-3520]** *(Retired by [S-2300], [S-2327] (2026-11-12): there is no ⚙ drop-down; Themia's settings are the General, Accounts and Appearance pages, each a flyout hung from its entry.)*
- **[S-3521]** *(Retired by [S-2327]: one flyout at a time, closed on its entry, Escape, a press elsewhere, the add menu, a placement or Done.)*
- **[S-3522]** *(Retired by [S-2325]: a flyout hangs from its entry by a beak (`FlyoutPlacement`); `DropDownPlacement` is gone.)*
- **[S-3523]** *(Retired by [S-2327]: the keyboard in a flyout reaches no edit key of the selection but Undo, Redo and F6.)*
- **[S-3524]** The settings' sections are Core lines (`SettingsMenu.General`, `.Language`, `.OnlineUpdates`,
  `.Motion`, `.Assistants`, `.Backup`, `.Pro`, `.About`, `.Accounts`, `.Appearance`, over an
  `ISettingsMenuHost`: `SettingsToggle`, `SettingsChoice` of `SettingsRadio`s, `SettingsSlider`,
  `SettingsColour`, `SettingsFill`, `SettingsFont`, `SettingsSubmenu`, `SettingsCommand`, `SettingsNote`,
  `SettingsHeading`, `SettingsLink`, `SettingsReading`, `SettingsField`), each keyed by a path that is the
  same in every language (`appearance/cards/radius`); the pages group them ([S-2300], [S-2301]) and draw
  each line as the row of its kind ([S-2302]). A section with no lines is not there. There is no first
  level any more: `SettingsMenu.Root` is gone.
- **[S-3525]** **General**: **Start with Windows** ([S-933]; its glyph a speedometer, as Task Manager
  marks its Startup apps, never the power button Quit Themia wears; ticked while Windows will start Themia,
  read from Windows each time the menu opens and the stored value until then; a click asks Windows and
  the tick shows what it reached), with under it, as notes, what holds against it ([S-935]: switched
  off in Windows, a policy, a change that did not take, the raw error under that advice only);
  **On the desktop** (Themia's items, Windows' icons or Nothing, [S-5500]); **Pause during full-screen apps** (game mode, [S-920]; "Paused now" at its end
  while it holds the widgets).
- **[S-3526]** **Appearance ›**, the look every widget shares ([S-3830]), behind dividers: **Style ›**
  ([S-3527]); **Theme and colours ›** (Theme › Use system setting, Light, Dark; Accent, a colour whose
  Default is the Windows accent and whose swatches are Windows' palette; Background, the widgets'
  fill, [S-6568]; Blur behind widgets, a switch, [S-2110]; Header, the header strip's colour, Same
  as the widget by default, [S-828]; Header opacity, a slider 0–100 % in fives); **Item background ›** (Background, the items' fill; Item text
  colour, [S-6545]); **Widget cards ›**, its value the border's style (Corner radius 0–32 px; Gap
  between widgets 0–48 px in twos; Border › None, Solid, Dashed, Dotted; Border width 0–8 px; Border
  colour; Border opacity 0–100 %); **Text and size ›** (Font ›, [S-3528]; Text size 50–200 % in
  fives); and **Motion ›** ([S-3529]). There is no Layout › and no Widget scale ([S-3590]): snapping
  is the edit bar's ([S-3516]) and a scale a display's ([S-850]) or a widget's ([S-6531]); the gap
  is with the cards it parts. A card's own look (its Appearance foot, [S-3739]) overrides these per
  widget; its Default is this. Each line writes
  the field its page row wrote, in one change (one undo step), and the appearance engine applies it
  live ([S-822]); a slider stores once, on the release ([S-3731]); the option that holds changes nothing.
- **[S-3527]** **Style ›** lists the presets, the tick on the one the design matches, and Custom
  (greyed, with the tick) while it matches none ([S-834]). A preset picked over a look of the user's
  own asks first, on a level of its own ([S-6483]: the app's one "are you sure?", [S-3737], "Replace
  your custom look?", what it replaces and what stays under it, Replace and Cancel); only Replace writes it.
- **[S-3528]** **Text and size › Font ›**: its level holds the font picker (`FontPicker`, [S-1723]:
  its search box over the presets and every installed family, each drawn in its own font, the chosen
  one ticked, [S-6440]..[S-6442]), then **Add fonts** ([S-6443]). The Font line's value is the chosen
  font's name ([S-6441]). A pick stores it, and the picker is drawn again with that one ticked.
- **[S-3529]** **Motion ›** ([S-1014]): Startup animation ›, Unlock animation ›, View change
  animation ›, each with its style at its end and as its level the ten styles, the tick on the one that holds,
  **Length** (the animation's, a slider from 0.1 to 5 s in tenths, written in seconds) and **Play it now**;
  and, while Windows' animation effects are off, a note that nothing plays ([S-1010]). Live values
  have no line here: they never move ([S-6330]; the Smooth value changes switch is gone).
  A new style or length is stored and played on the desktop at once.
- **[S-3530]** **Online updates ›** ([S-3780]): one line per cadence row (the host's, then any
  provider's own, [S-6910]), its value the interval that holds ("Every 30 minutes") and its level
  the row's intervals with the tick, then a note of what the intervals mean. A pick is stored with no
  relayout.
- **[S-3531]** The menu follows the state: every change (its own, an undo, an import, another
  display's) is written into its rows in place, so the open level, a slider and the focus stay
  where they are; what Windows reports (start with Windows, the desktop icons, game mode holding the
  widgets) updates it the same way. A language switch writes its words in the new language and keeps
  what is open open: the lines' keys do not change.
- **[S-3532]** **Quit Themia** is the General page's last row ([S-67]): it quits on every display.
- **[S-3533]** *(Retired by [S-3584]: the settings window has no page left, so there is no All
  settings… and nothing opens it.)*
- **[S-3534]** The ways in, each landing on its row (page and row, the row scrolled into view and lit for
  a moment): the General entry; the surface menu's Settings, View settings and Display settings
  ([S-1744]: the General, Views and Display pages on that display); starting Themia again by hand
  ([S-66]: the General page at its top on the main display); a widget's way to its accounts ([S-3558]:
  the Accounts page, or Add account while there is none), a widget's link row ([S-3749]: the row that
  owns the setting) and a refusal's See plans ([S-3573]: Themia Pro). Nothing opens a settings window:
  there is none ([S-3585]). `DesktopWindow.RequestSettings(key)` opens the page that holds the row
  `key` at it (`SettingsPages.PathTo`), as clicks would; `OpenPage(page, path)` opens any page.
- **[S-3536]** **Language & region ›**, every localisation choice in one place, as the settings
  window's Language & region page held them ([S-3797]; the page is gone, with its navigation entry,
  search entries and globe icon): **Language ›** ([S-1109]), its value the language Themia speaks in
  its own words; its options **Windows setting (…)** first, naming the language Windows gives Themia
  ("Windows setting (Deutsch)"), then the forty, each in its own words in every language; a pick is
  stored and the whole app speaks it at once ([S-1110]), the menu staying open at the same level in
  the new language ([S-3531]). Under a divider the regional formats every widget shares ([S-3770]):
  **Time format ›**, **Start of week ›** and **Temperature unit ›**, each offering what Windows says
  first ([S-3771]: "Windows setting (12-hour)") and writing its field of `Preferences.Region` with
  no relayout. Each line's value is what is in force ("12-hour", "Sunday", "°C", "English"), short
  enough to stand beside its words; its level says where it comes from. The line's own value is
  the language spoken. There is no link to Windows' region settings: the page had none.
- **[S-3537]** The languages are a long choice: their level has the engine's filter under its head
  ([S-3742]), its hint "Find a language" (`SettingsChoice.Filter`): each letter typed keeps the
  options whose words or keywords (`SettingsRadio.Keywords`: a language's English name and code) hold
  it, whatever the case (`SettingsMenu.Matches`), "No results" when none does; Enter picks the first
  kept, Down moves to it; nothing goes out to be asked, so nothing waits. Tests `SettingsMenuTests`
  (`S3536_`, `S3537_`).
- **[S-3535]** *(Retired by [S-2328]: the settings pages' pictures are `page-<theme>-general*`, `-appearance*` and `-accounts*`.)*

Needs a live pass: ⚙ on every display at mixed DPI and with a top taskbar (the menu under the
button, its one rectangle inside the work area, the view bar at the top); Windows' text size above
100 % (the rectangle growing with it); the keyboard (focus in it on open, a level in and out by the
arrows, Escape back one level then to ⚙, the mouse's back button, F6, Undo inside it); the slide
going in and back and with Windows' animations off; a press on a widget closing it and selecting
the widget; Start with Windows against Task Manager's switch and in the packaged build (the startup
task); Show desktop icons followed while the desktop's own menu flips them; game mode's "Paused now"
on a second display; the font picker's search and the installed fonts; the colour dialog and the
eyedropper from a colour level; every Appearance line applying live; a language switch with a
level open; Quit Themia.

### The edit bar's View and Display menus (BAR-MENUS)

Since 2026-11-12 View ▾ and Display ▾ are the Views and Display pages (EDITBAR-PAGES, [S-2300]): their
first levels are the pages' sections, their submenus the sections or expanders of [S-2301].

The Views and Displays pages of the settings window moved onto the edit bar (user request
2026-10-25, step 2 of the move of [S-3520]), after Karpo's Display ▾ and Layouts menu (`karpo-app`,
`docs/widgets.md` §336, §348). Owning files: Core `Settings/ViewsMenu.cs`, `Settings/DisplayMenu.cs`
(the lines, over `IViewsMenuHost` / `IDisplayMenuHost`), `Settings/SettingsPages.cs` (the Views and Display
pages); App `Shell/DesktopWindow.BarMenus.cs` (the one mechanism every flyout opens, closes and is
placed by), `Shell/DesktopWindow.ViewsMenu.cs`, `Shell/DesktopWindow.DisplayMenu.cs`,
`Shell/Pages/`, `Shell/IconPicker.cs`. Tests `ViewsMenuTests`, `DisplayMenuTests`.

- **[S-3540]** *(Retired by [S-2300], [S-2327] (2026-11-12): Display ▾ and View ▾ are the Display and Views pages; their content is [S-3541]..[S-3545], [S-3549].)*
- **[S-3541]** **View ▾** is the view this display shows (its assignment, else the active view,
  [S-1706]) and, behind Manage views ›, every view (Karpo's Layout ▾). Which view a display shows is
  set in Display ▾ alone ([S-3543], [S-3590]): no line here shows a view. The lines are Core's
  (`ViewsMenu.Root`), keyed by what they are (`name`, `icon`, `manage/view:<id>`), so a rename, a move
  or a language switch keeps what is open open.
- **[S-3542]** Its first level: how many widgets the view holds (a note); **Name** (its value the
  name; its level the field holding it, [S-3736]; `ViewOps.Rename`, [S-1707]); **Icon ›**, whose level is
  the icon picker ([S-1803]) at the menu's width, its default cell the view's initials ([S-903]), a
  pick stored at once and drawn ticked; **Colour ›**, the one colour level ([S-3724]: Default, the
  theme's text colour, then Windows' colours, custom and the eyedropper; `ViewOps.SetColor`, no
  relayout); under a divider **Duplicate view** ([S-900], the copy shown here) and **Delete view** (in
  the critical colour; asked first on a level of its own, "Delete "Work"?" with what goes with it,
  Delete and Cancel, [S-3737]; the undo notice still offers it back, [S-881]; greyed on the last view,
  [S-12]); under another **Manage views ›** ([S-3549]), its value how many there are.
- **[S-3549]** **Manage views ›**, Karpo's level of things: **New view** on top (an empty view at the
  end, shown here; past the free limit refused with a notice saying why, [S-1065]), a rule, then
  every view in the user's order, its icon in its colour and its name, with at its end **Rename**
  (the pencil, F2: its field on the row's level, [S-3736]) and **Delete** (Del: its "are you sure?" on the row's
  level, [S-3737]; greyed on the last view), 24-DIP subtle squares whose names (and keys) are their
  tooltips, and the row's grip: dragged ([S-3747]) or Ctrl+Up / Down ([S-3748]) a view takes another's
  place (`AppHost.ReorderView`, one undo step), and the view bar, Ctrl+n and the menu follow. A click
  on a view's row does nothing of its own. The vocabulary grows `SettingsItem` (a thing with tools
  and an order) and `SettingsTool` (a glyph, a name, a key, and a line of text it asks or a command);
  the engine draws them (`MenuNode.Tools`, run with the row in hand). A view's name is reached here
  and on the first level's Name: one field of one level, View ▾'s ([S-3592]).
- **[S-3543]** **Display ▾**: this display under its name (a note: "DELL U2723QE — 3840 × 2160
  (primary)", `MonitorTarget.Describe`, so two identical screens are told apart): under the heading
  **View on this display** ([S-3740]) every view as a row of its own (Karpo's inline choice: its icon in
  its colour, its name, the tick on the one this display shows), a click showing it here ([S-19],
  `AppHost.ShowView`; switching views is here alone, [S-3590]); under a divider **Widget scale**, a
  slider 50–200 % in fives ([S-850], `AppHost.SetDisplayScale`, stored on the release; the only scale
  besides a widget's own, [S-6532]), and **View bar ›** ([S-3544]); then under a divider and the
  heading **All displays**: **New displays show ›** and **Show Themia on ›** ([S-3545]). The lines are
  Core's (`DisplayMenu.Root`, the views keyed `show:<id>`); they follow the state and, while the menu
  is open, every display pass (a display plugged in or switched off).
- **[S-3544]** **View bar ›**, this display's bar ([S-906]), its value where it sits or **Hidden**:
  **Show the view bar**, **Type ›** (Inline, Select, [S-5400]), **Labels ›** (Icon and text, Icon
  only, Text only, [S-5405]), **Position ›** (the eight anchors), **Size ›** (Medium, Large, Extra
  large), each choice with what holds at its end; under a divider **Views ›** (the
  views on the bar), its value "All views", the one view's name or "2 of 4 views" (`ViewBarRules.Offer`): **All views**
  (on while the bar offers every view; switched off it keeps only the view this display shows,
  `ViewBarRules.SetAllVisible`), a divider, then a switch per view with its icon in its colour
  ([S-908]: the last one stays on; every switch is read back from what was stored). Each writes this
  display's `ViewBarSettings` in one change; the option that holds changes nothing ([S-3718]).
- **[S-3545]** What every display shares: **New displays show ›** ([S-847]): The last view shown, or
  a view (each with its mark), stored with no relayout. **Show Themia on ›** ([S-845]): a switch per
  connected display, switched off or not, by its name, this one saying **This display**, so a display
  switched off (which has no surface, so no bar) is switched back on from another's menu; the last
  display on is greyed, with "Themia has to stay on at least one display" under the list. Switching
  this display off puts its menu away first and runs once the click has finished: the surface the
  click came from closes.
- **[S-3546]** The ways in: the Display and Views entries, the surface menu ([S-1744]), and a view
  chip's **View settings** ([S-1708]: that view shown here, then the Views page open on this display).
- **[S-3547]** *(Retired by [S-2302]: the row vocabulary is the pages' one, for every page.)*
- **[S-3548]** *(Retired by [S-2328]: `page-<theme>-views*` and `page-<theme>-display*`.)*

Needs a live pass: View ▾ and Display ▾ on every display at mixed DPI and with a top taskbar; a
click on a view showing it and opening its level; dragging a view's row ([S-3747]: the list holds the
pointer, the release drops it) and Ctrl+Up / Down in the menu; the icon picker's search typed into
on the desktop surface; Rename's field and Delete's question on their levels; New view past the free limit (the notice); a view chip's View settings; Show Themia on with a
second display, switching this one off from its own menu and back on from the other's; a display
plugged in while Display ▾ is open; a long list of views scrolling under the bar.

### One setting, one level (OWNERSHIP)

After Karpo's rule (`karpo-app`, `docs/widgets.md` §351, `OptionLinks.cs`; user request 2026-10-26:
"global settings are linked to and not repeated in the widget menus, so that it is clear when i am
editing a global setting vs local widget setting; settings should generally not be repeated").
Owning files: SDK `Modules/SettingLink.cs`, `WidgetMenuLines.Link`; Core `Settings/SettingsPages.cs`;
tests `WidgetMenuLinesTests` (`S3749_`), Core `SettingsMenuTests.Ownership.cs` and Modules
`SettingOwnershipTests` (`S3592_`).

- **[S-3590]** Every setting is at **one level** — a widget's (the selection's page, [S-2340]), a
  view's (View ▾), a display's (Display ▾) or Themia's as a whole (⚙) — and that level's menu alone
  sets it. A lower level that follows something a higher one owns gets a **link row** to it
  ([S-3749]), never a copy: no widget carries its own clock format, start of week, temperature unit,
  update interval or accounts. Nor is one thing set twice at one level: the header's title is the
  Header row's Title alone ([S-3704]; the weather's Location name switch, [S-2849], and the sky's
  Place name line, [S-3101], set it a second time and are gone). A card's own look (the Appearance
  foot, [S-3739]) is a level of its own over the look every widget shares (⚙ › Appearance): its
  Default is that look, so it is an override, not a copy.
- **[S-3591]** Who sets what, after the audit of every bar menu and every widget's menu (2026-10-26):
  ⚙ (Themia as a whole): start with Windows, the desktop icons, pausing in full screen, the look every
  widget shares, the accounts, the language and the regional formats (time format, start of week,
  temperature unit), how often each kind of online source is asked, the licence, the settings file.
  Display ▾ (one display): the view it shows, its widget scale, its view bar; and what every display
  shares (new displays, where Themia is on). View ▾ (one view): its name, icon, colour, order.
  The edit bar: snap. A widget's menu: what is its own (its place, its zone, its feeds, its look's
  options, its card's own look). What went: Snap in ⚙ › Appearance › Layout and in the surface menu
  ([S-3516]); the app-wide widget scale (`Design.UiScale`, the display's scale for every display
  under the same words, [S-6532]); switching views in View ▾ (Display ▾'s, [S-3543]); "Duplicate
  this view" beside a view's own Duplicate view; the weather's Location name switch and the sky's
  Place name, second ways to the header's title ([S-2849], [S-3101]). Links added ([S-3749]): Time
  format (Clock), Start of week (Calendar's month, week and year; Week planner), Temperature unit
  (Weather now, Forecast), Update interval (Weather now, Forecast, Rain, Stocks, Feed). Followed now
  that was not: the alarm's days start on the start of the week ([S-4631]), a note's own font size
  composes with the text size ([S-1349]). Left as they are, each the one place it is set: the
  screen-time limits and hidden apps (one store, set from any screen-time widget's menu, each saying
  it is shared, [S-3322]); a clipboard widget's Entries kept (its own, the history keeping the most
  any widget asks); a quote's change cadence and a video stream's refresh (their own, no ⚙ row).
- **[S-3592]** The guard (Karpo's `NoSettingIsSetAtTwoLevels`): Core runs every setting line of ⚙,
  Display ▾ and View ▾ and the bar's Snap one at a time on a state of its own and reads what each
  wrote off the state file's shape: no field is written from two levels (a view's name, on its Name
  row and its row of Manage views, is View ▾'s twice by design), every field of the look and the
  behaviour has its level, no two menus call a setting by the same words, and snap is the bar's
  alone. Modules builds every widget's menu: no line sets something under the words of a setting the
  bar's menus or the card's own rows set unless it is a link (a focus session's Length is its own),
  and no widget's config has a field of a type only a higher level holds (a temperature scale, a
  clock, a week start, a cadence, a theme, a language).
- **[S-3593]** Edit mode's menus live only while edit mode does (user request 2026-10-26: "release
  the edit-mode menus when edit mode ends"). The bar's buttons and its add menu are built as edit
  mode starts (`EditBar.Fill`), a page's flyout as it opens (`OpenPage`, `OpenSelectionPage`) and
  dropped as it closes; when edit mode ends the open flyout goes, the widget's lines with it, and the
  bar's buttons and add menu with them (`EditBar.Release`);
  only the surface's layers and the bar's empty frame stay. Each is built again as it is next
  needed. Measured (`--switch-check 1 <out.txt> --edit-menus`, Debug, the ordinary view): every bar
  menu opened 150.9 MB private, after Done 136.9 MB (14 MB given back; the native heaps 59 → 45 MB);
  a first open of Display ▾ 31 ms, View ▾ 14 ms, ⚙ 59 ms, and ⚙ again after Done 19 ms.

### The edit bar's Accounts (SETTINGS-ACCOUNTS)

The settings window's Accounts page moved into ⚙ (user request 2026-10-25, step 3 of the move of
[S-3520]), after Karpo's ⚙ › Accounts (`karpo-app`, `docs/widgets.md` §338). Owning files: Core
`Settings/SettingsMenu.Accounts.cs` (the lines), `Settings/AccountsMenu.cs` (`IAccountsMenuHost`,
`AccountsMenuState`, `AccountStatus`), `Settings/AccountForm.cs` (the forms as drafts),
`Accounts/AccountRules.ImapServerProblem`; App `Services/Accounts/AccountsMenuHost.cs` (the flows over
the account service), `Shell/Pages/PageView.Rows.cs` (the fields, the masked one, notes written in
place), `Shell/DesktopWindow.SettingsMenu.cs` (`RequestAccounts`). Tests
`SettingsMenuTests` (`S355x_`, `S3561_`), `AccountFormTests`.

- **[S-3550]** **Accounts ›** on ⚙'s first level, after Appearance: what the Accounts page said
  first (why the accounts file could not be used, in the critical colour; "No accounts yet" while
  there are none), then every account as a row of its own (its name, its provider's glyph, or the
  warning glyph and **Sign in again** at its end while it needs a sign-in, [S-3552]), then under a
  divider **Add account ›** ([S-3553]). The line itself says Sign in again while any account needs
  it. Without an account service (a failed start) it says accounts are not available. The lines
  are Core's (`SettingsMenu.Accounts` over an `IAccountsMenuHost`), keyed by the accounts' ids
  (`accounts/account:<id>`), so a rename or a language switch keeps what is open open.
- **[S-3551]** An account's level ([S-3821]'s group, moved): its address and provider; a
  **reading** per kind its provider feeds, in the order mail, calendar, contacts, tasks
  (`SyncStats.Summarize`): its name and count ("Mail · 1,240 messages") and under it "in 3 folders ·
  synced 5 min. ago · 2 changes waiting to sync", or "Nothing stored on this PC yet"; where a service
  grants access per resource, **Choose what it may read** ([S-9919]); **Nickname**, its value the
  nickname, a line of text on a level of its own ([S-3736]) with the one there and the account's own
  name as the hint, an empty field clearing it (`AccountService.RenameAsync`); and under a divider **Sign out** in the
  critical colour, asked first as a destructive question on a level of its own ([S-3737]: "Sign out of Alex Wilber?", what
  goes and that nothing changes at the provider, Sign out and Cancel). What a rename or a sign-out
  could not do stands under its line in the critical colour. There are no switches for an account's
  parts and no colours: the page had none, and nothing behind it turns a part off.
- **[S-3552]** An account that needs signing in again ([S-3822], moved) says why under its address
  (in the critical colour), then **Sign in again**: for a provider with a sign-in of its own the
  sign-in that added it (the browser, a code), which replaces its sign-in in place, how it goes under
  the line; for a mail, calendar or contacts server **Sign in again ›**, that server's form ([S-3554]).
- **[S-3553]** **Add account ›**: every offered provider ([S-3562]) with a sign-in of its own in its
  place ([S-9910]: Microsoft), then under a divider the password servers' forms: **Mail server
  (IMAP) ›**, **Calendar server (CalDAV) ›**, **Contacts server (CardDAV) ›**. A provider's line is its sign-in
  ([S-1783]'s card, moved): a click starts it, the line greyed while it waits; under it what it is
  doing with a ring ("Opening the browser…", "Waiting for the browser. Finish signing in there."; a
  device code's code with **Copy code** and **Open the page**, [S-9919]) and **Cancel**; once back,
  who it signed in ("Signed in to Microsoft as …") or why it did not, in the critical colour. A
  provider this build cannot sign in to ([S-536]) is greyed, why under it. A provider that signs in
  with a pasted token has a form of one masked field ([S-9915]); one with several ways has a submenu of
  them, a level of their own. The browser opens only from a click; nothing is signed in from a widget.
- **[S-3562]** A provider may be **held back** (`IProviderModule.Offered` false; `ProviderCatalog.Offered`
  is the rest): Add account has no line of it and a widget's Set up does not count what it feeds as
  something an account could bring ([S-1862]); nothing else knows. Its accounts already in
  `accounts.json` are untouched: they load, sync, are listed under ⚙ › Accounts with their numbers,
  sign in again the way they were added ([S-3552]) and sign out, so no data is dropped and a widget
  reading one keeps showing it. **Google is held back** while Google has not verified the OAuth app
  for its restricted Gmail scope (docs/GOOGLE-VERIFICATION.md); its sign-in, sync and tests stay, and
  offering it again is deleting `GoogleProvider.Offered` (`src/Themia.Core/Accounts/HostProviders.cs`)
  and naming Google again where the texts stopped: the Contacts and Tasks descriptions, the Tasks
  menu's no-account note and the Tasks and Calendar picker keywords.
  Gmail itself can still be added as a mail server with an app password ([S-522]).
- **[S-3554]** The forms, a draft each (`AccountForm`, one per kind, the same wherever it is opened
  from, kept while menus come and go), each a level of one-row rows as Karpo's (§338): every field a
  row with what it holds as its value whose level is the field alone ([S-3736]), every letter written
  into the form as it is typed: the mail server's **Email address**, **Password** (masked,
  [S-3556]; under it "Some providers want an app password here" where Themia knows no provider for
  the address), **Set the server myself** and, while it is on, **Server**, **Port** (empty for 993,
  digits), **User name** ("Usually your address") and the note "IMAP over TLS", then **Nickname**
  ("Optional"); over them, what Themia found for the address ([S-522]: "Gmail: …" and its advice).
  Switched on, the server's fields start from what Themia knows for the address. A calendar or
  contacts server's: **User name** ("Often your email address"), **Password**, **Set the server
  myself** and **Server** with the page's words under it, **Nickname**. There is no security choice:
  Themia speaks IMAP over TLS only (the classic app never spoke STARTTLS, see "Not ported"). A field
  writes the form and never the state; nothing is saved until Add has proven the account.
- **[S-3555]** **Test connection** and **Add**, under a divider: greyed while the form lacks
  something, what it lacks over them in the page's words ("Enter the email address and its
  password.", "Themia does not know the server for example.com. Set it yourself.", a host name, a port
  from 1 to 65535, the server's web address); the mail server's rule is the service's
  (`AccountRules.ImapServerProblem`, one check). A try greys every line of the form, "Checking…" with
  a ring under the line tried; once back, how it went stands under that line: "Connected to
  imap.example.com." (a test, nothing kept: `AccountService.TestImapAsync`, `TestDavAsync`), the
  server's refusal in the critical colour, or the page's "Connected anna@example.com." (an add, through
  the page's `AddImapAsync` / `AddDavAsync`, which prove before they save, [S-528], [S-517]); the menu
  stays open and the new account is listed above. An add that worked empties the form and wipes its
  password; an edit takes the last outcome away. The form's rows keep their place while it is typed
  in and tried, so the field being typed in keeps the keyboard.
- **[S-3556]** The masked field (`SettingsField.Masked`): its row's value is ●●●●●●●● while the form
  holds a password, nothing otherwise; its level's field is Windows' `PasswordBox`: a bullet for each
  letter, no copy or cut (paste works), no reveal button, the IME off while it has the keyboard, and
  to UI Automation an edit named for its row that IsPassword and exposes no value. What it holds is
  never in a row (`Value` null, `HasValue` only), a tooltip, a render's name or the log ([S-983]); a
  field drawn again starts from what the form holds (`Fill`), and Escape gives the form back what it
  held as the field's level opened. The form keeps the password as characters it wipes
  (`AccountForm.WipePassword`): as the form's level closes (`Wipe`, going back from it or the menu
  closing), as an add succeeds, and as the menu closes; .NET's strings cannot be wiped, so the typed
  words pass through one on their way to the form and to the service, as they did on the page.
- **[S-3557]** Closing ⚙ (the last one open on any display) gives up every sign-in and try still
  out, as leaving the page did, and empties every form, its password wiped, every status gone
  (`AccountsMenuHost.Closed`, `AccountsMenuState.Close`).
- **[S-3558]** A widget's ways to its accounts open **⚙ › Accounts on its display**, entering edit
  mode: its menu's Manage accounts and Add an account ([S-3745], [S-7063]), its card's "Open Accounts
  settings" and its header's sign-in problem (`IWidgetHost.ShowAccounts`), and the Set up button of a
  widget that lacks only an account ([S-1862]), which opens it at Add account while there is no
  account (`DesktopWindow.RequestAccounts`). The widget's menu shares no add-account rows of its own:
  one place adds and mends accounts, and the SDK is unchanged but for its words. Nothing opens a
  window for an account.
- **[S-3559]** While ⚙ is open it follows the accounts and their numbers (`AccountsChanged`,
  `SyncHost.StatsChanged`) and every flow's progress, written into its lines in place ([S-3531]).
- **[S-3560]** Debug renders (`--editbar-png`, [S-2328]): `page-<theme>-accounts`,
  `-accounts-account` (a page of its own), `-account-again`, `-signout` (its "are you sure?" page),
  `-add`, `-signin`, `-imap`, `-imap-filled`, `-imap-testing`, `-imap-failed`, `-caldav`, over a stand-in host with three made-up
  accounts and their numbers; no sign-in, server or account file is reached, and the password drawn
  is made up for the picture.
- **[S-3561]** The vocabulary of [S-3524] grows what forms need, for every menu: `SettingsReading`
  (words and a value, nothing to do), `SettingsField` (a row whose level is a line of text typed in
  the menu, [S-3736], each letter handed on as it is typed, Enter going back; `Numeric`, `Masked`), a
  note's `Critical` (the critical colour) and `Busy` (a ring), a note without words taking no room,
  and a `SettingsAsk` that may be given empty and says its value. The engine writes notes, the
  reason under a field and whether a row is greyed in place (a danger row aside), so a level is not
  drawn again while it is typed in; one drawn again gives the keyboard back to the field that had it.

Needs a live pass: a real Microsoft and Google sign-in from Add account (the browser, its return,
the new account listed), Cancel while it waits; Test connection and Add against a real IMAP server
(an app password) and a CalDAV / CardDAV server; Sign in again for an account whose sign-in
expired; Sign out; a rename; Narrator on the password field ("password", nothing read out of it), the
IME off in it, copy and cut doing nothing, the password wiped as the form's level is left; typing in
every field without the keyboard leaving it; a
widget's Manage accounts, "Open Accounts settings" and Set up opening ⚙ › Accounts on that display
from outside edit mode; two displays with ⚙ open.

### The edit bar's Themia Pro, Back up & reset and About (SETTINGS-ABOUT)

The settings window's last pages, License and About, moved into ⚙ (user request 2026-10-25, step 4
of the move of [S-3520]), after Karpo's ⚙ › Karpo Pro, Back up and reset and About Karpo
(`karpo-app`, `docs/widgets.md` §339). Owning files: Core `Settings/SettingsMenu.Pro.cs`
(`ILicenceMenuHost`, the licence's lines), `Settings/SettingsMenu.Backup.cs` (`IBackupMenuHost`,
the settings file's lines, `ImportQuestion`), `Settings/SettingsMenu.About.cs` (`IAboutMenuHost`,
`MenuPicture`, About's lines and Uninstall Themia ›), `Settings/SettingsOutcomes.cs` (what stands
under a line); App `Services/Licensing/LicenceMenu.cs`, `Services/SettingsBackup.cs`,
`Services/Uninstaller.cs`, `Services/AppHost.SettingsMenu.cs`, `Shell/DesktopWindow.SettingsMenu.cs`
(`RequestSettings`, this display's window as the dialogs' owner), `Shell/Menu/MenuPictures.cs`,
`Shell/Gallery/EditBarRender.About.cs`. Tests `SettingsMenuTests` (`S357x_`, `S358x_`).

- **[S-3570]** **Themia Pro ›** on ⚙'s first level, in the band after Online updates: what this
  copy is and why, for every basis ([S-1064]: Developer build, Checking, Themia Free, Themia Pro,
  the offline grace with its date, the Store unreachable), as a reading with its glyph (the
  certificate on the free tier, Themia's mark otherwise) and a note with the why (`LicenseText`).
  A developer build has nothing more. Otherwise, on the free tier, the allowance ([S-3571]); then,
  under a divider, the Store's lines ([S-3572]).
- **[S-3571]** The free allowance as readings: **Widgets** "12 of 20" (every view, groups and their
  members included, said under it) and **Views** "2 of 2", in the language's numbers. Pro and a
  developer build have none.
- **[S-3572]** Where Pro is not had, **Upgrade** with the Store's price once it has said it
  ("Upgrade · CHF 19.00"; asked the first time ⚙ opens, again later while it has not answered), the
  pitch under it; greyed, with "not on sale yet" under it, while the add-on's id is the placeholder
  ([S-1067]). Then **Restore purchases**, what it does under it. Upgrade opens the Store's purchase
  dialog owned by the window of the display whose ⚙ it is (`IInitializeWithWindow` on that
  surface's handle: the settings window that owned it is gone); while the Store is asked both lines
  are greyed and "Waiting for the Microsoft Store…" (or "Checking your licence…") stands under the
  line with a ring; then how it ended (`LicenseText.Purchase`, in the critical colour where it
  failed; Restore: what this copy is now). The menu stays open at that level throughout.
- **[S-3573]** A refusal that opened the menu (the free-limit notice's **See plans**, [S-1065]):
  edit mode, ⚙ open at Themia Pro on the display the notice was clicked on, and on top, while the
  free limits apply, "Free limit reached" with the warning glyph and the refusal under it. It is
  said until the last settings menu closes, then never again.
- **[S-3574]** The Themia Pro line's value is the tier: "Free", "Pro" (the grace too) or "Developer
  build". The line's words are the add-on's name, "Themia Pro", in every language.
- **[S-3575]** **Back up & reset ›**, the About page's settings file rows: **Export settings…**
  ([S-494]), **Import settings…** ([S-495]..[S-498]), **Open config file** with the file's path
  under it ([S-3578]), then under a divider **Reset all settings…** ([S-892]). Each line's outcome
  stands under it.
- **[S-3576]** Export and Import open Windows' Save and Open dialogs (`Microsoft.Windows.Storage.Pickers`,
  a window id) owned by the window of the display whose ⚙ it is. While one is up the file's lines
  (Export, Import, Reset) are greyed. ⚙ stays open at Back up & reset, and how it went stands under
  the line: "Saved as <file>", "Settings imported. The ones you had are kept as <backup>", what was
  refused (in the critical colour); a cancelled dialog says nothing. What stands under the lines of
  Themia Pro and Back up & reset is one for the app (`SettingsOutcomes`, every display's ⚙ says the
  same) and goes when the last settings menu closes, but what is still out, which says so again
  when ⚙ opens.
- **[S-3577]** An import's file, once read ([S-495]), asks on a level of the menu's own before anything changes
  ([S-3737], Import and Cancel): "Import these settings?", then **What changes** (a line
  each, "Nothing: the settings are the same" when it holds what is there now) and **Good to know**
  (the repairs, the displays not here, Start with Windows staying, where the current settings are
  kept), each cut at twelve lines with "and N more" (`SettingsMenu.ImportQuestion` over
  `SettingsTransfer.Compare`, the one preview). Only Import takes it ([S-497]); ⚙ is opened again at
  Back up & reset first where it was closed while the dialog was up.
- **[S-3578]** **Open config file** opens `state.json` in what edits JSON here (its folder before it
  has been written), through the one launcher ([S-7062]). **Reset all settings…** asks first as a
  destructive question ([S-3727]: "Reset all settings?", the page's words of what goes and where it
  is kept, Reset in the critical colour, the keyboard on Cancel); only Reset resets ([S-892]).
- **[S-3580]** **About Themia ›**: the name card (**Themia** with its mark; under it "Version x.y.z"
  and what it runs on, ".NET x · Windows App SDK y"); **Lometics** with its mark, "lometics.com" at
  its end, opening the site, the studio's line under it ([S-493]); **Privacy policy** and
  **Support** ("support@themia.app" at its end, a mail) ([S-1072]); **Open-Meteo**
  ("open-meteo.com"), the source the weather, rain and air-quality widgets credit, with what comes
  from it (CAMS for the air quality); then, last, **Uninstall Themia ›** ([S-3582]). Every link
  opens through the one launcher. There is no Quit Themia here: it is ⚙'s last line ([S-3532]).
- **[S-3581]** A line may show a picture instead of a glyph in its icon column (`SettingsLine.Picture`,
  `MenuPicture`: Themia's mark `Assets/themia-logo.png`, decoded at the column's size; Lometics' mark
  `Assets/lometics.svg`, rasterised), named for a screen reader; a command may say a value at its end.
- **[S-3582]** **Uninstall Themia ›**: what happens (packaged: Themia closes and is removed with
  the user's widgets, settings and accounts, [S-1070]; loose: Themia closes and Windows' Installed
  apps opens, [S-1552]); a loose build's **Also delete my data**, unticked; ticked, "Your widgets,
  settings and accounts are deleted too" and the folders that go named under it ([S-1553]), unticked again when the last
  settings menu closes; then **Uninstall…** (in the critical colour), asked first as a destructive
  question ("Uninstall Themia?", what happens and, ticked, that the data goes too). Only its
  Uninstall does it (`Uninstaller.Run`, the one plan, [S-1071]); nothing is removed or deleted
  before that answer ([S-1555]).
- **[S-3583]** Debug renders (`--editbar-png`, [S-2328]): `page-<theme>-general-pro-free`, `-pro-refused`, `-pro-pro`, `-pro-grace`,
  `-pro-offline` (the License page's `--license` states, gone with the page), `-pro-developer`,
  `-pro-onsale` (the price on Upgrade), `-pro-buying` (the Store waited for); `-backup`,
  `-backup-done` (outcomes under the lines), `-backup-import` (an import's question over a made-up
  file, its level), `-backup-reset` (its "are you sure?" level); `-about`, `-uninstall-packaged`,
  `-uninstall-loose`, `-uninstall-loose-data`, `-uninstall-confirm` and
  `-uninstall-packaged-confirm` (their "are you sure?" levels), over a stand-in host: no Store, file dialog, file
  or install is reached.
- **[S-3584]** *(Retired by [S-3585]: the window that had no page left is gone with it.)*
- **[S-3585]** There is **no settings window** (user request 2026-10-25, the last step of the move
  of [S-3520], after Karpo's `docs/widgets.md` §340–§341: every way to the settings opens the edit
  bar's menus, and the settings app is deleted). Its code (`Shell/Config`: the window, its pages'
  table, targets, navigation, "Find a setting", history, keys and title bar), the `--settings-png`
  render, its navigation icons (`Assets/NavIcons`) and their notice, its styles in `App.xaml`,
  `SettingsPageSync`, `NavHistory`, `SettingsKeys`, `SettingsSearch`, `SettingRow` (a row's record
  for search) and `FluentLayout`'s rows that only pages built (`ClickRow`, `Expander`,
  `DangerButton`, `ConfirmRow`, a picture row), and the words only they used are gone. Every way into
  settings is the edit bar's menus ([S-3534]): ⚙, View ▾ and Display ▾, the surface menu's ways in
  ([S-1744]), a second start ([S-66]), a widget's accounts ([S-3558]), See plans ([S-3573]).
  `FluentLayout` keeps the rows the app-layout editor, Themia's one window, is built from ([S-1411]);
  a menu's debug render is `--editbar-png` ([S-3535]).

Needs a live pass (step 5): Themia started again from Start, from a shortcut and as `Themia.exe` while
it runs, with edit mode off and on, with ⚙ or another bar menu open, during a placement, with the
primary display not the first one Windows lists (⚙ on the main display, the keyboard in it after the
hand-off); a second start at sign-in (the Run entry, and the packaged build's startup task) opening
nothing; the surface menu's View, Display and Settings opening the bar's menus on that display with the
keyboard in them after the context menu has closed; Ctrl+Alt+W still only toggling edit mode.

Needs a live pass: the packaged build from the Store (a sideload has no licence): Upgrade's Store
dialog over the desktop on each display (in front, owned by the surface), its price, a purchase,
Restore purchases; See plans from a refusal on a second display; Export and Import through Windows'
dialogs over the surface (in front, the menu still open after them), the import question, Open config
file, Reset all settings; About's links (the browser, the mail app); Uninstall Themia in a loose
build (Installed apps, the data purge after exit when ticked) and in the packaged build (the package
removed, `%LOCALAPPDATA%\Packages\<family>` gone); Narrator on the pictures.

### The selected widget's properties strip (QUICKSET)

Since 2026-11-12 the selection's options are its page (EDITBAR-PAGES, [S-2340]..[S-2343]); the menu
beside the selection, its engine and its placement are gone. What a line of a widget is and does
([S-3719]..[S-3733], [S-3744]..[S-3749]) stands, drawn as the rows of [S-2302]; "the menu" below is the
selection's page and "a level" a card opened inside it or a page of its own.

How a widget's card looks — its header, its background, its title — is set where the widget is, in
edit mode, not in a window, and not on the edit bar: on a small panel that stands beside
the selection. After Karpo's properties panel (`karpo-app`, `docs/widgets.md` §145: a click in edit mode
only selects and a small panel of the selection's own properties stands by it) and its selected
widget's flyout (§65: closed while the selection is carried, opened again where it lands). Owning
files: `Shell/Pages/SelectionRows.cs` (the selection's page, [S-2340]), `Shell/DesktopWindow.Properties.cs`, `Services/AppHost.QuickSet.cs`;
rules in Core `Model/QuickSet.cs`, `Shell/SelectionPage.cs` and the SDK's
`Settings/ListDrop.cs`, `Settings/ListDrag.cs` and `Settings/MenuShare.cs` (tests `QuickSetTests`,
`ListDropTests`, `ListDragTests`, `MenuShareTests`); a widget's own lines in the SDK's
`Modules/WidgetMenuLines.cs` and `Modules/MenuSlider.cs` (tests `WidgetMenuLinesTests` in the module host).

- **[S-3700]** *(Retired by [S-3707]: the controls are no longer a group on the edit bar. A user
  looked for them at the widget, and the bar's group re-centred the bar every time the selection
  came and went.)*
- **[S-3701]** The strip shows on a display only while the selection ([S-1580]) has widgets in the
  view that display shows (`QuickSet.For`); ids no longer in it are not counted. It follows every
  selection change and every state change (an undo, a change from another display).
- **[S-3702]** One widget: the top row (its kind's name and the icon buttons, [S-3717]), Style
  ([S-6007]) with the options only that look draws right under it, the Header row ([S-3735]) and the
  widget's own rows ([S-3719]), each a section behind a rule; the card's own look is the pinned
  Appearance foot ([S-3739]). Several: the top row saying how many ("3 widgets"), then Header (its
  positions only), Style (one kind only) and the Appearance foot, and **no title** and no widget
  rows: a title names one widget, so one typed for many would make them indistinguishable.
- **[S-3703]** With several, a value the widgets share is ticked and a mixed one ticks nothing
  (`QuickSet.Common`); a pick sets it on every selected widget of the view (`SetHeader`,
  `SetBackground`) in one `Mutate`, so one undo step ([S-884]) however many it reached.
- **[S-3704]** The title is a row of the Header level ([S-3735]), its value the title as it reads;
  its level is the field alone ([S-3736]), the kind's name as its hint. It commits on Enter or when
  its level is left any other way (going back, a click on another widget, the menu put away), and
  Escape puts back what it held; Enter and Escape go back to the Header level. While the field has
  the keyboard the edit keys do not fire ([S-804]): Delete, arrows and Ctrl+Z are the field's. What
  is stored (`QuickSet.Title`): trimmed, and nothing (the kind's name, in the current language) for a
  blank title or the kind's name itself. A commit that changes nothing records no step; one cut off
  by a click on another widget still lands, on the widget it was typed for. A change made elsewhere
  never replaces text half typed in the field.
- **[S-3705]** *(Retired 2026-10-19: a widget has no settings page any more; its options are its properties menu's, [S-3719].) Delete stays on the icon row, the card menu, the Delete key and the undo
  notice ([S-1743]); a widget's own destructive line (clearing a history) is a confirmed line, [S-3727].)*
- **[S-3706]** *(Retired by [S-3713]: the edit bar no longer draws a selection group.)*
- **[S-3707]** *(Retired by [S-2340], [S-2343] (2026-11-12): the selection's options are its page, a flyout hung from its entry on the bar; the panel beside the selection is gone.)*
- **[S-3708]** *(Retired by [S-2325]: the page hangs from the bar; `StripPlacement` is gone.)*
- **[S-3709]** *(Retired by [S-2325]: nothing is placed by the selection's box.)*
- **[S-3710]** While the selection is dragged or a widget resized, the selection's page does not open
  on its own ([S-2343]); one open stays where it hangs.
- **[S-3711]** Some kinds can never have a header (`WidgetInstance.CanHaveHeader`): the thirteen
  quick-setting toggles ([S-3901]) and all six audio visualizer designs ([S-3200], [S-3212]). For a selection holding one, the strip has no Header line at all
  (no positions, and such a widget's title is shown nowhere, so it has no title box either) and
  shows only what every selected widget can take; `SetHeader`
  never reaches one; it is created headerless; load validation takes a header off it and says so;
  and the card draws it headerless whatever the file holds (`WidgetInstance.ShownHeader`).
- **[S-3716]** *(Retired by [S-2302], [S-2340]: the 320 × 440 menu engine (`MenuPanel`, `MenuNode`) is gone; the page draws Karpo's cards.)*
- **[S-3717]** *(Retired by [S-2340]: Cut, Copy and Remove are the header card's icon buttons.)*
- **[S-3718]** *(Retired by [S-2308]: a choice is an inline picker in its card.)*
- **[S-3719]** A widget's **own lines** come from its module (`IWidgetModule.MenuLines`, over a
  `WidgetMenuLines`): `StyleLines` on the first level right under the Style line, in its section
  (what only the current look draws with, [S-6070]; user request 2026-10-19: not inside the Style
  submenu, which holds the styles only), `Lines` after the Header row; the card's look is never among
  them: it is the pinned Appearance foot ([S-3739]). They are the one menu vocabulary, `ItemMenuEntry`:
  `Toggle` (a switch, [S-3741]), `Choice` (a level of ticks, any value type), `Submenu`, `Search`
  ([S-3722]), `Command` (a change and its save), `Ask` ([S-3721], its line `Asks`), `Colour` and
  `Fill` ([S-3724]), `Number` ([S-3726]), `Slider` ([S-3731]), `Confirm` ([S-3727]), `Note`
  ([S-3728]), `List` ([S-3729]), `PickFolder` ([S-3730]), `AccountsLine` (a link, [S-3749]) or a plain
  command, every one a row one row high. The lines are built again on every selection and state change over the live config (a
  change edits it in place and saves), so they always say what the widget holds. They are **every**
  option a widget has (user request 2026-10-19): there is no widget settings page ([S-6536]). A
  module without lines (the default) is a widget with no options of its own: its menu is the
  card's lines. [S-1862] The one line Set up opens is marked `WidgetMenuLines.SetsUp`.
- **[S-3721]** A row may **ask for one line of text** (`WidgetMenuLines.Ask`, `WidgetTextAsk`; its
  line marked `ItemMenuEntry.Asks`, so it has its chevron): its click opens the text level of
  [S-3736], the field holding `Initial` (selected), its hint `Placeholder`, the reason under it while
  the widget's check refuses what is typed (nothing while it is empty, and an empty field is refused
  unless `AllowEmpty`), and under a rule the row that gives it (`Accept`'s words), greyed while
  refused; Enter gives it too, and both go back. Escape or going back any other way drops it.
- **[S-3722]** A row may be a **search** (`WidgetMenuLines.Search`, `MenuSearch`): what is chosen at
  the end of its row; its level has the search's field pinned under its head, every answer as a row
  under it (one row each, scrolled where there are more than the rectangle holds) and the search's
  fixed rows after a rule (clear the choice, edit what was chosen; user request
  2026-10-19: a switch or a choice of what the widget shows stands on the first level after the
  search's line, never among them: the Clock's Offset). The empty text is asked
  as it opens; what is typed is asked once typing settles for `Settle` (at once for an answer in hand,
  the geocoder's pause, [S-2841], for a network one, "Searching…" while it is out), each answer
  replacing the one before and a newer question dropping an older one's answer; an answer may be a
  word in place of rows (no match, offline). Enter picks the first answer, Down moves to it, and the
  level stays open on a pick. The Clock's zone ([S-1478]) and the weather family's location
  (`WeatherPlaceRows.LocationLine`, [S-6174]) are searches.
- **[S-3725]** *(Retired by [S-3734]: the menu is one fixed rectangle, so a search's level never
  grows; every answer is a row, the rows past the rectangle's a scroll away, and there is no Show
  all (N) and no room kept under few answers.)*
- **[S-3723]** The card's own rows are the **Appearance** level, for every widget and for several
  selected, reached from the pinned foot ([S-3739]): Alignment; then, behind a rule, the colours
  (Background, Item background, Item text) and the background image; then, behind another, Scale, a
  slider ([S-3731], [S-6531]); each where it applies ([S-3718]). Header is not in it (user request
  2026-10-19): it is a row of the menu's own ([S-3735]). Its colours are the menu's one colour
  vocabulary ([S-3724]): Background and Item background fills, Item text a colour, built by the
  host as `ItemMenuEntry` colour lines and drawn by the same path as a widget's own; a mixed
  selection reads as a dash and holds no tick.
- **[S-3724]** A **colour** setting (`WidgetMenuLines.Colour`: `#RRGGBB`, or empty to follow the
  default) and a **fill** setting (`WidgetMenuLines.Fill`: a `FillChoice`) are rows whose level is the
  one colour level (`MenuColour` holds the value and what a pick does; Karpo's colour level):
  Default (the options' label; none when a colour is required, which then shows its first swatch)
  and a fill's Transparent, ticked while they hold; under a rule the swatches (the options' presets,
  `ColorPresets.Surfaces` unless given), 24 DIPs in 32-DIP cells, eight to a line, the first under
  the rows' icons, the chosen one ticked and ringed in the accent, each one's name as its tooltip; a
  fill's Opacity, a one-row slider from 10 % to 100 % in fives (`MenuColour.OpacitySlider`, [S-3731];
  greyed at 100 % while the fill holds no colour; a drag of the card's own shows on the widgets as it
  goes); under a rule Custom colour (the picker's dialog) and Pick a colour from the screen (the
  eyedropper, [S-1556]), both only where custom colours are allowed. A swatch keeps a fill's
  opacity. The row shows the colour's swatch in its icon column and its words at the end. A pick
  sets and saves; the value that holds changes nothing. There is no row of recent colours: Themia
  keeps none.
- **[S-3726]** A **number** (`WidgetMenuLines.Number`: a value, its range, its step and how it is
  written) shows its value at the end of its row. Up to twenty steps it is a choice of one, one tick
  per step; more is a row that asks for it as text ([S-3721], [S-3736]): the field's placeholder says the range
  in the number's own words, its unit included ("Enter a number from 15 min to 720 min", the line's
  format; the calendar's custom range in days), refused outside the range with the same words as the
  reason, a decimal comma read as a point, and rounded to the nearest step.
- **[S-3727]** A **confirmed** row (`WidgetMenuLines.Confirm`, its line `Asks`) asks first, on the
  "are you sure?" level of [S-3737]: the question, and under it what goes with it where the row says
  (`detail`: the habits and streaks of a habit list, a note's text for every widget showing it, a
  task list's tasks), then the action and Cancel; only the action runs it and saves (and goes back),
  Cancel, Escape or going back change nothing, and with nothing to ask nothing runs. A danger row's
  words and icon are the critical colour, and so is its question's action, the keyboard on Cancel
  so Enter straight after it keeps everything; any other question's keyboard is on its action.
  (Karpo asks in Windows' task dialog and closes its menu; Themia's menu stands on a no-activate
  desktop surface where a dialog cannot be owned reliably, so the question stays in the one
  rectangle, as everything else the menu asks does.) Tests `ConfirmLookTests` (the widgets' own
  questions, which still stand beside what they ask about).
- **[S-3728]** A **note** (`WidgetMenuLines.Note`) is words only, in the tertiary colour and the 12-DIP
  size, where a row's words start, wrapped in full within the menu's width (a data source's credit);
  never lit, never picked, skipped by the keys.
- **[S-3729]** An **editable list** (`WidgetMenuLines.List` over a `MenuList<T>`: feeds, symbols,
  hosts, shortcuts) is a level of its items, each a level of its own: Rename… (a text level, starting
  from the name; saved only when it changed), the list's own rows for it and Remove; the items' rows
  are dragged up and down to reorder ([S-3732]; not for a list whose order means nothing), so there
  is no Move up or Move down (user request 2026-10-19); while there are none, its
  empty sentence as a note; then Add… (a text level, checked as it is typed) or the caller's add
  rows, greyed at the list's limit; Remove is greyed at its fewest (`Min`: a table's last column).
  Every change is made in the list and saved.
- **[S-3730]** A **folder** (`WidgetMenuLines.PickFolder`) is a line with the folder's name at its
  end (a drive as itself) whose click opens Windows' folder picker (`FolderBrowse.PickAsync`, the one
  picker the settings' path boxes use too); a folder picked sets and saves, a cancel changes nothing.
- **[S-3731]** A **slider** (`WidgetMenuLines.Slider`, `MenuSlider`; user request 2026-10-19: the
  card's Scale and a fill's Opacity, and a widget's own number set by feel) is **one row** as every
  row (user request 2026-10-26: "the configuration items should only be 1 entry high"; Karpo has
  steppers there, but sliders were asked for): the icon column, its words, a compact 96-DIP track
  and its value right-aligned in the values' column; the track gives way to long words down to 64
  DIPs, and words longer still are trimmed, the whole their tooltip: the row never grows a second
  line (the two-row slider of 2026-10-25 is gone). A drag shows the value at the row's end as it goes
  (and, for the card's Scale and fills, on the selected widgets: `CardPreview`, painted only) and
  **stores once on the release**, one change and one undo step; a key (Left, Right, Home, End, Page
  Up/Down) steps it and stores the step; Up and Down move between the rows as on any row. A value is
  kept inside the range on the step's grain (`MenuSlider.Snap`); the value that holds stores
  nothing. A mixed selection's slider reads as a dash. Leaving its level mid-drag stores nothing.
- **[S-3732]** An **ordered list**'s item rows ([S-3729], `ItemMenuEntry.Order`, `MenuOrder`) are
  **dragged up and down** inside their level (user request 2026-10-19): a mouse press that travels 4
  DIPs ([S-913]) starts it (the row fades), an accent drop line shows
  the gap it lands in (`ListDrop.Gap`: before the first line whose middle is below the pointer), and
  the release moves the item there (`ListDrop.Target`; the gaps either side of it leave it where it
  is) and saves once. It is drawn in the panel's own window with the pointer captured, never an OLE
  drag. Ctrl+Up / Down on a focused item line moves it a place ([S-3748]), the focus staying
  on it. Each item line carries a grip before its chevron and says both ways in its tooltip and its
  automation help ("Drag to reorder, or press Ctrl+Up or Ctrl+Down"). A release on the
  line it started on is not a click.
- **[S-3747]** *(Retired by [S-2302]: an item is dragged within its card.)*
- **[S-3748]** *(Retired by [S-2302]: Ctrl+Up / Down on an item.)*
- **[S-3733]** *(Retired by [S-3718]: a level opens in place in the one rectangle, so no submenu
  stands beside another and nothing places one; `SubmenuPlacement` and `MenuRoom` are gone.)*
- **[S-3734]** *(Retired by [S-2325]: the flyout is as tall as its page up to 70 % of the work area.)*
- **[S-3735]** *(Retired by [S-2342]: What it shows, Header, Appearance.)*
- **[S-3736]** *(Retired by [S-2302], [S-2303]: a text is a box in its row; a widget's Ask is a page of its own.)*
- **[S-3737]** *(Retired by [S-2303]: an "are you sure?" is a page of its own.)*
- **[S-3738]** *(Retired by [S-2303]: nothing slides; a sub-page stands in the page's place.)* A level's rows (and its head) slid in 16 DIPs over 167 ms, decelerating
  (cubic-bezier (0, 0)–(0, 1)): entering from the end, going back from the start, right to left
  mirrored; only their offset moves, never their opacity, and not at all while Windows' animations
  are off ([S-1010]).
- **[S-3739]** *(Retired by [S-2342]: Appearance is a section of the page.)*
- **[S-3740]** *(Retired by [S-2301], [S-2304]: a heading starts a section or a card.)*
- **[S-3741]** *(Retired by [S-2302]: a toggle row with On / Off before the switch.)*
- **[S-3742]** *(Retired by [S-2308]: a long choice is a lookup.)*
- **[S-3743]** *(Retired by [S-2327]: the keys of the bar and its flyouts.)*
- **[S-3749]** A **link row** (Karpo's, §351): a setting another menu owns, never a copy of it
  (`ItemMenuEntry.LinkTo`, `SettingsLink`): the owner's icon and words, Windows' "open in new window"
  mark (E8A7) at its end and no chevron, its tooltip "Opens Accounts"; a pick closes the menu and
  opens the owner's menu at the level that sets it. A widget's Manage accounts and Add an account are
  links to ⚙ › Accounts ([S-3745]). A module links to a setting it follows with
  `WidgetMenuLines.Link(SettingLink)` (the SDK's closed set `OwnedSetting`: Accounts, Time format,
  Start of week, Temperature unit, Online updates with its cadence row): the setting's own icon and
  words as its level names them, and at its end, before the mark, what is in force ("24-hour",
  "Monday", "°C", "Every 30 minutes": `SettingLink.Value`, so the widget's menu says what applies
  without setting it). The host opens it through `WidgetMenuLines.OpenSetting` (`DesktopWindow.RequestSetting`):
  ⚙ › Accounts ([S-3558]), ⚙ › Language & region at the format's own level (`SettingsMenu.Keys.Format`),
  ⚙ › Online updates at the cadence's level; greyed where there is no edit bar (a render, a test).
- **[S-3745]** An account widget's lines open **⚙ › Accounts** on the widget's display ([S-3558];
  `WidgetMenuLines.AccountsLine`: Manage accounts, Add an account, link rows, [S-3749]) through the host's `OpenSetting`;
  the line changes and saves nothing, and is greyed where the host has no edit bar (a test). No account is ever signed
  in from a widget's menu.
- **[S-3746]** A line may show what is **read asynchronously** (an account's calendars, its task
  lists: `MenuLoads`): the lines are built from what is held, a read is started (one per key at a
  time, through the source's own cache), and while nothing has come the line says it is looking;
  when an answer comes that differs from what was held, every menu built meanwhile is out of date
  (`WidgetMenuLines.Outdated`) and the host builds it again. An answer that says the same changes
  nothing, so building again never loops; a read that throws is logged and said once.
- **[S-3738]** A **time of day or a date typed** on the menu ([S-3721]) is read by one rule
  (`TypedTime`): a time in either clock ("7:30", "07:30", "19.30", a bare hour, an optional am/pm with
  the hour then 1–12), a date with the year first ("2026-12-24", "2026/12/24") or the day first with
  dots ("24.12.2026", "24.12.26"). A slash with the day or the month first means two days in two
  countries and is refused, as is a day the month lacks; a refused text changes nothing.
- **[S-3712]** Header Hidden leaves no trace on the card ([S-401]): no strip, no icon, no title, no
  reserved row; the content area is the whole card less the inset over it ([S-5700]) at every size tier.
- **[S-3714]** *(Retired by [S-2325].)*
- **[S-3715]** *(Retired by [S-2325]: the flyout is Windows' ground and cards.)*
- **[S-6521]** *(Retired by [S-6566]: the Item background colour control and the Item opacity row
  are one fill control; there is no opacity row.)*
- **[S-6566]** **Background** and **Item background** each set the selected widgets' own fill
  ([S-6562]) with the one fill control ([S-6567]): **Default** (the app-wide fill, its swatch the
  app-wide colour), **Transparent**, or a colour with its opacity (`QuickSet.SetBackground`,
  `SetItemBackground`; every widget gets a copy). The offer reads the fill the widgets share
  (`QuickSet.CommonFill`: same kind, and for a colour the same colour and opacity), or none when they
  differ — the control then reads a dash until a pick. Each pick is one `Mutate`, one undo step; an
  opacity dragged is stored once, when the slider is let go.
- **[S-6571]** Where no selected widget has items ([S-6570]), the **Item background** and **Item
  text** lines are not on the menu at all (user request 2026-10-19: hidden, not greyed). Decided for a
  multi-selection: the lines are there when **any** selected widget has items, read the fill (and
  text colour) of those widgets only, and a pick reaches only them (`SetItemBackground` and
  `SetItemTextColor` skip a kind without items).
- **[S-6572]** A widget without items keeps whatever item background and text colour it holds (a
  pick never writes one), and they draw nothing: there is nothing for them to reach.
- **[S-6573]** Per style and state too (user request 2026-10-19, [S-6070]): a widget of a kind with
  items that paints none in its current look says so on its config (`IDrawsItems.DrawsItems`, read by
  `ItemKinds.Draws`), and counts as without items for [S-6571]: the lines are not there, a pick does
  not reach it. The clock in every style but Flip (its cards are the only items); the meters' graph
  style, the network meter in both its styles and the battery meter in its graph and battery
  styles (the shared meter view paints items only in its bars, rows, tiles, gauge, ring, core graphs
  and signal bars); Pomodoro in Bar (a progress bar of its own) and Digits; a power button's Round style (its circle is an item only for
  the moment a press failed, [S-1915], which is no reason to offer it). Size is not a state: a part a
  small tile sheds still counts. A config without the contract draws what its kind's trait says;
  only a kind with items may carry it (conformance).
- **[S-6574]** Item text never reaches a track ([S-6546]), so where every item a widget draws is a
  track (`IDrawsItems.DrawsItemText` false, `ItemKinds.DrawsText`) the menu has **Item background**
  but no **Item text** (`QuickSetOffer.HasItemText`), and `SetItemTextColor` skips it: Focus session
  (its ring), Pomodoro's ring and every meter (bars, tiles' washes, dials, rings
  and signal bars are tracks). A multi-selection offers the line when any selected widget's items
  carry text, and reads and sets those widgets only.
- **[S-6544]** **Item text** follows (after Item background, before Scale): the colour control with
  **Default** (the app-wide item text colour, its swatch that colour) and the colours
  (`QuickSet.SetItemTextColor`); Default empties the widgets' own text colour, a colour sets it. It
  never touches their item background. The offer reads the shared own colour, empty, or mixed (null,
  shown as Default; `QuickSet.CommonItemText`). One pick, one undo step.
- **[S-6530]** Each widget has a **scale of its own** (`WidgetInstance.Scale`, a factor, 1 = 100 %),
  50–200 % on a 5 % grain (`WidgetScale.Normalise`: clamped and rounded; not a number is 100 %).
  A new widget is at 100 %; a copy keeps its scale. It magnifies the whole card's content —
  header, text, icons, padding, the [S-850] way — never the card's cell, which stays where it is
  and the same size.
- **[S-6531]** The properties menu's **Scale** line, last in Appearance after the colours: an inline
  slider ([S-3731], user request 2026-10-19) from 50 to 200 % on the stored 5 % grain
  (`WidgetScale.Min`, `Max`, `Step`), its percentage at the end (a dash when the selected widgets
  differ, `QuickSet.Common` over the stored values). A drag shows the scale on the selected cards as
  it goes and the release sets every selected widget (`QuickSet.SetScale`) in one `Mutate`, one
  undo step; a key steps it by 5 %.
- **[S-6532]** The scale a card's content is drawn at (`WidgetScale.Effective`) is the display's
  scale ([S-850]) × the widget's own, a display factor that is not a positive number read as 1
  ([S-3590]: the app-wide widget scale, `Design.UiScale`, was the display's scale for every display
  at once under the same name, and is gone). It is the one input to the card's content transform:
  the content is laid out at cell ÷ effective scale, so the size ladder ([S-660]) classifies and
  discloses on the scaled box — a widget at 150 % shows what a smaller card would ([S-663]).
- **[S-6533]** Load validation repairs a stored scale outside 50–200 % or off the 5 % grain to the
  nearest one it may hold, and says so; a file without one reads as 100 %.
- **[S-6534]** *(Retired by [S-6536]: the settings page has no Scale row; the panel is the scale's one place.)*
- **[S-6535]** Debug: `--widget-png <Kind> <out.png> --scale <f>` draws the cards at that widget
  scale, through the same content transform.
- **[S-6536]** The properties panel is the **one place** for a widget's options (user request
  2026-10-16, and for the widget's own options 2026-10-19): its title, style, header, background,
  item background, item text, scale and alignment, and every option of its own ([S-3719]). There is
  **no widget settings page** and no settings window ([S-3585]), and nothing opens a page for a widget: no
  card menu Settings ([S-1743], [S-1758]), no More settings ([S-6009]), no Enter ([S-1974]), no click
  following the window ([S-1745]); Set up goes to the menu ([S-1862]). The app-wide Widget scale
  (Appearance, Displays) is not the widget's own and stays. Settings export and import carry all of
  them as before: they are the instance's.
- **[S-6580]** Each widget has an **alignment** of its own (`WidgetInstance.Align`, `ContentAlign`
  Left / Center / Right, a closed set saved by name): where its content block sits across its card.
  Left and Right are the reading direction's start and end, as Windows aligns text. None stored (the
  default) means the kind's own place ([S-6582]), so a widget never given one does not move; a copy
  keeps it. It moves the block the size ladder ([S-660]) built, never what the ladder shows.
- **[S-6581]** Only a widget whose content is **one block** aligns (`ContentAlignment.Applies`: its
  config implements `IContentAligned`, per style; `ContentAlignmentTests` checks it against the views that implement `IWidgetAligned`):
  every style of the Clock, Weather now, Air quality, Countdown and Quote; the Number style of
  Exchange rates and Stocks; the Moon's Disc. Not lists, grids, calendars, mail, folders, charts,
  meters (a reading over its graph), mixers, timers with their controls, button and toggle tiles:
  they fill the card or are the card. A style that fills the card (a rate's or a stock's chart, the
  moon's month) does not align either.
- **[S-6582]** Without a choice each widget sits where it always drew: centred — the Clock,
  Weather now's Large icon and Minimal styles, Air quality's Gauge; from the start —
  everything else (Weather now Standard, Air quality Number, the rate, the price, the Countdown, the
  Quote, the Moon's readout, since G3 as every data widget's, [S-6702]). The Countdown no longer centres itself on a Compact tile: one rule at every size.
- **[S-6583]** The card hands its view the alignment it draws (`ContentAlignment.Shown`: the own
  choice, else the kind's default for its style, and the default for a value outside the set or a
  style that does not align) before the view attaches and before every config it hears. In the
  view: the clock's column; the weather's and air quality's Readout ([S-6702]: its value row and
  the line under it together), the fact rows beside or under it keep their places; the large-icon and minimal reading and the gauge; the
  rate and the price; the countdown's lines, ring and cards; the quote's mark, lines and author; the
  moon's readout (the moon, its lit share and the phase's name under them) over or beside its rows.
- **[S-6584]** The properties panel's **Alignment** row, first in Appearance: a level of Left, Centre
  and Right (Segoe AlignLeft, AlignCenter, AlignRight), the current one ticked, none for a
  mixed selection. A pick sets every selected widget that aligns in its style (`QuickSet.SetAlign`;
  the others keep theirs) in one `Mutate`, one undo step. Where none does (a kind with nothing to
  align, or one that aligns only in another style), the line is not there ([S-3718]).
- **[S-6585]** Load validation drops an alignment outside the set, or one on a kind that never
  aligns, and says so; one on a style that does not align is kept for the style that does.
- **[S-6586]** Debug: `--widget-png <Kind> <out.png> --align left|center|right` draws the cards with
  that alignment.
- **[S-3713]** *(Retired by [S-2328]: the selection's pictures are `select-<theme>-*.png`.)*
## Widget styles (STYLES)

A different look of the same widget with the same data and the same settings, chosen next to the
widget on the properties panel ([S-6007], its one place, [S-6536]). Owning files:
`Themia.Core/Widgets/WidgetStyles.cs` (the registry), each styled kind's config (its style enum),
`Themia.App/Shell/Pages/SelectionRows.cs`. Tests:
`WidgetStylesTests`; the weather, forecast, stock, exchange-rate and air styles in `WeatherStylesTests`.

- **[S-6000]** A kind's styles are one setting: where a kind already had a look option (the clock's
  face, a gallery's layout, a forecast's layout), that option **is** the style, never a second
  setting beside it. A style never adds or removes data, only how it is drawn.
- **[S-6001]** A styled kind's config implements `IStyled` over its own int-backed enum
  (`<Kind>Style`, property `Style`, the default the first value): `Styles` lists one
  `StyleOption(Value, Name, Label, Glyph)` per style in menu order, the name camelCase (its wire
  name), the label a `Str`, the glyph a Segoe Fluent Icons code point. `WidgetStyles` discovers every
  styled kind from its default config (`WidgetConfig.CreateDefault`), never from a list kept beside
  them, so giving a kind styles touches only its config, its view and its strings.
- **[S-6002]** A kind offers a style choice when it has two styles or more (`WidgetStyles.Offers`).
- **[S-6003]** `WidgetStyles.Set` writes only one of the kind's own styles, and reports whether
  anything changed.
- **[S-6004]** Stored as `"style": "<name>"` (`StyleJsonConverter`, which never throws: a throw in
  one config refuses the whole state file). An unknown name, a number or any other token reads as
  unknown, and load validation (`WidgetStyles.Repair`, for every styled kind at once) sets the
  kind's first style and logs the repair.
- **[S-6005]** A view receives a style change like any config change (`ApplyConfig`): it re-lays
  itself for the style on the same responsive ladder ([S-660]..[S-666], concise first), and lets go
  of whatever the previous style built or subscribed.
- **[S-6006]** Debug: `--widget-png <Kind> <out.png> --style <name>` draws any styled kind in one of
  its styles; a wrong name prints the kind's styles.
- **[S-6007]** The properties panel ([S-3707]) shows **Style** first, a drop-down with the
  current style's icon and name, whose menu lists the kind's styles with icon and name, the current one
  ticked, built as it opens, when every selected widget is of **one** kind and that kind offers
  styles (`QuickSetOffer.StyleKind`). Several of one kind with different styles tick nothing
  (`QuickSetOffer.Style` null). Mixed kinds: no Style.
- **[S-6008]** A pick sets the style of every selected widget (`QuickSet.SetStyle`) in one `Mutate`:
  one undo step however many it reached.
- **[S-6075]** A style that cannot change anything now is not offered (user request 2026-10-19,
  [S-6070]): the config says so (`IStyled.StyleApplies`, read by `WidgetStyles.Applies`), and the menu
  has no Style line while no selected widget's look would differ (a gallery of one picture, a file or
  the photo of the day, drawn full-bleed in every look). The style is kept for when it applies again.
  The options only the look draws with that still apply (a single picture's Picture size) keep their
  place at the top of the menu, in a section of their own, without the Style line over them. With
  several of the kind, the line is there when any one's look would differ, and a pick sets every one.
- **[S-6009]** *(Retired by [S-6536], 2026-10-19: there is no **More settings** line; with every
  option on the menu there is no widget page left for it to open.)*
- **[S-6060]** *(Retired, group G5, 2026-10-29: every ring is the Chart block's `ChartRing`, stroke 2
  over its Track ring ([S-6717]); the timers set their readout in its hole ([S-6739]). `RingPart`, its
  geometry `RingArc` and the stroke of a twentieth of the diameter are gone.)*
- **[S-6061]** One reflow rule for every dot grid (`DotGrid.Fit`: the moon's month): every column count is tried and the one whose square cells are largest kept, on a tie
  the one with fewer empty cells in the last row; the gap is a quarter of the pitch, so a larger
  tile spaces the dots in proportion.
- **[S-6010]** *(Retired by [S-6536]: the style is picked on the properties panel only.)*
- **[S-6070]** A widget's properties menu offers only the lines that change what it visibly does or
  shows in its current style and state (user rule). The rule is one Core property per config,
  `IShownSettings<TSetting>.ShownSettings` (the kind's own `<Kind>Setting` enum, menu order), read by
  the menu lines through `Offers(setting)`, never a check written in a menu; an option every style
  and state uses is not listed and always offered. A line left out is not built at all (no greyed
  lines). The values of options left out are kept for when they apply again. Size is not a state
  here: an option a small tile sheds ([S-660]) is still offered. A rule never reads a typed value,
  so typing a number never reshapes the menu.
- **[S-6071]** *(Retired 2026-10-19 with the widget pages: no page shape to rebuild; the menu is
  built again on every change and redraws only lines whose shape changed, [S-3716].)*
- **[S-6072]** *(Retired by [S-6536]: with no Style row on the page there is nothing to leave out;
  `IShownSettings.OffersStyle` is gone.)*
- **[S-6073]** *(Retired 2026-10-19 with the widget pages: the settings search indexes the app pages
  only, [S-1542].)*
- **[S-6074]** The options each kind offers only in some styles or states: Mail: Subject/Account in List
  and Compact, Preview lines in List, the columns in Table. Folder: [S-4121]. Calendar: [S-3641].
  Clock: Seconds in Digital, Analog, Stacked and Flip; Date and Offset in all but Minimal and Date
  (its own Text size is gone, [S-1476]). Quote: Shuffle and
  Refresh with two quotes or more, Reshuffle with Shuffle on too. Gallery: a folder's rows (folder, subfolders, pictures, shuffle, Explorer) for the
  folder source, the slide time for its Slideshow, Picture size for Slideshow and Grid (Scatter and
  Mosaic always crop), for a picture file without an outline and for the photo of the day; a
  picture file's rows (image, rotation, outline) for that source, the outline's colour and width
  with the outline on. Shortcuts: tile size, icons only and stretch in Grid, row size in List and
  Details, file extensions except on a grid of icons alone. Meters (CPU, memory, GPU, disk, network,
  battery, ping): Chart span (and its History heading) in the graph style only, each meter's first
  ([S-6300]). Current weather: Show detail in Standard. Stocks: Closed indicator for a listed symbol
  (not crypto), Extended hours for a listed symbol over one or five days (`WantsPrePost`), high and
  low markers in Chart. Sun: Events (and its Sky heading) not in Times, which is the events. Moon:
  Longitude in Disc only. Week planner: "Tasks from" while today's tasks show and there are two
  accounts with tasks or more. Tasks: Show due date, Due and Important only for an account's tasks,
  Important only for one whose tasks can be important (Microsoft To Do, not Google). Calendar and
  week planner: Show contact birthdays while a chosen account has contacts (not CalDAV alone). Next
  meeting: no calendar colours (it draws none), so Calendars only for an account with two or more. Habits: Interval under the custom period, streaks
  and history in Dots. Pomodoro and Alarm: Volume while a sound is chosen. Contacts: the columns in
  Details. Screen time: the daily limit on the chart and the donut, the per-app limits on the app
  list, its Daily average over a week or a month. Debug: `--editbar-png` opens a kind's menu (and a
  style's lines under Style) to look at them.

Needs a live pass: picking a style on the strip for one widget and for several of one kind, the
undo of it, the gear opening the right page, and the flip turning over on the desktop with Windows'
animation effects on and off.

## About and the settings file

The settings file's export, import and their rules, run from ⚙ › Back up & reset ([S-3575]); About
itself is ⚙ › About Themia ([S-3580]). Owning files: `Themia.Core/Storage/SettingsTransfer.cs`,
`Services/SettingsBackup.cs`.

- **[S-493]** *(Retired by [S-3580]: About is ⚙ › About Themia, its name card, the studio and its
  links as lines; Quit Themia is ⚙'s last line, [S-3532].)*
- **[S-494]** Export writes the state document — the same JSON as `state.json` — to a file picked
  with `Microsoft.Windows.Storage.Pickers` (window id, works unpackaged; the window of the display
  whose ⚙ it is, [S-3576]), atomically (temp + move).
- **[S-495]** Import reads the picked file through `AppStateStore.Parse`, the startup trust boundary,
  so an import can never accept what a restart would refuse. Empty, non-JSON, newer-schema and
  view-less documents (any other app's JSON would otherwise read as "one blank view") are refused
  with a reason and change nothing.
- **[S-496]** Before anything changes, the question beside ⚙ says what will ([S-3577]): views matched by id, then by name;
  renamed, added and removed views; per view the widgets added, removed and changed; every design
  value before → after. Repairs, displays named in the file that are not connected, and Start with
  Windows staying put are notes, not changes.
- **[S-497]** On confirm, what the user has now is written beside the state file as
  `state.before-import-<yyyyMMdd-HHmmss>.json` — the in-memory state, since a debounced save may not
  have landed — and if that backup cannot be written nothing is imported.
- **[S-498]** The import replaces the state **in place** in one mutation, then gives connected
  displays without an assignment a view as at startup. Start with Windows keeps this PC's value: it
  is a fact about the registry here, not a preference. So does the assistants' server switch
  ([S-9960]): a file never opens or closes a listener on this PC.
- **[S-499]** *(Retired by [S-3585]: there is no settings window and no `--settings-png`; ⚙'s
  sections are drawn by `--editbar-png`, [S-3535], [S-3583], over the same detached store that writes
  nothing.)*

Not ported, on purpose: Karpo's "isn't running / Start" card — the settings here are the running
app's own menus, so they are never open while the app is not.

## Web widgets: video stream

Ported from `widgets/online/video-stream` on `main`. The widget is a module of its own
(`src/Widgets/Themia.Widget.VideoStream`, its SPEC.md). Here: what it shares and the host's camera
connections it reads through `IWidgetHost.Streams`: `Services/Web/StreamSession.cs`
(`HostLauncherServices.cs`), `Themia.Core/Widgets/MjpegFrameReader.cs`, the SDK's
`VideoStreamRules`, `HlsVariants`, `RotatedBox` and `IWidgetContextActions`. The original's web element
widget is not part of this app ([S-1724]).

- **[S-460]** Video stream title: the user's label, else the stream's `host:port` (`Uri.Authority`,
  so a non-default port is kept), else the widget name (was [S-01]).
- **[S-464]** A failed, dropped, stalled or ended stream shows "Stream unavailable. Reconnecting…" and
  retries after 2 s, 4 s, 8 s … capped at 30 s (`VideoStreamRules.RetryDelay`; was [S-05]/[S-06]). The
  counter resets on the first decoded frame and on any restart. The delay is armed by the failure and
  cancelled with the session. A stream that sends no bytes for 30 s counts as dropped — a socket
  deadline, which closes the original's [U-02] without a frame-rate watchdog.
- [S-465], [S-466], [S-467], [S-469]: moved with the Video stream widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.VideoStream/`).
- **[S-468]** Rotation 0/90/180/270, from settings or cycled by Rotate (was [S-08]). `RotatedBox` lays
  a sideways picture out against the swapped axes before turning it, so it fits instead of clipping.
  A value the enum does not name reads as none.
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
`src/Themia.App/Services/Accounts/`; what the user sees of them is ⚙ › Accounts (SETTINGS-ACCOUNTS).

- **[S-500]** A closed set of providers, the provider modules compiled in ([S-6910]): Microsoft,
  Google, IMAP, CalDAV, CardDAV (`HostProviders`, declared in Core). A provider this build does not
  know is dropped on load and reported, never carried as a string ([S-1901]). A held-back provider
  (Google today, [S-3562]) is still one of them: its accounts load as any other's.
- **[S-501]** Providers are written under lower-case names (as the original wrote them), so the
  file stays readable and a hand edit is easy to validate. The name is the provider's module id
  (`microsoft`, `google`, `imap`, `caldav`, `carddav`); it is read forgiving case and surrounding
  space, as it always was.
- **[S-502]** What an account feeds (a `FeatureSet` of `FeatureId`s: `mail`, `calendar`, `contacts`,
  `tasks`, `HostFeatures`) and how it proves itself (`AccountAuth`) follow from the provider's module
  alone. Widgets ask for a feature, never a provider name.
- **[S-622]** A CalDAV account (any server: Nextcloud, iCloud, Radicale, a DAVx⁵ / jtx Board setup)
  feeds `calendar` and `tasks` (`CalDavProvider`, issue 185): its calendar home set's VEVENT
  collections are calendars ([S-567]) and its VTODO collections task lists ([S-618]), read-only
  ([S-621]). Nothing new is asked at sign-in; the one stored collection and password serve both.
- **[S-503]** `accounts.json` lives beside `state.json` and carries a `schemaVersion`; a newer schema is
  set aside rather than half-read.
- **[S-504]** Every stored entry becomes a whole `Account` or nothing. A field that says *where* the
  account is (provider, IMAP host, DAV address) cannot be guessed and its entry is dropped; a field it
  can live without (nickname, IMAP port) is repaired; a missing secret is kept as "sign in again".
  Every drop and repair is logged. Duplicates keep the first.
- **[S-505]** Ids are built as the classic app built them, DAV aside (`google:<sub>`, `imap:user@host`,
  `caldav:user@authority/path` of the collection, the Graph id for Microsoft), so signing in again
  replaces the account (keeping its nickname), while a second collection of the same user on the
  same server (Google's per-calendar CalDAV addresses) is a second account (classic #133).
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
  `THEMIA_CLIENT_SECRETS` under Google's key (`google=…`, [S-9917]; a generated constant, the whole
  value checked by the build); without it, Google sign-in refuses before the browser opens and token
  refresh fails with the same sentence. Any provider whose browser member needs a secret behaves the
  same way.
- **[S-539]** *(Retired by [S-3550]..[S-3555]: the Accounts page is ⚙ › Accounts.)* Settings → Accounts listed each account as a group ([S-3821]: provider icon, name, address
  and provider, or that it needs signing in again), renames in a flyout, signs out after a confirmation, and adds through one flow at a time drawn in place:
  Microsoft and Google waiting on the browser ([S-1783]); forms for IMAP, CalDAV and CardDAV. Errors
  appear on the page; leaving it cancels a flow.
- **[S-3820]** What an account has on this PC is counted from the shelf a sync service just loaded or
  saved (`SyncStats.Of`: mail messages over folders, calendar events over windows (birthdays aside),
  contacts, tasks over lists; the newest successful read; the queued changes), never by opening a
  file for it.
- **[S-3821]** *(Retired by [S-3551]: an account is a line whose submenu holds the same.)* Each account was a group on the Accounts page: header name, then address and provider
  (or "Needs you to sign in again"); inside, a row per kind its provider feeds, in the order mail,
  calendar, contacts, tasks (`SyncStats.Summarize`), with the count on the right and "in 3 folders ·
  synced 5 min. ago · 2 changes waiting to sync" under it, or "Nothing stored on this PC yet"; then
  Nickname (Rename) and Sign out (danger). Open groups stay open across the list's rebuilds.
- **[S-3822]** *(Retired by [S-3552].)* An account that needs signing in started its group with Sign in again (the problem as
  the description): Microsoft and Google run the browser flow, IMAP and DAV open their form.
- **[S-3823]** `SyncHost.Ledger` holds every shelf's latest numbers this run (`SyncLedger`, per owner
  and kind, thread-safe); `SyncHost.Report` raises `StatsChanged` on the UI thread when they change,
  and ⚙ › Accounts follows them while it is open ([S-3559]). A sign-out purges the shelves and forgets the numbers.
  A kind whose service has not loaded its shelf this run (no widget reads it) shows nothing yet.
- **[S-3824]** *(Retired by [S-3560]: the page and its render are gone.)* Debug render: `--settings-png Accounts --demo-accounts` drew two stand-in accounts
  (one healthy with numbers, one needing a sign-in) from the render host's own ledger, groups open.
- **[S-1901]** No GitHub or Claude account and no OneDrive access: they went with the widgets that
  used them ([S-1902]), with the GitHub device-code and token sign-ins, Claude's paste-back sign-in
  and Microsoft's `Files.Read` scope. A stored `github` or `claude` entry in `accounts.json` is an
  unknown provider: dropped on load and reported ([S-500]).
- **[S-1780]** Microsoft signs in with the authorization-code grant + PKCE (S256) in the system
  browser: `…/common/oauth2/v2.0/authorize` with `response_type=code`, `response_mode=query`,
  `prompt=select_account`, a fresh state and challenge, and redirect `http://localhost:<port>/`; the
  code is exchanged at `…/token` with the verifier, the same redirect URI and the scopes, and no client
  secret (a public client). Microsoft and Google run this as one path, each provider's browser
  member ([S-9910]); they differ only in the member's data: addresses, Google's secret ([S-536]),
  the extras on the sign-in page and the "who" call ([S-9916]). The app registration must list `http://localhost`
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
- **[S-1783]** *(Retired by [S-3553]: the card is the provider's line in Add account and what stands under it.)* Settings → Accounts: Microsoft and Google shared one browser card ("Your browser opens
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
  history as a `SparkBand` edge to edge at the bottom of the tile ([S-1824]). The adapter is the header's label beside "GPU" ([S-2260]), without vendor and
  trademark noise (`GpuRules.ShortName`: "RTX 4060 Ti"); its whole name is the tooltip. Load reads `--` for the first sample: it is
  a rate and needs two readings.
- **[S-431]** Data comes from DXGI (which adapters exist: name, LUID) and the PDH GPU counters
  (`\GPU Engine(*)\Utilization Percentage`), all through `LibraryImport` and vtable calls, so
  AOT-clean. The original's registry enumeration is replaced: its numbered class subkeys include
  adapters that are gone and stop at the first gap, while DXGI hands over the LUID the counters are
  filed under. One sampler is shared by every GPU widget: opened by the first, closed by the last,
  sampled on the pool, single-flight, read by the hub once per due tick ([S-2501], [S-2507]).
- [S-432], [S-433], [S-436], [S-6320], [S-6321]: moved with the GPU widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gpu/`).
- **[S-434]** A counter instance belongs to the adapter whose `luid_...` token it carries, found
  anywhere in the name (engine instances start with `pid_...`, which the original's "starts with"
  match missed). Load is the **busiest engine**: per-engine sums across processes, then the
  maximum, which is Task Manager's figure. The original summed the 3D engine only and read near zero
  during video decode or compute.
- **[S-435]** On NVIDIA cards the driver's management library (`nvml.dll`, loaded with
  `NativeLibrary`, called through function pointers) is asked for the load too, and it wins over the
  counters (a borderless full-screen game collapses the 3D counter). Matched to the DXGI adapter by
  name (the n-th of identical cards), never guessed among several. Held only while a GPU widget is
  shown ([S-437]); any failure leaves the counters' figure.
- **[S-437]** The NVIDIA library is opened only while a GPU widget is **on screen**: a watch holds the
  hub's GPU feed (a widget on a view a display shows, or a render; `HubSource.Held`), not merely the
  state's want ([S-2513] still samples a GPU widget in a hidden view, with the counters' figure). The
  sampler's next sample opens it (`GpuDriverDemand`, on the sample's single flight, so it is never
  opened or freed under a call): a widget coming on screen reads the driver's figure from that sample,
  a second at most after it appears, the counters' until then. The sample after the last one leaves
  the screen shuts it down and unloads it; so does the sampler closing. A library that would not
  open is tried again only on a later showing, not every second. It is loaded from the driver's own
  folder, which the graphics kernel names for the adapter (`D3DKMTQueryAdapterInfo`,
  `D3DDDI_QUERYREGISTRY_DRIVERSTOREPATH`, `GpuDriverLibrary`): System32's `nvml.dll` is, with a DCH
  driver, a loader that loads that copy and never lets go of it or itself, so freeing it after
  `nvmlShutdown` left about 16 of the 18 MB initialising costs. System32's loader and the old NVSMI
  folder remain the fallbacks (the loader then stays loaded, logged). Switch check over a state whose
  GPU widgets are only in hidden views ([S-1883], Debug AOT): 125 MB private at start, now 106 MB,
  and no `nvml.dll` loaded; with the GPU details view shown and emptied again, 147 MB, now 127 MB,
  and the module gone.

- **[S-6322]** **Engines**: a row per engine type ([S-6304]) in the order 3D, Copy, Video encode,
  Video decode, for the types the adapter's counters list (an engine at 0 % is still listed; other
  types, compute or a vendor's own, are not drawn). A type's load is its busiest engine, summed
  across processes as the adapter's load is ([S-434]); always the counters' figure (`nvml` gives one
  number only). Empty until the counters have a rate: the load alone.

Not ported: the widget's own `label` (the card's label is the instance's); video memory, the
driver's sensors and the processes ([S-1762]).

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
  fallback (bell for system sounds, a window for an app).
- **[S-442]** `GroupByApp` (default on) makes every session of one executable one row: the loudest
  level, muted only when all are, active when any is (no session count is drawn, [S-2771]). Off: one
  row per session, keyed by its instance identifier.
- **[S-443]** Rows are ordered by name, case-insensitively, system sounds last; never by activity, so
  a row never slides out from under a dragged slider.
- **[S-444]** *(Retired: the mixer has no master row and no `ShowMaster` setting, [S-3251]. The
  `__master__` key stays as the volume tile's write key, which no session key can equal.)*
- **[S-445]** `ShowSystemSounds` (default off) adds the system-sounds row; `OnlyActive` (default off)
  hides apps that hold a session but are silent, never system sounds.
- **[S-446]** Writes go through a newest-value-per-key queue drained by the worker when it is free:
  single-flight, no throttle interval. A dragged level is held on screen until a snapshot reports it
  or its row is gone. A write whose key matches nothing is a no-op; a session failing
  mid-enumeration is skipped; expired sessions are dropped.
- **[S-447]** A muted row's slider sits at 0 but the row keeps its level; dragging a muted row above zero unmutes it.
  The mute button toggles a row (all of a group's sessions); right-click offers Mute/Unmute and
  Reset to 100%. Rows are reused by position so a snapshot never rebuilds the slider under the
  pointer.
- [S-448], [S-449]: moved with the Mixer widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Mixer/`).

Not ported: the poll `interval` setting (there is no poll).

### Screen time

Since [S-3320] screen time is three widgets (chart, donut, apps); the rules below that still hold
are cited from there, the ones it replaced are marked.

- **[S-450]** The widget draws the app's one tracker ([S-458]) through `IWidgetHost.ScreenTime`; it
  has no timer of its own and redraws when the tracker says something changed.
- **[S-451]** *(Retired by [S-3321]/[S-3330]: the period is per widget and only the chart has a
  style.)* `Period` (Day, Week = last 7 days, Month = last 30 days; rolling windows ending today)
  and `Style` are separate. A day draws Doughnut, Bars or None; a window Bars, Heatmap or None. An
  impossible pair (or an unknown value) becomes the period's first style. Changing the period in
  settings writes both fields.
- **[S-452]** *(The legend, the day bar and the shared readout are retired by [S-3341]/[S-3351]; durations,
  colours and "Other" stand.)* The ring's wedges are the busiest apps up to the cap plus one "Other" wedge, offsets
  from cumulative seconds; the total sits in the hole. A day of Bars is one bar split the same way.
  Durations read `3h 24m`, `12m`, `<1m`. How many apps the legend and the drawing name is
  [S-1211]/[S-1212] (`MaxApps` is a ceiling over what fits, `ShowList` hides the legend). Each app's tone is its rank's step of the accent ([S-2272]; the hash of its key
  that picked a hue is gone); a legend row carries the icon (or a dot in that tone), the name,
  and the time as its last column. Hovering a wedge, segment or row lights it, dims the rest and
  names it in the readout.
- **[S-1989]** *(Superseded by [S-3307]/[S-3334]/[S-3344]/[S-3354]: the same keys in each of the three,
  Enter/Space select, and the cursor is the linked hover.)* Screen time keys: Up/Down, Page Up/Down, Home/End light one legend app the way
  hovering does (the rest dim, the headline names it); the Menu key opens its Hide menu (not on
  Other); Left / Right step the day as the header arrows do, Day period only and never past today;
  Escape lets the app go. Hiding is never a bare key: it has no undo.
- **[S-453]** *(`ShowSwitches` retired with the stats, [S-666]; average and share are the app list's, [S-3352].)*
  `ShowAverage` (on): in a window, the per-day average (divided by the window length, so
  idle days count) beside the total and on each row. `ShowSwitches` (off): how often the foreground
  app changed. `ShowPercent` (off): each app's share of the shown total.
- **[S-454]** Day navigation in the header, on the Header block's parts ([S-6710], `ScreenTimeDayNav`,
  G6 2026-10-29): the day as its informing label ("Today", "Yesterday", "Mon 26 Oct"), then two 24
  header buttons, back and forward, each wanted only while it can go somewhere (forward away from today,
  back until the oldest day), so the header folds them as any; back to today is the card menu's
  "Back to today" while a past day is pinned (the header's third button went: two at most). Today plus
  89 days back. A pinned date stays that date across midnight, and returning to
  today unpins. A window shows "Last 7 days" or "Last 30 days" as the label alone. Reading a day includes the
  interval still open, unless paused or older than a suspend leaves it; a range has one entry per
  calendar day, oldest first, gap days as zeros, clamped to retention. Executable paths are kept per
  app so past days have icons; at start, apps in the history with no path or name are filled from
  running processes.
- **[S-455]** *(The list moved out of the config into the shared preferences, [S-3322].)*
  Right-click "Hide from list" adds an app key to `HiddenApps`; hidden apps leave every
  view and the totals re-base. The properties menu lists them with a way back ([S-3323]).
- **[S-456]** A window of Bars: one bar per day, stacked by app in the legend's order, scaled to the
  busiest day, its two ends labelled ([S-3331]). Calendar: seven columns, padded so the window ends on the
  last cell, each day a ring of its share of the busiest day (the Chart block's ring, [S-6717]; a dot
  under 16), today's weekday letter primary (the quartile shades and today's outline went: an opacity per
  level is not a token).
- **[S-457]** *(Stored in the shared preferences since [S-3322].)* Limits are daily, in seconds:
  `GoalSeconds` for the whole and `AppLimits` per app; zero
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
  switches?, hidden?, goalSeconds?, appLimits?}` ([S-3322]), the optional parts omitted when empty (bytes pinned by a characterization test). Saved
  atomically (unique temp file and rename), one writer, in order, at most once a minute while dirty,
  on lock, and synchronously at exit; only the newest 90 dates are kept. A corrupt or
  version-mismatched file is moved to `.corrupt` and an empty history started; junk keys are dropped
  on load. The tracker is started by the app host only: a debug render never hooks the
  foreground or writes a file.

Not ported: the original's config migrations (`migrateMetrics`, `applyLegacyMetrics`, the pre-period
`displayStyle` values; [S-1050]); the retired `list` style; invisible
filler rows under a short legend, since the drawing here is sized from the tile rather than pushed
by the list.

## Screen time: chart, donut and apps (SCREENTIME-SPLIT)

Screen time is three widgets on the one tracker, so a view can put the week, the share and the list
side by side and they light the same app together. Each is a module of its own
(`src/Widgets/Themia.Widget.ScreenTimeChart`, `.ScreenTimeDonut`, `.ScreenTimeApps`, each its SPEC.md and
its own ladder). What they share stays here: the SDK's `Widgets/{ScreenTimeRules,
ScreenTimePreferences,ScreenTimeDay}.cs` and `Model/Configs/ScreenTimeSettings.cs` (the period, the chart
style and the settings the configs share), Themia.Sdk.WinUI's `Widgets/ScreenTimeShared.cs`
(data, links, the look on the family's roles with each app the accent at its rank's step, `ScreenTimeLook.Brush`,
and a part not in focus at its wash, the day navigation on the Header block's parts), `Widgets/ScreenTimeSample.cs` (the previews' fixture) and
`Shell/ScreenTimeMenuLines.cs` (the menus' shared lines), the `IScreenTimeSource` contract; Core's
`Widgets/ScreenTimeTracker.cs` and `Storage/ScreenTimeStore.cs`; the host's `Services/ScreenTimeService.cs`.
Tests `ScreenTimeRulesTests`, `ScreenTimeStoreTests`, `LocalStoreFixtureTests`, the module host's
`ScreenTimeHostTests` (`S33xx_`).

- **[S-3320]** Three kinds replace Screen time (41, gone; load validation drops unknown kinds):
  `ScreenTimeChart` (80), `ScreenTimeDonut` (81), `ScreenTimeApps` (82), each with its own config
  (`screenTimeChart`, `screenTimeDonut`, `screenTimeApps`) and add-menu entry in System › Screen time ([S-3511]). They read the
  one tracker ([S-450], [S-458]); none has a timer or polls.
- **[S-3321]** Each widget has its own `Period` (Day, Week = last 7 days, Month = last 30 days, rolling
  and ending today; an unknown value reads as Day). A day period has the day navigation of [S-454]
  in the header (the donut and the list, each its own pinned day); a window shows its name there.
- **[S-3322]** The hidden apps ([S-455]) and the limits ([S-457]) are **shared**: one set for the
  machine, `ScreenTimePreferences`, kept in `usage_stats.json` with the history (`hidden`,
  `goalSeconds`, `appLimits`, omitted when empty) and saved by the tracker on its flush ([S-459]). So
  the three always add up to the same figures, and app keys never travel with a layout or an
  export. Every screen-time widget's properties menu shows them ([S-3323]) and
  writes them through `IScreenTimeSource.SetPreferences`, which replaces the whole set and redraws
  every widget. On load, blank keys and limits of zero or less are dropped; preferences changed
  before the file was read win the merge. Right-click "Hide from list" works in all three, never
  on Other.
- **[S-3323]** The shared lines (`ScreenTimeMenuLines`, user request 2026-10-19: no settings page),
  after each widget's own on its properties menu ([S-3719]) and apart from them under a divider
  (they change every screen-time widget, user request 2026-10-19): **Limits**, a submenu of the kind the
  widget draws with ([S-6074]): the chart and the donut the **Daily limit** (typed minutes, 0 to
  1440 in quarter hours, [S-3726]; 0 is no limit and reads Off), the list each app's (an app's line
  with its limit at the end, its submenu the limit, typed minutes 15 to 720 in quarter hours, and
  Remove; "No app has a limit of its own." while none; then **Limit an app**, a search over the
  apps of the last month without a limit, a pick starting at an hour); **Hidden apps**, each with
  Show again ("No hidden apps…" while none); both end with the note that they apply to every
  screen-time widget. Last, under another divider, the privacy note. Without a tracker the lines are the not-recording and
  privacy notes alone. Every change replaces the whole set through the tracker.
- **[S-3324]** The tracker tells its widgets only what changed (CPU audit 2026-10-26: it said "changed"
  every 30 s for as long as Themia ran). `ScreenTimeTracker.Revision` moves on with every change a
  read can see (seconds credited, a switch, a name, a path, the preferences, a merge or a prune,
  accrual pausing or resuming); a flush does not reset it. The sampling tick raises `Changed` only
  when the revision moved since the widgets last heard, or the day did (`ScreenTimeTracker.Tells`: a
  day period's today turns over at midnight while nothing accrues). Idle, locked or with Themia in
  front, the history stands still and nothing redraws. A lock, an unlock, the preferences, the
  history loaded and the icons filled in still tell them at once.
- **[S-3325]** A screen-time drawing moves in place (`KeyedParts`): the donut keeps one wedge per app
  and Other across reads (a read writes an arc's geometry and its tooltip only when they say
  something else; a new app gets a new wedge, one that left is let go); the chart builds its drawing
  only for a new style, number of days, plan, size, today, first day or language, and otherwise
  sets each bar's parts (kept per app in their bar, the stack laid again only when its apps or
  their order changed) and each tooltip that changed, and shades the calendar in place. The app
  list reuses its rows by position ([S-3351]).

### Chart

- **[S-3330]** `ScreenTimeChartConfig`: `Period` Week (default) or Month (a day reads as the week)
  and its style Bars (default) or Calendar ([S-3336]: an unknown one is repaired to Bars at load).
- [S-3331], [S-3332], [S-3333], [S-3334]: moved with the Screen time: chart widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.ScreenTimeChart/`).

### Donut

- [S-3340], [S-3341], [S-3342], [S-3343], [S-3344]: moved with the Screen time: donut widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.ScreenTimeDonut/`).

### Apps

- [S-3350], [S-3352], [S-3353], [S-3354]: moved with the Screen time: apps widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.ScreenTimeApps/`).
- **[S-3960]** A packaged (Store / MSIX) app's icon is its package's logo, as Start shows it: its
  executable (under `WindowsApps\<package full name>\`) is asked for as the Apps-folder item
  `shell:AppsFolder\<family>!<app id>` (`PackagedApps.AppsFolderTarget`: the manifest's
  `Application` whose `Executable` is the file, else the package's only app; a manifest that is
  absent, not XML, has a DTD or does not say gives the file's own icon). WhatsApp's
  `WhatsApp.Root.exe` has no icon resource, so the file's icon was Windows' blank program icon. One
  path for every icon of a program (`AppIcons`: screen time, the mixer, the layout editor), cached per
  (path, size); the manifest is read once per entry, off the UI thread, and a read failure is logged.
- **[S-3965]** The logo follows the card's theme. The Apps-folder item always draws the dark-theme
  asset, so Codex's white logo vanished on a light card. `PackagedApps.Logo` picks the file itself from
  the app's `Square44x44Logo` and the qualifiers of the files beside it: on a light card
  `altform-lightunplated` (or `altform-unplated` with `theme-light`), on a dark card
  `altform-unplated`; else the plated logo, drawn on the manifest's `BackgroundColor` (`#RRGGBB`;
  `transparent` or a named colour is no plate); else the other theme's unplated one. High-contrast and
  other alternate forms are never picked; within a kind the smallest size not under the drawn one,
  else the largest. No asset (or one that will not decode, logged) leaves the icon to [S-3960]'s path.
  The theme is in the cache key of a packaged icon only; the screen-time, mixer and process rows ask
  again when their theme changes.
- **[S-3351]** As many whole rows as fit, up to the ceiling; when apps are left over the last row is
  Other, so the rows add up to the total (`ScreenTimeRules.ListNamed`). Rows are reused by position.
- **[S-2272]** **One palette: the apps of a period are a ramp of the accent by rank**
  (`ScreenTimeRules.Ramp`, `Tones`, `ToneOf`, `ScreenTimeTone`): the app with the most time among what
  the widget shows (its segments, busiest first, ties by key, so the order is stable) the accent at
  100 %, then 72 %, 50 %, 32 %; Other and every app past the fourth the tertiary text role at the
  family's wash ([S-6706]: no data colour, no hue of an app's own). The same app has the same tone in
  every bar of the period, the list's dot and the ring. The alpha is the shape's opacity, never the
  shared brush's (`ScreenTimeLook.Brush`); a focused part is drawn at full strength and every other at
  most the wash (`ScreenTimeRules.Opacity`, [S-3312]). Tests `ScreenTimeRulesTests.S2272_`.
- **[S-3355]** Debug renders: `--widget-png <ScreenTimeChart|ScreenTimeDonut|ScreenTimeApps>
  --state sample|hovered|selected` draw a fixed month of sample use (ten apps, quieter weekends, a
  6 h day limit, Microsoft Edge over its 1 h; Google is named nowhere Themia shows of itself, [S-3562]) through
  `SampleScreenTime`; `hovered` lights Code and `selected` selects Edge through the render's one board, so every card on the page shows it.
  `packaged` lists the packaged apps running on the machine by their real executables, and
  Explorer, so their icons go through [S-3960]'s path.

## Layout pass over the ported widgets

Found by drawing every ported widget at every size tier (with the debug widget gallery, since
removed, [S-1863]). Sizing rules live in Core with tests in
`WidgetFitTests` (`S64x_`/`S65x_`).

- **[S-640]** A card header's title ends in an ellipsis. A Grid does not clip a child to its cell,
  so an untrimmed title was cut mid-word ("Countdow") or drawn under the trailing element.
- **[S-641]** *(Retired, group G5, 2026-10-29: no readout is scaled to fill its box any more. A
  data widget's number is on the Big steps ([S-6702]) and a one-readout widget's on the Hero steps
  ([S-6737], [S-6739]); `FluentType.Hero`, `Fit`, `FitWidth` and `Scaled` are gone.)*
- **[S-642]** *(Retired, shell sweep, 2026-10-30: every empty, problem and waiting state is the Status
  block's one row at the top, [S-6714]; `Fluent.EmptyState`, `EmptyStatePanel` and `EmptyStateFit`
  are gone with their last callers, the card's fault and waiting states.)*
- **[S-643]** *(Superseded by [S-2501]: the hub is the only reader, once per tick.)* CPU load and network throughput are deltas against a process-wide baseline, so a read
  within 250ms of the last real one is answered from that one. Two CPU tiles used to show 31% and
  0% side by side: the first moved the baseline and the second measured a millisecond.
- [S-644]: moved with the Pomodoro widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Pomodoro/`).
- [S-645]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).
- **[S-646]** *(Retired: a reading's line under is the Readout block's, [S-6701], [S-6702];
  `ReadoutLayout` had no caller left and went in the shell sweep.)*
- **[S-647]** *(Retired: the power tile is gone, [S-1910].)*
- **[S-648]** A process row shows both numbers only from 232px of content; below that only the
  ranked one, and on a narrow tile the columns stop holding open their widest value. The "by CPU"
  label leaves a tile too narrow for it and the title both.
- [S-649]: moved with the Sun widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Sun/`).
- [S-650]: moved with the Music widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Music/`).
- [S-651]: moved with the Scratchpad widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Scratchpad/`).
- **[S-652]** Glyph code points are checked against the font's cmap. The weather set, Quote and
  Wrap were not in Segoe Fluent Icons (or MDL2) and drew as boxes; the forecast folded rain, snow
  and thunder into its own thermometer icon because of it. Weather now and the forecast share one
  condition→glyph map.
- [S-653]: moved with the Quote widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Quote/`).
- **[S-655]** An informational header label ("Just now", "by CPU") gives the title at least 64px and
  is not drawn when less than 40px would be left for it. Through `MaxWidth`, never `Visibility`.
  The rule reaches a label that is the trailing element; a label among the kit's header extras (the
  exchange rates' day) is one of their parts and folds first ([S-7064]); other panels are left alone. Commands in the
  header are not touched; their views shed them.
- **[S-656]** *(Retired 2026-10-29, group G3: weather now's reading is the family's Readout on the
  step its widest case takes, [S-6702]; the condition is its line under and the glyph its lead.)*
- **[S-657]** *(Retired 2026-10-29, group G3: the price is the family's Readout and its change folds
  beside it or under it by `ReadoutRules.Fold`, [S-6707]; the session is the line under it.)*
- **[S-658]** A forecast row on a content narrower than 120 (the smallest tile) is the day and its high, and no
  row leads (they all lead or none do, [S-6711]); the sky's glyph and the low come back from 120 ([S-725]).
- **[S-659]** *(Retired, group G5, 2026-10-29: a readout no longer fills its tile. The clock's time
  was its last user (`HeroFit.Row`, `Stacked`, `Figures`, `BaselineDrop`, the solid leading, the 0.45
  meridiem): it is on the Hero steps now ([S-6702], [S-6737], the clock's [S-6741]..[S-6744]), so a
  larger card sets it on a larger fixed step and the room past the step is left empty ([S-666]).)*
  What stays is the measure: `HeroFit.Ems` takes Segoe UI Variable's advances by class (digit 0.58,
  colon or stop 0.25, space 0.28, "-" 0.41, "°" 0.39, capital 0.72, "M"/"W" 0.95, small letter 0.56,
  an unknown script 1 em, a combining mark 0), the widest over its weights and optical sizes, so a
  readout's width is judged by its glyphs ([S-6702]).


Known and left: sparklines grow in from the right for their first minute by design ([Sparkline]);
the scrolling lists (feed, folder) show a partial last row at
the bottom edge as any scrolling list does.

## Mail

The Mail widget reads Themia's own accounts ([S-529]) — Microsoft (Graph), Google (Gmail) and IMAP —
instead of a separate hub program; [S-90]/[S-91] no longer describe it. Ported from the classic app's
`widgets/office/email` (its SPEC tags in brackets) and the mail half of its `lib.rs`.

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
- **[S-549]** Screens are the kit's ([S-7040], [S-6421]): no mail account (the Set up button,
  [S-1862]), chosen accounts gone, every account needing a sign-in (naming it), each with the
  Accounts link; loading; with nothing held each failed account's line ("Work: No network");
  an empty folder ("No messages in Junk."); a list whose failed or signed-out accounts are the
  header's problem lines [S-04, S-05, S-06]. Mail's own failure sentences went with `MailScreens.Decide`.
- **[S-550]** IMAP headers: folded fields unfold, the blank line ends the block, RFC 2047 words
  decode with their own charset (the word ends at the `?=` after the encoding, so a Q word starting
  `=C3` is one word), From splits name and address (including `addr (Name)`).
- **[S-551]** Dates apply their zone to a true instant [#45(d)]; the US zone names RFC 5322 defines are
  honoured (the classic app kept EST as wall time) and any other name reads as UTC; two-digit years
  are 19xx/20xx; an unparseable date is null, never a guess.
- **[S-552]** The IMAP preview walks MIME [S-52]: first text/plain, else text/html, attachments and
  non-text skipped, quoted-printable / base64 / charset decoded before the whitespace collapse, 200
  characters, bounded (6 levels, 32 parts, 16 KB a part). The body stays bytes (Latin-1 chars) until
  a part's charset decodes it, so 8-bit ISO-8859-1 bodies survive; the legacy code pages (KOI8-R,
  windows-1251, GBK, Big5, Shift_JIS; classic #147) are read from the in-box tables, in bodies and in
  RFC 2047 words. A cut at the 16 KB bound inside a character never throws (classic #164). HTML block
  tags leave a space.
- **[S-553]** IMAP over implicit TLS, one session per job: LOGIN (literal when a value needs one), then
  the RFC 2971 `ID` (name and vendor only) when the server advertises it (163.com refuses SELECT
  otherwise, classic #169),
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
  under 240px), the time 110px; the row view keeps the most
  essential columns whose minimums fit (From, Subject, Date, Account, Preview — so a narrow tile keeps
  From and Date), in the user's order, never fewer than one; the column header needs 140px.
- **[S-557]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-558]** Write email (issue #6): the card menu offers it under Refresh, once per account the widget
  reads ("Write email from Work" when there are several), whatever the screen and whether or not the
  header shows; the header carries a pen button for it from 240px wide (a menu of the accounts when
  there are several). It opens a new message where the account lives (`MailLinks.Compose`): Microsoft
  Outlook on the web's compose page — outlook.live.com for a personal account (Graph's web links say
  so; before any message is held, an Outlook.com / Hotmail / Live / MSN address), else
  outlook.office.com; Gmail `mail.google.com/mail/?authuser=<address>&view=cm&fs=1`; IMAP a bare
  `mailto:` for the default mail app. A bare `mailto:` is the fourth launchable shape ([S-547]);
  a refusal from the shell is a line of the header's problem glyph ([S-7064]: the header's view
  buttons are the kit's, `KitHeaderButton`).
- **[S-559]** When mail last refreshed (issue #120): the menu's Refresh names the list's age, "Refresh ·
  updated 3 min. ago" (the language's short relative time, "now" under a minute or for a clock
  ahead), of the oldest account in the feed. "Updated" is when a list was last known current: fetched,
  or a probe found its counts unchanged ([S-546]); the failure stamp of [S-2612] uses the same time.
  Nothing new on the tile: the rows already carry their own times ([S-666]).

Not ported, deliberately: the per-widget header hide and label [S-03, S-19] (the card's own header
placement and label do this for every widget), the "unread only" filter, and the composite
`account:id` message ids (the account travels separately). Keyboard navigation, the header sort
button, the scroll fade and the envelope icons are ported in [S-1281]..[S-1289].

## Tasks

Ported from `widgets/office/todo` on `main` and its Graph / Google
calls in `widgets/office/email/src/lib.rs`, off the earlier hub program and onto Themia's own accounts. Owning
files: `Themia.Core/Tasks/*` (wire, Today fan-out, local store), the SDK's `Tasks/*` (model, due
dates, list rules, optimistic edits, the lists on this PC, the `IWidgetHost.Tasks` contract, the
ladder and its keys), `Themia.App/Services/Tasks/*` (the service). The widget is the module
`src/Widgets/Themia.Widget.Tasks` (its `SPEC.md`: the list picker, the styles, load repair, the
important mark). Tests: `TaskRulesTests`, `LocalTaskListsTests`, `TaskSyncTests`.

- **[S-590]** A Tasks widget reads Microsoft To Do (Graph), Google Tasks or a CalDAV server's task
  lists ([S-618], read-only) through an account with the Tasks feature, or a list kept on this PC (`TaskSource`, a closed set). An empty account
  id means the first such account and an empty list id its default list, so a widget dropped by a
  signed-in user shows tasks without a trip to settings; a *named* account that has gone is never
  swapped for another — the widget says so. Every non-list state names itself (no account, sign in
  again, account gone, no lists, loading, offline / provider error, no list on this PC) and the ones
  ⚙ › Accounts can mend link there; a list on this PC needs no account at all.
- **[S-591]** One `TaskService` per app (`AppServices.Tasks`). Widgets on the same account share its
  stored tasks and one sync ([S-2650]); a widget's own tick asks nothing. A sync never stacks on one
  still out (a forced one is remembered and runs after); failures back off through `Backoff` (a 429
  far longer). Graph and Google are
  read through `GetGraphPagesAsync` / `GetGooglePagesAsync` (400 tasks a list at most); ids are
  escaped into the path; completed tasks are filtered for Graph here and by Google's own
  `showCompleted`; Google's hidden and deleted tasks are dropped; every Graph status but `completed`
  is open work. Refresh is configurable, 30–600 s (default 60 s).
- **[S-593]** A due date is a calendar day: midnight of the intended day (Graph zoneless, Google UTC
  `Z`, a bare date, or midnight at any offset) is read verbatim; anything else is an instant —
  zoneless is UTC — resolved to the viewer's local day (the To Do mobile encoding, classic #101).
- **[S-594]** Labels: Overdue / Today / Tomorrow / short weekday (2–6 days) / `14 Oct`, counted in day
  numbers so a DST change cannot shift them. Overdue is said in words and in the critical colour, and
  never for a finished task.
- **[S-595]** Completed tasks are hidden unless "Show completed" is on; then they are struck through
  (under Today only those finished today, [S-612]). Completing one with completed hidden makes it
  leave the list at once (no timed removal). This and the filters of [S-611] are one rule
  (`TaskRules.Visible` over a `TaskFilter`).
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
  widget's config (`ApplyOrder`: known ids first, unknown ones — new tasks — after, or before with
  [S-610]; a gone id leaves no hole, a duplicate cannot double a row). A list on this PC is its own
  order. A move is written over the whole list (`MergeOrder`), so tasks hidden by "Show completed" or
  a filter ([S-611]) keep their slots. Rows move by dragging ([S-786]) or with Alt+Shift+Up / Down on the
  focused row ([S-3802]); the row's menu has no Move up / Move down.
- **[S-599]** A new task is added from the add footer pinned at the bottom of the widget ([S-3850]; Enter adds and keeps the box
  open for the next; leaving it adds and closes) and appears at the bottom (the top with [S-610]):
  optimistically under a temporary id that is never written to, then under the provider's id, with
  the whole list's order pinned into the config (`PlaceAdded`) so the next refresh cannot re-sort it.
- **[S-600]** Double-click a task (or Rename in its menu) to rename it in place. Enter or leaving the
  box commits, Escape cancels, a blank or unchanged title writes nothing. The request is a PATCH of
  the title alone, its body written with a JSON writer so a quote cannot break out.
- **[S-601]** A title is one line, cut with an ellipsis; the tooltip carries the whole title, its
  state, due date and priority. The "Title lines" setting is retired (G2, 2026-10-29): a row's
  height is the tier's, never its title's.
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
- **[S-610]** "New tasks at the top" (per widget, settings, off by default = [S-599]): a task added
  here goes first — a list on this PC inserts it at the head of its array; an account list pins it
  first in the widget's order — and tasks the widget's order has not seen (added on a phone, or a
  temporary row confirmed under the provider's id) lead the list instead of trailing it. Neither
  provider is asked to move anything: Graph keeps no order and Google's `position` is not read
  ([S-598]); the order stays the widget's own.
- **[S-611]** Filters (per widget, in settings under Filter; none by default): Due — any time,
  overdue (open tasks only: a finished task is never overdue, [S-594]), today, this week (the
  calendar week holding today, from the regional week start [S-564]), no due date; Important only
  (To Do's importance); Title contains (words, any case, at most 100 characters, written on Enter or
  leaving the box). Due and importance exist only on account tasks: a list on this PC applies the
  text filter alone and its settings do not offer the others. Filters combine with "Show completed"
  and Today. A list a filter empties says "No tasks match the filter."; while a filter narrows the
  list the add footer hides ([S-3850]), because a task typed there could vanish as it is added.
- **[S-612]** A finished task carries when it was finished (`TaskStamp`, parsed at the boundary from
  Graph `completedDateTime` — To Do may write the day only, midnight, which reads as that day — or
  Google `completed`; a value that is not a date, or one on an open task, is none). Under Today,
  "Show completed" shows only the tasks finished today in the viewer's zone, as To Do's My Day does
  (#137); a list shows every finished task. A tick shows its own moment until the provider's arrives,
  a reopen clears it. Stored in the task shelf ([S-2651]); a list on this PC has no Today and keeps none.
- **[S-613]** My Day (#128, #137): Microsoft Graph does not expose it — no `todoTask` property
  (v1.0 or beta) says a task is in My Day and no `wellknownListName` names it (checked 2026-10) —
  and Google Tasks has no such list. So Today ([S-596]) stays what can be read honestly: every task
  due today or overdue, across the account's lists, with today's finished ones ([S-612]). It is
  called "Today", never "My Day".
- **[S-614]** The header's list picker (a chevron after the badge, from Regular width; like Notes'
  [S-1364]): Today and the account's lists, or the lists on this PC, to switch to (an account list
  picked here starts its own order, as in settings), then Rename list… for the list shown, in a box
  under the picker (Enter or leaving it renames, Escape does not; a blank or unchanged name writes
  nothing). An account list is renamed through the account's queue ([S-2610]): `PATCH` of
  `displayName` (Graph) or `title` (Google) alone; the new name shows at once in every widget on the
  account, a second rename replaces a queued one, a confirmed one stays on top of a list read that
  started before it, and a refusal puts the old name back and says why. Today and To Do's own lists
  (`wellknownListName` other than `none`: Tasks, Flagged email — To Do refuses to rename them) offer
  no Rename. A list on this PC is renamed in its pool ([S-603]).
- **[S-618]** CalDAV task lists (issue 185): the account's calendar home set (found as for calendars,
  `DavHomes`: the stored collection, its home set directly or through the principal, never on another
  origin, [S-578]) is listed at Depth 1 and every collection whose
  `supported-calendar-component-set` includes VTODO is a list (`DavOffice.TaskLists`; one that
  advertises no set takes every component, RFC 4791 §5.2.3); a VEVENT-only calendar and the
  scheduling inbox are not. The stored collection is the default list when it takes tasks, else the
  first. With no home set to list, the stored collection is the one list. The lists are the same
  chooser as every account's ([S-592], [S-7516]), Today included ([S-596]).
- **[S-619]** A list is read whole with a `calendar-query` REPORT for its VTODOs (`DavOffice.TodoQuery`,
  no time range, no expand, finished ones included and filtered here, [S-595]) on the account's sync
  and cadence ([S-591], [S-2652]: single-flight, backed off, failures in the one vocabulary,
  [S-615] its stale rule), no change cursor. `IcsTasks` reads each VTODO: SUMMARY (unescaped,
  unfolded), DUE (a DATE or a wall-clock midnight, floating or zoned, is the day; any other time is
  the instant it names through the object's VTIMEZONE, [S-578], [S-593]), done when STATUS is
  COMPLETED or, with no STATUS, when COMPLETED is set or PERCENT-COMPLETE is 100 (COMPLETED its
  stamp, [S-612]), important for PRIORITY 1 to 4 ("Important only" is offered, [S-6070]). A
  CANCELLED task is no row; one without a UID is skipped (no id survives a refresh); a UID seen twice
  is one task. The task model is flat and has no categories: RELATED-TO subtasks are rows like any
  other, CATEGORIES are not read. VEVENTs and VJOURNALs in the same collection are not tasks.
- **[S-620]** A recurring VTODO (RRULE over DUE, else DTSTART; EXDATE; RECURRENCE-ID overrides) is one
  row under its UID: its current instance, the first occurrence due today or later that an override
  has not finished or cancelled (earlier ones count as done), with a moved instance's own fields;
  once the rule has run out, its last occurrence; a finished master is the finished task. An
  unsupported rule ([S-576]) shows the master once. Picked at each sync against the viewer's day.
- **[S-621]** CalDAV tasks are read-only, by decision (issue 185): writing a VTODO back is an
  If-Match PUT of the whole object other clients wrote. The task account says so
  (`TaskAccount.ReadOnly`, `ITaskFeed.ReadOnly`, `TaskWire.ReadOnly`): no tick (the check does
  nothing; its tooltip and the row menu's one disabled line say "Read-only here: change it in the app
  it syncs with"), no Rename or Delete, no add row, no Rename list…, and the properties menu says
  why under the list line ("Tasks from <account> are read-only here…"). The week planner's ring
  does nothing and says so. The service queues no write for the account and the remote refuses one
  (`AccountError.Refused`) as the floor under it. A widget's own row order ([S-598]) still moves:
  it is the widget's config, not the server's.

Not ported, deliberately: the pop-out window [S-39] (out of scope by user decision), the per-widget
header hide and label [S-02, S-20] (the card's own header does this for every widget), the 600 ms delay before a completed task leaves the list [S-26] (no wall-clock timing in the
list; it leaves at once), the one-shot `localTasks` config-bag migration [S-59] (the app is
unreleased: no compatibility code), and the single click on a title that renames with the caret at
the click point [S-50] (a click focuses the row for the keyboard; double-click or F2 renames).
Drag-to-reorder, keyboard navigation, Tab between editors, the scroll fade and the "!" badge are
ported in [S-781]..[S-788].

## Calendar and contacts

The Contacts widget is the module `contacts` (`src/Widgets/Themia.Widget.Contacts/`, its `SPEC.md`:
the view's search box, the details avatar and the page's count box); the people, their rules and
the shared source stay here under their tags.

The classic Calendar and Contacts widgets, on Themia's own accounts ([S-529]) instead of the earlier hub
hub. Pure rules in `Themia.Core/Office` (tested), one shared source in `Services/Office`, the views in
`CalendarView*.cs` / `ContactsView.cs`, options in the SDK's `OfficeMenuLines` (the properties menu, [S-3719]).

- **[S-560]** Calendar shows events from Microsoft Graph (`calendarView`, `Prefer:
  outlook.timezone="UTC"`), Google Calendar (`events`, `singleEvents=true`) and CalDAV (`REPORT
  calendar-query`) accounts. An all-day event is held as two dates (first day, exclusive end) and is
  never converted between zones; a timed one as two instants. Clicking an event opens its provider
  page (CalDAV has none: tooltip only).
- **[S-561]** Config, one for every calendar style ([S-3601], [S-3641]): `accountIds`, `calendars`,
  `calendarColors`, `showBirthdays` (on); the week style adds `days` and `firstDay` ([S-3637]), the agenda
  `agendaCount` (1–100, 20) and `agendaDays` ([S-3636]). No `layout`: the style is the layout
  ([S-3640]). The context menu offers Refresh.
- **[S-562]** An empty `accountIds` means every calendar account (later ones too — the Mail
  pattern); the menu offers "Every calendar account" or a choice where the last cannot be turned off.
  Chosen accounts that were signed out are skipped; when none are left the widget says so with a
  link to ⚙ › Accounts.
- **[S-563]** Every value is read leniently: a wrong type or unknown name becomes the default
  instead of refusing the state. *(The hearth-era config fold-in that shared this tag is retired,
  [S-1050].)*
- **[S-564]** Week start Monday, Sunday or Saturday (the Iranian week, fa-IR) drives the month grid, the week view, the year's small
  months and the weekday header; the day and the agenda have none. It is the shared regional
  setting ([S-3770]), not a calendar's own: no calendar stores `weekStart`. Only the week calendar
  may start its columns on today instead, for itself alone ([S-3637]).

- **[S-565]** Provider datetimes: a zone (`Z`, `±HH:MM`, `±HHMM`, negative offsets included) is kept;
  a zoneless one is UTC. Cancelled events (Graph `isCancelled`, Google `status: cancelled`, iCal
  `STATUS:CANCELLED`) are not listed.
- **[S-566]** Colours: a fixed six-colour palette by fetch-target index; a valid `#rrggbb` override
  per calendar (keyed by calendar id, or account id for a default calendar) wins; provider colours
  are normalised (`#rrggbbaa` → `#rrggbb`, `""`/`auto` → none). Chips carry colour when more than one
  calendar is shown or an override is set. Birthdays use the caution hue.
- **[S-567]** Several calendars per account: the menu's Calendars line lists each chosen account's
  calendars (Graph `/me/calendars`, Google `calendarList`, CalDAV home-set listing; one account's
  straight in it, several a submenu each, read as [S-3746] says) with a colour (Automatic or one of
  the six, under Calendar colors when there are several) and, when there is more than one, a switch. No selection means the default calendar;
  deselecting the last one goes back to it. Google signs in with `calendar.calendarlist.readonly`
  beside `calendar.events.readonly`, which reads events but cannot list calendars (original PR
  #172); a sign-in not granted the list scope (403) is offered its primary calendar.
- **[S-568]** Ranges: month = whole weeks around the month; week = its days ([S-2719], a whole week
  by default); day; year = 1 Jan – 31 Dec; agenda = its date range from today ([S-3636], 30 days
  with today by default). Requested from local midnight to local midnight (the classic app used UTC
  midnight); what decides them beside the kind and the anchor is one `CalendarPeriod` (week start,
  week days and first day, agenda days). The header title is the period ("October 2026",
  "28 Sep – 4 Oct", "Sun 4 Oct", "2026"; a one-day week reads like a day); the agenda's is the
  widget's name.
- **[S-569]** Back / forward step a month (day clamped), the week's period ([S-2719]), day or year (29 Feb clamped); the
  agenda pages its rows ([S-2709]). They are the header's two buttons ([S-7064], 24 with 16 glyphs;
  a narrow header folds them from the last). Today is the card menu's line while today is off
  screen (the agenda: past its first page) and the T key, no third header button (the header holds
  two). The widget follows today across midnight until the user pages away.
- **[S-570]** A day lists birthdays, then all-day events, then timed ones by start; an all-day
  event covers [first, end); a timed one shows on every local day it overlaps.
- **[S-571]** Agenda: grouped by day ("Today", "Tomorrow", "Mon 5 Oct"; [S-2716]), at most `agendaCount`
  entries within its date range ([S-3636]); an event spanning several days is listed under each day it covers (classic #155), one
  still running from before from today on; timed events already over are left out. A timed event's
  row shows that day's part of it (18:00–00:00, "All day" for a day it covers whole, 00:00–10:00).
  Rows are the kit's schedule look ([S-7036]): the title, the start as the value ("All day", "Now"
  strong while under way), the span, place and organizer on the second line where the tile gives two.
- **[S-572]** Month cells (the family's tokens, [S-6703]): chips (Caption on the item layer's chip fill,
  the item's mark as a 2 px bar at the start, the one radius, lanes 4 apart) as many as fit, the
  overflow as "+N" beside the day number; on smaller cells up to three dots (4, 4 apart; two in narrow
  cells) plus a muted one; on the smallest the number alone, strong when the day has something.
  Today is the accent's round pill under its number. A birthday's mark is the tertiary role, never
  the caution colour (it is not a state).
- **[S-573]** Day: an all-day band (all-day and multi-day events, birthdays, as span bars, [S-2718])
  and a timeline of the hours that fit ([S-2705]), overlapping events in side-by-side lanes. Not scrolled.
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
  Config: `accountIds` (empty = every contacts account), `layout`, `columns`, `count`.
  Clicking selects (the keyboard's ring, [S-1288]), clicking the background clears. With no contacts
  account the card's Set up row says "Add an account to show your contacts" ([S-1862]).
- **[S-581]** Layouts: list (monogram, name, role, address/number), compact (name and one field),
  details (table), grid (cells). Details narrower than 88 px a column fall back to the list, a grid
  under two cells to compact. The count is the header's neutral pill and folds with its extras
  ([S-7064]).
- **[S-582]** Details columns stay in canonical order (Name, Email, Phone, Company, Title); the last
  one cannot be turned off.
- **[S-583]** `count` is at least 5 (steps of 5, no upper bound); each account is asked for
  clamp(count, 500, 1000) so a search spans the address book; search filters before the cap.
- **[S-585]** Search matches name, email, company or job title, case-insensitively.
- **[S-586]** Monograms: one or two initials (the first and last word) in the secondary role on the
  subtle fill, round, 32 (48 in a grid on an expanded width); a person is not a data colour, so no
  hue of their own (G2, DECISIONS 6; was `hsl(hash(name), 38%, 34%)` with white initials).
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
accounts out (⚙ › Accounts is where sign-in is mended), the birthday-permission toast (there
is no toast pipeline yet; the header warning stays and opens Accounts), and birthdays from CardDAV
(the classic app had none either). Keyboard navigation goes through the shell's keyboard lease: see
[S-1269] / [S-1277] and [S-806] / [S-807].

## Widget sizing (SIZE)

Content decides a widget's size; the size no longer decides its content (owner's decision
2026-11-12: widgets grew without end and filled the room; they get a largest size and are split into
small widgets instead). Owning files: `Themia.Sdk/Modules/WidgetSizing.cs` (the contract),
`WidgetDescriptor.Sizing` / `.SizeSets` (each kind's), `Themia.Core/Geometry/SizeFit.cs` (the host's
side); tests `WidgetSizingTests` (Core: the rules; module host: every kind, the legacy list),
`SizeFitTests`. How a kind is converted: docs/WIDGET-KIT.md 17.

- **[S-2438]** A kind states its sizes as a pure function of its config (`WidgetDescriptor.Sizing`,
  a `WidgetSizing`): `Min`, `Natural` and `Max` (`SizeDip`, a width and a height) in DIPs of the
  card's box, its header included, at widget scale 1 and with the kind's default header. An axis
  whose Min equals its Max is fixed. A size is fitted into the sizing axis by axis: held to Min..Max,
  a stepped axis on its nearest step (`WidgetSizing.Fit`).
- **[S-2439]** An axis may take only steps (`SizeSteps`: `Base + n × Step` for n in
  `MinCount..MaxCount`; a list's n rows). With `Count` set the axis follows the config: its size is
  the config's step, whatever is asked of it.
- **[S-2440]** **A kit list that states its rows** (`IKitRows` on its config: `Rows`, 1..20, default 6;
  `Lines` per row, 1 or 2): its height is the family's content top (36), its rows (28 or 40 each,
  [S-6704]), the add footer (28) where it adds in place and the bottom inset (12), stepped by a row
  and following the config's rows; its width 220..520, a new one 300 (`KitSizing`). Dragging its
  bottom edge sets Rows ([S-2442], `KitSizing.SizeSets`); more items than rows scroll inside them (the
  wheel as before). Its plan is the same at every size in its range (`KitChoices.Rows`: one column,
  its lines, every field it has on, the text cut with its ellipsis; a check list's add row and done
  heading always, its due in the short words). Its menu has Rows and, where its rows can have a second
  line, Lines per row (`KitMenuLines.Rows`). A kit list without it keeps the ladder and the rows
  fitted by height ([S-7031], [S-2443]). Tests `KitRowsTests`.
- **[S-2441]** Every kind's sizing holds, for its defaults and every style: sizes positive and finite
  (a legacy Max unbounded), Min ≤ Natural ≤ Max on both axes, a stepped axis's Min, Natural and Max
  on its steps and its count in range (`WidgetSizing.Problems`, checked over the registry).
- **[S-2442]** A kind whose size follows a count answers, for its config and a wanted size, with the
  config that size means (`WidgetDescriptor.SizeSets`: a list dragged to five rows tall has Rows = 5).
  It returns a copy and never changes the config it is given. A kind has the hook exactly when one of
  its axes follows its config.
- **[S-2443]** **Legacy until converted.** A kind not yet converted states `WidgetSizing.Legacy(w, h)`:
  Natural its old default size, Min the 32 floor ([S-08]), no Max. It keeps the old way whole: no fit,
  `IWidgetSizeAware` with the ladder ([S-660]..[S-666]) and the seven tiers. The module host's
  `WidgetSizingTests.Legacy` lists those kinds and only shrinks.
- **[S-2444]** **On a display** a sizing is DIPs times the card's content scale ([S-6532]: the display's
  and the widget's own) plus the gap, as fractions of that screen's cell (`SizeFit.Limits`,
  `SizeLimits`, `AxisLimits`). It is stated with the kind's default header ([S-700]); a card that
  shows a header the kind did not count adds what it costs, one that hides a header the kind counted
  takes it away (`SizeFit.ForHeader`): a header above costs its line and gap (24) in height, the inset
  over it being there either way ([S-5700]); one at the foot its gap, line and own inset (36) in height
  and a side strip the same in width, since the view keeps its inset on that side as well. A fixed axis
  has no handle: its two side handles lose their grip and bar, a corner resizes the free axis alone
  with that axis's cursor, and on a widget fixed both ways the corners keep their brackets as marks
  but take no press (`EditMarkers.Moves`, `WidgetCard.ApplyResizable`).
- **[S-2445]** **Resizing a converted kind** ([S-31a]): the moved edge also stops at the sizing's Min
  and Max, and on a stepped axis lands on the nearest step, which replaces the grid as its snap
  (`GridGeometry.Settle(…, limits)`); a fixed axis does not move. Shift+arrows obey the same, a press
  on a stepped axis being one step (`KeyboardNudge.Resize(…, limits)`).
- **[S-2446]** **Layout on any display.** Geometry stays fractions of the monitor ([S-01], [S-02]).
  Where the stored rect comes out outside the sizing in DIPs on this display (another monitor, DPI or
  scale, a changed config), the card is painted in the stored rect fitted into the sizing, anchored at
  its top-left (an axis that follows the config at the config's step), then kept in reach as any rect
  ([S-09]) (`SizeFit.Shown`, in `PositionCard`; the card's `LiveRect` is that box, so the hit region,
  the markers, a move and a resize start from what is seen). Being shown never rewrites the stored
  rect; a gesture (a move, a resize) or a config change does.
- **[S-2447]** **A config change that changes the sizing refits** the widget's stored rect in the same
  mutation, one undo step: fitted into the new sizing at its top-left and shifted back into reach
  where it would leave the work area, on the display that shows its view (`AppHost.NoteSizings`, over
  every `Mutate`; a widget first seen, loaded or brought back by an undo, is noted as it is). A
  resize of a kind whose size follows a count stores the config that size means with the rect, in
  one mutation and one undo step; while it is dragged the view already shows that config
  (`WidgetCard.LandResize`, `SizeFit.ConfigFor`; the keyboard's run of resizes lands the same way).
- **[S-2449]** **A big number's size is a setting.** A one-readout widget (the clock, the timers, the
  toggles) converted to the sizing states its readout's size in its config (`IReadoutSized.Size`,
  `ReadoutSize` Small, Medium, Large: the family's 40, 68 and 96, `ReadoutSizes.Step`; a new one
  Medium), set by one menu line, Size (`KitMenuLines.ReadoutSize`), and its sizing follows from the
  widest text at that step (`HeroFit` widths, [S-6702]); the number never grows with the card. A data
  widget's converted number is the family's 28 ([S-2220]; the 40 step stays for a kind that states
  it): `ReadoutRules.Ceiling` by area is retired for converted kinds and kept for legacy ones.
- **[S-2448]** **A converted kind is checked at its range**, not the seven tiers: its Min, its Natural
  and its Max (`SizeFit.RangeTiers`, named `min`, `natural`, `max`; `SizeFit.CheckedAt` picks the range
  or the tiers). `--widget-png <Kind>` draws a converted kind at its range by default and a legacy one
  at the tiers; `--sizes range|tiers|WxH,..` picks (`range` on a legacy kind says it has none). The
  showcase views ([S-1810]) hold a converted kind at min, natural and max, packed as the tiers are; the
  family render walk (`--family-check`) walks the range, its page sized for the largest. A store scene
  ([S-1074]) paints a converted kind fitted into its sizing as the desktop does ([S-2446]).

## Responsive layout (shared foundation)

*Legacy until converted ([S-2443]):* [S-660]..[S-666] hold for the kinds on the legacy list only. A
converted kind states its sizes and shows the same content inside them ([S-2438]..[S-2449]): no
`Disclosure`, no `WidgetArea` classes, no tiers. The types stay until the list is empty.

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
- [S-665]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).
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
- **[S-667]** *(Retired 2026-10-29, group G3: `HeroReadout` is gone; every data widget's number is
  the family's Readout ([S-6702], [S-6707]). `HeroLayout` went with the drive's ring face, `RingFace`,
  in group G6.)*
- **[S-668]** *(Retired 2026-10-29, group G3: the stat line and cell (`WidgetParts`, `PartMetrics`) are
  gone; a reading is a fact row of the Row block, the value primary, [S-6711].)*
- **[S-669]** *(Retired 2026-10-29, group G3: the forecast's days are the family's Row block and tokens at
  every size ([S-725], [S-726], [S-6171], [S-6711]); the day-row parts, `DayScale` and its ramp steps are gone.)*
- **[S-3860]** **One hero ladder for chart and reading widgets.** While a hero shares its tile with
  the content it heads (a chart, readings) it grows to the tier's step, by the tile's smaller size
  class: Title up to Regular, TitleLarge at Expanded, Display at Huge, fitted down where the width
  is short. No widget uses it any more (Stocks left it with group G3, `HeroLayout.TierStep` is gone):
  the meters, the GPU, the detail widgets, the price and the rate are on the family's Readout ([S-6702]: 28 at XS to Tall, 40 at L and
  XL, and no larger; [S-1762], [S-2402]). A formatted reading is split for a
  readout by `ReadoutRules.Split`: the leading figure (digits of any script, `.` `,` and the Arabic
  separators) and the unit after it with the space that stood between ("42" + "%", "1.2" + " MB/s");
  a text that does not start with a figure and a unit ("--", "%42", a direction mark, "1 234 ms")
  is drawn whole, never reordered. The fit measures a worst-case reading as it is drawn (the number
  at the step, the unit at the unit's step), never all of it at the number's size.
- **[S-3861]** *(Retired with G1 of the family, 2026-10-29: a detail widget is concise first, [S-666];
  room its rows cannot use at L and XL stays empty rather than growing the figure or centring the
  block, [S-2402].)*
- **[S-3862]** A detail widget's facts are fact rows (the Row block, [S-6711]): one line of 32 each,
  the value primary and end-aligned, the name secondary, no glyph, as many whole rows as fit
  ([S-6712]). A fact that is "--" or of a thing not on this PC is drawn in the disabled role. The stat
  cells and lines (`StatPart`) are no longer theirs.
- **[S-3850]** **The add footer** (every widget that adds an item in place: Habits [S-1427], Tasks
  [S-780]). One Row block of 32 ([S-6711]), built only by `AddFooterRow`: the + glyph as its lead and
  its name the secondary Body line ("Add a habit", "Add a task"), the card's side inset, the row hover; a tap swaps the line for the widget's text box (Enter adds and keeps it
  open, leaving it adds and closes, Escape cancels). It is **pinned to the bottom of the content
  box**: the list sits at the top and scrolls or pages above it, and the room a short list leaves is
  between them, never under the footer (the Windows 11 list-ends-in-its-add-row pattern, held in
  place). Whether it shows is one rule (`AddFooter.Show`): only where an add is possible; always
  under an empty list, at any size (it is then the way to begin); otherwise a rung right after the
  first row, from Regular height, when a whole row is left for it. Where it hides, the widget's
  settings still add. It takes no keys of its own; the editor in it keeps the widget's lease
  ([S-809]) while it is open.

## Shell skeleton and shared settings

Prep for the parallel shell and widget packages (S-700..S-719). Mostly file layout; the behaviour
rules are these.

- **[S-700]** The clock starts with its header **hidden**, as in the original
  (`WidgetInstance.DefaultHeader`); so do the power buttons ([S-1911]), the audio visualizer
  ([S-3200]), Alarm, Gallery, Video stream, Now playing, Quote, Stopwatch, Pomodoro, Focus session and
  Next meeting ([S-2590]). The sun and the moon start with theirs since G3 and the drive card since G6 (user
  decision 2026-10-29: every data widget has the Header, [S-3100]; [S-4675] retired).
  The disk meter starts with its header like the other meters (user requirement), so its reading
  sits where theirs does ([S-1161]). Only creation reads the default; a loaded widget
  keeps the placement it was saved with.
- **[S-2590]** Not every widget starts with a header (owner decision 2026-11-12: "for some it looks
  better without"): Alarm, Gallery, Video stream, Now playing, Quote, Stopwatch, Pomodoro, Focus session
  and Next meeting start headerless (`WidgetTraits.StartsHeaderless`), as the clock and the audio
  visualizer already did ([S-700]); their content (a time, the pictures, the stream, the track, the
  quote, the face, the meeting) says what they are. The time is centred in the card's insets, the
  pictures and the stream are drawn edge to edge, and the others are laid from the inset the card gives
  a view with no header above it ([S-5700]), so nothing of theirs stands on the card's edge. Each that may have
  a header keeps the choice (the header placement, [S-3711]); only a new widget reads the default, so the
  add menu's previews, the renders and the showcase follow it and a store scene that sets a header keeps
  it. Nothing that lived in the header is lost: Alarm's actions are its switch and its right-click menu,
  Video stream's its control bar and right-click menu, Gallery's its right-click menu; Gallery's
  slideshow counter is the header's informing label and comes back with the header ([S-767]), Video
  stream's address is its header's name. Quote's header button, Show another quote, is on its
  right-click menu too ([S-5711]); Now playing's, Stopwatch's, Pomodoro's and Focus session's headers
  have no button (their controls are in the content); Next meeting's when line was its first line and
  stands over Join now ([S-5710]). Tests `WidgetRegistryHostTests.S2590_`.
- **[S-701]** *(Retired by [S-3585]: there is no settings window, so no table of its pages.)*
- **[S-702]** Behaviour switches live in `AppState.Preferences` (`AppPreferences`), apart from the
  look (`DesignState`): language (`AppLanguage`, default follow Windows), hide in full-screen apps
  (off), desktop icons (on), the regional formats and the update cadences every widget shares
  ([S-3770], [S-3780]). There is no local-network switch ([S-3840]).
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

- [S-760], [S-764], [S-765], [S-6402], [S-766], [S-767], [S-768], [S-769], [S-770], [S-771], [S-773], [S-2760], [S-776], [S-777]: moved with the Gallery widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gallery/`).
- **[S-761]** File: one picture full-bleed. An empty box is the Set up button ([S-1862]), as is a folder source with no folder; a name that is not
  a picture is refused in settings and reported by the widget, and a missing file says so. The
  picker offers only the extensions the widget can draw.
- **[S-762]** Bing: the `HPImageArchive` JSON (today, en-US) through the shared HTTP client and a
  source-generated contract. Only a path on `www.bing.com` is accepted (protocol-relative, absolute,
  backslashed and non-rooted paths are refused). Offline and "could not load" are separate states;
  the reply goes through a shared web cache (one request for every widget showing it, fresh for half
  the refresh interval), and a failed ask with an earlier photo in hand keeps showing it ([S-3410]).
  The picture is decoded at the size shown and cached by the system's HTTP cache; a failed load is
  dropped from the decoder cache so the next refresh asks again.
- **[S-763]** *(Retired: the photo of the day's title and credit caption and its `ShowCaption`
  setting are gone, [S-2760].)*
- **[S-1992]** Slideshow keys (`ControlKeys.Slideshow`) beyond [S-811]: Home / End jump to the first
  and last picture, Enter opens the shown one (local files only, as a double-click does). The other
  layouts take no keys, and a click on them no longer takes the keyboard (`WantsKeys`).
- **[S-772]** *(Retired by [S-3792]: no `RefreshMinutes`; every gallery re-reads its source hourly.)*
- Left / Right / Space keys ([S-29] of the original) are ported through the keyboard lease
  ([S-811]). Not ported: a file or folder caption
  of date taken. The slide minimum is 1 s, as in the original ([S-1620]).
- **[S-3112]** The photo of the day credits its picture: Bing's pictures are copyrighted and Bing
  shows a credit with each, so the picture's hover tooltip is that credit (e.g. "© Photographer/
  Agency"). Only a tooltip: nothing is drawn on the tile, no title, no setting, and the
  screen-reader name is unchanged ([S-2760]). `BingCredit` reads the reply's `copyright` field at
  the trust boundary: control and format characters stripped, white space collapsed and trimmed,
  only the "© …" part kept when there is one (Bing's field leads with the place), capped at 200
  characters without splitting a surrogate pair. A missing, blank or bare "©" field is no tooltip;
  the photo still shows. Local pictures (folder, file) have no tooltip. (Tests `S3112_`.)
- **[S-3410]** One shared source per folder (with or without its subfolders), picture file or photo
  of the day (`GallerySourceKey`: the path compared as Windows compares paths), owned by the app for
  its lifetime (`GalleryService`); the view is a thin subscriber that holds its source in Attach and
  lets go in Detach. A source is read once when its first widget joins, again when its folder's
  watcher sees a picture arrive, go or be renamed (names only: a file being written wakes nothing),
  hourly ([S-3792]), and at once on "Refresh images";
  **never because a widget was built again**, so a view switch, edit mode or a display change
  re-reads nothing. One read is out at a time; asks while it is out are one more read after it. A
  read hands back every picture found in a stable order; each widget takes its own selection
  (shuffle, count) from it, so changing the shuffle or the count reads nothing. A read that found
  what was there already redraws nothing. (Tests `S3410_`.)
- **[S-3411]** Decoded pictures are shared by every gallery widget (`PictureCache`): decoded at the
  size drawn (physical pixels, widths rounded up to 64 and capped at 3840, `DecodeWidth`; a larger
  decode already held serves a smaller ask), two decodes at a time across all widgets. A frame on
  screen pins its picture and a pinned picture is never evicted; besides them the cache keeps, least
  recently used out, half as many bytes again as are on screen, at least 32 MB and at most 96 MB ([S-6605])
  (`PictureBudget`, four bytes a pixel). So a widget built again finds its pictures decoded and draws
  them in the same pass, with no blank or loading state. Suspending the desktop ([S-928]) drops
  every picture nobody shows. (Tests `S3411_`.)
- **[S-3412]** Each widget keeps its place by widget id for the session, never persisted
  (`GalleryPositions`, at most 256 widgets): the picture on show, its position, the seed its
  selection and scatter / mosaic are dealt from, and whether it was paused. Built again, it comes back
  on that picture (found by path wherever it is now, else the same position kept inside the list)
  and the slideshow continues from it. The seed is dealt again only by "Refresh images" and, for a
  shuffled folder larger than the widget shows, by the refresh interval (so in time it shows all of
  it, [S-304]); a folder change, a first read and a widget built again never deal
  (`GalleryDeal`). The shuffle ranks each path by a hash mixed with the seed (`ImageFiles.Choose`), so
  a picture arriving or going moves no other one, and the order does not depend on how the disk
  listed the files. (Tests `S3412_`.)
- **[S-3413]** A source nobody shows lingers 2 minutes (the
  shared `LingerTable`), with its list and its watcher but no ticker subscription, so a view switch
  and back within it finds everything in hand; then it is released: watcher closed, read cancelled,
  and the decoded pictures only it showed dropped. A widget coming back later reads its source again
  but still returns to its picture ([S-3412]). (Tests `S3413_`.)
- **[S-774]** A sticker (issue #158): a `File` source turned (`Rotation`, any whole degree, read in
  (−180, 180]) or outlined is drawn **whole** (the fit setting is hidden, never a crop) and turned
  about its centre by `RotatedBox`, the video stream's panel ([S-468]) generalised to any angle: the
  picture is laid out at the largest box of its own shape that still fits the frame once turned
  (`StickerRules.FitRotated`; a quarter turn is the old axis swap), so nothing is clipped at any
  angle. Folders and the photo of the day are not stickers. Settings: Rotation (−180°..180° slider)
  and Outline, shown for one picture only. (Tests `S774_`.)
- **[S-775]** The outline follows the picture's own transparent shape: every pixel at least 50 %
  opaque is the shape, and the band is the exact Euclidean distance from it (`StickerRules.Outline`,
  round at corners, half a pixel of edge softening), `OutlineWidth` 1–24 DIPs wide (default 6) in
  `OutlineColor` (`ColorPickerRow`, default white; one that does not parse is white). The pad is
  settled in the picture's pixels so the band shows that many DIPs once fitted and turned
  (`StickerRules.OutlinePad`). A picture with no opaque pixel has no outline. (Tests `S775_`.)

## Weather: current conditions and forecast

Round 3 of `WeatherNow` and `WeatherForecast` (originals `weather-current`, `weather-forecast`).
Both are modules (`src/Widgets/Themia.Widget.WeatherNow/`, `.WeatherForecast/`, their own tags in
their `SPEC.md`); this section keeps what they share. Shared rules in `Themia.Sdk/Widgets`
(`WeatherReport`, `WeatherData`, `WeatherGlyph`, `WeatherSample`), the rows in `Themia.Sdk.WinUI`
(`WeatherPlaceRows`), the store in `Themia.Core/Storage/WeatherCacheStore`; tests in
`WeatherCacheStoreTests`, `WeatherReportTests` (`S72x_`).

- **[S-720]** One open-meteo request per place carries the current block (temperature, feels-like,
  humidity, wind + direction + gusts, precipitation, pressure, cloud cover, UV), today and the next
  five days (high/low, sky, precipitation sum + max chance, max wind, max UV, sunrise, sunset),
  `timezone=auto`; no hourly block (nothing draws it). The forecast asks the same daily fields for
  1..14 days. Every value is range-checked; a missing daily block still yields a reading.
- **[S-721]** Day labels: "Today", then the culture's own abbreviated weekday (English while the app
  runs globalization-invariant).
- [S-722], [S-723], [S-6160], [S-6161], [S-6162]: moved with the Weather now widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.WeatherNow/`).
- [S-724], [S-725], [S-726], [S-727], [S-6163], [S-6164], [S-6171], [S-6172]: moved with the Weather forecast widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.WeatherForecast/`).
- **[S-728]** The header's label is the chosen place's city (first part of `LocationName`,
  `WeatherConfig.PlaceLabel`) beside the widget's name ([S-2260]); before a pick there is none (the
  typed query is not kept, [S-2846]). A title the user typed replaces the name; it is the one way to
  change what the header says ([S-3590]: the Location name switch, `HideLocation`, that hid the city is gone).
- **[S-729]** The last good answers persist in the sealed `web` shelf of the offline store
  ([S-2660]; `schemaVersion` 1 inside, one writer, saved after each fetch that brought a new answer).
  At launch they are restored marked `Restored`: drawn at once, fresh only for their real age
  ([S-2662]); the card menu's Refresh line says that age ([S-7042]). A document
  of another schema or not JSON is refused whole (a damaged shelf is set aside, [S-2605]); entries
  older than 3 days, from the future, or without a temperature are dropped. A debug render never
  reads or writes it.
- **[S-730]** "Refresh" in both widgets' menus resets the back-off and bypasses cache freshness
  (`WebCache.GetAsync(force)`), still joining a request already in flight.
- **[S-731]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-732]** *(Retired by [S-2840]..[S-2846]: the two-row search, its separate results drop-down
  and the "Change" button that cleared the place are gone.)*
- **[S-2840]** The location is one line on the properties menu, a search ([S-3722], user request
  2026-10-19: no settings page), shared by the weather family (`WeatherPlaceRows.LocationLine`) and
  Sun and Moon (`SkyPlaceRows.LocationLine`, [S-3101]), both over `PlaceSearch.MenuAnswerAsync`:
  title "Location", the chosen place's city at the line's end (none: nothing); its submenu's box
  says "Search for a city". The box is only ever a question, never the choice: a pick stores
  coordinates + label. No Change mode; clearing is [S-2848]. The device's own location is the
  Use my location switch before it ([S-2850]); there is no app-wide default place to fall back to.
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
- **[S-2844]** *(Retired with the settings page's search box, 2026-10-19: the menu search's keys are
  [S-3722]'s.)*
- **[S-2845]** One search at a time, a newer one cancels the older. Answers (places or no match) are
  kept for the session in a 32-entry LRU keyed by the query in any case and the app language;
  failures are not kept. The geocoder is asked in the app language (`language=`), through the
  guarded `WebFetch` and the source-generated JSON context only.
- **[S-2846]** The typed query is not stored: `WeatherConfig.Place` is gone (a half-typed word is not
  a place and must not title the widget).
- **[S-2847]** Debug renders: `--editbar-png` opens the location searches and types into them
  against a fixture geocoder (`PlaceRender.UseFixture`); nothing touches the network.
  `--place-cleared` on `--widget-png` clears the widget's place ([S-2848]): a Sun or Moon starts at
  Zurich. (The settings page's `--place-chosen`, `--place-query` and `--place-reply` went with it.)
- **[S-2848]** Clear (user requirement: a place can be cleared, not only overwritten). Every widget
  about a place implements `IPlaced` (`HasLocation`, `ClearLocation`): current conditions, forecast,
  air quality, rain, Sun and Moon. Under the location search's answers, Clear location
  (`Glyphs.Dismiss`, greyed while none is chosen) forgets the coordinates and the name (the
  location-name switch stays; Use my location goes off, [S-2850]) and saves. A cleared place is no place for every kind alike: the widget shows
  its Set up button ([S-1860]) with its view parked ([S-1881]), asks the sync for nothing, and a later
  pick starts a fresh view, so nothing fetched for the old place is drawn. No kind falls back to a
  default. Sun and Moon still start at Zurich; cleared, their coordinates are stored as `null` (always
  written, so they never read back as Zurich), their coordinate lines show nothing (their asks start
  empty), and a place typed into the coordinate lines counts as chosen (named by its coordinates
  when it has no name).
- **[S-2849]** *(Retired by [S-3590]: the Location name switch set the header's title a second time,
  beside the Header row's Title; it is gone with `HideLocation`, and with it what a widget's lines knew
  of their card, `MenuCard`.)*
- **[S-2850]** **Use my location** (user request 2026-10-19), a switch every widget about a place
  has (`IPlaced.UseDeviceLocation`: current conditions, forecast, air quality, rain, Sun, Moon), on
  the first level right before the Location line (`DevicePlaceRows`, Windows' location glyph): on,
  the place is wherever Windows says the device is, and the Location search is not there (one source
  of the place at a time, [S-3718]); off again, the place chosen by hand, kept meanwhile, is back.
  The config stores the switch only, never the device's coordinates; it round-trips, a missing one
  reads as off. Setup ([S-1862]): a widget with the switch on is set up, place or not; Clear
  location turns it off with the place.
- **[S-2851]** One host service (`DeviceLocationService`, `Windows.Devices.Geolocation`) runs only
  while some view holds a widget with the switch on (`DeviceLocationInterest.Wanted`) and stops,
  forgetting the position, when none does. On start it asks for access (`RequestAccessAsync`,
  Windows' own prompt where it has one), reads one position (Windows' fix if under 30 min old, else a
  fresh one within 30 s), then hears only the locator's events: a move of 1 km or more and its status
  (location turned off or on in Settings). No timer, no polling. The state (`DeviceLocationRules`,
  pure): Off; Locating; Located; **Denied** (access refused, or location turned off: the menu says
  "Location is turned off in Windows settings" as a note under the switch and offers **Location
  settings**, which opens `ms-settings:privacy-location`; turned back on, it looks again by itself);
  **Unavailable** (no position to give, a failure: "Windows can't find your location right now"); a
  position held stays through a passing failure. While locating the note says "Finding your
  location…". A change is applied on the UI thread and is a state change for the cards, the sync and
  the menu. The manifest declares `location` ([S-1061]). A debug render never starts it.
- **[S-2852]** Privacy: a position is rounded to 0.01° (about 1 km) the moment it is heard, and only
  the rounded one is held, drawn, asked for or cached; a new one replaces it only after a move of
  2 km or more from it (so a reading wobbling across a rounding edge moves nothing). No coordinate is
  logged at any precision: the log says states only. Nothing is written to disk but the weather the
  sync keeps for the rounded place, as for any place.
- **[S-2853]** The widget reads a copy of its config at the device's position (`DevicePlaces.Resolve`,
  `IPlaced.AtDevice`): its view, its header's title ("My location") and its sync wants (`SyncInterests.From(state, device)`) alike, so no view
  knows where its coordinates came from; the stored config is never written by it. The name is "My
  location": the geocoder answers names, not places, so there is no reverse lookup.
- **[S-2854]** While the switch is on and Windows gives no position, the card's body is the widget's
  wait (`DevicePlaces.Waits`): the Status block's row at the top ([S-6714]), Windows' location glyph
  and the sentence of the state (finding it,
  turned off in Windows settings, not available), the view parked as under Set up ([S-1881]), so
  nothing is fetched or drawn for a place it does not have and no other place stands in.
- **[S-2855]** Debug renders: `--editbar-png` draws `props-*-device-off`, `-located`, `-locating`,
  `-denied`, `-unavailable` and `-sun` over a stand-in state (`AppServices.DebugDeviceLocation`);
  `--device locating|denied|unavailable|located` on `--widget-png` turns the switch on over one
  (located: Zurich). Neither asks Windows.

Needs a live pass (Use my location): Windows' permission prompt in the packaged build and the
unpackaged one; location turned off for the device and for Themia in Settings, then on again while
Themia runs; a laptop moved more than 2 km (or a changed Wi-Fi place); no Wi-Fi to place it.
- **[S-733]** Reading glyphs, checked against the font: droplet EB42, thermometer E9CA, wind E7BC;
  layouts: list E8FD, columns E8C0, grid E8A9.

Not ported: wind in mph for °F (the original had none either); a gradient fill on the range bar
(solid accent, as every other bar in the app); the refresh stepper (fixed choices app-wide).

## Tasks: using the whole tile, and the rest of the original

- **[S-780]** Tasks ladder (the kit's check look, `KitChecks.Plan`, [S-7037], from the card's `WidgetArea`) on the family's
  rows ([S-6711]): one task always; rows of 28, or of 40 with the list's name under Today from a
  regular tile ([S-789]), never taller for a long title (one line, the tooltip has it whole); the
  add footer ([S-3850], one row of 28) from Regular height, and at any size under an empty list
  (never while a filter narrows the list, [S-611]); the list takes the rest (no summary strip,
  [S-783]). The header's list picker ([S-614]) is the kit header's button, folded by the header.
  The due is the row's value: none on an XS tile (both axes Compact), only Overdue/Today in a
  Compact-wide column, the short label otherwise, label plus date ("Overdue · 2 Oct", "Thu 8 Oct")
  from Expanded × Expanded. An open overdue task's check rings in the critical colour, its due too.
  With no task account the Set up row says "Add an account to show your tasks" ([S-1862]).
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
- **[S-786]** Drag a row to reorder it (classic S-54): a press arms a drag after 5px (never on the
  marker, never while an editor is open); the lifted row follows the pointer and the rows it passes
  make way (into a slot over its middle half, within its own section); the list scrolls a little when
  the pointer nears an edge; letting go commits once through the same write as Alt+Shift+Up / Down; a lost
  capture puts the rows back. In edit mode the card captures the press and moves the widget instead.
  Redraws wait for the drop.
- **[S-787]** Keyboard (classic S-33..S-37, S-50 F2): a click in the widget takes the shell's
  keyboard lease ([S-808]) and a click on a row focuses it; Up/Down/Home/End move without wrapping,
  Enter renames (the kit's primary action, [S-616]), Space ticks, Delete deletes, F2 renames, Escape (and the lease ending) gives the keyboard back; the
  focused row wears the kit's ring ([S-1288], laid over the row so it moves nothing; it was the
  system focus rect); a redraw keeps the focus on the same task, or the row now in its place.
- **[S-1981]** Space ticks the focused task, and Page Up / Page Down jump ten rows (`KitKeys.Decide`,
  clamped; from no row, the first). All widget keys: Widget keys (WIDGET-KEYS).
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

## Colours, fonts and uninstall (the settings window, retired)

What the settings window (S-1540..S-1559, drawn as Windows 11's Settings app) held that lives on in
the menus: the colour submenu and the eyedropper, the font picker, the uninstall. The window itself
is gone ([S-3585]). Owning files: `Shell/ColorChoice.cs`, `Interop/ScreenColorPicker.cs`,
`Shell/FontPicker.cs`, `Services/Uninstaller.cs`, `Services/DataPurge.cs`, `Core/Settings/UninstallPlan.cs`
(tests: `ColorPresetTests`, `UninstallPlanTests`).

- **[S-1540]** *(Retired by [S-3585]: there are no settings pages. Still true of the app-layout
  editor's window ([S-1411]): its rows are the Community Toolkit `SettingsCard`, built only through
  `FluentLayout`, with BodyStrong section headings 30 above / 6 below, Mica behind it, and brushes from
  App.xaml styles with ThemeResource, so they follow the window's own light/dark choice.)*
- **[S-1541]** *(Retired by [S-3585]: there is no settings window, so no a page's title.)*
- **[S-1542]** *(Retired by [S-3585]: there is no settings window, so no "Find a setting".)*
- **[S-1543]** *(Retired by [S-3585]: there is no settings window, so no the search's matching.)*
- **[S-1544]** *(Retired by [S-3585]: there is no settings window, so no the search's ranking.)*
- **[S-1545]** *(Retired by [S-3585]: there is no settings window, so no the search's suggestions.)*
- **[S-1546]** *(Retired by [S-3585]: there is no settings window, so no landing on a search result.)*
- **[S-1547]** *(Retired by [S-3585]: there is no settings window, so no back and forward through pages (`NavHistory`).)*
- **[S-1548]** *(Retired by [S-3585]: there is no settings window, so no the `--settings-png` render.)*
- **[S-1549]** Colours are never typed. Every colour is the menus' one colour level ([S-3724]):
  "use the default" (where empty means follow the theme), the preset swatches in rows of 8 (Windows'
  48 accent colours by default; a neutral row first for surfaces; a setting may give its own set),
  and "Custom colour" opening WinUI's `ColorPicker` in a dialog with no text entry (`ColorChoice`).
  The chosen preset carries a check. (The settings window's swatch button, `ColorPickerRow`, went
  with its last colour row, a view's, [S-3542].)
- **[S-1550]** Stored format stays `#RRGGBB` (empty = default). Replaced: Appearance's accent,
  widget background and border colour; the calendar colours (Automatic + the six + custom); the
  palette editor's entries (rgb()/hsl() entries open on their colour, are stored as hex once
  changed); the scratchpad ink (its six inks, swatches of the palette above, [S-1497], + custom).
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
- **[S-1551]** *(Retired by [S-3585]: there is no settings window, so no the navigation pane.)*
- **[S-1552]** ⚙ › About Themia ends with Uninstall Themia › ([S-3582]). Packaged (has a package
  identity): Themia removes its own package with `PackageManager.RemovePackageAsync` — no capability
  is needed (full trust, medium integrity, own publisher); if that fails, Settings' page for the app
  (`ms-settings:appsfeatures-app`) opens. Not packaged: Installed apps (`ms-settings:appsfeatures`)
  opens and Themia quits.
- **[S-1553]** (Loose builds; packaged see [S-1070].) Uninstall Themia › offers, unticked, "Also delete my data" ([S-3582]; ticked, "Your widgets, settings and accounts are deleted too" under it);
  ticked, it names the folders: `%APPDATA%\Themia` and `%LOCALAPPDATA%\Themia` (`AppStateStore.FolderName`),
  and nothing else — a missing/relative root or a name that is not a plain folder name deletes
  nothing. They go only after Themia has closed and saved: packaged, in-process just before the
  package is removed; loose, by `Themia.exe --purge-data <pid>`, which waits on the app's and its
  children's process handles, works the folders out again itself, and does nothing unless the id
  is a running copy of the same executable.
- **[S-1554]** The goodbye survey `https://themia.app/goodbye?v=<version>` (escaped; "unknown"
  without one) opens on a confirmed uninstall, as the original installer did on an interactive one.
- **[S-1555]** Nothing is removed or deleted without the answer to Uninstall…'s question; no question, no
  uninstall; a debug render's stand-in host never uninstalls.
- **[S-1720]** *(Retired by [S-3585]: there is no settings window, so no an open page catching up with changes (`SettingsPageSync`); the menus follow the state in place, [S-3531].)*
- **[S-1721]** *(Retired by [S-3585]: there is no settings window, so no a page's signature (`SettingsPageSync`).)*
- **[S-1722]** *(Retired by [S-3585]: there is no settings window, so no the app pages.)*
- **[S-1723]** Every font setting picks through one picker, `FontPicker`: the settings menu's Font
  submenu ([S-3528]), a search box over a virtualised list of the presets, then the installed families
  ([S-827]), each entry drawn in the font it names (a preset in the family it maps to, [S-826]; the
  system entry in the UI's own), the current one ticked and scrolled into view; under it "Add fonts"
  ([S-6443]).
- **[S-6440]** The list (`FontSearch.Options`): the presets, then the installed families in order; a
  set family that is not installed (removed since, or the list not read yet) right after the presets;
  nothing listed twice; an implausible name adds nothing. A symbol or icon family (every GDI report
  is the symbol charset or lacks Basic Latin: Wingdings, Segoe Fluent Icons) is named in the UI font.
- **[S-6441]** The ticked entry is the one the setting names (case-insensitively), else the system font.
- **[S-6442]** Search (`FontSearch.Filter`): case- and accent-insensitive, the query anywhere in the
  name (also with spaces left out: "segoeui"); names starting with it first, then a word starting
  with it, then the rest, each in list order. The system entry stays on top whatever is typed. Keys:
  typing on the list goes to the box; Down enters the
  list, Up from its first row returns; Enter in the box takes the best match (`FontSearch.Best`: the
  system entry only if its own name matched; nothing for a blank query or no match, which says so),
  Enter on a row takes it; Escape closes the Font submenu.
- **[S-6443]** "Add fonts" opens Windows Settings › Personalization › Fonts (`ms-settings:fonts`,
  through `ShellLauncher.OpenWindowsSettings`). `WM_FONTCHANGE` on the command window drops the
  cached family list: an open picker re-reads it and keeps its query, a later one reads the new set.
- **[S-1724]** No developer mode and nothing behind one: no `DevMode` or `VerboseLogging`
  preference, no Logs page, no developer banner, no `--dev` render switch, and no Web element
  widget (the original's pre-release widget behind developer mode: its view, picker window,
  off-screen WebView2 renderer, saved renders, settings, keys and fixture are gone). The debug-only
  render flags (`--editbar-png`, `--picker-png`, ...) are build tooling and stay.
- Not ported / different: karpo's status bar (removed by user request). The SDK's `TitleBar` and
  `BreadcrumbBar` controls are not used (the first crashed an earlier build; the second needs a WinRT
  collection) — equivalents are built from a grid and text blocks.

## Meter framework and memory

The six meters (CPU, memory, disk, battery, network, ping) share one view, `MeterView` (the SDK's,
`Themia.Sdk.WinUI`), and differ only in a `MeterPart` per kind, each in its module project
(`src/Widgets/Themia.Widget.<Kind>/<Kind>Meter.cs`): where the number comes from, what the chart
plots and how it is coloured. Layout is the SDK's `MeterFace` (tests `MeterFaceTests`).

- **[S-1120]** Every meter reads on the system hub's one cadence ([S-2503]): every second, a ping every
  five ([S-2512]). How often is not a setting and not a config field; a stored `intervalSeconds` is
  not read. CPU, memory, disk, network, battery and GPU tiles side by side move on the same second.
- **[S-1121]** Every meter has one timing setting, its history length (`GraphSpanSeconds`: 1, 5, 15 or
  30 min or 1 h; load snaps anything else to the nearest). The chart holds span / spacing points
  (`MeterTiming.Points`; 3600 for an hour, 720 for an hour of probes). A chart with fewer
  2-DIP slots than that folds neighbouring points into one slot (`MeterTiming.BucketSize`, `Fold`):
  the mean on a share chart, the peak on a rate or latency chart, so the whole span is always on
  screen and a burst is not averaged away. Slots are fixed by each point's absolute position
  (`SampleRing.Added`), so only the newest slot moves as points arrive. Defaults: CPU, memory,
  network, GPU 1 min; disk 5 min; battery and ping 15 min.
- **[S-1122]** A meter's history lines are shared (`MeterMenuLines`, on the properties menu since 2026-10-19,
  [S-3719]; no settings page): Chart span under Style in the look that draws a history, none in a look
  without one ([S-6074]); no Refresh, no interval ([S-1120]). A styled meter has no Show history: its
  graph style is the history ([S-6267]); a meter without styles would have Show history and the span
  on the first level. The kind's own lines (a drive, a host, adapters) stand on the first level,
  after Appearance; CPU, memory, battery and network have none.
- **[S-1123]** Pressure colours, as the original: usage accent < 70 %, caution 70–89 %, critical
  ≥ 90 % (CPU, memory); a battery accent > 50 %, caution 21–50 %, critical ≤ 20 %, accent while
  charging (no success green, [S-6706]; `MeterPlan.Role`). Compared in whole percent. They colour the
  chart (and the battery glyph before the reading: Caution or Critical, else secondary) — never the
  reading, which stays primary text.
- **[S-1124]** A share is charted against 100 %. A rate (network) is charted against the peak of
  the samples on screen, floor 1 KB/s — not the session's all-time peak, which let one burst
  flatten the trace for the rest of the session. Disk throughput rounds that top up ([S-1821]).
- **[S-1125]** Performance counters go through one AOT-safe `PdhQuery` (English counter paths,
  `SafeHandle`-owned query, every counter closed with it). The GPU sampler uses it too.
- **[S-1126]** The chart (`SparkBand`, on the Chart block's `ChartSeries`, [S-6717]) draws one or two
  series: the first a line at 2 in its role over a 16 % wash, the second a line at 2 in the tertiary
  role (upload, writes, kernel time). The battery draws its history when Show history is on.
- [S-1130], [S-6315], [S-6316]: moved with the Memory widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Memory/`).
- **[S-1762]** **A system meter shows its label, its reading and its history — nothing else** (user
  requirement): the card's header is the label (a drive's, a host's or an adapter's name where the kind
  knows one), the reading is the number (the battery's glyph before it, [S-1187]), the history is the
  chart. No caption, detail lines, stat tiles, scale line, usage / composition bar, per-core strip
  or breakdown list. Why a reading is unavailable is its tooltip. The face is built from the family's
  blocks (`MeterView`, rules `MeterPlan`, tests `MeterPlanTests`): the reading is a Readout at the
  view's top, start-aligned in the 12 inset, on the step its widest case takes in the area (28 at XS
  to Tall, 40 at L and XL, [S-6702]); the history runs from 8 under it to the
  card's foot ([S-1824]), shown when it is at least 24 tall. A larger tile is a longer chart, never a
  bigger number ([S-666]); with Show history off the reading stands alone at its step. This is the
  **Graph** style; the other styles of a meter ([S-6300]) draw the same reading with another part
  under it, chosen by the user, never added to the Graph style.
- **[S-1823]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1824]** The history is the card's bottom band, edge to edge (user decision 2026-10-11): no
  inset at its left, right or under it, from 8 under the reading to the card's foot
  (`MeterPlan.Band`). Only the reading keeps the content inset (16). The card's rounded corners clip
  the band. The trace is kept half a stroke inside the band's width and height (`ChartRules.Y`), so
  the newest sample's stroke is not cut in half at an edge.
- **[S-1825]** A window still filling stays right-aligned, as Task Manager draws it: the trace grows
  in from the right, one slot per sample. The shared ticker keeps whole-second cadences under a
  minute (`TickSchedule.Normalise`; it used to collapse them all to one second), so a meter set to
  5 s samples every 5 s and its chart spans its configured span.
- **[S-1826]** *(Retired by [S-6330]: the history no longer scrolls between points; it steps once per finished slot, [S-6331].)*
- **[S-1827]** *(Retired by [S-6330]: every drawing is settled, so there is no slide for a point to start.)*
- **[S-1828]** *(Retired by [S-6330]: nothing slides, whatever Windows' animation effects say; there is no Smooth value changes switch.)*
- **[S-1829]** A folded slot ([S-1121]) stands at the time of its last member and is **drawn only once
  that member has arrived**: a slot still taking points changes value with each one, and the segment
  into it would show that at the right edge. So nothing on screen changes value as points arrive, and
  a folded chart steps one slot each time a slot finishes ([S-6331]). The traces sit in a `Canvas`,
  not a `Grid`, so the shapes keep their own coordinates and the band's own clip is the only edge. A
  rate chart's top is the drawn slots' peak ([S-1124]). Debug: `--widget-png <Kind> --machine sample
  [--span s]` draws the sample machine's full history at a span ([S-1932]) (an hour folds on any tile). *(Its slide, its lag of
  a slot less one sample and `--progress` are retired by [S-6330].)*
- **[S-6323]** *(Retired by [S-6330]: readouts no longer glide between readings.)*
- **[S-6324]** *(Retired by [S-6330].)*
- **[S-6325]** *(Retired by [S-6330]: no number counts towards a reading; `Glide`, `GlideClock` are gone.)*
- **[S-6326]** *(Retired by [S-6330]: every readout jumps, whatever Windows' animation effects say.)*
- **[S-6327]** *(Retired by [S-6330]: no colour glides; `OkLab`, `ColorGlide`, `DoubleGlide` are gone.)*
- **[S-6328]** *(Retired by [S-6330]: there is nothing between two readings to draw; `--tween` is gone.)*
- **[S-6329]** *(Retired by [S-6330]: there is no Smooth value changes switch; `DesignState.SmoothValues`
  is gone, and a stored one is ignored as any unknown field is.)*
- **[S-6330]** **Live values step once per sample, they never animate** (user request 2026-10-26:
  the smooth motion cost too much CPU; it reverses [S-1826] and [S-6323]). Every history chart (the
  meters', every GPU panel's, the CPU's core graphs) and every readout that moves with the hub's
  reads — a meter's number in every style, the faces' figures, the network's two numbers, a core's
  figure, the gauge's and the ring's sweep, a bar's fill (cores, memory, disk, GPU engines), the
  battery's fill, a core tile's shade and every pressure colour — shows each reading **at once, when
  it arrives**, and stands still until the next: on the hub's one cadence ([S-2503]: every second; a
  ping every five, [S-2512]), which is not a setting ([S-1120]). Nothing runs between two readings:
  no frame callback, no timer, no storyboard or compositor animation, no setting to turn any of it
  on. The reading and its point are never delayed. Not the clock's flip, the intros or the menus'
  motion: those are a style's or a moment's, not a value's.
- **[S-6331]** A history chart is drawn **settled** (`SparkSlots`): the newest finished slot on the
  right edge, half a stroke inside it ([S-1824]), each older one a whole slot to its left, the oldest
  half a stroke from the left edge once the window is full (a young one grows in from the right,
  [S-1825]). A point that finishes a slot redraws the chart once (a slot is one point unfolded, so
  every sample; folded, every `size` samples, [S-1121]: the only interval the chart's span sets); a
  point that finishes none draws nothing (`SparkSlots.Moves`), and a band not shown draws nothing
  until it is shown again. A history loaded, a clear, a span or scale change and a resize draw it
  the same way. The band holds `SparkSlots.Lead` points beyond its span (the slot still filling and
  the oldest slot's members the span cuts off), so a whole window of finished slots is always on
  screen.
- **[S-6332]** **A read that says what a tile shows touches nothing** (user request 2026-10-26, CPU):
  the hub reads every second whether or not a figure moved, so a view compares before it writes. A
  detail tile whose new sheet says what it shows already (`DetailSheet.SameAs`: its facts and rows by
  value) skips the whole fill; a list row reused by position that shows the same row is not touched;
  a reading's tooltip and an absent tile's reason are set only when they say something else
  (`UnavailableLook.Say`); a GPU tile is laid out again only when its adapter goes or comes back (a
  new engine count lays the engines out itself), not on every snapshot; a meter's number, gauge,
  bars and tiles write only what changed ([S-6330]). The audio visualizer's capture counts a packet
  flagged silent as no sound ([S-3202], `Spectrum.Heard`), so an app holding its stream open while
  paused leaves the worker on the idle wait instead of an analysis every frame.
- **[S-6333]** **A detail tile is laid out again only when its face changes** (CPU audit 2026-10-26,
  `DetailFace.Relays`): a read with another number of facts, a caption or hero coming or going, the
  thing turning absent, a value longer than the columns were measured for, or a list emptying or
  filling plans the tile anew; a read with only another number of list rows does so only when the
  plan for it differs (a list as tall as its rows). A list that scrolls is the room's height whatever
  it holds, so Top processes at Fit (every process a row) fills its rows in place when a process
  starts or exits instead of laying the whole tile out, and the rows reused by position that show
  the same process are not touched ([S-6332]).
- **[S-6300]** **Meter styles** ([S-6000]): a meter's style says what stands under its reading
  (`MeterLook`: the history, a gauge, the kind's items as columns, rows, rings or graphs, the battery,
  a ring, the signal bars, two readings); the reading is the same Readout in every style. The parts are
  the family's Chart and Row blocks ([S-6711], [S-6717]), laid out by `MeterPlan` (tests
  `MeterPlanTests`), so a CPU gauge and a memory gauge of one size are one gauge; each stands 8 under
  the reading, inside the content inset. A kind's items come from the hub like its reading
  (`MeterPart.WatchItems`, [S-2500]): one read per tick for every widget, watched only while a style
  draws them and let go when another style is chosen ([S-6005]). Show history and Chart span stay
  the Graph style's settings. Debug: `--machine sample` draws the sample machine's reading and items ([S-1931]) in
  every style.
- **[S-6301]** An item's role: the usage pressure's ([S-1123]: the accent, Caution from 70 %, Critical
  from 90 %); an item with no reading is the disabled role, never "idle". The heat-map shades
  (`LoadTint`) are gone with the tiles ([S-6305]).
- **[S-6302]** **Gauge**: the Chart block's ring, its upper half (`ChartRing` half: from nine o'clock
  over the top to three, stroke 2, the track in the Track role, the arc the reading's share in its role,
  round caps), as wide as the content while its half and a stroke fit the height, start-aligned
  (`MeterPlan.Gauge`; under 48 the reading stands alone). The reading stands centred on the gauge's foot
  on the Readout step its widest case takes in the bowl (its width less 24 a side, its height less 8),
  capped by the area as every reading ([S-6702]).
- **[S-6303]** **Columns**: one column per item, the Chart block's 4 track standing up (`ChartBar`
  vertical, round, the track in the Track role, the fill from its foot in the item's role), each centred
  in an equal share of the content width, from 8 under the reading to the foot's inset; not drawn when
  the room is under 16 or a share under 8 (`MeterPlan.Columns`).
- **[S-6304]** **Rows**: per item a fact row ([S-6711]: the label secondary, the value primary and
  end-aligned) whose inline meter (`ChartBar`) takes all the room between them, the label at most half
  the content; whole 28 rows from 8 under the reading, the rest shed from the end (`MeterPlan.Rows`).
- **[S-6305]** **Rings** (the Grid style): one Chart ring per item (stroke 2, its share in the item's
  role), in the grid of square cells 8 apart whose ring is largest in the room under the reading and
  holds every item (16 cores as 4 × 4 or 8 × 2 by the room); not drawn when a ring would be under 16
  (`MeterPlan.Rings`).
- Not ported / different: the original's `visibleMetrics` multi-select and everything it switched
  (removed by [S-1762]). The audit's `MeterLadder` was not built: `MeterFace` and `Disclosure` are
  the ladders.

## Power and Bluetooth

Power: one widget per action (R7-POWER), replacing the port of `main`'s Power tile
(`widgets/system/power`, [S-01]..[S-11]). The SDK: `PowerActionRules`, `PowerButton`,
`PowerButtonView` (the one view of all seven, on the family's `TileBlock`) and the base `PowerButtonConfig`; App: `Win32PowerActions`
and `InertPowerActions`, behind `IWidgetHost.Power`; tests `PowerActionTests` (the module test host).
Each power button is a module of its own (`src/Widgets/Themia.Widget.Shutdown` … `.ScreenOff`, one
tag each, [S-7400]..[S-8000]), and so is Bluetooth (`src/Widgets/Themia.Widget.Bluetooth`).

- **[S-1910]** Each power action is its own widget kind and picker entry, with its own glyph, name and
  description: **Shut down** (`Shutdown`), **Restart**, **Sleep**, **Hibernate**, **Lock**, **Sign
  out** (`SignOut`) and **Turn off display** (`ScreenOff`), in the add menu's Devices & power › Power buttons ([S-3511]), default size
  120×120. The kind maps to its `PowerAction` in Core (`PowerActionRules.ForKind`/`KindOf`); one
  view (`PowerButtonView`) serves all seven. Each kind has a config of its own (`ShutdownConfig` …
  `ScreenOffConfig`, base `PowerButtonConfig`): nothing to set beyond the card's common settings and
  the style ([S-1919]), so the menu shows only those, and a power widget never needs setting up.
  There is no combined power tile, no power-state strip (source, plan, battery saver, time left), no
  power plans or timeouts, and no power-state sampling. A state file with the old `power` kind fails
  load validation like any unknown kind ([S-1050]).
- **[S-1911]** The whole widget is the button. A new power widget has no header ([S-700]); the icon is
  centred with the name under it. Hover and pressed paint the card's item layer's fills
  ([S-6513]) across the whole content area, inside the card's own fill, border and corners; a click
  is press and release on the tile (sliding off cancels). It is not a `Button`: in edit mode the
  press is left to the card, which selects and moves the widget, so a click there never runs anything
  (`IWidgetSurface.EditMode`).
- **[S-1912]** The face is the family's Tile block, ringed ([S-6715], [S-6736]): the icon centred in
  its ring's room (the step is the largest Readout or Hero step above the name's Body line and at
  most 0.6 of the width, the icon the largest family glyph size within three quarters of it), the name
  under it at every size (cut with an ellipsis where it does not fit, whole in the tooltip), no state
  line, in the card's inset of 12. The armed sentence and a failure take the name's line, so arming
  moves nothing. Room left at L and XL stays empty past the 192 step (concise first).
- **[S-1913]** Shut down, restart, sign out and hibernate arm on the first click: the icon, a draining
  ring round it and the name's line turn critical, and the line becomes "Click again to shut down"
  (one Body line, cut where it does not fit; the tooltip carries it whole); the tile itself takes no
  fill (`PowerActionRules.Tile`: the Tile's critical state, [S-6736]). The second click within 5 s
  runs it; a second click within 400 ms of arming is ignored (a double-click is one intent); the arm
  ends after 5 s, on pointer exit, on a config change or when the widget goes away. Sleep, lock and
  display-off run on one click. The window is checked by `PowerButton` against the press time. The
  ring is the Tile's (`ChartRing`: an arc from twelve, clockwise, round caps, stroke 2 over its
  track) and drains on the compositor (`ChartRing.Drain`), one linear animation of its end over the
  time left (`PowerButton.ArmLeft`), and one timer set to that time draws the widget disarmed:
  nothing runs on the UI thread while it drains (CPU audit 2026-10-26: it was a whole repaint every
  50 ms). Both are cosmetic; a click after the window arms again whatever the drawing shows. A
  render does not capture the compositor's arc: the armed render shows the ring's track only.
- **[S-1914]** Hibernate is offered only where the machine can (`IsPwrHibernateAllowed`): elsewhere
  the widget is drawn in the disabled text colour, takes no hover or press, and its tooltip says
  "Hibernation is turned off on this PC". The probe runs when the widget attaches and when the
  pointer comes onto it, so turning hibernation on shows up without polling.
- **[S-1915]** A failed action shows the warning glyph and "Could not <verb>: <reason>" in the
  critical role in the name's line (cut where it does not fit, whole in the tooltip) until the next
  press, and logs a warning ([S-1246]).
- **[S-1916]** One run at a time per widget: a press while the action is still running (sleep runs
  until the machine resumes) does nothing.
- **[S-1917]** Debug: `--widget-png <Kind> <out.png> [w h] [--theme light|dark] [--state name]`
  draws one widget in a real card on a preview host ([S-1251]), off-screen, at the seven tiers of
  `WidgetTiers` (or once at `w h`), labelled. `--state` asks a view that implements
  `IWidgetPreviewState` for an interaction state a render cannot click into — for the power widgets
  `hover`, `pressed`, `armed`, `failed`, `unavailable`. States, not sample data ([S-1863]).
  `--style tile|round` draws either style ([S-6006]). `--sizes 100x100,160x100[,tiers]` draws several
  in-between sizes on one page (with `tiers`, the seven as well).
- **[S-1931]** Debug: `--widget-png ... --machine sample` draws the system widgets (the meters, the
  details, Top processes) on the sample machine instead of this PC; without it they read this PC, as
  a developer wants. The sample machine (`SamplePc`, Core) is one ordinary good laptop: a 12-core
  processor (16 logical) at 31 %, 32 GB of memory 52 % used, a discrete GPU at 14 %, a system drive
  and a data drive, Wi-Fi at 3.4 MB/s down, a ping of 12 ms, a battery at 78 % discharging (3 h 41 min
  left), a private local address (`192.168.1.42`) and a documentation public one (`203.0.113.24`).
  It answers every `Sources` member in its own snapshot type, through the hub (`SystemHub` over a
  `SystemMachine`: `ThisPc` or the recording), so the widgets' own code and the hub's rules
  (`SeriesProjections`) draw it: no second look in any view. A recording is one moment: the hub reads
  each feed once and never ticks or listens to Windows' events over it, so two renders draw the same
  picture. Nothing in `SamplePc` reads the machine, the clock or a random source. Any render installs
  it in one line, before its cards are built: `using var machine = SampleMachine.Install(ticker,
  dispatcher);` (`Themia.App.Services.SystemData.SampleMachine`, Debug builds). The quick toggles
  draw `--state on|off` (the shared `QuickToggleView`, so a scene can show Wi-Fi on beside others
  off) and the power buttons stay inert in every render ([S-1251]). It is the one fixed picture of
  the system widgets: the meters' `history` and `sample` states and the details' `sample` state
  (a second look, a sine-wave fixture) are gone; `unavailable` stays.
- **[S-1932]** The sample's charted sources answer their whole history, oldest first, as many points
  as the hub keeps (`SeriesHistory.Capacity`), and the hub starts a series of a source with a past
  (`HubSource.Past`) with it, so every chart is full on the first frame. Each has its own shape, out
  of a fixed seed (SplitMix64, not `Random`, whose algorithm is not promised): the processor spiky,
  each core its own level and spikes with all-core bursts, the total their mean (the Core graphs read
  the same cores' history); memory in slow steps; the network idling with short blips and long
  downloads, upload a share of them; the disks in bursts, the data drive rarely; the ping jittering
  about 12 ms with a slow answer now and then and no probe lost; the GPU low with a long plateau and a
  video's rhythm at the end; the battery falling from 91 % to 78 % over the hour.
- **[S-1933]** The sample's process list is programs every Windows PC runs (Desktop Window Manager,
  Explorer, the service hosts, Search, Task Manager, Notepad, Settings ...), each with its executable
  where Windows keeps it (`ProcessSample.Image`), so Top processes draws their real icons; a read that
  knows a process's image is not asked for it by pid. Grouping keeps the image.
- **[S-1919]** Styles ([S-6000]): one `PowerButtonStyle` on the base `PowerButtonConfig`, shared by all
  seven kinds: **Tile** (the default, [S-1911], [S-1912]) and **Round button** (`round`), named,
  labelled and drawn as the toggles' Round style ([S-3917]: the Tile block's round look). The circle
  is the item layer's tile (a power button is never on, so never the accent) holding the icon in the
  primary role at the largest family glyph size within half of it, the name under it; armed, the
  icon, the ring round its rim and the sentence in the name's line are critical; failed, the warning
  glyph and the sentence critical; unavailable, every part disabled. The whole tile stays the button:
  arming, edit mode and a config change disarming it ([S-1913]) are the same in both styles.
- **[S-1918]** *(Retired: the seven power widgets are one family of the add menu, Devices & power ›
  Power buttons, [S-3511].)*
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
  card host): a power widget's backend, the host's `IWidgetHost.Power`, is `InertPowerActions`
  there, which logs and runs nothing (it still answers the hibernate probe truthfully). Only a card
  on a non-preview surface gets `Win32PowerActions` (orig. [S-08] demo mode).
- **[S-1252]** *(Retired: a power widget always has its action, [S-1910].)*
- **[S-1253]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- [S-1254], [S-1255], [S-1256], [S-1257], [S-1258], [S-1259], [S-3252], [S-3253], [S-3254], [S-3961]: moved with the Bluetooth widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Bluetooth/`).

## Peripheral batteries (DEVICE-BATTERIES)

The host's one read of every peripheral battery Windows reports, for the Device batteries widget
(`src/Widgets/Themia.Widget.DeviceBatteries`, its own `SPEC.md`) and whatever else lists one. SDK:
`Themia.Sdk.Devices` (`DeviceBatteryReading`, `DeviceBatterySnapshot`, `DeviceKinds`,
`DeviceBatteryRules`) and `IWidgetHost.DeviceBatteries` (`IDeviceBatteries`); App:
`Services/Devices/DeviceBatteryService.cs` and `DeviceBatteryInventory.cs`. Tests:
`DeviceBatteryRulesTests` (Core).

- **[S-4470]** One source for every widget instance (`IDeviceBatteries`: the last snapshot, Busy,
  Watch, Refresh), reading only while a widget watches. A snapshot is every reading of every path,
  unfolded: the paired Bluetooth devices that are **connected**, with the level Windows holds
  (`DEVPKEY_Bluetooth_Battery` on a node of their container, [S-3252]) or else, within an 8 s budget
  per pass, the GATT Battery Service of an LE device; every device container that carries
  `System.Devices.BatteryLife` (with `System.Devices.ChargingState`) and is not disconnected: Windows'
  devices list, any bus, which is where a USB receiver's mouse or keyboard appears when its driver
  reports a level; every game controller with a battery report (`Windows.Gaming.Input`: Xbox
  controllers wired, on the wireless adapter or on Bluetooth; charging from its status). Windows
  reads no other HID battery (Logitech's Unifying and Bolt receivers speak HID++), so those devices
  are not listed. No WMI. A failed path is logged and the others still answer; a failed read is the
  Failed state with no readings.
- **[S-4471]** The readings' rules (`DeviceBatteryRules`, `DeviceKinds`): a level outside 0..100 or
  not a whole number is none; charging is a 0 or 1 (or a boolean), anything else no answer; a
  controller's level is its remaining over its full capacity, rounded, a remaining one over the full
  one full, either missing none; a container id is Windows' braced GUID, the empty one none; the kind
  is Windows' category read part by part (a paired device's own, `System.Devices.Aep.Category`:
  "Communication.Headset.Bluetooth", else its container's: "Input.Mouse", "Audio.Headphone"; audio
  gives way to a more specific part), else the name's guess ([S-240]).
- **[S-4472]** Events, not polling, where Windows gives them: device watchers on the paired Classic
  and LE devices (their connection), on the containers that carry a level, and on the device nodes
  of the connected Bluetooth containers (rebuilt as that set changes), and the game controllers
  arriving and leaving. Only what comes after a watcher's first enumeration asks for a read; such a
  read takes what Windows holds and keeps the GATT levels of the last pass for devices still connected.
  Reads are single-flight: one asked for while one is out follows it.
- **[S-4473]** What Windows sends no event for (a GATT level, a controller's report) is read again
  every 2 minutes on the shared ticker while a widget watches, and on Refresh. The last widget
  letting go stops the watchers and cancels a read that is out.

## Habits

Code: the host's `Services/Habits/HabitPool.cs` (behind `IWidgetHost.Habits` and `SettingsHost.Habits`),
Core's `Habits/HabitListStore.cs`, the SDK's `Habits/HabitLists.cs`, `Widgets/HabitPeriods.cs`,
`Widgets/HabitRules.cs`. Tests: `HabitRulesTests`, `HabitListsTests`, `LocalStoreFixtureTests`, the
module host's `HabitHostTests` (S1420/S1422). The widget is a module of its own
(`src/Widgets/Themia.Widget.HabitTracker`, its SPEC.md).
Ports the original `widgets/others/habit-tracker/SPEC.md`.

- **[S-1420]** Habits are device-wide **lists** (original S-18), not per-widget data. The pool lives
  in `habits.json` beside the state file: `schemaVersion` envelope, every list/habit/key validated
  (malformed entries dropped and counted, duplicate ids keep the first), a corrupt or newer file set
  aside and never overwritten, an unreadable one leaves the pool read-only. Saves are atomic (temp +
  move), single-writer (one gate) and coalesced (one more write of the latest snapshot); the UI
  thread is the only mutator and every change goes through `HabitPool.Update` (`IHabitLists.Update`). A habit owns its
  log, so deleting it takes its history (original S-05/S-20 by construction). The widget config
  holds only `ListId` plus its own settings; with none chosen it takes the first list, creating
  one only when there is none; a deleted list is not replaced ("pick a list"). Two widgets on one
  list see each other's ticks at once (`HabitPool.Changed`). The header title is the list's name.
- **[S-1421]** Only the check ticks a habit (original S-02); name and run never do. The check is
  the habit's Row block lead ([S-6711]), drawn as the kit's check look draws its own (not a stock
  `CheckBox`, whose template clipped its glyph to a dash in both states): a 16 ring of stroke 2 in
  the secondary role when not done, the accent's filled circle with the 16 tick when done. A tap
  reads the clock once and toggles that period; un-ticking removes every copy of the key.
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
- [S-1425], [S-1427], [S-1428], [S-1982], [S-1429], [S-6408], [S-6409], [S-6410], [S-1430]: moved with the Habit tracker widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.HabitTracker/`).
- **[S-1426]** History dots (0–31, default 7): oldest left, the current period right, from the same
  step as the streak; each dot's tooltip names its period in the cadence's units ("Week of Monday
  28 Sep 2026", "14:00, …", "September 2026"). A dot is 8 and round, 4 from the next, done in the
  accent and open in the track role (the Chart block's two, [S-6717]); they are the habit row's
  inline meter ([S-6711]). A narrow tile shows fewer, dropping the oldest, and fewer than three none;
  the name never yields to them.
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
  100 px cells (classic S-44); a chip line is a Caption line and 4 under it at every size; only the
  weeks holding a day of the month are drawn (4–6 rows), all of them where every week's number line
  fits, else the weeks that fit around today ([S-2702]).
- **[S-1266]** *(Retired: 32/40 px hours on a scrolled timeline, [S-2705].)* A timeline block of three
  caption lines adds a "location · organizer" line; one of a single line keeps the title only.
- **[S-1267]** Agenda: under 128 tall the first page's "Today" heading is dropped (any other day keeps
  its heading); at ≥720 px each page fills two columns, one after the other.
- **[S-1268]** Opening an event: a Microsoft account's event opens `ms-outlook://events/{id}` when
  Outlook's protocol is registered (one cached check shared with Mail, `ShellLauncher.OutlookInstalled`),
  else the provider's https page; the provider comes from the event's account, never the link's
  domain. Only https (no user info) and `ms-outlook://events/<percent-encoded>` pass
  `CalendarLinks.IsLaunchable`, re-checked by `ShellLauncher.OpenCalendarLink`.
- **[S-1984]** *(Retired: Page Up / Page Down page every layout, [S-2710].)*
- **[S-2701]** The view reads its data through one seam, `ICalendarData.LoadAsync(config, first,
  last)`, for exactly the range its layout covers ([S-568]): the live one wraps the shared office
  source; the render's fixture is another ([S-2711]).
- **[S-2702]** Month (`CalendarFit.Month`), inside the card's 12 inset: weekday header as names (≥64 px
  columns), two-letter initials (≥22), one letter (≥10), dropped where it and every week's number line
  (a Caption line, never squeezed) do not fit; a box too short for every week shows the weeks that fit,
  today's among them (`FirstRow`); then cells of chips (40 wide, holding the 4 padding, the number and a
  chip line), dots (the number and a dot) or the number alone, with its dots beside it from 48 px
  columns (Wide). A column too narrow for a two-digit number (`CalendarFit.HoldsDays`: XS) shows no
  grid: the day the view is on as a Readout, its weekday under it (a week does the same). XS: the day;
  S: every week's numbers; M: dots under initials; Tall: dots; L: two chip lines; XL: three, with times.
- **[S-2703]** Bars (`CalendarFit.Bars`): in a month row every item is a one-line chip, and in the
  week's band every all-day or multi-day event and birthday; an event over several days is one bar
  across them, squared off where it runs past the row. Lines are packed from the first column, the
  longest first; what a day's lines cannot hold is its "+N". A timed event ending at midnight is
  one day.
- **[S-2704]** Week (`CalendarFit.Week`): its day columns ([S-2719]; the widths below are a column's)
  under weekday and number (never without one letter of the weekday); a band of bars (up to two lines, three on the hour grid, always leaving a line for
  the body); then, 280 px tall, an hour grid with each day's timed events in overlap lanes; else each
  column's timed events as chips (time over title), then "+N". All of this from 96 px columns only:
  narrower, a column holds marks and the chosen day's events are rows under the week (the widget's
  SPEC, [S-2281]..[S-2285]).
- **[S-2705]** Hours (`CalendarFit.Hours`): the day wanted is 07–22, widened to every timed event of
  the day(s) drawn. When it fits at ≥20 px an hour it is drawn whole, a tall box widening it toward
  midnight rather than stretch an hour past 48 px; else as many whole hours as fit, starting an hour
  before the focus (today: the next event still to end, else now; another day: its first event, else 08:00). Hours fill
  the box exactly; hour labels read "07:00", or "07" in a day under 160 px. Today has an accent
  now-line. Events outside the window are on the day's hover.
- **[S-2707]** Year (`CalendarFit.Year`): twelve small months in the arrangement of 4×3, 3×4, 6×2,
  2×6, 12×1 or 1×12 with the largest day cell, preferring cells that hold numbers (a caption line)
  and then a weekday row; under 4 px it falls back to 6, 4, 3, 2 or 1 months around the current one
  (`FirstMonth`, inside the year). The current month's name is primary, the others secondary (never
  the accent). Number cells: today the accent disc, a day with something a strong number over a
  2 px stripe of its first item's colour, the selected day ringed (a hairline). Square cells: filled
  with that colour, today ringed. Every day's items on hover. XS shows two months, the rest twelve.
- **[S-2709]** Agenda pages (`CalendarFit.AgendaPages`): whole rows of one height per render (a
  wrapped title's second line aside, [S-2717]), a day's heading before its first row and again atop a
  column that starts mid-day; pages fill the columns in turn and always advance. Back / forward show
  only where there is a page; Home is the first.
- **[S-2710]** Key map (`CalendarFit.Key`), every layout: Left / Right and Page Up / Page Down step
  the layout's unit (month, week, day, year, agenda page); Up / Down the cursor day a week (month,
  week); Home today; Enter the cursor day's first event (the agenda page's first; none in the year).
  The header's arrows hide on a Compact-width tile (the keys still page).
- **[S-2711]** *(Retired: one widget with five layout states; each calendar style has its own renders, [S-3606].)*
- **[S-2716]** Agenda day headings (`CalendarFit.AgendaHeading`): today and tomorrow are "Today" and
  "Tomorrow", any other day its date ("Tue, Oct 6"). Where the heading line holds the longest
  dated heading the language writes (every weekday and month, `AgendaHeadingDated`), the two named
  days carry their date too ("Today · Sun, Oct 4"); else the word alone (concise first), so the
  date never comes and goes with the day of the week.
- **[S-2717]** Long titles wrap: a week column's chip and a timeline block take a second line where a
  title runs past its one (word wrap, an ellipsis after the second). A list is fitted at one title
  line a row first, so a wrap never costs an event its place; what is left under the column (less
  the "+N" line) goes to the wrapping titles in order, one line each (`Disclosure.SecondLines`). A
  timeline block spends its whole caption lines on the title, its time, the title's second line,
  then where and who (`CalendarFit.BlockParts`). Month and band chips and the agenda's rows stay one
  line ([S-7036]).
- **[S-2718]** The day's all-day band (`CalendarFit.DayBand`): each all-day or multi-day item and
  birthday is a full-width span bar a body line tall (with room around it), up to three, as long as
  the timeline keeps three hours. Its colour runs along the leading edge; an item that began before
  the day or runs past it is squared off with a chevron on that side, and from 200 px names its days
  ("Oct 6 – Oct 9"). Items past the bars share the last line ([S-2706]); a box too short for one bar
  keeps the one shared chip line, and one too short for that has no band (the hover lists them).
- **[S-2719]** Week days (`CalendarRules.WeekFirst`, `WeekStep`): the week shows `weekDays` columns
  ([S-3637]) from the first day of the week or from today. Its periods are laid end to end from
  today (from today) or from the start of today's week, one step apart: its own length from today,
  else whole weeks (5 days step Monday to Monday, 14 days two weeks). The period on screen is the
  one holding the anchor, so following today keeps the next days rolling, a day selected inside the
  period moves nothing and one outside pages to its period ([S-3621]). Columns share the width
  (`CalendarFit.Week(width, height, days)`), so fewer days are roomier columns; the band, lists and
  hour grid are the same layout for any count.
- **[S-1270]** Contacts list rows are Row blocks ([S-6711], `ContactsPlan.Of`): one, two or three
  lines by the tier ([S-6712], `RowRules.Lines` on the view's area): the name primary; then the role
  (else the address, else the number) secondary; then the address (else the number) tertiary. Every
  row of a render is one height (28, 40, 56). Rows flow into columns of 240 px (up to 4), row-major;
  the 32 monogram leads from 180 px columns; the rows that fit whole in the view's area less the
  card's bottom inset and the search box show, the rest a scroll away under the fade ([S-1281]).
- **[S-1271]** Compact rows are one line: the name and, from 240 px columns, the one field (address,
  else company, else number) as the row's value, secondary and end-aligned.
- **[S-1273]** Grid cells are the family's `Row.Cell` ([S-6713]): the monogram (32, 48 from an
  expanded width) over the name in Caption, each the icon and 24 either side wide, 12 apart, whole
  lines only.
- **[S-1274]** *(Retired: no detail pane beside the list on an XL tile. The list keeps the whole
  width; a person's actions (Send email, Call, Message, Copy …) are the row menu's, [S-1277].)*
- **[S-1275]** Search box: always from 220 px tall; from 128 a header search button (one of the kit's
  24 header buttons, [S-7064]) opens it (it folds away again when left empty); below 128 none. A
  typed query keeps the box open. The box is one control of 32 at the card's inset, 8 over the list.
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

## Calendar: five styles and shared dates (CAL-SPLIT)

The calendar's layouts are its five styles, and the calendars on one view share the day selected and
the event pointed at. Configs and their descriptors in Core's `Model/Configs/OfficeConfigs.cs`;
the sharing rules in `Office/CalendarShare.cs`; provider reading in `Office/OfficeWire.cs`,
`Office/CalendarRules.cs`, `Office/IcsEvents.cs`. One view, `CalendarView*.cs` (`CalendarView.Links.cs`
for the links), one set of menu lines, the SDK's `OfficeMenuLines`. Tests `OfficeConfigTests`
(`S36xx_`), `CalendarDaysTests`, `CalendarShareTests`, `CalendarProviderTests`.

- **[S-3600]** *(Retired: five widget kinds, one per layout; they are one kind with five styles,
  [S-3640].)*
- **[S-3601]** One set of menu lines for every calendar widget (`OfficeMenuLines`: the calendar, Next
  meeting, the week planner): accounts and their calendars with colours ([S-562], [S-566], [S-567]) and birthdays,
  plus only what the calendar's style uses ([S-3641]). No calendar stores a week start ([S-564]).
- **[S-3602]** One data seam and one sync for every style ([S-2701], [S-588]): two calendar widgets on
  a desktop ask the same shared source and store, never their own.
- **[S-3636]** The agenda's date range (`CalendarWidgetConfig.AgendaDays`, 1–365, 30): how many days it
  covers from today, today included; it replaces the fixed 30 days in the one range path
  (`CalendarRules.ViewRange`). The menu offers Today, Today and tomorrow, 3, 7 and 30 days, and
  Custom…, which asks for a number of days of its own; the length (`agendaCount`) still caps the entries within
  it. A value out of range is brought inside it at load and logged.
- **[S-3637]** The week's own days (`CalendarWidgetConfig`): `weekDays` (1–14, 7) and `weekFrom`
  (`week`, the regional first day of the week, or `today`); a working week is 5 from the week
  start, the next days any count from today ([S-2719]). Settings: "Starts on" (First day of the week
  (Monday) or Today) and "Days shown". It overrides the regional week start for this widget only and
  stores no `weekStart`; out-of-range or unknown values fall back at load (days clamped, an unknown
  first day the week start).
- **[S-3640]** One kind, **Calendar** (`Calendar`, 71, `CalendarWidgetConfig`, discriminator
  `calendar`, 340×260, glyph E787, Popular and Calendar & mail's first row), with five styles
  ([S-6000]; `CalendarStyle`, stored as `"style"`, menu order): **Month** (the default, E787),
  **Week** (E8C0), **Day** (E8BF), **Year** (EA89) and **Agenda** (E8FD). Month calendar, Week
  calendar, Day calendar, Year calendar and Agenda were five kinds (71..75); a state file holding one
  drops that widget at load like any unknown kind ([S-1050]). The style is the layout
  (`CalendarConfig.Layout`): each style keeps its own size ladder ([S-2702]..[S-2709]), and the
  range it loads (`CalendarRules.ViewRange`), its paging unit (`Step`), its title and its keys
  (`CalendarFit.Key`) follow it. A style change re-draws from the data in hand, loads the new range
  when it differs, starts the agenda on its first page, anchors a dated style on the selected day
  ([S-3621]) and publishes the new range (an agenda clears its own, [S-3625]); the selected day and
  the hovered event keep working across calendars of any styles on a view ([S-3620]..[S-3624]).
- **[S-3641]** The config holds what every style shares and the union of the styles' own fields
  (`weekDays`, `weekFrom`, `agendaCount`, `agendaDays`); a style reads only its own
  (`Period`: a month, day or year is the week start alone). The menu shows only the
  current style's rows (`CalendarWidgetConfig.ShownSettings`, [S-6070]): Week: "Starts on" and "Days shown";
  Agenda: its date range and "Agenda events"; Month, Day, Year: none; then birthdays for every style.
  Picking another style rebuilds the page ([S-6071]).

Provider events ([S-560], [S-565]), as real calendars send them:

- **[S-3610]** A Graph change round (`calendarView/delta`) can name an event without sending it: an
  item without `isAllDay` (and so without its subject) is a change notice, not an event
  (`OfficeWire.FromGraph` gives none). `OfficeWire.SortGraphChanges` sorts a round into whole events,
  ids that left (removed, or cancelled) and notices; each notice is read again by id
  (`me/events/{id}` with the calendar view's `$select` and `Prefer: outlook.timezone="UTC"`; a 404
  leaves), and more than 25 read the window whole instead. A failure there fails the round, so its
  cursor never moves past an unread change. Folding a notice in had blanked the title ("No title")
  and made an all-day event a timed one from UTC midnight to UTC midnight: two local days east of UTC.
- **[S-3611]** All-day events are dates, never instants: Graph's `isAllDay` dates and Google's
  `start.date` / `end.date` are read by `CalendarRules.ParseAllDayDate` — a bare date or a midnight as
  written; a date-time that is not a midnight (the event's own midnight written in UTC, "30 Sep
  22:00" for 1 Oct in Zurich) is the nearest midnight. The end is exclusive: a one-day event covers
  exactly its day in every zone, and a stored one still does.
- **[S-3612]** iCalendar: a `VALUE=DATE` event covers [DTSTART, DTEND); Outlook's
  `X-MICROSOFT-CDO-ALLDAYEVENT:TRUE` (or `-MSNCALENDAR-`) on wall-clock midnights is a date too, never
  read through its TZID; a folded, escaped SUMMARY reads whole. An event with no title is untitled
  (empty), for the widget to word in the user's language ("No title"), never an English placeholder.

Shared dates and events on a view ([S-3300], [S-3311]):

- **[S-3620]** A click on a day in the month (its cell), the week (its column head) or the year (its
  day) selects it on the view (`SelectedDate`); a click on the selected day lets it go
  (`CalendarShare.Toggle`), and so does Escape. The selected day is outlined in the month (with chips: its
  number ringed and its cell filled, so no outline crosses a chip spanning it), underlined in the
  week, ringed in the year (a day with something keeps its stripe inside the ring), its heading in the accent in the agenda. A selection stays when the
  widget pages and when the view is switched away and back; nothing is persisted.
- **[S-3621]** Every calendar on the view follows the selected day: the month, week, day and year
  anchor on it (`CalendarShare.AnchorFor`), paging only when it is in another period, so the day
  calendar shows the day picked in the month or the year. A widget that joins a view reads the
  selection there at once.
- **[S-3622]** The agenda keeps its own range (its date range from today, [S-3636]) and turns to the page
  holding the selected day: the page with the first row on or after it (`CalendarShare.AgendaPageOf`);
  a day after every row moves nothing.
- **[S-3623]** The event under the pointer is shared (`HoveredEvent`, `EventLink(account, calendar,
  id)`, a hover): while it is pointed at in one calendar, every other calendar on the view dims its
  other events to 35 % (`CalendarShare.Dimmed`), without a redraw; the same provider id in another
  account or calendar is another event. The hover goes when the pointer leaves, the chip is redrawn
  or its widget leaves ([S-3304]).
- **[S-3624]** The keys' cursor day is the selected day: Up / Down move it a week (from the selection
  when it is on screen, else from the anchor, `CalendarShare.Step`) and publish it, so the other
  calendars follow as for a click; Enter opens its first event.

## Mail: the whole tile

- **[S-1281]** Bottom scroll fade, shared (`Widgets/ScrollFade` + Core `ListFade`): while the list
  continues below the viewport, rows within 48px of its bottom edge fade to 0.35 opacity (the Tasks
  depth and floor, [S-781]); scrolled to the end every row is whole. Opacity per row, never a
  gradient over the list (the card can be transparent). Event-driven: scroll, size, one layout pass
  after a rebuild. Used by Mail.
- **[S-1285]** *(Retired: no folder summary ("4 unread of 1,661 in Inbox") under a large tile's
  list; the large tile shows more messages instead. The header badge keeps the unread count.)*
- **[S-1288]** List keyboard navigation, shared (Core `ListKeys`, App `RowFocus`): a click focuses a
  row (an inset 1.5px accent ring every row reserves, so nothing moves); Up/Down step, Page Up/Down
  jump 10, Home/End go to the ends, Enter opens, Delete deletes (Mail, where the account allows),
  F2 renames (lists that rename), Escape lets go. With no row focused a movement key lands on an end.
  The focus follows its item across redraws and moves to the row that took its place when the item
  left. Keys reach a view only once the shell's keyboard lease (WP11) makes the surface take the
  keyboard; the views' `KeyDown` handlers are in place for it. Clearing the focus on a click outside
  or on app blur is the lease's job.
- **[S-1295]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*

Not ported, deliberately: main's low-level keyboard hook for list focus (the lease replaces it).

## Clipboard and Countdown: the whole tile

Code: the SDK's `ItemMenu.cs` (submenus), `ClipboardHistory.cs` and `ItemCursor.cs`, Core's
`ClipboardInterest.cs`, the host's `ClipboardHistoryService.cs`. Tests: `TrackersLayoutTests` (S1457),
`ClipboardHistoryTests`, the module host's `ClipboardHostTests` (S1458). The clipboard and the countdown
are modules of their own (`src/Widgets/Themia.Widget.Clipboard`, `.Countdown`, their SPEC.md).

- [S-1440], [S-1441], [S-1442], [S-1443], [S-1444]: moved with the Clipboard widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clipboard/`).
- **[S-1458]** The clipboard history is the app's, not the widget's (user requirement): one
  `ClipboardHistoryService` (`AppServices.Clipboard`, a widget's `IWidgetHost.ClipboardHistory`)
  listens while any view on any display holds a widget whose config asks for it
  (`IKeepsClipboardHistory`, read by `ClipboardInterest`), on screen or not, so a view switched to shows what was
  copied while it was away. It keeps the longest capacity any of them asks for; each widget shows
  its newest entries up to its own. Remove and Clear history act on the one history, for every
  Clipboard widget. With no Clipboard widget left in the state the listener stops and the history
  is forgotten; still text only, in memory, never written. A detached state (a debug render) has
  none.
- [S-1445], [S-1446], [S-1447], [S-1448], [S-1986]: moved with the Countdown widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Countdown/`).
- **[S-1456]** `ItemMenuEntry.Children` makes an entry a submenu (`MenuFlyoutSubItem`), separators
  collapse inside it as at the top level.
- **[S-1457]** `ItemCursor.Move`: the shared list cursor (Up/Left back, Down/Right forward, clamped;
  Home/End; Escape clears; no cursor + forward = first, + back = last).
- **[S-1985]** `ItemCursor.Move` also pages: Page Up / Page Down jump ten items (`ListKeys.PageStep`),
  clamped; with no cursor they land on the first. RSS and Screen time move with it.

Not ported, deliberately: clipboard pin (an optional extra the audit listed, not in the original);
Countdown C-61 keep-open (above).

## CPU

- [S-1140], [S-6311], [S-6312], [S-6313], [S-6317]: moved with the CPU widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Cpu/`).
- **[S-1143]** Uptime wording (`CpuRules.Uptime`, used by the power tile), two largest units:
  `3d 4h`, `2h 5m`, `7m`.
- **[S-6310]** The core styles read each logical processor from the hub's `CpuCores` source
  (`\Processor Information(*)\% Processor Time`, the measure the total uses, so cores and number
  agree; `CpuCoreRules.Loads`: processor order by group then number, the totals left out, a reading
  that is not a number unknown). One PDH query, read once per tick for every such widget, opened
  only while a widget in some view has a core style (`SystemInterests`, [S-2513]) and closed with
  the feed. No counter: the core styles show the reading alone.
- **[S-6314]** **Gauge** (CPU, memory, GPU): the reading inside the shared gauge ([S-6302]).
- **[S-6318]** The per-core history is the hub's, not the view's: the `CpuCores` source keeps one
  ring for every core together (`CoreHistory`, a `float` per core per second, an unknown load a gap),
  filled by the same once-a-tick read the other core styles share ([S-2501]), so a meter built again
  draws every core's history at once ([S-2504]). Unlike a charted series it is not an hour by
  default: it holds the longest Core graphs span any view shows (`SystemInterests.CoreHistorySeconds`,
  [S-2513]) plus the slide's lead on a 24-DIP tile (`CoreHistory.CapacityFor`), and nothing while no
  widget shows the style. Cost: a minute on 16 cores is about 4 KB, an hour on 32 cores about 470 KB.
  A read with another number of processors starts it again; a shorter span keeps the newest.
- **[S-6319]** A core's graph: its history (the main graph's band, stepping as it does, [S-6331]) in its
  load's role, under its load in Caption (primary) 4 above it where the cell is at least 36 tall; the
  cells 8 apart in the grid whose cells come closest to half again as wide as tall, at least 32 wide and
  16 tall, else none (`MeterPlan.Graphs`). Room at L and XL is larger cells, never a larger figure.
- The original's per-core grid is the Core grid style ([S-6313]).

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
  drop and duplicate settle against it ([S-6608]), so nothing is lost under the taskbar; a rect that
  does not fit shrinks to it, one pushed out of reach slides back to the visible minimum keeping its
  size, and one dragged just past the taskbar's edge lands flush with it ([S-6609]). A widget the
  taskbar later grows over **stays where it is**: load validation keeps it in reach of the whole
  screen only, and nothing re-settles on a display change (a desktop item's widget is the one
  exception: it follows the work area as Explorer's icons do, [S-5562]). An unusable work area
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
  place, so the screen keeps its view and its widgets are not restarted. Monitors that report one
  device path in one enumeration (`DisplayIdentity.Separate`): the first keeps it, every later one
  falls back to its own GDI name the same way, logged, so each screen keeps a surface and its widgets
  (classic #176: two identical monitors, and the second screen showed nothing).
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
- **[S-847]** `DefaultViewForNewDisplays` (Display ▾ › New displays show, [S-3545]): a display without a
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
  ([S-663]). Each widget's own ([S-6532]) multiplies in through the same path; there is no
  app-wide one ([S-3590]).
- **[S-851]** The hit region never covers the taskbar: it is cut to the work area in normal mode,
  and is the work area itself (not the whole window) in edit mode. A widget the taskbar has grown
  over is clipped there (`SetWindowRgn` clips painting as well as input) rather than moved. This
  matters while Show Desktop lifts the surface above the topmost band ([S-33]), when the region is
  all that keeps the taskbar clickable.
- **[S-852]** The edit bar ([S-3500]) and the placement hint sit inside the work area (margins from
  `GridGeometry.AllowedPixels`), so a taskbar on the top edge does not cover them.
- **[S-853]** Display ▾ ([S-3543]) names its display by the monitor's name ("(primary)" on the
  primary) and its resolution, the id never shown, and holds its widget scale and its view bar
  ([S-3544]); its Show Themia on lists **every connected display**, switched off or not ([S-3545]).

Not ported, deliberately: the original's legacy ordinal-key migration ([MI-03]/[MI-04]; no
compatibility, by decision); its ~1 s work-area self-heal sweep ([DW-04]), which polls, replaced by
[S-848]'s messages plus [S-849]'s check on every pass; its adoption sequence counter ([MI-07]): the
pass runs synchronously on the UI thread, so there is no await for an older adoption to win across.

## Notes: note store and markdown editor

Code: Core `Notes/NoteStore.cs`, the SDK's `Notes/NoteMeta.cs` (`NoteMeta`, `NoteRead`), App
`Services/Notes/NotePool.cs` (behind `IWidgetHost.Notes` and `SettingsHost.Notes`). Tests:
`NoteStoreTests`, `LocalStoreFixtureTests`. The widget, its editor and the markdown rules are a module
of their own (`src/Widgets/Themia.Widget.Notes`, its SPEC.md).
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
- [S-1346], [S-1347], [S-1349], [S-1350], [S-1351], [S-1352], [S-1353], [S-1354], [S-1355], [S-1356], [S-1357], [S-1358], [S-1359], [S-1360], [S-1361], [S-1362], [S-1363], [S-1364], [S-1365], [S-1366], [S-1367], [S-1368], [S-3963]: moved with the Notes widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Notes/`).
- **[S-1348]** One `NotePool` per process: a change lands in memory at once and raises `Changed(id)`
  for a body, `Changed(null)` for the list; widgets re-read only for their id or null. Disk work runs
  off the UI thread, one operation at a time, in order; a queued body write is coalesced, never
  stacked. An unflushed local edit outranks an incoming body (original S-09/S-34). The pool is flushed
  on exit after the surfaces close.
- **[S-3964]** A hidden header (the card's Hidden placement, or a tile too short for one, [S-402])
  takes the bar with it: there is no other place for it. Ctrl+B / Ctrl+I, Tab and the list
  continuations still work, and the right-click menu still edits. A header at the bottom carries the
  bar there.

Not ported, deliberately: the pop-out window (original S-11..S-13, user decision); the selection
bubble (S-26: the bar and Ctrl+B/I cover it); the original's choice of where the bar goes (S-29: it
is always the header's, [S-3963], and goes where the header goes); the "On this
device" source row (S-35: there is no other source); a comfortable-measure cap at L (a 560-wide card
is about 80 characters already, and a cap would leave the empty band the ladder exists to avoid).

## Disk and ping

Package P2 of the system audit. Both stay `MeterPart`s on the shared `MeterView`; like every meter
they show the label, the reading and the history only ([S-1762]).

- **[S-1160]** Disk reports on one `Drive` (a root, `C:\`; empty = the system drive). Load repairs it
  to its root and drops anything that is not a drive letter, logged. Changing the drive clears the
  chart (original [S-22]). The menu offers "System drive (C:)" and every drive Windows lists;
  a chosen drive that has been removed stays offered as "not connected".
- **[S-1161]** The header title names the drive as Explorer does ("System (C:)", "Local Disk (D:)",
  "USB Drive (E:)"), "C:" for a drive the walk did not list, "All drives" for every drive
  (`DiskRules.HeaderTitle`). The header is the only place the meter names its drive (user
  requirement): the card's body is the rate and its history (or the Bars rows), with no drive
  token beside or under the rate at any size or style. With the header switched off, the
  reading's tooltip carries the same title. The rate is at the step, column and place every
  meter's reading is at that tier ([S-3860], Memory's percentage).
- **[S-1162]** Read and write rates are per drive from one shared PDH query
  (`\LogicalDisk(*)\Disk Read|Write Bytes/sec`, matched by letter ignoring case; `_Total` and folder
  mounts left out). A drive the counters do not list has no rate, never the system total (original
  themia-releases#49). The query is held while the hub's disk feed lives and closed with it ([S-2505]). Volumes and labels are the drive service's walk ([S-4655]: every 30 s and on a device change, original [S-21]), held by the hub's disk sources.
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
- [S-1166], [S-1168]: moved with the Disk widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Disk/`).
- **[S-1167]** Framework hooks for a kind (`MeterView.Part.cs`): a header title
  (`IMeterHost.SetTitle`), right-click commands (`MeterPart.ContextActions`), a header trailing
  element, and `IMeterHost.SampleNow`. A reading that carries a fraction is plotted even when it is
  not available (a missed ping); every other unavailable reading carries none.
- **[S-1169]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1170]** Ping status: Checking… until the first probe returns, then Online / Offline from the
  last probe. Offline is the header's problem glyph (the caution warning, [S-6710]) at its right, its
  tooltip saying it in words; Online and Checking show nothing there (no success green, [S-6706]); a
  dimmed reading's tooltip says it too.
- **[S-1172]** The ping chart is latency against the window's slowest answer (floor 1 ms); an
  unanswered probe is a dimmed "--" that plots on the baseline. Online draws the chart in the accent,
  offline in the critical role ([S-1123]).
- **[S-1173]** *(Retired by [S-3791]: no `HostLabel`. The host is the header's label beside "Ping" ([S-2260]); a name for it
  is the card's own title.)*
- [S-1174], [S-1175]: moved with the Ping widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Ping/`).

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
  on the surface losing activation (Alt+Tab, another window), on a press outside every surface
  ([S-815]), when the holder's card leaves its surface, on entering edit mode and when the surface
  closes. Only Escape gives the keyboard back: after a click elsewhere the click already put it where
  the user wants, and a launched app must not be raced. Every end moves XAML focus out of the old
  holder's card so no caret is left blinking; its view hears `WidgetKey.Escape` through the session
  ([S-813]), or directly when the session does not know the holder (a box focused without a press).
- **[S-803]** Keys: arrows, Page Up/Down, Home/End, Tab / Shift+Tab, Enter, Space, Delete, F2
  (Rename), F5 (Refresh), the Menu key / Shift+F10 (Menu), Escape. Ctrl and Alt chords are never widget keys
  (but the clipboard's, [S-1327], [S-6417], and the selection's Ctrl+arrows and Ctrl+Space, [S-6623]);
  Shift+Space and Shift+F2 are not either. Shift+Delete is its own key (`PermanentDelete`), never
  Delete: only the desktop's items answer it, with the shell's own prompt ([S-5545]). A view returns
  whether it used the key; an unused key carries on (Tab still moves focus).
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
- [S-811]: moved with the Gallery widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gallery/`).
- **[S-812]** Tasks and Habits with the lease but no row focused: a movement key lands on an end
  (Up/Down/Home/Tab the first, End/Shift+Tab the last); Enter, Delete and F2 do nothing.
- **[S-813]** One interaction session, app-wide (Core `WidgetSession<T>`, tests `WidgetSessionTests`;
  `AppHost.Session.cs`, `DesktopWindow.Session.cs`): from a press on any surface until the user is done.
  It names the widget being worked in — every widget, keys or not — and is the one rule for when a
  selection or cursor goes: its view hears `WidgetKey.Escape` (`IWidgetKeys`) whenever the session
  leaves it — or `WidgetKey.Deactivate` when it keeps its item selection and the user only went
  elsewhere ([S-6626]). A refused foreground ([S-800]) no longer drops the selection the click just made.
- **[S-814]** A press inside the active widget on its **background** (not an item, not a control,
  not a press a handler took) clears its selection; with Ctrl or Shift held (extending), or while the
  user is typing in it, nothing changes. Items are marked once (`WidgetItems.Mark`; `RowFocus.Prepare`
  marks every ring-carrying row; Shortcuts cells, Tasks, Habits, Contacts and Clipboard rows, Calendar
  days and events mark theirs). A press on another widget, or on the chrome (view bar), leaves the
  active widget: its selection clears, on any display.
- **[S-815]** The session ends — selection cleared, lease ended without giving the keyboard back — on
  a press outside every Themia window (the desktop, the taskbar, another app, empty surface that
  clicks through), on another app taking the foreground (unless it was Start or Search handing it
  back after our own click closed them, [S-938]), on Escape, edit mode and the widget going away.
  The outside press is heard by `OutsideClickWatcher`: a low-level mouse hook plus the foreground
  WinEvent, installed only while a session is open, on a thread of its own that only pumps messages
  (a busy UI thread never stalls the system's mouse), its callbacks catch and log, both removed on
  that thread. A hook because nothing else sees a press on the window that already has the
  foreground — on a desktop-widget app, usually the desktop.
- **[S-816]** The same signal closes menus: a press outside closes every open light-dismiss popup
  (item menus, the card menu, view-bar menus, the add menu, confirmations, flyouts) on every surface;
  a press on one surface closes the others' (its own light dismiss handles its own). Popups that are
  not light-dismiss (the scratchpad's full-screen page) stay. Live-check only: a Debug render cannot
  show light dismiss.

Not wired here (lanes still running; see the WP11 report): Mail (`ListKey` handlers), Clipboard
(`HandleKey(WidgetKey)` ready), Notes' list and Network drives.
Not live-verified: focus taking, Alt+Tab, deactivation timing, IME (see the report).

Key routing was fixed after this section was written: the holder now gets its keys in the tunnelling
pass, before a `ScrollViewer` can take them ([S-860]).

## Widget keys (WIDGET-KEYS)

Every widget view's keys in one place. The maps are Core (`ListKeys`, `ItemCursor`, `KitKeys`,
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
| Calendars (month, week, day, year, agenda) | Left/Right and PgUp/PgDn page by the layout's unit (month, week, day, year, agenda page); Up/Down move the selected day a week (month, week); Escape lets it go; Home today; Enter first event ([S-1269], [S-2710], [S-3624]) |
| Contacts | Up/Down, PgUp/PgDn, Home/End; Enter writes or calls; type-ahead ([S-1277], [S-1983]) |
| Tasks | Up/Down, PgUp/PgDn, Home/End; Enter/Space tick; Delete; F2; Esc ([S-787], [S-1981]) |
| Habits | as Tasks, plus Tab/Shift+Tab wrap ([S-1428], [S-1982]) |
| Folder | the selection keys ([S-6623]: arrows grid aware, Shift extends, Ctrl+arrows focus, Space/Ctrl+Space toggle, Home/End, PgUp/PgDn, Ctrl+A); Enter; Delete; F2; Menu; Ctrl+C/X/V; Backspace; type-ahead ([S-1327], [S-1993]) |
| Shortcuts | the selection keys ([S-6623]); Enter opens; Delete; F2; Menu; type-ahead ([S-1390], [S-1995]) |
| RSS | the kit's list map: Up/Down/Left/Right, PgUp/PgDn, Home/End; Enter opens; Menu; Esc ([S-1306], [S-7020]) |
| Clipboard | Up/Down, PgUp/PgDn, Home/End; Enter copies; Delete; Menu ([S-1444]) |
| Pictures (slideshow) | Left/Right; Home/End; Space pauses; Enter opens ([S-811], [S-1992]) |
| Mixer | Up/Down/Home/End rows; Left/Right, +/- step; PgUp/PgDn step 10; M/Space mute; Menu ([S-1225]) |
| Volume | arrows, +/- step; PgUp/PgDn step 10; Home/End 0/100; M/Space mute ([S-1226], [S-2733]) |
| Music | Space/Enter play-pause; Left/Right track; Shift+Left/Right seek 5 s ([S-1468]) |
| Pomodoro | Space/Enter start-pause; Right skip; Home reset ([S-1987]) |
| Screen time chart, donut, apps | Up/Down, PgUp/PgDn, Home/End move the linked cursor; Enter/Space select; Menu hides; Left/Right a day ([S-3307], [S-3334], [S-3344], [S-3354]) |
| Quote | Right/Space/Enter next; Left previous ([S-1990]) |
| Video stream | Space pause ([S-1991]) |

## Item selection (ITEM-SELECTION)

Selecting items in a widget looks and behaves as selecting icons on the Windows 11 desktop (user
request 2026-10-19). One rule set and one look for every widget whose items are selected: the
Folder ([S-1327]), Shortcuts ([S-1390]); Recent files' single cursor takes the look ([S-4845]).
Owning files: SDK `Themia.Sdk/Widgets/ItemSelection.cs` (the rules), `SelectionBand.cs` (the
rubber band), `SelectionLook.cs` (the look's rule); SDK WinUI `Widgets/SelectionChrome.cs`
(`SelectionChrome`, `SelectionLabel`, `SelectionInput`). Tests `ItemSelectionTests` (`S662x_`).
Outside edit mode only: there the cards are inert ([S-1751]) and edit mode clears a selection.

- **[S-6620]** Clicks (`ItemSelection.Click`): a click selects the item alone and focuses it; Ctrl
  toggles it; Shift selects the range from the anchor (Ctrl+Shift adds that range). A click on the
  widget's empty space clears the selection (Ctrl or Shift held keeps it, [S-814]). The selection is
  kept by id against the order drawn (`SetOrder`): a redraw, a re-read or a search keeps what is
  still shown and drops the rest, focus and anchor included. Whether a click also opens is the
  widget's `ClickMode` (single: a plain click; double: a double-click), unchanged.
- **[S-6621]** A plain press on an item already selected changes nothing until the release
  (`Press`/`Release`), so dragging it drags the whole selection (the folder's files out, the
  shortcuts' reorder); a click without a drag then selects it alone. A press elsewhere or with a
  modifier acts at once.
- **[S-6622]** A right press (`Context`) on an item outside the selection selects it alone; on one
  inside, the selection stays and the item menu acts on all of it.
- **[S-6623]** Keys, with the widget's keyboard lease ([S-800]), one map (`ItemSelection.Key`):
  arrows move the focus and select it alone (a grid: Up/Down a row, Left/Right an item; a one-column
  list ignores Left/Right; Down past the last full row lands on the last item; Up from the top row
  stays); Shift+arrows, Shift+Home/End/Page keys extend from the anchor (Shift is read as the key
  arrives, `SelectionInput.ShiftHeld`); **Ctrl+arrows** move the focus only and **Space / Ctrl+Space**
  toggle the focused item (`WidgetKey.FocusUp/Down/Left/Right`, `ToggleSelection`: the lease's only
  Ctrl chords besides the clipboard's, [S-803]); Home/End, Page Up/Down; Ctrl+A selects all. The
  first key with nothing focused lands on the first item (End on the last). Enter opens, Delete,
  F2, the Menu key and Escape stay the widget's own. Type-ahead ([S-1980]) selects the match alone.
- **[S-6624]** The rubber band (`SelectionBand`): a press on the empty space (the folder: also beside
  a name, [S-6412]) that moves past the 5 DIP dead zone draws a band; every item whose box it
  overlaps at all is selected, live as it grows and shrinks (`Hits` over the drawn boxes;
  `GridHits` over a layout's cells, so a folder's rows not built yet are covered, [S-1887]). The
  band stays inside the items' area. Look: the highlight colour at 28 % under a solid 1-DIP border
  of it (`SelectionLook.Band`, `SelectionChrome.PaintBand`), above the items.
- **[S-6625]** How the band combines (`SelectionBand.Mode`, `BeginBand`/`Band`/`EndBand`): no
  modifier replaces the selection; Shift adds to the selection the band started with; Ctrl toggles
  each covered item against it (Ctrl wins with both), as the desktop's Ctrl+drag. Live: an item the
  band leaves again goes back to how it was. The focus goes to the last item covered.
- **[S-6626]** Going elsewhere keeps the selection, as the desktop keeps its icons selected while
  another window is active: a view that keeps it (`IWidgetKeys.KeepsSelectionWhenLeft`: the Folder,
  Shortcuts) hears `WidgetKey.Deactivate` instead of Escape when the session leaves it ([S-813]: a
  press on another widget, on the chrome, outside every surface, another app taking the
  foreground) and draws the selection inactive; the focus cue, a pending press, a half-typed name
  and an open rename (committed) go. Escape, a press on its own empty space ([S-814]) and edit mode
  still clear it (`AppHost.LeaveWidget`). A press in the widget makes it active again.
- **[S-6627]** The look (`SelectionLook.Ink`, `SelectionChrome`): each item's mark is one chrome border
  (`SelectionChrome.Wrap`: the hairline, the family's radius 4) over the whole of it, icon and name, laid behind its
  content (a row's mark under its Row block, over the row's full width; a cell's 4 around its lead and name, [S-6713]),
  so marking it moves nothing; it sits inside the item and over its item fill ([S-6513], painted once at rest), so the
  user's item background ([S-6562]) stays the item's rest and the selection is laid over it, behind the
  text; the text keeps its colour ([S-6540]). Colour: Windows' highlight (`UISettings` Highlight,
  `COLOR_HIGHLIGHT`; the contrast theme's in high contrast; #0078D7 if it cannot be read), a colour from data made a
  brush through the data entry (`HexBrush.Colour`, [S-6705]), the same
  in light and dark, recoloured when Windows' colours change. Opacities, fill / border: hover
  12 % / 35 %; selected 32 % / 65 %; selected under the pointer 40 % / 75 %; selected without the
  keyboard a neutral grey (#808080) 28 % / 42 % (34 % / 50 % under the pointer); the focus alone a
  65 % border; a drop target as selected under the pointer. Nothing at rest.
- **[S-6628]** The focus shows (a border only, [S-6627]) only after keyboard use and while the
  widget has the keyboard (`ItemSelection.Cues`): a key that moves or toggles shows it; a press, a
  right press, a band, Escape and leaving hide it.
- **[S-6629]** A selected tile's full name (`SelectionLabel`): when the focused item is selected, its
  name is cut short and the widget has the keyboard (`SelectionLook.ShowsFullName`), the whole name
  is shown in an overlay over the tiles below (the band's layer, so no tile moves), on the default
  card's colour with the selection's mark, wrapped to the tile's width; it takes no presses. Not
  while renaming. Tiles only (the folder's Grid and Icons, the shortcuts' labelled grid); rows keep
  trimming.
- **[S-6630]** Debug: `--widget-png Folder|Shortcuts … --state selected|inactive|lasso|fullname`
  draws two items selected, one under the pointer and the focus moved by Ctrl+arrows (`selected`), the
  same after the widget lost the keyboard (`inactive`), a band (`lasso`) and a long name shown in full
  (`fullname`); the folder's `fullname` picks its longest name.

## View links (VIEW-LINKS)

Widgets on one view share what the user is pointing at and what they picked, so a hover in one
lights the same thing in the others. Owning files: SDK `Themia.Sdk/Links/ViewLinks.cs`
(`LinkTopicId`, `LinkTopics`, `LinkBoard`, `LinkMember`, `LinkRegistry`); App
`AppServices.Links`, `IWidgetSurface.Links`, `IWidgetCardHost.Links`. Tests `ViewLinksTests` (`S33xx_`).

- **[S-3300]** Each view has one **board** (`LinkBoard`), keyed by the view id in the host's one
  `LinkRegistry` (`AppServices.Links`), so every display showing a view shares it and two views never
  do. A widget reads its board from `IWidgetSurface.Links` in Attach (null where a host has no view),
  joins it (`board.Join()` gives a `LinkMember`) and disposes the member in Detach. A debug render's
  host has one board for all its cards.
- **[S-3301]** Topics are typed and closed: `LinkTopics.X` is a `LinkTopic<T>` with a `LinkTopicId`
  member and a lifetime; the value is a record (`AppLink`, `DateLink`, `RangeLink`), so setting the
  same value again is no change. A topic is added in `LinkTopics` with its enum member, nowhere else.
- **[S-3302]** Two lifetimes. **Hover** is ephemeral: set on pointer enter (or a keyboard cursor
  move, [S-3307]), cleared on leave. **Sticky** (selection) stays until a member changes or clears it:
  `Toggle` selects a value, or clears it when it is already the selection (a second click on it);
  Escape and a click on the widget's empty space clear it. Nothing is persisted: a restart starts with
  nothing selected (it is a pointer gesture, not a setting).
- **[S-3303]** A member never hears its own change; it applies its own state first, then publishes.
  `member.Watch(topic, handler)` hears every other member's change with the topic's value as it is
  then (null when cleared). One handler per topic per member.
- **[S-3304]** A hover belongs to the member that set it: only that member's clear takes it away (a
  late "pointer left" in one widget never clears what another lit since), and a member that leaves
  (Detach) takes its hovers with it, telling the rest. A selection can be cleared by any member and
  survives its setter leaving. A disposed member is inert: every call is a no-op.
- **[S-3305]** A view switch detaches the old view's widgets, so its hovers clear; its selections stay
  on its board, in memory, and the widgets read them again when the view is shown. Deleting a view
  drops its board (`Retain` on every state change).
- **[S-3306]** UI thread only, no lock: every member is a widget view. Handlers run in member order
  over a copy, so a handler may join, leave or publish another topic; a handler that throws is logged
  and the others still hear the change.
- **[S-3307]** A widget holding the keyboard lease ([S-800]) that moves a cursor over linked items
  publishes the cursor as the hover, and Enter / Space as the selection, exactly as the pointer does.

Topics:

| Topic | Value | Lifetime | Published by | Shown by |
|---|---|---|---|---|
| `HoveredApp` | `AppLink(app key)` | Hover | Screen time chart, donut, apps ([S-3310]) | the same three |
| `SelectedApp` | `AppLink(app key)` | Sticky | the same three | the same three |
| `SelectedDate` | `DateLink(DateOnly)` | Sticky | date widgets (calendar) ([S-3311]) | date widgets |
| `VisibleRange` | `RangeLink(first, last)` inclusive, ordered | Sticky | date widgets (calendar) ([S-3311]) | date widgets |
| `HoveredEvent` | `EventLink(account, calendar, id)` | Hover | the calendar widgets ([S-3623]) | the calendar widgets |

- **[S-3310]** The screen-time widgets ([S-3320]) share `HoveredApp` and `SelectedApp` by app key
  (`ScreenTimeRules.OtherKey` for the "Other" remainder). What a widget emphasises is the hover when
  there is one, else the selection (`ScreenTimeRules.Focus`).
- **[S-3311]** `SelectedDate` and `VisibleRange` are defined for the date widgets; what they publish
  and how they show it is their section's: the calendars' is [S-3620]..[S-3625].

## Keyboard in the shell (SHELL-KEYS)

Keys everywhere outside a widget's own view: edit mode on the desktop surface, the view bar and the
edit bar's menus. Rules in Core `EditKeys`, `KeyboardNudge`, `SelectionRules.Cycle` and the widgets'
`TypeAhead` ([S-1980], one implementation for every list; tests `ShellKeyboardTests`); carried out by
`DesktopWindow.Keys.cs` and `ViewBar.cs`. Widget views' keys are the lease's ([S-800]..[S-812]).
The surface keeps `WS_EX_NOACTIVATE` outside edit mode ([S-20]), so none of the surface keys exist
there: Themia never takes a key from the app the user is typing in.

| Where | Key | Does |
|---|---|---|
| Anywhere | Ctrl+Alt+W | Toggle edit mode ([S-65], global hotkey) |
| Anywhere | Ctrl+Alt+Shift+W | Edit mode with the add menu open ([S-1998], global hotkey) |
| Edit mode | Ctrl+N | Open the add menu ([S-1998]) |
| Edit mode | Arrows | Move the selection one grid cell ([S-1970]) |
| Edit mode | Ctrl+Arrows | Move ten cells ([S-1972]) |
| Edit mode | Alt+Arrows | Move one DIP, off the grid ([S-1972]) |
| Edit mode | Shift+Arrows (+Ctrl / +Alt) | Resize by the right / bottom edge, same steps ([S-1971]) |
| Edit mode | Tab / Shift+Tab | Select the next / previous widget ([S-1973]) |
| Edit mode | Enter | Into the selection's properties menu ([S-1974]) |
| Edit mode | Menu key / Shift+F10 | The selection's menu, or the surface's ([S-1974]) |
| Edit mode | F6 / Shift+F6 | Widgets → toolbar → view bar → notices ([S-1975]) |
| Edit mode | Delete | Delete the selection, undoable ([S-1592], [S-885]) |
| Edit mode | Ctrl+C / X / V / D | Copy, cut, paste, duplicate ([S-70]) |
| Edit mode | Ctrl+A | Select all on the display under the pointer ([S-1582]) |
| Edit mode | Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z | Undo / redo ([S-885]) |
| Edit mode | Ctrl+1..9 | Show the n-th view ([S-904]) |
| Edit mode | Escape | Placing, then the selection, then edit mode ([S-1583]) |
| View bar (edit mode) | Arrows, Home / End; Enter / Space | Move between chips; show the view ([S-1979]) |
| Settings | F6 / Shift+F6 | Title bar → navigation → page ([S-1977]) |
| Settings | Alt+Left / Alt+Right, mouse back / forward | Page history (existing) |
| Settings | Ctrl+W | Close the window ([S-1977]) |
| Settings navigation | Up / Down, Enter / Space | Move, open (NavigationView's own) |
| Settings navigation | Home / End, letters | First / last entry, type-ahead ([S-1978]) |
| Add menu | Arrows, Home / End, Enter, Space, Escape | Walk the tree, open a subject or family (Right, Enter), close it (Left, Escape), place, empty the search then close ([S-3514], [S-3515]) |
| Menus, flyouts, dialogs | Arrows, Enter, Escape | WinUI's own: Escape closes the innermost |

- **[S-1998]** The add menu straight from the keyboard (issue #120). **Ctrl+Alt+Shift+W** anywhere
  (a second global hotkey: Ctrl+Alt+W with Shift for "and add") and **Ctrl+N** in edit mode take one
  path, `AppHost.OpenAddMenu`: edit mode on, a placement in progress dropped, and the add menu
  ([S-3501]) opened on the display under the pointer (as a paste lands there, [S-74]), at the
  pointer, else mid work area. The surface takes the foreground (the hotkey press is what lets it)
  so typing reaches the menu's search ([S-3514]). Both hotkeys are rows of one table, Core
  `ShellHotkeys`, registered `MOD_NOREPEAT` and unregistered on shutdown by the command window's one
  loop; one another program owns is logged and skipped, as [S-65]. The surface menu's Add widget
  shows Ctrl+N. (Tests `S1998_`.)
- **[S-1970]** In edit mode an arrow moves **every selected widget on this display** one snap cell
  ([S-06b]) as one block, by the drag's rules ([S-1584]): the block's corner snapped when the grid
  is on, the block free to hang over an edge until a widget would keep less than the visible
  minimum ([S-6608]), never pulled apart at an edge. An off-grid widget lands
  on the next line in the arrow's direction, never back where it was. Nothing selected, or a ghost
  being placed: the arrows do nothing.
- **[S-1971]** **Shift** + arrow resizes instead, each selected widget on its own, always by its
  **right or bottom edge** (Right / Down grow, Left / Up shrink), so the top-left corner stays put.
  Only the moved edge snaps ([S-31a]); the [S-08] floor and the visible minimum ([S-6608]) stop it,
  and it may grow past the work-area edge. Shift was
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
- **[S-1974]** **Enter** moves the keyboard into the selection's properties menu ([S-3707]), on its
  first control, as F6 reaches it, where the widget's options are (decided 2026-10-19: there is no
  widget page to open); nothing while the menu is away, and Escape there hands the keyboard back.
  The **Menu key** or
  **Shift+F10** opens the selection's card menu on its card ([S-1743]), or the surface menu
  ([S-1744]) at the work area's centre when nothing is selected.
- **[S-1975]** **F6 / Shift+F6** moves the keyboard round the surface's panes: the widgets (the
  selected card, else the surface), the toolbar's first button, the view bar (when it shows) and the
  notices (while one has a button). This is how the toolbar is reached without the mouse.
- **[S-1976]** One key map: `EditKeys.Decide` turns a key and its modifiers into a command and the
  surface only carries it out; the old per-key switch is gone. Ctrl+Shift letters, Shift+Delete,
  plain letters and F10 alone are not edit keys. Ctrl+D is not one either: widgets have no
  Duplicate, Copy then Paste does that (one way to do it). While a text box has the focus (the picker's
  search) no key is an edit key: Delete or Ctrl+V there edits the text, not the desktop.
- **[S-1977]** *(Retired by [S-3585]: there is no settings window, so no Ctrl+F to its search, F6 round its panes or Ctrl+W.)*
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

Package SYS-P3. Both meters are modules (`src/Widgets/Themia.Widget.Battery`, `.Network`, their
`SPEC.md`); the rules are the SDK's (`BatteryRules`, `NetworkRules`, tests in
`BatteryNetworkTests`). Like every meter they show the label, the reading and the history only
([S-1762]).

- **[S-1180]** Battery: charge from `GetSystemPowerStatus`; charging is the `BatteryFlag` charging bit,
  not the plug. The no-battery decision is `BatteryRules.Read`'s (bit 7 of a known flag with no
  usable percentage; 255 is "unknown", not "none"); a machine with no battery shows an empty state.
- **[S-1186]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1187]** The battery reading leads with the Segoe Fluent battery glyph filled to the nearest
  tenth, with a bolt while charging (`MeterPart.Badge`), at the reading's step in the secondary role,
  Caution or Critical while the charge is ([S-1123]); the Battery and Ring styles draw the charge
  themselves and have no badge.
- **[S-1190]** Network throughput is per adapter (`ThroughputTracker`): a counter that falls is that
  adapter's reset (reads 0 once, re-baselines) and no longer zeroes every other adapter's traffic;
  a new adapter starts from a baseline. Read by the hub once per tick for every tile ([S-2501]).
- [S-1191]: moved with the Network widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Network/`).
- **[S-1192]** Counted adapters: up, not loopback/tunnel, not a packet-capture mirror (name or
  description contains "pcap packet driver"; the standalone Npcap Loopback Adapter is kept), and not
  an NDIS filter binding listed as its own adapter ("WiFi-QoS Packet Scheduler-0000": another
  adapter's name, "-", a filter, "-NNNN"), each of which re-reports its adapter's counters — on the
  dev machine seven of them multiplied the reading sevenfold, a bug the first port had too.
- Not ported / different: the battery's status line, health, rate, cycles, capacity list and the
  network's connection, addresses, public IP, session totals and adapters list ([S-1762]); RX green /
  TX accent colours (the chart's two-series styling is the framework's).

### The network, disk, battery and ping styles

Styles ([S-6000]) of the I/O meters, on the shared `MeterView` (`MeterLook`, [S-6300]), each the
Readout and a part of the family's Chart block under it, laid out by `MeterPlan`. The hub, the series
and their history are the same in every style. Tests `MeterPlanTests`, `IoStylesTests` (`S626x_`, `S627x_`).

- **[S-6261]** A picture style (the battery, the signal bars) is the reading at the top on its step and
  the picture 8 under it, start-aligned, as large as the rest of the content allows at its own aspect;
  a picture under 16 is not drawn and the reading stands alone (`MeterPlan.Battery`, `MeterPlan.Signal`).
- **[S-6262]** **Battery** (battery): a battery drawn as Windows' battery glyph is: a body twice as wide
  as tall with an outline of 2 in the secondary role and the one radius, a 4-wide round cap at its right
  (two fifths of its height, centred), and, 4 inside the outline, a fill from the left to the charge in
  its role ([S-1123]). While charging the bolt (16, 32 or 48: the largest within three quarters of the
  fill's room) stands in the middle of the body. Not on this PC: outline and fill disabled, at the middle.
- **[S-6263]** **Signal bars** (ping): four bars 4 apart rising left to right on one line, as Windows'
  Wi-Fi signal (a box a quarter wider than tall, as tall as the room under the reading; the one radius,
  round when a bar is narrower than 8), as many in the reading's role (the accent, Caution, Critical) as
  the last answer earns: up to 50 ms 4 bars, up to 100 ms 3, up to 200 ms 2, slower 1, an unanswered
  probe or none yet 0 (`PingSignal.Thresholds`); the rest the Track role. The latency is the reading.
- **[S-6264]** **Numbers** (network): download and upload, two Readouts each behind its arrow (the lead
  glyph, secondary), on the one step the widest takes in its half: side by side in two columns 12 apart
  on an expanded width, else one over the other 8 apart (`MeterPlan.Numbers`). No history.
- **[S-6265]** **Bars** (disk): the total rate as the reading, and under it a labelled row for reads
  and one for writes ("Read  42 MB/s"), each with a bar (the shared bar rows, [S-6304]).
- **[S-6266]** **Ring** (battery): the Chart block's ring of the charge in its role, as large as the
  content's short side, start-aligned, the reading centred in it on the step its widest case takes in
  three quarters of the hole's width and half its height (`MeterPlan.Ring`, `RingReading`); under 48
  the reading stands alone. The drive's ring ([S-6274]) is the same ring with the space line under it
  (G6); `RingFace` is gone.
- **[S-6267]** Only the graph style draws the history (`GaugeConfig.DrawsHistory`); a styled meter's
  menu offers no Show history switch ([S-1122]), the chart span stays (under Style).
- [S-6270]: moved with the Network widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Network/`).
- **[S-6271]** Disk: `DiskConfig.Style`, **Graph** ([S-1820]) and **Bars** ([S-6265]). A bar's share
  is its rate against the top the graph would have: the doubling of 1 MB/s past the peak of the last
  minute's totals and this sample's ([S-1821], `DiskRules.BarShares`), so idle reads as a sliver; the
  window starts again when the drive or the scope changes.
- [S-6272]: moved with the Battery widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Battery/`).
- [S-6273]: moved with the Ping widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Ping/`).

## GPU and screen time: the whole tile (SYS-P4)

Code: the GPU module (`src/Widgets/Themia.Widget.Gpu`: `GpuView.cs` + `GpuView.Panel.cs`, its
menu), `Services/GpuSampler.cs`, the SDK's `GpuRules` and `MeterFace`;
`Widgets/ScreenTimeView*.cs` (+ `.Footer.cs`), Core `ScreenTimeRules`. Tests `GpuRulesTests`, `ScreenTimeRulesTests` (`S12xx_`).

- [S-1200], [S-1201]: moved with the GPU widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gpu/`).
- **[S-1207]** *(Retired: one adapter per widget, [S-433]; nothing shares the tile.)*
- **[S-1210]** The ring's total stands in its hole on the family's Readout steps (`ScreenTimeRules.Figure`,
  [S-6702]): the larger of 28 and 40 (capped by the area as every data widget's number) whose line,
  and the line under it where it fits, stands inside the hole as the rectangle the circle holds; when
  `4h 37m` fits on none, the compact form (`4.6h`, `13h` from ten hours, `37m`, `<1m`) on the largest
  that holds it, else in BodyStrong. Never clipped; the line under is cut to the hole's width.
- **[S-1211]** *(The app list's since [S-3352]; `ShowList` retired with the legend.)* `MaxApps` is a ceiling over what fits: 0 (the default) lists as many apps as the tile has
  whole rows for, 1–50 at most that many; an "Other" row joins when apps are left over. `ShowList`
  (on) switches the legend off. Supersedes the fixed default of 7 and "0 lists none" in [S-452].
- **[S-1212]** *(Retired by [S-3341]/[S-3351]: the drawings split out seven apps, the list names what fits.)*
  The drawing names the apps the legend lists: all of them when they fit (no "Other"),
  else all but the last row, which is "Other" — but never fewer than 7 unless `MaxApps` says fewer.
- **[S-1213]** *(Retired: no footer in any of the three, [S-666].)* On a tile Expanded both ways, with the drawing keeping at least 200 px, a footer
  appears from the bottom: for a day, the week strip. Nothing else (the stats line and the daily
  limit bar are retired: the limit is the headline's "of 4h" already, [S-457]; concise widgets).
- **[S-1214]** *(Retired with the footer: the chart is the week, [S-3330].)* Week strip (Day period, not style None): the seven days ending on the shown day, one
  bar each scaled to the busiest, the shown day in the accent, weekday under each; a click shows that
  day (today unpins) within the navigation's reach.
- **[S-1215]** *(Retired with the stats line, [S-1213].)*
- **[S-1216]** *(Holds for each of the three, [S-3332]/[S-3342]/[S-3352].)* Screen time lays out from the card's `WidgetArea` (`IWidgetSizeAware`), not its own
  `SizeChanged`; a debug render never hooks or writes ([S-459]).
- Not ported / different: the original's GPU `visibleMetrics` order (fixed ladder order); per-engine
  history charts (one chart, the load).

## System data hub (SYS-HUB)

Every system widget — the meters (CPU, memory, disk, network, battery, ping), the GPU tile and the
detail widgets — reads through one hub that lives as long as the app. Owning files:
`Themia.App/Services/SystemData/*` (`SystemHub*.cs`, `ThisPc*.cs` (this PC's sources), `SeriesProjections.cs`, `HubSource.cs` (with `SystemMachine`), `SampleMachine.cs` ([S-1931]), `WatchSlot.cs`),
Core `Themia.Core/SystemData/*` (`FeedKey`, `SystemCadence`, `FeedTable`, `SystemInterests`, `SampleRing`, `SeriesPoint`).
Tests `SystemDataHubTests` (`S25xx_`).

- **[S-2500]** One hub, `AppServices.System` (a widget reaches it as `IWidgetHost.System`, [S-6913]), made by the host on the shared ticker and closed with it
  (a debug render makes its own). A view watches in Attach (a parked view watches nothing, [S-1881])
  and lets go in Detach; no view reads a counter, walks a list or sends a probe. The samplers
  (`SystemMetrics`, `MemoryCounters`, `PerformanceInfo`, `ProcessorInfo`, `ProcessList`, `DiskMetrics`,
  `NetworkThroughput`, `LocalAddresses`, `PublicAddress`, `BatteryDevice`, `GpuSampler`,
  `NetworkMetrics.PingAsync`) are read by the hub alone, so none keeps a same-tick cache of its own.
- **[S-2501]** A source (`SystemSource`; the ping source per host) is read **once per tick**,
  however many widgets show it, and every delta (CPU load, throughput, process shares) has exactly one
  reader. Sources are read in `SystemSource` order, so whatever a widget combines comes first: CPU
  details' load and counts before the processor, network details' addresses before the throughput,
  the battery's charge before the pack.
- **[S-2502]** A feed is a source and a target (the ping host, trimmed and lower-cased; empty otherwise,
  `FeedKey`); there is no cadence in it. Every widget asking for one key shares one feed and one history.
- **[S-2503]** **One cadence for every system figure, not a setting** (`SystemCadence`): the hub holds one
  ticker subscription, every second, while any feed is open, and a source is read when its own period
  index moves on. CPU, memory, disk activity and space, network, addresses, processes, the battery's
  charge and GPU are read every second, so every tile on the desktop moves on the same second (a ping
  and the battery pack every fifth, on the same boundaries: [S-2512], [S-2514]). A new feed is read at once.
- **[S-2504]** A charted series (a feed plus an instance: a drive root or `*`, an adapter's position)
  keeps its history in the hub, in a ring of `SeriesHistory.Capacity(source)` points: the longest
  span offered (1 h) at the source's spacing, 3600 points, 720 for a ping (at least the widest
  statistics window). A view built again — a view switch, edit mode, a display change — loads the history and the newest reading at once; a longer span shows the longer
  history at once; a different drive or host is a different series with its own history. The point is
  the chart's terms (`MeterSample.Of`): a fraction, or the value on a window-scaled chart; an
  unanswered probe plots on the baseline but is no answer for statistics.
- **[S-2505]** A feed lives while the state **wants** it ([S-2513]) or a watch **holds** it (a render, a
  preview; `FeedTable`), and is dropped the moment it has neither, with its history: its source closed
  (its PDH query, GPU session, process buffer or address lease given back, a probe out cancelled). There
  is no linger: a widget still in some view is still wanted. A series nothing wants or watches (the
  drive a meter no longer shows) is dropped with its history.
- **[S-2506]** A watch never calls back from inside the call that makes it: the view reads the newest
  value and the history off the watch, and every later read arrives through its callback on the UI
  thread. A callback that throws is logged and reported to that widget's card ([S-891]), and the
  other widgets still get the reading; the hub's own ticker subscription is outside any card's fault
  scope (`Ticker.SubscribeUnscoped`).
- **[S-2507]** A source that takes a while (GPU on the pool, ping) has one read out at a time; a due
  tick while it is out is served by its answer, so a slow host updates more slowly instead of
  stacking probes, and an answer for a closed source is dropped.
- **[S-2508]** Ping: one prober per host, shared by every ping meter and ping statistics widget on it
  (one history); its transitions are logged once per prober.
- **[S-2509]** Event-driven where Windows says so: a power event (battery status, power supply,
  charge) re-reads the battery feeds at once; an address change marks the local and public addresses
  stale and re-reads them and the throughput. The hub listens only while such a source is open.
  These reads update the reading and add no chart point. The battery pack is asked only while Windows
  says there is a battery ([S-2423]).
- **[S-2510]** Refresh (ping) reads the source now and adds a point to its history: a probe
  asked for is a probe made.
- **[S-2511]** The switch check ([S-1883]) prints the hub's feeds, the feeds the state wants and the
  series kept (and the gallery's sources, lingering sources and decoded pictures, [S-3413]); the hub's
  one ticker subscription does not count against the live widgets.
- **[S-2512]** A ping probes every fifth tick (`SystemCadence.PingSeconds`, 5 s): a probe is a packet to
  somebody else's host, and one a second for the app's life is not a desktop's business. It lands on
  the same second boundaries as every other read, and its history points are five seconds apart.
- **[S-2513]** What the hub reads is the needs of **every system widget in every view**, shown or not
  (`SystemInterests.From(state)`, recomputed on every state change and passed only when it changed),
  from app start, as the sync services do for accounts and the web ([S-2608]). A view switched to finds
  its charts with their history and its details already read; nothing starts when a widget is first
  looked at. A widget's view watches through the same rule (`SystemInterests.For`). The public address
  is wanted only by a details widget with it switched on ([S-2417]). The cost is bounded by what the
  views hold, and the hub's ticker stops with the desktop. One thing waits for a widget on screen:
  the NVIDIA library a GPU widget's load comes from ([S-437]).
- **[S-2514]** The battery pack (what the battery class driver reports: capacities, rate, voltage,
  temperature, cycles, chemistry, maker; read only by Battery details, [S-2423]) is asked every fifth
  tick (`SystemCadence.BatteryPackSeconds`, 5 s) and at once on each of Windows' power events
  ([S-2509]: the charge moved a step, the supply or the status changed), not every second (CPU audit
  2026-10-26: a round of IOCTLs to the firmware sixty times a minute). An ask reads the battery's tag,
  its temperature and its status; its information and maker (`BatteryFactCache`) only for a tag not
  read before (a battery put in, another one in the bay), after a power event and after the device
  list's own refresh (once a minute), so a full-charge capacity or a cycle count is never older than
  that. Windows has no event for the rate, so the rate a tile shows is at most five seconds old.

## System details (SYS-DETAILS)

The details the original's meters showed, back as their own placeable widgets; the meters themselves
stay label, reading and history ([S-1762]). Seven kinds, each in its meter's family of the
add menu ([S-3511]): CPU details, Memory details, Top processes, Network details, Battery
details, Ping statistics, GPU details. One view, the SDK's `DetailView` (+ `.List.cs`), one `DetailPart` per
kind in its module project (`src/Widgets/Themia.Widget.<Kind>Details`, `.TopProcesses`, with its config,
menu and `SPEC.md`); rules in the SDK (`DetailFace`, `DetailSheet`, `CpuDetailRules`,
`MemoryDetailRules`, `ProcessTable`, `NetworkDetailRules`, `BatteryDetailRules`, `GpuDetailRules`; the
ping window's `PingWindow` is the ping details module's). Tests `SystemDetailsTests` (`S24xx_`). Shared
strings in `Locales/widgets-details`.

- **[S-2400]** A detail widget is a hero (its main figure), a caption saying what it is of, facts in
  priority order and, for some kinds, a list. No "which facts" settings: the tile shows more as it grows.
  Every source is Win32, PDH, `NtQuerySystemInformation`, DXGI, D3DKMT or a battery IOCTL: no WMI, no
  `PerformanceCounter`, no driver, no admin rights. Every figure is a hub feed ([S-2500]), watched in `Attach` and let go in
  `Detach` ([S-1881]); a parked widget holds none.
- **[S-2401]** A detail widget reads on the hub's one cadence ([S-2503]); `DetailConfig` has no cadence
  of its own and a stored `intervalSeconds` is not read.
- **[S-2402]** The ladder (`DetailFace.Plan`, tests `DetailFaceTests`), on the family's blocks: the
  figure is a Readout at the view's top, start-aligned, its line under it (Caption under 28, Body under
  40 and 68) where it fits, on the step its widest case takes (`DetailPart.HeroWorst`): 28 or 40 and
  no larger ([S-6702]). On an expanded width (Wide, L, XL) the figure stands in a column of two fifths
  of the card and the rows beside it from the top; otherwise the rows start 8 under it. The rows are
  the facts first (fact rows, [S-3862]), then the list, only whole rows ([S-6712]); a tiny tile is the
  figure alone, a widget without a figure (Top processes) its list from the top. A larger tile shows
  more rows, never a larger figure; what they cannot use stays empty ([S-666]).
- **[S-2403]** A fact Windows does not report on this machine is left out, never "--"; a fact that can be
  read but is not available right now (a lost probe) is a dimmed "--". A thing that is not on the machine
  (no battery, no adapter) is its facts, each "--", dimmed ([S-1942]). Why the hero is dimmed is its tooltip.
- **[S-2404]** A list is the family's list rows ([S-6711]: a 16 lead, the name primary, its figure
  secondary at the end; a second figure on the second line and a detail on the third, as the tier's
  lines allow, [S-6712]) in a scroller of whole rows with the foot fade ([S-1281]) and the row's focus
  ring (stroke 2, the accent) walked by the keyboard lease ([S-800], `ListKeys`): Up/Down, Page,
  Home/End, Escape; Enter opens where a row opens. The ring stays on its row's key across reads. Only
  a list on screen wants keys.
- **[S-2405]** CPU details: the effective clock as the hero, "23% utilization" (the CPU meter's own shared
  read) under it; base speed, cores (`GetLogicalProcessorInformationEx`), logical processors, processes,
  threads, handles (`GetPerformanceInfo`), up time, the thermal zone. The header's label ([S-2260]) names the processor from
  the registry, without vendor and generation noise ("Core i9-12900K").
- **[S-2406]** The clock is the base clock times `\Processor Information(_Total)\% Processor Performance`
  (Task Manager's figure, past 100 % under turbo); without the counter the reported clock stands in.
- **[S-2407]** Without a temperature tool's reading ([S-2434]) the temperature is the hottest ACPI thermal
  zone (`\Thermal Zone Information(*)\Temperature`), labelled "Thermal zone" because it is the board's
  zone, not the package; 1–150 °C or left out. Many desktop boards expose none (issue 95).
- **[S-2434]** The temperature's sources, in order: Core Temp's shared memory ([S-2435]), HWiNFO's
  ([S-2436]), the thermal zone ([S-2407]); nothing found is no temperature fact. The reading carries its
  source (`CpuTemperature`), and the fact is labelled "CPU package" for a tool's reading, "Thermal zone"
  for the zone. A mapping is opened read-only on the processor source's own tick and closed before
  its copy is parsed (`CpuSensorMemory`, pure): no handle is held between reads (it would outlive the
  tool and freeze its last figures), no faster retry, the tools' mutexes are not taken. The copy is
  what the layout or header claims and never past the view (`VirtualQuery`); a short, foreign or
  absurd memory is logged once per fault and skipped, a tool with no reading yet is simply absent.
- **[S-2435]** Core Temp: `CoreTempMappingObjectEx`, else the older `CoreTempMappingObject` (the same
  prefix, 4-byte packing, alcpu.com/CoreTemp/developers.html). The hottest core of every CPU
  (`fTemp[cpu × uiCoreCnt + core]`, counts at most 256 cores, 128 CPUs); `ucFahrenheit` is converted,
  `ucDeltaToTjMax` is subtracted from that CPU's `uiTjMax`; both at once is refused (the TjMax unit is
  undocumented). 1–150 °C or left out.
- **[S-2436]** HWiNFO: `Global\HWiNFO_SENS_SM2` (hwisenssm2.h, packed). Only while HWiNFO's "Shared
  Memory Support" is on, which is off by default and in the free version switches itself off after
  12 hours (HWiNFO writes "DEAD" into the signature: absent, not corrupt). A temperature reading
  (`SENSOR_TYPE_TEMP`) whose English label (`szLabelOrig`, whatever the user renamed it to) is, best
  first, "CPU Package", "CPU (Tctl/Tdie)", "CPU (Tdie)", "CPU Die (average)", "CPU (Tctl)", "Core Max";
  the hottest of the best label present (two sockets); the unit (UTF-8 in layout 2, else ANSI) ending
  in F is converted. A board's "CPU" zone is not one. Signature "HWiS", at most 65 536 elements and
  64 MB, elements no smaller than their version-1 layout, arrays after the header.
- **[S-2408]** Memory details: in use as the hero, "41% of 64 GB"; available, committed (of the commit
  limit), cached, paged pool, non-paged pool (`GetPerformanceInfo`), page file.
- **[S-2409]** The page file is `NtQuerySystemInformation(SystemPageFileInformation)`, every file summed
  ("None" without one); the chain is walked defensively and never past the buffer.
- **[S-2410]** Top processes: one `NtQuerySystemInformation(SystemProcessInformation)` snapshot (no
  per-process handle, so elevated and protected processes are counted), parsed in Core; memory is the
  private working set (Task Manager's column). One hub read per due tick for every list ([S-2501]). The pinned buffer goes with the last list. The idle process is left out.
  A new widget: the removed process list ([S-1902]) stays removed.
- **[S-2433]** Every process row leads with its program's icon, as Task Manager's does: the executable's
  path (`QueryFullProcessImageName` on a limited handle) is asked once per process, pid and start time
  (`ProcessPathCache`, shared by every list; an exited process is forgotten, one Windows will not open
  is remembered as having none: a system process is drawn by its image in System32, as Task Manager does), and the icon once per path and pixel size (`AppIcons`, the shell's
  icon at the row's 16 DIP times the display scale; a packaged app's package logo). A group takes its
  oldest member's. A row without an icon, or before it is extracted, shows the program glyph.
- **[S-2437]** End task (issue 197): a process row's right-click menu (and the Menu key's) ends what the
  row stands for, as Task Manager's End task does: grouped, every process of that program; one row per
  process, that process (its key is the name and the pid, `ProcessTable.RowKey`, so two "svchost" rows
  are two rows; `ProcessTable.Members`). Only in that menu, never a button on the row. The host
  (`ProcessControl`, `ProcessEnding`) opens the process to terminate it and ends it only while its pid
  still names the process the list read (its start time), so a pid Windows has handed on is never
  ended. Themia runs unelevated: a protected, elevated or system process refuses, and a notice says
  Windows would not let it be ended. Themia's own row has no End task. A detail list's rows have no
  menu of their own unless their part gives one (`DetailPart.RowMenu`); without one a right-click is
  the card's menu, as before.
- **[S-2411]** `SortBy`: processor (default) or memory; ties by the other figure, then name.
- **[S-2412]** `MaxRows`: 0 (default) lists every process the list can scroll to, 1–50 a ceiling.
- **[S-2413]** `GroupByName` (on): every process of one image name is one row, figures added, the count
  after the name ("brave (26)").
- **[S-2414]** A share is processor time over wall time over the logical processors (the CPU meter's
  scale). A process first seen reads 0; a pid with another start time is another process and starts over.
  From 280 px a row shows the other figure beside the ranked one.
- **[S-2415]** Network details, laid out as Task Manager's Wi-Fi/Ethernet page: download as the hero,
  captioned with the connection it comes over ("Download over Wi-Fi", the busiest adapter); then upload,
  the busiest adapter's local address, received and sent since Themia started, its link speed
  ("866.7 Mbps", "1 Gbps", as Settings writes it) and the public address when asked for, most wanted
  first, so a small tile keeps the rates; from the network meter's own shared reader ([S-1190]), so
  the counted adapters are the meter's ([S-1192]).
- **[S-2416]** One list row per counted adapter that has an address or has moved data this session
  (an idle virtual adapter, Wi-Fi Direct's "Local Area Connection* 10", still counts in the totals but
  gets no row), busiest first: its kind's icon (Wi-Fi, cable, cellular, other: `NetworkRules.MediumOf`
  the interface type), its name, "↓ x" at the right ("↑ y" beside it from 280 px), and its address and
  link speed under it. No adapter is "No connection".
- [S-2417]: moved with the Network details widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.NetworkDetails/`).
- **[S-2418]** The answer is shown only when it is one IPv4 or IPv6 address; it is trusted 10 minutes and
  asked again at once after the machine's addresses change. A failure is logged without the body.
- **[S-2419]** An adapter's local address is its first IPv4 that is not link-local, else its first global
  IPv6; re-walked at most every minute, or when Windows says the addresses changed.
- **[S-2420]**..**[S-2422]** *(Retired with Disk space, [S-6275]: Drive is one drive's card in a bar or a ring.)*
- **[S-2423]** Battery details: the charge and status from `GetSystemPowerStatus` (the meter's source);
  the rest from the battery class driver (`SetupDi` + `IOCTL_BATTERY_QUERY_TAG/_INFORMATION/_STATUS`),
  every battery summed, devices re-listed once a minute, one hub read per due tick (every five seconds
  and on a power event, [S-2514]); asked only when Windows says there is a battery.
- **[S-2424]** `BATTERY_INFORMATION` and `BATTERY_STATUS` are parsed in Core: all-ones and zero are
  unknown, the chemistry code becomes a word, the temperature level is tenths of a Kelvin (−40–120 °C).
- **[S-2425]** Status: Charging, Fully charged, Plugged in, not charging, On battery. Then time left
  (Windows' estimate, else remaining over the drain) or full in (while charging), health (full over
  design), full-charge and design capacity, cycles, charge or discharge rate, voltage, temperature,
  chemistry and maker, each only when reported; a relative-capacity battery shows no Wh or W.
- [S-2426], [S-2427], [S-2428], [S-2429]: moved with the Ping details widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.PingDetails/`).
- **[S-2430]** GPU details: video memory in use (`\GPU Adapter Memory(*)\Dedicated Usage`, summed per
  LUID) as the hero, "of 7.8 GB video memory" (DXGI); load ([S-434]), shared memory, temperature, fan, GPU
  clock, memory clock. An integrated adapter leads with its shared memory. From the GPU meter's own shared
  sampler (the memory counters join its query); the header's label names the adapter ([S-430], [S-2260]).
- [S-2431]: moved with the GPU details widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.GpuDetails/`).
- **[S-2432]** Sensors from the graphics kernel, any vendor's WDDM 2.4+ driver (`D3DKMTQueryAdapterInfo`
  adapter and node performance data and caps, one handle per adapter per sampler session): temperature
  1–150 °C, the fan only when the caps name one, clocks only when non-zero; a driver that answers none is
  asked no more.
- Not ported / dropped: CPU fan speed and package temperature (no Windows source without a driver or a
  third-party monitor; the original's hardware-monitor bridge stays out); GPU power draw and
  encode/decode (D3DKMT gives power only as an unlabelled share, NVML is NVIDIA-only); the per-core grid;
  IOPS and per-disk I/O (the disk meter is I/O); the Wi-Fi name (Windows 11 gates it behind location
  consent).

## Feed, stocks and the web widgets: parity and the whole tile (OFF-E)

Ported from `widgets/online/{rss,stocks,video-stream,web-element}` on main, renumbered; the rules
live in Core (`NetworkGuard`, `NetworkCacheStore`) and the SDK (`FeedParser`, `SiteIcons`,
`MarketSearch`, `MarketQuote`, `ExchangeTime`), tests in `FeedWidgetTests`/`FeedParserTests`/`StockWidgetTests`.
Rss and Stocks are modules (`src/Widgets/Themia.Widget.Rss/`, `src/Widgets/Themia.Widget.Stocks/`, their
own tags in their `SPEC.md`).

- **[S-1300]** Feeds and quotes survive a restart (classic S-23/S-29): the last good answers are
  saved to the sealed `web` shelf of the offline store ([S-2660]; never in an export;
  schema-versioned; one writer; saved after each fetch that brings a new answer). At launch they are
  read back before the windows are built and marked restored: drawn at once, fresh only for their
  real age ([S-2662]), which the card menu's Refresh line says ([S-7042]), so a fetch follows once the widget's interval has passed. A widget shown takes what
  the feed sync holds at once (fixed: an Rss widget said "Loading" until its feeds' next fetch). Entries older than 7 days, from the future, or not a usable feed/quote are
  dropped; feed links are re-checked; a corrupt file is set aside as `.corrupt`. Only the app wires
  it; a debug render never reads or writes the user's file. This reverses the earlier "in memory, so a
  cold start never shows old data as current" note: the Refresh line says how old it is.
- **[S-1301]** Network guard (classic `netguard::check_url`, S-08b): an address that comes from a
  user or a stranger and is then fetched by the app — every feed, every feed item's site icon, a
  pinned web page in the renderer and the picker — is refused when its host is, or resolves to
  (every address, not the first), loopback, private (10/8, 172.16/12, 192.168/16), link-local
  (incl. 169.254.169.254), CGNAT 100.64/10, 0/8, 240/4, a documentation range, ::1, ::, fe80::/10,
  fc00::/7 or 2001:db8::/32 (IPv4-mapped judged as IPv4). Feed requests follow redirects themselves
  and check every hop; https→http is refused. An unresolvable name is refused unless a proxy will
  resolve it (classic NG-07), but an IP literal is checked even then. No switch: the local network
  is reached only for the host of an address the user typed ([S-3840]). A refused feed says so in the
  widget ("x is on the local network. Themia goes there only for an address you typed yourself").
  Every request, the camera stream and the fixed public services included, connects through the
  guard ([S-960]), and the loopback hardware monitor is reached as "this machine only" ([S-967]).
- **[S-1304]** Site icons (classic S-26/S-27, the Google s2 service replaced): fetched from the site
  itself, `scheme://host[:port]/favicon.ico` of the item's link — no third-party service. Guarded
  ([S-1301]), 64 KB cap, the bytes must be ICO/PNG/GIF/JPEG/BMP (not HTML, not SVG), one request per
  origin per session shared by every row and widget, a missing icon retried after 6 h; decoded at
  2× the 16px drawn size. A globe stands in until it arrives or when there is none.
- [S-1302], [S-1303], [S-1305], [S-1306]: moved with the Rss widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Rss/`).
- [S-1307], [S-1308], [S-1310], [S-1311], [S-2770], [S-6165], [S-6166], [S-2776], [S-2777]: moved with the Stocks widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Stocks/`).
- **[S-1309]** Times on the chart are the exchange's (classic S-36): Yahoo's `gmtoffset` and
  `timezone` ("EDT") — not an IANA lookup, which needs ICU the app does not ship — "09:30" for 1 day,
  "Tue 15:00" for 5 days, "Mar 4" beyond, with the year from 2 years; local time when the reply has
  no offset. The badge and the chart (whose times are drawn on it) say which zone in their tooltip.
- **[S-2775]** One baseline for the colour, the change and the line (`MarketQuote.Trace`): the
  change is the price against the start of the shown range, and the line starts at that same
  point, so a falling colour never sits over a rising line. A listed symbol's baseline is Yahoo's
  `chartPreviousClose`, the close just before the range's first bar (the previous session's close
  on 1 day); `previousClose` may stand in on 1 day only (`ListedBaseline`); without either, the
  first bar. The baseline is drawn as the line's first point (at the first bar's time) when it
  differs from it. A coin's change is over its chart's series, from its first price; CoinGecko's
  24-hour change stands in only on 1 day without a series (`CoinTrace`). Fixed bug: the change was
  measured from the previous close while the line began at the day's first bar, so a day that
  opened below the close and climbed back part of the way (AAPL) drew a rising line in red; coins
  showed a 24-hour change over any range. The colour follows the regular-session price; a dashed
  extended-hours tail has its own line and colour ([S-1308]).

Not ported, on purpose: RSS feeds as shared *connections* and the classic "Manage connections" page
(S-08/S-29 — feeds stay a widget setting, recorded earlier); per-widget `label`/`hideHeader` (card
title and header placement); demo mode; single-click open of a feed item
(double-click, recorded); the original's IANA time-zone names in labels (offset + short name
instead, see [S-1309]).

## Web widgets: size ladder and overlay

Round 3 parity and layout for the video stream widget (package OFF-E): the widget is a module of its
own now (`src/Widgets/Themia.Widget.VideoStream`), and its chrome went with it.

- [S-1312], [S-1313]: moved with the Video stream widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.VideoStream/`).

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
`FamilyType.StepsAt`), tested in `DesignEngineTests`.

- **[S-820]** Accent override. Empty = the Windows accent (`UISettings` Accent, AccentDark1..3,
  AccentLight1..3). A colour of the user's own is walked into seven shades (HSL lightness, hue and
  saturation kept; `DesignLook.Shades`). The framework's accent brushes are found once, by colour,
  before anything is recoloured (every `AccentFill…`/`AccentTextFill…`/`SystemControl…Accent…`
  brush is one of the seven shades at some alpha) and recoloured in place, alpha kept; the
  `SystemAccentColor*` resources are set for later lookups. So template `{ThemeResource}` uses
  follow, in widgets and in the menus. `SystemFillColorAttentionBrush` is an accent alias with no colour
  resource of its own (Light2 in dark theme, the accent itself in light) and is mapped like the others.
- **[S-821]** Text size (`FontScale`, 0.5..2) scales every family step (`FamilyType.TextScale`, size
  and line height together), so a chooser still picks real steps and a layout sheds rather than
  overflows. Live: before a card's view re-lays out on the state change, text already on the card
  whose size was set on the element is rescaled by the ratio; text built afterwards comes from the
  scaled ramp. Icons keep their size (widget scale, [S-850], scales everything).
- **[S-822]** Theme. "Use system setting" is resolved by the engine (Windows background colour) and
  every window root gets the explicit Light/Dark. `ThemeBrush.Of` hands out one engine-owned
  brush per key, coloured from the effective theme's dictionary and recoloured in place on a theme
  or accent change — a brush looked up from code used to be the app's start-up theme, so code-made
  text stayed white in a light card. A Windows theme or accent change (`ColorValuesChanged`) re-runs
  the apply path.
- **[S-823]** Widget background ([S-6560]): **Default** is Themia's own card ([S-2100]); a colour of
  the user's own replaces it, exactly that colour at its opacity (0..1); **Transparent** has no fill.
  *(The old 0.2..1 opacity slider and the 60 % "translucent" card are retired: a colour with an
  opacity replaces them.)*
- **[S-2100]** **Themia's own look** (owner's decision 2026-11-12: the widgets come together on the
  desktop instead of reading as a wall of opaque slabs). The default card is one neutral colour per
  theme at one opacity (`DesignLook.Card`): dark `#1C1C1C` at 72 %, light `#F9F9F9` at 78 % over the
  blurred wallpaper ([S-2110]), and 88 % / 90 % where none lies under it ([S-2111]). Neutral on
  purpose: the card takes its colour from what is behind it, so the look fits any wallpaper. It
  follows the effective theme, live when the theme is System ([S-822]). The card paints that one fill
  across the whole card, header included; there is no Fluent base-and-tint pair any more. The item
  layer's default (the Fluent subtle and control fills) and the theme's text roles read on it in both
  themes. Corner radius 8 ([S-6482]), a hairline ([S-2102]), no header band ([S-2103]), no gap ([S-2247]).
- **[S-2102]** The theme's hairline is Themia's own pair, not Fluent's `CardStrokeColorDefault`
  (black at 10 % / 6 %, darker than a dark card and so no edge on one): white at 10 % in dark, black
  at 8 % in light, a 1px solid line (`DesignLook.DarkHairline`, `LightHairline`), drawn over the card
  at the border opacity.
- **[S-2103]** **No header band.** By default the header has no fill and no line of its own: it is
  the card. A translucent card used to show a darker strip with a seam where the header was, because
  the header painted a second copy of the card's fill (faded by the header opacity) and the body's
  fill reached 1 DIP under it; both are gone (`WidgetCard`: one `_fill` across the 3 × 3 content grid).
  A header colour the user sets (`DesignLook.HeaderFill`) is drawn over the card at the header
  opacity; without one the header opacity does nothing.
- **[S-824]** Border: style None/Solid/Dashed (3-on-2 stroke widths)/Dotted (round dots two widths
  apart), width 0..8, colour (empty = the theme's card stroke), opacity 0..1. Drawn as a shape over
  the content and inset with the gap; none on a transparent widget. Where cards touch, one line between them ([S-2242]).
- **[S-6481]** Every border setting shows (`DesignLook.Ink`). A colour of the user's own is drawn
  exactly, opaque, and the border opacity fades the line once (the element's opacity, never also the
  brush's). The theme's colour is Themia's hairline ([S-2102]) for a solid line up to 1px, and the
  strong stroke (`ControlStrongStrokeColorDefault`, #8BFFFFFF dark /
  #72000000 light) for a heavier, dashed or dotted one. Drawn in the hairline's 6-10 %, a 4px,
  dashed or faded line looked exactly like none, so width, style and opacity all seemed ignored.
  The opacity never swaps the ink, so fading is monotone; at the 1px hairline it is subtle by nature.
- **[S-6480]** `--widget-png … --design key=value,...` draws the cards with appearance values (`border`,
  `borderWidth`, `borderOpacity`, `borderColor`, `radius`, `background` and `items` (a fill, [S-6569]),
  `opacity` and `itemOpacity` (their colours' opacity), `header`, `headerOpacity`, `accent`, `font`,
  `itemText`, `gap`, `blur` (on|off, [S-2110]), `preset`), so a look is checked without ⚙ › Appearance. The text is read by Core's one
  `DesignText` parser, which a store scene's `design` uses too ([S-1075]): pairs in order, a colour
  `#RRGGBB` or empty for the design's own; a pair it cannot read is named (the render prints and skips it,
  a scene is refused). Tests `SceneParserTests.S6480_`.
- **[S-825]** Corner radius 0..32 on the fill, tint, border and content clip; square where a joined neighbour covers the corner ([S-2241]). The edit markers stay square ([S-31b]).
- **[S-6482]** The default corner radius is 8px (`DesignState.DefaultCornerRadiusPx`; a radius that is
  not a number loads as 8): Windows' own radius for a top-level surface, the same as Themia's menus
  and flyouts (`ShellMetrics.OverlayRadius`), so cards and edit chrome agree; the family's inner
  radius stays 4. The Default preset reads it from a fresh design ([S-831]); the other presets keep
  their own ([S-2121]).
- **[S-826]** Font: `system` (Segoe UI Variable), `sans` (Arial), `serif` (Georgia), `mono` (Cascadia
  Mono, Consolas), or any installed family name; a value that is not a plausible family name is the
  system font. Applied to the card content through a presenter, so text inherits it; icon glyphs
  name their own font and are untouched. Text boxes keep the control font.
- **[S-827]** The font picker lists every installed family (GDI `EnumFontFamiliesExW`, off the UI
  thread, cached until the installed fonts change, [S-6443]): vertical `@` faces dropped, duplicates
  folded, sorted. Each entry is drawn in its own font ([S-1723]).
- **[S-828]** Header background (empty = none: the header is the card, [S-2103]) and header opacity
  (0..1, the header colour's); the strip reaches the card's edges and follows its corners.
- **[S-1761]** *(Retired by [S-2103]: the header no longer paints a copy of the card's fill.)* The header opacity always fades the header strip (it did nothing while the header had
  no colour of its own, which is the default — the widget's fill was painted under the header too, so
  a header sharing it could not look any different). The card's fill now paints the body only, in
  the content grid beside the header (`WidgetCard.ApplyBodyFill`), and the strip has its own fill:
  a header colour at the header opacity, or the widget's fill and Fluent lift at the widget's opacity
  times the header opacity (`DesignLook.HeaderFillOpacity`). 100 % looks as before; 0 % leaves the
  strip clear over the desktop. The body's base reaches 1 DIP under the strip, so no anti-aliased seam
  opens between the two where their edge falls between pixels (widget scale, an intro, fractional DPI).
- **[S-829]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-830]** Design presets (the original's six): Default, Midnight, Daylight, Glass, Terminal, Paper.
  Each owns 15 fields (theme, accent, font, widget background (one fill choice, [S-6565]), header
  fill + opacity, border style/width/colour/opacity, corner radius, widget gap, blur behind widgets
  ([S-2120]), item background (a fill choice) + item text colour, [S-6515], [S-6545]) and
  never widget scale, text size or motion.
- **[S-2120]** Blur behind widgets ([S-2110]) is a preset's: a frosted look and an opaque one differ.
- **[S-2121]** Each preset is one coherent look on the new base, and none draws a header band or an
  item pane of its own (`DesignPresets`): **Default** the fresh design ([S-2100]); **Midnight** dark,
  opaque near-black `#0B0B0F`, a `#7C8CFF` hairline at 18 %, radius 8, gap 10, blur off; **Daylight**
  light, white at 70 % frosted, a black hairline at 8 %, radius 8, gap 12; **Glass** dark, `#1B2030`
  at 50 % frosted (thinner than Default), no border, radius 16, gap 16, the items on their Fluent
  fills (its old header strip and item panes made every card look striped); **Terminal** mono, square,
  opaque `#050D07`, a green line, blur off; **Paper** serif, opaque `#FAF7F0`, a warm hairline,
  radius 4, blur off.
- **[S-831]** Default is read from a fresh `DesignState`, so a fresh install reads as Default.
- **[S-832]** Applying a preset writes its fields in one mutation and leaves every other field alone.
- **[S-833]** The picker shows the matching preset, or Custom; only owned fields are compared, colours
  case-insensitively, numbers within 0.001.
- **[S-6483]** Picking a preset while the design reads as Custom first asks beside the settings menu
  (the app's one "are you sure?", [S-3527]): "Replace your custom look?", naming the preset and what it
  replaces (theme, colours, font, border, corners, spacing) and what stays (text size, widget scale,
  motion); Replace, or Cancel. Cancel, Escape or a click away write nothing, and Style still says
  Custom. A design that is a preset is never asked about: nothing of the user's is lost. The rule is
  `DesignPresets.ReplacesCustomLook`.
- **[S-6510]** **Two layers.** The card's background ([S-823]) and the **item background** — what the things a widget draws inside its card are
  filled with — are apart: a widget whose card is None still shows its items on the item layer.
  The item background is a fill choice like the card's ([S-6560]), app-wide on the design
  (`DesignState.ItemBackground`). Its Default draws exactly the fills the widgets drew before it
  existed ([S-6514]).
- **[S-6511]** *(Retired by [S-6561]: the item layer is validated as every fill choice is.)*
- **[S-6512]** A widget may have an item background of its own (`WidgetInstance.ItemBackground`,
  Default = follows the app-wide one, [S-6562]), set on the properties panel ([S-6566]). It wins
  whole (`ItemLook.Effective`); a duplicated widget keeps a copy of it. Each card owns
  one item palette (`Widgets/ItemPalette`) recoloured in place from its layer on every apply, so a
  widget's layer reaches its items and no other widget's, and nothing is rebuilt.
- **[S-6513]** One path for every item fill (`ItemFill.Paint`, `ItemFill.Button` for a button whose
  fills are template resources, `ItemFill.For` for many alike): an item says its role — **Row** (list
  rows, grid cells, crumbs, row buttons, a toggle's or power button's tile, the quote's
  commands), **Tile** (a round button's circle), **Chip** (calendar events and selected days, an
  empty year day, a flip clock's card), **Track** (meter bars, gauges, rings,
  the seek bar, signal bars, the sun's night bar, the year's days ahead)
  — and its state (Rest, Hover, Pressed, Selected, SelectedHover, Disabled), and the fill comes
  from its card's palette (`ItemLook.Ink`). With a colour every role rests on it at the layer's
  opacity; a state moves it toward the theme's contrast (white in dark, black in light): hover 8 %,
  pressed 4 %, selected 14 %, selected under the pointer 20 %; a track stands 20 % off it; disabled is
  the rest at half the opacity. Default is the Fluent fill as it is; Transparent is [S-6564]. A surface that
  is not an item (a list's scroll area, a whole view's hit area, a check ring) is `Fluent.Clear()`
  and never takes the layer.
- **[S-6514]** The default fills, by role (`ItemLook.ThemeKey`): Row `SubtleFillColorTransparent` /
  `Secondary` (hover, selected) / `Tertiary` (pressed, selected under the pointer); Tile
  `ControlFillColorDefault` / `Secondary` / `Tertiary` / `Disabled`; Chip `SubtleFillColorSecondary`
  / `Tertiary` (any lit state); Track `ControlStrongFillColorDefault` / `Disabled`.
- **[S-6515]** Presets own the item layer too ([S-830]). Every preset keeps the Fluent fills
  ([S-2121]: Glass's pane of its own striped its cards).
- **[S-6516]** *(Retired by [S-6568]: the Item colour and Item opacity rows are one fill row.)*
- **[S-6517]** *(Retired by [S-6569]: `--surface` is gone; `--background` and `--own-items` take a fill.)*
- **[S-6560]** **One fill choice** (`FillChoice`) for the widget background and the item background,
  app-wide (`DesignState.Background`, `DesignState.ItemBackground`) and per widget
  (`WidgetInstance.Background`, `WidgetInstance.ItemBackground`): **Default**, **Transparent**, or a
  **Colour** (`#RRGGBB`) with an **opacity** 0..1, stored as `{ "kind": "Colour", "colour":
  "#1B2030", "opacity": 0.55 }` (the kind case-insensitively on read). Default app-wide is Themia's
  card ([S-2100]) and each item's own Fluent fill; on a widget it is "follow the app-wide one". A
  Default or Transparent choice keeps the colour and opacity last picked, so going back to a colour
  finds it; they draw nothing until then and never make two choices differ (`FillChoice.SameAs`).
  Translucent is gone: a colour with an opacity is the see-through card now.
- **[S-6561]** On load (`FillChoice.Repair`, loudly, app-wide and per widget): an unknown kind is
  Default; a colour choice whose colour is not one is Default; a colour is normalised; an opacity that
  is not a number is 100 % and the rest is clamped to 0..1. A missing choice is Default, silently.
- **[S-6562]** What is drawn (`FillChoice.Effective`): a widget's own choice unless it is Default,
  else the app-wide one — the background and the item background each on their own.
- **[S-6563]** The card paints its effective background (`DesignLook.Card` → `CardFill`): Default is
  Themia's card for the theme ([S-2100]), a colour exactly that colour at its opacity, and
  Transparent nothing — no fill and no border ([S-824]); a colour at 0 % is not transparent and keeps
  its border. The header paints no copy of it ([S-2103]). The view bar reads the app-wide background
  unfrosted (it has no backdrop), never under 20 %.
- **[S-6564]** Items on a **Transparent** item background have no fill at rest, disabled or as a
  track; a lit item (under the pointer, pressed, selected) still takes the Row's subtle Fluent fill,
  so a list keeps answering the pointer and showing its selection. Default has no opacity of its own.
- **[S-6565]** Presets own both fills ([S-830]); the old colour + opacity pairs map onto a colour
  choice: Default is Default and Default; the others are [S-2121]'s colours.
  Applying one writes copies, never the preset's own objects.
- **[S-6567]** *(Retired by [S-3526]: the settings window's fill control went with the Appearance
  page; every fill is a menu's fill line, with Default, Transparent, the swatches, Opacity, Custom
  colour and the eyedropper, [S-3724].)*
- **[S-6568]** The settings menu's Appearance › Theme and colours has one **Background** line (the
  widgets' fill, Default reading "Theme colour") and Item background one **Background** line (the
  items'), both fill lines ([S-3724]); there is no separate opacity line. Settings import names each
  fill's change in words ("Theme colour", "Transparent", "#1B2030, 55%").
- **[S-6569]** Debug: `--design background=default|transparent|#RRGGBB[@percent]` and `items=…` set
  the app-wide fills of a render (`opacity=` and `itemOpacity=` their colours' opacity);
  `--widget-png … --background <fill> --own-items <fill>` the cards' own.
- **[S-6590]** A widget's own **background image** (user request 2026-10-25; `WidgetInstance.BackgroundImage`,
  a picture file's full path, absent for none): it covers the whole card, header included, under the
  fills and the content, clipped to the card's corners, centred and cropped to fill it
  (`UniformToFill`, `WidgetCard.Image.cs`). It is decoded near the card's own size (twice its pixel
  width, 640 to 2560 pixels), off the design pass, and a load that lands after the path changed again
  is dropped. A file that is not there, cannot be read or is no picture Windows decodes draws the card
  without it and is logged; the path stays, so the picture is back with the file. Copied with the
  widget, undone with its background ([S-6566]). Debug: `--widget-png … --background-image <path>`.
- **[S-6591]** The path rule (`BackgroundImage.IsImagePath`): full (a drive or a network share, never
  relative), at most 1024 characters, no character a path cannot hold, of a picture type Windows
  decodes (.png, .jpg, .jpeg, .jfif, .bmp, .gif, .tif, .tiff, .webp). A stored path that breaks it
  loads as no image, logged ([S-1900]'s validation, not a migration); whether the file exists is not
  asked at load.
- **[S-6592]** Over an image the widget's **Background** is a tint, not a card (`BackgroundImage.Over`):
  a colour of its own lies over the picture at its opacity (the way to keep text readable on a busy
  picture); Default and Transparent paint nothing, so the picture is the card. The header strip
  follows the same rule. The border is the card's as it would be without the image.
- **[S-6593]** The properties menu's Appearance submenu has a **Background image** line after the
  colours, its value the file's name (a dash for a mixed selection), with **Choose an image…**
  (Windows' file picker, starting in Pictures, picture types only; `FolderBrowse.PickFileAsync`) and
  **Remove image** (greyed while none is set). Both apply to every selected widget in one undo step
  (`QuickSet.SetBackgroundImage`; a path that breaks [S-6591] changes nothing). The three labels are
  English only so far.
- **[S-6570]** Whether a kind **has items** (`ItemKinds.Has`): its view paints rows, tiles, chips or
  tracks through the item layer ([S-6513]). Sixteen kinds paint none (Alarm, Audio visualizer,
  Brightness, Exchange rates, Gallery, Music (its seek bar is the Controls slider's rail, G5), Next
  meeting, Notes, Quote, Rain next hour, Recycle bin, the screen time chart and donut, Stopwatch,
  Video stream, Volume); Stocks' holding and Weather now's
  readings are Rows, and the quote's commands left its body for the header (G3). Each widget's descriptor states
  it (`Traits.HasItems`, [S-6905]) and it cannot drift: `ItemKindsTests` reads every view's source (and the shared parts
  it builds — the flip card, rings, meter bars — to the fixed point) and fails when a descriptor and
  what its view paints disagree either way.
- **[S-6540]** **Item text colour.** The item layer has a text colour too: the colour of the text and
  glyphs drawn on items (`#RRGGBB`, empty = the theme's text colours). App-wide on the design
  (`DesignState.ItemTextColor`) and per widget (`WidgetInstance.ItemTextColor`, empty = follows the
  app-wide one). Never automatic: no contrast is computed, the user picks it.
- **[S-6541]** It resolves on its own, apart from the item background (`ItemLook.TextColour`): the
  widget's own, else the app-wide one, else the theme's. A widget with a background of its own and
  no text colour of its own takes the app-wide text colour, and the other way round. A stored value
  that is not a colour reads as empty.
- **[S-6542]** The Fluent hierarchy is kept (`ItemLook.TextInk`, `ItemTextTier`): without a colour each
  tier is its `TextFillColorPrimary/Secondary/Tertiary/Disabled`; with one, primary is the colour at
  full strength and the others the colour at Fluent's own alphas divided by its primary's (dark
  #C5, #87, #5D of #FF; light #9E, #72, #5C of #E4: 77/53/36 % and 69/50/40 %). The item opacity never
  fades the text.
- **[S-6543]** On load a text colour that is not one is empty (the default), loudly, app-wide and per
  widget (`ItemLook.RepairTextColour`); a missing one is empty, silently.
- **[S-6545]** The settings menu's Appearance › Item background has **Item text colour** (a colour line,
  Theme colour as its default, the neutrals first). Presets own it and none sets one (Glass's dark
  items read with the dark theme's white text). Settings import names the change.
- **[S-6546]** **Where it reaches: what stands on an item.** One path: `ItemFill.Paint` of any role but
  Track makes the element an item, and every text, glyph and shape inside it drawn in a theme text
  colour (the four `TextFillColor*` tiers) is drawn in its card palette's text brush of that tier
  (`ItemPalette.Text`). A colour the view sets later is inked as it is set (a property-changed watch),
  new content when the item is painted again or loads; runs follow their text block. `ItemFill.Button`
  sets the button's foreground resources too. What is laid over an item without being inside it says
  so (`ItemFill.OnItem`: the round button's icon, a flip card's figures); an item that stops being one says so (`ItemFill.Release`: a toggle or power
  tile turned accent or critical, the round looks' tile). **Not reached:** the card's own text outside
  items (the header, hero readouts such as a clock face, a meter's figure, a day number on a clear
  calendar cell), tracks, and any colour that is not a theme text
  colour (accent, text on accent, status, event colours, the user's own).
- **[S-6547]** Debug: `--design itemText=#RRGGBB` sets the app-wide item text colour of a render;
  `--widget-png … --own-items <fill> --own-item-text #RRGGBB` give the cards a layer and a text
  colour of their own, the per-widget override.
- **[S-835]** *(Retired by [S-2100]: the default card is Themia's own; the text, accent, status and
  type below still are Windows'.)* The default look was Windows 11's, value for value, with the framework's own theme
  resources (WinUI 1.8 `generic.xaml`, read by key through `ThemeBrush.Of`, never copied):
  card = `CardBackgroundFillColorDefault` (#0DFFFFFF dark / #B3FFFFFF light) over
  `SolidBackgroundFillColorBase` (#202020 / #F3F3F3), i.e. #2B2B2B / #FBFBFB; a 1px
  `CardStrokeColorDefault` (#19000000 / #0F000000); a 4px card corner ([S-6482]: the user's
  decision over `OverlayCornerRadius` 8, which the `--reference` card still draws), 4
  (`ControlCornerRadius`) for controls and row highlights; text `TextFillColorPrimary/Secondary/
  Tertiary`; accent `AccentFillColorDefault` (Light2 dark / Dark1 light); status
  `SystemFillColorSuccess/Caution/Critical` (#6CCB5F/#FCE100/#FF99A4 dark, #0F7B0F/#9D5D00/#C42B1C
  light); no widget paints a success green ([S-6706]: a falling price is Critical, [S-1310]). Type ramp
  and 16px inset as the Windows widget guidance (Caption 12/16 … Title 28/36, 16px margins). The
  references: [Color](https://learn.microsoft.com/windows/apps/design/style/color),
  [Geometry](https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry),
  [Layering](https://learn.microsoft.com/windows/apps/design/signature-experiences/layering),
  [Typography](https://learn.microsoft.com/windows/apps/design/signature-experiences/typography),
  [Widget design fundamentals](https://learn.microsoft.com/windows/apps/design/widgets/widgets-design-fundamentals),
  and the Adaptive Cards designer's Widgets Board host
  ([widget-dark.json](https://github.com/microsoft/AdaptiveCards/blob/main/samples/HostConfig/widget-dark.json)),
  whose card (#2C2C2C / #F6F6F6, 1px #19000000 / #0F000000, radius 8) is a CSS approximation of
  the same tokens; the theme resources win where the two differ. Deliberate differences: no card
  shadow (the surface is clipped to the cards by `SetWindowRgn`, so a shadow outside them cannot
  show), and the header is Body Strong in the secondary colour ending 44px down rather than the
  board's own 48px attribution strip (the board draws that strip itself; it is not a widget's).
- **[S-836]** *(Retired by [S-2100]: there is no Fluent tint to inset; `--reference` still draws the
  stock card to compare.)* The card composited as a WinUI card does: the Fluent tint stops at a solid stroke's
  inner edge (`DesignLook.TintInset`; a `Border`'s background is `InnerBorderEdge`), so the stroke
  lies over the base alone (#1D1D1D / #E5E5E5 at 1px); dashed and dotted lines keep the tint
  full-bleed so their gaps do not show bare base. `DesignLook.Over` is the source-over rule.
  `--widget-png … --reference` draws a stock WinUI card (framework brushes, application theme) beside
  the first tier and prints the colours it should composite to, for a pixel comparison.
- **[S-837]** Tracks follow the control they stand for: a seek bar is a slider
  (`SliderTrackFill` = `ControlStrongFillColorDefault`, value `AccentFillColorDefault`, 4px), and so
  are the tracks that stand for a progress bar or ring (Pomodoro's ring). None uses the disabled fill, which reads
  as a control that is switched off. A habit's open dot and an empty heat cell are the family's
  track role ([S-6717], since G6).
- **[S-834]** *(Retired by [S-3526] and [S-3527]: there is no Appearance page; its rows are the settings
  menu's Appearance lines, in the same groups, each written in place.)*

### Blur behind widgets (FROSTED)

A translucent card over a busy wallpaper is hard to read. Themia's surface is on the desktop layer,
so what lies behind a card is (almost) always the wallpaper: the card shows the wallpaper itself,
blurred, under its tint. Windows' Mica, done by hand.

- **[S-2110]** **Blur behind widgets** (`DesignState.BlurBehind`, on by default; ⚙ › Appearance › Theme
  and colours, a switch right under Background, `appearance/theme/blur`; a preset's, [S-2120]): every
  card whose painted fill is translucent shows its display's blurred wallpaper under the fill, the
  header and the border, clipped to the card's rounded shape (`DesignLook.Frosts`). An opaque card, a
  bare one (Transparent, or a colour at 0 %) and one with a background image of its own ([S-6590])
  draw no blur. Settings import names the change.
- **[S-2111]** **Fallback, loudly correct.** Where no picture lies under the cards (the switch is off,
  Windows shows no picture Themia can read, the file cannot be read or decoded: logged) the default
  card draws without blur at a firmer opacity, dark 88 % and light 90 %, so text stays readable over
  a wallpaper it cannot soften (`DesignLook.DefaultCardOpacity`). A solid colour desktop is a picture
  of that colour, so the cards frost over it (the same colour blended under the tint). **Detected:**
  the wallpaper file, its fit and the background colour per display, as `IDesktopWallpaper` reports
  them, and their changes. **Not detected:** a live-wallpaper program drawing over the desktop that
  Windows knows nothing of (the cards then show the static wallpaper Windows has under it), Windows
  Spotlight's picture where Windows reports no file, and a desktop window of another program between
  the wallpaper and Themia.
- **[S-2112]** The picture (`WallpaperPicture`, Core `Desktop/Wallpaper.cs`): the display's wallpaper as
  Windows shows it there, at 1/12 of its pixels per side (`WallpaperLayout.Downscale`,
  `PictureSize`), BGRA premultiplied and opaque. A solid colour is a picture of that colour
  (`WallpaperLayout.Solid`). Where it comes from is a seam, `IWallpaperSource`: per display (its bounds
  in virtual screen pixels) the picture already placed for it, or none, and a `Changed` event.
- **[S-2113]** Placement, value for value with Windows' `DESKTOP_WALLPAPER_POSITION` (`WallpaperFit`,
  `WallpaperLayout.Place`): **Fill** covers the display, centred and cropped; **Fit** fits inside it,
  centred, the background colour around; **Stretch** is the display's size; **Center** the image's own
  size, centred (cropped when larger); **Tile** repeats it at its own size from the display's top-left;
  **Span** fills the bounding box of every display and each shows its part. `Plan` brings the
  placement into the picture (one image size for every copy), `Compose` lays the image over the
  background colour there (its own transparency shows the colour) and never reads outside either buffer.
- **[S-2114]** A card shows the part of its display's picture that lies behind it
  (`WallpaperLayout.Part`): its rectangle on the display, in DIPs from the display's top-left, scaled
  from the display's DIP size to the picture's; it moves with the card by the same fraction.
- **[S-2115]** The blur (`BoxBlur`): box passes over the pixel buffer, rows then columns, radius 3 at
  1/12 scale, three passes (close to a Gaussian of about 40 display pixels). Integer arithmetic, so it
  is deterministic; an edge repeats its last pixel (no darkening toward the display's edge) and no read
  leaves the buffer, whatever the radius. No package, no Win2D.
- **[S-2116]** Windows' answer (`WindowsWallpaper`, `Interop/DesktopWallpaper.cs`): `IDesktopWallpaper`
  through a function-pointer vtable (Native AOT: no built-in COM interop, no reflection, no
  `GetActiveObject`), off the UI thread in its own apartment scope: the monitor whose `GetMonitorRECT`
  is the display's bounds, its `GetWallpaper`, `GetPosition` and `GetBackgroundColor`, and every
  monitor's rectangle for Span. Every interface pointer and returned string is released by a `using`.
  The file is decoded by Windows' codecs straight to its size in the picture (`WallpaperFile`, EXIF
  orientation respected, colour-managed to sRGB). A file that is missing, unreadable or not a picture
  is logged and means none ([S-2111]).
- **[S-2117]** Changes are heard, never polled: the command window raises `WallpaperChanged` on
  `WM_SETTINGCHANGE` with `SPI_SETDESKWALLPAPER` (a new wallpaper, a slideshow step) and on
  `WM_SYSCOLORCHANGE` (the colour); a display change (`DisplaysChanged`) counts too. Nothing escapes
  the window procedure.
- **[S-2118]** One picture per display, shared (`Backdrops`, `AppServices.Backdrops`, keyed by the
  display's bounds): built when its first card asks, and again only on a change (read, decode, place
  and blur off the UI thread; a newer change cancels an older load), so it costs nothing while nothing
  changes. A change drops the pictures no card watches any more (a display gone or moved). A card
  (`WidgetCard.Backdrop.cs`) draws it as an `Image` the display's DIP size inside its surface's rounded
  clip, moved by minus the card's place on its surface plus the gap's inset: a drag, a resize, a gap
  or a display change moves an offset (`Canvas.Left/Top` watched, `ApplyGap`), never re-blurs. The
  frost is outside the content's scale ([S-850]); the fill, the header colour and the border lie over it.
- **[S-2119]** Debug renders: `--widget-png … --wallpaper <image path>` draws the cards frosted over
  that picture, the page being the screen: the page shows the picture itself behind them (placed as
  Fill) unless `--transparent`. A renderer of its own uses the one entry point
  `RenderWallpaper.Use(path, screen)` (an `IDisposable`; the screen in DIPs, the cards' `Canvas.Left/Top`
  on it) and may await `RenderWallpaper.WhenLoaded()` before its shot; every render card host then
  reports that screen and its backdrop.

Not ported, on purpose: the original's preset preview tiles (a drop-down lists them, through the
shared `FluentLayout` row like every setting); the blur that tracked opacity (the blur behind widgets
is one switch, [S-2110]); the eyedropper (optional in the audit). Motion settings are WP9's.

### Cards that touch (JOIN)

At a small gap neighbouring cards meet. Each used to round all four corners and draw its whole
border, so a stack showed a notch of wallpaper at every meeting point and a double hairline between
two cards. Cards that touch now read as one panel: round on the outside, one line between.

- **[S-2240]** **When the painted boxes of two cards touch along a side, they join** (`CardJoins.Outlines`,
  Core, pure: the painted box of each card in, an outline per card out). Two cards are joined on a side
  when that side of one lies on the opposite side of the other within the tolerance (half a device
  pixel, `CardJoins.Tolerance`: 0.5 DIP at 100 %, 0.25 at 200 %) and they overlap along it by more than
  the tolerance; cards that meet only at a corner do not. Only a card that draws a card surface joins:
  one whose background is not Transparent, or that has a picture of its own ([S-6590]); a bare widget
  (a desktop item, a sticker), a hidden one ([S-873]) and one being dragged ([S-2244]) join nothing and
  nothing joins them. The rule takes no radius and no border: two cards of different looks join by the
  same geometry, each drawing its own radius and line. With a gap above the tolerance nothing touches
  and nothing changes. Tests `CardJoinsTests.S2240_`.
- **[S-2241]** **Corners.** A card's corner is square when a joined neighbour covers it: on one of the
  two sides meeting there, a joined neighbour's span reaches that corner (within the tolerance).
  Otherwise it keeps the card's radius. A column of equal cards rounds only the top card's top corners
  and the bottom card's bottom ones; a narrow card on a wide one squares its lower corners while the
  wide card keeps the top corners the narrow one does not reach. The card's outline (`CardOutline`,
  physical sides; mirrored for a card laid out right to left) gives one corner radius per corner to
  everything that follows the card's shape (`WidgetCard.Join.cs`): the surface's clip (the frost, the
  fill, the view, a fault or setup overlay, hover and focus fills inside it), a background image, the
  header's own fill ([S-828]: its strip's outer corners, each only where the card's is round; a side
  strip maps its turned top corners onto the card's two on that edge). A surface with no round corner
  at all clips nothing in WinUI, so such a card clips its frost to its box itself (`ClipFrost`; only
  then, as a rectangle clip under a rounded one changes how the round corners are smoothed). Tests
  `CardJoinsTests.S2241_`.
- **[S-2242]** **One line between.** Where two cards join exactly one hairline is drawn. For a joined
  pair (first: the upper or left card; second: the lower or right one): the second leaves out its
  joined side when its neighbours cover that side whole; else the first leaves out its own when its
  neighbours cover it whole and every neighbour along it draws its side; else both draw (staggered,
  rare: where a first card keeps its line because one neighbour left its own out, another neighbour's
  stretch along it may show two). A side not drawn never moves the card's content: the border is
  drawn over the content and insets nothing. Tests `CardJoinsTests.S2242_`.
- **[S-2243]** **The border's path** (`CardOutline.Stroke`, drawn by a `Path`, which replaced the
  `Rectangle`): half the stroke inside the card, a round corner an arc at the radius less that half, a
  square corner a point; all four sides drawn are one closed figure (a free card draws as before), a
  side left out breaks the path there, and the sides beside it run on to the card's edge so they meet
  the neighbour's line with no notch. Solid, dashed and dotted borders follow the rule alike; a dashed
  or dotted pattern starts again at each run's start, so two neighbours' dashes need not line up
  across a join. Tests `CardJoinsTests.S2243_`.
- **[S-2244]** **One path, live.** The desktop (`DesktopWindow.JoinCards`) and the scene renderer
  ([S-1076]) both call `WidgetCard.Join` over a surface's cards (each card's painted box: its live rect's
  cell inside the gap). The desktop joins again whenever the layout is applied: a relayout (a view
  switch, a design change such as the gap slider or a preset), a card hidden or shown, a move, a resize,
  a keyboard nudge. A card being resized is free, every corner round and its whole border, until it is
  dropped; the cards a drag carries (with the selection, once the drag has moved) are free of the cards
  standing still but stay joined among themselves, so a dragged group stays one panel ([S-2457]); their
  former neighbours get their corners back at once. Edit mode's selection outlines, handles, markers, ghosts and the snap
  guides keep their own shapes; so does an intro's cover while it plays ([S-1007]).
- **[S-2245]** **The window's region follows the shape** ([S-872]): each card's piece is its painted
  box with its round corners cut round at its radius in device pixels and its square ones square
  (`CardFrame.Corners`; `DesktopLayer.SetHitRegion` takes each round corner's square out and puts a
  circle's quarter back), so no click falls through a notch where cards join and none is taken outside
  a round corner. A bare card stays a rectangle: its content may reach its corners. The union is cut
  to the work area last ([S-851]). Tests `CardJoinsTests.S2245_`.
- **[S-2246]** A preset's gap stays as it is ([S-2121]); at a gap above nothing nothing touches and
  the cards draw as before.
- **[S-2247]** The default gap is none, so widgets put side by side join without a setting: a column
  or a grid of them is one panel. The gap is still the design's to widen (⚙ › Appearance). A gap that
  is not a number is repaired to none. Test `CardJoinsTests.S2247_`.

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
- **[S-987]** Only an explicit quit ends the process: `Application.DispatcherShutdownMode` is
  `OnExplicitShutdown`, and every way out (Quit, the single-instance hand-off, the data purge, the
  renders) calls `Exit`. WinUI's default ends the process when the last window closes, and Themia has
  no main window: the display pass closes a surface before it builds its replacement ([S-844]), so a
  lone display that changed id, a dead surface being rebuilt, or every panel powering off at once
  exited without a log line (the original's #87 / [EX-01]).
- **[S-988]** An exception that escapes on a background thread is logged
  (`AppDomain.UnhandledException`) before the process ends, and a faulted task nobody awaited is
  logged and observed (`TaskScheduler.UnobservedTaskException`). The UI thread's own throws are logged
  and handled ([S-891] keeps one widget's fault in its card).

Not ported, on purpose: the original's Developer section and Logs page — developer mode, verbose
logging, the session list of records ([S-1724]); DevTools (no web view), demo mode and dev views (the
showcase views, [S-1810], play that role), the per-window webview log forwarding gate and console-noise
filter ([LOG-02], [LOG-03]; no webview), the dependency DEBUG cap ([LOG-06]; .NET libraries do not
log into `Log`). Not done here: the original's pre-release gate on the Google sign-in provider (a launch decision, not a developer feature).

## Mixer and Volume (SYS-P5)

`Themia.App/Services/CoreAudio{,.Worker,.Native}.cs` (replaces `AudioSessions.cs` and the polled
`AudioEndpoint.cs`) behind `IWidgetHost.Audio`; the widgets are modules (`src/Widgets/Themia.Widget.Mixer`,
`.Volume`, their `SPEC.md`) over the SDK's `AudioFeed`, `AudioLook`, `AudioDeviceLine` and
`{AudioRules,MixerRules,VolumeRules}.cs`, tests `AudioRulesTests`.

- **[S-1220]** One Core Audio worker serves both widgets (the [S-440] MTA thread, now named
  `CoreAudio`). The volume tile no longer polls: it is a subscriber that does not ask for sessions,
  and moves on the endpoint's `IAudioEndpointVolumeCallback`. `VolumeConfig.IntervalSeconds` and its
  settings row are gone. Each subscriber states what it wants (`AudioWant`: device, sessions);
  the worker binds exactly the devices wanted and releases the rest, and publishes one
  snapshot per subscriber per wake.
- **[S-1221]** `DeviceId` (+ `DeviceName`, remembered for display) on both configs; null follows
  Windows' default output (console role). A chosen device that is not connected is "*name* is not
  connected", never silently the default. The worker re-enumerates active render endpoints (friendly
  name from `PKEY_Device_FriendlyName`) on device added/removed/state/rename notifications. A blank
  id is repaired to null at load.
- **[S-1222]** Device picker: the Output device line on the widget's properties menu only
  (`AudioDeviceLine`, a search, [S-3722]: "Default output" first, then the devices connected as its
  submenu opens, enumerated off the UI thread, the chosen one with the dot; a chosen device that is
  not connected stays listed as "… (not connected)"), which binds the widget to a device. Neither widget offers a device
  picker of its own at any size (user decision: choosing the output is Windows' job, not the
  widget's); the device line, the volume caption's menu and the XL panel's device list are gone.
  Context action "Follow the default output" when a device is chosen. Inert in previews.
- **[S-1236]** The Output device line is offered only where it changes something
  (`AudioDeviceLine.Offers`, user request 2026-10-19): with two output devices or more connected,
  while a device is chosen (so the widget can always go back to the default), or when the devices
  could not be listed. With one device "Default output" and that device are the same sound, so the
  line is not there. The devices are read as the menu is built (`AudioDeviceLine.Outputs`, a
  `MenuLoads` read, [S-3746]): until the first answer the line is not there, and an answer that
  differs builds the menu again.
- **[S-1223]** *(Retired: the mixer has no level meter under its sliders (user decision). The
  `ShowPeaks` setting, `AudioPeaks` and the worker's `IAudioMeterInformation` sampling are gone:
  nothing in the worker is sampled any more, it sleeps until a notification or a write wakes it.)*
- **[S-1224]** *(Retired: neither audio widget shows the microphone; it is not their job. The
  `ShowMicrophone` settings are gone.)*
- **[S-1225]** Mixer keys (keyboard lease): Up/Down/Home/End move the row cursor (system focus rect on
  the row), Left/Right and +/− step the row 2 points, Page Up/Down 10, M or Space mute, Menu key opens
  the row menu, Escape lets go. With no row focused a row key only focuses the first row. `WidgetKey`
  gains `Plus` (VK_ADD, VK_OEM_PLUS), `Minus` (VK_SUBTRACT, VK_OEM_MINUS), `Mute` (M without Shift).
- **[S-1226]** Volume keys: arrows and +/− step 2 points, Page Up/Down 10, Home/End to 0/100
  ([S-2733]), M or Space mute. Ignored when `AllowControl` is off.
- **[S-1227]** *(Retired by [S-449] on the family's rows, G5 2026-10-29: the compact, two-line and
  one-line row styles by width (`MixerRules.RowStyle`) and their 32/48/40 heights are gone; a row is
  a Row block of 32 or 48 by the tier, whole rows only.)*
- **[S-1228]** Mixer rows by room: apps holding a session but silent are drawn dimmed; with
  `OnlyActive`, the silent apps it hid follow the list dimmed when all playing rows fit. The mixer is
  its rows and nothing else at any size: no device line (was: name, "N playing", chevron).
- **[S-1229]** *(Retired: the XL mixer has no side panel — no device name, master level beside the
  list, microphone or device list. Nor is the master a row of the list any more, [S-3251].)*
- **[S-1230]** *(Retired: the level meter and its `ShowPeaks` setting, and the `ShowSlider` setting
  are gone; the number is back as the tile's Readout, [S-2150].)*
- **[S-1231]** *(Retired: the volume tile lists no apps; that is the mixer's job.)*
- **[S-1232]** *(Retired: `VolumeRules.Caption`/`Readout` are gone; the level, muted and the device
  name are the tile's Readout now, [S-2150], the Volume module's [S-2151].)*
- **[S-1233]** Previews never write (sliders, buttons, keys and device picks are inert); they read
  the machine.
- **[S-1234]** Stepping lands on the whole-percent grid and clamps (`AudioRules.Step`).
- [S-2730]: moved with the Volume widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Volume/`).
- **[S-2731]** The slider lies along the tile's foot: it is the Controls block's row at the bottom
  inset on every shape ([S-6716]: the speaker a 28 icon button at its start, the rail the Track
  role, the value the accent), on the volume tile and the brightness tile alike. Growing a tile
  lengthens the slider and grows the level's Readout over it ([S-2150]); a column no longer stands
  it up (G5, 2026-10-29: the family has one Controls row, and `VolumeRules.Axis` is gone).
- **[S-2150]** A level tile (volume, brightness) is a meter over its controls (owner decision
  2026-11-12, "the widgets come together"; `LevelFace.Plan`, tests `LevelFaceTests`): the level is
  a Readout at the view's top, start-aligned, on the step its widest case ("100%") takes in the room
  the Controls row (28, and 8 over it) leaves, so it climbs 28 (S to Tall) or 40 (L, XL)
  exactly as CPU's reading; the step is chosen on the value alone, never lowered for a line under.
  The controls come first: a card too short for the floor step's line over the row (shorter than XS)
  is the row alone. The widget's own line under stands only where the step's line and it fit over the
  row (S and larger; not XS); else it gives way, never the value.
- **[S-2732]** The mouse wheel anywhere on the tile steps the level one arrow step (2 points) per
  notch (120); a touchpad's part-notches carry to the next event, turning back drops the carry
  (`AudioRules.Wheel`). Ignored when `AllowControl` is off; no keyboard lease needed.
- **[S-2733]** Home/End set the volume to 0 / 100 (`AudioRules.VolumeKey`); the mixer keeps
  Home/End for its row cursor.
- [S-2771]: moved with the Mixer widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Mixer/`).
- **[S-3251]** The mixer lists apps and nothing else (user decision): no master / current-volume
  row, no output device name or line, at any size, and no `ShowMaster` setting. The device's own
  level is the volume tile's job. The mixer still follows the default output (or the device chosen in
  its settings, [S-1221]) to find the apps. No widget draws a meter, so the worker opens none
  ([S-1223]).

The original Volume had no peak meter or device name; the audit added them and [S-2730] took them
out again, and [S-1223] took the mixer's meters out too.

## Widget polish 1 (POLISH-1)

Keyboard lease in the remaining lists, Accounts links, and four layout gaps.

- **[S-1560]** One key vocabulary: `ListKeys.Decide` takes the lease's `WidgetKey` (the separate
  `ListKey` enum and `RowFocus.Key(VirtualKey)` are gone). Keys a list has no use for (Left/Right,
  Tab, Space) decide `None` and go on to the surface; the Menu key (or Shift+F10) is
  `ListCommand.Menu`, the focused row's menu. Mail implements `IWidgetKeys` (no view
  `KeyDown`): Up/Down, Page Up/Down, Home/End, Enter opens, Delete deletes (Mail, where the account
  allows), Menu, Escape (also every lease end) clears the row focus.
- [S-1561]: moved with the Clipboard widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clipboard/`).
- **[S-1563]** *(Retired: no note list, [S-1363]; Notes takes no lease keys. The editor and the
  picker's rename box still request the keyboard for typing.)*
- **[S-1994]** *(Retired: no note list to type ahead in, [S-1363].)*
- **[S-1564]** `IWidgetKeys.WantsKeys` (default true): a view whose keys belong to a part shown only
  at some sizes says no while it is off screen, so the click does not take the keyboard. The card's
  `TakesKeys` honours it.
- **[S-1568]** RSS: the Menu key opens the focused item's menu.
- [S-1569], [S-1988]: moved with the Bluetooth widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Bluetooth/`).
- **[S-1571]** *(Retired: no summary line or strip at any size, [S-1427]; the header's "3/5" keeps
  the count.)*
- [S-1380], [S-6401], [S-1381], [S-1382], [S-1383], [S-1384], [S-1385], [S-1386], [S-1387], [S-1388], [S-1389], [S-1390], [S-1995], [S-1391], [S-1392], [S-1393]: moved with the Shortcuts widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Shortcuts/`).

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
  `NetRefusedException` carrying the sentence ("x is on the local network. Themia goes there only
  for an address you typed yourself"): `FetchStatus.Refused` in a widget, `AccountError.Refused`
  for an account. The camera stream is guarded too ([S-3840]); only the Windows media player it hands a
  real video to opens its own connection, to the address the guard just let through.
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
- **[S-965]** Retired: account servers followed the local-network switch, which is gone. A typed
  CalDAV/CardDAV or IMAP server on the local network is reached by [S-3843].
- **[S-966]** Redirects: both clients follow them by hand, never the stack, so every hop is a new
  request through the guard. One rule for which hops to follow (`RedirectRules.MayFollow`, [S-519]):
  http(s) only, never https→http, never from a public host into local space; at most 5 hops.
  `RedirectRules.IsLocal` uses the guard's address classes (CGNAT, documentation and embedded IPv4
  now count as local).
- **[S-967]** "This machine only" (`NetReach.ThisMachine`): the loopback hardware monitor's
  `localhost:8085` is fetched by a second client from the same factory whose connections admit
  loopback addresses only, whatever grant came along, and are never proxied.
- **[S-968]** One user agent for every HTTP and CONNECT request (PX-10); TLS 1.2 or newer for HTTP
  and IMAP; timeouts and caps: web 8 s connect / 20 s request / 4 MB body, accounts 10 s connect /
  30 s per request / 16 MB body, IMAP 30 s per job, 1 MB line, literal cap; favicons 64 KB.
- **[S-969]** Retired with the switch ([S-3840]). Nothing reaches the LAN without a grant, from the
  first request on.
- **[S-3840]** The local network without a switch (`Core/Net/LocalGrant.cs`, tests `LocalGrantTests`):
  a private, loopback or CGNAT address is reached only as the host of an address the user typed into
  a setting (a feed, a camera stream, an account's mail or DAV server), through a `LocalGrant` for that
  host the caller hands in. The grant is bound to its host: a redirect to another host, and every
  address read out of fetched content (feed links, pictures, favicons, DAV hrefs to elsewhere) carry
  none and meet the guard as before. A redirect from a public host into local space is refused
  outright ([S-966]). Granted requests ride their own client and pool (`GuardedHttp.CreatePair`), so a
  connection opened under a grant is never lent to a request without one.
- **[S-3841]** DNS rebinding: only an address that names the local network as typed earns a grant
  — an IP literal, a single-label name (`nas`, `localhost`), or a name under .local, .lan, .home,
  .home.arpa, .internal, .localdomain or .localhost. A typed public name (`rss.example.com`,
  `nas.mydomain.com`) earns none even when the local DNS points it at 192.168.x: the user cannot see
  where a public name resolves. Split-DNS setups type the address or the local name instead.
- **[S-3842]** What a grant can open: loopback, 10/8, 172.16/12, 192.168/16, 100.64/10, fc00::/7,
  fec0::/10. Never, whatever was typed: link-local (169.254/16 with the cloud metadata address,
  fe80::/10), 0/8 and ::, 240/4, the documentation ranges. A granted name that also resolves to one of
  those is refused whole ([S-962]). A proxy is no bypass: an ungranted literal is judged as before.
- **[S-3843]** Account servers: an IMAP server's host and a CalDAV/CardDAV collection's host (and,
  while adding, the address typed) earn a grant on every request of that account; Microsoft and Google
  never do. Discovery that redirects to another host goes on without one.

Not ported: proxy authentication (classic known limit; Windows stores no proxy credentials) and
proxy failover; the classic per-scheme+host proxy cache and its invalidation (PX-04) — .NET's
`DefaultProxy` owns caching and notices setting changes itself; the updater path (PX-09; no updater).

## Widget picker: search (WP5)

Search is `Core/Widgets/WidgetSearch.cs` over each descriptor's `Keywords` and `Providers` (their ids) ([S-6902]; ported from `widgetSearch.ts`,
`searchKeywords.ts`, WidgetPicker.SPEC S-01..S-11; tests `WidgetSearchTests`, the original's cases);
the menu it searches is the edit bar's add menu ([S-3501]), `Shell/WidgetPicker.cs`.

- **[S-940]** A query matches six weighted fields: label 1.0, curated keywords 0.9, the kind's name
  split on camel case 0.8, the add menu's folders it is in 0.55 ([S-3513]), account providers 0.55, description 0.45 (S-01).
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
- **[S-948]** *(Retired: a search lists its matches flat again, each with where it lives, [S-3515].)*
- **[S-949]** *(Retired: the keys walk the add menu's tree, [S-3514].)*
- **[S-957]** *(Retired: the render flag is [S-3506].)*

Not ported: picker row previews (`WidgetRowPreview`) and the group-by choice (`pickerGroups.ts`) — the
subjects are the grouping ([S-3510]); the pro lock (licensing is out of scope).

## Desktop integration and suspend (WP3)

Game mode, Windows' desktop icons, the click clean-up a non-activating surface needs, and start with
Windows for both installs. Rules in `Themia.Core/Shell/{GameMode,DesktopIconRules,AutostartRules,
ShellClickRules}.cs` (tests `DesktopIntegrationTests`); Win32 in `Interop/{UserNotificationState,
FullscreenWatch,DesktopIcons,DesktopFolderView,ShellClicks}.cs`; host in `Services/AppHost.Suspend.cs`,
`Services/Autostart.cs`; surface in `Shell/DesktopWindow.Integration.cs`; lines in Core
`Settings/SettingsMenu.General.cs`.

- **[S-920]** Game mode (the settings menu's "Pause during full-screen apps", [S-3525], off by default, original
  `game_mode.rs`/`gameMode.ts`): the widgets are suspended while `SHQueryUserNotificationState`
  reports an exclusive Direct3D app, a full-screen window (`QUNS_BUSY`: borderless games, full-screen
  video) or presentation settings, and resumed when it no longer does. One pure decision
  (`GameMode.Decide(enabled, state, foreground, suspended)`) from a fresh query on every check. The
  line says "Paused now" at its end while it holds the widgets.
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
  with their last subscriber), leaves the z-order controller and hides; the controller's hooks come
  out (it catches up on resume, [S-34a]) and the shared ticker pauses. A surface the display pass
  builds while suspended starts suspended. Screen time keeps recording on purpose ([S-459]: the game
  is what it counts).
- **[S-929]** Resuming re-tracks and shows each surface, rebuilds its widgets from the state (each
  fires at once, so nothing shows pre-game data), unpauses the ticker and requests a display pass for
  any mode or monitor change the game made.
- **[S-930]** The shell's "Show desktop icons" command (original DI-01..05), sent only by the pick of
  [S-5505] (⚙'s line is the desktop choice now, [S-5500]): posted as the shell's own command
  (`WM_COMMAND 0x7402` to `SHELLDLL_DefView`), so Explorer persists it and the desktop menu's check
  mark agrees; Themia never re-asserts or restores it (it survives quitting Themia, Explorer
  restarts and reboots: Explorer persists it, so nothing is re-applied on `TaskbarCreated`). The
  shell windows are looked up on every use, never cached: the shell's own `Progman`
  (`GetShellWindow`), else a `WorkerW` in between (a child of Progman on 24H2+, top-level before),
  and only a `SHELLDLL_DefView` of the shell's process that has its icon list
  (`DesktopIconRules.ChooseView`) — another program's `Progman`/`WorkerW` never gets the command.
- **[S-931]** The command is a toggle: sent only when the live state is known and differs; an
  unreadable shell gets nothing. The live state is the icon list's own `WS_VISIBLE` bit, not
  `IsWindowVisible` (which is also false while an ancestor is hidden, and then turned a "show" into
  a hide).
- **[S-932]** *(Retired by [S-5500]: ⚙'s line is Themia's own choice, so nothing adopts Windows' state
  into it; the icon list's show and hide hook now keeps the session hide, [S-5503].)* Windows owns the state; `Preferences.DesktopIconsVisible` is a first-paint cache. The
  switch adopts the live state on every visit and, while on screen, on the icon list's own
  `EVENT_OBJECT_SHOW/HIDE` (a WinEvent hook scoped to that one Explorer thread) — event-driven,
  replacing the original's 1 s poll. Adopting writes only on a known disagreement. The switch shows
  the live state, else the cache — never a position nothing was done about.
- **[S-939]** Applying the switch is single-flight and latest-wins (`DesktopIconRequests`): one
  worker thread, a request made while it works replaces any still waiting, so quick off-on-off
  flicks end as the switch was left. Each request sends the command at most once, with
  `SendMessageTimeout` (abort-if-hung, 3 s bound) rather than `PostMessage`: a posted command is
  still queued when the state is read back, so a quick second request toggled on a stale state
  (lost or doubled toggles, the switch and the desktop out of step). The read after the handled
  send confirms (`DesktopIconRules.Plan`); not found, not answered, or handled-but-unchanged is
  logged as a warning and not retried. When the worker is done it raises `Settled` and the switch
  adopts what Windows reached; while it works the hook's events are not adopted.
- **[S-933]** Start with Windows, picked by package identity: unpackaged → `HKCU\…\Run\Themia`;
  packaged (MSIX) → the manifest's startup task, id `ThemiaStartup` (`Package.appxmanifest` declares
  `desktop:StartupTask TaskId="ThemiaStartup" Enabled="false"`). One switch, same states and advice.
- **[S-934]** The Run value is `"<exe>" --autostart`; a startup-task activation counts the same.
  `Autostart.LaunchSource` tells a sign-in launch from a manual one (read once in `App.OnLaunched`).
- **[S-935]** A change that did not take says what to do (original AS-04/05): a note under the
  settings menu's Start with Windows line ([S-3525]; Windows switched it off → Settings › Apps › Startup; policy; access denied; other), with the
  raw error in small print under the advice, never alone. The switch stays clickable and shows what
  was reached.
- **[S-936]** "On" means it will actually run (original AS-07): the Run value must launch this
  executable (a stale path is off) and Windows' own switch (`Explorer\StartupApproved\Run`, low bit
  of the first byte = disabled) must allow it. Turning it on from Themia removes that record, the
  user's newest choice winning. Read fresh each time the settings menu opens; `Design.RunAtStartup` is a first-paint cache.
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

## Settings navigation icons (NAV-ICONS, retired)

The settings window's navigation pane drew its entries with full-colour icons, the way Windows 11
Settings does. The window is gone ([S-3585]), and so are the icons (`Assets/NavIcons`), their
notice and their tests.

- **[S-1600]** *(Retired by [S-3585]: there is no navigation pane to draw an icon in.)*
- **[S-1601]** *(Retired by [S-3585]: there is no navigation pane to draw an icon in.)*
- **[S-1602]** *(Retired by [S-3585]: there is no navigation pane to draw an icon in.)*
- **[S-1603]** *(Retired by [S-3585]: there is no navigation pane to draw an icon in.)*
- **[S-1604]** *(Retired by [S-3585]: there is no navigation pane to draw an icon in.)*

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
  nothing; every other card takes its painted rect, inside the gap, its corners as drawn ([S-2245]).
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
- **[S-1061]** Capabilities: `runFullTrust`, plus `radios` and `bluetooth` for the Bluetooth widget,
  and `location` for a place widget's Use my location ([S-2851]; the Wi-Fi name is still never read,
  [S-1762]). Not declared: `internetClient` (not enforced for full trust), and every broad capability.
- **[S-1062]** The manifest declares the `windows.startupTask` `ThemiaStartup` (disabled by default)
  that the packaged autostart ([S-937]) asks for. No event-log source is declared: MSIX can only
  register one with a message file, and Themia ships none; `EventLogSink` already writes under the
  unregistered source (Application log, "description cannot be found" preamble) and is best-effort.
- **[S-1063]** `LicenseService` (one, owned by the host) asks `StoreContext.GetAppLicenseAsync` at
  start for the durable **Pro** add-on (`StoreIds.ProAddOn`, a marked placeholder until Partner
  Center issues the id), single-flight; re-asks on `OfflineLicensesChanged`, after a purchase and on
  "Restore purchases". Purchases use `RequestPurchaseAsync`, with the window of the display whose ⚙
  asked handed to the Store through `IInitializeWithWindow` ([S-3572]). `Changed` fires on the UI thread. Without package identity
  the Store is never touched.
- **[S-1064]** Tier: no package identity: **Unrestricted** ("Developer build"); the Store's "owned":
  Pro; "not owned": Free; no answer: Pro only inside the offline grace, else Free. An unknown or
  corrupt state never unlocks Pro.
- **[S-1065]** Free tier: every widget type, at most **20 widgets** (all views) and **2 views** (the
  original allowed 10 widgets; raised so the first-run seed, [S-893], leaves room to add). Gates at every create: placement start and landing, paste (net of a cut's original;
  a selection pastes whole or not at all), duplicate and new view. A newly seen display creates nothing ([S-1705]), so it needs no gate. A refusal creates nothing and says why in a notice ([S-881]) whose "See plans"
  opens ⚙ › Themia Pro on that display, which says it once on top ([S-3573]). Nothing
  is ever deleted or hidden for being over the limit; moving, editing and deleting are never gated.
  Import is a replacement, not a create, and is not gated.
- **[S-1066]** Offline grace: 7 days from the last Store "owned" on this PC, when the Store cannot be
  asked (also before its first answer, so a Pro user never starts limited). A confirmation dated
  more than 10 minutes in the future gives none.
- **[S-1067]** Add-on ownership: an active licence whose SKU id is `<productId>/<sku>`. A product id
  that is not 12 upper-case letters/digits (the placeholder) is never owned and is not offered.
- **[S-1068]** *(Retired by [S-3570]..[S-3574]: the License page is ⚙ › Themia Pro; its
  `--settings-png License --license …` render is `--editbar-png`'s `-pro-*` pictures, [S-3583].)*
- **[S-1069]** The grace record is `%LOCALAPPDATA%\Themia\license.dat`, `themia-license:v1:<unix
  seconds>` sealed with DPAPI (current user), written atomically on "owned", deleted on "not owned",
  untouched when the Store is unreachable. Absent, unreadable, plain text or malformed: no grace.
- **[S-1070]** Uninstall, packaged: the data cannot be kept (Windows deletes the package's
  redirected AppData with it), so Uninstall Themia › offers no "also delete" line and says the data
  goes ([S-3582]); the
  folders are deleted in-process before the package is removed. Loose builds keep the choice ([S-1553]).
- **[S-1071]** Uninstall steps after the app has closed, in order: autostart off
  (`Autostart.SetAsync(false)`), then packaged: delete data, `RemovePackageAsync` on the own package
  (fallback `ms-settings:appsfeatures-app`); loose: start the `--purge-data` helper (only when
  there are folders), open Installed apps.
- **[S-1072]** ⚙ › About Themia, under Lometics, links what the Store listing gives too ([S-3580]): the privacy policy
  (`lometics.com/de/legal/privacy/` for a German interface, `/en/` otherwise: the two it is written
  in) and the support mailbox `support@themia.app` (`AboutLinks`).
- **[S-1073]** The package declares every interface language ([S-1100]) as a manifest `Resource`
  with the Store's code for it (`zh` is `zh-Hans`, `pt` is `pt-BR`, as the translation is
  Brazilian), so the listing shows them; English (`en-US`) is first, the default. It targets Windows
  11 (`MinVersion` 10.0.22000.0), the only Windows it is built and tested on.
- **[S-1074]** Debug: `--scene-png <scenes.json> <scene> <out.png> [--lang <code>] [--scale <n>]` draws one
  scene of a store scenes file ([S-1075]), a whole stand-in desktop of real widget cards, off-screen, to one
  PNG of exactly the display's width and height times `--scale` pixels (default 2, from 0.5 to 4: a
  1920 × 1080 display is 3840 × 2160) whatever the machine's DPI: the display is laid out in DIPs at the
  window's scale with layout rounding off (so an edge at any whole or half DIP lands on output pixels)
  and magnified by scale / DPI, and drawn in tiles of at most 4096 pixels (a `RenderTargetBitmap` scales a
  larger element down), each a whole number of the window's DIPs, stitched; the display is drawn
  transparent under its cards (a bitmap covers only what is drawn), so without a wallpaper the PNG keeps
  the cards' alpha. A bitmap of another size fails the run. One scene per process; the scene's theme is the
  application theme, chosen before the resources load. Exit codes: 0 written; 2 refused before drawing
  (the command line, the file, an unknown scene, kind or style, a missing wallpaper or a sample wallpaper
  this build does not paint, [S-2210]), each named on the console; 1 the drawing failed (a preview state a view does not have, a submenu the properties menu does
  not have, a card showing its error card, a tile of the wrong size), nothing written. The system widgets
  read the sample machine ([S-1931]), never this PC, and a scene's wallpaper is its display's: the cards
  frost over it ([S-2119]).
- **[S-1075]** The scenes file (`store/scenes.json`) is read by Core's `SceneParser`, a trust boundary: the
  whole file or a refusal naming the JSON path and the problem. `screen` (whole DIPs, 320 to 8192, default
  1920 × 1080) and `scenes`, each `name` (unique, `[a-z0-9-]+`), `like` (another scene, before or after it;
  what it does not set comes from that one; `wallpaper` or `edit` null drops the one it would take; a circle
  or a missing name is refused), `theme` (`dark`|`light`, default dark), `design` (the `--design` text, read
  by the one `DesignText` parser, [S-6480]; every pair must read: a colour is `#RRGGBB` or empty, `gap` the
  card gap), `scale` ([S-1080]), `taskbar` (`true`|`false`, default false: Windows' taskbar stood in,
  [S-2200]), `wallpaper` (a picture's path relative to the file, filled UniformToFill, or `sample:<name>`,
  [S-2210]), `widgets` and `edit`; what a `like` scene does not set comes from the other, `taskbar` too.
  A widget: `kind`, `at` (`[x, y, w, h]` DIPs, wholly on the display), and optionally `state`, `style`,
  `header`, `background`/`items` (fills), `itemText`, `scale` (0.5 to 2), `align`, `label`, `bare`,
  `config` (an object) and `selected`. A property the reader does not know or one written twice is refused,
  numbers are finite, closed sets are their names; a selection needs `edit` and `edit.open` a selection.
  Tests `SceneParserTests.S1075_`.
- **[S-1079]** A scene's widget has a name, for what points at it (the store's pages, `store/panorama.mjs`):
  its `id` (letters, digits and `-`), its kind where it gives none. Two widgets of a scene with one name are
  refused, so two of a kind need ids. Tests `SceneParserTests.S1079_`.
- **[S-1080]** A scene's `scale` is its display's content scale (Display ▾, [S-6535]: 0.5 to 2, default 1; a
  `like` scene takes the other's): every card's content is drawn at it times the widget's own, as on a
  display set to it, so a desktop made for a picture reads like one at 125 %. Tests `SceneParserTests.S1080_`.
- **[S-2200]** A scene with `"taskbar": true` is drawn with a stand-in of Windows 11's taskbar along the
  bottom of its display, over the desktop layer (over the cards, as on a desktop), so a picture shows where
  Themia lives (`TaskbarStandIn`). It is a render fixture standing for Windows' own chrome, as the stand-in
  wallpaper is ([S-2119]): not Themia UI, compiled into Debug builds only, its colours and sizes Windows'
  rather than the family's tokens. Tests `SceneParserTests.S2200_`.
- **[S-2201]** Its height is Windows' 48 DIPs times the scene's display scale ([S-1080]: 60 at 1.25), and
  everything in it is laid out at 100 % and magnified by that scale with it.
- **[S-2202]** With a taskbar a scene's work area ends above it: a widget whose `at` reaches into the
  taskbar's strip is refused by the parser, naming the widget's path and the height it must stay above
  (`Scene.WorkBottom`); in edit mode the properties menu's room and placement area is the work area
  ([S-1077]). Tests `SceneParserTests.S2202_`.
- **[S-2203]** Its look, as Windows 11 draws it in the scene's theme: the frosted wallpaper under it (the
  cards' backdrop, [S-2119]), a translucent neutral over that (`#1C1C1C` at 85 % dark, `#F3F3F3` at 85 %
  light) and a one-pixel hairline along its top; in the centre the Start logo (four squares in Windows'
  blue), the Search pill (200 × 32, radius 16: the magnifier and the word, the host's `menu.filter`, in the
  language drawn), Task view and the pinned apps, each a 24 DIP icon in a 40 DIP button, 4 between, the
  first in front (a highlight and Windows' wide accent pill), two more running (small pills).
- **[S-2204]** The pinned apps are the sample PC's (`SamplePc.TaskbarApps`, [S-1931]): File Explorer, Edge,
  Terminal, Notepad, Settings, Calculator, the Microsoft Store, at the fixed paths every Windows 11 PC has
  them (a packaged app as its `shell:AppsFolder` name), drawn through the process list's icon loader
  (`AppIconSource`) at the output's pixel size; one missing on the PC drawing it is left out, never a failure.
- **[S-2205]** On the right the tray: the hidden-icons chevron, Wi-Fi, volume and the battery (the sample
  PC's 78 %), the clock in two lines right-aligned (the sample moment's short time and short date,
  `SampleDay`, through `LocaleFormat` in the language drawn) and the notification bell, 12 DIPs right of
  the clock.
- **[S-2206]** On the left Windows' weather button: the sample day's condition glyph, temperature and
  condition (`SampleWeather`, `WeatherReport`, the region's unit), so it agrees with the scene's weather
  widgets.
- **[S-2210]** A scene's `wallpaper` may be `sample:<name>`: one of the sample pictures painted in code
  ([S-2056]) by its stable name, defined once beside the pictures (`SamplePictures.Slugs`: the file name's
  words in lower case joined by `-`, `lake-at-dusk`, `sunset-over-the-hills`, …) or `sample:cover`, the
  album cover. The parser checks the form (`sample:` and `[a-z0-9-]+`); the renderer, which knows the names,
  refuses one it does not have before drawing, listing those it has (exit 2). The picture is painted at the
  output's pixel size (the display's DIPs times `--scale`: 3840 × 2160 for a full-HD display at 2, sharp
  at 1:1), written as a PNG of the run's own under `%TEMP%\themia-sample\wallpapers` (named for the
  process, deleted when the run ends) and used exactly as a file wallpaper: filling the display, the cards
  and the taskbar frosting over it ([S-2119]). A file path keeps working. `SceneWidgetsTests.S1078_` checks a
  checked-in sample wallpaper's name as it checks a file wallpaper's existence. Tests
  `SceneParserTests.S2210_`, `SampleDayTests.S2210_`.
- **[S-1076]** A scene's widget (`SceneWidgets.Build`): the kind installed, its default config with the
  `config` fragment merged over it (`StateJson.Merged`; a property the kind's config does not have is
  refused rather than dropped, a value of the wrong type too), the style one of the kind's, then `bare`
  (transparent, no header, as the first-run desktop's) and over it what the widget names itself. Its card
  stands where the desktop puts it: the whole cell (`GridGeometry.ToPixels` of its fractions), the
  design's gap inside it (`ApplyGap`), its own content scale, the language's flow. Each card has a preview
  host of its own, so its `state` (applied as `--widget-png --state` does: a widget waiting for setup runs
  for the preview first) and a kit widget's recordings ([S-9929]) are its own; the hosts share one link
  board. A preview state's name lives in the view, so it is checked when drawn ([S-1074]). Tests
  `SceneWidgetsTests.S1076_`.
- **[S-1077]** A scene with `edit` is drawn in edit mode as the edit bar's pictures compose it ([S-3713]):
  every card in edit mode with the marker layer, the real bar (`bar`, default true) at the top centre with
  the selection's entry on it, the add menu hung under Add widget (`add`), the snap grid (`grid`, the real
  `SnapGridOverlay`, its lines on whole output pixels), and with a selection its page hung from its entry
  ([S-2343]), `open` the key of a row opened in it (the card's own rows `style`, `header`, `align`,
  `background`, `items`, `itemText`, `image`, `scale`; a module's own lines `w/<their words>`).
- **[S-1078]** `store/capture.ps1 [-Lang en] [-Only a,b] [-NoBuild] [-Jobs 4]` builds the Debug app into
  `artifacts/store` (unless `-NoBuild`), draws every scene of `store/scenes.json` (or those named) one
  process per scene, a few at a time, to `store/out/shots/<lang>/<scene>.png` (git-ignored; the console
  beside it as `<scene>.log`), and fails naming each scene whose PNG did not appear or whose process did not
  exit 0. A test reads the checked-in `store/scenes.json`: every scene parses, every kind, style and config
  fragment is one this build has, every wallpaper exists (`SceneWidgetsTests.S1078_`).
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
  on a card selects its unit alone (the card, or its whole group, [S-2451]); **Ctrl+click** adds the
  unit or takes it out without starting a drag; picking
  on a display that shows another view starts a new selection there. The last widget picked is the
  primary one, which single-widget commands (the picker's "Add to") mean.
- **[S-1581]** A left-button drag on empty surface draws a **rubber band** on that display; every
  widget whose cell it touches is selected, live, as the band changes. With Ctrl or
  Shift held at the press it adds to the selection on that display; without, it starts from nothing
  (a click on empty surface clears the selection). A card that draws nothing is not picked.
- **[S-1582]** **Ctrl+A** selects every widget on the display under the pointer.
- **[S-1583]** Every selected card shows it (accent markers, [S-31c]). A widget that is
  deleted or leaves the view leaves the selection with the change. Escape clears the selection
  before a second Escape leaves edit mode.
- **[S-1584]** Dragging a selected card moves **every selected widget on that display** (a selected
  group whole, [S-2452]; an entered member alone) by one
  translation: the block's bounding box is moved, its top-left snapped to the grid or an edge flush
  on the work area's ([S-06], [S-6609], Alt suspends both), and the move stops where the first of its
  widgets would keep less than the visible minimum on the work area ([S-6608]) — never resized — and
  each rect is then kept in reach ([S-09]). Offsets between the widgets never change. Resizing is per
  card and narrows the selection to it; a group's members are resized only once it is entered ([S-2453]).
- **[S-1585]** A ghost of several widgets shows **every widget's outline** at its offset in the
  block. An outline too small for the icon and name shows the frame alone.
- **[S-1586]** A payload lands with its bounding box centred on the pointer (a paste),
  or with the held widget centred on it (a drag handed to another display), then snapped and
  clamped as a block on the display under the pointer. The ghost and the landing come from the same
  calculation, so the widgets land exactly where the ghost was.
- **[S-1587]** One **placement session** for everything that produces or moves widgets: Add widget
  (a new widget of one kind), Paste (Ctrl+V and menu; a cut lands on
  this display or any other). No menu has Move: a widget moves by dragging it or with the arrow
  keys ([S-1970]). No command drops a widget at a computed spot
  any more. Click lands; Escape, a right-click or leaving edit mode cancels and changes nothing. The
  licence gate ([S-1065]) runs before the ghost appears (what the session would create: a copy of N
  widgets counts N, a move 0) and again on landing.
- **[S-1594]** Escape cancels a placement wherever the keyboard is: on another display, in another app, with
  the focus on nothing after the menu that started it closed. A surface hears keys only as the foreground
  window with the focus inside it, and a placement mostly starts when it is neither (user report 2026-10-17:
  Escape did nothing while placing). While a session is open a low-level keyboard hook on a thread of its
  own (`PlacementKeyWatcher`, the outside-click watcher's pattern, [S-815]) hears Escape and cancels on the UI
  thread; the cancelling press, its repeats and its release are kept from every other window, so one press
  does not also back out of edit mode ([S-1583]), and the hook stays on until that Escape is up. Escape with
  Ctrl, Alt or the Windows key is the system's and passes on. Cancelling changes nothing: a cut stays on
  the clipboard, its originals where they were, nothing is created ([S-1590]). Core `PlacementKeyFilter`;
  tests `S1594_`.
- **[S-1588]** A payload taken from the selection holds the selected widgets of that view, in view
  order, as a snapshot with original ids, and the groups it holds two or more members of ([S-2455]).
- **[S-1589]** A **copy** (paste of a copy) lands fresh widgets with fresh ids every time, so
  pasting one copy twice gives two sets. There is no separate Duplicate for widgets: Copy then Paste
  is that command.
- **[S-1590]** A **move** (paste of a cut) lands the **same** widgets: the originals are taken
  out of whatever view they are in now and the live instances — with any config change since the
  snapshot — take the planned rects. An original deleted meanwhile is put back from the snapshot.
  Until the click lands the
  originals stay where they are, faded; cancel leaves them and the cut as they were.
- **[S-1591]** Dragging a selection onto a display that shows another view hands **all** of it over
  in one landing ([S-76], [S-77]) and selects it there.
- **[S-1592]** Delete, Copy, Cut, Bring to front and Send to back act on the whole
  selection; the card menu says how many ("Delete 3 widgets"). A right-click on a selected card keeps
  the selection; on any other card it selects that card's unit (its whole group, [S-2451]) first.
  Layering keeps the order the selected widgets had among themselves, a group's members included. The
  card menu also offers Group, Ungroup and Remove from group where they apply ([S-2454]).
- **[S-1593]** A press on a card of a multi-selection keeps the selection so the drag that may follow
  carries all of it; released without moving (3 DIPs), it narrows the selection to that card.

Not done: a debug render of the multi-widget ghost (the outlines are pinned by
`S1585_*` instead). Not live-verified: everything pointer- and multi-monitor-driven here — the
rubber band, Ctrl+click, block drag clamping at the taskbar, hand-off of a selection across
displays, the ghost following the pointer across displays, Ctrl+A/Escape order.

## Groups and ready-made groups (GROUPS)

Owning files: `Themia.Core/Model/WidgetGroups.cs` (the rules), `AppState.cs` (`WidgetGroup`,
`ViewState.Groups`, `WidgetInstance.Group`), `WidgetSelection.cs` (the group rules of a press, a click,
Ctrl+click, Escape and Tab), `PlacementPayload.cs`, `WidgetTransfer.cs`, `LayoutHistory.cs`,
`Widgets/CardJoins.cs` (sets), `Model/ReadyMadeGroups.cs` (the recipes), `Scenes/ReadyMadeScene.cs`;
`AppHost.Groups.cs`, `GroupFrameLayer.cs`, `WidgetCard.cs` (the press), `DesktopWindow.Keys.cs`,
`WidgetPicker.cs`. Tests: `WidgetGroupsTests`, `GroupShellTests`, `SceneGroupsTests`, `ReadyMadeGroupsTests`.

Small widgets put side by side already draw as one panel ([S-2240]); a group is what makes such a
composition one thing to arrange: an optional lock over ordinary cards, never a card of its own.

- **[S-2450]** **State.** A view holds its groups (`Groups`: an id and an optional name); a widget holds
  the id of its group (`group`, not written when it has none). No nesting: a widget is in one group at
  most and a group holds widgets only. A group of fewer than two members does not exist: load
  validation repairs the groups after the widgets (a group with a blank or repeated id is dropped, a
  blank name is none, a widget naming a group its view lacks is lone, a group of fewer than two
  dissolves), logged with the other repairs, and every mutation settles the same rule
  (`WidgetGroups.Settle` in `AppHost.Mutate`), so a delete, a cut or a move that leaves one member frees
  it. Export and import carry the groups with the views ([S-494]); the assistants' `list_widgets` says
  each widget's group and each view's groups (id, name) ([S-9971]).
- **[S-2451]** **Selection: the unit is a group or a lone widget.** A press on a member of a group that
  is not selected selects the whole group; a click (released without moving, [S-1593]) on a member of
  the group that is selected alone **enters** it: that member alone is selected, inside the group
  (`WidgetSelection.Entered`). Inside, a press on another member enters that one; a press outside
  selects that unit and leaves the group. **Escape** steps out: an entered member to its whole group,
  a selection to nothing ([S-1583]). **Ctrl+click** puts a whole group in or out of the selection
  ([S-1580]); the **rubber band** takes a group when it touches one member ([S-1581]); **Ctrl+A** takes
  everything. **Tab** cycles units in reading order (a group by its top-left corner); inside an entered
  group it cycles its members ([S-1973]). An entered group that dissolves is entered no more.
- **[S-2452]** **A selected group acts as one**: it moves, copies, cuts, pastes, deletes and is layered
  (Bring to front / Send to back, the members keeping their order among themselves) as the selection
  of its members does ([S-1584], [S-1592]); the arrow keys nudge it whole ([S-1970]). An entered member
  is moved on its own and stays a member wherever it is dropped.
- **[S-2453]** **Resizing is a member's alone.** In edit mode a group draws one frame of the edit
  markers' brackets and edge bars round its members' painted boxes (`GroupFrameLayer`: neutral, accent
  while selected, faint accent while entered), with no grips, and its members draw no markers of their
  own; an entered member shows its own markers and handles. Shift+arrows do nothing to a selection that
  holds a whole group.
- **[S-2454]** **Group and ungroup.** **Ctrl+G** makes one group of the selection (two widgets or more,
  a member bringing its whole group; groups in it merge into the new one, which takes the name of the
  first of them, in view order, that had one; the new group is selected whole). **Ctrl+Shift+G**
  dissolves every group the selection touches (inside an entered group, that group); the widgets stay
  where they are and stay selected. A widget's card menu offers **Group**, **Ungroup** and, on an
  entered member, **Remove from group**, only where each applies; a group left with one member
  dissolves.
- **[S-2455]** **Copies and moves.** A payload carries the groups it holds two or more members of; a
  member taken alone travels as a lone widget. A copy of a group lands as a new group (fresh id, the
  same name) every time it is pasted; a move keeps its group, across views and displays too, and the
  view it left lets it go. A duplicated view ([S-900]) has new groups of its copies.
- **[S-2456]** **Undo** covers every group change: the layout shapes carry each widget's group and each
  view's groups (id, name), `SameLayout` compares them, a restore puts them back. Group and Ungroup
  (and Remove from group) are one step each, named "group" and "ungroup".
- **[S-2457]** **Joined while dragged.** The cards a drag carries (a group, any selection dragged
  together) stay joined among themselves while they move, a set of their own free of the cards standing
  still ([S-2244]; `CardJoins.Outlines` takes a set per card: two cards join only within one set).
- **[S-2458]** **Scenes** ([S-1076]): a widget may carry `"group": "<name>"` (two widgets or more to a
  group, its name its id on the scene's view); the `edit` block may select a group whole (`"group"`) or
  enter one with a member (`"enter": "<widget id>"`), so the pictures show both frames. A group of one,
  an edit naming a group or a member that is not there, or both at once are refused at their path.

**Ready-made groups.** A ready-made group is a recipe that places several widgets, touching, already
grouped, so a first desktop is a few clicks.

- **[S-2470]** **Data** (`ReadyMadeGroups.All`): an id, a name and a one-line description (through `Str`,
  the `groups` locale area), a glyph, its subject in the add menu (`AddFolderId`), and its widgets: a
  widget id, a box in DIPs from the group's corner, a config fragment merged over the kind's default
  (as the first-run seed's, [S-893]), an optional header placement and title. Adding one is an entry and
  its two strings (docs/WIDGET-KIT.md 18). A place, an account or a folder is left to the widget's own
  Set up, never filled with someone else's.
- **[S-2471]** **Placing** is the one placement session ([S-1587]): a ghost of every widget at its
  offset, one click lands them all, touching, as one new group named after the recipe (fresh ids every
  time), selected whole; one undo step ("add"). The licence counts it as adding its widgets one by one
  ([S-1065]): the gate before the ghost and on landing see all of them, and it lands whole or not at all.
- **[S-2472]** **Rules** every recipe keeps (`ReadyMadeGroupsTests`): every kind installed and every
  config property one its kind has; two widgets or more; no two boxes overlapping; every widget touching
  another, so the recipe draws as one panel ([S-2240]); the whole within a 1280 × 720 work area. A build
  lacking one of a recipe's kinds leaves the recipe out whole.
- **[S-2473]** **The add menu**: its first column, **Ready-made**, before Popular and Browse, a row per
  recipe with its glyph, its name and how many widgets it places ("4 widgets"), its description the
  row's tooltip; the keys and the pointer pick it as a widget's row. The search finds recipes with the
  widgets, by name, description and the names of the widgets they hold; a result says "Ready-made · n
  widgets".
- **[S-2474]** **The seeds**: **This PC** (CPU, memory, network and disk as a 2 × 2 grid), **My day**
  (the clock on top, the next meeting and the tasks under it) and **Weather** (now, the forecast and
  the rain in the next hour, stacked), each with its kinds' default configs.
- **[S-2475]** Debug: `--recipe-png <id> <out.png> [--states kind=state,..] [--scale <n>]` draws a recipe
  placed: a scene built from it (`ReadyMadeScene`), edit mode, the new group selected, on a sample
  wallpaper; a widget given no state shows its "sample" preview where it has one.

Not live-verified: entering a group by clicks, dragging a group and an entered member, the keys,
the card menu's lines, a group handed to another display, placing a recipe on a display with a
taskbar or a scale other than 100 %.

## Music and Pomodoro: parity and the whole tile (PF)

- [S-1460], [S-1466], [S-1467], [S-1987]: moved with the Pomodoro widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Pomodoro/`).
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
- **[S-1468]** Music keys via the keyboard lease (`IWidgetKeys`; taken only with a track shown):
  Space/Enter play/pause, Left/Right previous/next, Shift+Left/Right seek ∓5 s (new
  `WidgetKey.ShiftLeft/ShiftRight`; Shift+arrows no longer arrive as plain arrows in other views).
- **[S-1469]** The large tiles split the subtitle into artist and album (album dropped when it
  repeats the artist). No "Playing · <app>" line (retired, [S-666]).
- **[S-1470]** *(Retired: the spectrum left Music for the audio visualizer, [S-3200]..[S-3201];
  `ShowSpectrum` and its settings row are gone.)*
- **[S-1471]** *(Retired: moved to [S-3202].)*
- **[S-1472]** *(Retired: moved to [S-3203].)*
- **[S-1473]** *(Retired: moved to [S-3204].)*
- **[S-1474]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- [S-1475], [S-6406], [S-6407]: moved with the Music widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Music/`).
- Not ported: the compact-header default of the original's [S-21] (WinUI header modes differ);
  `pollInterval` (legacy, unused).
  Not live-verified: actual sound output of the cues and the preview, single-flight stop, seeking
  against real players (Spotify, browsers), the hidden card's hit-region on the desktop, and keys
  through the lease.

## Audio visualizer (VISUALIZER)

- [S-3200], [S-3205], [S-3206], [S-3207], [S-3211], [S-3212], [S-3213], [S-3216], [S-3217]: moved with the Audio visualizer widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.AudioVisualizer/`).
- **[S-3201]** The analyser (was [S-1470]): WASAPI loopback of the default render endpoint
  (console role), mixed to mono, Hann-windowed 1024-point FFT, 24 log bands from bin 1 to Nyquist,
  sqrt compression ×3.5, clamped 0..1 — the original's. Float32/Int16/Int32 mix formats; anything
  else and silent packets count as zeros.
- **[S-3202]** Bars rise at once and fall 0.06 per frame (was [S-1471]). The worker drains every
  33 ms while sound arrives or a bar is up, and every 150 ms once all are down and nothing came
  (inside the 200 ms capture buffer); in silence it sends frames only until the bars are down,
  then nothing. A packet the capture flags silent is silence, not sound ([S-6332]). Display cadence, not a correctness path: event-driven loopback capture is
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
- **[S-3210]** *(Retired: six designs as six kinds, `VisualizerStyle` from the kind; they are one
  kind with six styles, [S-3216].)*
- **[S-3214]** The shared feed carries the trace (one path, no second capture): each worker frame is
  an `AudioFrame(Bands, Trace)`, the trace read from the same 1024-sample analysis window the FFT
  reads. `Oscilloscope.Trace` shows the newest 512 samples (about 11 ms at 48 kHz) from the newest
  rising zero crossing that still leaves a whole span after it (else the newest span), so a steady
  tone stands still; each point is the loudest sample of its stretch, square-root compressed with
  its sign, as the bands are; bad samples are silence. The other designs read the bands only.
- **[S-3215]** While sound arrives the line is the fresh trace; when it stops the line fades ×0.8 a
  frame and points under 0.01 are zero, so it lies flat (the design's idle, [S-3206]) in about half
  a second; the worker delivers while the line or a bar moves, then goes idle as before ([S-3202]).
- Not live-verified: loopback capture against real output and device-switch healing, the
  unavailable sentence on a machine with no output device, the idle cadence's CPU cost, and the
  capture stopping under game mode.

## Localization infrastructure (LOC-INFRA)

- **[S-1100]** One string layer in Core (`Themia.Core.Localization`): every UI text is a member of the
  generated `Str` enum (key `settings.language.label` → `Str.Settings_Language_Label`, summary = its
  English text), read with `Str.X.Text()`, `Str.X.Format(("name", v))` or `Str.X.Plural(n)` in
  `Strings.Current`, or `Strings.Get(locale, id)` for an explicit language. Lookup is an array read;
  no `ResourceManager`, no reflection, nothing loaded at run time (Native AOT clean).
- **[S-1101]** Source: one JSON file per language, `src/Themia.Sdk/Localization/Locales/<code>.json`
  (en, de, es, fr, it, ja, pl, pt, zh, hi, ar, bn, ru, id, ur, mr, vi, ko, nl, tr, zh-Hant, te, ha, pa,
  sw, fil, ta, fa, th, gu), flat `"dotted.key": "text"` or a plural object. en.json is the
  master (its keys, in order, are the enum). `Themia.Localization.Generator` (Roslyn incremental
  generator, build-time only) compiles them; a malformed file, a bad key, a duplicate, a key en.json
  lacks, an unknown locale file or two keys clashing on one member name is a build error
  (`THEMIALOC001`..`009`) with the file position. Each language's table is built on first use.
- **[S-1102]** Placeholders are `{name}` (ASCII letters, digits, `_`), filled in one pass; a value is
  never re-scanned; a placeholder without an argument stays visible; number arguments are formatted
  for the language (`1,500` / `1.500`).
- **[S-1103]** Plurals are CLDR cardinal categories for whole numbers (`zero one two few many other`):
  en/de/es/it/ur/mr/nl/tr/te/ha/sw/ta one = 1; fr/pt/hi/bn/pa/gu/fa one = 0 and 1; fil one = every
  count not ending in 4, 6 or 9 (0, 1, 2, 3, 5, 7, 8, 10 …), other the rest; pl one / few (2–4, not 12–14) / many;
  ru one (1, 21, not 11) / few (2–4, not 12–14) / many (and `other` for fractions = few's text);
  ar zero / one / two / few (n%100 3–10) / many (n%100 11–99) / other (100–102 …); ja/zh/zh-Hant/ko/
  id/vi/th other only. Russian and Arabic plural keys supply every category (test).
  `Plural(n)` fills `{count}`; a missing category uses the language's `other`, then English.
- **[S-1104]** A string the language lacks falls back to English, logged once per key and language at
  debug level; an id or key nobody has returns the key text. Never an exception, never empty.
- **[S-1105]** Completeness test (`LocalizationTests.S1105_*`): every key is in every locale or is
  listed under `"$englishOnly"` in en.json (a listed key translated everywhere fails as stale); every
  form's placeholders equal English's; plural keys are plural in every language and use only that
  language's categories; no text is blank.
- **[S-1106]** Formatting without `CultureInfo` (`LocaleFormat`, data in `LocaleData`, transcribed
  from CLDR per main region — en-US, pt-BR, zh-CN, es-ES, ar-SA with the Gregorian month names,
  bn-BD, ur-PK, hi-IN, mr-IN, ko-KR, nl-NL, tr-TR, zh-TW for zh-Hant, te-IN, ta-IN, gu-IN, pa-Guru-IN,
  ha-NG, sw-KE (Windows' Swahili), fil-PH, fa-IR, th-TH, the rest their home country): month and weekday names
  (wide/short/narrow; Polish, Russian and Vietnamese months declined inside dates, long and short,
  Persian months with the ezafe inside a date: "۴ مهٔ ۲۰۲۶"),
  8 `DateStyle`s, times on the language's default clock or forced 12/24 h (with seconds, which take
  the minutes' separator: Indonesian 15.05.09), integers and decimals with the language's
  grouping/decimal marks (es and pl leave 4-digit numbers ungrouped; hi, bn, mr, te, ta, gu and pa
  group in lakh and crore, 12,34,567), percent (Turkish before the number, %42), first day of the week
  (Monday, Sunday or Saturday: ar-SA, ko-KR, zh-TW, the Indian languages, sw-KE, fil-PH and th-TH are
  Sunday, ha-NG Monday, fa-IR Saturday, [S-564]). Every date is Gregorian, in every language: Persian
  and Thai get their language's Gregorian month names, not the Solar Hijri or Buddhist-era calendars
  Windows' fa-IR and th-TH default to (the app draws no other calendar). Pure
  functions; overloads without a locale use `Strings.Current`. Arabic relative times say one and two
  in words ("قبل دقيقتين"), as CLDR does.
- **[S-1107]** Relative time (`LocaleFormat.Relative`): under 1 min "now", then whole minutes, hours,
  days, weeks (from 7 d), 30-day months (from 30 d), 365-day years (from 365 d), truncated; past
  "… ago", future "in …"; long and short styles, plural-correct.
- **[S-1108]** "Follow Windows": `GetUserPreferredUILanguages` (read once; changing it needs a
  sign-out), first tag whose primary subtag Themia has, else English. Chinese goes by script, as
  in Windows and Karpo: `zh-Hant*`, and `zh-TW`/`zh-HK`/`zh-MO` without a script, → zh-Hant
  (Traditional); every other Chinese tag (`zh-CN`, `zh-SG`, `zh-Hans-HK`, `zh`) → zh (Simplified,
  zh-Hans). Every Portuguese tag (`pt-BR`, `pt-PT`) → pt, the Brazilian table. `fa-IR`, `fa-AF` and Dari
  (`prs-AF`) → fa; `fil-PH` and Tagalog (`tl`) → fil; `sw-KE`/`sw-TZ` → sw; `ha-Latn-NG` → ha;
  `pa-IN`/`pa-Guru*` → pa, but Shahmukhi (`pa-Arab*`, Pakistan) is not taken for the Gurmukhi table and
  the walk goes on. Walks the whole list, not only its head.
- **[S-1109]** ⚙ › Language & region › Language ([S-3536]): Windows' setting + the forty, each named in its own
  language. The host resolves the setting before any window is built and again on every state
  change (an import or reset), and `SetLanguage` applies it at once.
- **[S-1110]** Live switch: `Strings.Changed(Locale)` fires on the UI thread after the language
  changed (not when it is set to the same one). The edit bar's open menus write their words in the
  new language on it, keeping what is open ([S-3531]). Cards, widgets and the
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
- **[S-1112]** Seventeen languages: the nine the original shipped plus Hindi, Arabic, Bengali,
  Russian, Indonesian, Urdu, Marathi and Vietnamese, the same set as Karpo's (Italian and Polish stay
  as extras). Each is a `Locale`, an `AppLanguage` (persisted by name) and a file per locale folder;
  `LanguageResolver.Setting` maps a locale back to its setting. The eight new languages are machine
  translations marked for native review in every file's `$comment`. Twenty-one since Korean,
  Dutch, Turkish and Traditional Chinese (`zh-Hant`, Taiwan wording: 設定, 檔案, 桌面) joined, the rest
  of Karpo's set; also machine translations marked for native review, reusing Karpo's wording where
  it has the same concept. One file per language: `pt` is Brazilian Portuguese (pt-BR), `zh` is
  Simplified Chinese (zh-Hans); their file codes stay short, their layout tags carry the variant.
  Thirty, the thirty most widely spoken, since Telugu, Hausa, Punjabi (Gurmukhi), Swahili, Filipino
  (`fil`, the one three-letter code), Tamil, Persian, Thai and Gujarati joined (named తెలుగు, Hausa,
  ਪੰਜਾਬੀ, Kiswahili, Filipino, தமிழ், فارسی, ไทย, ગુજરાતી); also machine translations marked for native
  review in every file's `$comment`.
  Forty since Malay, Ukrainian, Romanian, Greek, Czech, Kannada, Malayalam, Amharic, Yoruba and Burmese
  joined (named Bahasa Melayu, Українська, Română, Ελληνικά, Čeština, ಕನ್ನಡ, മലയാളം, አማርኛ, Yorùbá,
  မြန်မာ), so Themia has the same forty languages as every Lometics app and site. Czech counts
  one/few/other and Romanian one/few/other ("many" is for fractions only); Malay, Yoruba and Burmese
  write one form; Kannada and Amharic take the singular for nought too. Burmese keeps Latin digits.
- **[S-1115]** Native digits: a language that writes its own digits gets them in the text, not from
  DirectWrite's contextual substitution, which follows the neighbouring script (an Arabic agenda row
  "09:00–09:15 · Teams" was drawn with Latin digits). `LocaleFormat` swaps them in as the last step
  of every date, time, number, percent and relative time (`LocaleData.DigitZero`), and
  `LocaleFormat.Digits(locale, text)` does it for numbers a view composes itself (the clock readout
  and its local offset, calendar day numbers, hour labels and "+N"). Arabic does: Arabic-Indic
  ٠–٩ with ٫ decimal, ٬ grouping and ٪ (CLDR "arab"); so does Persian, with the Extended Arabic-Indic
  ۰–۹ (U+06F0, CLDR "arabext", Windows' fa-IR default) and the same marks. Thai, Telugu, Tamil,
  Gujarati and Punjabi keep Latin digits: Windows' th-TH, te-IN, ta-IN, gu-IN and pa-IN default to
  them (native digits are an opt-in there). Urdu keeps Latin digits (Windows' ur-PK and
  CLDR default); Bengali keeps Latin ([S-1114]). The swap is one character for one, so the clock's
  character-count type ladder and the agenda's measured time column (measured on the language's own
  "00:00") see the digits that are drawn. Never applied to text that must stay as written: URLs,
  addresses, stock symbols, "UTC+2", what the user typed.
- **[S-1116]** No digits typed into prose: a number in a string ("Last {count} days", "Reset to
  {percent}", "{minutes}-minute steps", "{zero} is off") is a placeholder in every language, filled
  from code through `LocaleFormat`, so Arabic and Persian draw it in their own digits. Names that
  merely contain digits stay Latin and are listed in the test's allow-list: ports (993, 65535), key
  names (F2), formats (MP4), heading levels (H1), chart ranges (1D, 5D); the shell's `%1` insert is
  not prose. A Core test fails on any other Latin digit in the Arabic or Persian files, and the
  module test host runs the same rule over every module's own table, with the modules' share of the
  allow-list (by widget and key) beside it.
- **[S-1113]** Right to left: Arabic, Urdu and Persian (`TextDirection.IsRightToLeft`). The direction is set
  on the roots whose whole content is text-like UI — the app-layout editor, each card, the edit bar,
  the view bar, the notices, the placement hint, the anchor menus on the bare surface open from —
  and never on a desktop surface itself: its canvases, the hit region (`SetWindowRgn`), the rubber
  band and every stored geometry stay physical, so no widget moves when the language changes and
  `WidgetArea`/`Disclosure`/view-bar fit (widths and heights only) are unchanged. Flyouts, menus and
  tooltips take the direction of the element they open on. Kept left to right on purpose: a card's
  resize grips (physical corners), media transport and the seek bar (Music, the Gallery slideshow),
  charts over time (meter sparklines, Stocks, Screen time, the Sun's day arc), the audio spectrum and
  the analog clock face. Numbers stay left-to-right runs by the bidi algorithm. Directional icons
  mirror (`TextDirection.MirrorsGlyph`: back, forward, chevrons, send, reply, undo/redo, return,
  open outside, sign out, bulleted and checked lists, via `FontIcon.MirroredWhenRightToLeft`);
  play/previous/next never do. The layout editor's caption buttons stay where Windows draws them
  (right); a right-to-left title bar keeps their width clear at its start.
- **[S-1114]** Fonts per language follow Karpo's rule: no font is named per language, the layout tag
  (`TextDirection.LayoutTag`, the language with its main region) makes DirectWrite's fallback pick
  the script's Windows UI face — Nirmala UI for Devanagari (hi, mr) and Bengali, Segoe UI for
  Latin (incl. Dutch and Turkish), Cyrillic, Vietnamese, Arabic and Urdu, Yu Gothic UI for
  Japanese, Microsoft YaHei UI for Simplified Chinese (zh-Hans), Malgun Gothic for Korean (ko-KR),
  Microsoft JhengHei UI for Traditional Chinese (zh-TW), Nirmala UI for Telugu, Tamil, Gujarati and
  Gurmukhi (te-IN, ta-IN, gu-IN, pa-IN), Leelawadee UI for Thai (th-TH), Segoe UI for Persian (fa-IR),
  Hausa (ha-NG, with its hooked letters), Swahili (sw-KE) and Filipino (fil-PH). No Nastaliq for Urdu: "Urdu Typesetting" ships only with the Urdu language
  pack, and its tall lines would overflow the line-height budgets. Bengali is tagged bn-IN rather
  than bn-BD: Windows' bn-BD digits are Bengali and run about a quarter wider than the Latin advance
  the type ladder measures, which clipped the tall clock.
- Ported from `main`: 1,673 of the original's 1,748 keys with all nine translations (key names
  kept; `folder.showHidden_`/`showExt_` → `showHiddenMenu`/`showExtMenu`; `shortcut.appCountOne/Other`
  and `binCountOne/Other` → plural keys; `<br/>` → newline; `settings.language.system` takes the
  translations of `settings.theme.system`). Dropped as out of scope: tray, updater, telemetry consent,
  Lemon Squeezy key activation, newsletter, pop-out, always-on-top, YouTube. Not built yet (with the
  extraction round): replacing the literals, rich text (`tm`), the literal scanner (`i18nScan`), the
  widget picker's translated + English search names. Polish plural keys have only
  one/other until translated (few/many fall back to other). The CLDR tables want a native-speaker
  review in the translation wave. Not verified live: the language switch in the running app (no app
  run here); the General row was checked with the settings window's render, since gone ([S-3585]).

## Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ)

Tests: `tests/Themia.Core.Tests/DeskParityTests.cs` (the astronomy); the clock, the sun, the moon,
the quote and the scratchpad are modules of their own, each with its SPEC.md and tests.

- [S-1480], [S-1481], [S-1482], [S-1483], [S-1484], [S-1485], [S-1486], [S-1487], [S-1488], [S-1476], [S-1477], [S-1489]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).
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
  geocoder, resolved once at the pick); exact name/latitude/longitude stay editable (asked for as
  text under the search's answers, [S-3101]) and validated
  (S-171's refusal rule unchanged). This supersedes S-171's "no geocoding service" reason.
- [S-1495], [S-1990], [S-1496], [S-6404], [S-6405]: moved with the Quote widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Quote/`).
- [S-1497], [S-1498], [S-1499]: moved with the Scratchpad widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Scratchpad/`).
- **[S-1500]** Clear asks first (user bug: the question was clipped to the tile), through the
  widgets' one confirmation `ConfirmFlyout` (also the folder's delete, [S-1330]): the question, the
  action (accent) and Cancel, beside the button. It is never constrained to the root: inside the
  surface's window the region ([S-851]) clips it to the card, so it opens as its own popup window,
  which the framework keeps in the display's work area, flipping to the side with room — at any tile
  size, either theme, either direction. Escape, a click elsewhere or Cancel are "no". It is every
  widget's one way to ask (`ConfirmFlyout.AskAsync`, optionally with a detail line under the
  question): the clipboard's clear and the notes' delete
  ([S-1364]) too, which were `ContentDialog`s centred on the monitor-sized surface and drawn inside
  its region. No widget view and no part of the surface opens a `ContentDialog`, and every flyout or menu a widget opens (a rename box, a note, `ItemMenu`) is its own popup
  likewise. The scratchpad's clear is its card menu's command, asked beside the widget ([S-1501]);
  in full screen it is on the toolbar, asked beside its button. Tests read the source (`ConfirmFlyoutTests`); renders (`--state confirm` for
  Scratchpad, Clipboard, Notes) draw the question inside the render's
  window, as a windowed popup cannot be drawn to a bitmap; a live pass checks it on a small tile at
  a display edge.


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
  default output" only when bound. The menu's device line still binds the widget.
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

## Quick-setting toggles (QUICK-TOGGLES)

The toggle widgets of Karpo's Quick settings (`karpo-app`, docs\widgets.md §117: the ones a click
flips where they stand), each its own widget. SDK: `QuickToggleRules`, `ToggleGate`
(`Themia.Sdk`), `QuickToggleView` (on the family's `TileBlock`) and `IQuickToggles` (`Themia.Sdk.WinUI`); Core:
`ToggleFollowing` (`ReadingOrder`, `KeyFollow`), `NightLightValue`; App: `Services/Toggles/*` (one
source per toggle, `ToggleHub`); tests `QuickToggleTests`.

- **[S-3900]** Thirteen kinds, one per toggle, in the add menu's subject **Quick settings** ([S-3511]), default size 120×120: Wi-Fi (`WifiToggle`), Bluetooth (`BluetoothToggle`), Airplane mode,
  Energy saver, Night light, Rotation lock, Do not disturb, Sound (the default output's mute),
  Microphone (the default microphone's mute), Spatial sound, Mono audio, Color filters, Game mode.
  The kind maps to its `QuickToggle` in the SDK (`QuickToggleRules.ForKind`/`KindOf`, read off the
  catalogue); one view serves all thirteen. Each is a module project of its own
  (`src/Widgets/Themia.Widget.WifiToggle` …, tags [S-8200]..[S-9400] in their `SPEC.md`), with a
  config of its own (`WifiToggleConfig` …, base `QuickToggleConfig`) whose
  one setting is the style ([S-3916]), picked on the properties panel ([S-6536]), so its menu has no lines of its own; it
  never needs setting up. The picker names it "<name> on/off" (the module's own label and
  description; the name on the tile is the SDK's, which the shared view reads) so it never
  reads as another widget ("Bluetooth on/off" beside the Bluetooth devices widget); the tile says the
  name alone. Not ported from Karpo: its row of several toggles in one widget, mobile hotspot, VPN and
  nearby sharing (Karpo keeps those out of its toggles: they open lists or flyouts), live captions,
  accessibility and project (Windows tells no app whether they are on).
- **[S-3901]** A toggle never has a header ([S-700], [S-3711]): the whole tile is the switch, its name
  under the icon. Its properties strip has no Header choice, and a header in the file is taken off on
  load.
- **[S-3902]** Icons are Windows 11's quick-settings glyphs (Karpo's set); Sound and Microphone are
  struck through (E74F, EC54) while muted.
- **[S-3903]** A source reads `ToggleReading(On, Block)`. `Block` says why it cannot be switched:
  `Reading` (asked, not answered: no state, inert, not dimmed), `NoHardware` (no radio, no rotation
  sensor), `NoDevice` (no output or microphone), `NotOnWindows` (energy saver before 24H2),
  `NotNow` (rotation while docked, on several screens, as a laptop, remote), `NoSwitch` (Windows'
  interface or Settings handler is not there for an app), `NotSupported` (the output cannot do
  spatial sound), `TurnedOff` (Windows says the setting does not apply, or a radio's hardware switch
  holds it). Nothing ever elevates: what needs more than the user's rights is one of these.
- **[S-3904]** The tile is a Windows 11 quick-settings button over the whole content area, inside the
  card's own fill and corners, drawn as the family's Tile block ([S-6715], [S-6736]): on, the whole
  tile in the accent (the subtle fills laid over it on hover and press) with the icon and name on it
  and the state line in the on-accent secondary role; off, the card's item layer with its hover and
  press fills; not known, every part secondary; unavailable, every part disabled, no hover, no
  press, and the reason in the tooltip ("Bluetooth: This PC does not have it"). The tooltip is
  otherwise "Wi-Fi: On"; after a refused click, "…: Windows did not switch it" until the next click.
  In edit mode the press is the card's ([S-1911]).
- **[S-3905]** Clicks go through `ToggleGate`: a click asks for the opposite of what is read and the
  tile shows it at once; while that switch is on its way every click does nothing; a click within
  400 ms of the last is the same click (a double-click never flips back). When Windows answers, the
  source's reading stands. Space or Enter switch it while the widget holds the keyboard lease
  ([S-800]); a toggle that cannot be switched does not take the keyboard (`WantsKeys`).
- **[S-3906]** The face is the Tile block ([S-6715]) in the card's inset of 12: the icon centred on the
  largest Readout or Hero step above the name (Body) and the state line ("On", "Off",
  "Unavailable", Caption) and at most 0.6 of the width, the name and the state at every size (cut
  with an ellipsis where they do not fit, whole in the tooltip). Both lines are planned whether or
  not they say anything (a reading not answered yet has no state text), so switching moves nothing.
  Room left at L and XL stays empty past the 192 step (concise first).
- **[S-3907]** One source per toggle for the whole app (`ToggleHub`, host code; a widget reaches it
  through `IWidgetHost.Toggles`, [S-6913]), started by its first widget and stopped by its last; a change reaches each widget on its own thread. No source polls: each follows
  the notification its setting has and reads again when it fires. The pointer coming onto a tile
  re-reads what Windows announces nowhere (availability, a do not disturb rule). Nothing blocks the UI
  thread; every native callback catches, logs and returns. Native AOT: `LibraryImport`, COM through
  function-pointer vtables, CsWinRT projections only for documented WinRT types.
- **[S-3908]** Energy saver, night light, game mode, color filters and mono audio switch through the
  Settings handler Windows' own tile calls (`GetSetting` in `SettingsHandlers_*.dll` from System32,
  undocumented `ISettingItem` "Value"), checked to be the switch asked for (its id said back, type 3,
  a boolean) before use. The handlers live on one STA thread with a message loop, every call handed to
  it as a task; no wall-clock wait (Karpo's two-second start-up wait is not ported: whether a handler
  applies is asked when the pointer comes by).
- **[S-3909]** Debug: `--widget-png <Kind> <out.png> [--theme light|dark] [--style tile|round|switch] --state on|off|unavailable|unknown|hover|pressed|refused`.
  A render reads nothing and switches nothing ([S-1251]); a click there flips a made-up reading.
- **[S-3910]** Wi-Fi and Bluetooth: `Windows.Devices.Radios`, the radio of the kind found on the pool,
  `StateChanged` followed, `RequestAccessAsync` then `SetStateAsync` (Windows may refuse an app; the
  click says so). Airplane mode: the Radio Management service's system switch, `IRadioManager`
  out of process (`GetSystemRadioState`/`SetSystemRadioState`, "enabled" is airplane mode's
  opposite), followed through HKLM `…\RadioManagement\SystemRadioState`. Karpo's fall-back of
  switching each radio by hand is not ported (one path): without the service, `NoSwitch`.
- **[S-3911]** Energy saver: `GUID_ENERGY_SAVER_STATUS` through `PowerSettingRegisterNotification`
  (callback; 1 standard and 2 high savings are on), Windows 11 24H2 and later only (`NotOnWindows`
  elsewhere). Night light: switched through the Display handler; its state is the CloudStore
  blue-light value, read and followed ([S-3919]); Karpo's hand-written Bond rewrite of that value is
  not ported.
- **[S-3912]** Rotation lock: `GetAutoRotationState` read as Windows' handler reads it (no sensor or
  not supported: no hardware; remote, multimon, docked, laptop: not now; else locked while disabled),
  switched by `SetAutoRotation` (user32 ordinal 2507), followed through HKLM `…\AutoRotation`.
- **[S-3913]** Do not disturb: `IQuietHoursSettings` (WpnUserService, undocumented). On while the
  active profile is not the off profile; on sets the user's profile to PriorityOnly, off sets it back
  and turns off a rule holding it on. Heard through the CloudStore quiet-hours value (its key's
  holder watched with everything under it, [S-3921]); a rule turning
  it on or off by itself (a game, the schedule) shows when the pointer next comes by (Karpo's
  `NotificationModeChanged` needs a Windows 11 SDK projection this build does not target).
- **[S-3914]** Sound: the default output's mute through the shared Core Audio worker ([S-1220]).
  Microphone: the default capture endpoint's (console role) mute, its own small Core Audio watch
  (endpoint-volume and device callbacks, re-bound when the default changes). Spatial sound:
  `SpatialAudioDeviceConfiguration` of the default output, on is Windows Sonic for Headphones, off is
  GUID_NULL, followed through `ConfigurationChanged` and the default output changing; Windows keeps
  the choice for Settings and may refuse. Karpo's list of installed formats is not ported.
- **[S-3915]** Game mode (`HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled`, absent is on), color
  filters (`…\ColorFiltering\Active`, absent is off; the filter is Windows' last one) and mono audio
  (`…\Multimedia\Audio\AccessibilityMonoMixState`, absent is off) are read from those DWORDs, watched
  (a key not there yet is waited for at its parent, [S-3921]), and switched through their handlers.
- **[S-3919]** Night light's state is the CloudStore value (`…\default$windows.data.bluelightreduction.bluelightreductionstate\…`
  "Data"), read as Bond compact binary v1 (`NightLightValue`: the state's field 0 present is on; field
  10 optional; anything not exactly that shape reads as unknown, never off), never written. The
  Display handler's answer is used only where the value does not read: its item, made once per run,
  kept saying the old state after a switch in Windows' panel, Settings or the schedule (the reported
  bug). After Themia's own switch the tile shows where it went until the value moves from what it was
  just before; any other value then stands (`NightLightFollow`). The value's holder key is watched
  with everything under it.
- **[S-3920]** Readings are taken in order (`ReadingOrder`): every read takes a ticket before asking
  Windows, and a reading older than the one last shown is dropped, so two quick changes made
  elsewhere (or a notification and the switch's own read) can never end on the earlier state.
- **[S-3921]** A registry watch (`RegNotifyChangeKeyValue`, one-shot) is armed again before the owner
  reads. A key that is not there (or went: the CloudStore writes a key again from scratch) is waited
  for at its parent, the parent armed first and the key looked for after it, and on a parent's
  signal the parent asked again before the key is looked for (`KeyFollow`), so a key coming back in
  between is never lost behind a parent that will not signal again.
- **[S-3916]** Styles ([S-6000]): one `QuickToggleStyle` on the base `QuickToggleConfig`, shared by
  all thirteen kinds (never an enum per kind): **Tile** (the default, [S-3904]..[S-3906]), **Round
  button** (`round`, [S-3917]) and **Switch** ([S-3918]), the Tile block's three looks. Every style is
  drawn over the same whole-tile hit target, so the click gate ([S-3905]), Space/Enter under the
  lease, edit mode, the tooltip and "never a header" ([S-3901]) are the same in each; only the
  drawing differs. Every style (and the power widgets') insets by the card's 12 at every size
  ([S-6703]).
- **[S-3917]** Round button (the Tile block's round look, which the power widgets' Round style
  shares, [S-1919]): a circle of the Tile's step holding the icon at the largest family glyph size
  within half of it, the name and the state line under it at every size. On, the circle is the
  accent (the subtle fills over it on hover and press) with the icon on it, the name primary; off, the
  circle is the item layer's tile with its hover and press fills; not known, every part secondary;
  unavailable, every part disabled. The tile behind it stays clear.
- **[S-3918]** Switch (the Tile block's switch look, [S-6736]): a row centred in the tile's height:
  [icon 16] 8 [name, Body, over its state, Caption] 12 [Windows' toggle switch, 40 × 20], the switch
  drawn by WinUI's own control, inert (`IsHitTestVisible` off), so the tile stays the one hit
  target. Off, the switch off; on, the switch on (its track the accent); unavailable, the switch
  disabled and every part disabled. The name and state need 96 beside the switch: on a narrow tile
  the icon gives way first (S, Tall), then the lines (XS: the icon and the switch). Room left stays
  empty (concise first).
- Needs a live pass on real hardware: every switch is untested against this PC's state by design
  (no test touches the user's radios or settings).

## Windows' panels: notifications, focus session, brightness (WINDOWS-PANELS)

Three widgets after Karpo's (`karpo-app`, docs\widgets.md §18, §19, §35, §92, §100, §103), each on
Windows' own state. All three are modules of their own (`src/Widgets/Themia.Widget.Notifications`,
`.FocusSession`, `.Brightness`, each its SPEC.md). What they share with the host stays here: Core's
`Shell/WinRtIid`; the SDK's `NotificationRules`, `FocusSessionRules` (+ `FocusQuietHold`) and
`BrightnessRules` (+ `NewestLevels`); the host's `Services/Notifications/NotificationFeed` (behind
`IWidgetHost.Notifications`), `Services/Focus/*` (behind `IWidgetHost.Focus`), `Services/Brightness/*`;
strings in `Locales/panels/`; tests `WindowsPanelsTests`, the module host's `WindowsPanelsHostTests`.

### Notifications

- [S-4400], [S-4404], [S-4409]: moved with the Notifications widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Notifications/`).
- **[S-4401]** The list is Windows' notification center's, read with `UserNotificationListener`
  (`GetNotificationsAsync(Toast)`): the sender's AUMID and name (`AppInfo`), the toast's texts (first
  non-blank the title, the rest the body, control characters dropped, title ≤ 200, body ≤ 600), when it
  came; newest first, at most 100. Karpo reads Windows' database (`wpndatabase.db`) for the whole toast
  and dismisses through the listener; Themia, a Store app, reads only through the listener, so a toast's
  buttons and reply box are not offered — a double-click opens its app instead ([S-4406]).
- **[S-4402]** Grouped by app as Windows 11's center, from an expanded height ([S-4404]): a heading per
  app (one row of 28: its icon and its name, BodyStrong secondary), the apps in the order of their
  newest, each app's newest first (two of one moment: the higher id first), its rows leading with an
  empty slot so every title stands at one edge. A group of more than three shows its newest three and a
  "See more (n)" row (secondary, a row of the list's own, never a link); "See fewer" folds it again;
  what is opened is the showing's. **Clear all** is the header's button (24, its 16 glyph) beside the
  neutral count and clears Windows' own list (`ClearNotifications`); an app's notifications are cleared
  and one is dismissed from the row's menu or with Delete, through `RemoveNotification` (the heading's
  and the row's × are gone: a row has no controls of its own, G2, 2026-10-29, as the kit's [S-7033]).
- **[S-4403]** Access: a packaged Themia declares `userNotificationListener` (Package.appxmanifest).
  Windows' answer is Allowed (the list), Denied ("Notification access is off for Themia" and **Open
  settings** → `ms-settings:privacy-notifications`), Unspecified (never decided: **Allow** calls
  `RequestAccessAsync`, Windows' own prompt; a no opens the privacy page) or no listener at all
  ("This version of Windows has no notification list to read", no button). Until Windows answers the
  widget draws its frame only. An unpackaged Themia (F5, publish.ps1) is answered by the privacy page's
  notification access, as Karpo found; access has no change event, so it is looked at again when the
  pointer comes onto the widget.
- **[S-4405]** A sender Windows names nothing gets a name from its id
  (`NotificationRules.FallbackName`, Karpo's `NotificationText.FallbackName`): "Windows" for Windows'
  own (`Windows.*`, `Microsoft.Windows.*`, none), a program path's file name, else the last real word
  of a dotted or packaged id (`5319275A.WhatsAppDesktop_…!App` → WhatsAppDesktop). Icons come from the
  shared app-icon service as `shell:AppsFolder\<AUMID>` (packaged apps included, [S-3960]); the bell
  until one comes, and where none does.
- **[S-4406]** A double-click (or Enter) on a notification opens its app; a click only focuses its row ([S-7065]; it opened on a click): `ShellLauncher.OpenApp`, the AUMID's Apps-folder
  item, through its own allow list (`NotificationRules.AppTarget`: letters, digits, `. _ - ! { } \ :`
  and space, ≤ 512, no `..`, not rooted with a slash). The notification stays in Windows' list.
- **[S-4407]** A dismissal shows at once: the feed hides the ids it dismissed until Windows' list no
  longer has them, then forgets them (`NotificationRules.Visible`), so the hidden set never grows and
  never hides a later notification. A refusal from Windows shows them again; nothing is deleted on a
  recoverable error.
- **[S-4408]** One feed for the whole app, counted by its widgets. Changes are heard, not polled: the
  listener's `NotificationChanged`, and where Windows refuses that subscription to the process, the
  change notification of the folder Windows keeps the list in (`…\Microsoft\Windows\Notifications`), a
  signal only, never opened. Reads are single-flight (a change during a read asks for one more). The
  relative times ("4m") are kept true by a one-minute tick.

### Focus session

- [S-4420], [S-4426], [S-4427], [S-4428]: moved with the Focus session widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.FocusSession/`).
- **[S-4421]** One session for the app, as Windows has one: started from any focus widget at its own
  length, shown by all (`FocusSessionService`, a widget's `IWidgetHost.Focus`). It runs from Start to its end and ends there (a
  one-shot timer at the end, not a poll) or at End. −/+ step the length by five minutes on the five-minute
  grid while idle, and save it. It runs on with no focus widget shown (a view switch); in memory:
  quitting Themia ends it.
- **[S-4422]** The time left reads as a clock rounded up to the second (`25:00`, `1:23:45` from an
  hour; the last second `00:01`); at rest the length. The second tick runs only while a session does.
- **[S-4423]** While a session that wants it runs, Windows' do not disturb is held on, as Windows' Focus
  does (Karpo's `FocusQuiet`, `FocusQuietHold`): turned on at the start and off at the end; on already,
  it is the person's and left on; switched by the person during the session (the toggle widget,
  Windows), theirs from then; an "off" reading before Windows answered the switch is not the person;
  unknown waits for Windows; refused or not switchable here, the session runs without it and says "Do
  not disturb can't be turned on here". It goes through the do not disturb toggle's own source
  ([S-3913]), so the toggle shows what the session did. Quitting mid-session hands it back (a
  bounded two-second wait on the way out).
- **[S-4424]** Windows' own focus session (Clock, Settings) is followed through
  `Windows.UI.Shell.FocusSessionManager` (`IsFocusActive`, `IsFocusActiveChanged`), reached through
  raw vtables because Themia's SDK projection is Windows 10's (IIDs from the 10.0.26100 metadata).
  Idle, the widget says "A Windows focus session is on". Starting or ending a Windows session
  (`TryStartFocusSession`, `DeactivateFocus`) is a Limited Access Feature that needs an unlock token
  Microsoft issues per package family; Themia has none, so its session is its own countdown with
  Windows' do not disturb, as Karpo's. Windows gives no end time for its own session.
- **[S-4425]** The handler Windows calls is a COM object of Themia's (IUnknown, IAgileObject, Invoke),
  answering the parameterised IID of `TypedEventHandler<FocusSessionManager, Object>` computed as the
  WinRT type system does (`WinRtIid`, SHA-1 version-5 UUID); `Invoke` catches everything and reads
  again off Windows' thread.

### Brightness

- [S-4440], [S-4445], [S-4446]: moved with the Brightness widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Brightness/`).
- **[S-4441]** How a display is reached, without WMI (Native AOT): a **built-in panel** (the display
  configuration API's output technology internal, LVDS or embedded DisplayPort) through Windows' power
  policy — `VIDEO_BRIGHTNESS` of the active plan, AC or DC as in force, applied with
  `PowerSetActiveScheme`, the setting Windows' own slider moves — and heard through
  `GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS` (sent at registration and on every change: keys, quick
  settings, adaptive brightness); **an external monitor** over DDC/CI (dxva2
  `GetMonitorBrightness`/`SetMonitorBrightness`, VCP 0x10), its own range read as percent; a monitor
  that does not answer the read has no brightness. Karpo takes WMI for the panel. DDC/CI has no
  notification: monitors are read when a widget comes up and when the pointer comes onto it; a missed
  reply keeps the level last said.
- **[S-4442]** Which displays: every display with a brightness together (default, Karpo's), or **Only
  the screen it's on** — the display of the widget's surface window. It shows its own display's level,
  else (all screens) the first display's; only its own and that one has none: "This display's
  brightness can't be set".
- **[S-4443]** One worker thread does every DDC/CI and power call. A drag asks at every move; the
  worker writes each display's newest level only (`NewestLevels`), the last level always wins and
  nothing queues; a dragged level stays on the slider until the display has said what it took.
- **[S-4444]** Keys: arrows and +/− by 1, Page Up/Down by 10, Home/End to 0 and 100; the wheel anywhere
  on the tile, part-notches carried as on the volume tile.
- Needs a live pass: notifications on the packaged build (consent prompt, Store capability) and
  unpackaged (privacy page), a reply-less toast clicked, dismiss and clear reaching Windows' list;
  do not disturb held and handed back by a session, a Windows focus session from Clock showing; a
  laptop panel and a DDC/CI monitor (mixed DPI, two displays, "only the screen it's on"), the keys
  and Windows' quick settings moving the panel while the widget shows.

## Views and the view bar (WP8)

Duplicate, reorder and icons for views, the original's desktop view bar, Ctrl+1..9 and the
toolbar's snap switch. Rules in Core `ViewOps` and `ViewBarRules` (tests `ViewsTests`); the bar is
`Shell/ViewBar.cs`, wired by `DesktopWindow.ViewBar.cs`. Every switch goes through
`AppHost.ShowView` → `AppState.ShowViewOn` ([S-19]).

- **[S-900]** Duplicate a view (View ▾'s Duplicate this view and a view's Duplicate view, [S-3541], [S-3542]; a view bar chip's menu in edit
  mode): the copy goes right after the source, named "<name> (Copy)", with its icon and a deep copy
  of every widget under fresh ids. Data a widget only refers to (a note, an
  account) stays shared, as in the original. The copy is shown on the display asked from; it counts
  against the licence as one view plus its widgets.
- **[S-901]** Reorder views by dragging a chip on the view bar (edit mode), by dragging a view's line
  in View ▾ ([S-3541]), or Alt+Shift+Up / Down on a view's line there ([S-3802]). No menu has Move up / Move down. By id, never by bar index, so a bar showing a filtered subset moves the right view.
  The order is the bar's, View ▾'s and Ctrl+n's.
- **[S-902]** Deleting a view (never the last) repairs everything naming it in the same change:
  display assignments (the display then gets an existing view again, [S-1705]), the active view, the view for
  new displays (back to null) and every bar whitelist (one left empty means all views again).
- **[S-903]** A view's icon is picked in View ▾ › the view › Icon › from the shared glyph grid
  (`IconPicker`, [S-3542]), whose default cell shows the view's initials — what a view without an
  icon shows. The icon is drawn by the view bar and the edit bar's View and Display menus (a view
  without one shows the shared view glyph there), in the view's colour ([S-3810]).
- **[S-904]** In edit mode only, Ctrl+1..Ctrl+9 (and the keypad digits) show the n-th view on the
  display under the pointer (the one a paste lands on, [S-74]), or this one. Outside edit mode
  Themia takes no digits: there is no global hotkey (audit B6).
- **[S-905]** The edit bar's Snap toggle sets `Design.GridSnap`, and nothing else does ([S-3516]).
- **[S-906]** Each display has its own view bar settings (`AppState.ViewBars`, keyed like
  `MonitorViews`): anchor (default bottom left), size M/L/XL, type ([S-5400]), labels ([S-5405]),
  hidden, and a whitelist of views. A display without an entry uses the defaults and nothing is stored until it
  is changed. At load, blank keys and null entries are dropped, unknown anchors/sizes/types/labels reset, and
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
- **[S-911]** Sizes M, L and XL are on the family's tokens (shell sweep, 2026-10-30; they were the
  original's Caption/Body/Body Large, 14/16/20 icons, 28/34/40): the words Caption, Body and Title at
  400, the marks the family's 16 icon (Windows Settings' 20 icon box at XL), a chip at least 28, 32 and 40 tall (its square
  when it shows the mark alone, `ShellMetrics.ViewBar`), padded 8, 12 and 16 either side and 4 over
  and under, the bar's own padding 4, 4 and 8; the mark 8 from the name; a select bar's chevron the
  menus' 12. A dragged chip is faded as a dragged item is (`SelectionLook.Faded`). A chip shows the view's icon or initials (the first letters of the
  first two words, or the first two letters of one word, upper-cased, counted in text elements; "?"
  for a blank name) and its name (bold for the
  view new displays start on), or one of them ([S-5405]); the shown view is a raised control, the rest subtle until hovered.
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
- **[S-915]** In edit mode a chip's right-click menu offers Duplicate view and View settings (the chip's view shown here, then View ▾, [S-3546]); outside
  edit mode it has none. A click always shows the chip's view on this display.
- **[S-916]** A display's view bar is set in its Display ▾ › View bar › ([S-3544]) and nowhere else
  (a chip's right-click menu offers no look, [S-915]): Show the view bar, Type ([S-5400]), Labels
  ([S-5405]), Position (8 anchors), Size, and Views on the bar. A view is
  shown on a display through `ShowViewOn` (Display ▾, [S-3543]).
- **[S-917]** The bar never runs off its display (user bug: with many views it ran past the edge).
  Its room along its axis (`ViewBarRules.Room`) is the work area's width (a row) or height (a
  column) less the 16-DIP margin at both ends; a column in edit mode also gives up the toolbar's
  strip, so pushed below it ([S-909]) it still ends a margin above the bottom. When the chips do not
  fit (`ViewBarRules.Fit`, on their measured lengths, the bar's padding and the grip), the bar draws
  the chips from the start of the user's order, as many as fit beside a final overflow chip, and
  the shown view's chip always (it takes the place of the last one that would have fit); order is
  kept. Refitted on every refresh: a resize, a DPI or work-area change, another anchor (also live
  while dragging), the toolbar showing, views added or renamed. There is no free position to clamp:
  a dragged bar drops on an anchor ([S-913]) and is placed inside the area by [S-909].
- **[S-918]** The overflow chip is the Windows command bar's: "…" in one icon-only square, tooltip and
  name "n more views". A click (or Enter / Space; in edit mode the arrows, Home and End reach it last)
  opens a menu of the views left off the bar in bar order, each with its icon in its colour (the
  default view's name semi-bold); a pick shows that view here, which brings its chip onto the bar.
  The menu opens into the display (below a top bar, above a bottom one, beside a column,
  `ViewBarRules.MenuSide`) and is no taller than the work area on that side (`MenuHeight`), so a
  long list scrolls inside it; the edit bar's View menu scrolls inside the work area below the bar too ([S-3540]).
  The overflow chip is part of the bar's box, so it is in the hit region ([S-912]); it is never
  dragged, and chips are reordered among the chips drawn ([S-914]). Chosen over a scrolling strip
  with chevrons: a strip hides the shown view as easily as any other and needs a scroll position
  per display; the overflow keeps every drawn chip one click away and is how Windows overflows a
  command bar.
- **[S-919]** Debug render: `Themia.exe --viewbar-png <outDir> [--views n] [--theme light|dark]`.
  With `--views n` (1..60) it draws n views, the shown one two thirds along, at all eight anchors
  with each of the three labels ([S-5405], the sizes in turn) and two edit-mode shots, and the
  overflow menu of the bottom-left icon-and-text and text-only shots; it prints each bar's box and
  flags one outside the work area.
- **[S-5400]** Each display's bar has a type (`ViewBarSettings.Type`, `ViewBarType`): **Inline** (the
  default, the bar above: every view the bar offers as a chip, side by side or stacked) or **Select**
  (only the shown view, [S-5401]; a click opens the list of the bar's views to pick another,
  [S-5402]). Set by Display ▾ › View bar › Type ([S-3544]), after Show the view bar. At load an
  unknown type resets to Inline and is logged as a repair.
- **[S-5401]** A select bar is one raised button: the shown view, labelled as the bar is ([S-5405]),
  the name cut to the room the bar has, and a chevron; a display showing a view the bar does not
  offer reads "Views". Its tooltip says to click to choose a view. It is a row at every anchor, so
  its room is the work area's width (`ViewBarRules.IsVertical` / `Room` with the type). Anchors,
  sizes, the edit-mode grip and drag ([S-913]), placement inside the work area ([S-909]), the hidden
  preview ([S-907]) and the hit region ([S-912]) are the inline bar's. In edit mode the button takes
  the keyboard like a chip (F6, Enter / Space opens it).
- **[S-5402]** A click opens the bar's one menu (`ViewBar.ViewMenu`, the overflow chip's menu of
  [S-918]): `ViewBarRules.MenuViews` lists every view on the bar for a select bar (the views the
  bar offers, [S-908], in the user's view order), the views left off for the overflow chip. Every
  row has the view's name, with its icon in its colour, or with text only its colour dot
  ([S-5406]); in bar order, the shown one ticked and focused; a pick shows that view on this
  display ([S-19]). It opens into the display and scrolls within the work area there (`MenuSide`,
  `MenuHeight`). It is a standard flyout, opened and closed (a click outside, Escape, a pick) as the
  overflow menu is, with or without edit mode; arrows and Enter are the menu's own.
- **[S-5403]** Type-ahead in the menu: a letter moves to the next view after the focused one whose
  name starts with it, wrapping, so the same letter steps through them (`ViewBarRules.TypeAhead`;
  case-insensitive, no timer).
- **[S-5404]** `--viewbar-png` draws the three labels inline (rows at M, L and XL; columns, icon-only and
  text-only; edit mode) and ends both sets with select shots (bottom left M, top right L icon only,
  bottom centre XL text only, left middle XL, right middle M text only, top centre M in edit mode),
  each closed and with its menu open (`…-select-…-menu.png`); `--theme` and `--lang` apply.
  `--editbar-png --only bar` draws Display ▾ › View bar and its Type and Labels levels.
- **[S-5405]** Each display's bar has labels (`ViewBarSettings.Labels`, `ViewBarLabels`): **Icon and
  text** (the default: the view's icon or initials and its name), **Icon only** (the mark alone in
  one square, [S-911]) or **Text only** (the name alone, padded like a named chip). They apply to
  both types: the chips, the select button and the menu's rows, except that a menu row always has
  the name (a list of marks cannot be read). Set by Display ▾ › View bar › Labels ([S-3544]); at
  load an unknown value resets to Icon and text and is logged. Outside edit mode a chip has the
  view's name as its tooltip only where it does not say it (icon only); the view new displays start
  on keeps its note "(shown on new displays)" with every label, and edit mode keeps its tip of what
  a click, a drag and a right-click do (`ViewBarRules.ChipTip`). Chip widths are measured as drawn,
  so fitting and overflow ([S-917], [S-918]) hold for every label.
- **[S-5406]** Text only keeps a view's colour ([S-3810]) as a dot before its name (half the step's
  icon size), not as the name's colour: the name stays in the theme's text, so it reads on any
  card fill and in both themes, as the icon did beside it. A view without a colour has no dot; on a
  column (left and right middle anchors) every chip keeps the dot's room once one view on the bar
  has a colour, so the names start on one line (`ViewBarRules.Dot`). The menu's rows put the dot
  where the icon was (`ViewMark.Dot`). The flow follows the language, so in a right-to-left
  language the dot sits right of the name.

Not ported: the original's chip "drag-over" highlight (the chip itself moves instead); the free-tier view-limit
tooltip on the bar (licensing refuses through `AllowCreate`). Debug render:
`Themia.exe --viewbar-png <outDir>` draws the real bar, placed by the real rule, at nine
anchor/size/mode combinations on a stand-in display with a taskbar strip ([S-919] for many views).

## The Displays and Views pages (R4-NAV, R5-PAGES)

How a display is given a view, and the view rules the pages wrote. The pages themselves are gone:
every display's and every view's settings are the edit bar's Display and View menus (BAR-MENUS,
[S-3540]..[S-3548]). Rules in Core `DisplayPlan.AssignViews`, `ViewOps.Rename`, `ViewOps.SetColor`
(tests `DisplayPlanTests`, `ViewPerDisplayTests`, `ViewsTests`, `AppPagesTests`, `ViewsMenuTests`).

- **[S-1700]** Retired by [S-3800]: a view's group listed its widgets and held Add a widget,
  Duplicate and Delete rows.
- **[S-1701]** *(Retired by [S-3542]: no Edit layout from Settings; View ▾ is open in edit mode on its display, and a click on a view shows it there.)*
- **[S-1702]** *(Retired by [S-3540]: every command is given on the display whose bar menu it is in; there is no "settings display".)*
- **[S-1703]** *(Retired by [S-3541]: a view's line is dragged in View ▾.)*
- **[S-1704]** *(Retired by [S-3542]: a view's submenu in View ▾ holds what its "…" menu did.)*
- **[S-1705]** A display is never given a new view. The display pass (and import, delete and undo,
  which re-run it) gives every display without a valid assignment an **existing** view: the one
  chosen for new displays ([S-847]), else the active view. It is stored, so switching another display
  later does not drag this one along. A display that has a view keeps it. Views are only ever made by
  the user (Add view, New view, Duplicate). Was: each new display got an empty "Display n" view of its own.
- **[S-1706]** *(Retired by [S-3541]: View ▾ shows a view on its display; there is no Shows drop-down. (`ViewsMenu.ShownOn` is the display's assignment, or the active view before it has one.))*
- **[S-1707]** Renaming a view (View ▾ › the view › Rename…, [S-3542]) goes through
  `ViewOps.Rename`: the name is trimmed; blank, unchanged or an unknown view changes nothing. The
  view's line follows in place.
- **[S-1708]** *(Retired by [S-3544]: Views on the bar is a submenu of switches.)*
- **[S-1709]** *(Retired by [S-3543]: Display ▾'s heading names the display and its resolution.)*
- **[S-1830]** *(Retired by [S-3585]: there is no navigation pane.)*
- **[S-1831]** *(Retired by [S-3546]: no page target names a display or a view; View settings opens View ▾.)*
- **[S-1832]** *(Retired by [S-3540]: there are no Displays and Views pages.)*
- **[S-1833]** *(Retired with the list pages ([S-3540]): no groups to keep open.)*
- **[S-1834]** *(Retired with the list pages ([S-3540]): the bar menus follow the state in place ([S-3531]).)*
- **[S-1835]** *(Retired by [S-3548]: `--editbar-png --only bar` draws View ▾ and Display ▾.)*

- **[S-3800]** *(Retired by [S-3541], [S-3542]: a view is a line of View ▾ and its submenu.)*
- **[S-3801]** No menu reorders by arrows where dragging exists: View ▾ ([S-3541]) and the view bar
  chip's menu ([S-915]) have no Move up / Move down.
- **[S-3802]** Keyboard reordering, the Windows way (the Start menu's pins): Alt+Shift+Up / Down on a
  focused view's line in View ▾ moves it one place (`ReorderKeys.Delta`: exactly Alt and Shift; with
  Ctrl, or an arrow alone, nothing). At either end nothing happens. The move runs after the key,
  through `AppHost.ReorderView` (one undo step), and the moved line has the focus again after the
  list is drawn again. The same drag and shortcut order every list of the menus (the engine's ordered
  lists, [S-3732]: an item takes another's place, `DeskListOrder.MoveTo`; the line's tooltip says
  how). No list or widget menu has arrow buttons or Move up / Move
  down / Move to start: in the Tasks, Habits and Shortcuts widgets the keyboard lease carries the
  shortcut as `WidgetKey.MoveUp` / `MoveDown` (`WidgetKeys`), moving the focused task, habit or the
  cursor's shortcut.
- **[S-3810]** A view may have a colour (`ViewState.Color`, `#RRGGBB`, null = the theme's text colour),
  set in View ▾ › the view › Colour › (the one colour submenu, [S-3542]; `ViewOps.SetColor`:
  normalised, anything else refused, empty clears). Its icon or initials are drawn in it on the view
  bar chips (shown or not), View ▾, and Display ▾'s New displays show and Views on the bar (where a
  view without an icon shows the view glyph). Duplicate and undo carry it.
- **[S-3811]** On load a colour is normalised (`#abc` → `#AABBCC`); one that is not a colour is
  dropped (the theme's), with a repair line.
- **[S-3812]** *(Retired with the pages ([S-3540]): the bar menus are drawn from the state on every change.)*
- **[S-3832]** *(Retired by [S-3545]: New displays show is under All displays in Display ▾.)*
- **[S-3833]** *(Retired by [S-3525]: there is no General page; start with Windows, the desktop icons and
  game mode are the settings menu's first lines. Still no Edit layout switch and no local-network
  switch, [S-3840].)*
- **[S-3834]** *(Retired by [S-3585]: there is no navigation pane and no title bar of a settings window.)*
- **[S-3830]** *(Retired by [S-3526]: there is no Appearance page. Its groups (Theme and colours,
  Item background, Widget cards, Text and size, Layout, then Motion) are the settings menu's
  Appearance submenus.)*
- **[S-3831]** *(Retired by [S-3585]: there are no app pages to describe.)*
What needs a manual pass is BAR-MENUS' ([S-3548]).

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
  as Windows 11 draws a notification, on the family's tokens: the severity glyph (16, centred on the
  title's line) in its role, the accent for Info and Success (no success green, [S-6706]), Caution
  and Critical for a warning and an error; the title BodyStrong, the message Body secondary right
  under it (4 lines max), the action as a button (on the title's line when there is no message, so
  "Deleted 3 widgets · Undo" stays one row; under the message otherwise), a close button of the
  header's 24 with its 16 glyph; 364 DIP wide (never wider than the work area), padded 16, 12, 12, 16,
  the raised sheet's opaque fill and the flyout's stroke (`ShellRole`). Its buttons never take focus.
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
- **[S-886]** A step restores the arrangement (`LayoutShape`): views (id, name, icon, order, groups)
  and every widget's kind, geometry, z-order, header, surface, title and group ([S-2456]). Not content: a widget that
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
  the start then counts as good) or a reset in ⚙ › Back up & reset ([S-3578]). Fails open: an unreadable counter
  is a normal start; a corrupt one starts over (logged).
- **[S-891]** Widget error boundary (`WidgetCard.Fault`): a view that throws while created, bound,
  attached, given new settings, or on a tick is detached and replaced by an error card. The ticker
  routes the faults of subscriptions made inside a card's `CatchFaults` scope (Attach, ApplyConfig)
  to that card; a fault on the first tick during Attach counts as an attach failure, a later one
  swaps on the next UI-queue pass. The card keeps its header (the view's trailing part goes) and
  shows the Status block's row where its content's first row would be ([S-6714]; at the top inset
  without a header above): the caution warning, "This widget stopped working." and Retry, and under
  it Details, a standard button opening a flyout with the exception type and
  message (scrolling past 240 DIPs) and **Copy**, which puts the widget's kind and the whole
  exception with its stack on the clipboard, through the log's redactor ([S-983]). The flyout is a
  windowed popup, never cut off at the card's edge by the surface's region ([S-851]; user
  report). Logged with the widget id and kind. Retry builds the view again in place. Below 150×110
  DIP the Details go and the sentence shortens to "Stopped working."; on a row too narrow for the
  sentence beside Retry the sentence goes ([S-6714]); Retry always stays. Safe mode's paused card
  ([S-890]) is the same row with the pause glyph, "Paused: …" and Start.
- **[S-892]** ⚙ › Back up & reset › Reset all settings… (asked first, [S-3578]): the current state is written to
  `state.before-reset-<stamp>.json` beside the state file (if that fails nothing changes and an error
  notice says why), then views, widgets, appearance and preferences become a fresh profile's (Start
  with Windows is kept, as on import); the undo history is cleared; what was kept, and where, under the line ([S-3576]). Accounts,
  notes, tasks and habits are separate files and are kept.
- **[S-893]** First-run desktop (`AppState.CreateDefault`, also what a reset and a corrupt-state
  recovery show), the maker's own two views (user requirement), so switching views is discoverable:
  named Home and Work (translated, `shell.view.homeName`/`workName`); Home is active. Laid out on a
  2560×1440 grid and stored as fractions, so it holds on any display. Home, clock first: Clock (digital,
  no seconds, no date) top right (2250, 0, 310×180); Shortcuts grid under it (2250, 180, 310×160) with
  Recycle Bin, This PC, Downloads, Documents, Pictures and Home (shell targets, labels empty so the
  names are the translated target names); Weather now (large icon) top left (0, 0, 310×180); CPU,
  Memory, GPU and Network as graphs down the right edge (x 2250, y 920/1040/1160/1280, 310 wide,
  89/1080 of the screen tall); these seven are transparent and headerless. Along the top centre, six
  60×60 quick-setting tiles from x 1090: Do not disturb, Color filters, Night light, Airplane mode,
  Bluetooth, Wi-Fi. Work: Clock (digital, with the date, transparent, headerless) top right (2330, 0,
  230×120); Calendar (month) (0, 0, 800×710), Tasks (list) (800, 0, 510×710) and Notes (1310, 0,
  510×710), each with its header on top and the default background. Nothing in it is anyone's data:
  the weather has no place and shows its setup ([S-2848]), Calendar and Tasks show Set up until an
  account exists ([S-1860]), the note is made on first show ([S-1346]), every id is new on each
  call. Every widget is inside the screen, none overlaps another, and load validation changes
  nothing. The two views and 17 widgets fit the free tier ([S-1065]), with room to add a widget.
  The seed is data (`FirstRunDesktop`): widget ids, grid rects, bare or not, and each widget's
  differences from its default config as a JSON fragment in the state's own shape, built through the
  descriptors and the state's JSON and then copied (so a shortcut's ids are new, as a paste's are).
  It names no config type; a seeded id this build does not have is left out and logged.
- Not ported: the original's pending-operation framework (an operation that resolves later; nothing
  here records one yet, and file operations will add their own step when they need it), and
  per-window boot counters (one process here, so one counter).

## Widget polish 2: leftover parity and layout gaps (POLISH-2)

Items earlier packages left "not done", each checked against the original on `main` first: what
the original had is ported, what it never had is recorded here and not built.

- [S-1620]: moved with the Gallery widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gallery/`).
- [S-1621]: moved with the Habit tracker widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.HabitTracker/`).
- **[S-1622]** `Disclosure.Fits` / `Disclosure.TakeFirst`: a rung offered in several sizes takes the
  richest that fits, and only a total miss closes the ladder.
- **[S-1623]** *(Retired: the power tile and its side panel are gone, [S-1910].)*
- **[S-1624]** A mixer row's second line, where the tier gives its rows two ([S-449], [S-6712]), says
  what the app is doing (`MixerFill.Detail`: "Playing", "Silent", "Muted", "Windows sounds"). The rows
  no longer share an expanded card's height (G5): what a short list leaves stays empty, and no hint
  or link to Windows' mixer fills it (user decision: a widget shows its own content, not filler).
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

The widget is a module of its own (`src/Widgets/Themia.Widget.Folder`, its SPEC.md). Here: the file
services it reads through `IWidgetHost` (`FileIcons`, `FileClipboard`, `Folders`, `FileOps`, `ExplorerMenus`;
`Services/Files/{DragDropInterop,FileClipboard,ShellFileOps,ShellContextMenu,ShellNewTemplates,FolderWatch,
FileIcons,GitStatus,ZipFiles,OpenWithApps,HostFileServices}.cs`, `Services/FolderListing.cs`), reusable by
Shortcuts, Recent files and the shell, and the shared rules (the SDK's `FolderBadges`, `FolderFormatting`,
`FolderPath`, `Files/{FileDrops,FileNames}`; Core's `Files/{DropFiles,FileDescriptors}`). Tests:
`FolderBrowserTests` (+ `FolderFormattingTests`, `FolderPathTests`); the module host's `FolderHostTests`.

- [S-1320], [S-1323], [S-6411], [S-1326], [S-1993], [S-1328], [S-6412], [S-6413], [S-6414], [S-6415], [S-6416], [S-6417], [S-6418], [S-6419], [S-1334], [S-1335], [S-1339], [S-4120], [S-4121], [S-4122], [S-4123]: moved with the Folder widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Folder/`).
- **[S-1321]** The header search (orig. S-21, S-50): comma-separated keywords, OR, case-insensitive substring; it is the
  only filter (the stored keyword filter is gone, [S-4121]). Names hide the extension unless `ShowExtensions` (folders and
  dot-files keep theirs). Folders always come first, as in Explorer. Sort and search re-draw what is in hand; only the
  path re-reads the disk.
- **[S-1322]** Details columns: Size, Modified, Type (shell type name, `SHGetFileInfo`, cached per extension), Cloud, Git,
  Items (folder entry count, counted off-thread up to 1000, shown "999+"). Shown in the user's order while each fits beside
  a 120 DIP name; the first that does not fit ends the row. The cells are the Row block's ([S-6711], `RowBlock.Cells`):
  Body, secondary, 12 apart, at widths for their worst case in Body (Size 72, Modified 96, Type 120, Items 80, Git 24,
  Cloud one 16 glyph); the titles a Caption line over them. Header click sorts Name/Size/Modified (again = reverse, saved);
  the columns are chosen in the settings only ([S-1746]; the header has no menu of its own).
- **[S-1324]** Real shell icons (`IShellItemImageFactory`, shared per extension except exe/lnk/ico/url…, customised folders
  per path) and image thumbnails in Grid/Icons (`SIIGBF_THUMBNAILONLY`; never for cloud placeholders), at the family's
  icon sizes (16 in a row, 32 or 48 in a cell, [S-6704]). Caches bounded and shared; extraction serialised (orig. S-64).
  The cloud state from attributes (cloud-only / always kept / available) is the Cloud column's 16 glyph (secondary) and a
  line of the item's tooltip; no badge over the icon (it would be under the family's 16 and in the accent or a success
  green, [S-6706]; group G6 2026-10-29). "Always keep on this device" / "Free up space" set the pin bits.
- **[S-5175]** Icon and thumbnail caches (`LoadCache`) keep only what a load found: one load per key in flight, and a
  load that found nothing is asked again on the next render, never kept as "no icon" (orig. #4/#16: a transient
  extraction failure left a type's items as glyphs until restart).
- **[S-1325]** Git: only when a `.git` exists up the tree; `git --no-optional-locks status --porcelain=v1 --branch -z`
  off-thread, single-flight per repository; no git = no badge. The header's informing label "main ~2 ?3" (Caption,
  secondary; List/Details; the first part to give way, [S-6710]) and the Git column (file code; "•" for a folder with
  changes beneath).
- **[S-1327]** Selection is the desktop's, through the one item selection ([S-6620]..[S-6629]): click, Ctrl, Shift,
  a press on a selected item narrowing on release (a multi-item drag keeps it), background click clears, right-click,
  the band, the selection keys (Ctrl+arrows, Space/Ctrl+Space, Ctrl+A among them) and the look; the selection stays,
  drawn inactive, while the user is elsewhere ([S-6626]). The widget's own keys via the lease (`IWidgetKeys`): Enter opens,
  Delete, F2, Menu, Ctrl+C/X/V (`WidgetKey.Copy/Cut/Paste`, Ctrl alone; other chords still never reach widgets),
  Backspace (`WidgetKey.Back`); Escape clears selection and focus. Double-click mode opens on double-click; Single opens
  on a plain click (Ctrl/Shift only select).
- **[S-1329]** The file clipboard is the real Windows one: the shell's data object (CF_HDROP, shell ID lists) plus
  `Preferred DropEffect` (Move for cut), `OleSetClipboard` + `OleFlushClipboard`. Paste reads CF_HDROP and the effect from any
  source (Explorer, other widgets); a cut-paste moves, then empties the clipboard. Cut items are dimmed while the clipboard
  sequence number is still the cut's.
- **[S-1330]** Shell operations through `IFileOperation` on one STA worker (undoable in Explorer; the shell's progress and
  conflict UI): copy, move, rename, and delete = **Recycle Bin only** (`FOFX_RECYCLEONDELETE|FOF_ALLOWUNDO|FOF_WANTNUKEWARNING`)
  after a confirmation flyout at the widget. The item menu is [S-6416]'s; Show more options is the shell's own
  `IContextMenu` (the owner window is subclassed for the menu's life so Send to / Open with fill in).
- **[S-1331]** Background menu ([S-6418]): New > Folder + ShellNew templates (NullFile/FileName/Data;
  Command entries skipped). New items get the next free
  "New folder (2)" name and start an in-place rename; rename edits the stem when extensions are hidden, validates Windows
  name rules and keeps the item selected.
- **[S-1332]** Drag out: an OLE drag of the shell data object with our `IDropSource` (`SHDoDragDrop`), posted after the
  pointer handler. Drop in: through the surface's OLE target ([S-1020]..[S-1024], `IWidgetDropTarget`): plain drop moves,
  Ctrl copies, into the folder row under the pointer (marked as a drop target, [S-6627]) or the folder on screen, carried out by that folder's
  shell drop target; a drag whose every item is already there or is the destination's ancestor is refused
  (`FileDrops.Pastable`, orig. `dropTarget.ts`). Virtual files (`FileGroupDescriptorW`, SDK layout 592 bytes / name at 72)
  are saved on paste, and on drop by the shell's target.
- **[S-1333]** `FolderWatch`: a `FileSystemWatcher` plus every in-app file operation's notice; events coalesce into one posted
  reload (no timers); a reload in flight is marked dirty and followed by exactly one more. No watcher (some shares): 5 s
  timer, else the 30 s backstop. Disposed exactly once (orig. S-63).
- **[S-1336]** Tests use made-up paths and byte buffers only; no test touches a real file.

Needs live verification: drag in/out with
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
- **[S-1014]** The settings menu's Appearance › Motion ([S-3529]): one submenu per moment (Startup,
  Unlock, View change) with the style, the length of the whole animation (100..5000 ms) and Play it
  now ([S-6330]: live values never move, so there is no switch for them). Any change replays that moment on the
  desktop. The startup sound ([S-1008], [S-1009]) is removed.
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
  never lingers over another app. Folder rows also mark the folder a drop enters as a drop target ([S-6627]).
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

## The desktop layer (DESKTOP)

Themia can take over what the desktop shows: Explorer's icons hidden for the session, and each of
the desktop's files, folders, shortcuts and shell items a widget on the grid, placed, moved and
removed like any widget and different in every view (the user's decisions of 2026-10-29). Phase
D1 built the choice, the full-display surface, the session-only hide with its restore, the items
as widgets and their placement, and click, double-click and Enter; D2 the rest of Explorer's
desktop outside edit mode ([S-5540]..[S-5550], [S-5591]); D3 Themia's own lines in the background
menu, New ›'s rename, the keyboard after Win+D, displays that come and go, mixed DPI, UI Automation
and living beside other desktop programs ([S-5551]..[S-5575]). The item widget's own look is
its module's `SPEC.md` (`src/Widgets/Themia.Widget.DesktopItem`, [S-5600]..[S-5699]).

Owning files: `Themia.Core/Desktop/*`, `Themia.Core/Shell/DesktopIconRules.cs`,
`Themia.App/Interop/DesktopIcons.cs`, `Themia.App/Interop/DesktopNamespace.cs`,
`Themia.App/Services/AppHost.Desktop.cs`, `Themia.App/Services/AppHost.DesktopDesk.cs`,
`Themia.App/Shell/DesktopWindow.Desktop.cs`, `Themia.App/Shell/DesktopRoot.cs`,
`Themia.Sdk/Files/DesktopItem.cs`, `Themia.Sdk/Files/DesktopRename.cs`,
`Themia.Sdk/Files/DesktopIconPixels.cs`, `Themia.Sdk.WinUI/Modules/IDesktopItems.cs`. Tests:
`DesktopLayerTests`, `DesktopParityTests`, `DesktopEdgeTests` (Core), the module's own.

- **[S-5500]** **The choice.** ⚙'s one line **On the desktop** chooses what the desktop shows while
  Themia runs: **Themia's items**, **Windows' icons** or **Nothing** (`Preferences.Desktop`, a
  `DesktopShows`). It replaces "Show desktop icons": one setting, one level ([S-3592]); no view,
  display or widget holds a copy, and it applies to every display. The default is **Themia's
  items**, since Explorer parity holds outside edit mode ([S-5540]..[S-5550]): a new state hides
  Explorer's icons for the session and lays every desktop item into the primary display's view
  ([S-5522]). A stored choice that is no `DesktopShows` is repaired to Windows' icons, the desktop as
  Windows has it. A pick is stored and applied at once.
- **[S-5501]** **The surface covers the display** while the choice is Themia's items: outside edit
  mode too, every display's hit region is its whole work area (as in edit mode, [S-851]), so no
  click on empty desktop falls through to Explorer; empty desktop is Themia's (the press of
  [S-5502], and in D2 the lasso, the background menu and drops). The taskbar stays Windows' (the
  region stops at the work area), and so does the wallpaper, drawn under the transparent surface.
  With Windows' icons or Nothing the region traces the widgets as before ([S-851]).
- **[S-5502]** A press on empty desktop under Themia's items, outside edit mode, clears the item
  selection (Ctrl or Shift keep it, for the band, [S-5544]), ends a card's keyboard lease and gives
  the keys to the desktop's items ([S-5550]); a left press may become the band, the right button's
  release opens the desktop's background menu ([S-5548]).
- **[S-5503]** **Explorer's icons are hidden for the session only** while the choice is Themia's
  items or Nothing: Themia hides the desktop's icon list window (the `SysListView32` of the shell's
  `SHELLDLL_DefView`, found as in [S-930]) with `ShowWindowAsync(SW_HIDE)`, never with the shell's
  toggle command, so Explorer's own setting (its desktop menu's View › Show desktop icons,
  `Explorer\Advanced\HideIcons`) and its check mark stay as the user left them, and an Explorer
  restart builds a list that shows the icons by itself. While the choice holds, the hide is kept,
  event-driven: the list's own show event (the WinEvent hook of [S-932]) hides it again, and on
  `TaskbarCreated` (Explorer restarted) the new list is found, hooked and hidden. A list shown again
  more than `DesktopIconRules.MaxRehides` (8) times since the choice was last applied is left
  shown, logged once: Themia never fights Explorer in a loop.
- **[S-5504]** **Restore.** Themia shows the list again (`ShowWindowAsync(SW_SHOWNA)`) when the
  choice becomes Windows' icons, on quit, at every start before the choice is applied (so a crash
  or a kill of an earlier run is undone by the next start), and from the last-chance handlers (an
  exception nothing caught off the UI thread, and process exit): one non-blocking call with no COM
  and no reply awaited, safe while the process goes down. It shows the list only when the list is
  hidden and Windows' own setting says shown (`HideIcons` not 1, no `NoDesktop` policy): it never
  shows icons the user hid in Windows (`DesktopIconRules.Restore`). A process killed outright
  cannot restore: the icons stay hidden until Themia starts again, Explorer restarts or the user
  signs out, and nothing persisted has changed.
- **[S-5505]** Picking Windows' icons while Windows' own setting hides them sends the shell's own
  command once ([S-930], [S-931], [S-939]): the user asked to see Windows' icons. That pick is the
  only time Themia writes Windows' setting; it is never re-asserted at start or on an Explorer
  restart, and Themia's items and Nothing never write it.
- **[S-5506]** Item widgets show only while the choice is Themia's items. Under Windows' icons and
  Nothing they stay in their views untouched but have no card (not drawn, no clicks, not selected
  by Select all), Add widget offers none ([S-5523]), and the namespace is neither read nor watched.
- **[S-5510]** **The items are the shell's desktop namespace**: the desktop root's own
  `IShellFolder` enumeration (`SHGetDesktopFolder`, `EnumObjects` with folders and non-folders),
  which merges the user's and the Public desktop and lists the shell items Windows' desktop-icon
  settings show (This PC, the Recycle Bin, the user's folder, Network, Control Panel): exactly what
  Explorer's desktop lists, nothing Themia decides. Each is a `DesktopItem`: its **key**, the
  shell's parsing name relative to the desktop (a full path for a file, `::{CLSID}` for a shell
  item, `DesktopItemKeys`), its name as Explorer shows it (`SHGDN_NORMAL`: no `.lnk`, extensions as
  Explorer's setting says) and its kind (file, folder, shortcut, shell item). Hidden items are listed
  as Explorer's own settings say ([S-5547]). Read off the UI thread (a COM worker); a failed read keeps the last listing, is
  logged, and is never taken as "every item gone".
- **[S-5511]** **Kept current by the shell's change notifications**, never a poll:
  `SHChangeNotifyRegister` (shell and interrupt level, new delivery) on the desktop root and on both
  desktop folders (one registration per folder: a call naming more crashes in windows.storage on
  build 26300), for items created, deleted, renamed, updated, their attributes and the shell's
  icons, posted to the command window. A burst is coalesced without a timer (one re-read queued
  while one is pending, as [S-1333]); a rename's old and new keys are kept for [S-5520]. Registered
  while the choice is Themia's items, dropped otherwise.
- **[S-5512]** An item's icon is the shell's as Explorer's list draws it: the system image list's
  image (the extra-large list at up to 48 pixels, the jumbo list above) with the item's overlay
  (the shortcut arrow, a share, a sync engine's state) composed by the image list itself,
  extracted off the UI thread one at a time with every other icon ([S-1324]) and cached per item
  and size. A key the shell cannot parse any more (a moment before its removal, a preview) gets
  its type's icon from its name, with the shortcut arrow when it is a shortcut.
- **[S-5520]** **Reconcile** (`DesktopReconcile.Plan`, pure): after each read, the listing, the
  known set ([S-5522]) and every view's item widgets give the changes, applied as one mutation that
  is not an undo step (the history is rebased: the disk changed, not the arrangement). In order:
  (1) a rename heard from the notifications rewrites the key of every widget of the old item in
  every view, keeping its place; (2) an item no longer listed (deleted, moved or renamed away) has
  its widgets removed from every view; (3) an item not in the known set is new and gets one widget
  in the view showing on the primary display (a drop's own display in D2, [S-5591]), at the next
  free cell ([S-5521]), and no other view gets it; (4) the known set becomes the listing. An item
  known but in no view stays unplaced, by the user's own removal ([S-5523], [S-5524]). It runs after
  each read and after an undo or redo (which may bring back a widget whose item has gone), only
  while a listing is held. A rename made while Themia was not running reads as a removal and an
  arrival. Nothing in the reconcile touches a file.
- **[S-5521]** **The next free cell** (`DesktopGrid.NextFree`): cells of the item's size (90 × 100 with two name lines
  DIPs, [S-5601]) from the work area's top-left corner, down a column and then the next column to
  the right (Explorer's order), as fractions of the display ([S-01]); the first that overlaps no
  widget of the view. A view with no free cell leaves the item unplaced, logged, and Add widget
  reaches it. New items of one read take cells in `DesktopItemOrder` (shell items first, then
  folders, then files, each by name ignoring case: Explorer's Sort by name).
- **[S-5522]** The known set is `desktop.known` in the state document: the keys of every item the
  reconcile has seen. Load keeps it a list of distinct (case-insensitive), non-empty keys of at
  most `DesktopItemKeys.MaxLength` characters, dropping the rest loudly. The first time Themia's
  items is chosen it is empty, so every item arrives in the primary display's view.
- **[S-5523]** **Unplaced items stay reachable**: while the choice is Themia's items, Add widget's
  Files subject has an **On the desktop** folder listing the items with no widget in the view being
  added to, in `DesktopItemOrder`; picking one places its widget like any widget ([S-1587]). With
  none unplaced the row is not there. The item kind is never offered on its own: an item widget
  always stands for one item.
- **[S-5524]** **Two deletes, apart.** In edit mode Delete (the properties menu's, the card menu's,
  the key) removes the item's widget from this view only: the file stays on the desktop, its
  widgets in other views stay, and it is listed under [S-5523]. Its button on the properties menu's
  top row and its line on the card menu say so: **Remove from this view** while every widget it acts
  on is a desktop item's (`DesktopBackground.RemovesFromView`, `QuickSetOffer.RemovesFromView`); a
  selection with any other widget says Delete. Outside edit mode Delete is the shell's own delete of
  the file, with the shell's prompt ([S-5545]).
- **[S-5530]** **Opening** outside edit mode: a double-click on an item, or Enter with the keys
  ([S-5550]), opens the selection (the double-clicked item alone when it is not selected), at most 15
  items at once (the rest logged), through the launcher (a path as it is, a shell item as
  `shell:::{CLSID}`, `DesktopItemKeys.LaunchTarget`); a double-click on an item never toggles edit
  mode, and a right-click on one never opens the card's menu.
- **[S-5540]** **One selection** of the desktop's items, across every item widget on every display,
  as Explorer's desktop keeps its icons' (`DesktopSelection`, Core, over the SDK's `ItemSelection`,
  [S-6620]..[S-6626]; the host's `AppHost.DesktopDesk`, read by the widgets through
  `IDesktopItems.IsSelected`, `ShowsFocus`, `Active`): a press selects the item alone, Ctrl toggles
  it, Shift selects the range from the anchor, a press on a selected item narrows only on release
  (so the selection drags whole), a right press keeps a selection it lands in. The order a range and
  Home/End run in is the desktop's (`DesktopNav.Order`): display by display from the left, down each
  column, then the next; an item shown on two displays (two views) holds one place. An item no
  longer shown leaves the selection; the look is the SDK's one ([S-6627]), inactive while another
  window has the keyboard ([S-6626]).
- **[S-5541]** **Arrows by place** (`DesktopNav.Step`): the nearest item that way in the same column
  (Up, Down; Page Up and Down the same) or row (Left, Right), the boxes overlapping across the
  arrow; with none there the nearest within 45 degrees (the next display, a column that does not
  line up); with none the focus stays, as at the end of Explorer's column. Shift extends from the
  anchor, Ctrl moves the focus only (its cue drawn, [S-6628]), Ctrl+Space and Space toggle, Home and
  End go to the first and last in the order, Ctrl+A selects every item shown on every display.
- **[S-5542]** **Type to find**: letters typed with the keys select the next item whose name starts
  with them ([S-1980]'s `TypeAhead`, the desktop's order, a pause starting over).
- **[S-5543]** **F2 renames in place** (`DesktopRename`, SDK): the focused item's name becomes a box on
  its widget (the one the keys came from, else the one on the display with the keyboard), holding
  the name as shown with the part before a shown extension selected (a folder's and an
  extension-hidden name whole); Enter or a click elsewhere commits, Escape reverts. The full name
  puts back a hidden extension and a shortcut's `.lnk`; a name Windows refuses (`FileNames`) or one
  another item in the same folder has (case aside) keeps the box open with a tip saying why; an
  unchanged one closes it. The rename is the shell's own (`IShellFileOps.RenameAsync`: its prompts,
  Explorer's undo); the notification then rewrites the key ([S-5520]) and the item stays selected
  under it. The shell menu's Rename opens the same box. Shell items (This PC, the Recycle Bin) are not
  renamed in place.
- **[S-5544]** **The band** on empty desktop: a left press there past the 5-DIP dead zone
  ([S-6624]) draws the SDK's band (`SelectionChrome.PaintBand`) on the surface's ghost layer and
  selects, live, the item widgets of that display it touches: alone, added to the earlier selection
  with Shift, toggled with Ctrl ([S-6625]). What it selected stays when the button comes up.
- **[S-5545]** **Commands** with the keys: Delete is the shell's own "delete" verb on the selected
  files (`IContextMenu`, as Explorer invokes it), with the shell's own prompt as the user set it up
  (to the Recycle Bin); Shift+Delete the same verb with Shift down, so the shell deletes for good
  after its own prompt (shell items are never deleted). Ctrl+C and Ctrl+X put the selected files on
  the clipboard as Explorer does ([S-1329]; a cut item is faded while the cut is pending); Ctrl+V
  pastes the clipboard's files (or virtual files) into the user's desktop folder through the shell's
  copy or move, landing per [S-5591]. Alt+Enter opens the shell's Properties, F5 reads the desktop
  again, the Menu key and Shift+F10 open the shell's menu for the selection at the focused item
  (else the background menu). **Ctrl+Z is not offered**: Explorer's undo lives in Explorer's own
  process and no API reaches it from outside, so Themia's desktop has no undo of file operations
  (its own arrangement keeps the edit bar's undo, [S-5546]).
- **[S-5546]** **Moving items on the desktop**: the desktop's own drag ([S-5549]) let go over empty
  desktop of any display moves the dragged item widgets of the display it started on, never a file:
  the held one to the cell under the pointer, the others keeping their offsets in whole cells
  (`DesktopCells.Move`), one that would leave the work area or cover a staying widget at the next
  free cell from there; onto another display's view the widgets go there (one widget per item in a
  view: one already there gives way). One undo step of the arrangement.
- **[S-5547]** **Explorer's view settings followed**: "Hidden items" and "Hide protected operating
  system files" (`SHGetSetSettings`) choose the namespace's enumeration flags
  (`DesktopViewSettings.EnumFlags`), a hidden item drawn faded; "File name extensions" through the
  shell's display names. A settings change (`WM_SETTINGCHANGE`, Explorer's folder options among them)
  reads the desktop again.
- **[S-5548]** **Menus**: a right-click on an item opens the shell's own full menu for the selection
  (`IContextMenu` of the items, `ShellContextMenu.ShowForItems`, every extension's lines, Send to and
  Open with filling themselves through `IContextMenu2/3`'s messages); its Rename is the desktop's box
  ([S-5543]). A right-click on empty desktop opens the desktop's own background menu: the desktop
  root's `IShellFolder::CreateViewObject` menu (`ShowForDesktop`), so New ›, Paste, Paste shortcut,
  Display settings, Personalize and third-party lines appear as Explorer's desktop shows them, with
  New ›'s owner-drawn lines forwarded. **View, Sort by, Group by and Refresh are left out**: they are
  Explorer's own view's (they would arrange Explorer's hidden icon list); Themia's arrangement is the
  grid and edit mode, one meaning only. What a background command brings (a new folder, a paste)
  lands where the click was ([S-5591]). Themia's own lines are in it too ([S-5551]).
- **[S-5549]** **Drags**: an item pressed and moved past the dead zone drags the selection (the item
  alone when it is not selected) out as the shell's own data object with the shell's drag image
  (`SHDoDragDrop`, the Folder widget's `DragDropInterop.DragOut`): onto Explorer, an app or the
  Recycle Bin the target does what it does. Dragged over an item widget, a folder, an app, a shortcut
  or the Recycle Bin takes it through its own shell drop target (`IShellItem` bound to
  `IDropTarget`, the drag's `ForwardOver`/`ForwardDrop`), the item drawn as the drop target while it
  answers; an item without a drop target refuses (asked once per hover), and an item in the drag
  refuses itself. Dropped on empty desktop, from outside, the drop goes to the user's desktop folder
  through the shell's own drop target (copy, move, link, the right-drag menu, virtual files from
  Outlook or a browser), landing per [S-5591]; the desktop's own drag there moves widgets ([S-5546]).
- **[S-5550]** **Who has the keys**: a press on an item gives its card the keyboard lease ([S-800]),
  a press on empty desktop gives it to the desktop's items on that surface (the surface takes the
  foreground and drops `WS_EX_NOACTIVATE` while it holds them, [S-800]'s rule); either way the keys
  above go to the one selection. Escape ends the hold and hands the keyboard back; a click on
  another app or a card ends it. The selection stays (drawn inactive) when the keys go elsewhere.
- **[S-5551]** **Themia's lines in the background menu**: with Themia's items, empty desktop's
  right-click menu ([S-5548]) holds **Edit widgets** (edit mode on, as the card menu's Edit layout and
  Ctrl+Alt+W) and **Add widget** (edit mode on, then the picker where the right-click was, as the
  surface menu's Add widget, [S-1744]), so edit mode is reachable with no widget on screen and no
  shortcut known. One group where Windows puts an app's lines: right after Paste and Paste shortcut,
  else first, before the third-party lines, New › and Display settings, a separator on either side and
  never two in a row (`DesktopBackground.Slot`, over the lines the shell filled in). Their ids lie past
  every id the shell's handlers were given, so the shell never sees them; their words are Themia's
  ("Add widget" is the bar's own). Debug: `--desktop-menu-check <out.txt>` builds the menu as a
  right-click would and writes its lines, never showing it or running a command.
- **[S-5552]** **New › renames what it made**, as Explorer does: a command chosen in New › (the
  submenu that holds New › Folder, `DesktopBackground.IsNewCommand`) expects its arrival at the click
  ([S-5591]) with `Renames`; the first item that lands for it is selected and its rename box opens
  ([S-5543]) once the reconcile that placed it is applied. A paste, a drop or any other command renames
  nothing.
- **[S-5553]** **The keyboard after Win+D** stays where Windows puts it. Show desktop makes
  Explorer's desktop window the foreground (the z-order controller hears it and lifts the surfaces
  over it, [S-33], so the items stay visible and take clicks) and Explorer gives the keyboard to its
  own icon list, hidden. Themia cannot take the keyboard there properly: `SetForegroundWindow` is
  refused to a process that did not receive the last input (the Win+D was Explorer's), and the ways
  around the foreground lock (attaching to Explorer's input queue, a synthetic key press, a timer
  retrying) are exactly the hacks Themia does not use. So after Win+D the first click on Themia's
  desktop (an item or empty desktop) gives it the keys ([S-5550]), and from then on arrows, Enter, F2
  and the rest work as on Windows' desktop. Until that click, keys typed go to Explorer's hidden list:
  a manual pass checks what Explorer does with them.
- **[S-5560]** **A display unplugged**: the listed items placed only in the view of a display that is
  no longer connected (its stored assignment, [S-18]) and in no view a live surface shows get a
  stand-in widget each in the primary display's view, at its next free cells ([S-5521]) in the
  stranded view's own order, recorded with their cells (`desktop.fallbacks`, written only while there
  are some, repaired at load). The unplugged display's view is never touched: its item widgets keep
  their cells. A view no display shows is the user's own (per view, the user's decision 1) and lends
  nothing; a display Themia is switched off on is still connected and lends nothing either (its items
  are listed by Add widget, [S-5523]). Not an undo step (`DesktopDisplays`, after every display pass and
  every read of the desktop).
- **[S-5561]** **A display plugged in** shows its view as the user set it ([S-18]: its stored
  assignment; a new display the active view), and new files still land on the primary display's view
  ([S-5520]). A stand-in whose item a shown view holds again (its display is back) goes; one the user
  moved off its cell, or removed, is the user's own from then on and stays.
- **[S-5562]** **The work area changing** (the taskbar moved, resized, or no longer hiding): in every
  shown view, an item widget no longer whole inside its display's work area moves to the free cell
  nearest after where it was (`DesktopCells.FreeFrom`), as Explorer keeps its icons off the taskbar;
  one with no free cell stays. A work area that grows moves nothing back, and no other widget follows
  the work area ([S-841]). Not an undo step.
- **[S-5563]** **Mixed DPI**: an item's icon is drawn at its display's own scale
  (`DesktopIconPixels.For`: a 48 icon is 48 pixels at 100 %, 60 at 125 %, 72 at 150 %, 96 at 200 %),
  from the extra-large list up to 48 pixels and the jumbo list above (drawn down, as Explorer does);
  an item with no large image, which the jumbo list returns as a small image in a corner of an empty
  square, is the extra-large image drawn up (`HoldsOnlySmallImage`). A display's scale changing, or
  the widget moving to a display of another scale, asks for the icon again at the new pixels. The cell
  stays its size (90 × 100 with two name lines, [S-11204]) on every display; the selection's boxes are in physical pixels across displays
  ([S-5540]).
- **[S-5570]** **Each item to a screen reader** (UI Automation, the item view's peer): a list item
  named as Explorer shows it, its place in the desktop's order and the count ("3 of 12",
  `DesktopNav.PositionOf`), the selection item pattern over the one selection (select alone, add,
  remove, its container the desktop's list), invoke opening it ([S-5530]), and a value that is its
  name, set by F2's rules (`IDesktopItems.RenameTo`; read-only for shell items). The focused item has
  the keyboard focus while the desktop holds the keys; a focus that arrives on it (an arrow, a click)
  and a selection that changes are announced. A refusal is logged, never thrown back into UI Automation.
- **[S-5571]** **The surface is the desktop's list** while Themia's items show outside edit mode
  (`DesktopRoot`): a list named **Desktop**, as Explorer's is, holding the items in the desktop's order
  before anything else, with the selection pattern (several at once, none required) and the focused
  item as its focused element. In edit mode, and under Windows' icons or Nothing, it is a plain pane.
- **[S-5575]** **Living beside other desktop programs** (`DesktopCoexistence`), read from the
  desktop's windows when the choice is applied, at start and when Explorer restarts (`TaskbarCreated`),
  never by a message to another program's window: a window of another process among `Progman`'s or a
  `WorkerW`'s children is a wallpaper engine's (Wallpaper Engine, Lively): it changes nothing, as it
  draws under the surface (an interactive wallpaper no longer gets the clicks on empty desktop, which
  are Themia's, [S-5501]); a desktop view (`SHELLDLL_DefView`) of another process means something has
  replaced the desktop's icons; the re-hide cap reached ([S-5503]) means something keeps Explorer's
  icons shown. In those two cases Themia never fights: its items stand down (no item widget has a card,
  the surface traces the widgets as under Windows' icons) and Explorer's list is neither hidden nor shown
  by Themia, and ⚙ › On the desktop says why under the choice. Picking a choice again (the same one
  too) tries once more. A program that hides Explorer's list itself (Fences can) is not told apart from
  an earlier run of Themia that was killed with the list hidden, so it is not detected: both its icons
  and Themia's items show (a manual pass with Fences).
- **[S-5590]** *Retired*: D3's plan, now [S-5551]..[S-5575].
- **[S-5591]** **Where an arrival lands** (`DesktopLandings`, `DesktopCells`): a drop on empty
  desktop names the files it brings (the drag's file names and virtual file names) and the point;
  when they arrive, their widgets go to the view of the display it was dropped on, at the cell under
  the point or the next free one after it down its column (then the next columns), other views
  getting none ([S-5520]'s step 3 with its own view and point). A paste names its files and lands at
  the last press on that display's empty desktop, else at its next free cell; a background-menu
  command (New ›, Paste) expects whatever arrives next at the click. A hint that names nothing is
  dropped by the next press on the desktop, a newer hint replaces an older one, and an arrival no
  hint names lands as [S-5520] says.

## Shortcuts B: drop, reorder and app layouts (PD)

Original `widgets/others/shortcut` S-28..S-38, S-50..S-60, S-65, S-68..S-74. The widget is a module of
its own (`src/Widgets/Themia.Widget.Shortcuts`, its SPEC.md: its drops, reorder and drag out). Here: the
app layouts and the host's side of a drop: the SDK's `LayoutTree`, `LayoutPlan` and
`Model/Configs/AppLayoutModel.cs` (with `IHoldsAppLayouts`), Core's `LayoutWindows` and `Files/DropUrls`;
App: `Services/AppLayoutLauncher.cs`, `Shell/LayoutEditorWindow.cs` (behind `IWidgetHost.AppLayouts`).
Tests: `ShortcutsDropLayoutTests`; the layout item's config, the module host's `ShortcutsHostTests`.

- [S-1400], [S-1401], [S-1410], [S-1415]: moved with the Shortcuts widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Shortcuts/`).
- [S-1402] Browser links: `UniformResourceLocatorW`, else the http(s) lines of the text. A Start menu app
  (shell items, no paths) is found by name among the Start menu's shortcuts and added as that `.lnk`; a
  packaged (Store / MSIX) app with no shortcut is added as its Apps-folder item
  `shell:AppsFolder\<AUMID>` from the item's parsing name, named as Start names it (issue 189: Claude).
  `LaunchTargets` accepts that one shell name with a separator, and only for a packaged app's id
  (`<name>_<13-char publisher id>!<app id>`, `IsPackagedAppId`); anything else is skipped (logged).
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
- [S-1411] "Edit layout…" (item menu) and "App layout…" (the properties menu's Add, [S-1388], then
  straight into the editor) open one editor window for the app, retargeted when asked again; Escape and Done close it. It
  holds only the item id and writes through `AppHost.Mutate`; if the item disappears it says so.
- [S-1412] The editor draws the panes in the primary display's shape (≤ 440 DIP tall); dividers drag
  live and save once on release.
- [S-1413] Each pane: split side by side / top and bottom / remove (not on the only pane), and centred
  the icon and name when assigned, the source, then a searchable Start menu app box, a URL box, or a
  path box with Browse (checked by `LaunchTargets`, the reason shown), reuse, Clear. Shrinks with the pane.
- [S-1414] Debug: `--layout-editor-png <out.png> [waitMs] [--empty]` renders the editor off-screen over
  a demo state; nothing is launched.

Not ported: per-pane command-line arguments (allow-list rule: nothing is ever a command line); drag-out
of web, Windows-location and layout items (no file to hand over); Ctrl+Z op log (as PC). Needs live
verification: drops from Explorer / Edge / Chrome / the Start menu, pointer reorder and drag-out on the
real surface, layout launch with Word/Outlook/Code/browser/Explorer windows on multiple monitors and
mixed DPI, the editor's divider drag, pickers and app search.

## Context menus (R4-MENUS)

Every right-click menu in the app — a widget's card, an item inside a widget, empty surface in edit
mode, a view chip — and how a click on a card in edit mode is told from a drag.
Owning files: `Themia.Core/Shell/ContextMenus.cs` (`ContextMenuLayout`, `ShellMenus`, `CardGestures`);
`Widgets/ItemMenu.cs` (the one builder), `WidgetCard.cs` (card menu, click), `DesktopWindow.Menu.cs`
(surface menu), `Gallery/MenuRender.cs`; `Shell/KeyChord.cs`, `Shell/MenuArt.cs` (Core) and `Shell/MenuArtIcons.cs`.
Tests: `ContextMenuTests`, `MenuShortcutAndArtTests`.

- Every one of these menus closes on a press anywhere else on screen, the desktop and other apps
  included ([S-816]).
- **[S-1740]** One menu, Windows 11 File Explorer style: a `CommandBarFlyout`, always expanded, with
  the common commands as a **row of icons** on top and the labelled list below. The row's commands
  and order: Cut, Copy, Paste, Rename, Share (the folder widget's files, [S-6416]), Duplicate (items and views only,
  never a widget, [S-1743]),
  Settings, Delete. Each icon (Explorer's picture, [S-1754]) has its label as tooltip
  (with the shortcut, "Copy (Ctrl+C)", [S-1753]) and as its accessible name. A disabled command stays in its
  place, greyed, so the row never shifts. A second entry claiming the same command, a toggle and a
  submenu stay in the list. `ItemMenu` is the only code that builds a context menu; no view builds a
  `MenuFlyout` for one by hand.
- **[S-1741]** With fewer than two row commands there is no row: a lone icon reads as a stray button,
  so it stays a labelled line in the list, and the menu is the list alone (a `MenuFlyout`: a command
  bar flyout with no row still draws an empty bar above its list).
- **[S-1742]** The list keeps its callers' order; separators collapse (none leading, trailing or
  doubled), including where moving commands to the row emptied a group.
- **[S-1743]** A widget card's menu in edit mode: the row is Cut, Copy, Paste (disabled with nothing to paste)
  and Delete, acting on the selection ([S-1592]); no Settings, since a widget's options are its
  properties menu's beside it ([S-6536]); the list is the view's own
  commands, Bring to front / Send to back, and Edit layout / Done editing. There is no Move:
  dragging and the arrow keys move a widget. There is no
  Duplicate: Copy then Paste already does it ([S-1589]), and one command is enough.
- **[S-1758]** Outside edit mode the card menu is for using the widget, not arranging it: the view's own
  commands (Refresh, Pause, which folder, [S-1746]) and Edit layout, the way to the widget's options
  ([S-6536]); no Settings. No Cut, Copy, Paste, Delete, layering or icon row: with no
  selection shown and nowhere to paste outside edit mode they did nothing the user could see (user report
  2026-10-17: "I right click and cut a widget, nothing happens"). Item menus inside a widget (a file's
  Explorer commands, [S-6416]) are the widget's own and unchanged; empty surface stays click-through ([S-1744]).
  `ShellMenus.ForCard`; tests `S1758_`.
- **[S-1744]** In edit mode a right-click on **empty surface** (not a card, the toolbar or the view
  bar) opens the surface menu, a list with no icon row (Paste is its one row command, and one is
  none, [S-1741]), an action menu that holds no setting ([S-3590]): **Add widget** (opens the picker
  at the click, whose choice then follows the pointer to be placed, [S-1587]), **Paste** and **Select
  all** ([S-1582], off with no widget); then the ways into the bar's menus in the bar's order,
  **Display**, **View** and **Settings** (each opened under its button on that display, [S-3540],
  [S-3534]); and Done editing. Snap is not here: it is the bar's alone ([S-3516]). Outside edit
  mode empty surface is click-through; while placing, the right-click cancels the placement and opens
  nothing.
- **[S-1745]** In edit mode a press on a card that travels 3 DIPs or more (`CardGestures.Travelled`)
  is a drag; less is a click, which selects ([S-1580]). A click no longer follows an open settings
  window to the widget's page (retired 2026-10-19 with the widget pages, [S-6536]): the properties
  menu stands beside the selection instead.
- **[S-1746]** A widget's menus never change how it looks: no view, layout, sort, size, label,
  column, format or "show X" switches on the card menu or an item menu. Those are options and live on
  the widget's properties menu only ([S-3719]), with the card's own header, background and title
  ([S-3707]). The menus keep commands the user
  runs while looking at the widget: refresh, pause, open, which folder, the click mode, search.
- **[S-1747]** Debug: `--menu-png <outDir> [--theme light|dark]` opens the card menu (edit mode, one
  widget; desktop, three widgets; desktop, a widget with view commands, [S-1758]), the surface menu, two sample item menus and the folder widget's file, Open with,
  folder and background menus (a real view over read-only Windows folders) for real, off-screen,
  and writes each open popup to a PNG.
- **[S-1748]** A menu opened by the pointer opens down and to the right of it (top-left corner at the
  pointer), as Explorer's does, and flips only to stay on screen. A menu opened from the keyboard sits
  under its anchor's left edge.
- **[S-1753]** Every shortcut a menu shows (beside a line, in an icon's tooltip "Cut (Ctrl+X)", a notice's "Ctrl+Z")
  is a `KeyChord` written by the one formatter `KeyChordText` (Core), the way Windows 11 and File Explorer write it:
  modifiers Ctrl, Alt, Shift in that order, then the key, joined by "+" ("Ctrl+Shift+C", "Alt+Enter", "Shift+F10",
  "Delete", not "Del"). The modifier words and Enter / Delete come from the language layer as Windows localizes
  them ("Strg+Umschalt+C", "Entf", "Maj+F10"); letters and F1..F12 are written as on the key. No caller types
  shortcut text.
- **[S-1754]** Menu icons are File Explorer's two-tone pictures (grey body, blue part; user report 2026-10-16: our
  one-colour glyphs did not look like Explorer's): a line whose Segoe glyph shows a picture Explorer has (Cut, Copy,
  Paste, Rename, Share, Delete, Settings, open, open with, new window, file location, pin, ZIP, copy as path, link,
  properties, show more options, new, new folder, select all, align to grid, refresh, undo, redo, sort, view;
  `MenuArtRules.ForGlyph`) draws Explorer's picture in the icon row and the list, in the theme the menu draws in.
  Every other line keeps its glyph, as Explorer's own lines without art do. A disabled line keeps the glyph (greyed
  by the menu), and so do high contrast (the glyph follows the user's contrast colours) and a glyph that turns
  round right to left. The view drop-down's New view / Duplicate use the same pictures.
- **[S-1755]** The pictures are Windows', never shipped with Themia: the standard-contrast `theme-light` /
  `theme-dark` SVGs under `%WINDIR%\SystemApps\MicrosoftWindows.Client.FileExp_cw5n1h2txyewy\FileExplorerExtensions\Assets\images`,
  one file per picture (`MenuArtRules.FileName`), each read once per theme and kept. A Windows without
  them (older builds) keeps the glyphs everywhere; logged once, as absent, not as an error.
- **[S-1756]** A picture file is a trust boundary: drawn only when it is at most 64 KB of UTF-8 with an `<svg>`
  root and no script, external reference, entity or foreign object (`MenuArtRules.IsPlausibleSvg`); anything
  else, a read error or a picture that does not decode is logged and the line keeps its glyph.
- **[S-1757]** A picture costs kilobytes: it is rasterized at the 16-DIP icon slot at its screen's scale, rounded up
  and at most 64 px (`MenuArtRules.RasterPixels`), and decoded once per theme and raster size into one source every
  menu line showing it shares. Explorer's SVGs carry only a viewBox, and a source given no raster size drew each
  line at 2858² px, 32 MB held until a collection (user report 2026-10-17: 1 GB after opening some menus).
- **[S-1749]** The Add-widget picker opens centred under what opened it — the toolbar's Add button,
  or the point of a right-click on the surface — and flips above only
  when there is no room below.
- **[S-1750]** *(Retired 2026-10-19 by [S-6536]: no widget has a settings page, so none has a
  Settings command anywhere; the edit bar has no per-widget Settings button either.)*
- **[S-1752]** *(Retired 2026-10-19 with the widget pages: `PageSections` went with the only pages
  whose headings could be left over nothing.)*
- **[S-1751]** In edit mode a widget's own content is inert (`CardGestures.ContentTakesInput`): the
  card turns hit-testing off for its header and view, every widget's alike, so a press, right-click,
  long press or hover lands on the card and selects, drags ([S-1745]) or opens the card menu
  ([S-1743]). No item menu (a mail, a file, a shortcut, a task), hover action, row click, button or
  drag out of a widget fires there; outside edit mode they all work as before. One guard on the card,
  not one per view.

Needs a live pass (pointer and focus on the real desktop): the surface menu opening only on empty
surface and at the pointer, Add widget's picker at the click then placing, the right-click that
cancels a placement opening no menu, keyboard use of the menus (arrows across the icon row and into
the list, Menu key / Shift+F10 on rows), submenus opening beside their line, a double-click still
leaving edit mode, and both menus on a second display with another DPI.

## Icon picker and showcase views (R4-ICONS)

The icon a view is given, chosen from the whole Segoe Fluent Icons set, and a Debug tool
that puts every widget on the real desktop at every size tier. Owning files: Core
`Icons/FluentIcons.cs` (+ the generated `FluentIconData.g.cs`, from `scripts/fluent-icons.py`),
`Icons/IconSearch.cs`, `Widgets/WidgetTiers.cs`, `Model/ShowcaseViews.cs`; App
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
- **[S-1803]** The picker (View ▾ › a view › Icon ›'s submenu, [S-3542]) is built when it opens: the default cell (initials for a view, [S-903]) beside a search
  box, over a virtualised grid of 8 columns of 32-DIP cells (the menu's one width, [S-3734]), 8 rows high, scrolling. Typing filters at
  once; Enter in the box takes the best match, Down moves into the grid; arrows, Home/End and Page
  keys move in the grid and Enter or Space picks. The current icon is selected and scrolled to. No
  match says so in the grid's place, which keeps its size. Debug render:
  `Themia.exe --iconpicker-png <out.png> [--search text] [--theme light|dark] [--initials]`.
- **[S-1810]** Showcase views: one view per widget kind in the registry's order (by id, [S-6902]), named after the widget
  and with its glyph as the view icon, holding seven default instances of it (a converted kind: three,
  at its min, natural and max, [S-2448]), one per size tier
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
- **[S-1815]** A kind with styles ([S-6000]) gets one showcase view per style, in its menu order,
  named "<widget> · <style>" (ids `showcase-<kind>-<style>`, `showcase-<kind>-<style>-<tier>`), every
  widget of it in that style, so each look can be gone through at every tier. A kind without styles
  keeps its one view.

Needs a manual pass: the picker's keyboard path inside the real flyout (Down from the box, Enter,
Escape), and the showcase views on a real desktop (each tier's size at
the display's scale, the second page on a small or scaled display, the refusal while the app runs).

## The Set up button (R5-SETUP)

Owning files: `Themia.Core/Widgets/WidgetSetup.cs`, `Themia.App/Shell/WidgetCard.Setup.cs`,
`Themia.App/Widgets/SetupPrompt.cs`, `DesktopWindow.RequestSetup`, `PageView.OpenSetup`; tests
`WidgetSetupTests` (module host). The section also records the removal of the debug widget
gallery ([S-1863]).

- **[S-1860]** A widget that can show nothing useful until it is configured needs setting up, decided
  for every kind by one pure rule, `WidgetSetup.Needed(config, signed-in features)`, which asks the
  widget's own config (`ISetupRule`, [S-6906]): Countdown with no
  target ([S-6036]); Quote with no quote left; Feed with no feed
  address; Stocks with no symbol; Weather (now and forecast), Air quality and Rain with no place; Video stream with no
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
- **[S-1862]** Such a widget's body is the Status block's set-up row ([S-6714]) and nothing else, where
  its first content row would be (under the header; at the top inset when the header is not above):
  the widget's glyph, one sentence saying what it waits for, and one accent button ("Set up"; its
  tooltip the sentence). The sentence is the config's own (`ISetupRule.SetupSentence`, in the
  module's words: "Choose a place" for every widget about a place; "Add a feed", "Add an account to
  show your mail", "… your tasks", "… your calendar", "… your contacts", "Add an account to plan
  your week", "… your next meeting" for the lists, G2; "Choose a symbol" for Stocks and "Add a quote"
  for a Quote with no quote to show, G3; "Add a shortcut" for Shortcuts, "Choose a folder of pictures" or "Choose a picture" for Pictures and "Add a stream" for a Video stream, G6), else "Add an account to show this" where an
  account is all it lacks, else "Set up what this widget needs". On a row too narrow for both (XS, S) the sentence
  goes and the glyph and the button stay, at every size. The button takes
  no focus, so it works outside edit mode on the `WS_EX_NOACTIVATE` surface. A click goes where what
  the widget waits for is set (`WidgetSetup.Goes`): where an **account** is all it lacks (it would be
  set up with every feature the installed providers offer: Mail, Calendar, Contacts, Next meeting, the
  week planner, Tasks from an account), **⚙ › Accounts** on that display, edit mode entered ([S-3558]),
  since an account is added there and never from a widget's menu ([S-3745]); otherwise **edit mode**
  is entered, the widget is selected alone and its **page** ([S-2340]) opens at the row its module
  names for it (`WidgetMenuLines.SetsUp`, one per widget), as a deep link opens a row
  (`PageView.OpenSetup`): its card opened where it opens (a choice, a lookup), the row scrolled into
  view and lit. The lines named: the location's search for Weather now, the forecast, Air
  quality and Rain; the feeds for Feed; the symbol's search for Stocks; the day, asked for, for
  Countdown; the address, asked for, for Video stream; the quotes for Quote; Add for Shortcuts; the
  folder or the picture for Pictures. A module that names none leaves the menu as it stands. The card
  owns the button, not the view: the view underneath is parked, not running ([S-1881]), invisible and
  not hit-testable, and the button goes the moment the config (from the menu, or a change the view
  saves itself such as a shortcut dropped onto it) or a newly signed-in account makes the widget
  useful. The views carry no "add one in settings" sentences of their own.

- **[S-1863]** There is no widget gallery: no `--gallery` window, no `--gallery-png` render, no
  toolbar entry, and no `#if DEBUG` sample data inside widget views or services. A widget's layout at
  the seven size tiers (`WidgetTiers`) is checked on the showcase views ([S-1810]) on the live
  desktop, or off-screen with `--widget-png` (one card at every tier, default config, [S-1917]); the other debug render flags (`--editbar-png`, `--picker-png`, `--viewbar-png`,
  `--notices-png`, `--menu-png`, `--iconpicker-png`, `--motion-png`, `--layout-editor-png`) stay and
  draw widgets with their default config. The tags that described the gallery's fixtures are retired.

Needs a manual pass: the button on the live desktop outside edit mode (an option widget: edit mode
on, the widget selected, its menu open at the line beside it, a search's box taking typing; an
account widget: ⚙ › Accounts on that display, at Add account with no account), and the button going
away when an account is added.

## One sample day (SAMPLE-DAY)

The Debug previews ([S-1917]) are the Store's screenshots: whole desktops of widgets side by side.
Every kind's main data state (`sample`, or its first data state: `messages`, `tasks`, `list`, ...)
tells one story, so any set of them agrees with itself. The facts live once, in the SDK
(`Themia.Sdk/Samples/`, Debug only), and the previews read them; tests `SampleDayTests`.

- **[S-2050]** One moment: `SampleDay.Now` is the day the render runs on at 10:42 local (the clock's
  sample). A preview that shows a time relative to now, today, a countdown or an age computes it
  from there, never from the real clock, so renders at 9 am and at 3 pm draw the same picture apart
  from the date. A view whose sample needs it has a clock seam (`_clock`, the real clock by default)
  that only its preview sets; outside previews the widgets read time as before.
- **[S-2051]** One place: `SamplePlace`, Zürich (spelled so on every card, 47.37 N 8.54 E). Its
  sunrise and sunset come from the same astronomy the Sun and Moon widgets draw with, so the forecast's
  and the Sun widget's agree; the Sun and Moon samples are drawn at the sample moment.
- **[S-2052]** One weather: `SampleWeather`, an overcast autumn morning at 9.4 °C, today 6.2 to
  12.8 °C with light rain from a quarter past eleven (the rain nowcast's shower), UV 1.1 now. Weather
  now, the forecast, air quality and rain in the next hour read these numbers.
- **[S-2053]** One cast: `SampleCast`, invented people on `.example` addresses. Robin Keller (product
  manager at Lumen Labs) owns the desktop; colleagues Ana Silva, Dana Ruiz, Priya Natarajan, Alex
  Morgan and Sam Okafor, the client Bea Lindqvist and Liam O'Brien of Northwind, and friends Lena
  Fischer, Mia Chen and Jonas Weber write the mail, organise the meetings, fill the address book and
  send the notifications.
- **[S-2054]** One schedule: `SampleSchedule` (Work on a Microsoft account, Home on a CalDAV one: only what Themia offers, [S-3562]) over
  the `SampleCalendarData` seam the calendar, the week planner and the next meeting all read. Today:
  Standup 9:00, "Design review: new onboarding" 11:00 to 11:45 in Room 4.12 · Microsoft Teams,
  organised by Ana Silva (the next meeting, "in 18 min"), Lunch with Lena 12:30, 1:1 with Sam 14:00,
  Budget check-in with Northwind 16:00. Every other day within four weeks follows its weekday (two or
  three events on a workday, one or two at the weekend); a weekend in the mountains, an offsite in
  Lisbon, a conference and holidays run over several days, an all-hands falls on each month's first
  Tuesday, a night train crosses midnight; Mia's birthday is today.
- **[S-2055]** One errands list: `SampleTasks`, ten tasks; the tasks widget shows them and the week
  planner the ones due by today, overdue first.
- **[S-2056]** Lists fill a large card: mail ten messages over the last day (two accounts where the
  state shows accounts), notifications, clipboard, recent files, contacts, feeds and tasks eight to
  twelve items. Numbers are believable: the rates and the stock are seeded random walks, the main
  screen-time sample is under its limits (`over` is the state over them), battery devices sit at
  ordinary levels with one low. No preview reads a picture, a folder or a name from this PC: the
  music art and the gallery's pictures are drawn in code (`SamplePictures`: each a flat, poster-like
  landscape matching its file name, the same bytes for the same picture and size on every run; the
  album cover the northern lights over a winter lake), the folder, recent files and recycle bin
  list invented documents with the shell's stock icons by extension.

## Opening a widget's app (R7-OPEN, retired)

Removed by the user's decision on 2026-10-05, with its table, launcher, hover affordance, strings
and tests: a single click had to wait out the double-click time (a double-click entered edit mode
then), and one global table decided every kind. Brought back differently as CARD-OPEN below
([S-2000]..[S-2009]). These tags are not reused.

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

## Opening what a widget is about (CARD-OPEN)

Issue 75 ("click a widget to open its app"), as the user decided on 2026-11-07. Each widget's own
target is in its module's `SPEC.md` ([S-2010]..[S-2045]: the meters, details and settings-page
widgets, Clock and Alarm, Music, the file widgets, weather, Stocks and the account widgets); the host
has no table of kinds. Widgets with nothing sensible to open (notes, habits, countdowns, toggles and
power buttons, whose whole body is their button) open nothing.

- **[S-2000]** Outside edit mode a **plain left click** on a widget (no Ctrl, Shift or Alt), one that
  none of the view's own buttons, rows, links, sliders or boxes took, **opens what the widget is
  about at once** (`CardGestures.Opens`): the press must reach the card unhandled and so must its
  tap (`WidgetCard.OnTapped`), so a control that takes either keeps its click. Nothing waits: a
  double-click is nothing of the card's ([S-65]), and its second click opens nothing again. A
  widget that shows its Set up button ([S-1862]) and a debug render's preview ([S-1251]) open
  nothing; in edit mode a click selects ([S-1745]). A click that opened nothing (refused, missing,
  the shell said no) is logged; the desktop stays as it was.
- **[S-2001]** The widget decides, through its view: a view implements the SDK's `IWidgetOpens`
  (`CardOpen? Opens`, read on the click so it follows what the view shows now: the playing app, the
  folder it is in). Null opens nothing (not set up, no data yet, nothing fits); a view with nothing
  to open at all does not implement it. The shared views ask their module's part: `MeterPart.Opens`
  and `DetailPart.Opens` (default nothing). What is opened is a `CardOpen`, built only through its
  factories, and opened only through `IWidgetHost.Launcher.OpenCard`.
- **[S-2002]** A `CardOpen` is one of three kinds, each with its own gate, checked when it is built
  and again by the launcher on the literal value: a **target** `LaunchTargets` accepts (`Target`,
  `Web` for https pages only (an http page opens nothing), `Place` for a folder, file or `shell:`
  location, `SystemProgram` for a bare `.exe` name in the system folder: Task Manager), a **Settings
  page** (`Settings("sound")` → `ms-settings:sound`, the name lower-case letters, digits and dashes
  only), or an **app** (`App`). Windows' Clock app is its Apps-folder item
  (`shell:AppsFolder\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App`, `CardOpen.ClockApp`).
- **[S-2003]** An app is the AppUserModelID another process reported (the media session's
  `SourceAppUserModelId`, `NowPlaying.AppId`): untrusted, so a packaged app's id becomes its
  Apps-folder target and a desktop app's own id (`Spotify.exe`, `Chrome`) passes only as ASCII
  letters, digits and `. - _` (no leading dot or dash, no `..`, at most 128). The launcher brings a
  running desktop app's visible window to the front (restored when minimised) and otherwise opens
  its Apps-folder item.
- **[S-2004]** A widget's own clicks stay its own: a kit row (`KitList`, plain and checked), a
  detail row (`DetailView`), a calendar event, a header's button (`HeaderBlock`), the Quote widget's
  next-quote click and every WinUI control (buttons, sliders, toggles, links, which handle their
  presses) mark the click handled, so the card never opens on top of them. Empty space between
  them, the header's icon and title, and a widget's readouts are the card's.
- **[S-2005]** Account widgets open the account's own service on the web
  (`AccountPages.Home(feature, chosen, accounts)`, SDK, over `IWidgetHost.AccountsWith`): of the accounts the widget reads (all with
  the feature when it names none), the first whose provider has a page for the feature. Microsoft:
  mail `https://outlook.office.com/mail/`, calendar `https://outlook.office.com/calendar/`, contacts
  `https://outlook.office.com/people/`, tasks `https://to-do.office.com/tasks/`. Google: mail
  `https://mail.google.com/`, calendar `https://calendar.google.com/`, contacts
  `https://contacts.google.com/`, tasks `https://tasks.google.com/`. IMAP, CalDAV and CardDAV have no
  page: nothing opens.
- **[S-2006]** Weather widgets open MSN Weather's forecast for the widget's own place
  (`WeatherPages.Forecast(config, locale)`, SDK, beside `WeatherConfig`), in the UI language's
  market (`de-de`, …, `en-us` for English and any other), in its unit; the place travels as MSN's
  `loc` parameter (base64 JSON of the name and the coordinates to four places), because a name alone
  lands on whichever town MSN guesses. No place, no page.

Needs a manual pass (the live desktop): a click on each widget with a target ([S-2010]..[S-2045])
outside edit mode opens it once and at once; a click on a row, a button, a slider or a link does only what it did; a
double-click does not enter edit mode; Ctrl+Alt+W and launching Themia again still do; in edit mode
a click still selects.

## Removed widgets (R7-REMOVE)

Taken out of the app by the user's decision, with everything that existed only for them (views,
settings pages, services, Core rules, glyphs, strings in all nine languages, tests). Their tags are
retired with them and are not reused.

- **[S-1902]** Removed: Themia (the mark), Help (getting started), Claude usage, GitHub (repository,
  issues, notifications, reviews), the view switcher (the view **bar** stays, [S-906]..[S-916]),
  Group, Stack and Tabs, the mood tracker, the colour swatch, web search, OneDrive, Apps, the
  calculator and the process list. (The numbers `WidgetKind` kept for the kinds that remained went
  with the enum: a widget is its id now, [S-6900].) Retired
  with them: [S-100]..[S-105], [S-639], [S-120]..[S-126], [S-131], [S-410]..[S-419], [S-480]..[S-492],
  [S-510]..[S-512], [S-514], [S-537], [S-606]..[S-609], [S-654], [S-790]..[S-796], [S-810],
  [S-861]..[S-871], [S-875], [S-876], [S-951]..[S-956], [S-1144], [S-1145], [S-1151]..[S-1155],
  [S-1290]..[S-1294], [S-1449]..[S-1455], [S-1562], [S-1565], [S-1566], [S-1570], [S-1573], and the
  sections on the developer widgets, the GitHub widgets, the calculator keypad and colour swatch
  modes (PK), Apps/Search/Help/Themia (PI-A) and the view switcher (PI-B).
- **[S-6275]** Removed (user decision 2026-10-16): Disk space, the list of every drive's space. Drive
  ([S-4670]) covers it, one drive per card; the disk meter stays throughput. Gone with it: its kind,
  config, part, settings rows, catalogue and add-menu entries (the disk meter is now a plain entry of
  System), the hub's disk-space source, `DiskSpaceRules`, its strings in every language and its tests;
  retired [S-2420]..[S-2422].
- **[S-6276]** Removed (user decision 2026-10-25): Year progress, the share of the year, month,
  week or day gone by. Gone with it: its module (`src/Widgets/Themia.Widget.YearProgress`: kind,
  config, styles, view, previews, `TimeProgress` rules, its strings in every language and its tests),
  its line in the Time folder of the add menu and its widget in the every-kind state fixture. A
  stored one is a kind this build does not have ([S-1900]). Retired with it: [S-4800]..[S-4805],
  [S-6040]..[S-6042].
- **[S-1903]** The model is flat: a widget has no parent, and nothing holds other widgets. There is no
  "Move into", no dropping a widget into another, no picker target but the desktop, and no side
  header (`HeaderPlacement` is Top, Bottom, Left, Right or Hidden). A selection is copied, moved, layered and
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
  file over 64 MB, not sealed, not opening with this key and context, not JSON, of a newer schema or
  another owner is logged, moved aside as `.corrupt` (one copy kept) and the owner resyncs. A sound
  shelf of an **older** schema is a stale cache, not damage: logged at Info (no notice), left for the
  resync's first save to replace, and the owner starts empty and resyncs, change cursors included.
  A kind's schema is bumped when what earlier builds stored cannot be trusted: the calendar shelf is
  v2 because v1 could hold Graph delta notices stored as untitled events and all-day events stretched
  over two days ([S-2632]); only the calendar shelves start over. The task shelf is v2 because v1
  held no completion dates ([S-612]) and its Graph change cursors never asked for them. A file
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
  task; rename a task list, [S-614]) show at once and wait in the owner's queue, persisted in its shelf (`PendingWrite`), until
  the provider has them. Strictly in order, head only, one at a time. Offline, unreachable, timed
  out, throttled, the server's trouble or a sign-in to mend: kept and retried after 30 s, 1, 2, 4 …
  30 min (or at once when the network returns); refused (gone, forbidden, malformed): taken back and
  the header says why. At most 500 queued; the next is refused in words. A queue restored at start
  is shown again and resent.
- **[S-2611]** A new change folds into what is still queued for the same thing: a second tick or read
  flag replaces the first, a rename replaces a rename (a list's rename a list's rename), a delete swallows the task's queued tick and
  rename, and a task created and deleted before either reached the provider leaves nothing. The head
  is never folded while it is on the wire. A confirmed create re-addresses later changes to the
  provider's id.
- **[S-2612]** A stored copy the last sync could not improve on says when it is from: "Work: No
  network · updated Mon 10:42" (contacts; the calendar by the kit's rule, [S-2664]), and the kit's
  one stale rule ([S-7042], [S-7064]) for Rss, Mail, Tasks ([S-615]) and the online widgets (weather now, the forecast,
  Stocks, exchange rates, air quality, rain): the header's problem glyph with one tooltip line per
  source and problem ("Work: No network · updated 10:42"), the age on the card menu's Refresh line
  ("Refresh · updated 4 min ago"), and no age drawn in the header (it was, in the caution colour
  when stale; decided, Q3). Mail's Refresh line says its age the same way ([S-559]). Times through `LocaleFormat`: a clock time today, weekday and time this week, else a
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
  Calendar lists are refreshed every 6 hours when the menu asks, birthdays daily. A
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
- **[S-2651]** A task shelf on load: lists need an id (once, at most 50) and keep whether they are
  To Do's own ([S-614]); held lists need an id and appear once (never Today); tasks need an id,
  appear once per list, stop at 400, and a completion date ([S-612]) is kept only on a finished task
  and only when it reads as a date; the queue keeps task and list changes with a list, and a rename,
  list rename or add with a title.
- **[S-2652]** The lists a sync reads: each list a widget shows (an empty id is the default list),
  every list once Today is shown (Today is built from all of them, [S-596]); finished tasks only for
  a list a widget shows them for, so finished ones cannot push open ones past the bound.
- **[S-2653]** A task change waits in the queue ([S-2610]) with its optimistic edit on screen; a
  restored queue is shown again on start. An added task keeps its temporary row (pinned at the
  bottom, [S-599]) until the provider confirms it.
- **Incremental sync.** Owning files: `Themia.Core/Sync/{DeltaSync,DeltaMerge}.cs`,
  `Core/Office/DavDelta.cs`, `Core/Mail/ImapDelta.cs`, `Services/Office/AccountOfficeSource.Delta.cs`,
  `AccountService.GetDeltaAsync`, `MailService.FetchGraphAsync`, `ImapMailClient.SyncAsync`,
  `AccountTaskRemote.SyncListAsync`. Tests: `IncrementalSyncTests` (`S268x_`), `DeltaFallbackTests` (`S2690_`).

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
  A refusal is never the account's failure. Any other refusal of the change request itself is
  [S-2690]'s.
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
  the window and leaves when it moved out; a removed or cancelled one leaves; a bare change notice is
  read again first ([S-3610]); the window stays and
  both refresh stamps move on; at most 5000 events, and a window at the bound keeps no cursor.
- **[S-2685]** Contacts: changes upserted by id, removals out, name order, at most 1000; a book at
  the bound keeps no cursor, so a book larger than the widget can show is read in full each time.
  Graph lists the first 1000 by name as before and, when that is the whole book, starts the default
  folder's delta round, whose answer is what is held.
- **[S-2686]** Tasks: a changed task keeps its place, a new one joins at the end, a removed, deleted
  or hidden one leaves, and one finished elsewhere leaves a list held without finished tasks (and
  comes back reopened); at most 400, a list at the bound keeps no cursor. Graph reads every list as a
  delta round (one path: from the start, or from its link) unless the list refused it ([S-2690]); Google reads the list in full with an
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
- **[S-2690]** A change request (Graph delta, Google sync token or `updatedMin`) refused as a request
  — 400 without naming the token, 405 or 501; Graph answers a To Do list's `tasks/delta` with a bare
  400 "Invalid request" for some lists — is not a skipped list, calendar, book or folder: it is read
  the plain way at once (as before incremental sync: `tasks?$top=100`, `calendarView`, the contacts
  listing, the newest N messages), with no cursor, and remembered for the session per account and
  thing (`DeltaFallback`), so the next cycles go straight to the plain read; logged once at Info. A
  failure of the plain read is an error as any other; 401, 403, 404, 429 and 5xx on a delta are not
  refusals. DAV keeps its own fallbacks ([S-2688]); IMAP has no HTTP refusal.
- **[S-2660]** Feeds, quotes and weather keep their documents ([S-1300], [S-729]: same validation,
  same ages) in the sealed `web` shelves of the store, no longer as plain `network_cache.json` /
  `weather_cache.json` files.
- **[S-2661]** Rss: `FeedSync` (`AppServices.Feeds`, a widget reads it through `IWidgetHost.Feeds`) keeps every feed of every Rss widget fresh on
  the shortest refresh any widget asks for, wakes with the network and goes now on Refresh; the
  widget draws what is held, hears the updates and asks nothing on attach (a feed address never
  fetched yet is the exception). The kit header's ring shows only while a fetch for one of its feeds
  is out ([S-7064]).
- **[S-2662]** A restored answer (feed, quote, weather) is fresh for its real age: a launch within a
  widget's refresh interval of the last fetch asks nothing; its Refresh line says its saved age
  until a fetch replaces it ([S-7042]). The caches are restored before the web syncs start,
  and the syncs start only in the app (after the request guard reads the user's choice), never in a
  debug render.
- **[S-2663]** WeatherNow, WeatherForecast and Stocks: `WebSync` (`AppServices.WebSync`) keeps every
  reading, forecast and quote of every such widget in every view fresh (`SyncInterests.Weather`,
  `.Quotes`; a widget with no place or no usable symbol asks nothing), on the shortest refresh any
  widget on the same answer asks for (weather 5–240 min, quotes 1–120 min), backing off on the
  shared `Backoff`, waking with the network and going now on Refresh. Feeds and these share one
  scheduler (`AnswerSync`). The widget asks the sync for the answer of its own config
  (`WeatherInterests.For`, or the stock config's `Interest()`: the same rule), draws what is held, hears `WebSync.Updated`, and its tick only
  redraws (day labels); it never fetches. Offline or failing, the last answer stays, with the kit
  header's problem glyph saying the failure and when the answer is from ([S-2612], [S-7042]). The quote and weather caches
  are unchanged.
- **[S-2664]** Calendar: a stored copy the last sync could not improve on says when it is from by the
  kit's one stale rule ([S-7042], [S-3651]): the header's problem glyph (`KitHeader`), one line per
  account and problem in the kit's words ("Work: No network · updated 10:42", `KitFreshness`, folded
  and stamped with the oldest stored copy among them; nothing stored adds no stamp), and the card
  menu's Refresh line with the age. Contacts says it as before (`SyncFreshness.Stamp`).
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

## Shared widget settings: region and updates (SETTINGS-AUDIT)

What every widget shares instead of carrying its own copy, and the audit of every widget page that
moved it there. Core: `RegionFormat` (`Localization/RegionFormat.cs`), `UpdateCadence`
(`Sync/UpdateCadence.cs`), `RegionPreferences` / `RefreshPreferences` (`Model/AppPreferences.cs`).
Core `Settings/SettingsMenu.Language.cs` (⚙ › Language & region: the formats, [S-3536]),
`Settings/SettingsMenu.Online.cs` (the settings menu's Online updates, [S-3530]), `AppHost.Region.cs`,
`Interop/SystemRegion.cs`.
Strings in `Locales/language-region` and `Locales/widget-prefs`. Tests `WidgetPreferencesTests` (`S37xx_`).

- **[S-3770]** The regional formats (12- or 24-hour clock, first day of the week, temperature scale)
  are `AppPreferences.Region`, one set for every widget: no widget stores `use24Hour`, `unit` or
  `weekStart` any more, and a stored one is ignored like any unknown JSON. `RegionFormat.Current` is
  what is in force; a change raises `Strings.Changed`, so every widget redraws its text as it does for
  a language change (the weather widgets ask again under the new scale).
- **[S-3771]** Each format offers "Windows setting (…)" first, naming the value Windows has, then the
  fixed choices: 12-hour / 24-hour; Monday / Sunday / Saturday; Celsius (°C) / Fahrenheit (°F).
- **[S-3772]** "Windows setting" reads the user's regional format through `GetLocaleInfoEx`
  (`LOCALE_SSHORTTIME`: an `H` is the 24-hour clock; `LOCALE_IFIRSTDAYOFWEEK`: 6 is Sunday, 5 Saturday, any
  other day Monday; `LOCALE_IMEASURE`: 1 is Fahrenheit), never `CultureInfo` (`InvariantGlobalization`). It
  is read again on every state change. An unreadable value falls back to the app language's habit
  (`RegionFormat.Language`). An unknown stored value follows Windows again, loudly; a null block is
  the defaults.
- **[S-3773]** `HourCycle.Locale` in `LocaleFormat.Time` is the clock setting once the app has read it,
  so every time a widget prints (clock, agenda, mail, countdown, sun, freshness lines) follows one
  switch. Before that (and in the tests) it is the language's habit.
- **[S-3780]** How often online sources are asked is `AppPreferences.Refresh`, one cadence per kind of
  source, set in the settings menu's Online updates ([S-3530]): Weather (now and forecast, rain, air quality: [S-5101]), Stocks, News feeds, and
  Mail, calendars and tasks. No widget has a refresh setting for an online source. Each row is a
  `CadenceRow` (the host's four are `HostCadences`; a provider module may add its own, [S-6910]) and
  `Refresh` is a map from the row's `CadenceId` to minutes: `"refresh": { "weather": 30, "markets": 5,
  "feeds": 30, "accounts": 2 }` (schema changed in place, [S-1050]: the four `…Minutes` properties of
  before are entries no row has, dropped on load, so those cadences start at their defaults again).
- **[S-3781]** The choices, in minutes: weather 15, 30, 60, 120 (default 30; open-meteo publishes every
  15); stocks 1, 5, 15, 30, 60 (default 5); feeds 15, 30, 60, 120 (default 30); accounts 1, 2, 5, 15,
  30 (default 2). A stored value off the list snaps to the nearest choice, a tie to the slower one,
  and says so. An entry that is no row's id is dropped and said; a row without an entry follows its
  default (not a repair). A new state writes the host's four at their defaults.
- **[S-3782]** The accounts cadence probes every mail folder, asks the calendars' near window and
  every task list (`SyncInterests.AccountInterval`); contacts stay at 15 minutes and the calendar's
  whole window at 6 hours. The sync reads the cadences from the state itself
  (`SyncInterests.From`), so a change is heard on the same state change.
- **[S-3790]** Stocks has no market choice. A search pick sets the symbol, its service (Yahoo or
  CoinGecko) and its name together (`StocksConfig.Choose`); a symbol typed in full is a listed one
  (`ChooseTyped`: Yahoo quotes coins too, as `BTC-USD`) and drops the old name. The name row is gone:
  the pick's name is the header title, and the card's own title overrides it.
- **[S-3791]** A field that only renamed the header is gone where the card's own title does the same:
  the feed's `title`, the ping host's `hostLabel` (meter and statistics).
- **[S-3792]** Every gallery re-reads its folder or Bing's photo hourly (`GallerySourceKey.RescanInterval`);
  "Refresh images" reads now. No refresh setting.
- **[S-3793]** Local readings have no refresh setting: meters and system details read on the system
  hub's one cadence ([S-1120], [S-2401], [S-2503]); a stored `intervalSeconds` is ignored. Bluetooth
  enumerates every 30 seconds ([S-3794]). The camera's snapshot interval stays the stream's own
  ([S-470]): it is how a camera is read, not how often a service is asked.
- [S-3794]: moved with the Bluetooth widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Bluetooth/`).
- **[S-3795]** Long widget pages fold related rows into `FluentLayout.Expander` groups: Stocks' period and
  colours (timeframe, high and low, colours; whether a chart is drawn is the style, [S-6165]); the clock's zone (offset);
  Shortcuts' view (tile and row size, icon only, stretch) and the Windows locations; Folder's sort
  (reverse, folders first) and filter (hidden items, extensions). Descriptions are one short line,
  or none where the title says it. Repeated actions are not repeated: Folder's "Open in File
  Explorer" is on the widget's menu only.
- **[S-3796]** Removed outright, with the behaviour that replaced them: the clock's 24-hour switch, the
  weather unit, the calendars' week start ([S-3770]); every online refresh row ([S-3780]); Stocks'
  market and name rows and the fill-under-the-line switch (always filled); the feed title and the
  ping name ([S-3791]); the gallery, meter, detail and Bluetooth refresh rows ([S-3792]–[S-3794]);
  Bluetooth's device count; Notes' word wrap (always wraps); the contacts' starting search
  ([S-584]); the mail page's sort row (the widget's own sort button stays).
- **[S-3797]** *(Retired by [S-3536]: the Language & region page is the settings menu's
  Language & region ›, which holds every localisation choice in one place as the page did: the
  language, then the regional formats. There are still no other units or formats: no widget has its
  own date, number, wind or distance format.)*

## Next meeting (NEXT-MEETING)

The widget is the module `nextMeeting` (`src/Widgets/Themia.Widget.NextMeeting/`, its `SPEC.md`: the
config, the view, the hover link, the page). What it shares stays here: the SDK's rules it is drawn
by. Owning files: `Themia.Sdk/Widgets/NextMeetingRules.cs`, `Themia.Sdk/Office/MeetingLinks.cs`;
tests `DeskInfoWidgetsTests` (Core).

- **[S-4821]** Which event (`NextMeetingRules.Pick`): the timed events not declined (Graph's
  `responseStatus`, Google's attendee marked `self`; CalDAV cannot say) that have not ended. One
  under way shows until it ends, unless the next starts within 5 minutes; of several under way the
  one that began last; of several starting together the one with a join link, then the shorter,
  then by title. Nothing: "Nothing coming up". A minute tick re-picks from what is in hand.
- **[S-4822]** The join link (`MeetingLinks`) is read when the event is: the provider's own field
  first (Graph `onlineMeeting.joinUrl`, then `onlineMeetingUrl`; Google `hangoutLink`, then a
  `conferenceData` video entry point; iCalendar `X-MICROSOFT-SKYPETEAMSMEETINGURL`,
  `X-GOOGLE-CONFERENCE`), else the first link to a meeting service in the location, then the body
  (Graph `bodyPreview`, Google `description`, iCalendar `DESCRIPTION`): a Teams `meetup-join` or
  `teams.live.com/meet`, a Zoom `/j/` `/w/` `/my/` (zoom.us, zoomgov.com), a Meet room, a Webex
  meeting, or `msteams:` / `zoommtg:`. Any other link in free text is not a join link. Outlook Safe
  Links are unwrapped, trailing punctuation and `&amp;` undone. Only https, http, `msteams:` and
  `zoommtg:` with no user info, whitespace or control character, at most 2048 characters, pass
  `IsLaunchable`; the store drops anything else, and `ShellLauncher.OpenMeetingLink` re-applies
  the gate before the default handler opens it.
- **[S-4823]** When, in words: "Now · ends 10:30" under way; "in 12 min." within the hour; else
  "Today, 14:00", "Tomorrow, 09:00", "Mon, Oct 19, 09:00". Times on the regional clock ([S-3773]).
- **[S-4824]** The ladder (`NextMeetingLayout.Plan`) on the family's tokens, in the lines' box (the
  view's area less the card's insets, and less the end column where it stands beside them, [S-5710]):
  the title first and always (Title, one line ending in an ellipsis), then the time range, the location
  and the organiser, each a Body secondary line behind its 16 glyph, 4 apart, over the end column's row
  where that stands under them; the first line that does not fit ends the ladder. A larger tile shows
  more of the meeting's own lines, never a larger title (concise first, [S-666]). The when line is the
  countdown at the end column's top ([S-5710]), never the accent. Nothing coming up, loading
  and a problem are the state row at the content top ([S-7039]). A click elsewhere on the event
  opens it in its calendar ([S-1268]).

## Recent files (RECENT-FILES)

The widget is a module of its own (`src/Widgets/Themia.Widget.RecentFiles`, its SPEC.md). Here: the
host's one list it reads through `IWidgetHost.RecentFiles` (`Themia.App/Services/Files/RecentItems.cs`,
`HostFileServices.cs`; `RecentFile` and `RecentFileRules` in the SDK).

- [S-4840], [S-4842], [S-4844], [S-4845]: moved with the Recent files widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.RecentFiles/`).
- **[S-4841]** The source is the Recent Items folder (`shell:Recent`, `FOLDERID_Recent`), the
  shortcuts Windows writes on every open and Explorer's Home "Recent" is built from. The newest
  120 shortcuts by last write are resolved (`IShellLinkW` through `ShellFileOps.ResolveShortcut`,
  off the UI thread); a target that is not a fully qualified path, is a folder or is gone is left
  out; a file reached by two shortcuts is listed once, newest first, at most 40. Each row: the
  shell's icon (the shared `FileIcons`, a file glyph until it comes), the name, the folder's name
  and when ("2h"). A debug render never reads the user's list.
- **[S-4843]** Live: one folder watch on Recent Items for every widget, started with the first and
  disposed with the last; a read is single-flight and runs once more for a change during it. A
  failed read keeps the last list; none at all says the list could not be read. A minute tick keeps
  "2h" honest.

## Recycle Bin (RECYCLE-BIN)

The widget is a module of its own (`src/Widgets/Themia.Widget.RecycleBin`, its SPEC.md). Here: the
host's one answer and Empty it reads through `IWidgetHost.RecycleBin` (`Themia.App/Services/RecycleBinStatus.cs`,
`HostRecycleBin.cs`; `RecycleBinState` in the SDK), which the Shortcuts badge shares.

- [S-4860], [S-4861], [S-4862]: moved with the Recycle Bin widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.RecycleBin/`).
- **[S-4863]** Empty calls `SHEmptyRecycleBinW` without `SHERB_NOCONFIRMATION`, on its own STA
  thread: Windows asks first, then shows its progress and sound; the widget never empties on its own
  say. It is offered only when the bin has items and not twice at once; the answer is asked again
  when Windows returns, whatever the user chose. A click elsewhere on the tile opens the bin
  (`shell:RecycleBinFolder`). Debug: `--state sample`, `empty`.
- [S-4864]: moved with the Shortcuts widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Shortcuts/`).

Needs a live pass: Empty with Windows' dialog (Yes and No), deleting a file from Explorer (the count
following within 15 s).

## Stopwatch, alarm and drives (KARPO-2)

Four widgets after Karpo (`karpo-app`, `docs/widgets.md` §84 and §92 the stopwatch, §106 the alarm,
§91 the removable devices, §75, §99 and §122 the drive). The stopwatch, the alarm and the drive
widgets are modules of their own (`src/Widgets/Themia.Widget.Stopwatch`, `.Alarm`, `.Drive`,
`.RemovableDrives`, each its SPEC.md). What they share stays here: the SDK's `AlarmRules.cs` (with
`IAlarm`, what an alarm config holds for the service) and `DriveRules.cs`, Core's `StateAlarmSlots.cs`,
tests `AlarmTests` (the module host) and `DriveRulesTests`; services `Services/AlarmService.cs` (+
`AppHost.Alarms.cs`, behind `IWidgetHost.Alarms`), `Services/Drives/DriveCatalog.cs` and
`DriveService.cs`.

### Stopwatch

Moved with the widget into its module: `src/Widgets/Themia.Widget.Stopwatch/SPEC.md` ([S-4600]..[S-4605]).

### Alarm

- [S-4620], [S-4628], [S-4629], [S-4630], [S-4631]: moved with the Alarm widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Alarm/`).
- **[S-4621]** Times parse strictly (hours 0–23, two-digit minutes); unknown day words are dropped and
  days are written once each, Monday first; a name is one line of at most 40 characters; a
  hand-edited file is repaired loudly.
- **[S-4622]** The next ring is the alarm's time on the first day after now it may ring on, reckoned on
  the wall clock of the zone in force and kept as a UTC instant. A time summer time skips rings at the
  first minute after the gap; a time winter time says twice rings once, at the first.
- **[S-4623]** The **alarm service** (app lifetime, started by the host, never by a render) keeps the
  alarm of every alarm widget of every view (`AlarmSlot.Of`: one per widget whose config is an
  `IAlarm`, keyed by the widget's id; `AlarmBook`, synced on every state change and every switch on a widget), so an alarm rings
  whether or not its widget is shown (another view, game mode). Deleting the widget deletes its
  alarm (a ring stops); an undo brings both back, set from then. One waitable timer is set to
  the next thing due (a ring, a snooze ending, a ring that has run five minutes) as an absolute UTC due
  time, which Windows keeps through a clock change and fires on waking when it passed during sleep. It is
  set again after every tick, on `WM_TIMECHANGE` (zone cache cleared) and on resume
  (`WM_POWERBROADCAST`). Nothing polls; with no alarm on, nothing is set. Set at a time already past it
  is tomorrow's: switching on or starting never rings for a moment before. Late by up to five minutes
  (asleep through it, the clock set forward) it rings once when seen; later it is missed. Each ring or
  miss is remembered, so the clock set back never rings it twice.
- **[S-4624]** The widget says, under the time in the language's clock, its name and: Off; Ringing (accent); "Snoozed
  until 7:10"; for an alarm that repeats, its days ("Every day", "Weekdays", "Weekends", else "Mon, Wed"
  in the language's week); for one that rings once, Today, Tomorrow or the day.
- **[S-4625]** A ring sounds until Snooze (the widget's snooze), Dismiss, or five minutes pass. An alarm
  that does not repeat turns itself off when its ring is over (dismissed, run out or missed), and that is
  written to the file.
- **[S-4626]** A ring plays the widget's ringtone looped (`CuePlayer.PlayLooping`: `PlaySound` with
  `SND_LOOP`; Off rings silently) and shows a Windows notification with the app's identity
  (`AppNotificationManager`: packaged, the package's; unpackaged, registered as "Themia" with its icon):
  the alarm scenario, the name and the time, Snooze and Dismiss, its own audio muted. A button's
  arguments cross a trust boundary: only those two actions, and only for an alarm the service holds. The
  notice goes when the ring stops, however it stops.
- **[S-4627]** **Alarms ring only while Themia runs**, and its menu says so (a note). One that came due
  while Themia was closed does not ring at the next start (it is set from then).

### Drives

- **[S-4650]** One walk of the drives (`DriveCatalog`, after Karpo's `DriveWatch`): every letter of
  `GetLogicalDrives`, its kind (`GetDriveTypeW`: fixed, removable, CD, network, RAM disk), label and
  file system (`GetVolumeInformationW`) and the space free to this user (`GetDiskFreeSpaceExW`), with the
  critical-error box off so an empty reader never asks for a disk. A reader with nothing in it is
  NoMedia. A fixed disk on USB, FireWire or SD (`IOCTL_STORAGE_QUERY_PROPERTY` bus type, asked with no
  access rights) is hot-plug: an external disk. Drive is one drive's card, Removable drives
  the plug list. They share one reader and one walker: the drive service ([S-4655]) is the only caller of
  this walk; the disk meter's volumes and the disk-space figures are its last walk's.
- **[S-4651]** A drive is named as This PC names it: its label, or "Local Disk", "USB Drive", "CD
  Drive", "Network Drive", "RAM Disk", then the letter; a mapped share without a label is
  "share (\\server) (Z:)".
- **[S-4652]** Its kind as Explorer's Type column says it, with the file system: "Local Disk · NTFS",
  "Removable Disk · exFAT", "Network Drive", "Disconnected network drive".
- **[S-4653]** The capacity bar is the used share as the Chart block's bar ([S-6717]: the 4 track, its
  fill in the load's role: the accent, Caution from 70 %, Critical from 90 %, `DriveRules.Role`; the
  figure itself keeps the primary role); with it "419 GB free of 931 GB", or why there are no figures:
  "No media", "Connecting…", "Disconnected", "Not connected".
- **[S-4654]** A network drive is never waited for. `WNetGetConnectionW` (the redirector's own record,
  no question to the network) speaks first: a connection Windows remembers but has not made
  (`ERROR_CONNECTION_UNAVAIL`) is disconnected and its share is not asked. Any other share is probed on
  the pool, one probe per letter at a time. Until its first answer it is "Connecting…" (never the red
  cross at start); after that the last answer stands while a new one is out, and an answer that changes
  whether it is there walks again.
- **[S-4655]** The drive service (`DriveService`) is the one delivery of the drives, for every drive
  widget: one walk on the pool, single-flight, when Windows says a volume came or went
  (`WM_DEVICECHANGE` arrival or removal, heard by the command window, also for a share mapped or
  dropped), when a share answers, and every 30 s while anything watches (free space). Drive and
  Removable drives watch it while live ([S-1880]); the system hub's disk activity source
  holds a watch while its feed lives, so the disk meter is read from app start like
  every hub source ([S-2513]). A settings
  page reads its last walk, or walks once when nothing watches.
- **[S-4656]** A walk that changes nothing shown tells nobody: two walks are the same when every drive
  is, free space compared to the megabyte.
- **[S-4657]** A drive's picture is the shell's icon for a local drive that is there (cached per drive
  and medium) on the family's icon sizes (16, 32, 48, [S-6704]); the kind's glyph (`DriveRules.Glyph`,
  secondary) until it is in, always for a share (never asked for a picture: that is a question to the
  server), and in a render, whose drives are not this PC's (`DriveIcon`). A disconnected share says so
  in words ([S-4671]); its picture wears no badge.
- **[S-4660]** Removable drives (add menu: Devices & power, [S-3511]): a row per USB stick or card (removable), disc
  (optical) and external disk (hot-plug fixed), by letter; an empty reader is left out, as Explorer
  hides empty drives. Nothing plugged in is the Status row at the top under the stick glyph ([S-6714]).
  No options of its own.
- [S-4661], [S-4662], [S-4664]: moved with the Removable drives widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.RemovableDrives/`).
- **[S-4670]** Drive (add menu: Devices & power, [S-3511]): one drive (`drive`, a root; empty for the system drive; anything
  else is repaired to the system drive at load). A drive that is not there keeps its card, "Not
  connected".
- [S-4671], [S-4675], [S-4672], [S-4673], [S-6274], [S-4674]: moved with the Drive widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Drive/`).

Needs a live pass: an alarm ringing (the sound, the notice and its buttons, packaged and unpackaged;
the package declares the notification activator), across sleep and a time-zone change; a USB stick
plugged in, ejected and pulled; a mapped share connected, unplugged (the cross, no hang) and remembered
but not reconnected at sign-in; a stopwatch running across a restart and a reboot.

## Air quality, rain and exchange rates (ONLINE-2)

Three online widgets on free, unauthenticated services, fetched only by the background web sync
([S-2663]); a widget draws what the sync holds and never fetches. Each is a module
(`src/Widgets/Themia.Widget.AirQuality/`, `.RainNextHour/`, `.ExchangeRates/`) with its config, view,
ladder, words, settings and tags; this section keeps what they share. SDK: `Widgets/AirQuality.cs`,
`Widgets/RainNowcast.cs`, `Widgets/ExchangeRates.cs` (wire parsers and rules), `Widgets/OnlineLayouts.cs`
(the measures the ladders share), `Sync/WebInterests.cs`, `OnlineParts` (an answer as the kit's stale rule reads it, Sdk.WinUI).
Core: `Storage/OnlineCacheStore.cs`. App: `Services/Web/OnlineService.cs`, `WebSync` (behind
`IWidgetHost.Weather` and `.Rates`). Strings: the source line in `Locales/online`, the rest in each
module. Tests `OnlineWidgetsTests` (`S51xx_`), the side-by-side ones in the module test host
(`OnlineFamilyTests`).

Air quality (`AirQuality`, 128):

- [S-5100], [S-5101], [S-5103], [S-5104], [S-5105], [S-5106], [S-5107], [S-5108], [S-5109], [S-6169], [S-6170]: moved with the Air quality widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.AirQuality/`).
- **[S-5102]** open-meteo's air-quality API (`air-quality-api.open-meteo.com/v1/air-quality`,
  `current=european_aqi,us_aqi,pm2_5,pm10,ozone,uv_index` and the six pollen fields, unix time).
  `AirQualityWire.Parse`: not JSON, not an object, an error document, or no timed `current` block is
  rejected; neither index is Empty; each figure null, a string or out of its physical range is
  dropped alone (never 0). A pollen field carried as null means "no pollen forecast here" and is
  told apart from 0 grains. Free for non-commercial use; the page credits CAMS and Open-Meteo.

Rain next hour (`RainNextHour`, 129):

- [S-5120], [S-5121], [S-5125], [S-5126], [S-5127]: moved with the Rain next hour widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.RainNextHour/`).
- **[S-5122]** open-meteo's forecast API with `minutely_15=precipitation&past_minutely_15=1&forecast_minutely_15=9`
  and `hourly=precipitation_probability` for three hours, unix time. A 15-minute value is the sum of
  the quarter hour **ending** at its stamp. `RainWire.Parse`: no `minutely_15` block with `time` and
  `precipitation` arrays is rejected; steps with a bad stamp or amount are dropped one by one (time
  order, a repeated stamp once); none left is Empty; the hourly chances are an extra. Outside Central
  Europe and North America open-meteo interpolates the 15-minute series from hourly data. Snow counts
  as precipitation.
- **[S-5123]** Under 0.1 mm in a quarter hour is a trace, not rain. Intensity from the hourly rate
  (4× the step): light under 2.5 mm/h, moderate under 7.6, heavy above.
- **[S-5124]** `RainRules.Outlook(now)`: the window is the quarter under way and the next ones, eight
  in all. Dry throughout: "No rain in the next 2 hours"; wet throughout: "Rain for the next 2 hours";
  a change: "Rain in N min" / "Rain stops in N min", N the minutes to the start of the first changed
  quarter rounded to 15 and never below 15. Fewer than four quarters ahead (a nowcast kept too long
  offline) is "No forecast for the next hour", never "dry". The caption: the heaviest intensity and
  the window's total ("Moderate rain · 4.4 mm in 2 hours"), and the hour's chance.

Exchange rates (`ExchangeRates`, 130):

- [S-5140], [S-5143], [S-5144], [S-5146], [S-5147], [S-5148], [S-6167], [S-6168]: moved with the Exchange rates widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.ExchangeRates/`).
- **[S-5141]** One document for every pair: `eurofxref-hist-90d.xml` (about 70 KB, the last 90 days,
  newest first; the daily file is the same shape). `EcbWire.Parse`: XML with DTDs prohibited and no
  resolver, at most 320 KB; no dated `Cube` is rejected; a bad date, a rate that is not a positive
  number or a code that is not three capitals is dropped, the rest of its day kept; nothing left is
  Empty. ECB terms: free reuse with the source named (the menu's note and the header tooltip do).
- **[S-5142]** The currencies offered are the ECB's 30 plus the euro (`ExchangeRules.Published`).
- **[S-5145]** Every pair is a cross rate through the euro (quote per euro ÷ base per euro); the change
  is against the newest earlier day holding both; a currency missing on the latest day is "Not
  published". Rates print four decimals under 10, three under 100, two above, locale digits.

Shared:

- **[S-5160]** The header follows the kit's one stale rule as weather does ([S-2612], [S-7042]): the
  problem glyph while the answer on screen is one the last attempt could not improve on, the age on
  the Refresh line of the widget's menu ([S-730]); the source is named on its menu (a note).
- **[S-5161]** The last answers survive a restart in their own sealed web shelf (`web:online-cache`,
  never swept, [S-2665]): each reply as the service sent it, parsed again through the same parser on
  restore, so a stored copy is never trusted more than a fresh one. Newest 16 per source; not
  restored beyond 2 days (air), 6 hours (rain) or 10 days (rates); another schema version is refused whole.

Needs a live pass: the three widgets against the real services (a place in and outside Europe for
the index and pollen, a rainy hour, an ECB publication day and a weekend), offline with a restored
shelf, and the Windows region read (`GetUserDefaultGeoName`) in the packaged build.

## Date (DATE)

The clock's Date style (issues #150, #131), with the clock in its module.

- [S-5300], [S-5301], [S-5302], [S-5303], [S-5304]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).

## Week planner (WEEK-PLANNER)

The widget is the module `weekPlanner` (`src/Widgets/Themia.Widget.WeekPlanner/`, its `SPEC.md`: the
config, its page, setup and sync); the SDK's rules it is drawn by stay here. Tests:
`tests/Themia.Core.Tests/WeekPlannerTests.cs`. Issue #160.

- **[S-5321]** A rolling week: today and the six days after (`WeekPlannerRules.Range`), fetched whole
  whatever the size shows. Days stand side by side as the width allows (`WeekPlannerRules.Plan`, on
  the view's area: a column at least 240 wide, its rows carrying the card's side inset so the
  columns meet, up to seven). One column: today alone on a Compact-height tile (no heading), else
  the days stacked as an agenda's. A column under 180 drops the events' start times.
- **[S-5322]** A day's rows (`WeekPlannerRules.Build`), each a Row block of 32 ([S-6711]): its
  events as a calendar day lists them ([S-570]: birthdays, all-day, then timed by start), today
  without the timed events already over; an event row is its calendar's colour as a 2 px bar in the
  lead ([S-566], a data colour, [S-6705]; the secondary role with one calendar), its title as the
  name and its start in the region's clock as the value (secondary, also while it runs; none for a
  whole day); a click opens it as a calendar's does ([S-1268]). Under today only: the open tasks due
  today or overdue, oldest first ([S-596]), led by a 16 ring of stroke 2 (critical when overdue);
  the ring ticks the task off through the service ([S-597]), inert in a preview and in edit mode.
- **[S-5323]** Whole rows only (`Columns`, `Stack`): a day shows its heading (one row of 28,
  BodyStrong: "Today" primary, "Tomorrow", then the short date secondary; never the accent) and as
  many rows as fit; when it has more, the last row says "+N" (secondary), except in a single row,
  which shows the day's first item. An empty today says "Nothing planned"; stacked, later days with
  nothing are skipped and the list ends at the first day cut short or whose heading and a row no
  longer fit. Loading and a problem are the state row at the content top ([S-7039]).

## The assistants' server (MCP)

Issue #165. AI assistants (Claude Code, other MCP clients) read what Themia shows over the Model
Context Protocol, only while the user has switched it on. Core: `Themia.Core/Mcp/` (JSON-RPC,
the HTTP rules, the protocol, the tools, the listener, the address) and
`Settings/SettingsMenu.Assistants.cs`; the app: `Services/AppHost.Mcp.cs`, `Services/Mcp/`
(`HostMcpData`, `McpKeyFile`). Tests: `tests/Themia.Core.Tests/McpTests.cs`,
`SettingsMenuTests.Assistants.cs`, `tests/Themia.Modules.Tests/McpDeskTests.cs`.

- **[S-9960]** Off by default (`Preferences.McpServer` false). ⚙ › AI assistants' switch is the one
  line that sets it ([S-3592]); with it off no socket is open and no server code runs. Every state
  change reconciles the server (`AppHost.ReconcileMcp`), so the switch, an undo or a reset start or
  stop it at once: stopping closes the listening socket before it returns and drops the requests
  being served. An import keeps this PC's value ([S-498]). A detached state (every render flag) and
  the time before the app has started never run it.
- **[S-9961]** Transport: MCP's Streamable HTTP, served by the running app on `127.0.0.1` only (a
  `TcpListener` on `IPAddress.Loopback` with exclusive use of the address, never `Any` or IPv6), at
  `http://127.0.0.1:<port>/mcp`. Why not a stdio shim (`Themia.exe --mcp` over a named pipe):
  Themia is one long-running process, and HTTP needs no second process, no exe alias in the
  package and no start of the WinUI exe for each client session; Claude Code adds an HTTP server
  with one command. Another machine cannot reach it (loopback is not routed; a peer that is not
  loopback is dropped all the same); another Windows user on this PC can open the port but is
  refused without this install's token ([S-9966], [S-9977]). Left open: a program of another user
  that takes the port while Themia is not running would receive the token from a client; on a PC
  shared with untrusted users, choose New address and key afterwards. Claude Desktop's config file
  starts stdio servers only: it reaches Themia through a stdio-to-HTTP bridge of the user's choosing
  until a stdio shim exists. The MSIX package is full trust (`runFullTrust`), which is not subject to
  AppContainer's loopback isolation, and binding to loopback raises no firewall prompt; this needs a
  live pass in the packaged build.
- **[S-9962]** Protocol version 2025-06-18, and 2025-03-26: `initialize` answers the client's
  version when it is one of them, else 2025-06-18, with the `tools` capability (`listChanged`
  false), `serverInfo` (`themia`, the app's version) and short `instructions`. `ping` answers `{}`;
  `tools/list` and `tools/call` as [S-9967]; any other method is -32601. Notifications and a
  client's replies get 202 and no body. Stateless: no `Mcp-Session-Id`, no server-sent events, no
  requests from the server.
- **[S-9963]** JSON-RPC 2.0 at the trust boundary (`JsonRpc.Parse`, pure): one object per body; a
  batch (an array: 2025-06-18 has none), a missing or wrong `"jsonrpc"`, an id that is null,
  fractional, an object or longer than 256 characters, a method that is not a string of 1 to 128
  characters, or params that are not an object or array are -32600 (invalid request), answered
  under the id when one could be read and `null` otherwise; a body that is not UTF-8 JSON, has
  comments or trailing commas is -32700 (parse error). Answers are written with `Utf8JsonWriter`, no
  reflection.
- **[S-9964]** Limits: a body of at most 64 KiB (`JsonRpc.MaxBodyBytes`, refused by its length
  before it is read, [S-9966]), nesting at most 16 deep; past either the message is refused.
- **[S-9965]** HTTP/1.1 only, one request per connection, every answer `Connection: close` and
  `Cache-Control: no-store`. The head is at most 8 KiB (431 past it) and 64 header lines, printable
  ASCII, no folded lines; `Host`, `Origin`, `Authorization`, `Content-Length`, `Content-Type`,
  `Transfer-Encoding` and `MCP-Protocol-Version` at most once. At most 8 connections are served at
  once (503 beyond); a client has 10 s to send its request and a tool 30 s to answer (the UI thread
  busy): resource bounds, not correctness waits.
- **[S-9966]** Checked before the body is read, in this order: `Host` is `127.0.0.1:<port>` or
  `localhost:<port>` and an `Origin`, when sent, is `http://` one of those (DNS rebinding: 403); the
  path is `/mcp` exactly (404); `Authorization: Bearer <token>` holds this install's token, compared
  in constant time (401 with `WWW-Authenticate: Bearer`); the method is POST (405, `Allow: POST`:
  no event stream to GET, no session to DELETE); an `MCP-Protocol-Version` header names a version
  spoken (400); no `Transfer-Encoding` (400); a `Content-Length` of 1 to 64 KiB (411, 400, 413);
  `Content-Type` `application/json` (415); an `Accept`, when sent, takes JSON (406). A message that
  could not be read at all is answered 400 with its JSON-RPC error, any other 200. No CORS headers
  are ever sent, so a web page cannot read an answer.
- **[S-9967]** Eight tools, each read-only (annotations `readOnlyHint` true, `destructiveHint`
  false, `idempotentHint` true, `openWorldHint` false), each with an object input schema that
  allows no other argument: `list_displays`, `list_views`, `list_widgets` (`view_id`),
  `list_notes`, `read_note` (`note_id`), `read_tasks`, `read_habits`, `read_calendar` (`days`, 1 to
  31, default 7). No tool writes, opens, signs in or starts a sync.
- **[S-9968]** A call answers one JSON object as MCP text content. An unknown tool, an argument
  that is not in the schema or of the wrong type, and a value out of range are -32602 (invalid
  params); a view or note that is not there, and a read that failed, are a result with `isError`
  true and words for the model (a failure's exception is logged, never sent).
- **[S-9969]** Every read is made on the UI thread (`HostMcpData`) from what the host already
  holds; the server's threads only wait. Never returned: a token, an account's secret or address
  book, a mail, a file's contents, a display's device path, a meeting's join link, an event's web
  link. The clipboard history is left out on purpose: it holds what was copied from password
  managers and one-time codes, and an assistant that reads it can be steered by text in a page or
  file to read it out. Mail is left out of this first version for the same reason (its senders and
  previews are other people's words, and a mail is the classic carrier of instructions aimed at an
  assistant).
- **[S-9970]** `list_displays`: each display as the shell has it, numbered from 1 in the shell's
  order, its name (`MonitorTarget.ShortName`), its size in physical pixels, primary, whether Themia
  is on there, and the id of the view it shows (its assignment, else the active view: read without
  repairing a dangling one, `McpDesk.Of`).
- **[S-9971]** `list_views`: id, name, active, widget count and the displays showing it.
  `list_widgets`: per view, each widget's id, kind (its module id), label (the one the user gave,
  else its kind's name in the current language) and position and size as fractions of its display
  (four decimals).
- **[S-9972]** *(Reserved.)*
- **[S-9973]** `list_notes`: id, title and last change of each note of the note pool; `read_note`:
  one note's Markdown as the Notes widget would show it (the latest typed), cut at 32 768
  characters with `truncated` true.
- **[S-9974]** `read_tasks`: the task lists kept on this PC (source "This PC"), then each account
  list (or Today) a Tasks widget holds now, as last synced, with the account's label and when it
  synced; a list nobody shows is not read, so no sync is started. Each task's title, done and due as
  the provider gave it; at most 500 a list (`truncated`).
- **[S-9975]** `read_habits`: each habit list, each habit's name and the periods it was done in,
  most recent first, at most 120.
- **[S-9976]** `read_calendar`: from today's local midnight for `days` days, every signed-in
  calendar account's default calendar as the calendar widgets read it (the offline store, never
  forced), in start order, at most 500 an account: subject, all-day days or start and end with
  their offsets, location, organizer, declined; the account's label, when it synced and the sync's
  problem when there is one.
- **[S-9977]** The address: a port and a token of 32 random bytes (base64url, 43 characters), in
  `mcp.json` beside the state file with the token sealed by DPAPI for this Windows user ([S-525]),
  written atomically; never in the settings file, so no backup carries it, never logged (the log
  shows the URL only, and redacts bearer credentials besides, [S-983]). The first start takes a free
  port from Windows and stores it, so the setup stays valid across restarts. A file that is absent,
  not JSON, of another shape, or whose token will not open here (another user or PC, plain text) is
  made again and logged. A token that cannot be stored stops the start ("its key could not be
  saved"). New address and key (asked first) takes a new token and a new free port at once: what
  was set up before is refused from then on. A taken port is said in ⚙ and never retried on a timer.
- **[S-9978]** ⚙'s setups: the Claude Code command (`claude mcp add --transport http --scope user
  themia <url> --header "Authorization: Bearer <token>"`) and the JSON entry other clients take
  (`mcpServers.themia`: `type` http, `url`, `headers.Authorization`). They are copied, never shown:
  the clipboard text is marked `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory` 0 and `CanUploadToCloudClipboard` 0 (Windows' history, the cloud
  clipboard and other monitors leave it), and Themia's own clipboard history skips it
  (`ClipboardWatcher.SetPrivateText`). Under the line: "Copied. It holds this PC's access key…", or
  that another app holds the clipboard.
- **[S-9979]** ⚙ › AI assistants (after Online updates; "On"/"Off" at its end): the switch "Let AI
  assistants read Themia", under it what the server is doing (off, starting with a ring, "Listening
  on http://127.0.0.1:<port>/mcp, for this PC only.", or why it could not start in the critical
  colour); while on and listening, how to connect, Copy Claude Code command and Copy JSON setup
  with their outcome under each; while on, New address and key; then what assistants can and cannot
  read. The token is in no line's words. Not there for a host that runs no server (a render's
  stand-in draws a made-up one, `--editbar-png`: `settings-*-assistants*`). Words English-only for
  now (`Locales/mcp/`).
