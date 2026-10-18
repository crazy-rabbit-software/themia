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

- **Foundations.** Geometry model; Rendering; Grid snap aid; Constraints; Defaults & placement; Views & displays; Window layering; Transparency; Ticking; Live widgets and resource use (R6-RESOURCES); Memory; Persistence; Failure rules; Clipboard & transfer between displays; No backwards compatibility; Widget modules and the registry (MODULES).
- **Shell, windows and settings.** Displays (WP2); Desktop integration and suspend (WP3); Appearance engine (WP1); Notices, resilience and undo (WP4); Widget picker: search (WP5); Multi-select and the placement session (MULTISELECT-PLACEMENT); Widget styles (STYLES); Context menus (R4-MENUS); Views and the view bar (WP8); The Displays and Views pages (R4-NAV, R5-PAGES); Icon picker and showcase views (R4-ICONS); Keyboard lease (WP11); Widget keys (WIDGET-KEYS); View links (VIEW-LINKS); Keyboard in the shell (SHELL-KEYS); Motion: intros, hyperspace, hacker (WP9-MOTION); Desktop surface drops (WP10-SURFACE); Opening a widget's app (R7-OPEN, retired); Shell hooks (PA); Removed widgets (R7-REMOVE); Shell skeleton and shared settings; Shared widget settings: region and updates (SETTINGS-AUDIT); Settings window: Windows 11 Settings; Settings navigation icons (NAV-ICONS); Logging (WP7); Network safety (WP6); Localization infrastructure (LOC-INFRA); Microsoft Store package and licensing; Gallery.
- **Look and layout.** Rows and the card edge; Looking like Windows; The widget header; Responsive layout (shared foundation); Adding a widget; Layout pass over the ported widgets; Widget polish 1 (POLISH-1); Widget polish 2: leftover parity and layout gaps (POLISH-2); The Set up button (R5-SETUP).
- **Accounts and sign-in.** Where account data comes from; Accounts and sign-in.
- **System widgets.** (The seven meters, the seven detail widgets, Drive, Removable drives, Brightness, Mixer, Volume, Music and the Audio visualizer are modules: their sections are their projects' `SPEC.md`; the sections named here keep the hub, the samplers and the rules they share, the SDK's.) System data hub (SYS-HUB); System meters; Meter framework and memory; CPU; Disk and ping; Battery and network; Network and ping; System widgets: GPU, mixer and screen time; Screen time: chart, donut and apps (SCREENTIME-SPLIT); GPU and screen time: the whole tile (SYS-P4); System details (SYS-DETAILS); Mixer and Volume (SYS-P5); Power and Bluetooth; Device and location (DEVICE-AND-LOCATION); Quick-setting toggles (QUICK-TOGGLES); Windows' panels: notifications, focus session, brightness (WINDOWS-PANELS).
- **Time, desk and launch widgets.** (Clock, Countdown, Stopwatch, Pomodoro, Quote, Sun, Moon, Scratchpad, Recycle Bin and Year progress are modules: their sections are their projects' `SPEC.md`, `src/Widgets/Themia.Widget.<Name>/`; so are Bluetooth, the seven power buttons and the thirteen quick-setting toggles, under System widgets, whose shared rules stay here.) Clock; Countdown and pomodoro; Stopwatch, alarm and drives (KARPO-2); Sun and moon; Audio visualizer (VISUALIZER); Clock, Sun & moon, Quote and Scratchpad: parity and the whole tile (PJ); Music and Pomodoro: parity and the whole tile (PF); Notes; Notes: note store and markdown editor; Clipboard and Countdown: the whole tile; Year progress (YEAR-PROGRESS, its module's SPEC.md); Recent files (RECENT-FILES); Recycle Bin (RECYCLE-BIN); Date (DATE); Habits; Shortcuts B: drop, reorder and app layouts (PD); Folder: the file browser (FOLDER); About and the settings file.
- **Mail, calendar, tasks and online widgets.** (Weather now, Weather forecast, Rain next hour, Air quality, Exchange rates and Stocks are modules: their sections are their projects' `SPEC.md`; the sections named here keep what they share with the host.) Mail; Calendar and contacts; Tasks; Mail: the whole tile; Calendar and contacts: the whole tile; Calendar: five styles and shared dates (CAL-SPLIT); Tasks: using the whole tile, and the rest of the original; Weather: current conditions and forecast; Feed, stocks and the web widgets: parity and the whole tile (OFF-E); Web widgets: video stream; Web widgets: size ladder and overlay; Offline sync (SYNC); Next meeting (NEXT-MEETING); Week planner (WEEK-PLANNER); Air quality, rain and exchange rates (ONLINE-2).

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
  fractional floor at the moment of resize, so it means the same physical size on every monitor.
- **[S-09]** Widgets are kept **in reach** (`GridGeometry.Clamp`), not inside: at load against the
  whole screen, and against the display's **work area** for drag, resize, placement, paste, drop and
  the arrow keys ([S-841]), each by the one rule of [S-6608]. No widget is ever bigger than that area.
  Clamping is **total**: a non-finite or inverted rect collapses to a valid one rather than
  propagating NaN into storage.
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
  minimum on it ([S-6608]); an anchored edge never moves. Snapping the whole rect rounded the anchored
  edge too, and clamping the whole rect pushed the widget about instead of stopping the moved edge.
- **[S-31b]** Edit mode draws no outline round a widget. The eight handles are its markers
  (`EditMarkers`): a **square-cornered L-bracket** at each corner (arms 14 DIPs, never rounded, whatever
  the card's radius) and a **16-DIP bar** at the centre of each edge, 3 DIPs thick, flush inside the
  cell bounds, each over a 1-DIP contrast halo (`SolidBackgroundFillColorBase`) so it reads on any
  wallpaper. A side too short for two arms, a bar and 4-DIP gaps shrinks the arms and bar alike (never
  below the thickness). Every edge lands on a device pixel at the window's scale. A handle's grip is
  its ink plus the old 10-DIP square on its corner or edge midpoint; grips never overlap. Cursors and
  the keyboard resize are unchanged. (Tests `S31b_`.)
- **[S-31c]** Every widget in edit mode shows its markers: a selected one in the accent
  (`AccentFillColorDefault`), every other one in the neutral strong stroke (`ControlStrongFillColorDefault`).
  Each widget of a multi-selection shows accent markers. `--editbar-png` writes `marks-<theme>-r4.png`,
  `-r16.png` and `-multi.png`.

## Defaults & placement

- **[S-10]** A widget definition declares its default size in **pixels**. At placement that is
  converted to a fraction of the current screen, so a newly dropped widget has a consistent physical
  size on any monitor, and scales per [S-02] thereafter.
- **[S-11]** Placement centres the widget on the drop point, then snaps ([S-06], [S-6609]) and keeps
  it in reach ([S-09]): one dropped near an edge or on the taskbar may hang over it, but keeps the
  visible minimum on the work area ([S-6608]). The ghost shows exactly that.

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
- **[S-3962]** No hover state: the pointer-over fill and border are the rest ones (transparent), so
  the field never lights up as the pointer crosses it. Nor on focus (user requirement): the focused
  fill is the rest one too, with no accent frame or line (the focused state's elevation brush and
  thickness are cleared, and no system focus rectangle); the caret alone shows where the keys go. Through the same resource
  keys, theme brushes only.
- **[S-130]** "Clear note" confirms in a dialog. The text is unrecoverable, so one click is not
  enough, and failing to show the dialog leaves the note alone. It empties the note's body (for every
  widget showing it); the note and its title stay.

## Countdown and pomodoro

- [S-140], [S-141], [S-142], [S-143], [S-144], [S-6030], [S-6031], [S-6032], [S-6036], [S-6037]: moved with the Countdown widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Countdown/`).
- [S-150], [S-151], [S-152], [S-155], [S-6035]: moved with the Pomodoro widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Pomodoro/`).
- **[S-153]** The cue is synthesised in Core and played through `winmm!PlaySound` from memory, not
  through `MediaPlayer` or `AudioGraph`: those are whole media pipelines the AOT compiler roots in
  full in order to play two seconds of oscillators. `CuePlayer` copies the wave to native memory and
  plays it asynchronously, single-flight: the next cue stops the previous one (`PlaySound(NULL)`)
  before its buffer is freed, so at most one buffer is ever outstanding and nothing is GC-pinned.
  A widget (the pomodoro, a settings page's preview) reaches it through the SDK's `SoundCue`, the
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
  **rejected** — the last good value stands and the row says why — never coerced to 0, which would
  silently move the user to the Gulf of Guinea.

### Two widgets: Sun and Moon (SKY-SPLIT)

Sun & moon is split in two (user rule: one widget, one thing). Each is a module of its own
(`src/Widgets/Themia.Widget.Sun`, `src/Widgets/Themia.Widget.Moon`, their SPEC.md files). The SDK:
`SkyPlaceConfig`, the astronomy (`MoonPhase.NextRiseSet`, `SkyText.EventTime`), `SkyParts` (the
shared row and moon drawing) and `SkyPlaceRows` (the shared place rows); tests `SkyWidgetsTests`
(`S310x_`, the astronomy), the pair's in the module host's `SkyWidgetsHostTests`.

- **[S-3100]** **Sun** (`sun`, 320×240, sun glyph) is the sun's day: the path graphic, the
  number strip and the rows sunrise, sunset, daylight (+ change), solar noon, dawn, dusk ([S-3102]).
  **Moon** (`moon`, 240×220, moon glyph) is the moon: the disc, its phase's name, the lit
  percentage, moonrise, moonset and the next full or new moon ([S-3105]). Both sit in the add menu's
  Weather & sky subject ([S-3511]), start headerless ([S-700]; a header given them is titled with the place), start at Zurich, so they
  need setting up only once their place is cleared ([S-2848]), and have no keys ([S-1996]). There is no combined kind: `SunMoon` (16) is gone and
  a state file holding it drops that widget at load like any unknown kind ([S-1900], [S-1050]).
- **[S-3101]** Both carry the observer's place (`SkyPlaceConfig`: latitude, longitude, place name)
  and share one settings block: city search ([S-1494], [S-2840]) and the validated exact
  coordinates ([S-171]). Sun adds the Events switch; Moon has nothing else (how each draws itself
  is its style, [S-6045], [S-6050]). `Repair` at load: a latitude or longitude that is not a number
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
- **[S-407]** A side strip has the card's two corners on its edge and the body the other two
  (`StripCorners`, `BodyCorners`); its fill follows the header fill rules of a top one ([S-828],
  [S-1761]: its own colour at the header opacity, or the widget's fill faded by it), the Fluent tint
  stops inside a solid stroke on its three outer sides ([S-836]), and the body's base reaches one DIP
  under it on the seam.
- **[S-408]** A side strip is the **icon and the title** only (concise first): the header's trailing
  part (a badge, a caption, a picker, Notes' formatting bar) is sized by its view for a header as
  wide as the content, and along a strip it would be measured against the card's height and read
  sideways. It stays with the view and comes back with a top or bottom header.
- **[S-409]** A side strip sheds on a card under **160 DIPs** wide (`HeaderStrip.SideShedsBelow`; a
  top or bottom one under 96 high, `FlatShedsBelow`). Derived: the strip eats about 40 DIPs (a
  header's height), and the content beside it must stay at least as wide as the narrowest card every
  widget is laid out for, the XS tier's 120 ([S-1810]). Under 96 (the old threshold) an XS card kept
  about 76 DIPs of content: Notes' empty text wrapped per letter and Mail's Set up button clipped.

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
  `[assembly: ThemiaWidget(id, typeof(M))]` and `[assembly: ThemiaProvider(id, typeof(M))]`, in the
  app itself or in an assembly it references, becomes one `new M()` in `ModuleRegistry`, in id
  order. No reflection and no run-time loading, so Native AOT keeps every module. A malformed id
  fails the build (THEMIAMOD001), so does an id declared twice (THEMIAMOD002), a type that is not
  a public, non-abstract module class with a public parameterless constructor (THEMIAMOD003), and a
  module project (`Themia.Widget.<Name>`, `Themia.Provider.<Name>`) declaring anything but the one
  module its name says, `<name>` in camelCase (THEMIAMOD004).
- **[S-6902]** Each widget has one `WidgetDescriptor`, the pure half of its module: what the host
  knows of it without its UI. The host installs the registry into `ModuleCatalog` before it reads
  any widget (the app first thing in its constructor), once; a registry with a missing
  descriptor, an empty id, or an id or kind given twice is refused at that install, and so is a
  module whose descriptor's id is not the id its attribute declares. Every per-widget question the
  host asks is answered from the catalogue, not from a table of its own. A widget in its own
  project ([S-6911]) declares its descriptor on its module class. Until a widget moves there (M4),
  its descriptor is declared on its config in Core and its module class in
  `Themia.App/Widgets/Modules`; Core's tests install those descriptors and check that the app
  declares exactly one module for each.
- **[S-6903]** A state document is read and written with one set of options (`StateJson`): the
  host's own metadata, then each installed widget's source-generated resolver, and a contract
  modifier that adds every widget's config type to the `WidgetConfig` union with its id as the
  `kind` discriminator. No attribute or list in the host names a widget's config; the union holds
  exactly the registry's configs, and every widget's resolver uses the host's options (camelCase,
  indented, nulls left out), so a module's config is written as a built-in one is.
- **[S-6904]** A widget's settings rows are its module's (`IWidgetModule.SettingsRows`), built only
  from `FluentLayout` into the host's `WidgetSettingsRows` (the rows, the save, and Settings ›
  Accounts as the one place a page may send the user); a module with none has no page ([S-1750]).
  The host still lays every page out the same way ([S-3705], [S-1752]). There is no dispatch over
  config types: a page wired to no module, or a module naming a page that is not there, fails a test.
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
- **[S-6909]** Strings per module: a project that sets `ThemiaStringsEnum` (`YearProgressStr`, a
  name ending in `Str`, else THEMIALOC010) gets its own enum, tables and `Text()`/`Format()`/
  `Plural()`/`Local()` extensions from its own `Localization/Locales`, in its own namespace, under the
  same file rules as the host's ([S-1101], [S-1111]); the SDK sets none and gets `Str`. Every table,
  the host's included, looks up through one `StringTable` ([S-1104]: the language, else English
  noted once, else the key). A descriptor hands the host its texts as `LocalText` (a table and an
  id, or a fixed brand name such as "Microsoft"), read in the current language each time.

- **[S-6910]** Providers are modules too (`IProviderModule`, `[assembly: ThemiaProvider(id, typeof(M))]`):
  each states its id, its name (`LocalText`), the features its accounts offer, how they sign in
  (`AccountAuth`), its place in Settings › Accounts › Add account, and its own Updates row or none
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
  preview ([S-1251]), Settings › Accounts (false where there is none, a debug render), and the
  host's services a widget reads: the Recycle Bin (`IRecycleBin`: the one answer how full it is and
  Windows' own empty, [S-4863]) and what runs a power action (`IPowerBackend`: the machine's calls,
  inert on a preview, [S-1243], [S-1251]) and the quick settings (`IQuickToggles`: one
  shared `IToggleSource` per toggle, [S-3907]) and the web answers the background sync keeps
  fresh ([S-2663]: `IWeatherAnswers` for the conditions, forecast, air and rain, `IQuoteAnswers` for
  the quotes, `IRateAnswers` for the ECB table; each a `Fetched<T>` per want, the key it is synced
  under, `Show`/Refresh and `Updated(key)`; where the sync is not running, a debug render, what the
  shared caches hold), and the system widgets' sources: the system figures (`ISystemFeeds`: the one
  hub's `Watch` over a typed `SourceId<T>` and `WatchSeries`, null before the host has started it,
  [S-2500]), the drives (`IDrives`: the one walk, [S-4655]), the output devices (`IAudioOutputs`: the
  one Core Audio worker's subscriptions, levels, mutes and default-output switch, [S-1220]), every
  display's brightness (`IBrightness`, [S-4441]), Windows' media session (`IMediaSessions`) and what the
  speakers play (`IAudioCapture`: the one loopback capture, [S-3203]); a settings page's host lists are
  seams installed at start (`DriveList`, `AudioDevices`, `GpuAdapters`), as are a process's
  executable (`ProcessPaths`) and a row's shell icon (`AppIconSource`). The host makes one per card, every member shared by all of them. What a widget shares with the settings
  window is in `Themia.Sdk.WinUI` too: the settings rows (`FluentLayout`, `ColorChoice`,
  `RowReorder`, `SettingRow`), the item menu (`ItemMenu`), `Notices`, a drop target's contract
  (`IWidgetDropTarget` over `IWidgetDrag`); the parts that need Win32 (the eyedropper, a file's
  shell icon, the OLE drag) stay host code behind a seam the host installs at start, as do the
  place lookup and the Stocks settings' symbol search (`SymbolLookup`, [S-1307]).

## Adding a widget

- **[S-420]** *(Retired: the picker whose sections were never split across columns (`ColumnLayout`)
  is the edit bar's add menu, [S-3501]; it is a tree of subjects, [S-3510].)* There
  is still no second way in from the settings window.
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

### The edit bar and its add menu (EDITBAR)

After Karpo's edit bar (`karpo-app`, `docs/widgets.md` §43 and §50, `Dock.EditDeck.cs`). Owning
files: `Shell/EditBar.cs`, `Shell/DesktopWindow.Toolbar.cs`, `Shell/WidgetPicker.cs`,
`Shell/DesktopWindow.Picker.cs`; rules in Core `Widgets/AddMenu.cs` (tests `AddMenuTests`).

- **[S-3500]** In edit mode each display's surface shows the **edit bar** at the top centre of its
  work area, 8 DIPs under the work area's top ([S-852]): a compact Windows 11 command bar, 40 DIPs
  tall — 32-DIP buttons in a 4-DIP padding, 4 DIPs between them — on the flyouts' acrylic, stroke,
  8-DIP corners and shadow. Buttons have no fill at rest and the subtle fills under the pointer and
  pressed. In order: **Add widget** (its "+" in the accent), **View** (the display's views, New view,
  Duplicate this view, [S-900]), **Snap** (lit with the accent while on, [S-905]), a 20-DIP hairline,
  then three icon-only square buttons whose words are their tooltips and names: **More** (Exit,
  [S-67]), **Settings**, **Done**. Each display has its own bar; the bar is inside the edit-mode hit
  region (the whole work area). While placing, a line in the bar's frame and height stands in its
  place ([S-421]).
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
  itself in one ([S-6907]); lines part the runs. Two top columns under their headings, **Popular** and **Browse**; Browse is a row per
  subject; inside a subject a **family** (one widget in several layouts or parts) is one row
  opening its own submenu. Never deeper than that. The catalogue has no sections of its own any more.
- **[S-3511]** What is where. **Popular** (each again from its home): Clock, Weather, Forecast,
  Calendar, Mail, Tasks, Notes, Now playing, CPU, Folder, Shortcuts. **Browse**, in
  order: **Time** (clock, date, alarm, countdown, stopwatch | pomodoro, focus session | year progress);
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
- **[S-3506]** Debug renders: `--editbar-png <outDir> [--theme light|dark]` draws the real bar on a
  stand-in display (snap on and off, the add menu open under Add widget, the placement line, and the
  properties strip, [S-3713]), and
  `--picker-png <out.png> [--search text] [--open Subject[/Family]] [--theme light|dark]` the open
  menu, `--open` opening the cascade by `AddFolderId` names
  (`CalendarMail`, `System/Processor`, `QuickSettings`); the render draws the
  submenus on a layer over a 1280 × 800-DIP stand-in work area, where the popups would stand.

Needs a live pass: the bar on every display at mixed DPI and with a top taskbar, the menu opening
under Add widget and at a right-click (and growing sideways as a submenu opens, near a display's
edge too), the hover delay, the keys in the menu, and a pick starting the placement.

### The selected widget's properties strip (QUICKSET)

How a widget's card looks — its header, its background, its title — is set where the widget is, in
edit mode, not on its settings page, and not on the edit bar: on a small panel that stands beside
the selection. After Karpo's properties panel (`karpo-app`, `docs/widgets.md` §145: a click in edit mode
only selects and a small panel of the selection's own properties stands by it) and its selected
widget's flyout (§65: closed while the selection is carried, opened again where it lands). Owning
files: `Shell/PropertiesStrip.cs`, `Shell/DesktopWindow.Properties.cs`, `Services/AppHost.QuickSet.cs`;
rules in Core `Model/QuickSet.cs` and `Geometry/StripPlacement.cs` (tests `QuickSetTests`,
`StripPlacementTests`).

- **[S-3700]** *(Retired by [S-3707]: the controls are no longer a group on the edit bar. A user
  looked for them at the widget, and the bar's group re-centred the bar every time the selection
  came and went.)*
- **[S-3701]** The strip shows on a display only while the selection ([S-1580]) has widgets in the
  view that display shows (`QuickSet.For`); ids no longer in it are not counted. It follows every
  selection change and every state change (an undo, a change from another display).
- **[S-3702]** One widget: a Title row (a box 32 tall, the kind's name as its placeholder), then
  Style ([S-6007]), Header, Alignment ([S-6584]), Background, Item background ([S-6521]), Item text ([S-6544]) and Scale ([S-6531]), and the
  More settings row ([S-6009]). Several: the words "N selected" over the rows, then Style (one kind
  only), Header, Background, Item background, Item text and Scale, and **no title** and no More settings: a title names one widget, so
  one typed for many would make them indistinguishable.
- **[S-3703]** With several, a value the widgets share is ticked and a mixed one ticks nothing
  (`QuickSet.Common`); a pick sets it on every selected widget of the view (`SetHeader`,
  `SetBackground`) in one `Mutate`, so one undo step ([S-884]) however many it reached.
- **[S-3704]** The title commits on Enter or when the box loses the focus, and Escape puts back what
  it held; Enter and Escape hand the keyboard back to the surface. While the box has the focus the
  edit keys do not fire ([S-804]): Delete, arrows and Ctrl+Z are the box's. What is stored
  (`QuickSet.Title`): trimmed, and nothing (the kind's name, in the current language) for a blank
  title or the kind's name itself. A commit that changes nothing records no step; one cut off by a
  click on another widget still lands, on the widget it was typed for. A change made elsewhere
  never replaces text half typed in the box.
- **[S-3705]** The widget settings page has no Header, Background or Title row and no Delete row
  and no Style, Item background, Item text or Scale row either ([S-6536]): it holds the widget's own options, then a Danger section only when the widget has a danger row
  (`FluentLayout.DangerRow`, e.g. clearing the clipboard history). Delete stays on the card menu, the Delete key and
  the undo notice ([S-1743]).
- **[S-3706]** *(Retired by [S-3713]: the edit bar no longer draws a selection group.)*
- **[S-3707]** The strip is a small **panel** on a flyout's surface ([S-3715]: the flyout's solid
  fill, stroke, 8-DIP corners and drop shadow), on its own layer over the cards and under the edit
  bar, one per display. **[S-6520]** Its entries stack, one row per property: the property's name
  in the secondary text colour, then its control in a 176-DIP column (32-DIP drop-downs and box),
  4 DIPs between rows, after Karpo's properties panel (§145). Style, Header and Scale are
  drop-downs that show the current value's icon and words (a dash for a mixed
  selection) and whose menus tick it (Header: Top, Bottom, Left, Right, Hidden with their icons; Background:
  and Item background are fill controls instead, [S-6566]), built as they open; Item text is the colour control ([S-1549]);
  More settings is a full-width row with the gear and its words. A press on its padding or words stays
  on it, so it never starts a rubber band or clears the selection. It is inside the work area
  ([S-3708]), and in edit mode the hit region is the whole work area ([S-851]), so it always takes
  its clicks. F6 reaches it after the edit bar while it shows ([S-1975]), and the keyboard on it
  counts as the chrome's, not the widgets': Tab goes round its controls and stays in it, Up and Down
  move between its rows, and Escape hands the keyboard back to the surface.
- **[S-3708]** Where it stands (`StripPlacement.Place`): **beside** the selection's box, 8 DIPs off
  its edge — to its **right**, or to its **left** where the right has no room (right to left the
  sides mirror: left first) — centred on the box's height ([S-3714]) and slid to stay inside the display's
  work area; where that meets the edit bar or the view bar, the nearest clear place in that column
  (just under or over the bar). Only when neither side has room (a widget spanning the work area's
  width) is it **below** the box, 8 DIPs off its bottom edge, its start edge level with the box's
  and slid along inside the area, else **above** it on the same terms. It never covers the widgets
  while anywhere has room; a selection that fills the work area has it **over** its top end corner,
  pressed inside the area.
- **[S-3709]** The box is the selected widgets' **painted** rects on this display (the gap is not
  the widget, [S-1585]), one widget's or the bounding box of several (`StripPlacement.Bounds`),
  where they are now, so it follows a nudge with the arrow keys, an undo and a paste.
- **[S-3710]** While the selection is dragged or a widget resized, the strip is away (Karpo's
  flyout while a block is carried, §65): following the pointer it would hang off the widgets and
  flip sides on every move. It comes back on the drop, placed against where the widgets landed. It
  is away too outside edit mode, while placing ([S-421]) and with nothing selected on the display.
- **[S-3711]** Some kinds can never have a header (`WidgetInstance.CanHaveHeader`): the thirteen
  quick-setting toggles ([S-3901]) and all six audio visualizer designs ([S-3200], [S-3212]). For a selection holding one, the strip has no Header choice and
  shows only what every selected widget can take (Background, and the title for one); `SetHeader`
  never reaches one; it is created headerless; load validation takes a header off it and says so;
  and the card draws it headerless whatever the file holds (`WidgetInstance.ShownHeader`).
- **[S-3712]** Header Hidden leaves no trace on the card ([S-401]): no strip, no icon, no title, no
  reserved row; the content area is the whole card at every size tier.
- **[S-3714]** Beside the selection the panel is **centred on its height** (`StripPlacement.Place`:
  its middle level with the box's middle, as a flyout opened to the side of its anchor stands), then
  pressed inside the work area and stepped clear of the edit bar and the view bar as [S-3708] says.
  Below or above (no side has room) it keeps its start edge level with the box's.
- **[S-3715]** The panel's surface is a Windows 11 flyout's (`FlyoutSurface`), so it stands apart
  from the cards and any wallpaper: the flyout presenter's solid fill (`SolidBackgroundFillColor
  Quarternary`, #2C2C2C dark / #FFFFFF light, the fallback of `AcrylicInAppFillColorDefault`), a 1 px
  `SurfaceStrokeColorFlyout`, 8-DIP corners and a flyout's drop shadow (8 DIPs down, blurred over 32,
  42 % dark / 18 % light). Not acrylic: in-app acrylic blurs only what the window draws, and the
  surface window draws nothing behind the panel, so it came out a faint tint the colour of the cards.
  The shadow is a composition drop shadow cast by a plate under the frame (a theme shadow needs
  receivers, and nothing under the panel can receive one); it falls inside the edit-mode hit region.
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
  text** rows are greyed out: the control disabled, the name in the disabled text colour, and "This
  widget has no items" as the tooltip of both. Decided for a multi-selection: the rows are on when
  **any** selected widget has items, read the fill (and text colour) of those widgets only, and a pick
  reaches only them (`SetItemBackground` and `SetItemTextColor` skip a kind without items).
- **[S-6572]** A widget without items keeps whatever item background and text colour it holds (a
  pick never writes one), and they draw nothing: there is nothing for them to reach.
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
- **[S-6531]** The properties panel's **Scale** row, after Item background and Item text,
  before More settings: a drop-down showing the scale's icon and percentage (a dash when the selected
  widgets differ, `QuickSet.Common` over the stored values) whose menu offers 75, 90, 100, 110,
  125 and 150 % (`WidgetScale.Steps`) and ticks the current one; a scale between them (an imported or hand-edited one)
  between them is listed in its place (`WidgetScale.Offered`), so the menu always shows what the
  widget has. A pick sets every selected widget (`QuickSet.SetScale`) in one `Mutate`, one undo step.
- **[S-6532]** The scale a card's content is drawn at (`WidgetScale.Effective`) is the display's
  scale ([S-850]) × the app-wide widget scale (`Design.UiScale`) × the widget's own, each factor
  that is not a positive number read as 1. It is the one input to the card's content transform:
  the content is laid out at cell ÷ effective scale, so the size ladder ([S-660]) classifies and
  discloses on the scaled box — a widget at 150 % shows what a smaller card would ([S-663]).
- **[S-6533]** Load validation repairs a stored scale outside 50–200 % or off the 5 % grain to the
  nearest one it may hold, and says so; a file without one reads as 100 %.
- **[S-6534]** *(Retired by [S-6536]: the settings page has no Scale row; the panel is the scale's one place.)*
- **[S-6535]** Debug: `--widget-png <Kind> <out.png> --scale <f>` draws the cards at that widget
  scale, through the same content transform.
- **[S-6536]** The properties panel is the **one place** for what it sets (user request
  2026-10-16): no widget settings page has a Title, Style, Header, Background, Item background, Item
  text or Scale row (`WidgetPageTests` checks the page builder's source for the panel's row names and
  for a style or scale writer). The page holds only the widget's own options, so settings search
  never finds those rows for a widget; the app-wide Widget scale (Appearance, Displays) is not the
  widget's own and stays. The page's signature ([S-1721]) leaves the scale out, so a pick on the
  panel does not rebuild an open page; a style pick still rebuilds it when the page's shape changes
  ([S-6071]). Settings export and import carry all of them as before: they are the instance's.
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
- **[S-6582]** Without a choice each widget sits where it always drew: centred — the Clock, the
  Moon's disc, Weather now's Large icon and Minimal styles, Air quality's Gauge; from the start —
  everything else (Weather now Standard, Air quality Number, the rate, the price, the Countdown, the
  Quote). The Countdown no longer centres itself on a Compact tile: one rule at every size.
- **[S-6583]** The card hands its view the alignment it draws (`ContentAlignment.Shown`: the own
  choice, else the kind's default for its style, and the default for a value outside the set or a
  style that does not align) before the view attaches and before every config it hears. In the
  view: the clock's column; the weather's and air quality's reading (the shared hero readout,
  [S-667]: its value row and the lines under it together; lines beside the value still read from
  it), the readings under it keep their grid; the large-icon and minimal reading and the gauge; the
  rate and the price; the countdown's lines, ring and cards; the quote's mark, lines and author; the
  moon's disc and name over its rows (beside the disc on a strip the text still reads from it).
- **[S-6584]** The properties panel's **Alignment** row, after Header: three icon buttons across the
  control column (Segoe AlignLeft, AlignCenter, AlignRight; the word as tooltip and name), the
  current one lit with the accent, none for a mixed selection. A press sets every selected widget
  that aligns in its style (`QuickSet.SetAlign`; the others keep theirs) in one `Mutate`, one undo
  step. Where none does, the row is greyed as the item rows are ([S-6571]), its tooltip "This widget
  has nothing to align", or "Not available in this style" when the kind aligns in another style. Not
  on any settings page ([S-6536]): the clock's page lost its Alignment row.
- **[S-6585]** Load validation drops an alignment outside the set, or one on a kind that never
  aligns, and says so; one on a style that does not align is kept for the style that does.
- **[S-6586]** Debug: `--widget-png <Kind> <out.png> --align left|center|right` draws the cards with
  that alignment; `--editbar-png` also writes `props-<theme>-align.png` (a clock aligned right),
  `-alignmany.png` (a clock and a quote of different alignments: none lit) and `-alignstyle.png`
  (the moon's month: the row greyed).
- **[S-3713]** Debug: `--editbar-png <outDir> [--theme light|dark]` also writes the panel beside
  real cards in edit mode on a stand-in display: `props-<theme>-right.png` (one flip clock in the
  middle with a title, an item colour of its own and a scale of 125 %: Style with the flip icon,
  Item background "Custom, 80%", Scale and More settings), `-left.png` (a widget at the right edge of the display),
  `-below.png` (a widget spanning the width), `-many.png` (three of mixed headers and scales,
  "3 selected"), `-toggle.png` (a Wi-Fi toggle: no Header choice), `-hidden.png` (header hidden,
  beside the same kind with a header), [S-6569] `-fills.png` (flip clocks with the background
  Default, Transparent and a colour at 50 %, and item backgrounds to match), `-noitems.png` (a
  stopwatch: Item background and Item text greyed) and `-picker.png` (the Background control open
  beside the panel: Default, Transparent, the colours, the opacity slider), all over a busy stand-in
  wallpaper so the panel's surface is judged against one ([S-3715]). The render cannot show the
  drop shadow (a composition visual is not in a `RenderTargetBitmap`).

Needs a live pass: typing a title on the desktop (focus, IME, Enter / Escape / a click on another
card), a pick from the Header and Background menus on a multi-selection, and undo of each; the strip
following a selection change, a drag and a resize (away, then back where they landed), at a top
taskbar, by the edit bar, and on a second display at another DPI; the panel's drop shadow on the
transparent surface over the real wallpaper ([S-3715]), and an opacity dragged in the fill control
landing as one undo step ([S-6567]).

## Widget styles (STYLES)

A different look of the same widget with the same data and the same settings, chosen next to the
widget on the properties panel ([S-6007], its one place, [S-6536]). Owning files:
`Themia.Core/Widgets/WidgetStyles.cs` (the registry), each styled kind's config (its style enum),
`Themia.App/Shell/PropertiesStrip.cs`, `Themia.App/Shell/Config/ConfigPages.Widget.cs`. Tests:
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
- **[S-6009]** The panel ends with a **More settings** row (the gear and the words, the panel's
  width; its tooltip "This widget's own options, in the settings window") that opens the widget's
  settings page — "More", since the panel holds the rest of the widget's settings ([S-6536]); one widget only, and only when its page has rows in its current
  style and state ([S-1750]): a style picked on the panel can bring the row or take it away. Several selected: no gear (which one's page would it be).
- **[S-6060]** One progress ring for every widget that draws one (`RingPart`, its geometry
  `RingArc`): a track in the control's strong rest fill, the arc in a brush the widget gives,
  clockwise from twelve, round caps, nothing under 0.05 % and the whole ring from 99.95 % (two half
  arcs: an arc back to its own start draws nothing); the stroke a twentieth of the diameter, 3 to
  12 DIPs. Not the meters' half-circle gauge.
- **[S-6061]** One reflow rule for every dot grid (`DotGrid.Fit`: year progress's days, the moon's
  month): every column count is tried and the one whose square cells are largest kept, on a tie
  the one with fewer empty cells in the last row; the gap is a quarter of the pitch, so a larger
  tile spaces the dots in proportion.
- **[S-6010]** *(Retired by [S-6536]: the settings page has no Style row; the style is picked on the
  properties panel only. A widget page's shape is still its style, so a pick there rebuilds an open
  page whose rows differ by style, [S-6071].)*
- **[S-6070]** A settings page offers only the rows that change what its widget visibly does or
  shows in its current style and state (user rule). The rule is one Core property per config,
  `IShownSettings<TSetting>.ShownSettings` (the kind's own `<Kind>Setting` enum, page order), read on
  the page through `Offers(setting)`, never a check written in a page; a row every style and state
  uses is not listed and always offered. A row left out is not built at all (no collapsed rows), so
  search cannot find it. The values of rows left out are kept for when they apply again. Size is
  not a state here: a row a small tile sheds ([S-660]) is still offered. A rule never reads a
  slider's or a text box's value, so dragging or typing never rebuilds the page under the hand.
- **[S-6071]** A widget page's shape (`SettingRules.Shape`, read by `SettingsPageSync.Shape`) is its
  style (picked on the properties panel, [S-6536]) and the rows offered. A pick or a switch that changes it
  builds the page again once its own event has finished (`QueueRestyle`), with the scroll kept,
  the expanders that were open opened again and the focus back on the row that had it.
- **[S-6072]** *(Retired by [S-6536]: with no Style row on the page there is nothing to leave out;
  `IShownSettings.OffersStyle` is gone.)*
- **[S-6073]** The settings search indexes one page per kind **and page shape** in use, built over a
  copy of a widget of that shape (`ConfigPages.BuildWidgetForIndex`), so a row one widget's style or
  state leaves out is not found for it.
- **[S-6074]** The rows each kind offers only in some styles or states: Mail: Subject/Account in List
  and Compact, Preview lines in List, the columns in Table. Folder: [S-4121]. Calendar: [S-3641].
  Clock: Seconds in Digital, Analog, Stacked and Flip; Date, Label and date size, Label and Offset
  in all but Minimal and Date (there the zone is a plain row, not an expander). Quote: Reshuffle
  with Shuffle on. Gallery: a folder's rows (folder, subfolders, pictures, shuffle, Explorer) for the
  folder source, the slide time for its Slideshow, Picture size for Slideshow and Grid (Scatter and
  Mosaic always crop), for a picture file without an outline and for the photo of the day; a
  picture file's rows (image, rotation, outline) for that source, the outline's colour and width
  with the outline on. Shortcuts: tile size, icons only and stretch in Grid, row size in List and
  Details, file extensions except on a grid of icons alone. Meters (CPU, memory, GPU, disk, network,
  battery, ping): Chart span (and its History heading) in the graph style only, each meter's first
  ([S-6300]). Current weather: Show detail in Standard. Stocks: Closed indicator for a listed symbol
  (not crypto), Extended hours for a listed symbol over one or five days (`WantsPrePost`), high and
  low markers in Chart. Sun: Events (and its Sky heading) not in Times, which is the events. Moon:
  Longitude in Disc only. Week planner: "Tasks from" while today's tasks show. Tasks: Show due date,
  Due and Important only for an account's tasks. Habits: Interval under the custom period, streaks
  and history in Dots. Pomodoro and Alarm: Volume while a sound is chosen. Contacts: the columns in
  Details. Screen time: the daily limit on the chart and the donut, the per-app limits on the app
  list, its Daily average over a week or a month. Debug: `--settings-png Widget:<Kind> --config
  '{"field":value}'` writes those fields over the demo widget's config (through the state file's
  parse and repair) to render a state.

Needs a live pass: picking a style on the strip for one widget and for several of one kind, the
undo of it, the gear opening the right page, and the flip turning over on the desktop with Windows'
animation effects on and off.

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
`Widgets/RotatedBox.cs`, `Widgets/ShownWatch.cs`, `Themia.Sdk.WinUI/Widgets/IWidgetContextActions.cs`,
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

- **[S-500]** A closed set of providers, the provider modules compiled in ([S-6910]): Microsoft,
  Google, IMAP, CalDAV, CardDAV (`HostProviders`, declared in Core). A provider this build does not
  know is dropped on load and reported, never carried as a string ([S-1901]).
- **[S-501]** Providers are written under lower-case names (as the original wrote them), so the
  file stays readable and a hand edit is easy to validate. The name is the provider's module id
  (`microsoft`, `google`, `imap`, `caldav`, `carddav`); it is read forgiving case and surrounding
  space, as it always was.
- **[S-502]** What an account feeds (a `FeatureSet` of `FeatureId`s: `mail`, `calendar`, `contacts`,
  `tasks`, `HostFeatures`) and how it proves itself (`AccountAuth`) follow from the provider's module
  alone. Widgets ask for a feature, never a provider name.
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
  `THEMIA_GOOGLE_CLIENT_SECRET` (a generated constant, validated to look like a secret); without it,
  Google sign-in refuses before the browser opens and token refresh fails with the same sentence.
- **[S-539]** Settings → Accounts lists each account as a group ([S-3821]: provider icon, name, address
  and provider, or that it needs signing in again), renames in a flyout, signs out after a confirmation, and adds through one flow at a time drawn in place:
  Microsoft and Google waiting on the browser ([S-1783]); forms for IMAP, CalDAV and CardDAV. Errors
  appear on the page; leaving it cancels a flow.
- **[S-3820]** What an account has on this PC is counted from the shelf a sync service just loaded or
  saved (`SyncStats.Of`: mail messages over folders, calendar events over windows (birthdays aside),
  contacts, tasks over lists; the newest successful read; the queued changes), never by opening a
  file for it.
- **[S-3821]** Each account is a group on the Accounts page: header name, then address and provider
  (or "Needs you to sign in again"); inside, a row per kind its provider feeds, in the order mail,
  calendar, contacts, tasks (`SyncStats.Summarize`), with the count on the right and "in 3 folders ·
  synced 5 min. ago · 2 changes waiting to sync" under it, or "Nothing stored on this PC yet"; then
  Nickname (Rename) and Sign out (danger). Open groups stay open across the list's rebuilds.
- **[S-3822]** An account that needs signing in starts its group with Sign in again (the problem as
  the description): Microsoft and Google run the browser flow, IMAP and DAV open their form.
- **[S-3823]** `SyncHost.Ledger` holds every shelf's latest numbers this run (`SyncLedger`, per owner
  and kind, thread-safe); `SyncHost.Report` raises `StatsChanged` on the UI thread when they change,
  and the page rebuilds its list while on screen. A sign-out purges the shelves and forgets the numbers.
  A kind whose service has not loaded its shelf this run (no widget reads it) shows nothing yet.
- **[S-3824]** Debug render: `--settings-png Accounts --demo-accounts` draws two stand-in accounts
  (one healthy with numbers, one needing a sign-in) from the render host's own ledger, groups open.
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
  history as a `SparkBand` edge to edge at the bottom of the tile ([S-1824]). The adapter names the card, without vendor and
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
  name (the n-th of identical cards), never guessed among several. Loaded once per sampler session;
  any failure leaves the counters' figure.

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
  level, muted only when all are, active when any is, and a session-count pill. Off: one row per
  session, keyed by its instance identifier.
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

- **[S-450]** The widget draws the app's one tracker ([S-458]) through `AppServices.ScreenTime`; it
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
  [S-1211]/[S-1212] (`MaxApps` is a ceiling over what fits, `ShowList` hides the legend). Each app's colour is a stable hash of its key; a legend row
  carries the icon (or an initial chip in that colour), the name with the colour dot right after it,
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
- **[S-454]** Day navigation in the header (back, label, forward, and a today button when away from
  today): today plus 89 days back. A pinned date stays that date across midnight, and returning to
  today unpins. A window shows "Last 7 days" or "Last 30 days" instead. Reading a day includes the
  interval still open, unless paused or older than a suspend leaves it; a range has one entry per
  calendar day, oldest first, gap days as zeros, clamped to retention. Executable paths are kept per
  app so past days have icons; at start, apps in the history with no path or name are filled from
  running processes.
- **[S-455]** *(The list moved out of the config into the shared preferences, [S-3322].)*
  Right-click "Hide from list" adds an app key to `HiddenApps`; hidden apps leave every
  view and the totals re-base. Settings list them with a way back.
- **[S-456]** A window of Bars: one bar per day, stacked by app in the legend's order, scaled to the
  busiest day, weekday labels for a week. Heatmap: seven columns, padded so the window ends on the
  last cell, shaded in quartiles of the busiest day (any use is at least level 1), today outlined.
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
side by side and they light the same app together. Owning files: Core
`Model/Configs/ScreenTimeConfigs.cs`, `Widgets/{ScreenTimeRules,ScreenTimeLayout,ScreenTimePreferences}.cs`,
`Storage/ScreenTimeStore.cs`; App `Widgets/ScreenTime{Chart,Donut,Apps}View*.cs`,
`Widgets/ScreenTimeShared.cs` (data, links, look, day navigation), `Widgets/ScreenTimePreview.cs`
(debug fixture), `Services/ScreenTimeService.cs`, `Shell/Config/WidgetSettings.SystemWidgets.cs`.
Tests `ScreenTimeRulesTests`, `ScreenTimeLayoutTests`, `ScreenTimeStoreTests` (`S33xx_`).

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
  export. Every screen-time settings page shows them (with a note that they apply to all) and
  writes them through `IScreenTimeSource.SetPreferences`, which replaces the whole set and redraws
  every widget. On load, blank keys and limits of zero or less are dropped; preferences changed
  before the file was read win the merge. Right-click "Hide from list" works in all three, never
  on Other.

### Chart

- **[S-3330]** `ScreenTimeChartConfig`: `Period` Week (default) or Month (a day reads as the week)
  and `Style` Bars (default) or Heatmap (shown as "Calendar"; unknown reads as Bars).
- **[S-3331]** Bars: one bar per day, oldest left, stacked by app in the ring's order (the busiest
  seven, `ScreenTimeRules.DrawingApps`, then Other), scaled to the busiest day, today's weekday in the
  accent. Heatmap: [S-456]'s calendar.
- **[S-3332]** Ladder (`ScreenTimeLayout.Chart`): the drawing (never dropped, at least 32), then the
  figure over it (Subtitle from Expanded height at Regular width, else BodyStrong), the weekday row
  under a week's bars (when each column has 16 DIPs), the detail line; the drawing takes the rest.
  The figure is the window's total and the line its per-day average and the "of 4h/day" limit
  ([S-457]); a hovered day (pointer, or Left / Right) shows that day.
- **[S-3333]** Linked focus ([S-3310]): the focused app's part of every bar is lit and every other
  part dimmed; the calendar shades by that app's seconds per day; the figure is that app's time over
  the window (or on the hovered day) with its share. Hovering a part hovers its app; clicking it
  selects (again: lets go); clicking empty space clears the selection.
- **[S-3334]** Keys: Up/Down, PgUp/PgDn, Home/End step through the apps in the ring's order (the
  linked cursor, [S-3307]); Enter/Space select; Left / Right step the day the figure speaks of; Menu
  hides; Escape lets the cursor and the day go.

### Donut

- **[S-3340]** `ScreenTimeDonutConfig`: `Period` only. One ring, the total in its hole.
- **[S-3341]** The wedges: the busiest seven apps and one Other, from cumulative seconds ([S-452]).
  No legend: the list is its own widget ([S-666]); a wedge's tooltip names it.
- **[S-3342]** Ladder (`ScreenTimeLayout.Donut`): the ring as large as the tile allows (at least 56,
  the 16 DIP inset at the sides), a seventh of it the stroke; the hole's figure is [S-1210]'s fit,
  and its second line (the day's "of 4h", a window's "2h 10m/day", or the focused app's share)
  appears once the hole is 72 DIPs wide.
- **[S-3343]** Linked focus: the focused wedge is lifted (its stroke 1.3 times as thick), the others
  dimmed, and the hole names it ("Code · 41%"). An app folded into Other lights Other
  (`ScreenTimeRules.FocusPart`); a hidden one lights nothing. Click selects, empty space clears.
- **[S-3344]** Keys: Up/Down, PgUp/PgDn, Home/End step through the wedges clockwise from twelve;
  Enter/Space select; Left / Right step the day (a day period); Menu hides; Escape lets the cursor go.

### Apps

- **[S-3350]** `ScreenTimeAppsConfig`: `Period`, `MaxApps` ([S-1211]), `ShowAverage` (on),
  `ShowPercent` (off). A row: icon (or initial chip in the app's colour), name, the average and the
  share when shown, the time last; over its own limit the time is critical, and the tooltip says the limit.
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
- **[S-3352]** Ladder (`ScreenTimeLayout.Apps`): whole compact rows; the time always, the name from
  160 DIPs wide (under it a row is the icon and the time, the name in its tooltip: a squeezed name
  read as one letter), the share from 200, a window's average from 300. No headline, no footer ([S-666]): the total is the donut's and the chart's.
- **[S-3353]** Linked focus: the focused row has the hover fill, the others dim; the selected row
  keeps Fluent's selection pill and fill; the keyboard cursor has the focus ring ([S-1288]). Hover a
  row hovers the app; click selects (again: lets go); the space under the rows clears.
- **[S-3354]** Keys: Up/Down, PgUp/PgDn, Home/End move the cursor down the list; Enter/Space select;
  Left / Right step the day (a day period); Menu hides; Escape lets the cursor go and keeps the
  selection (Escape also arrives when the lease ends).
- **[S-3355]** Debug renders: `--widget-png <ScreenTimeChart|ScreenTimeDonut|ScreenTimeApps>
  --state sample|hovered|selected` draw a fixed month of sample use (ten apps, quieter weekends, a
  6 h day limit, Chrome over its 1 h) through `SampleScreenTime`; `hovered` lights Code and
  `selected` selects Chrome through the render's one board, so every card on the page shows it.
  `packaged` lists the packaged apps running on the machine by their real executables, and
  Explorer, so their icons go through [S-3960]'s path.

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
- [S-644]: moved with the Pomodoro widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Pomodoro/`).
- [S-645]: moved with the Clock widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Clock/`).
- **[S-646]** A reading's caption (meters, power, weather's condition) moves under the reading when
  it would be trimmed beside it, or when its own line lets the reading be drawn larger.
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
  Commands in the header are not touched; their views shed them.
- **[S-656]** The current-weather reading is sized against the tile less its detail line, not
  against its own height (which pinned the first step chosen); the condition icon goes first on a
  tile too narrow for it.
- **[S-657]** The stock price is fitted to its own text (at least seven characters), not the widest
  price any symbol could have, and the change, currency and session badge go under it when they do
  not fit beside it. The badge is a grid so its label ends in an ellipsis.
- **[S-658]** A forecast row narrower than 160px is the day and its high; the icon and the low go.
- **[S-659]** **A readout that is the whole tile fills it** (`HeroFit`; user request 2026-10-17: the
  clock and the weather left a wide band between their content and the card's edge). The clock's
  time (Digital, Stacked, Minimal, and the Flip cards' digits) and the current weather's
  temperature (every style) are measured by their glyphs, not by 0.58em a character:
  `HeroFit.Ems` takes Segoe UI Variable's advances by class (digit 0.58, colon or stop 0.25, space
  0.28, "-" 0.41, "°" 0.39, capital 0.72, "M"/"W" 0.95, small letter 0.56, an unknown script 1 em,
  a combining mark 0), the widest over its weights and optical sizes. The clock's row is "88:88"
  (or "88:88:88") and, after the row's gap, the language's wider meridiem at its own step
  (`HeroFit.Row`: the largest time whose meridiem, the largest ramp step whose line is within 0.45
  of the time's size, Caption..Title, fits beside it); it was laid out for "12:34" plus three
  full-size letters and drew "10:42 AM" across 61 % of the M tile. Below TitleLarge the size is a
  ramp step; from TitleLarge it is continuous, the size at which the readout fills the width or a
  1.15 line (`HeroFit.Leading`, figures set solid: digits have no descenders) fills the height,
  with no ceiling. The temperature fills from TitleLarge the same way (`HeroLayout.Fill`), the glyph,
  the number, the unit and the condition standing beside it measured together; the condition goes
  under the reading when that draws it larger ([S-646]). The meridiem sits on the digits'
  baseline (`HeroFit.BaselineDrop`: a line's baseline stands 0.19 of it above its bottom). Only
  the rungs ([S-660]) and the card's inset take room from the fill; lists, charts and the other
  heroes keep their ladders. The widget's own scale ([S-6532]) lays the content out in the card
  less that scale, so a filled readout still fills its card at every scale: the scale magnifies
  the header, the date, the label and the inset round it, and so takes room from it at 150 % and
  gives room at 75 %. The word clock keeps its own rule ([S-6013]); the analog dial fills by its geometry.

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
  `AppStateStore.Validate`; for mail it clamps the preview lines (1–5) and keeps at least one row
  column. No hearth-era field is read ([S-1050]).
- **[S-541]** `accountIds` empty means every mail account, including ones added later (the classic
  widget showed "select an account" instead); a non-empty list reads those accounts that are still
  signed in. Settings lists the signed-in mail accounts as switches under "Every mail account" and
  links to the Accounts page; no account is ever signed in from the widget's page. Folders: Inbox,
  Sent, Drafts, Deleted, Junk [S-12].
- **[S-542]** List (sender + time, subject, preview), Compact (sender + time, subject) and Row (one
  line of user-ordered columns under a header row) views [S-14, S-37]; newest or oldest first over
  the same newest N [S-17]. The card title is the folder; the header carries the unread badge
  [S-02], a warning glyph naming an account that failed or needs signing in (tap → Accounts), and a
  busy ring only for a refresh the user asked for. The card menu offers Refresh, Write email
  ([S-558]) and the five folders [S-20]; the style ([S-6403]) and the order are settings only ([S-1746]).
- **[S-543]** List fields Subject / Account, default both on (the preview is the List style's own
  line, [S-6403]); the account tag shows whenever chosen [S-43]. Table columns From, Subject, Date,
  Preview, Account, reorderable, at least one on [S-16].
- **[S-6403]** Mail's styles ([S-6000]), `EmailConfig.Style` (`EmailStyle`, `"style": "list"`):
  **List with preview** (sender + time, subject, `PreviewLines` lines of preview), **Compact list**
  (sender + time, subject) and **Table** (one line of the user's columns under a header row), in
  that order: what were the view setting and the list fields' Preview switch (List without the
  preview was Compact, so the switch is gone). The rest stay settings, shown only for the style that
  draws with them (`EmailConfig.ShownSettings`, [S-6070]): Subject / Account for List and Compact, Preview
  lines for List, the columns for Table. A hand-edited `"preview"` list field is dropped.
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
  under 240px), the time 110px, the header's warning glyph 160px; the row view keeps the most
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
  a refusal from the shell is the header warning.
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
- **[S-601]** A title wraps to "Title lines" (1–6, default 2; two at most on a compact row) and is
  then cut with an ellipsis; the tooltip carries the whole title, its state, due date and priority.
- **[S-602]** Style ([S-6400]): List puts the due date under the title, Compact beside it (under it anyway in a
  column under 240px wide). Rows are Fluent's fixed heights at minimum and grow only for a wrapped
  title; an important task's title is semibold. The size ladder is [S-780].
- **[S-6400]** Tasks' styles ([S-6000]), `TasksConfig.Style` (`TasksStyle`, `"style": "list"`):
  **List** and **Compact** ([S-602]), what was its Layout setting; no other look option exists.
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
  a Tasks config's strings, an unknown source, layout or due filter, a text filter over 100
  characters, and a broken row order are repaired on load.
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
- **[S-561]** Config, one for every calendar style ([S-3601], [S-3641]): `accountIds`, `calendars`,
  `calendarColors`, `showBirthdays` (on); the week style adds `days` and `firstDay` ([S-3637]), the agenda
  `agendaCount` (1–100, 20) and `agendaDays` ([S-3636]). No `layout`: the style is the layout
  ([S-3640]). The context menu offers Refresh.
- **[S-562]** An empty `accountIds` means every calendar account (later ones too — the Mail
  pattern); settings offer "Every calendar account" or a choice where the last cannot be turned off.
  Chosen accounts that were signed out are skipped; when none are left the widget says so with a
  link to Settings › Accounts.
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
- **[S-567]** Several calendars per account: settings list each account's calendars (Graph
  `/me/calendars`, Google `calendarList`, CalDAV home-set listing) with a colour (Automatic or one of
  the six) and, when there is more than one, a switch. No selection means the default calendar;
  deselecting the last one goes back to it. A Google sign-in whose scope cannot list calendars
  (403) is offered its primary calendar.
- **[S-568]** Ranges: month = whole weeks around the month; week = its days ([S-2719], a whole week
  by default); day; year = 1 Jan – 31 Dec; agenda = its date range from today ([S-3636], 30 days
  with today by default). Requested from local midnight to local midnight (the classic app used UTC
  midnight); what decides them beside the kind and the anchor is one `CalendarPeriod` (week start,
  week days and first day, agenda days). The header title is the period ("October 2026",
  "28 Sep – 4 Oct", "Sun 4 Oct", "2026"; a one-day week reads like a day); the agenda's is the
  widget's name.
- **[S-569]** Back / forward step a month (day clamped), the week's period ([S-2719]), day or year (29 Feb clamped); the
  agenda pages its rows ([S-2709]). A today button shows when today is off screen (the agenda: past
  its first page). The widget follows today across midnight until the user pages away.
- **[S-570]** A day lists birthdays, then all-day events, then timed ones by start; an all-day
  event covers [first, end); a timed one shows on every local day it overlaps.
- **[S-571]** Agenda: grouped by day ("Today", "Tomorrow", "Mon 5 Oct"; [S-2716]), at most `agendaCount`
  entries within its date range ([S-3636]); an event spanning several days is listed under each day it covers (classic #155), one
  still running from before from today on; timed events already over are left out. A timed event's
  row shows that day's part of it (18:00–00:00, "All day" for a day it covers whole, 00:00–10:00). Rows: time column (start over end, "All day", "Now"), title, location · organizer; on a
  tile under 260 px the time folds under the title. The time column is as wide as its widest label
  in the current language (at least 52 px, at most 35% of the row; a longer word label trims), so
  "Toute la journée" or "طوال اليوم" is not cut.
- **[S-572]** Month cells: chips (title, coloured edge) as many as fit, the overflow as "+N" beside
  the day number; on smaller cells up to three dots (two in narrow cells) plus a muted one; on the
  smallest the number alone, strong when the day has something. Today is an accent disc.
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
  Clicking selects (an accent pill), clicking the background clears.
- **[S-581]** Layouts: list (avatar, name, role, address/number), compact (small avatar, name, one
  field), details (table), grid (avatar cards). Details narrower than 88 px a column fall back to the
  list, a grid under two cards to compact. Under 160 px wide the avatars and the count are dropped.
- **[S-582]** Details columns stay in canonical order (Name, Email, Phone, Company, Title); the last
  one cannot be turned off.
- **[S-583]** `count` is at least 5 (steps of 5, no upper bound); each account is asked for
  clamp(count, 500, 1000) so a search spans the address book; search filters before the cap.
- **[S-584]** A search box (from 150 px tall) asks for the keyboard while focused (Esc clears). It
  starts empty: the `defaultSearch` setting is gone ([S-3796]).
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
- **[S-667]** **The hero readout** (`HeroReadout`, metrics in `HeroLayout`; Weather now, Stocks,
  Air quality, Exchange rates, the system meters and the GPU, the detail widgets, [S-3860]): the one big value
  with its unit, an optional token on its baseline, an optional leading glyph and caption lines.
  Value, unit and token are runs of one text block, so they share a baseline (they were three
  bottom-aligned blocks of different line heights, a few pixels apart). The unit is the largest ramp
  step at most 45% of the value's size (Caption under a Subtitle value, Title beside Display),
  regular, secondary, after the number ("14" then a smaller "°C"; "420.27" then " USD"); the token is Body, BodyLarge from a Title value; the
  glyph is 0.8 of the value's size, at least 24, and 12 from it, and is the first part shed; past
  Display (a filled reading, [S-659]) the unit grows with the value from Title, and the glyph has no ceiling. Caption
  lines stand beside the value (12 from it, centred on it) where the longest fits across and their
  height is within the value's line, else under it, starting at the readout's left edge.
- **[S-668]** **Stat line and stat cell** (`WidgetParts.StatLine` / `StatCell`, sizes in
  `PartMetrics`): a reading as one 28px line (16px glyph, Body label secondary, Body value primary
  at the end) or as a 36px cell (12px glyph and Caption label over a Body Strong value, no box). The
  weather readings use only these two, beside or under the reading (they were three shapes with
  three glyph sizes).
- **[S-669]** **Day row** parts: the weekday column 44 wide (with the glyph), the sky's glyph 20 with 8 either side, the low and high
  in a right-aligned tabular figure column of at least 32 (`WidgetParts.Figure`): the high Body
  Strong primary, the low Body secondary, as Windows sets a range. The forecast grows them with the
  room a day has (`DayScale`, levels 0..3, one ramp step each: high Body Strong / BodyLarge /
  Subtitle / Title, glyph 20 / 24 / 32 / 40, range bar 6 / 6 / 8 / 8 thick; it was 4 and read as a
  scratch), the same ladder in rows, columns and the grid.
- **[S-3860]** **One hero ladder for chart and reading widgets.** While a hero shares its tile with
  the content it heads (a chart, readings, a list) it grows to the tier's step, by the tile's
  smaller size class (`HeroLayout.TierStep`): Title up to Regular, TitleLarge at Expanded, Display
  at Huge, fitted down where the width is short. Stocks ([S-1311]), the meters and the GPU
  ([S-1762]) and the detail widgets ([S-2402]) use it, so at the seven tiers a CPU meter and a
  stock draw their number at 28 / 28 / 28 / 28 / 28 / 40 / 68. A hero alone on its tile (a meter
  with the history off, a detail widget whose every fact is shown and has no list, the weather)
  fills it: Display, then grown past it up to 1.5×. The readout stands 16 from the card's sides
  and 4 under the header (`HeroLayout.SideInset` / `TopInset`), centred instead where it is alone
  or beside a chart. A formatted reading is split for the readout by `HeroLayout.Split`: the
  leading figure (digits of any script, `.` `,` and the Arabic separators) and the unit after it
  ("42" + "%", "1.2" + " MB/s"); a text that does not start with a figure and a unit ("--", "%42",
  a direction mark, "1 234 ms") is drawn whole, never reordered. The fit measures a worst-case
  reading per run as it is drawn (`HeroBound`: the number at the step, the unit at the unit's
  step), never all of it at the number's size: counted that way the disk's "1.2 MB/s" fell to
  Subtitle where every other meter stood at Title.
- **[S-3861]** **Detail widgets never end in an empty band** (user decision 2026-10-12, one rule
  for all seven): what the ladder's rungs leave goes first to the hero (to the tier's step; past
  it to Display and 1.5× only when every fact is shown and there is no list), a list is as tall as
  its rows, and
  what is still spare is split above and below the block, so a short adapter list in an L tile
  stands with its hero and facts in the middle rather than under them at the top. A list that
  overflows fills the room and scrolls (no spare). A widget without a hero is its list from the top.
- **[S-3862]** A detail widget's facts are drawn only as the shared stat parts ([S-668], one
  implementation, `StatPart`, restyled in place each read): stat cells (Caption label over Body
  Strong value) when every fact fits in two or more columns of cells, else stat lines (28 each,
  no glyph), as many whole rows as fit. The Title-valued "large tile" and the Body Large tile are
  retired with the 2.4× hero.
- **[S-3850]** **The add footer** (every widget that adds an item in place: Habits [S-1427], Tasks
  [S-780]). One full-width list row, built only by `AddFooterRow`: a plus glyph and the secondary
  Body line ("Add a habit", "Add a task"), the list's own row height (compact with the rows) and
  side inset, the row hover; a tap swaps the line for the widget's text box (Enter adds and keeps it
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
  ([S-3200]), and (user requirement) the sun and the moon ([S-3100]) and the drive card ([S-4675]).
  The disk meter starts with its header like the other meters (user requirement), so its reading
  sits where theirs does ([S-1161]). Only creation reads the default; a loaded widget
  keeps the placement it was saved with.
- **[S-701]** The app pages of the settings window come from one table, `ConfigPages.AppPages`
  (page, title, glyph, main or footer slot, builder). The navigation and the body both read it, so a
  page is added with one line plus its own `ConfigPages.<Page>.cs`. A reserved `AppPage` value with
  no table entry is not in the menu and, if asked for, logs a
  warning and shows an empty page. About stays the last footer entry ([S-493]).
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

- **[S-760]** `Source` is a closed set: `Folder` (default), `File`, `Bing`. Folder uses `Path` +
  `Recursive`; File uses `FilePath`; Bing needs no setup. The settings page shows only the rows the
  chosen source (and, for the slide time and picture size, the layout) uses.
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
- **[S-1992]** Slideshow keys (`ControlKeys.Slideshow`) beyond [S-811]: Home / End jump to the first
  and last picture, Enter opens the shown one (local files only, as a double-click does). The other
  layouts take no keys, and a click on them no longer takes the keyboard (`WantsKeys`).
- **[S-769]** The hover bar (previous / pause-resume / next) shows only while the pointer is over
  the widget, in the slideshow with more than one picture. Pause is not persisted.
- **[S-770]** The slideshow's own timer runs only while it is a folder slideshow with more than one
  picture and not paused.
- **[S-771]** Double-click a picture opens it; right-click a picture: Open, Show in Explorer,
  Pause / Resume, Refresh images (no layout switch, [S-1746]).
- **[S-772]** *(Retired by [S-3792]: no `RefreshMinutes`; every gallery re-reads its source hourly.)*
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
  description and empties the box. No Change mode; clearing is [S-2848]. No "use my location": the
  app has no location source, and there is no app-wide default place to fall back to.
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
  list (`<out>-place.png`) against a fixture geocoder; nothing touches the network. `--place-cleared`
  (also on `--widget-png`) clears the demo widget's place ([S-2848]): a Sun or Moon starts at Zurich.
- **[S-2848]** Clear (user requirement: a place can be cleared, not only overwritten). Every widget
  about a place implements `IPlaced` (`HasLocation`, `ClearLocation`): current conditions, forecast,
  air quality, rain, Sun and Moon. While a place is chosen the location row shows a clear button
  (`Glyphs.Dismiss`, tooltip and name "Clear location") left of the search box; it forgets the
  coordinates and the name (the location-name switch stays), saves, says "None chosen yet…", hides
  itself and focuses the search. A cleared place is no place for every kind alike: the widget shows
  its Set up button ([S-1860]) with its view parked ([S-1881]), asks the sync for nothing, and a later
  pick starts a fresh view, so nothing fetched for the old place is drawn. No kind falls back to a
  default. Sun and Moon still start at Zurich; cleared, their coordinates are stored as `null` (always
  written, so they never read back as Zurich), their exact fields are empty (leaving an empty one is
  not refused), and a place typed into the exact fields counts as chosen (named by its coordinates
  when it has no name).
- **[S-733]** Reading glyphs, checked against the font: droplet EB42, thermometer E9CA, wind E7BC;
  layouts: list E8FD, columns E8C0, grid E8A9.

Not ported: wind in mph for °F (the original had none either); a gradient fill on the range bar
(solid accent, as every other bar in the app); the refresh stepper (fixed choices app-wide).

## Tasks: using the whole tile, and the rest of the original

- **[S-780]** Tasks ladder (`TaskLayout.Plan`, from the card's `WidgetArea`): one task always; the add
  footer ([S-3850]) from Regular height, and at any size under an empty list (never while a filter
  narrows the list, [S-611]); the list takes the rest (no summary strip, [S-783]). The header's list
  picker ([S-614]) from Regular width.
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
  the pointer nears an edge; letting go commits once through the same write as Alt+Shift+Up / Down; a lost
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
- **[S-1722]** The app pages: General, Appearance, Accounts, Language & region ([S-3797]), Displays,
  Views and Widgets in the main slot ([S-1830]); License and About in the footer. There is no Layout page: its two rows (gap between widgets, snap to grid) are the
  Layout section at the end of Appearance's look, before the motion rows. General has no Data
  section (it held only "open the data folder") and no Developer section.
- **[S-1723]** Every font setting picks through one control, `FontChoice`: a drop-down button whose
  flyout (`FontPicker`) is a search box over a virtualised list of the presets, then the installed
  families ([S-827]), each entry drawn in the font it names (a preset in the family it maps to,
  [S-826]; the system entry in the UI's own), the current one ticked and scrolled into view. The
  closed button shows the name in the UI font. Beside it, "Add fonts" ([S-6443]).
- **[S-6440]** The list (`FontSearch.Options`): the presets, then the installed families in order; a
  set family that is not installed (removed since, or the list not read yet) right after the presets;
  nothing listed twice; an implausible name adds nothing. A symbol or icon family (every GDI report
  is the symbol charset or lacks Basic Latin: Wingdings, Segoe Fluent Icons) is named in the UI font.
- **[S-6441]** The ticked entry is the one the setting names (case-insensitively), else the system font.
- **[S-6442]** Search (`FontSearch.Filter`): case- and accent-insensitive, the query anywhere in the
  name (also with spaces left out: "segoeui"); names starting with it first, then a word starting
  with it, then the rest, each in list order. The system entry stays on top whatever is typed. Keys:
  the caret is in the box when the flyout opens; typing on the list goes to the box; Down enters the
  list, Up from its first row returns; Enter in the box takes the best match (`FontSearch.Best`: the
  system entry only if its own name matched; nothing for a blank query or no match, which says so),
  Enter on a row takes it; Escape closes.
- **[S-6443]** "Add fonts" opens Windows Settings › Personalization › Fonts (`ms-settings:fonts`,
  through `ShellLauncher.OpenWindowsSettings`). `WM_FONTCHANGE` on the command window drops the
  cached family list: an open picker re-reads it and keeps its query, a later one reads the new set.
- **[S-1724]** No developer mode and nothing behind one: no `DevMode` or `VerboseLogging`
  preference, no Logs page, no developer banner, no `--dev` render switch, and no Web element
  widget (the original's pre-release widget behind developer mode: its view, picker window,
  off-screen WebView2 renderer, saved renders, settings, keys and fixture are gone). The debug-only
  render flags (`--settings-png`, `--picker-png`, ...) are build tooling and stay.
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
- **[S-1122]** A meter's settings page ends with the shared rows: Show history, Chart span (no Refresh, no interval, [S-1120]). A styled meter has no Show history: its graph style is the history ([S-6267]).
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
- [S-1130], [S-6315], [S-6316]: moved with the Memory widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Memory/`).
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
  reading the shared hero readout ([S-667]: the number, its unit smaller; the battery's glyph as
  its icon) at the tier's step ([S-3860]) in what is left, and the history taking every pixel
  the reading does not — no empty space at L or XL. With Show history off the reading fills the
  tile, grown past Display as the weather's is. This is the **Graph** style; the other styles of a
  meter ([S-6300]) draw the same reading with another part beside it (a gauge, the cores, a
  used/free bar, the engines), chosen by the user, never added to the Graph style.
