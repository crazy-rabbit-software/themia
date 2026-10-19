# Audio visualizer

The module `audioVisualizer` ([S-6911]): what the speakers play, drawn as a spectrum in six styles.
Its tags are this file's alone; the root `SPEC.md` holds the host's sections and the capture's root
tags: the analyser [S-3201], the bars' fall [S-3202], the one shared capture worker [S-3203] and its
self-healing [S-3204] (the host's `AudioLoopback` behind `IWidgetHost.Loopback`, the SDK's `Spectrum`),
the trace the feed carries [S-3214] and its fade [S-3215] (the SDK's `Oscilloscope`).

Owning files: everything in this folder: `AudioVisualizerModule.cs` (descriptor, view factory, JSON
context; no settings rows, [S-1750]), `AudioVisualizerConfig.cs` (with its six styles),
`AudioVisualizerView.cs` (the capture through its host, frames marshalled to the dispatcher),
`SpectrumCanvas.cs` (the drawing per style), `VisualizerLayout.cs` (the plans: bars, LEDs, the ring,
the wave and the waveform), `Preview/` (Debug renders: `--state playing|unavailable`),
`Localization/Locales/` (its own `AudioVisualizerStr`, 30 languages, its style names included). Tests:
`Tests/AudioVisualizerTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3200]** `AudioVisualizer` (kind 70, `AudioVisualizerConfig`, discriminator
  `audioVisualizer`): the spectrum of whatever the PC plays, split out of Music, which no longer
  draws it. Listed under Media beside Music; glyph `Glyphs.Visualizer` (E7F3; Equalizer is the
  mixer's). Six styles ([S-3216]). Headerless, whatever the style ([S-700], [S-3711]). No settings
  but its style: the spectrum in Music was an on/off switch and nothing else, and placing the
  widget is that switch now, and its style is on the properties panel ([S-6536]), so it has no settings page ([S-1750]). Accent fill (`AccentFillColorDefaultBrush`), never a typed colour.
- **[S-3205]** Layout (`VisualizerLayout.Plan`): one part, the spectrum, edge to edge (user decision
  2026-10-12, like the meters' charts [S-215]/[S-1824]): no inset, the box is the card's whole
  content area and the card's rounded corners clip what reaches them (the 16/12 DIP inset is gone).
  The linear designs run along the long side: standing on the floor of a tile or a strip, lying
  across a Tall column (growing from the start edge, bass at the bottom). The count is the largest
  of 24/12/8/6 (each a whole group of the 24 bands) whose bars stay ≥ 6 DIP thick (LED columns ≥ 10:
  XS 8, S 12, M and up 24); past 24 they only thicken, so a larger tile draws the same bands larger
  (bars: XS 12, S and up 24). Gap = run/100,
  2..6 DIP. A bar is the loudest band of its group; bad levels clamp to 0..1.
- **[S-3206]** Idle is a flat baseline: every bar (spoke, curve point) a 3 DIP stub, the same on
  every tile, no text; the LED columns light their first cell. A device the capture cannot reach
  ([S-3204]) draws that baseline in the disabled fill ([S-1940]), "Audio output unavailable" as the
  tile's tooltip; the spectrum comes back in the accent when the worker reaches it.
- **[S-3207]** Debug renders cannot capture sound: `--widget-png AudioVisualizer --style <name> --state playing`
  draws a still synthetic spectrum, `--state unavailable` the disabled baseline.
- **[S-3211]** A frame writes numbers only, never allocates: a scale per bar or spoke, the opacity of
  the LED cells whose state changed, the wave's Bézier points. Shapes are placed once per layout
  (the LED pool only grows), so the cost per frame is the same as the bars'.
- **[S-3212]** The sixth style (issue #136, a line like an ECG that moves with the music):
  **Waveform** (glyph E95E Health): not a spectrum but the sound itself, one accent line
  (`AccentFillColorDefaultBrush`) swinging about the centre line, like an oscilloscope. Same view,
  drawing and capture; never a header ([S-3711]).
- **[S-3213]** Its plan (`VisualizerLayout.Plan`, `AudioVisualizerStyle.Waveform`): along the long side like
  the linear designs (down a Tall column), a point every 4 DIP (`ScopeStep`) from 16 up to the
  trace's 128 (`Oscilloscope.Points`), the line 2 to 4 DIP thick (short side / 60), the swing the
  short side less the line's half-thickness. `Oscilloscope.Resample` fits the 128-point trace to
  the plan's count keeping each stretch's signed peak. Segments are placed once per layout (a pool
  that only grows); a frame moves their points ([S-3211]).
- **[S-3216]** One kind, `AudioVisualizer` (70, `AudioVisualizerConfig`, `audioVisualizer`,
  320×160), with six styles ([S-6000]; `AudioVisualizerStyle`, stored as `"style"`, menu order)
  sharing the one capture ([S-3203]), the one grouping ([S-3205]), one view (`AudioVisualizerView`)
  and one drawing (`SpectrumCanvas`): **Bars** (the default, E7F3), **Mirrored** (F4C3 MixVolumes,
  bars growing both ways from the centre line), **Wave** (E9D9 Diagnostic, a Catmull-Rom curve
  through the bands with the area under it at 35 % and a 2 DIP line; Bézier controls clamped into
  the box, so a peak never leaves it), **LED** (F232 GridViewSmall, columns of about-square cells,
  at most 24, lit from the base, unlit cells at 18 %; a level lights ceil(level × cells), at least
  one), **Radial** (E706 Brightness; spokes standing on a ring at 42 % of the half short side and
  reaching the nearer edge at full level, the bands top to bottom down each side, the left the
  right's mirror; the per-side count the largest of 24/12/8/6 whose spokes stay ≥ 3 DIP at the ring)
  and **Waveform** ([S-3212]). Spectrum bars, Mirrored spectrum, Spectrum wave, LED spectrum, Radial
  spectrum and Waveform were six kinds (70, 110..113, 132); a state file holding one of the five
  removed drops that widget at load ([S-1050]). Their names still find it in search ([S-943]).
- **[S-3217]** A style change ([S-6005]) replaces the drawing with one of the new style, laid out
  for the tile at once and drawn from the newest frame; the capture lease is held throughout, so
  nothing restarts.

Needs a manual pass: the widget on a live desktop with music playing and stopping (each style), the
output device switched and unplugged while it draws (the capture healing), never in a render: a render
cannot capture sound.
