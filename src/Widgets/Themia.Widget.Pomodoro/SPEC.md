# Pomodoro

The module `pomodoro` ([S-6911]): a focus and break timer that runs through its phases, its ringtone
sounding on each flip. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and
what the pomodoro shares with the alarm (the ringtones themselves [S-1461]..[S-1463], the cue's player
[S-153], reached through the SDK's `SoundCue`; styles [S-6000], the Readout [S-6702], the Controls
[S-6716], the one-readout block [S-12808], the scale-down floor [S-12833], the keyboard lease [S-800]).
It is no longer on the timers' sized face ([S-6739], [S-11300]): it has no Size ([S-8117]).

Owning files: everything in this folder: `PomodoroModule.cs` (descriptor, view factory, menu
lines, JSON context, glyph), `PomodoroConfig.cs` (the style, the phases, Repeat, the sound),
`PomodoroCycle.cs` (the cycle as arithmetic, the nudge, the key map `PomodoroKeys`),
`PomodoroSections.cs` (the sectioned progress's geometry), `PomodoroFace.cs` (the face's plan, its
arrangements, the layout in a room, the sizing), `PomodoroBlock.cs` (the panel that lays the parts where the plan puts them),
`PomodoroProgress.cs` (the sectioned bar and ring), `PomodoroView.cs`, `PomodoroMenu.cs` (its
properties-menu lines), `Preview/` (Debug renders), `Localization/Locales/` (its own `PomodoroStr`).
Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

From the original's own tags (`main`'s pomodoro, its S-01, S-02, S-04; written down at the move
from the tests that pinned them under those numbers, which are the root's geometry tags here):

- **[S-8101]** A phase is 1 to 180 minutes: a stored 0, a negative or a larger number is clamped, so a
  damaged file never flips once a second for ever (the original's S-01).
- **[S-8102]** A fresh cycle is its first phase in full; the readout is the minutes and the seconds
  padded to two ("25:00", "0:05", "180:00"), never a negative clock (the original's S-02).
- **[S-8104]** Progress is the elapsed share of the phase, kept within 0..1 when the lengths
  change mid-phase (the original's S-04).

- **[S-150]** Pomodoro run state — which phase, seconds left, running — lives in the view instance and
  is never persisted. The same widget on two monitors is two independent timers, which is also why only
  a running instance can sound the cue. What persists is the config: the phases, Repeat, the style and
  the sound ([S-8112]), in the state file as every widget's config does.
- **[S-151]** At zero the next phase of the cycle loads in full, still running. One second remaining is
  the last second, not a spare one.
- **[S-152]** The cue sounds on the flip alone and announces the phase being *entered*: ascending
  into focus, descending into either break (`PomodoroCycle.Entering`). Nothing sounds on start, pause,
  reset or a skip.
- **[S-155]** The 1 Hz subscription exists only while the timer runs, and the callback the shared
  ticker makes at subscribe time is swallowed: that immediate fire is right for a view that renders
  the time and wrong for one that *is* the tick.
- **[S-8112]** **Phases** (owner, 2026-11-20: "pomodoro timer you should be able to define phases that
  he runs through, the progress bar should be a sectioned one that shows all the phases in one progress
  bar sectioned."): the config holds an ordered list of phases (`PomodoroConfig.Phases`,
  `PomodoroPhaseSpec`), each a **kind** (`PomodoroPhaseKind`: Focus, Short break, Long break; read
  leniently, an unknown name is Focus), a **name** of the person's own (none or blank: the kind's; at
  most 32 characters) and a **length** ([S-8101]). The timer runs through them in order
  (`PomodoroCycle.Advance`), each flip loading the next in full ([S-151]); the kind colours its section
  ([S-8115]) and picks its cue ([S-152]). `Repair`: no phases is the classic cycle ([S-8113]), more
  than 16 keep the first 16, a length is clamped, a name trimmed and cut, blank cleared. A phase that
  is gone after an edit (fewer phases) starts the cycle again at its first. No backwards-compatible
  reading of the old `workMinutes`/`breakMinutes`/`size` keys ([S-1050]). Tests
  `PomodoroCycleTests.S8112_`, `PomodoroConfigTests`.
- **[S-8113]** The default is the **classic cycle**: Focus 25 and a Short break 5, four times, the
  fourth break the Long break, 15 (eight phases, `PomodoroConfig.Classic`).
- **[S-8114]** **Repeat** (on by default): after the last phase the cycle starts again at its first,
  still running; off, the last phase's end loads the first phase and stops the timer (its cue still
  sounds: it is a flip). A skip past the last phase does the same, silently. A flip never waits for a
  press: the timer runs on through the phases as it always flipped (no "wait for start" setting existed,
  so none was added).
- **[S-8115]** **One sectioned progress** (owner, 2026-11-20, as [S-8112]; `PomodoroSections`): a
  section per phase, its length its share of the whole cycle (the current phase's as it runs now, a
  nudge included), a gap of the family's least space (4, `FamilySpace.S1`) between two; the phases done
  filled, the current one filling from its start with its progress, the ones to come the track. A focus
  section fills in the accent, a break's in the calm role (the secondary text's, never the accent, never
  green): the colour says the kind, no word does ([S-6706] holds for the text). The **Bar** style lays
  them along one 4 track (`PomodoroBar`: the first section on the bar's start, the last on its end; upright, its top and
  its foot, [S-16604]);
  the **Ring** style round the Chart's ring at its stroke from twelve, clockwise, twelve standing in a
  gap, the gap measured on the ring's line between the round caps (`PomodoroRing`); a cycle of one
  phase is the whole bar or ring. Tests `PomodoroSectionsTests`.