- **[S-1823]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-1824]** The history is the card's bottom band, edge to edge (user decision 2026-10-11): no
  inset at its left, right or under it; beside the reading on a strip it fills the rest of the
  width and the whole height, the reading column's own inset separating the two. `MeterFace.Plan`
  spends no inset on the history; only the reading keeps the content inset (16). The card's
  rounded corners clip the band. The trace is still kept half a stroke inside the chart's width
  (`Sparkline.PlotX`), so the newest sample's stroke is not cut in half at the right edge. Every
  meter and every GPU panel draws it this way.
- **[S-1825]** A window still filling stays right-aligned, as Task Manager draws it: the trace grows
  in from the right, one slot per sample, over a hairline baseline that spans the whole chart from
  the first frame, so a young meter reads as a chart with little history, not an empty hole. The
  shared ticker keeps whole-second cadences under a minute (`TickSchedule.Normalise`; it used to
  collapse them all to one second), so a meter set to 5 s samples every 5 s and its chart spans
  its configured span.
- **[S-1826]** **The history scrolls, it does not step** (user request 2026-10-16). Every `SparkBand`
  (the meters, Battery and Ping included, and every GPU panel) draws its line **one sample behind**:
  a new point is placed one sample step beyond the right edge and the traces are slid left by
  exactly that step over the series' period (1 s; a ping's 5 s, [S-2512]) by a linear composition
  animation on their container's `Translation`, so between points nothing is redrawn on the UI
  thread. The next point rebuilds the geometry once and resets the translation in the same frame;
  a point's x depends only on its distance from the newest (`SparkScroll.PlotX`), so the rebuilt
  line lies where the slid one ended. The band holds `SparkScroll.Lead` points beyond its span (the
  slot sliding out at the left, the newest beyond the right) and clips to its own bounds, so the
  edge-to-edge look and the half-stroke right inset ([S-1824]) are what shows once a slide ends.
  The reading is never delayed: only the line is a sample behind it. Not for charts of a fixed
  range (Stocks, Exchange rates, the forecast curve, Screen time) or the audio visualizer.
