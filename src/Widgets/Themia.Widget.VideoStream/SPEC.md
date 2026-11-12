# Video stream

The module `videoStream` ([S-6911]): a live camera picture, an MJPEG stream, a snapshot polled on an
interval, or a video Windows plays itself. Its tags are this file's alone; the root `SPEC.md` holds
what it shares and reads of the host, with their root tags: the title ([S-460], the SDK's
`VideoStreamRules`), the reconnect backoff ([S-464]), rotation ([S-468], the SDK's `RotatedBox`), the
refresh interval ([S-470]), the transports and MJPEG ([S-471], [S-472], the host's `StreamSession`
behind `IWidgetHost.Streams`), Space to pause ([S-1991], the SDK's `ControlKeys`) and the HLS cap
([S-6602], the SDK's `HlsVariants`).

Owning files: everything in this folder: `VideoStreamModule.cs` (descriptor, glyph, view factory,
menu lines, JSON context), `VideoStreamConfig.cs` (the address gate, fit, rotation, paused, refresh,
the chrome's two switches), `VideoStreamView.cs` (the session through `IWidgetHost.Streams`, the frames,
Windows' player for a video) with `.Overlay.cs` (the hover strip, the controls, the chip),
`ShownWatch.cs` (whether the card can be seen), `StreamChromeRules.cs` (the chrome ladder, its words,
the frame-rate meter, the Status row's look, the tick's lease), `Preview/` (Debug stills, [S-1314]), `VideoStreamMenu.cs` (its properties-menu lines), `Localization/Locales/` (its own
`VideoStreamStr`, 30 languages). Tests: `Tests/StreamChromeTests.cs`, `Tests/VideoStreamConfigTests.cs`, `Tests/VideoStreamMenuTests.cs`
(run by `tests/Themia.Modules.Tests`); its config under the root's rules is the module host's
`VideoStreamHostTests`.

The widget runs only while it can be seen. A paused camera, or one whose card is not in the tree or is
collapsed (`ShownWatch` follows the card's ancestors), holds no socket and no decoder; `Detach`
releases everything.

- **[S-465]** The camera's right-click menu has **Pause/Resume** and **Refresh** (was [S-11] and the
  original's `contextActions`); rotation is a setting (and an overlay button), not a menu entry ([S-1746]). Refresh reconnects at once with the backoff reset.
  Context actions are a card-level hook: a view implements `IWidgetContextActions` and the card lists
  them in the list under the icon row ([S-1743]), read fresh each time the menu opens.
- **[S-466]** Fit: whole picture (letterbox, default), fill and crop, stretch (was [S-16]). One
  `Stretch` mapping drives both the decoded-frame `Image` and the video player.
- **[S-467]** The stream address is checked by `WebInput.Url` on every read — http/https only, no
  credentials, no UNC, refused rather than repaired — and the menu's address box explains a refusal as it is typed (was
  [S-09]). A refused address shows its reason in the Status row ([S-1314]) in place of the picture; an
  empty one is the Set up button, its sentence "Add a stream" ([S-1862], was [S-02]).
- **[S-469]** Paused is persisted, so a parked camera stays parked across restarts and uses no network;
  it shows "Paused" (was [S-07]).
- **[S-473]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Address** (asked beside the menu, [S-3721],
  starting from what is there; its host at the line's end; checked as typed, [S-467]: "Only http and
  https addresses" for another scheme, `WebInput`'s reason otherwise; stored trimmed; empty clears it
  and the widget waits for setup) and how it is read, **Refresh** (0 to 300 s, asked for as a
  number, [S-3726]; 0 reads "Live" and keeps the stream open); after a divider the picture,
  **Fit** ([S-466]) and **Rotation** (None / 90° / 180° / 270°, [S-468]); after another **Controls
  on hover** and **Status chip** ([S-1312]); and a note: pause, rotate and reconnect are on the
  right-click menu, cameras that need a password are not supported.
  (Tests `VideoStreamMenuTests`.)
- **[S-1312]** Video stream chrome ladder (`StreamChromeRules.Chrome`, `IWidgetSizeAware`). The
  picture is the widget at every size; nothing else ever shows on XS, S or a Wide strip (height
  under Regular). From M (Regular × Regular, so Tall too), with the pointer on the picture, a
  bottom strip shows a state dot, the state and the frame's age ([S-1314] its look). From L (Expanded × Expanded) the
  strip adds Pause/Resume, Rotate and Refresh (the same handlers as the context menu's) and
  the camera's resolution and measured frame rate. At XL (Huge × Huge) a persistent top-left chip
  shows the state and transport. Two switches ([S-473]): `HoverControls` (default **on**: invisible until
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
  shared ticker at 1 s only while the strip is on screen and the stream is live, held once
  (`TickLease`): the ticker fires a new subscription at once, inside Subscribe, and that first tick
  asked again and subscribed again until the stack overflowed whenever the strip first showed on a
  live stream (2026-10-29). (Tests `TickLeaseTests`.)

- **[S-1314]** On the family's blocks (G6, 2026-10-29). A stream with no picture to draw says why in
  the Status block's one row at the top of the content ([S-6714]), never a centred panel
  (`StreamChromeRules.Look`): a refused address or a reply this widget cannot show is a problem (the
  caution warning) with no action, only the menu mends it; a dropped stream is a problem with Retry
  (reconnect now, the backoff reset; why the last attempt failed is in the sentence's tooltip); a paused
  one is plain, the pause glyph tertiary, with no button (Resume is the card menu's, Space's and the
  strip's: a worded button overflows the small tiles); connecting is plain. The strip and the status chip
  are drawn from the tokens over the picture: the solid role with a hairline edge in the control stroke,
  the one radius, 8 in from the picture's edges; the strip a row of 28 at least (8 and 4 inside), the
  chip one Caption line with 8 and 4 around it, shown only while no Status row is (that row says the
  state itself); their words Caption, the state primary and the details
  secondary; the state dot a round 8 in the accent while live, caution while it waits, tertiary while
  paused (no critical red for a working stream, `StreamChromeRules.DotRole`); the strip's Pause,
  Rotate and Refresh the family's 28 icon buttons, clear on it, side by side. The acrylic fill is gone.
  Debug renders draw stills only, never a camera: `--state live|paused|connecting|dropped|refused`
  (a made-up frame, no address reached). (Tests `StreamStatusTests`.)

Needs a live pass: a real MJPEG camera, a snapshot endpoint on an interval, an MP4 and an HLS stream
(the cap at a small tile), a camera that drops off the network and comes back, and Pause surviving a
restart.