- **[S-8116]** The **caption**: the current phase's name and its place in the cycle, "Focus · 3 of 8"
  (`PomodoroCycle.Caption`; a cycle of one phase says its name alone), Body in the secondary text, under
  the time stacked, in the ring and upright beside the bar, and at the head of the column beside the time side by side
  ([S-16603]). "Next: Short break 5:00" is the face's tooltip.
- **[S-6035]** The pomodoro's styles ([S-6000]), `PomodoroConfig.Style` (`PomodoroStyle`):
  **Ring** (the default: the phases as the sectioned ring with the time, the caption and the controls inside it,
  [S-8115], [S-16601]), **Bar** (the phases as the sectioned bar, across or upright by the card's shape, [S-16604]) and
  **Digits** (the time, the caption and the controls alone). `DrawsRing` /
  `DrawsBar`. Debug: `--state sample` (the second focus, 9:24 left, paused).
- **[S-8117]** **The face fills the card** (owner, 2026-11-20: "the pomodoro timer should be designed more fluently not
  all in just one column. especially the bar style."; "the size should not be settable, it is set by the size of the
  widget."): the time left as the Readout, the caption ([S-8116]), the sectioned progress ([S-8115]) and the Controls row
  are planned once at their own size and laid in the card by one pure rule (`PomodoroFace.Lay`, drawn by `PomodoroBlock`):
  the card's shape picks the arrangement ([S-16603]), the parts scale as one until one axis stops them (WR-7, [S-12808])
  and stand centred on the other ("Centred is fine"), down to the floor (WR-14, [S-12833]: its Body text at 9, its glyphs
  at 12). The parts keep their size whatever they read (WR-12, WR-15): the time is held to the longest phase's widest time
  ("88:88", [S-14319]), the caption to the cycle's longest, Start as wide as Pause, so neither a tick, a flip nor a press
  moves the scale. No Size setting: there is no `IReadoutSized` here. Tests `PomodoroFaceTests`.
- **[S-16600]** **The time is smaller** (owner, 2026-11-23: "the pomodoro timer should generally show the time
  smaller"): in every style the time stands on the larger Readout step, 40 (`PomodoroFace.TimeStep`), never a Hero step
  (it was 68 stacked and 96 side by side, where it dominated the card): at most three times the caption's and the
  buttons' Body 14 (`TimeRatio`), its figures no taller than the caption over the controls, and in the ring under a sixth
  of its diameter. Tests `PomodoroFaceTests.S16600_`.
- **[S-16601]** **The Ring style holds everything inside its ring** (owner, 2026-11-23: "the version that has the circle
  should show everything inside of the circle, not outside too"): the ring is as large as the card's smaller side and
  centred on the other; the time, the caption right under it and the Controls row 8 under that stand inside its hole and
  scale with it, at every size. Before, the caption and the controls stood beside the ring (330x140) or under it
  (220x400). Tests `PomodoroFaceTests.S16601_`.
- **[S-16602]** **The ring's rule** (`PomodoroFace.RingRows`, `Circle`, `RingOf`): the parts inside are lines of ink (the
  time's figures as wide as its held box, the caption's capitals to its line's foot, the whole Controls row), each centred
  across and, as a dial, centred down on the ring's middle (as much ring over the time as under the controls); the circle
  through the farthest of their corners is found, and the ring is the least diameter in fours whose hole (its line inks
  inwards from its edge by its stroke) holds that circle with 8 to spare (`RingPad`). The lines fill the circle better
  than an inner square would (a ring 1.41 times the controls' width); laid in a card, the ring and its lines scale as one,
  so they keep within its hole at every size.
  Tests `PomodoroFaceTests.S16602_`, `S16601_`.
- **[S-16603]** **The card's shape picks the arrangement** (owner, 2026-11-23: "the version that has the progress bar
  should better scale to fill out the size of the container, right now all the components are placed underneath each
  other but they can also be placed side by side. (the progress bar would then be vertical for example)";
  `PomodoroFace.Choose`, `Arrangements`): the Ring style its ring ([S-16601]); the Bar style **upright** beside its bar
  where the card is taller than wide ([S-16604]); else, Bar and Digits, **stacked** (the time, the caption right under it,
  on the Bar style the bar 8 under that, the controls 8 under the last, all centred) or **side by side** (the time on the
  left, 24 from a column of the caption and the controls 8 under it, the time's figures and the column's ink centred on
  one band, [S-14281]; on the Bar style the bar 8 under them both), side by side where the card is wider than the
  geometric mean of the two blocks' aspects. Where the one chosen would draw the parts under the floor and another would
  not fall as far, that other. Within its range an arrangement only scales: every part keeps its place against the others
  (owner, of another widget: "it should scale to the size properly but it should not change its layout drastically"); the
  arrangement changes only at those thresholds of the card's shape. In every room the parts' ink fills one axis edge to
  edge and stands centred on the other, and no two parts overlap. Tests `PomodoroFaceTests.S16603_`.
- **[S-16604]** **The bar runs the card's whole length**: across the card's whole width stacked and side by side (its
  thickness the track's at the parts' scale), and upright in a card taller than wide, on its left from the card's top to
  its foot with the time, the caption and the controls stacked 16 beside it (`BarGap`), on their start, the sections from
  its top down (`PomodoroBar.Upright`). Its length is the card's, so it never limits the parts' scale. Upright, the stack is
  the narrowest block ([S-16608]), so a tall card reads it at least as large as stacked ([S-16603]). Tests `PomodoroFaceTests.S16604_`.
- **[S-16608]** **The Controls as two blocks, one width each** (widget audit 2026-11-23, WR-12 and the Column's empty
  bands): the main action and the four icon buttons are two `ControlsBlock`s, 8 apart in one row (stacked, side by side,
  in the ring); upright beside the bar the icon buttons stand 8 under the main action, so the stack is as narrow as its
  widest line and fills more of a tall card (at 240x440 it was held to the one row's width, wide bands over and under
  it). The main action is held to the widest of its labels in the current language (`PomodoroFace.MainLabels`: Start,
  Pause; unseen copies of each under its words): Start becoming Pause moves neither it nor a neighbour (before, a press
  shifted every button beside it by 0.6 to 2 DIP). The time stands unpadded in its held box ([S-16607]), centred where
  the block is centred (the ring, stacked) and on its start beside the caption and controls (side by side, upright), so
  "9:24" lines up with the caption as "25:00" does (WR-7; before, it stood 8 to 46 DIP off it). Tests
  `PomodoroFaceTests.S16608_`.
- **[S-16605]** **Digits** keeps its two arrangements, stacked and side by side, with the smaller time ([S-16600]); it
  never stands upright and draws no bar. Tests `PomodoroFaceTests.S16605_`.
- **[S-16606]** **Sizing** (`PomodoroFace.Sizing`, [S-2438]): the least card is the stacked block (the ring) at its floor
  in its inset, so every card the person can make holds an arrangement at a scale it is read at; a new card is the
  side-by-side block (the Ring style: its ring) in its inset, grown as one where the least card is taller, so it keeps the
  block's shape and the block fills it (raised to the least height alone, the side-by-side block stood with a band of 9
  over and under it at 362x106, 40 at twice that); the descriptor relaxes the most ([S-12411]). Tests `PomodoroFaceTests.S16606_`.
- **[S-14281]** **The column beside the face is one block** (owner, 2026-11-20: "the pomodoro timer should be designed
  more fluently not all in just one column. especially the bar style."; docs/WIDGET-RULES.md WR-7: "Keep them
  together"). Side by side, the caption and the Controls row 8 under it stand right under one another, the column
  centred on the band beside the time's figures, never its caption on the band's top and its controls on its foot with a
  band between (the Bar style's bar 8 under them both, [S-16604]); stacked, in the ring and upright, each part right under
  the one before (`PomodoroFace.Lay`, [S-16603]). The whole block is scaled as one to the card ([S-14280]). Before, at
  330x140 on the Ring style, "Focus · 3 of 8" stood on the card's top and the controls on its foot beside the ring (the
  ring now holds them, [S-16601]). Tests `PomodoroFaceTests.S14281_`.
- **[S-1466]** The face's words and controls: the time left (`M:SS`, never coloured), the caption
  ([S-8116]), "Next: …" its tooltip; Start (Pause while it runs) as the one main action, worded, and −,
  +, Reset and Skip as 28 icon buttons after it, always all of them. Skip loads the next phase in full,
  silently, keeping the running state ([S-8114] at the cycle's end).
- **[S-11304]** *(Retired 2026-11-20: the Size setting (Small, Medium, Large) is gone, owner: "the size
  should not be settable, it is set by the size of the widget." The card is the size: [S-8117].)*
- **[S-644]** *(Retired: no tile is too short for the Controls row; the card is the face and its row.)*
- **[S-1460]** Pomodoro `Sound` (`AlarmSound`: off + the original's twelve — chime (default),
  marimba, crystal, pluck, droplet, birdsong, zen, bell, gong, arcade, digital, buzzer; listed in
  that order, gentle → insistent) and `Volume` 0..100 (default 70, its line hidden while off). Unknown
  names read as chime (lenient converter); `Repair` clamps the volume. Changing either previews the
  focus cue. The old `Alarm` bool is gone (no compatibility).
- **[S-1467]** *(Retired: the XL "N sessions done" row (dots and count) and the session counter are
  gone; the sectioned progress says where the cycle stands, [S-8115].)*
- **[S-1987]** Pomodoro keys (`PomodoroKeys.Of`): Space or Enter start / pause, Right skips to
  the next phase, Home resets to the cycle's first: the three buttons in their order; + or Up and − or
  Down (`PomodoroKeys.Nudge`) are the nudges ([S-8111]), at a bound doing nothing. Nothing after the
  card left.
- **[S-8111]** The − and + nudge (issue 186, a student's request: change the time without going
  into the menu): − makes the **current** phase 5 minutes shorter, + 5 minutes longer, running or
  not, outside edit mode (the buttons take their own click, never the card's). One rule
  (`PomodoroNudge`): the time left and the phase's length move together, so the time already gone and
  its section's share of it stay put, and an idle phase starts 5 minutes shorter or longer; the current
  section is drawn as long as the phase now runs ([S-8115]). A press that would leave less than the
  least (1 minute) or make the phase longer than its most (180 minutes, [S-8101]) does nothing, its
  button disabled: a nudge is 5 minutes or nothing. The nudge is this phase's alone, run state like the
  rest ([S-150]), never the phase's stored length: the stored lengths are the standard every phase
  loads, a nudge is "a bit more of this one", and a write would change the standard from a button that
  says nothing about it (and, as run state is the instance's, race the same widget on another monitor).
  The next flip, a skip or a reset loads the stored length again; a changed length in the menu while
  stopped does too.
- **[S-8110]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page and no Size ([S-8117]). **Phases** ([S-8112]: the menu's editable list,
  [S-3729]): each phase a line of its name and length ("Focus · 25 min"), dragged or Ctrl+Up / Down to
  reorder ([S-3732]), opening Rename… (a name of its own; cleared, the kind's), **Kind** (Focus, Short
  break, Long break), **Duration** (typed, 1–180 min, [S-3726], the clamped length at the end of the line;
  not "Length", Motion's word, [S-3592]) and Remove (greyed on the last phase); then Add a focus phase,
  Add a short break and Add a long break (R8's "Add a …"), each at its classic length, greyed at 16.
  **Repeat the phases** ([S-8114]), a switch. Under a divider **Alarm
  sound** (a choice of off and the twelve ringtones, [S-1460]) and **Alarm volume** (0–100 % in tens,
  only while a sound is chosen, [S-6074]); a new sound or volume previews the focus cue once. (Tests
  `PomodoroMenuTests`.)

Debug renders (`--state`): `sample` (the second focus, 9:24 left, paused), `idle` (a fresh cycle),
`running` (the sample going), `break` (the second short break going, 3:12 left), `paused` (the third
short break paused, 2:40 left), `between` (the moment after a flip: the second short break loaded in
full, going), `last` (the long break going, 11:05 left), `least` (a fresh 5-minute break, − disabled).

Needs a manual pass: a whole cycle on the live desktop with a sound (the cue on each flip, nothing
on start, pause, reset or skip; the stop at the end with Repeat off), the phases list's drag on the
page, and the face at its least, natural and large sizes on the showcase views ([S-1810]).