- **[S-1827]** Only a new point slides. A history loaded (a view switch, edit mode, a longer span),
  a clear, a span or scale change and a resize draw **settled** — the newest point on the right
  edge, still — which is where a finished slide stands, so nothing jumps when a meter is built
  again and the next point slides on from there. A paused series (a parked view, a slow probe)
  simply ends its slide settled and waits; a point that arrives before a slide ends replaces it.
- **[S-1828]** With Windows' **Animation effects** off ([S-1010]), the user's **Smooth value
  changes** off ([S-6329]) or the band not shown, a point draws settled: no lag, today's one step per
  point. Read at each point, so flipping either setting applies to the next one.
- **[S-1829]** A folded slot ([S-1121]) stands at the time of its last member and is **drawn only once
  that member has arrived**: a slot still taking points changes value with each one, and the segment
  into it would show that at the right edge. A folded chart therefore stands a further slot less one
  sample behind, so a slot that just finished starts a whole slot beyond the right edge and slides in
  over its points' periods, one sample step per point; nothing on screen changes value as points
  arrive. The traces sit in a `Canvas`, not a `Grid`: a Grid arranges its shapes at the band's size
  and clips them there in their own coordinates, so the point beyond the right edge was cut off,
  slid in as a gap, and the next point appeared at the edge. The band's own clip is the only edge. A rate chart's top is the drawn
  slots' peak, the newest beyond the edge included, so the scale is in place before that point
  slides into view. Debug renders draw a fixed point of the slide: `--widget-png <Kind> --state
  history --progress 0..1 [--span s]` (0 just after a point, 1 slid one step; the geometry of a
  point's arrival under the translation the slide has reached, so the still shows what is clipped).
- **[S-6323]** **Readouts glide, they do not jump** (user request 2026-10-16). Every live readout
  that moves with the hub's reads — a meter's number (every style, the faces' figures, the
  network's two numbers, a core's figure), the gauge's and the ring's sweep, a bar's fill (cores,
  memory, disk, GPU engines), the battery's fill, a core tile's shade and every one of their
  pressure colours — moves from what it shows to the new reading **linearly over one period of its
  series** (1 s; a ping's 5 s), the history's own motion ([S-1826]), so the number reaches a reading
  as its point reaches the chart's edge (the reading is thus up to one period behind, as the line
  is). Linear, not ease-out: readings come every period and an ease-out would rush and stall once a
  second. A reading arriving mid-glide glides on from where the readout stands (`Core/Motion/Glide`,
  App `Widgets/Glides.cs`: one helper for every readout).
