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
`PomodoroSections.cs` (the sectioned progress's geometry), `PomodoroFace.cs` (the face's plan, its two
arrangements, the sizing), `PomodoroBlock.cs` (the block that stands the parts stacked or side by side),
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
  them along one 4 track (`PomodoroBar`: the first section on the bar's start, the last on its end);
  the **Ring** style round the Chart's ring at its stroke from twelve, clockwise, twelve standing in a
  gap, the gap measured on the ring's line between the round caps (`PomodoroRing`); a cycle of one
  phase is the whole bar or ring. Tests `PomodoroSectionsTests`.
- **[S-8116]** The **caption**: the current phase's name and its place in the cycle, "Focus · 3 of 8"
  (`PomodoroCycle.Caption`; a cycle of one phase says its name alone), Body in the secondary text, under
  the time stacked and at the head of the column beside it side by side, so it stands by the progress
  in both. "Next: Short break 5:00" is the face's tooltip.
- **[S-6035]** The pomodoro's styles ([S-6000]), `PomodoroConfig.Style` (`PomodoroStyle`):
  **Ring** (the default: the phases as the sectioned ring round the time, [S-8115]), **Bar** (the phases
  as the sectioned bar) and **Digits** (the time, the caption and the controls alone). `DrawsRing` /
  `DrawsBar`. Debug: `--state sample` (the second focus, 9:24 left, paused).
- **[S-8117]** **The face fills the card, stacked or side by side** (owner, 2026-11-20: "the pomodoro
  timer should be designed more fluently not all in just one column. especially the bar style."; "the
  size should not be settable, it is set by the size of the widget."): the time left as the Readout,
  the caption ([S-8116]), the sectioned bar on the Bar style and the Controls row are one block
  (`PomodoroBlock`) that its `ReadingFit` scales as one to the card ([S-12808]) and down to its floor
  (WR-14, [S-12833]: its Body text at 9, its glyphs at 12). Two arrangements of the same parts
  (`PomodoroFace.Plan`, `PomodoroArrangement`):
  **stacked** — the face (the time on 68, or the ring with the time on 40 in it), the caption right
  under the time (8 under a ring), the bar 8 under it as wide as the block, the Controls row on the
  foot, all centred; **side by side** — the face on the left (the time on 96, where its capitals stand
  as tall as the column; the ring), 24 from a column of the caption on its top, the bar 8 under it as
  wide as the column and the Controls row on its foot; the face's ink (the time's capitals to its
  baseline, the ring's box) and the column's (the caption's capitals to the controls' foot) are one band,
  the shorter centred on it, so the face is level with the caption and the controls. Its parts always
  touch ([S-14281]): the block is its parts' size and its fit scales it ([S-14280]).
  **The card's aspect chooses** (`PomodoroFace.Choose`): side by side where the room is wider than
  the geometric mean of the two arrangements' aspects, stacked where not; the other where the chosen
  one would fall under the floor and the other less far. The parts keep their size whatever they read
  (WR-12): the time is as wide as the longest phase's widest time ("88:88"), the caption as the cycle's
  longest, Start as wide as Pause, so neither a tick, a flip nor a press moves the block's scale.
  **Sizing** (`PomodoroFace.Sizing`, [S-2438]): a new card is the side-by-side block in its inset (no
  shorter than the least card); the least card is the stacked block at its floor in its inset, so every
  card the person can make holds one arrangement at a scale it is read at; the descriptor relaxes the
  most ([S-12411]). No Size setting: there is no `IReadoutSized` here. Tests `PomodoroFaceTests`.
- **[S-14281]** **The column beside the face is one block** (owner, 2026-11-20: "the pomodoro timer should be designed
  more fluently not all in just one column. especially the bar style."; docs/WIDGET-RULES.md WR-7: "Keep them
  together"). Side by side, the caption, the bar 8 under it and the Controls row 8 under that stand right under one
  another, the column centred on the band beside the face (a ring taller than it, or the time's capitals), never its
  caption on the band's top and its controls on its foot with a band between; stacked, each part right under the one
  before (`PomodoroFace.Stand`, `PomodoroStand`). The whole block is scaled as one to the card ([S-14280]). Before, at
  330x140 on the Ring style, "Focus · 3 of 8" stood on the card's top and the controls on its foot beside the ring.
  Tests `PomodoroFaceTests.S14281_`.
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