- **[S-6324]** Only numbers glide: a reading appearing, vanishing or becoming unavailable, a label, a
  state (charging, the ping's signal bars, the absent battery) and the row texts ("Read 42 MB/s") are
  shown at once (`Glide.Plan`).
- **[S-6325]** A number counts in its own resolution (a whole percent; a whole millisecond; a rate in
  its formatted unit) and at most 20 steps a second however far it goes (`Glide.Quantum`), each step
  the value the line has reached rounded towards the target (`Glide.Shown`), written by the kind's own
  formatter and landing on the reading's own text. It is redrawn only when its next step is due
  (`Glide.NextChange`): one shared one-shot timer (`GlideClock`) armed for the earliest due step,
  idle once every readout shows its target; no frame callback, no repeating timer.
- **[S-6326]** With Windows' **Animation effects** off ([S-1010]) or **Smooth value changes** off
  ([S-6329]) every readout jumps as before. Read per glide (Windows asked at most once a second, the
  user's switch every time), so flipping either applies to the next reading.
- **[S-6327]** Colours glide through OKLab (`OkLab.Mix`): accent to caution passes even-lightness
  colours, not sRGB's muddy middle; the compositor plays four straight key-framed segments along that
  path (`ColorGlide`, a brush of the readout's own). Fills, shades and transforms (a bar's scale, a
  tile's opacity) are independent compositor animations (`DoubleGlide`); only numbers and arcs are
  stepped on the UI thread.
- **[S-6328]** Debug renders draw a fixed point of every glide: `--widget-png <Kind> --state sample
  --tween 0..1` draws the sample from a reading a third as large (items shifted by five) that far
  towards it.
- **[S-6329]** **Smooth value changes** (user request 2026-10-16), Settings › Appearance › Motion,
  a toggle, **on by default** (`DesignState.SmoothValues`): one switch for every smooth motion of a
  live value, the readouts' glides ([S-6323]..[S-6327]) **and** the history charts' scroll ([S-1826]),
  since the two are one motion (a number reaches a reading as its point reaches the edge) and one
  without the other would put them a period apart. Off, values, colours and charts jump to each
  reading, as with Windows' animation effects off. Both ask one rule (`Glide.Smooth`: Windows'
  animation effects and the switch). Not the clock's flip or the intros: those are a style's or a
  moment's motion, not a value's. Like the intros, not part of a design preset ([S-1014]). Debug
  stills (`--tween`, `--progress`) draw regardless.
- **[S-6300]** **Meter styles** ([S-6000]): a meter's style says what stands beside its reading
  (`MeterLook`: the history, a gauge, the kind's items as columns, rows or tiles); the reading is the
  same hero readout in every style. One shared set of parts draws them for every meter
  (`Widgets/MeterLookParts.cs`: `GaugeDial`, `BarList`, `TileGrid`, `MeterBrushes`; rules in Core
  `MeterLooks.cs`, tests `MeterStylesTests`), so a CPU gauge and a memory gauge of one size are one
  gauge. A kind's items come from the hub like its reading (`MeterPart.WatchItems`, [S-2500]): one
  read per tick for every widget, watched only while a style draws them and let go when another
  style is chosen ([S-6005]). Show history and Chart span stay the Graph style's settings. Debug:
  `--state sample` draws a fixed 62 % reading and the kind's fixed items in every style.
- **[S-6301]** An item's shade (`LoadTint`): the usage pressure colour ([S-1123]) at an opacity
  rising from 0.22 at idle to 1 at full load, so tiles read as a heat map in the theme's accent,
  caution and critical colours; an item with no reading is the disabled fill, never "idle".
- **[S-6302]** **Gauge**: a half-circle over the top, left end 0 %, right end 100 %, its track the
  progress track ([S-837]) and its arc the reading's share in the pressure colour, round-capped
  (`GaugeLayout.Fit`: the largest half-circle in the content box, stroke 12 % of the radius, 4–16;
  under a radius of 28 the tile is the reading alone). The reading sits in the bowl, its line on the
  gauge's bottom edge, at the largest step (Body to Display, then grown up to 1.5 × Display) whose
  widest reading fits the bowl's chord at the top of its glyphs. Room at L/XL is a larger gauge and a larger number, nothing else.
- **[S-6303]** **Columns** (`BarList` Columns): one vertical bar per item filling from the bottom,
  across the content width under the reading (beside it on a wide strip, the history's place,
  [S-1762]) and inside the content inset (bars are not a trace: [S-1824] is the history's), the
  track the progress track at the history wash's faintness so the loads lead. Gaps
  4, then 2, 1, 0 as the items crowd, a bar never narrower than twice its gap (`BarLayout.Columns`).
- **[S-6304]** **Rows** (`BarList` Rows): per item its label and value on a line, the bar under it
  filling from the left; every row at the largest label step (Caption, Body, BodyLarge) and the
  thickest bar (4 to 12, at most half the label's line) that let all rows fit, a step taken only
  when the widest line (label and value) fits the width; when even the smallest rows do not fit,
  rows are shed from the end (`BarLayout.Rows`). The rows stand right under the reading (centred
  beside it on a strip); what they leave below is air, not a gap between a number and its bar.
- **[S-6305]** **Tiles** (`TileGrid`): one tile per item filling the history's room in the grid whose
  tiles are the largest and squarest, unless one with tiles at least 80 % that size leaves fewer
  holes (16 cores as 4 × 4, not 6 + 6 + 4; `BarLayout.Tiles`), gaps 4, 2, 1, each shaded by its
  load ([S-6301]).
- Not ported / different: the original's `visibleMetrics` multi-select and everything it switched
  (removed by [S-1762]). The audit's `MeterLadder` was not built: `MeterFace` and `Disclosure` are
  the ladders.

## Power and Bluetooth

Power: one widget per action (R7-POWER), replacing the port of `main`'s Power tile
(`widgets/system/power`, [S-01]..[S-11]). The SDK: `PowerActionRules`, `PowerButton`, `PowerFace`,
`PowerButtonView` (the one view of all seven) and the base `PowerButtonConfig`; App: `Win32PowerActions`
and `InertPowerActions`, behind `IWidgetHost.Power`; tests `PowerActionTests` (the module test host).
Each power button is a module of its own (`src/Widgets/Themia.Widget.Shutdown` … `.ScreenOff`, one
tag each, [S-7400]..[S-8000]), and so is Bluetooth (`src/Widgets/Themia.Widget.Bluetooth`).

- **[S-1910]** Each power action is its own widget kind and picker entry, with its own glyph, name and
  description: **Shut down** (`Shutdown`), **Restart**, **Sleep**, **Hibernate**, **Lock**, **Sign
  out** (`SignOut`) and **Turn off display** (`ScreenOff`), in the add menu's Devices & power › Power buttons ([S-3511]), default size
  120×120. The kind maps to its `PowerAction` in Core (`PowerActionRules.ForKind`/`KindOf`); one
  view (`PowerButtonView`) serves all seven. Each kind has a config of its own (`ShutdownConfig` …
  `ScreenOffConfig`, base `PowerButtonConfig`): nothing to set beyond the card's common settings and
  the style ([S-1919]), so the settings page shows only those, and a power widget never needs setting up.
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
  `--style tile|round` draws either style ([S-6006]). `--sizes 100x100,160x100[,tiers]` draws several
  in-between sizes on one page (with `tiers`, the seven as well).
- **[S-1919]** Styles ([S-6000]): one `PowerButtonStyle` on the base `PowerButtonConfig`, shared by all
  seven kinds: **Tile** (the default, [S-1911], [S-1912]) and **Round button** (`round`), named,
  labelled and drawn as the toggles' Round style ([S-3917], one `RoundButtonFace` and one round part
  for both). The round button is an accent circle (`AccentFillColorDefault`, Secondary on hover,
  Tertiary pressed) holding the icon in the on-accent colour, the name under it in the primary text
  colour; armed, the circle takes the critical background, the icon the critical colour and the
  draining ring runs round its rim, the sentence in the name's place (the label slot is planned for
  the taller of the two, so arming moves nothing); failed, the warning glyph on a control fill;
  unavailable, `AccentFillColorDisabled`. The whole tile stays the button: arming, edit mode and a
  config change disarming it ([S-1913]) are the same in both styles.
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
  `ItemMenu`) has Done/Not done, Rename, Delete (danger,
  no dialog). A habit is moved by dragging ([S-1621]) or with Alt+Shift+Up / Down on the focused row
  ([S-3802]); no menu has Move up / Move down.
  The settings page holds no habit editor (original S-23).
- **[S-1426]** History dots (0–31, default 7): oldest left, the current period right, from the same
  step as the streak; each dot's tooltip names its period in the cadence's units ("Week of Monday
  28 Sep 2026", "14:00, …", "September 2026"). A narrow tile shows fewer, dropping the oldest; the
  name never yields to them.
- **[S-1427]** Ladder (`HabitLayout.Plan`): XS box + name, two rows → S + run (number) → M + dots
  and the add footer ([S-3850], pinned at the bottom; always under an empty list) → Wide + run in words and a 7-period rate per row → L all of that → XL + best run
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
  History (both for the Dots style only, [S-6408]). Renaming a list retitles every widget showing it.
- **[S-6408]** Habits' styles ([S-6000]), `HabitTrackerConfig.Style` (`HabitTrackerStyle`,
  `"style": "dots"`): **Dots** (the rows of [S-1427]), **Heatmap** and **Checklist**, in that
  order, over the same list, log and cadence. Show streaks and History are the Dots style's own
  (`HabitTrackerConfig.ShownSettings`, [S-6070]); the page offers them for it alone and keeps their values.
- **[S-6409]** Heatmap (`HabitHeatmap`): GitHub's calendar over the log, columns of seven periods,
  the newest on the right. Under a daily cadence the rows are the weekdays from the language's first
  day, so a column is a calendar week and the days still to come this week are blank; any other
  cadence fills its last column to the current period. Decided by size: when every habit's name
  (Body) and its own seven-row grid of at least 4 weeks of 6-DIP cells fit, each habit gets one (a
  done period in the accent, the current period's cell ticking it as its box does); otherwise the
  list shares one grid, each cell shaded in four steps by the share of habits done (rounded up) and
  its tooltip naming the period and "3 of 5". Room goes to bigger cells (2-DIP gaps, at most 20), then
  more weeks (at most 53). No add footer beside it (except under an empty list) and no row keys.
- **[S-6410]** Checklist: the current period only, each habit's box and name (`HabitLayoutInput.Checklist`):
  no run, dots, rate or best run at any size; the rows, the add footer and the keys are the Dots
  style's ([S-1427], [S-1428]).
- **[S-1430]** Settings end with "Data folder" (issue #154): **Open in Explorer** opens the folder
  holding `habits.json` (`HabitPool.FilePath`, through `ShellLauncher.OpenContainingFolder`, so it is
  classified like every other launch), for a backup or another program. Off when the pool has no
  file (a debug render keeps it in memory).
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
  week (its column head underlined); Escape lets it go. The cursor day is the selected day, shared
  with the view's other calendars and kept across pages ([S-3624]).
- **[S-1984]** *(Retired: Page Up / Page Down page every layout, [S-2710].)*
- **[S-2700]** A calendar widget draws only its own layout (month, week, day, year or agenda: its
  style, [S-3640]), at every size. No size swaps it for another layout, adds an agenda beside a
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
- **[S-2704]** Week (`CalendarFit.Week`): its day columns ([S-2719]; the widths below are a column's)
  under weekday and number (never without one letter of the weekday); a band of bars (up to two lines, three on the hour grid, always leaving a line for
  the body); then, from 64 px columns and 280 px tall (L, XL), an hour grid with each day's timed
  events in overlap lanes; else each column's timed events as chips (time over title from 64 px,
  title alone from 40 px), then "+N"; under 40 px one dot per event and a muted one for the rest.
- **[S-2705]** Hours (`CalendarFit.Hours`): the day wanted is 07–22, widened to every timed event of
  the day(s) drawn. When it fits at ≥20 px an hour it is drawn whole, a tall box widening it toward
  midnight rather than stretch an hour past 48 px; else as many whole hours as fit, starting an hour
  before the focus (today: the next event still to end, else now; another day: its first event, else 08:00). Hours fill
  the box exactly; hour labels read "07:00", or "07" in a day under 160 px. Today has an accent
  now-line. Events outside the window are on the day's hover.
- **[S-2706]** Day: the all-day band's shared line ([S-2718]) splits its width in chips of at least
  56 px, the rest "+N" (on hover); a day with nothing says "No events today".
- **[S-2707]** Year (`CalendarFit.Year`): twelve small months in the arrangement of 4×3, 3×4, 6×2,
  2×6, 12×1 or 1×12 with the largest day cell, preferring cells that hold numbers (a caption line)
  and then a weekday row; under 4 px it falls back to 6, 4, 3, 2 or 1 months around the current one
  (`FirstMonth`, inside the year). Number cells: today the accent disc, a day with something a strong
  number over a stripe of its first item's colour. Square cells: filled with that colour, today
  ringed. Every day's items on hover. XS shows three months, S six, the rest twelve.
- **[S-2708]** Month names in the year follow the small month's width: "September", "Sep", "S"
  (`LocaleFormat.MonthName`); the current month's in the accent.
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
- **[S-2717]** Long titles wrap: an agenda row's title, a week column's chip and a timeline block
  take a second line where a title runs past its one (word wrap, an ellipsis after the second). A
  list is fitted at one title line a row first, so a wrap never costs an event its place; what is
  left under the column (less the "+N" line) goes to the wrapping titles in order, one line each
  (`CalendarFit.SecondLines`). A timeline block spends its whole caption lines on the title, its
  time, the title's second line, then where and who (`CalendarFit.BlockParts`). Month and band
  chips stay one line.
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

## Calendar: five styles and shared dates (CAL-SPLIT)

The calendar's layouts are its five styles, and the calendars on one view share the day selected and
the event pointed at. Configs and their descriptors in Core's `Model/Configs/OfficeConfigs.cs`;
the sharing rules in `Office/CalendarShare.cs`; provider reading in `Office/OfficeWire.cs`,
`Office/CalendarRules.cs`, `Office/IcsEvents.cs`. One view, `CalendarView*.cs` (`CalendarView.Links.cs`
for the links), one settings builder, `WidgetSettings.Calendar.cs`. Tests `OfficeConfigTests`
(`S36xx_`), `CalendarDaysTests`, `CalendarShareTests`, `CalendarProviderTests`.

- **[S-3600]** *(Retired: five widget kinds, one per layout; they are one kind with five styles,
  [S-3640].)*
- **[S-3601]** One settings builder for every calendar widget (the calendar, Next meeting, the week
  planner): accounts and their calendars with colours ([S-562], [S-566], [S-567]) and birthdays,
  plus only what the calendar's style uses ([S-3641]). No calendar stores a week start ([S-564]).
- **[S-3602]** One data seam and one sync for every style ([S-2701], [S-588]): two calendar widgets on
  a desktop ask the same shared source and store, never their own.
- **[S-3606]** Debug renders: `--widget-png Calendar <out> --style month|week|day|year|agenda
  --state sample|selected|linked|days` for each style: the fixed sample (two calendars, a one-day and a four-day all-day event, a trip across a
  week's end, an overnight event, overlaps, a long title, birthdays, a year of meetings) through a
  fixture `ICalendarData`; `selected` selects the day after tomorrow as another widget would ([S-3620]);
  `linked` puts another widget's pointer on one event ([S-3623]); `days` sets the style's own range
  (the week: the next four days from today; the agenda: today and tomorrow). A card that needs
  setting up runs its view for the preview (`WidgetCard.RunForPreview`).
- **[S-3636]** The agenda's date range (`CalendarWidgetConfig.AgendaDays`, 1–365, 30): how many days it
  covers from today, today included; it replaces the fixed 30 days in the one range path
  (`CalendarRules.ViewRange`). Settings offer Today, Today and tomorrow, 3, 7 and 30 days, and Custom,
  which shows a number of days of its own; the length (`agendaCount`) still caps the entries within
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
  (`Period`: a month, day or year is the week start alone). The settings page shows only the
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
- **[S-3625]** Paging (back, forward, today) publishes the days now on screen (`VisibleRange`) and moves
  no other widget: two months on a view can show two months, and only a selection is followed.

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
  name, "Work (Microsoft)", is retired, [S-666]). On one-line rows from 720px the pointer, or the
  keyboard focus ([S-1288]), swaps a row's time for quick actions: mark read or unread, archive (not
  in Junk), delete — only where the account allows changes ([S-544]). The swap moves nothing: the
  time's slot is always as wide as the actions (`MailLayout.InlineBoxes`), the hidden time keeps
  its box, and the buttons overhang the line instead of growing it, so every other part of the row
  keeps its position and size and the row its height.
- **[S-1285]** *(Retired: no folder summary ("4 unread of 1,661 in Inbox") under a large tile's
  list; the large tile shows more messages instead. The header badge keeps the unread count.)*
- **[S-1286]** How many messages is not a setting (user decision): every Mail widget asks for the
  whole shelf the sync keeps per folder (`EmailConfig.FetchCount` = `MailShelves.MaxPerFolder`, 50),
  newest first, and the list scrolls past the rows that fit, so a big tile is never half empty and
  resizing never refetches. A `maxCount` left in a hand-edited file is ignored.
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
  saves `SortOrder`, like the menu's toggle; its tooltip says the current order. The compose button
  joins before it from 240px ([S-558]).
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
- **[S-6319]** A tile's figure takes the largest step (Caption, Body, BodyLarge, Subtitle) whose line
  is at most 40 % of the tile's height and whose widest figure ("100%") fits inside a 4-DIP inset;
  a smaller tile is its history alone (`CoreGraphLayout.Tile`). Room at L/XL is a larger figure.
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
  screen only, and nothing re-settles on a display change. An unusable work area
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
- **[S-847]** `DefaultViewForNewDisplays` (Displays page, "New displays show", [S-3832]): a display without a
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
  ([S-663]). The app-wide widget scale (`Design.UiScale`) and each widget's own ([S-6532])
  multiply in through the same path.
- **[S-851]** The hit region never covers the taskbar: it is cut to the work area in normal mode,
  and is the work area itself (not the whole window) in edit mode. A widget the taskbar has grown
  over is clipped there (`SetWindowRgn` clips painting as well as input) rather than moved. This
  matters while Show Desktop lifts the surface above the topmost band ([S-33]), when the region is
  all that keeps the taskbar clickable.
- **[S-852]** The edit bar ([S-3500]) and the placement hint sit inside the work area (margins from
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
- **[S-1362]** No word or character count, and no "Saved" (user requirement). The one status line
  is "Not saved" under the text, only while the note's last write failed (the pool's save events),
  where a line is left after the text's two.
- **[S-1363]** Ladder (`NotesLayout.Plan`): the text always (at least two lines; headings at body size
  on a compact width) → the "Not saved" line while a write failed ([S-1362]); the text takes the
  rest. The formatting bar is not a row of the body ([S-3963]). The widget is the one
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
- **[S-3963]** The formatting bar stands in the card's header, on the title line between the title
  and the note picker, while the note is being edited: the caret is in the note, or the bar's "More"
  menu is open. At rest the header is the title and the picker, as every card's is; the bar comes
  with the caret, where formatting is wanted, and goes with it. Compact 24 DIP buttons that keep the
  header its height (so focusing never moves the text). `NotesLayout.HeaderBar`: every button where
  the header has room after its insets, icon, the title's 64 DIP minimum ([S-655]) and the picker;
  else as many as fit, by rank (bold, italic, task, bullet, heading, strike, code, numbered, quote),
  and a "More" button whose menu holds the rest (checked for the marks at the caret); nothing where
  not even "More" fits. Like the bar's buttons, "More" never takes focus. Debug renders:
  `--widget-png Notes <out> --state sample|editing` draw a fixed note at rest and while edited.
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
  chart (original [S-22]). The settings page offers "System drive (C:)" and every drive Windows lists;
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
  last probe. A dot at the right of the header carries it in the success / critical / tertiary
  colour; its tooltip says it in words, and so does a dimmed reading's.
- **[S-1172]** The ping chart is latency against the window's slowest answer (floor 1 ms); an
  unanswered probe is a dimmed "--" that plots on the baseline. Online colours the chart success,
  offline critical.
- **[S-1173]** *(Retired by [S-3791]: no `HostLabel`. The header title is the host; a name for it is
  the card's own title.)*
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
  on the surface losing activation (Alt+Tab, the settings window), on a press outside every surface
  ([S-815]), when the holder's card leaves its surface, on entering edit mode and when the surface
  closes. Only Escape gives the keyboard back: after a click elsewhere the click already put it where
  the user wants, and a launched app must not be raced. Every end moves XAML focus out of the old
  holder's card so no caret is left blinking; its view hears `WidgetKey.Escape` through the session
  ([S-813]), or directly when the session does not know the holder (a box focused without a press).
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
- **[S-813]** One interaction session, app-wide (Core `WidgetSession<T>`, tests `WidgetSessionTests`;
  `AppHost.Session.cs`, `DesktopWindow.Session.cs`): from a press on any surface until the user is done.
  It names the widget being worked in — every widget, keys or not — and is the one rule for when a
  selection or cursor goes: its view hears `WidgetKey.Escape` (`IWidgetKeys`) whenever the session
  leaves it. A refused foreground ([S-800]) no longer drops the selection the click just made.
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
| Calendars (month, week, day, year, agenda) | Left/Right and PgUp/PgDn page by the layout's unit (month, week, day, year, agenda page); Up/Down move the selected day a week (month, week); Escape lets it go; Home today; Enter first event ([S-1269], [S-2710], [S-3624]) |
| Contacts | Up/Down, PgUp/PgDn, Home/End; Enter writes or calls; type-ahead ([S-1277], [S-1983]) |
| Tasks | Up/Down, PgUp/PgDn, Home/End; Enter/Space tick; Delete; F2; Esc ([S-787], [S-1981]) |
| Habits | as Tasks, plus Tab/Shift+Tab wrap ([S-1428], [S-1982]) |
| Folder | arrows (grid aware), PgUp/PgDn, Home/End, Shift extends; Enter; Space; Delete; F2; Menu; Ctrl+C/X/V/A; Backspace; type-ahead ([S-1327], [S-1993]) |
| Shortcuts | arrows, Home/End, PgUp/PgDn; Enter opens; Space toggles; Delete; F2; Menu; type-ahead ([S-1390], [S-1995]) |
| RSS | Up/Down/Left/Right, PgUp/PgDn, Home/End; Enter opens; Menu ([S-1306], [S-1985]) |
| Clipboard | Up/Down, PgUp/PgDn, Home/End; Enter copies; Delete; Menu ([S-1444]) |
| Pictures (slideshow) | Left/Right; Home/End; Space pauses; Enter opens ([S-811], [S-1992]) |
| Mixer | Up/Down/Home/End rows; Left/Right, +/- step; PgUp/PgDn step 10; M/Space mute; Menu ([S-1225]) |
| Volume | arrows, +/- step; PgUp/PgDn step 10; Home/End 0/100; M/Space mute ([S-1226], [S-2733]) |
| Music | Space/Enter play-pause; Left/Right track; Shift+Left/Right seek 5 s ([S-1468]) |
| Pomodoro | Space/Enter start-pause; Right skip; Home reset ([S-1987]) |
| Screen time chart, donut, apps | Up/Down, PgUp/PgDn, Home/End move the linked cursor; Enter/Space select; Menu hides; Left/Right a day ([S-3307], [S-3334], [S-3344], [S-3354]) |
| Quote | Right/Space/Enter next; Left previous ([S-1990]) |
| Video stream | Space pause ([S-1991]) |

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

Keys everywhere outside a widget's own view: edit mode on the desktop surface, the view bar, the
settings window. Rules in Core `EditKeys`, `KeyboardNudge`, `SelectionRules.Cycle`, `SettingsKeys`
and the widgets' `TypeAhead` ([S-1980], one implementation for lists and the navigation; tests `ShellKeyboardTests`); carried out by `DesktopWindow.Keys.cs`,
`ViewBar.cs` and `ConfigWindow.Keys.cs`. Widget views' keys are the lease's ([S-800]..[S-812]).
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
- **[S-1974]** **Enter** opens the selected (primary) widget's settings page (nothing for a widget with none, [S-1750]); the **Menu key** or
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

Package SYS-P3. Both meters are modules (`src/Widgets/Themia.Widget.Battery`, `.Network`, their
`SPEC.md`); the rules are the SDK's (`BatteryRules`, `NetworkRules`, tests in
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

Styles ([S-6000]) of the I/O meters, on the shared `MeterView` (`MeterLook`, [S-6300]): a look that
draws the reading itself is a `MeterStyleFace` (the SDK's `Widgets/Meters/MeterStyleFace.cs`,
`MeterView.Faces.cs`); its rules are the SDK's `MeterStyleFaces.cs`. The hub, the series and their
history are the same in every style. Tests `IoStylesTests` (`S626x_`, `S627x_`).

- **[S-6261]** A picture style (the battery, the signal bars) is the picture and the figure: the figure
  at the tier's step ([S-3860]) on the line under the picture, beside it on a wide strip (at most 45 %
  of the width), the picture as large as the rest allows at its own aspect (`PictureFace`); a picture
  under 20 DIPs is not drawn and the figure stands alone.
- **[S-6262]** **Battery** (battery): a battery drawn as Windows' battery glyph is: a rounded body
  twice as wide as tall, a cap at its right (a tenth of its height wide, two fifths tall, centred), a
  secondary-colour outline and, inside it clear of the outline, a fill from the left to the charge in
  the charge's pressure colour ([S-1187]); any charge above 0 is at least a stroke wide. While
  charging the lightning bolt stands in the middle of the fill box (`BatteryShape`).
- **[S-6263]** **Signal bars** (ping): four bars rising left to right on one baseline, as Windows'
  Wi-Fi signal, as many lit in the status colour ([S-1170]) as the last answer earns: up to 50 ms 4
  bars, up to 100 ms 3, up to 200 ms 2, slower 1, an unanswered probe or none yet 0
  (`PingSignal.Thresholds`); the rest are the track. The latency is the figure.
- **[S-6264]** **Numbers** (network): download and upload, each behind its arrow in the meter's
  colour, both at one step, as large as the tile allows (to Display and grown past it as a lone
  reading, [S-1762]), stacked or side by side, whichever draws them larger, a tie stacked
  (`MeterNumbers`). No history.
- **[S-6265]** **Bars** (disk): the total rate as the reading, and under it a labelled row for reads
  and one for writes ("Read  42 MB/s"), each with a bar (the shared bar rows, [S-6304]).
- **[S-6266]** **Ring** (battery): a ring of the charge in its colour (the shared `RingPart`,
  [S-6060]), as large as the tile allows, the figure in its hole at the largest step a chord through
  it takes (`RingFace.Figure`). A ring face with lines (the drive's, [S-6274]) puts them beside the
  ring on a wide strip when 120 DIPs are left for them, else under it, each only where it fits whole;
  the ring keeps at least 48 DIPs.
- **[S-6267]** Only the graph style draws the history (`GaugeConfig.DrawsHistory`); a styled meter's
  page offers no Show history switch ([S-1122]), the chart span stays.
- [S-6270]: moved with the Network widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Network/`).
- **[S-6271]** Disk: `DiskConfig.Style`, **Graph** ([S-1820]) and **Bars** ([S-6265]). A bar's share
  is its rate against the top the graph would have: the doubling of 1 MB/s past the peak of the last
  minute's totals and this sample's ([S-1821], `DiskRules.BarShares`), so idle reads as a sliver; the
  window starts again when the drive or the scope changes.
- [S-6272]: moved with the Battery widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Battery/`).
- [S-6273]: moved with the Ping widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Ping/`).

## GPU and screen time: the whole tile (SYS-P4)

Code: the GPU module (`src/Widgets/Themia.Widget.Gpu`: `GpuView.cs` + `GpuView.Panel.cs`, its
settings page), `Services/GpuSampler.cs`, the SDK's `GpuRules` and `MeterFace`;
`Widgets/ScreenTimeView*.cs` (+ `.Footer.cs`), Core `ScreenTimeRules`; settings
`WidgetSettings.SystemWidgets.cs`. Tests `GpuRulesTests`, `ScreenTimeRulesTests` (`S12xx_`).

- [S-1200], [S-1201]: moved with the GPU widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Gpu/`).
- **[S-1207]** *(Retired: one adapter per widget, [S-433]; nothing shares the tile.)*
- **[S-1210]** The ring's total takes the largest step that fits its hole; when even Caption cannot
  hold `4h 37m` it shows the compact form (`4.6h`, `13h` from ten hours, `37m`, `<1m`). Never clipped.
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
`Themia.App/Services/SystemData/*` (`SystemHub*.cs`, `HubSources.cs`, `HubSource.cs`, `WatchSlot.cs`),
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
  index moves on. CPU, memory, disk activity and space, network, addresses, processes, battery and GPU
  are read every second, so every tile on the desktop moves on the same second. A new feed is read at once.
- **[S-2504]** A charted series (a feed plus an instance: a drive root or `*`, an adapter's position)
  keeps its history in the hub, in a ring of `SeriesHistory.Capacity(source)` points: the longest
  span offered (1 h) at the source's spacing, 3600 points, 720 for a ping (at least the widest
  statistics window). A view built again — a view switch, edit mode, a display change, a reopened
  settings window — loads the history and the newest reading at once; a longer span shows the longer
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
  views hold, and the hub's ticker stops with the desktop.

## System details (SYS-DETAILS)

The details the original's meters showed, back as their own placeable widgets; the meters themselves
stay label, reading and history ([S-1762]). Seven kinds, each in its meter's family of the
add menu ([S-3511]): CPU details, Memory details, Top processes, Network details, Battery
details, Ping statistics, GPU details. One view, the SDK's `DetailView` (+ `.List.cs`), one `DetailPart` per
kind in its module project (`src/Widgets/Themia.Widget.<Kind>Details`, `.TopProcesses`, with its config,
settings page and `SPEC.md`); rules in the SDK (`DetailFace`, `DetailSheet`, `CpuDetailRules`,
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
- **[S-2402]** The ladder (`DetailFace.Plan`): Compact both ways is the hero alone at the tier's step
  (a list kind: its rows). A Wide strip at least Expanded wide without a list puts the hero in a left
  column (40 %) and the facts in stat lines beside it. Otherwise one height budget: the hero (the shared
  readout, [S-667], its caption a Body line under it) is reserved at Subtitle; the facts take whole rows
  most wanted first, as stat cells or stat lines ([S-3862]); a list takes one whole row at least, or is
  not shown. The rest is [S-3861]. Costs are line heights of the live ramp.
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
- **[S-2433]** Every process row leads with its program's icon, as Task Manager's does: the executable's
  path (`QueryFullProcessImageName` on a limited handle) is asked once per process, pid and start time
  (`ProcessPathCache`, shared by every list; an exited process is forgotten, one Windows will not open
  is remembered as having none: a system process is drawn by its image in System32, as Task Manager does), and the icon once per path and pixel size (`AppIcons`, the shell's
  icon at the row's 16 DIP times the display scale; a packaged app's package logo). A group takes its
  oldest member's. A row without an icon, or before it is extracted, shows the program glyph.
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
  every battery summed, devices re-listed once a minute, one hub read per due tick; asked only when Windows
  says there is a battery.
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
  sampler (the memory counters join its query); the header names the adapter ([S-430]).
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
live in Core (`FeedAggregate`, `FeedLayout`, `NetworkGuard`, `NetworkCacheStore`) and the SDK
(`MarketSearch`, `MarketQuote`, `ExchangeTime`), tests in `FeedWidgetTests`/`StockWidgetTests`. Stocks
is a module (`src/Widgets/Themia.Widget.Stocks/`, its own tags in its `SPEC.md`).

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
  resolve it (classic NG-07), but an IP literal is checked even then. No switch: the local network
  is reached only for the host of an address the user typed ([S-3840]). A refused feed says so in the
  widget ("x is on the local network. Themia goes there only for an address you typed yourself").
  Every request, the camera stream and the fixed public services included, connects through the
  guard ([S-960]), and the loopback hardware monitor is reached as "this machine only" ([S-967]).
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
  follow, in widgets and in the settings window. `SystemFillColorAttentionBrush` is an accent alias with no colour
  resource of its own (Light2 in dark theme, the accent itself in light) and is mapped like the others.
- **[S-821]** Text size (`FontScale`, 0.5..2) scales the whole `FluentType` ramp (size and line
  height together), so `Fit`/`Hero` still pick real steps and a layout sheds rather than
  overflows. Live: before a card's view re-lays out on the state change, text already on the card
  whose size was set on the element is rescaled by the ratio; text built afterwards comes from the
  scaled ramp. Icons keep their size (widget scale, [S-850], scales everything).
- **[S-822]** Theme. "Use system setting" is resolved by the engine (Windows background colour) and
  every window root gets the explicit Light/Dark. `ThemeBrush.Of` hands out one engine-owned
  brush per key, coloured from the effective theme's dictionary and recoloured in place on a theme
  or accent change — a brush looked up from code used to be the app's start-up theme, so code-made
  text stayed white in a light card. A Windows theme or accent change (`ColorValuesChanged`) re-runs
  the apply path.
- **[S-823]** Widget background ([S-6560]): **Default** is the Fluent base + card tint at full
  strength; a colour of the user's own replaces both, exactly that colour at its opacity (0..1);
  **Transparent** has no fill. *(The old 0.2..1 opacity slider and the 60 % "translucent" card are
  retired: a colour with an opacity replaces them.)*
- **[S-824]** Border: style None/Solid/Dashed (3-on-2 stroke widths)/Dotted (round dots two widths
  apart), width 0..8, colour (empty = the theme's card stroke), opacity 0..1. Drawn as a shape over
  the content and inset with the gap; none on a transparent widget.
- **[S-6481]** Every border setting shows (`DesignLook.Ink`). A colour of the user's own is drawn
  exactly, opaque, and the border opacity fades the line once (the element's opacity, never also the
  brush's). The theme's colour is Fluent's card hairline (`CardStrokeColorDefault`, [S-835]) for a
  solid line up to 1px, and the strong stroke (`ControlStrongStrokeColorDefault`, #8BFFFFFF dark /
  #72000000 light) for a heavier, dashed or dotted one. Drawn in the hairline's 6-10 %, a 4px,
  dashed or faded line looked exactly like none, so width, style and opacity all seemed ignored.
  The opacity never swaps the ink, so fading is monotone; at the 1px hairline it is subtle by nature.
- **[S-6480]** `--widget-png … --design key=value,...` draws the cards with appearance values (`border`,
  `borderWidth`, `borderOpacity`, `borderColor`, `radius`, `background` and `items` (a fill, [S-6569]),
  `opacity` and `itemOpacity` (their colours' opacity), `header`, `headerOpacity`, `accent`, `font`,
  `preset`), so a look is checked without the settings window.
- **[S-825]** Corner radius 0..32 on the fill, tint, border and content clip. The edit markers stay square ([S-31b]).
- **[S-6482]** The default corner radius is 4px (`DesignState.DefaultCornerRadiusPx`; a radius that is
  not a number loads as 4), by the user's decision: Fluent gives an overlay card 8px. The Default
  preset reads it from a fresh design ([S-831]); the other presets keep their own (Midnight 6,
  Daylight 12, Glass 18, Terminal 0, Paper 4), none of which mirrored the old 8.
- **[S-826]** Font: `system` (Segoe UI Variable), `sans` (Arial), `serif` (Georgia), `mono` (Cascadia
  Mono, Consolas), or any installed family name; a value that is not a plausible family name is the
  system font. Applied to the card content through a presenter, so text inherits it; icon glyphs
  name their own font and are untouched. Text boxes keep the control font.
- **[S-827]** The font picker lists every installed family (GDI `EnumFontFamiliesExW`, off the UI
  thread, cached until the installed fonts change, [S-6443]): vertical `@` faces dropped, duplicates
  folded, sorted. Each entry is drawn in its own font ([S-1723]).
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
  Each owns 14 fields (theme, accent, font, widget background (one fill choice, [S-6565]), header
  fill + opacity, border style/width/colour/opacity, corner radius, widget gap, item background (a
  fill choice) + item text colour, [S-6515], [S-6545]) and
  never widget scale, text size or motion.
- **[S-831]** Default is read from a fresh `DesignState`, so a fresh install reads as Default.
- **[S-832]** Applying a preset writes its fields in one mutation and leaves every other field alone.
- **[S-833]** The picker shows the matching preset, or Custom; only owned fields are compared, colours
  case-insensitively, numbers within 0.001.
- **[S-6483]** Picking a preset while the design reads as Custom first asks (the settings window's
  `ContentDialog`, `FluentLayout.ConfirmAsync`): "Replace your custom look?", naming the preset and
  what it replaces (theme, colours, font, border, corners, spacing) and what stays (text size, widget
  scale, motion); Replace, or Cancel (the default button, Escape). Cancel writes nothing and the page
  shows Custom again. A design that is a preset is never asked about: nothing of the user's is lost.
  The rule is `DesignPresets.ReplacesCustomLook`.
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
  rows, grid cells, crumbs, row buttons, a toggle's or power button's tile, the quote and scratchpad
  commands), **Tile** (a round button's circle), **Chip** (calendar events and selected days, an
  empty year day, a flip clock's card, the forecast's today), **Track** (meter bars, gauges, rings,
  the seek bar, signal bars, habit dots and heat cells, the sun's night bar, the year's days ahead)
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
- **[S-6515]** Presets own the item layer too ([S-830]). Default and four others keep the Fluent
  fills; Glass gives its items a pane of their own (#2A3248 at 60 %), so rows and tiles still read on
  its thin glass.
- **[S-6516]** *(Retired by [S-6568]: the Item colour and Item opacity rows are one fill row.)*
- **[S-6517]** *(Retired by [S-6569]: `--surface` is gone; `--background` and `--own-items` take a fill.)*
- **[S-6560]** **One fill choice** (`FillChoice`) for the widget background and the item background,
  app-wide (`DesignState.Background`, `DesignState.ItemBackground`) and per widget
  (`WidgetInstance.Background`, `WidgetInstance.ItemBackground`): **Default**, **Transparent**, or a
  **Colour** (`#RRGGBB`) with an **opacity** 0..1, stored as `{ "kind": "Colour", "colour":
  "#1B2030", "opacity": 0.55 }` (the kind case-insensitively on read). Default app-wide is the Fluent
  fill (the Windows card, each item's own fill); on a widget it is "follow the app-wide one". A
  Default or Transparent choice keeps the colour and opacity last picked, so going back to a colour
  finds it; they draw nothing until then and never make two choices differ (`FillChoice.SameAs`).
  Translucent is gone: a colour with an opacity is the see-through card now.
- **[S-6561]** On load (`FillChoice.Repair`, loudly, app-wide and per widget): an unknown kind is
  Default; a colour choice whose colour is not one is Default; a colour is normalised; an opacity that
  is not a number is 100 % and the rest is clamped to 0..1. A missing choice is Default, silently.
- **[S-6562]** What is drawn (`FillChoice.Effective`): a widget's own choice unless it is Default,
  else the app-wide one — the background and the item background each on their own.
- **[S-6563]** The card paints its effective background (`DesignLook.Card` → `CardFill`): Default is
  the Fluent base and card tint at full strength, a colour exactly that colour at its opacity, and
  Transparent nothing — no fill and no border ([S-824]); a colour at 0 % is not transparent and keeps
  its border. A header sharing the widget's fill fades that fill once more by the header opacity
  ([S-1761]). The view bar reads the app-wide background, never under 20 %.
- **[S-6564]** Items on a **Transparent** item background have no fill at rest, disabled or as a
  track; a lit item (under the pointer, pressed, selected) still takes the Row's subtle Fluent fill,
  so a list keeps answering the pointer and showing its selection. Default has no opacity of its own.
- **[S-6565]** Presets own both fills ([S-830]); the old colour + opacity pairs map onto a colour
  choice: Default is Default and Default; Midnight #0B0B0F at 96 %, Daylight #FFFFFF at 86 %, Glass
  #1B2030 at 55 % (its items #2A3248 at 60 %), Terminal #050D07 at 97 %, Paper #FAF7F0 at 100 %.
  Applying one writes copies, never the preset's own objects.
- **[S-6567]** The **fill control** (`ColorChoice.CreateFill`, `FluentLayout.FillPickerRow`), one
  path for both fills, in the panel and on the Appearance page: the colour control's swatch button
  and flyout ([S-1549]) with **Default**, **Transparent** (an empty swatch), the swatches, Custom
  colour (WinUI's picker with its **alpha slider** too) and the eyedropper, and an **Opacity**
  slider (0..100 % in 5 % steps, on once a colour is chosen) under them. The button reads Default's
  label, "Transparent", or the colour's name with its opacity under 100 % ("Custom, 50%"), its swatch
  the colour at that opacity. Picking a colour keeps the opacity last set; the slider shows a move at
  once and stores it when it is let go (or a key steps it, or the flyout closes): one change, not one
  per step.
- **[S-6568]** The Appearance page has one **Widget background** row (Theme and colours, Default
  reads "Theme colour") and one **Background** row in the Item background group, both the fill control;
  the old Widget opacity and Item colour / Item opacity rows are gone. Settings import names each
  fill's change in words ("Theme colour", "Transparent", "#1B2030, 55%").
- **[S-6569]** Debug: `--design background=default|transparent|#RRGGBB[@percent]` and `items=…` set
  the app-wide fills of a render (`opacity=` and `itemOpacity=` their colours' opacity);
  `--widget-png … --background <fill> --own-items <fill>` the cards' own.
- **[S-6570]** Whether a kind **has items** (`ItemKinds.Has`): its view paints rows, tiles, chips or
  tracks through the item layer ([S-6513]). Sixteen kinds paint none (Alarm, Audio visualizer,
  Brightness, Exchange rates, Gallery, Next meeting, Notes, Rain next hour, Recycle bin, the screen
  time chart and donut, Stocks, Stopwatch, Video stream, Volume, Weather now). Each widget's descriptor states
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
- **[S-6545]** The Appearance page's Item background group has **Item text colour** (the colour
  control, Theme colour as its default, the neutrals first). Presets own it and none sets one (Glass's
  dark items read with the dark theme's white text). Settings import names the change.
- **[S-6546]** **Where it reaches: what stands on an item.** One path: `ItemFill.Paint` of any role but
  Track makes the element an item, and every text, glyph and shape inside it drawn in a theme text
  colour (the four `TextFillColor*` tiers) is drawn in its card palette's text brush of that tier
  (`ItemPalette.Text`). A colour the view sets later is inked as it is set (a property-changed watch),
  new content when the item is painted again or loads; runs follow their text block. `ItemFill.Button`
  sets the button's foreground resources too. What is laid over an item without being inside it says
  so (`ItemFill.OnItem`: the round button's icon, a flip card's figures, the forecast's today column
  and its curve figures); an item that stops being one says so (`ItemFill.Release`: a toggle or power
  tile turned accent or critical, the round looks' tile). **Not reached:** the card's own text outside
  items (the header, hero readouts such as a clock face, a meter's figure, a day number on a clear
  calendar cell, an unselected forecast column), tracks, and any colour that is not a theme text
  colour (accent, text on accent, status, event colours, the user's own).
- **[S-6547]** Debug: `--design itemText=#RRGGBB` sets the app-wide item text colour of a render;
  `--widget-png … --own-items <fill> --own-item-text #RRGGBB` give the cards a layer and a text
  colour of their own, the per-widget override.
- **[S-835]** The default look is Windows 11's, value for value, with the framework's own theme
  resources (WinUI 1.8 `generic.xaml`, read by key through `ThemeBrush.Of`, never copied):
  card = `CardBackgroundFillColorDefault` (#0DFFFFFF dark / #B3FFFFFF light) over
  `SolidBackgroundFillColorBase` (#202020 / #F3F3F3), i.e. #2B2B2B / #FBFBFB; a 1px
  `CardStrokeColorDefault` (#19000000 / #0F000000); a 4px card corner ([S-6482]: the user's
  decision over `OverlayCornerRadius` 8, which the `--reference` card still draws), 4
  (`ControlCornerRadius`) for controls and row highlights; text `TextFillColorPrimary/Secondary/
  Tertiary`; accent `AccentFillColorDefault` (Light2 dark / Dark1 light); status
  `SystemFillColorSuccess/Caution/Critical` (#6CCB5F/#FCE100/#FF99A4 dark, #0F7B0F/#9D5D00/#C42B1C
  light), which are also Stocks' rising/falling colours unless the user picks their own. Type ramp
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
- **[S-836]** The card composites as a WinUI card does: the Fluent tint stops at a solid stroke's
  inner edge (`DesignLook.TintInset`; a `Border`'s background is `InnerBorderEdge`), so the stroke
  lies over the base alone (#1D1D1D / #E5E5E5 at 1px); dashed and dotted lines keep the tint
  full-bleed so their gaps do not show bare base. `DesignLook.Over` is the source-over rule.
  `--widget-png … --reference` draws a stock WinUI card (framework brushes, application theme) beside
  the first tier and prints the colours it should composite to, for a pixel comparison.
- **[S-837]** Tracks follow the control they stand for: a seek bar is a slider
  (`SliderTrackFill` = `ControlStrongFillColorDefault`, value `AccentFillColorDefault`, 4px), and so
  are the tracks that stand for a progress bar or ring (Pomodoro's ring), the
  forecast's temperature range bar and a habit's empty dot. None uses the disabled fill, which reads
  as a control that is switched off.
- **[S-834]** Appearance page: Style (presets + Custom), theme, accent, widget background ([S-6568]),
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
`.Volume`, their `SPEC.md`) over the SDK's `AudioFeed`, `AudioLook`, `AudioDeviceRow` and
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
- **[S-1222]** Device picker: settings ComboBox only ("Default output" + connected devices,
  enumerated off the UI thread), which binds the widget to a device. Neither widget offers a device
  picker of its own at any size (user decision: choosing the output is Windows' job, not the
  widget's); the device line, the volume caption's menu and the XL panel's device list are gone.
  Context action "Follow the default output" when a device is chosen. Inert in previews.
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
- **[S-1227]** Mixer rows: compact (< 170 px: icon, mute; 32 px), two lines (48 px), one line
  (≥ 360 px; 40 px) — fixed heights so the list takes whole rows (`Disclosure.TakeRows`); the rest
  scroll, never half a row. Supersedes [S-449]'s thresholds' wiring (values unchanged).
- **[S-1228]** Mixer rows by room: apps holding a session but silent are drawn dimmed; with
  `OnlyActive`, the silent apps it hid follow the list dimmed when all playing rows fit. The mixer is
  its rows and nothing else at any size: no device line (was: name, "N playing", chevron).
- **[S-1229]** *(Retired: the XL mixer has no side panel — no device name, master level beside the
  list, microphone or device list. Nor is the master a row of the list any more, [S-3251].)*
- **[S-1230]** *(Retired: the volume tile has no ladder. The number, the level meter and its
  `ShowPeaks` setting, and the `ShowSlider` setting are gone; see [S-2730].)*
- **[S-1231]** *(Retired: the volume tile lists no apps; that is the mixer's job.)*
- **[S-1232]** *(Retired: no caption and no device name on the tile; muted reads from the speaker
  glyph and its tooltip. `VolumeRules.Caption`/`Readout` are gone.)*
- **[S-1233]** Previews never write (sliders, buttons, keys and device picks are inert); they read
  the machine.
- **[S-1234]** Stepping lands on the whole-percent grid and clamps (`AudioRules.Step`).
- [S-2730]: moved with the Volume widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Volume/`).
- **[S-2731]** The slider lies along the tile (`VolumeRules.Axis`): horizontal, centred vertically
  between side insets, on every shape but a column; a Tall area ([S-660]) stands it up, full
  height, with the speaker button under it. Growing a tile only lengthens the slider ([S-666]).
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
- **[S-1380]** Config (original S-21..S-23, S-33, S-46..S-48, S-71): `Style` Grid (default) / List /
  Details ([S-6401]), `GridSize` and `ListSize` Small / Medium / Large, `GridStretchHeight`, `GridIconOnly`,
  `ShowFileExtension`, `ClickMode` (shared `ItemClickMode`, Double by default). Enums are written as
  names; `Repair` brings unknown values back, drops null items and mends blank or repeated ids.
  `ShowTarget` and `OpenOnSingleClick` are gone (the details view and `ClickMode` replace them).
- **[S-6401]** Shortcuts' styles ([S-6000]) are its views, `ShortcutsConfig.Style`
  (`ShortcutsStyle`, `"style": "grid"`): **Tiles** (Grid), **List** and **Details**, in that order;
  the View expander is gone. The page offers the tile size, icons only and stretch for Tiles
  (`ShortcutsConfig.ShownSettings`, [S-6070]), the row size for List and Details.
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
  Rename (off for several; no moves: an item is dragged, [S-1410], or moved with Alt+Shift+Up /
  Down on the cursor, [S-3802]); Remove / Remove N (danger). Remove keeps one
  step of "Undo remove" on the card menu. Rename edits in place (Enter / losing focus commits,
  Escape cancels); a name equal to the derived one clears the label.
- **[S-1388]** Settings (original S-16, S-45, S-61): Display first, then Add (file / folder pickers
  and one switch per Windows location with its real icon; on adds it, off removes every item
  pointing there; the switches follow the list), the list editor last.
- **[S-1389]** Recycle Bin (original S-75): an item pointing at `shell:RecycleBinFolder` shows a
  count badge ("99+" cap; a dot under a 24 px icon), none when empty or unknown; tooltip
  "name — N items (size)" / "name — empty". `RecycleBinStatus` is process-wide, single-flight,
  asked when the bin changes, never on a clock: the command window registers for the shell's change
  notifications on the Recycle Bin folder (`SHChangeNotifyRegister`, recursive, shell level), and a
  change asks again only while a widget holds the bin (`RecycleBinStatus.Watch`); a change with nobody
  watching marks the answer stale and the next watcher asks once. A change during a query asks once more
  after it, so a burst costs two queries. It keeps the last answer on failure, and the icon is fetched per state (empty / full).
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
- **[S-932]** Windows owns the state; `Preferences.DesktopIconsVisible` is a first-paint cache. The
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
  no desktop monitor), Language & region = Time & language's globe (`globe`). A new app page names its icon in its `AppPages` line.
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
- **[S-1065]** Free tier: every widget type, at most **20 widgets** (all views) and **2 views** (the
  original allowed 10 widgets; raised so the first-run seed, [S-893], leaves room to add). Gates at every create: placement start and landing, paste (net of a cut's original;
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
- **[S-1583]** Every selected card shows it (accent markers, [S-31c]). A widget that is
  deleted or leaves the view leaves the selection with the change. Escape clears the selection
  before a second Escape leaves edit mode.
- **[S-1584]** Dragging a selected card moves **every selected widget on that display** by one
  translation: the block's bounding box is moved, its top-left snapped to the grid or an edge flush
  on the work area's ([S-06], [S-6609], Alt suspends both), and the move stops where the first of its
  widgets would keep less than the visible minimum on the work area ([S-6608]) — never resized — and
  each rect is then kept in reach ([S-09]). Offsets between the widgets never change. Resizing is per
  card and narrows the selection to it.
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
  order, as a snapshot with original ids.
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
  the selection; on any other card it selects that card alone first. Layering keeps the order the
  selected widgets had among themselves.
- **[S-1593]** A press on a card of a multi-selection keeps the selection so the drag that may follow
  carries all of it; released without moving (3 DIPs), it narrows the selection to that card.

Not done: a debug render of the multi-widget ghost (the outlines are pinned by
`S1585_*` instead). Not live-verified: everything pointer- and multi-monitor-driven here — the
rubber band, Ctrl+click, block drag clamping at the taskbar, hand-off of a selection across
displays, the ghost following the pointer across displays, Ctrl+A/Escape order.

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
- **[S-1109]** Settings › Language & region › App display language ([S-3797]): System + the thirty, each named in its own
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
  on the roots whose whole content is text-like UI — the settings window, each card, the edit bar,
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
  play/previous/next never do. The settings window's caption buttons stay where Windows draws them
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
  run here); the General row was checked with `--settings-png General`.

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
  geocoder, resolved once at the pick); exact name/latitude/longitude stay editable and validated
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
  its region. No widget view and no part of the surface opens a `ContentDialog` (the settings window
  may), and every flyout or menu a widget opens (a rename box, a note, `ItemMenu`) is its own popup
  likewise. The clear is in the header from three tools' width ([S-651]); in full screen it is
  always there. Tests read the source (`ConfirmFlyoutTests`); renders (`--state confirm` for
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

## Quick-setting toggles (QUICK-TOGGLES)

The toggle widgets of Karpo's Quick settings (`karpo-app`, docs\widgets.md §117: the ones a click
flips where they stand), each its own widget. SDK: `QuickToggleRules`, `ToggleGate`, `ToggleFace`,
`SwitchFace` (`Themia.Sdk`), `QuickToggleView` and `IQuickToggles` (`Themia.Sdk.WinUI`); Core:
`ToggleFollowing` (`ReadingOrder`, `KeyFollow`), `NightLightValue`; App: `Services/Toggles/*` (one
source per toggle, `ToggleHub`); tests `QuickToggleTests`.

- **[S-3900]** Thirteen kinds, one per toggle, in the add menu's subject **Quick settings** ([S-3511]), default size 120×120: Wi-Fi (`WifiToggle`), Bluetooth (`BluetoothToggle`), Airplane mode,
  Energy saver, Night light, Rotation lock, Do not disturb, Sound (the default output's mute),
  Microphone (the default microphone's mute), Spatial sound, Mono audio, Color filters, Game mode.
  The kind maps to its `QuickToggle` in the SDK (`QuickToggleRules.ForKind`/`KindOf`, read off the
  catalogue); one view serves all thirteen. Each is a module project of its own
  (`src/Widgets/Themia.Widget.WifiToggle` …, tags [S-8200]..[S-9400] in their `SPEC.md`), with a
  config of its own (`WifiToggleConfig` …, base `QuickToggleConfig`) whose
  one setting is the style ([S-3916]), picked on the properties panel ([S-6536]), so it has no settings page ([S-1750]); it
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
  card's own fill and corners: on, `AccentFillColorDefault` (Secondary on hover, Tertiary pressed)
  with `TextOnAccentFillColorPrimary` icon and name; off, the subtle fills on hover and press; not
  known, the secondary text colour; unavailable, the disabled text colour, no hover, no press, and
  the reason in the tooltip ("Bluetooth: This PC does not have it"). The tooltip is otherwise
  "Wi-Fi: On"; after a refused click, "…: Windows did not switch it" until the next click. In edit
  mode the press is the card's ([S-1911]).
- **[S-3905]** Clicks go through `ToggleGate`: a click asks for the opposite of what is read and the
  tile shows it at once; while that switch is on its way every click does nothing; a click within
  400 ms of the last is the same click (a double-click never flips back). When Windows answers, the
  source's reading stands. Space or Enter switch it while the widget holds the keyboard lease
  ([S-800]); a toggle that cannot be switched does not take the keyboard (`WantsKeys`).
- **[S-3906]** Ladder (`ToggleFace`): Compact width (XS) is the icon alone; then the name under it
  (Caption to Title, about a sixth of the height), and the state line ("On", "Off", "Unavailable",
  one step smaller) where the height allows; a wide strip puts the icon left of the name and state.
  The text is planned for the language's longest state line, so switching moves nothing. Icon 16–128.
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
  button** (`round`, [S-3917]) and **Switch** ([S-3918]). Every style is drawn over the same whole-tile
  hit target, so the click gate ([S-3905]), Space/Enter under the lease, edit mode, the tooltip and
  "never a header" ([S-3901]) are the same in each; only the drawing differs. Every style (and the
  power widgets') insets by `ButtonInset`: the card's standard 16 (`Fluent.Space4`), 8 while the
  tile's short side is under 128.
- **[S-3917]** Round button (`RoundButtonFace`, the part the power widgets' Round style shares,
  [S-1919]): a circle holding the icon, the name under it (Caption to Subtitle, about a sixth of the
  height) from Regular width; XS is the circle alone. The circle takes the rest of the box (32–320,
  even, 0.85 of the room), the icon two fifths of it; L and XL get a bigger button, never more parts.
  On, the circle is `AccentFillColorDefault` (Secondary on hover, Tertiary pressed) with the icon in
  `TextOnAccentFillColorPrimary`; off, `ControlFillColorDefault` with a control stroke (Secondary
  and Tertiary fills on hover and press); not known, the secondary text colour; unavailable,
  `ControlFillColorDisabled` and the disabled text colour. The tile behind it stays clear.
- **[S-3918]** Switch (`SwitchFace`): Windows' toggle switch drawn (not a `ToggleSwitch`, which would
  take the press): a pill track in Windows' 2:1 (40×20 at Body, the knob 12 of 20 at rest and 14
  under the pointer); off, the `ControlAltFillColorSecondary` track with a strong stroke and the knob
  left; on, the accent track with the knob right in the on-accent colour; under the pointer the tile
  takes the subtle fill, as a Settings card does; unavailable, the disabled track, stroke and knob.
  One level per step of the type ramp sizes everything together: the name on that step, the state
  line a step below, the track as high as the name's line (never below 20, at most 80), the icon 1.5×
  the line, the gaps 8 to 24 on the 4 px grid. The face takes the largest level at which icon, name
  and switch fit the box inside the inset ([S-3916]); only where none does it drop the icon, then the
  name; the state line is added where it fits at that level. So the content grows with the tile in
  steps and never falls to a smaller one while the next still fits, and the level never drops as the
  tile grows at one inset. A **column** (icon, name, state, switch) is centred as one group in both
  axes, every part on the centre line; a **row** (a Settings row: icon, then name over state, at the
  left; the switch at the right edge of the inset) has every part centred on the row's height. Both
  are planned and the higher level wins, a tie going to the row where the box is at least twice as
  wide as high. Every part's box is Core's (`SwitchFacePlan`), so the view only places them. The
  text is planned for the language's longest state line, so switching moves nothing, and a planned
  line always fits its box (no ellipsis by design).
- Needs a live pass on real hardware: every switch is untested against this PC's state by design
  (no test touches the user's radios or settings).

## Windows' panels: notifications, focus session, brightness (WINDOWS-PANELS)

Three widgets after Karpo's (`karpo-app`, docs\widgets.md §18, §19, §35, §92, §100, §103), each on
Windows' own state. Core: `NotificationRules`, `FocusSessionRules` (+ `FocusQuietHold`),
`BrightnessRules` (+ `NewestLevels`), `Shell/WinRtIid`, configs in `Model/Configs/WindowsPanelConfigs.cs`;
App: `NotificationsView`, `FocusSessionView`, `BrightnessView` (each with a `.Preview.cs`),
`Services/Notifications/NotificationFeed`, `Services/Focus/*`, `Services/Brightness/*`; strings in
`Locales/panels/`; tests `WindowsPanelsTests`.

### Notifications

- **[S-4400]** Kind `Notifications` (121), add menu Calendar & mail ([S-3511]), 320×320, glyph the bell. No
  settings (`NotificationsConfig` is empty, so no page, [S-1750]); never needs setting up.
- **[S-4401]** The list is Windows' notification center's, read with `UserNotificationListener`
  (`GetNotificationsAsync(Toast)`): the sender's AUMID and name (`AppInfo`), the toast's texts (first
  non-blank the title, the rest the body, control characters dropped, title ≤ 200, body ≤ 600), when it
  came; newest first, at most 100. Karpo reads Windows' database (`wpndatabase.db`) for the whole toast
  and dismisses through the listener; Themia, a Store app, reads only through the listener, so a toast's
  buttons and reply box are not offered — a click opens its app instead ([S-4406]).
- **[S-4402]** Grouped by app as Windows 11's center: a heading per app (its icon, its name, a × that
  clears the app's), the apps in the order of their newest, each app's newest first (two of one moment:
  the higher id first). A group of more than three shows its newest three and "See more (n)"; "See
  fewer" folds it again; what is opened is the showing's. **Clear all** is the header's trailing button
  and clears Windows' own list (`ClearNotifications`); a heading's × and a row's × go through
  `RemoveNotification`.
- **[S-4403]** Access: a packaged Themia declares `userNotificationListener` (Package.appxmanifest).
  Windows' answer is Allowed (the list), Denied ("Notification access is off for Themia" and **Open
  settings** → `ms-settings:privacy-notifications`), Unspecified (never decided: **Allow** calls
  `RequestAccessAsync`, Windows' own prompt; a no opens the privacy page) or no listener at all
  ("This version of Windows has no notification list to read", no button). Until Windows answers the
  widget draws its frame only. An unpackaged Themia (F5, publish.ps1) is answered by the privacy page's
  notification access, as Karpo found; access has no change event, so it is looked at again when the
  pointer comes onto the widget.
- **[S-4404]** The ladder (`NotificationRules.Plan`): Compact width or under 64 DIP of height, the bell
  and the count (the bell alone and dimmed with none; a click on it opens the privacy page where access
  is missing); otherwise rows, flat with each row's app icon up to Regular height, grouped from
  Expanded; the body takes no line at Compact height, 1 at Regular, 2 at Expanded, 3 at Huge; "No new notifications" when empty.
- **[S-4405]** A sender Windows names nothing gets a name from its id
  (`NotificationRules.FallbackName`, Karpo's `NotificationText.FallbackName`): "Windows" for Windows'
  own (`Windows.*`, `Microsoft.Windows.*`, none), a program path's file name, else the last real word
  of a dotted or packaged id (`5319275A.WhatsAppDesktop_…!App` → WhatsAppDesktop). Icons come from the
  shared app-icon service as `shell:AppsFolder\<AUMID>` (packaged apps included, [S-3960]); the bell
  until one comes, and where none does.
- **[S-4406]** A click on a notification opens its app: `ShellLauncher.OpenApp`, the AUMID's Apps-folder
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
- **[S-4409]** Keys while the widget holds the lease: Up/Down, Page Up/Down, Home/End move the row
  cursor (`ListKeys`), Enter opens the app, Delete dismisses, the Menu key opens the row's menu (Open,
  Dismiss, Clear all from the app), Escape lets go. In edit mode nothing is opened or cleared.

### Focus session

- **[S-4420]** Kind `FocusSession` (122), add menu Time ([S-3511]), 240×200, glyph Karpo's focus
  target (F272). Config: `Minutes` (5–240, default 30, Windows' own) and `DoNotDisturb` (default on);
  a length out of range in a hand-edited file is pulled into it and logged.
- **[S-4421]** One session for the app, as Windows has one: started from any focus widget at its own
  length, shown by all (`FocusSessionService`). It runs from Start to its end and ends there (a
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
- **[S-4426]** The ladder (`FocusSessionRules.Plan`): Compact both ways, the time alone (a click on it
  starts or ends); otherwise the Start/End button (accent) under it, its word where the width is
  Regular or more, −/+ beside it while idle; the status line (do not disturb, Windows' focus, or what
  Start will do) from Regular both ways; the ring of the time gone from Expanded both ways and 120 DIP.
- **[S-4427]** Keys: Space/Enter start or end; Up/Right/+ and Down/Left/− change the length while idle.
- **[S-4428]** Settings page: Length (slider, 5-minute steps) and Turn on do not disturb.

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

- **[S-900]** Duplicate a view (its group on the Views page and that group's menu, the toolbar's View menu, a view bar chip's menu in edit
  mode): the copy goes right after the source, named "<name> (Copy)", with its icon and a deep copy
  of every widget under fresh ids. Data a widget only refers to (a note, an
  account) stays shared, as in the original. The copy is shown on the display asked from; it counts
  against the licence as one view plus its widgets.
- **[S-901]** Reorder views by dragging a chip on the view bar (edit mode), by dragging a view's row
  on the Views page ([S-1703]), or Alt+Shift+Up / Down on a focused row there ([S-3802]). No menu has Move up / Move down. By id, never by bar index, so a bar showing a filtered subset moves the right view.
  The order is the bar's, the Views page's and Ctrl+n's.
- **[S-902]** Deleting a view (never the last) repairs everything naming it in the same change:
  display assignments (the display then gets an existing view again, [S-1705]), the active view, the view for
  new displays (back to null) and every bar whitelist (one left empty means all views again).
- **[S-903]** A view's icon is picked in its group on the Views page from the shared glyph grid
  (`IconChoice`), whose default cell shows the view's initials — what a view without an icon shows.
  The icon is drawn by the view bar, the toolbar's View menu and the row's
  header on the Views page (a view without one shows the shared view glyph there), in the view's
  colour ([S-3810]).
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
  View bar style ([S-5400]), View bar position (8 anchors), View bar size, Show view names, and
  "Views on the bar" (one drop-down button, [S-1708]). Its Shows drop-down assigns through `ShowViewOn`.
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
  long list scrolls inside it; the edit bar's View menu is capped the same way below the toolbar.
  The overflow chip is part of the bar's box, so it is in the hit region ([S-912]); it is never
  dragged, and chips are reordered among the chips drawn ([S-914]). Chosen over a scrolling strip
  with chevrons: a strip hides the shown view as easily as any other and needs a scroll position
  per display; the overflow keeps every drawn chip one click away and is how Windows overflows a
  command bar.
- **[S-919]** Debug render: `Themia.exe --viewbar-png <outDir> [--views n] [--theme light|dark]`.
  With `--views n` (1..60) it draws n views, the shown one two thirds along, at all eight anchors
  with names and icon-only (the sizes in turn) and two edit-mode shots, and the overflow menu of the
  bottom-left shot; it prints each bar's box and flags one outside the work area.
- **[S-5400]** Each display's bar has a style (`ViewBarSettings.Style`): Chips (the default, the bar
  above) or Drop-down. Set by "View bar style" in the display's group on the Displays page, after
  Show the view bar. At load an unknown style resets to Chips and is logged as a repair.
- **[S-5401]** A drop-down is one raised button: the shown view's icon (or initials) in its colour,
  its name (left out when names are off, cut to the room the bar has) and a chevron; a display
  showing a view the bar does not offer reads "Views". It is a row at every anchor, so its room is
  the work area's width (`ViewBarRules.IsVertical` / `Room` with the style). Anchors, sizes, the
  edit-mode grip and drag ([S-913]), placement inside the work area ([S-909]), the hidden preview
  ([S-907]) and the hit region ([S-912]) are the chip bar's. In edit mode the button takes the
  keyboard like a chip (F6, Enter / Space opens it).
- **[S-5402]** A click opens the bar's one menu (`ViewBar.ViewMenu`, the overflow chip's menu of
  [S-918]): `ViewBarRules.MenuViews` lists every view on the bar for a drop-down, the views left
  off for the overflow chip. Every view with its icon in its colour, in bar order, the shown one
  ticked and focused; it opens into the display and scrolls within the work area there
  (`MenuSide`, `MenuHeight`). It is a standard flyout, opened and closed (a click outside, Escape,
  a pick) as the overflow menu is, with or without edit mode; arrows and Enter are the menu's own.
- **[S-5403]** Type-ahead in the menu: a letter moves to the next view after the focused one whose
  name starts with it, wrapping, so the same letter steps through them (`ViewBarRules.TypeAhead`;
  case-insensitive, no timer).
- **[S-5404]** `--viewbar-png` ends both sets with drop-down shots (bottom left M, top right L icons
  only, left middle XL, top centre M in edit mode), each closed and with its menu open
  (`…-dropdown-…-menu.png`); `--theme` and `--lang` apply.

Not ported: the original's chip "drag-over" highlight (the chip itself moves instead); the free-tier view-limit
tooltip on the bar (licensing refuses through `AllowCreate`). Debug render:
`Themia.exe --viewbar-png <outDir>` draws the real bar, placed by the real rule, at nine
anchor/size/mode combinations on a stand-in display with a taskbar strip ([S-919] for many views).

## The Displays and Views pages (R4-NAV, R5-PAGES)

Every display's and every view's settings, each on one page, and how a display is given a view.
Owning files: `Shell/Config/ConfigPages.Displays.cs`, `ConfigPages.Views.cs`,
`ConfigWindow.Navigation.cs`, `ConfigTarget.cs`, `AppHost.Displays.cs` (`SettingsSurface`,
`EditView`); rules in Core `DisplayPlan.AssignViews`, `ViewOps.Rename`, `SettingsExpansion`,
`SettingsPageSync.Shape` (tests `DisplayPlanTests`, `ViewPerDisplayTests`, `ViewsTests`,
`SettingsListPagesTests`).

- **[S-1700]** Retired by [S-3800]: a view's group listed its widgets and held Add a widget,
  Duplicate and Delete rows.
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
  through the shared `ItemMenu`: Show on this display (off when it already shows there); Edit layout;
  Duplicate view (shown on this display, [S-900]), Delete view (danger,
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
- **[S-1830]** The navigation pane lists pages only: General, Appearance, Accounts, Language & region
  ([S-3797]), then **Displays** and **Views** (in that order, with Windows Settings' screen and
  content-view colour icons, [S-1600]), then Widgets, and License and About at the foot. No entry per view, display or widget, so no entry
  menus and no dragging in the pane.
- **[S-1831]** A page target is a page plus, on Displays or Views, the item whose group to open
  (`ConfigTarget.App(Page, Item)`; `ConfigTarget.ForView`). Everything that used to open one view's
  page (a view bar chip's View settings) opens
  the Views page with that group opened and scrolled to the top; the reader's other groups stay as
  they were. While a widget's page is open the Views entry stays selected, and the title is the
  breadcrumb "Views › Desktop › Clock": Views opens the page, the view's crumb opens it on that
  view's group. Clicking an entry (not a selection change) navigates, so Views opens its page even
  while it is already selected under a widget's page; clicking the entry of the page on screen
  leaves it as it is.
- **[S-1832]** The Displays page has one `FluentLayout.Expander` group per connected display ([S-853];
  "· Themia is off here" after the resolution of one switched off); the Views page has Add view at
  the top (a new, empty view at the end of the order, shown nowhere yet; within the licence,
  [S-1065]; its group opens), then one row per view in the user's order ([S-3800]). Search offers a row inside a group as "Views › Desktop" (or the
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

- **[S-3800]** The Views page lists each view as one row (a `FluentLayout.Expander`): its icon in its
  colour ([S-3810]), its name, its widget count, and the "…" button. Opened, it holds the view's own
  settings only: Name, Icon, Colour. Its widgets are not listed (they are arranged on the desktop);
  Edit layout, Duplicate and Delete are in its menu. A note under the list says how to reorder.
- **[S-3801]** No menu reorders by arrows where dragging exists: the view's menu ([S-1704]) and the
  view bar chip's menu ([S-915]) have no Move up / Move down.
- **[S-3802]** Keyboard reordering, the Windows way (the Start menu's pins): Alt+Shift+Up / Down on a
  focused view row moves it one place (`ReorderKeys.Delta`: exactly Alt and Shift; with Ctrl, or an
  arrow alone, nothing). At either end nothing happens. The move runs after the key, through
  `AppHost.MoveView` (one undo step), and the moved row has the focus again after the rebuild.
  The same drag and shortcut (`RowReorder`, one implementation) order every settings list: the list
  editors (quotes, feeds, shortcuts, screen-time limits) and the mail row view's
  columns (the ones switched on). A row is dragged onto another (it takes that one's place,
  `DeskListOrder.MoveTo`); a press in one of its controls never starts a drag. A hint under a list of
  two or more says so. No list, settings page or widget menu has arrow buttons or Move up / Move
  down / Move to start: in the Tasks, Habits and Shortcuts widgets the keyboard lease carries the
  shortcut as `WidgetKey.MoveUp` / `MoveDown` (`WidgetKeys`), moving the focused task, habit or the
  cursor's shortcut.
- **[S-3810]** A view may have a colour (`ViewState.Color`, `#RRGGBB`, null = the theme's text colour),
  set on its row with the shared colour picker (`ViewOps.SetColor`: normalised, anything else refused,
  empty clears). Its icon or initials are drawn in it on the view bar chips (shown or not), the Views
  page, the Shows and New displays drop-downs, "Views on the bar" and the edit bar's View menu (where
  a coloured view without an icon shows the view glyph). Duplicate and undo carry it.
- **[S-3811]** On load a colour is normalised (`#abc` → `#AABBCC`); one that is not a colour is
  dropped (the theme's), with a repair line.
- **[S-3812]** The Views and Displays pages' signatures include each view's icon and colour, so a
  change made elsewhere catches them up; a colour keeps the Views page's shape (no rebuild under the picker).
- **[S-3832]** "New displays show" ([S-847]) heads the Displays page: a setting of every display to
  come, not of one display's group. Each view in it carries its coloured mark.
- **[S-3833]** The General page holds start with Windows, desktop icons and game mode (the language
  is on Language & region, [S-3797]). No
  Edit layout switch (edit mode starts on the desktop or from a view's menu) and no local-network
  switch ([S-3840]).
- **[S-3834]** The navigation pane has no "Themia" heading; the title bar keeps the app's mark and name.
- **[S-3830]** The Appearance page: Style, then four groups, each a `FluentLayout.Expander` whose
  headline setting is the header's own control: Theme and colours (theme; accent, widget fill and
  opacity, header fill and opacity), Widget cards (border style; corner radius, border width, colour
  and opacity), Text and size (font, text size, widget scale), Layout (gap, snap to grid); then Motion.
- **[S-3831]** App page descriptions are one line where the words allow it.
Needs a manual pass: dragging a view's group onto another on the Views page; each "…" menu action
(Show on this display, Edit layout, Duplicate, Delete); Alt+Shift+Up / Down on a row, focus kept and the right-click menu; View settings from
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
  secondary message (4 lines max), the action as a button (on the title's line when there is no
  message, so "Deleted 3 widgets · Undo" stays one row; under the message otherwise), a close button; 364 DIP wide (never
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
  shows "This widget stopped working.", Retry, and Details: a flyout with the exception type and
  message (scrolling past 240 DIPs) and **Copy**, which puts the widget's kind and the whole
  exception with its stack on the clipboard, through the log's redactor ([S-983]). The flyout is a
  windowed popup, never cut off at the card's edge by the surface's region ([S-851]; user
  report). Logged with the widget id and kind. Retry builds the view again in place. Below 160 DIP
  high the glyph goes; below 150×110 DIP the Details link goes too and the line shortens to
  "Stopped working." (Retry always stays).
- **[S-892]** Settings › About › Reset all settings (confirm): the current state is written to
  `state.before-reset-<stamp>.json` beside the state file (if that fails nothing changes and an error
  notice says why), then views, widgets, appearance and preferences become a fresh profile's (Start
  with Windows is kept, as on import); the undo history is cleared; a Success notice. Accounts,
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

- **[S-1620]** Pictures (Gallery widget): a slide can be held for as little as **1 s** (`GalleryConfig.MinSlideSeconds`;
  the settings slider starts there), as the original's S-09/S-18. Stored values are clamped to
  1..3600 s.
- **[S-1621]** Habits are **reordered by dragging a row** in the widget (original S-13), beside the
  keyboard's Alt+Shift+Up / Down ([S-3802]). A press arms nothing until the pointer has moved 5 px
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

Original: `widgets/office/folder` on `main` (SPEC S-01..S-67). One kind, four styles ([S-4123]). View: `Widgets/FolderView{,.Render,.Input,.Commands}.cs`;
file interop services (reusable by Shortcuts and the shell): `Services/Files/{DragDropInterop,FileClipboard,ShellFileOps,
ShellContextMenu,ShellNewTemplates,FolderWatch,FileIcons,GitStatus}.cs`; rules in Core `Widgets/Folder{Columns,Filter,
Selection,Gesture,Layout,History,Badges}.cs` and `Files/{FileDrops,DropFiles,FileDescriptors,FileNames}.cs`; tests
`FolderBrowserTests` (+ `FolderKindsTests`, `FolderRulesTests`, `FolderFormattingTests`, `FolderPathTests`).

- **[S-1320]** Settings (orig. S-14..S-24, S-58), one config for every style ([S-4121]): `Path` (empty = profile), `ItemSize`
  ExtraSmall/Small/Medium/Large (rows 24/28/32/40, icons 12/14/16/20), `TileSize` Small/Medium/Large (80x90/110x120/150x160,
  icons 32/48/64), `Columns` (ordered; default Size, Modified, Type; `Repair` drops unknown/repeats loudly),
  `Navigation` Explorer/Navigate, `ClickMode` (`ItemClickMode`, default Double), `Sort`/`Descending`,
  `ShowHidden`, `ShowExtensions` (default off), `Style`. `ShowDetail`/`MaxCount`, and since [S-4121] `View`, `StretchTiles`,
  `Filter` and `FoldersFirst`, are gone (no compatibility).
- **[S-1321]** The header search (orig. S-21, S-50): comma-separated keywords, OR, case-insensitive substring; it is the
  only filter (the stored keyword filter is gone, [S-4121]). Names hide the extension unless `ShowExtensions` (folders and
  dot-files keep theirs). Folders always come first, as in Explorer. Sort and search re-draw what is in hand; only the
  path re-reads the disk.
- **[S-1322]** Details columns: Size, Modified, Type (shell type name, `SHGetFileInfo`, cached per extension), Cloud, Git,
  Items (folder entry count, counted off-thread up to 1000, shown "999+"). Shown in the user's order while each fits beside
  a 120 DIP name; the first that does not fit ends the row. Header click sorts Name/Size/Modified (again = reverse, saved);
  the columns are chosen in the settings only ([S-1746]; the header has no menu of its own).
- **[S-1323]** Ladder (`FolderLayout.Plan`): one row / one tile row always; an open search box; the details column header;
  list view adds unlabelled Modified (W >= Regular), Size (>= Expanded), Type (>= Huge), drawn right to left ([S-6411]); at Expanded x Expanded a breadcrumb
  (tail first, clickable), no status footer ("24 items · 2 selected", retired [S-666]); Huge width: grid tiles one size
  up. Grid columns = floor(width / tile), stretched to fill the row; rows keep the preset height (no stretch, [S-4121]);
  Icons view packs tiles to icon + 24. Over 1000 items: the first 1000 and a "N more in Explorer…" row.
- **[S-6411]** The rightmost column ends at the row's right inset, the same as the header's buttons, at
  every width and in every style (user report 2026-10-16: from 400 DIPs the list put Size last, blank for
  every folder, so the dates stopped a column short of the edge). The list's unlabelled columns are drawn
  Type, Size, Modified (Modified, filled by every row, last) and Size is left out when nothing shown is
  a file (`FolderLayout.ListColumns`); in Details the last column, whatever it is, is drawn from the right
  of its slot, header included (`FolderColumns.AlignsRight(column, last)`). Grid and Icons stretch their
  tiles to the row ([S-1323]).
- **[S-1324]** Real shell icons (`IShellItemImageFactory`, shared per extension except exe/lnk/ico/url…, customised folders
  per path) and image thumbnails in Grid/Icons (`SIIGBF_THUMBNAILONLY`; never for cloud placeholders). Caches bounded and
  shared; extraction serialised (orig. S-64). Cloud badge from attributes (cloud-only / always kept / available) over the
  icon and in the Cloud column; "Always keep on this device" / "Free up space" set the pin bits.
- **[S-5175]** Icon and thumbnail caches (`LoadCache`) keep only what a load found: one load per key in flight, and a
  load that found nothing is asked again on the next render, never kept as "no icon" (orig. #4/#16: a transient
  extraction failure left a type's items as glyphs until restart).
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
  a band selecting every overlapped item, live ([S-6412]..[S-6415]).
- **[S-6412]** Where a band starts (`FolderPress`, `FolderLasso.Classify`), as Explorer's full-row Details view (user
  report 2026-10-16: rows fill the width, so a full folder had nowhere to start one): only an item's icon and name carry
  the item (click selects, drag carries it out, [S-1332]); a press on the rest of a row or tile, or between or below the
  items, draws the band once it passes the dead zone. A click there selects that item (never opens); on the background
  it clears. The content is at least the viewport tall, so the band can be drawn down to the bottom. Works outside edit
  mode (the press takes the keyboard lease as every widget press does, [S-800]); the band captures the pointer.
- **[S-6413]** How the band combines (`FolderLasso.Mode`, `FolderSelection.Band`): no modifier replaces the selection
  with what the band covers; Shift adds it to the selection the band started with; Ctrl toggles each covered item against
  it (Ctrl wins with both). Live: shrinking the band gives items back. The focus goes to the last covered item.
- **[S-6414]** What it covers (`FolderLasso.Hits`): every item cell the band overlaps at all, from the layout's geometry
  (rows the list's width × row height; tiles the plan's cells), so items not built yet ([S-1887]) are selected too. The
  band is kept inside the content. Look: the accent's 1 px border over a 25 % accent fill, above the items.
- **[S-6415]** Edge scroll (`FolderLasso.ScrollVelocity`/`Step`): with the band's pointer within 24 DIPs of the list's top
  or bottom (a quarter of a short list), or past it, the list scrolls that way at 20 DIP/s per DIP of depth, at most
  2400 DIP/s, and the band follows. A frame tick (`CompositionTarget.Rendering`) runs only while the pointer is in a
  zone with the band down; a frame step is at most 0.1 s, so a stall never throws the list.
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
- **[S-6416]** The item menu is Windows 11 File Explorer's (user report 2026-10-16: it did not look like it), its
  commands per selection from `FolderMenu.Item` (Core, tested), drawn by `ItemMenu` ([S-1740]). Icon row: Cut, Copy,
  Paste (a single folder, the paste target; greyed with nothing to paste), Rename (one item), Share (files only, the
  shell's `Windows.ModernShare`), Delete. List: Open (a file's glyph, an open folder's), Open with > (one file: the apps
  `SHAssocEnumHandlers` recommends, at most 8, with their icons when the icon is an exe on disk, then Choose another app),
  Open in new window (one folder, navigate mode: Explorer), Open file location, Pin to Quick access (folders, shell
  verb `pintohome`), Pin to Start (one folder, exe or shortcut, `pintostartscreen`), Compress to ZIP file, Copy as path
  (quoted), Properties (Explorer's wrench); the cloud pins (a cloud item selected); Show more options. A shell verb the
  shell does not offer here (an already pinned folder) says so at the widget. Not ported, as not Explorer's built-ins:
  Open in Terminal, Open in new tab; app-added lines live under Show more options.
- **[S-6417]** Keys beside the lines are Explorer's, and only keys the widget answers through the lease ([S-1327]):
  Enter, Ctrl+X/C/V/A, F2, Delete, Ctrl+Shift+C on Copy as path (the selection's quoted paths), Alt+Enter on Properties
  (the selection's, or the shown folder's with nothing selected), and Shift+F10 on Show more options, which opens the
  shell's full menu as in Explorer (the Menu key opens ours). Written by [S-1753] (user report 2026-10-16: the
  shortcut naming did not match Explorer's).
- **[S-6418]** Background menu (`FolderMenu.Background`): Paste, Select all; Up (navigate mode), Refresh; New >; Open in
  Explorer, Copy as path, Properties; Show more options. Its lone Paste stays a labelled line ([S-1741]). No View or
  Sort submenus, though Explorer has them: how a widget looks is its settings' ([S-1746]).
- **[S-6419]** Compress to ZIP file (`ZipFiles`, `System.IO.Compression`, off the UI thread): one new ZIP beside the
  items, folders with all they hold (hidden files too; directory links not followed), named after the item the menu was
  opened on without its extension, made unique ("a (2).zip", `FolderMenu.ZipName`), then renamed in place as Explorer
  does. A failure removes the half-written ZIP (its own output, nothing of the user's) and says so at the widget.
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
- **[S-4120]** *(Retired: one widget kind per layout; they are one kind with four styles, [S-4123].)*
- **[S-4121]** Each style shows only what it draws (user decision 2026-10-12;
  `FolderConfig.ShownSettings`, [S-6070]), and the config holds the union, a style ignoring the rest (`Rows`,
  `Tiles`, `Extensions`, `DetailColumns` read a neutral value where the style draws none). Every
  style: the folder, Open files with (double / single click), Open folders (Explorer / inside the
  widget), Show hidden items. List: row size, file extensions, Sort by + Reverse. Details: row size,
  columns, file extensions; no sort row (its column headers sort, saved). Grid: tile size, file
  extensions, Sort by + Reverse. Icons: tile size, Sort by + Reverse; no extensions (it draws no
  names; a rename there edits the stem). One tile size for grid and icons (default Medium). Picking
  another style rebuilds the page ([S-6071]). Retired for every style: the stored keyword filter (the
  header search covers it), Folders first (always on), Stretch tiles (tiles keep their preset height).
- **[S-4122]** Debug renders: `--widget-png Folder <out> --style list|details|grid|icons --state web|wallpapers`
  lists `%WINDIR%\Web` or its Windows wallpapers (read-only) instead of the profile;
  `--settings-png Widget:Folder <out> --style <name>` its settings page in that style.
- **[S-4123]** One kind, **Folder** (`Folder`, 17, `FolderConfig`, `folder`, 300×280, glyph E8B7), with
  four styles ([S-6000]; `FolderStyle`, stored as `"style"`, menu order): **List** (the default,
  E8B7), **Details** (F12B FolderHorizontal), **Grid** (ECA5 Tiles) and **Icons** (F0E2 GridView).
  One view (`FolderView`) reads the style from its config: a change re-lays and re-draws what is in
  hand, the listing, selection and history kept. Folder details, Folder grid and Folder icons were
  kinds of their own (114..116); a state file holding one drops that widget at load ([S-1050]).
  Their names still find the folder in search ([S-943]).

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
  with the style, the length of the whole animation (100..5000 ms) and a ▶ replay, then the Smooth
  value changes toggle ([S-6329]). Any change replays
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
- **[S-1743]** A widget card's menu in edit mode: the row is Cut, Copy, Paste (disabled with nothing to paste),
  Settings and Delete, acting on the selection ([S-1592]); the list is the view's own
  commands, Bring to front / Send to back, and Edit layout / Done editing. There is no Move:
  dragging and the arrow keys move a widget. There is no
  Duplicate: Copy then Paste already does it ([S-1589]), and one command is enough.
- **[S-1758]** Outside edit mode the card menu is for using the widget, not arranging it: the view's own
  commands (Refresh, Pause, which folder, [S-1746]), Settings (a labelled line, [S-1741]; none for a widget
  without a page, [S-1750]) and Edit layout. No Cut, Copy, Paste, Delete, layering or icon row: with no
  selection shown and nowhere to paste outside edit mode they did nothing the user could see (user report
  2026-10-17: "I right click and cut a widget, nothing happens"). Item menus inside a widget (a file's
  Explorer commands, [S-6416]) are the widget's own and unchanged; empty surface stays click-through ([S-1744]).
  `ShellMenus.ForCard`; tests `S1758_`.
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
  the widget's settings page only (each one already has a row there); the card's own header,
  background and title are set on its properties strip ([S-3707]). The menus keep commands the user
  runs while looking at the widget: refresh, pause, open, which folder, the click mode, search.
- **[S-1747]** Debug: `--menu-png <outDir> [--theme light|dark]` opens the card menu (edit mode, one
  widget; desktop, three widgets; desktop, a widget with view commands and one without settings, [S-1758]), the surface menu, two sample item menus and the folder widget's file, Open with,
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
- **[S-1750]** A widget whose settings page would have no rows has no Settings: the card menu
  leaves it out of the row, the properties panel has no More settings row ([S-6009]), and Enter
  ([S-1974]), a click while the settings window is open ([S-1745]), the settings search and every
  other way in do nothing for it. With the panel's properties off the page ([S-6536]) that is the
  CPU, memory and battery details, the audio visualizer, the seven power buttons, the thirteen
  quick-setting toggles, Year progress, Stopwatch, Removable drives, Notifications, Recent files and
  the Recycle bin; the GPU details on a PC with one adapter; and the CPU, memory, battery and network
  meters in a style that draws no history (their page is the history's span, [S-6074]). Whether a
  page has rows is read off the page builder itself (a throwaway widget of the same kind and page
  shape, [S-6071], once per kind and shape), never a list kept beside the pages, so the two cannot
  drift. The edit bar has no per-widget Settings button.
- **[S-1752]** A section heading with no row under it before the next heading or the page's end is
  left out (`PageSections.Keep`), so a style that offers none of a section's rows leaves no heading
  over nothing, and a page of headings alone has no rows ([S-1750]).
- **[S-1751]** In edit mode a widget's own content is inert (`CardGestures.ContentTakesInput`): the
  card turns hit-testing off for its header and view, every widget's alike, so a press, right-click,
  long press or hover lands on the card and selects, drags ([S-1745]) or opens the card menu
  ([S-1743]). No item menu (a mail, a file, a shortcut, a task), hover action, row click, button or
  drag out of a widget fires there; outside edit mode they all work as before. One guard on the card,
  not one per view.

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
- **[S-1810]** Showcase views: one view per widget kind in the registry's order (by id, [S-6902]), named after the widget
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
  (`WeatherInterests.For`, or the stock config's `Interest()`: the same rule), draws what is held, hears `WebSync.Updated`, and its tick only
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

## Shared widget settings: region and updates (SETTINGS-AUDIT)

What every widget shares instead of carrying its own copy, and the audit of every widget page that
moved it there. Core: `RegionFormat` (`Localization/RegionFormat.cs`), `UpdateCadence`
(`Sync/UpdateCadence.cs`), `RegionPreferences` / `RefreshPreferences` (`Model/AppPreferences.cs`).
App: `ConfigPages.LanguageRegion.cs` (Settings › Language & region: the formats), `ConfigPages.Widgets.cs`
(Settings › Widgets: the updates), `AppHost.Region.cs`, `Interop/SystemRegion.cs`.
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
  source, set in Settings › Widgets › Updates: Weather (now and forecast, rain, air quality: [S-5101]), Stocks, News feeds, and
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
- **[S-3792]** Every gallery re-reads its folder or Bing's photo hourly (`GalleryConfig.RescanInterval`);
  "Refresh images" reads now. No refresh setting.
- **[S-3793]** Local readings have no refresh setting: meters and system details read on the system
  hub's one cadence ([S-1120], [S-2401], [S-2503]); a stored `intervalSeconds` is ignored. Bluetooth
  enumerates every 30 seconds ([S-3794]). The camera's snapshot interval stays the stream's own
  ([S-470]): it is how a camera is read, not how often a service is asked.
- [S-3794]: moved with the Bluetooth widget into its module's `SPEC.md` (`src/Widgets/Themia.Widget.Bluetooth/`).
- **[S-3795]** Long widget pages fold related rows into `FluentLayout.Expander` groups: Stocks' period and
  colours (timeframe, high and low, colours; whether a chart is drawn is the style, [S-6165]); the clock's zone (label, offset);
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
- **[S-3797]** Settings › Language & region (as Windows Settings' Time & language › Language & region;
  Segoe's TimeLanguage glyph, the colour `globe` in the pane) is every localisation choice in one
  place, after Accounts where Windows puts it: Language (App display language, [S-1109]), then
  Regional format (time format, first day of the week, temperature unit, [S-3770]). General and
  Widgets hold none of them; Widgets keeps only the update cadences ([S-3780]). There are no other
  units or formats to move: no widget has its own date, number, wind or distance format.

## Year progress (YEAR-PROGRESS)

A module of its own ([S-6911]): its section, [S-4800]..[S-4805] and [S-6040]..[S-6042], is
`src/Widgets/Themia.Widget.YearProgress/SPEC.md`.

## Next meeting (NEXT-MEETING)

Owning files: `Themia.Core/Widgets/NextMeetingRules.cs`, `Themia.Core/Office/MeetingLinks.cs`,
`Themia.App/Widgets/NextMeetingView*.cs`.

- **[S-4820]** `NextMeeting` (125): the one event that matters now from the synced calendars. Its
  config is a `CalendarConfig` (`NextMeetingConfig`): accounts and calendars chosen as every
  calendar widget chooses them ([S-562], [S-567]), so it needs setting up without a calendar
  account ([S-1862]). It reads through the calendars' seam (`ICalendarData`, the offline store and
  the shared sync, [S-2701]) for yesterday to fourteen days on, on the calendars' 5-minute poll and
  the accounts' change event; it never fetches on its own. It never asks for birthdays (all-day,
  never a meeting; `Repair` puts `showBirthdays` back to false).
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
- **[S-4824]** The ladder (`NextMeetingLayout.Plan`): Join when there is a link (always), then the
  when line, then the title (always, without Join), then the title's further lines (two, three from
  300 DIPs of height, where the title is set in Subtitle), the time range, the location, the
  organiser. XS with a link is the when line and Join; a short tile keeps a half inset top and
  bottom. A wide strip stands Join at the right. Join is the accent button;
  a click elsewhere on the event opens it in its calendar ([S-1268]).
- **[S-4825]** On a view ([S-3300]), pointing at the event publishes `HoveredEvent`: the same event
  lights in the calendar widgets there, the others dim ([S-3623]); one another widget points at
  dims this one unless it is the same event.
- **[S-4826]** Its settings page is the calendar widgets' accounts and calendars, with no display
  rows. Debug: `--state sample` (a Teams meeting twelve minutes away), `running`, `none`.

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting
(Join opening the app), a declined invitation, and the hover link with a calendar on the view.

## Recent files (RECENT-FILES)

Owning files: `Themia.Core/Widgets/RecentFileRules.cs`, `Themia.App/Services/Files/RecentItems.cs`,
`Themia.App/Widgets/RecentFilesView*.cs`.

- **[S-4840]** `RecentFiles` (126): the files opened last, as Windows lists them. No settings.
- **[S-4841]** The source is the Recent Items folder (`shell:Recent`, `FOLDERID_Recent`), the
  shortcuts Windows writes on every open and Explorer's Home "Recent" is built from. The newest
  120 shortcuts by last write are resolved (`IShellLinkW` through `ShellFileOps.ResolveShortcut`,
  off the UI thread); a target that is not a fully qualified path, is a folder or is gone is left
  out; a file reached by two shortcuts is listed once, newest first, at most 40. Each row: the
  shell's icon (the shared `FileIcons`, a file glyph until it comes), the name, the folder's name
  and when ("2h"). A debug render never reads the user's list.
- **[S-4842]** The ladder (`RecentFilesLayout.Plan`): compact one-line rows on a Compact height; the
  folder under the name from a Regular width and height; when, as a column at the end from an
  Expanded width (else after the folder). The rest scrolls.
- **[S-4843]** Live: one folder watch on Recent Items for every widget, started with the first and
  disposed with the last; a read is single-flight and runs once more for a change during it. A
  failed read keeps the last list; none at all says the list could not be read. A minute tick keeps
  "2h" honest.
- **[S-4844]** A click opens the file (a gone one reads the list again). The row menu: Open, Open
  file location (Explorer with it selected), Remove from list, which deletes its Recent Items
  shortcut as Explorer's "Remove from Recent" does; the file is never touched, and nothing outside
  the Recent Items folder is ever deleted.
- **[S-4845]** Keys through the lease ([S-1288]): Up/Down, Page Up/Down, Home/End move, Enter opens,
  Delete removes from the list, the Menu key opens the row's menu, Escape lets go.

Needs a live pass: opening files from Explorer and Office (the list updating), Remove from list
(and Explorer's Home agreeing), a file on a disconnected share.

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
- **[S-4864]** The same Empty is on a Shortcuts item pointing at the Recycle Bin ([S-1389], issue
  #120): its item menu offers "Empty Recycle Bin…" after Open, off unless
  `RecycleBinState.CanEmpty` (items in the bin, no empty already out). One implementation,
  `RecycleBinStatus.EmptyAsync`, guards and empties for both; "not twice at once" is process-wide
  (`RecycleBinStatus.Emptying`), so the widget's button and every Shortcuts menu agree. Not on a
  multi-selection. (Tests `S4864_`, `S4863_`.)

Needs a live pass: Empty with Windows' dialog (Yes and No), deleting a file from Explorer (the count
following within 15 s).

## Stopwatch, alarm and drives (KARPO-2)

Four widgets after Karpo (`karpo-app`, `docs/widgets.md` §84 and §92 the stopwatch, §106 the alarm,
§91 the removable devices, §75, §99 and §122 the drive). The stopwatch is a module of its own
(`src/Widgets/Themia.Widget.Stopwatch`, its SPEC.md). Rules in Core (`Widgets/AlarmRules.cs`,
`Widgets/DriveRules.cs`; configs `AlarmConfig.cs`, `DriveConfigs.cs`), tests `AlarmTests`,
`DriveRulesTests`; views `AlarmView`, `DriveView`, `RemovableDrivesView` (+ `DriveParts.cs`), services
`Services/AlarmService.cs` (+ `AppHost.Alarms.cs`), `Services/Drives/DriveCatalog.cs` and
`DriveService.cs`. Strings in `Locales/time-drives`. Kinds: Stopwatch 117, Alarm 118,
RemovableDrives 119, Drive 120.

### Stopwatch

Moved with the widget into its module: `src/Widgets/Themia.Widget.Stopwatch/SPEC.md` ([S-4600]..[S-4605]).

### Alarm

- **[S-4620]** An alarm widget (add menu: Time, [S-3511]) is **one** alarm (user decision; several
  alarms are several widgets): a time (`"HH:mm"`, 24-hour in the file), the days it repeats on
  (`"mon"` … `"sun"`, Monday first; none means it rings once), on or off, a name, the sound (the
  timer's ringtones, default Bell), its level and the snooze (1–30 min, default 10), all in
  `AlarmConfig`. A new widget is 07:00, ringing once (no days), off, and needs no setting up.
- **[S-4621]** Times parse strictly (hours 0–23, two-digit minutes); unknown day words are dropped and
  days are written once each, Monday first; a name is one line of at most 40 characters; a
  hand-edited file is repaired loudly.
- **[S-4622]** The next ring is the alarm's time on the first day after now it may ring on, reckoned on
  the wall clock of the zone in force and kept as a UTC instant. A time summer time skips rings at the
  first minute after the gap; a time winter time says twice rings once, at the first.
- **[S-4623]** The **alarm service** (app lifetime, started by the host, never by a render) keeps the
  alarm of every alarm widget of every view (`AlarmSlot.Of`: one per widget, keyed by the widget's
  id; `AlarmBook`, synced on every state change and every switch on a widget), so an alarm rings
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
- **[S-4627]** **Alarms ring only while Themia runs**, and the settings page says so. One that came due
  while Themia was closed does not ring at the next start (it is set from then).
- **[S-4628]** The widget is the time as its readout, "name · hint" under it, and the switch; while it
  rings (or is snoozed) Snooze and Dismiss stand where the switch was (worded from 300 DIP, else
  glyphs). Off, the time is in the tertiary ink. The card menu has Snooze and Dismiss while it rings.
  There is no alarm list *(the list's rows, [S-1281] fade and per-row keys retired with it)*.
- **[S-4629]** The ladder: the time is reserved, then the switch row, then the words (Caption, Body from
  the Regular height), each as it fits; the time takes the rest (`FluentType.Hero`), centred. A strip
  (2:1, at least 260 wide) puts the switch beside the time and its words, read from the left.
- **[S-4630]** Keys: Space or Enter switch the alarm, or dismiss it while it rings or is snoozed; S
  snoozes it while it rings; Escape lets go. The minute tick turns "Today" into "Tomorrow".
- **[S-4631]** Settings: Time (a time picker in the language's clock), Switched on, Name, Repeats on (a
  button per day in the language's week; none rings once), the note of [S-4627], then the sound
  (previewed once), the volume and the snooze.

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
- **[S-4653]** The capacity bar is the used share in the load's colours (accent, caution from 70 %,
  critical from 90 %; the [S-837] track); under it "419 GB free of 931 GB", or why there are no figures:
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
  and medium); the kind's glyph until it is in, and always for a share (never asked for a picture: that
  is a question to the server). A disconnected share wears a disc with a cross at its foot, in the
  theme's critical colour.
- **[S-4660]** Removable drives (add menu: Devices & power, [S-3511]): a row per USB stick or card (removable), disc
  (optical) and external disk (hot-plug fixed), by letter; an empty reader is left out, as Explorer
  hides empty drives. Nothing plugged in says so, centred, under the stick glyph. No settings page.
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
(the measures the ladders share), `Sync/WebInterests.cs`, `OnlineParts` (the age line, Sdk.WinUI).
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
  Empty. ECB terms: free reuse with the source named (the settings page and the header tooltip do).
- **[S-5142]** The currencies offered are the ECB's 30 plus the euro (`ExchangeRules.Published`).
- **[S-5145]** Every pair is a cross rate through the euro (quote per euro ÷ base per euro); the change
  is against the newest earlier day holding both; a currency missing on the latest day is "Not
  published". Rates print four decimals under 10, three under 100, two above, locale digits.

Shared:

- **[S-5160]** The header's age line, its caution colour and its tooltip follow weather ([S-2612]); the
  tooltip also names the source. Refresh is on the widget's menu ([S-730]).
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

Tests: `tests/Themia.Core.Tests/WeekPlannerTests.cs`. Issue #160.

- **[S-5320]** `WeekPlanner` (kind 133, `WeekPlannerConfig : CalendarConfig`, discriminator
  `weekPlanner`, glyph E762 MultiSelect, 480×280): the days ahead, each with its events, and under
  today the tasks due. One planner: a day's tasks are rows of that day after its events, not a task
  list beside a calendar. Calendar & mail subject, after next meeting ([S-3511]). Events through the
  calendar widgets' seam (`ICalendarData`, [S-2701]); tasks through the shared `TaskService`
  ([S-591]); the widget fetches nothing itself (a 5-minute poll of the synced store, the accounts'
  change events, a minute tick to redraw).
- **[S-5321]** A rolling week: today and the six days after (`WeekPlannerRules.Range`), fetched whole
  whatever the size shows. Days stand side by side as the width allows (140 DIP a column, 8 apart, up
  to seven). One column: today alone on a Compact-height tile (no heading), else the days stacked
  as an agenda's. A column under 120 DIP drops the events' start times.
- **[S-5322]** A day's rows (`WeekPlannerRules.Build`): its events as a calendar day lists them
  ([S-570]: birthdays, all-day, then timed by start), today without the timed events already over;
  an event row is its calendar's colour mark ([S-566]), its start in the region's clock (accent
  while it runs; none for a whole day) and its title; a click opens it as a calendar's does
  ([S-1268]). Under today only: the open tasks due today or overdue, oldest first ([S-596]), with
  the Tasks widget's ring (critical when overdue); the ring ticks the task off through the service
  ([S-597]), inert in a preview and in edit mode.
- **[S-5323]** Whole rows only (`Columns`, `Stack`): a day shows its heading ("Today" in the accent,
  "Tomorrow", then the short date) and as many rows as fit; when it has more, the last row says
  "+N". An empty today says "Nothing planned"; stacked, later days with nothing are skipped and the
  list ends at the first day cut short or whose heading and a row no longer fit.
- **[S-5324]** Settings: every calendar's rows (accounts, calendars, colours, birthdays, [S-561]),
  then Today's tasks: Show (on by default) and Tasks from (the first account with tasks, or one
  account; a chosen account that is signed out stays listed by its id, never swapped, [S-590]).
  Lists kept on this PC are not offered: the planner is the synced schedule.
- **[S-5325]** Setup and sync: it asks to be set up only while neither a calendar account nor (when
  it shows tasks) a task account is signed in; with tasks alone it is a week of tasks.
  `SyncInterests` takes its calendars as a calendar interest and its task account's Today view as a
  task interest, so both sync with everything else.

Needs a live pass: a signed-in Microsoft and Google account (events, colours, opening an event,
ticking a task off and seeing it leave every Tasks widget), a day turning over at midnight, and the
showcase views on the live desktop.
