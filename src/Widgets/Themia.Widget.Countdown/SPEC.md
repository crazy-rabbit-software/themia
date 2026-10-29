# Countdown

The module `countdown` ([S-6911]): one target, its name and the time to go (or gone), in words, a
ring or flip cards. Its tags are this file's alone; the root `SPEC.md` holds the host's sections
(styles [S-6000], the timers' face [S-6739], the ring [S-6717], the flip cards [S-6740], setup [S-1860], alignment
[S-6580], the shared ticker [S-155], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `CountdownModule.cs` (descriptor, view factory, menu
lines, JSON context), `CountdownConfig.cs` (with `CountdownFormat`), `CountdownFormatter.cs` (the
words), `CountdownLayout.cs` (the ladder, the ring's share, the flip cards), `CountdownView.cs`,
`CountdownMenu.cs` (its properties-menu lines), `Preview/` (Debug renders), `Localization/Locales/` (its own
`CountdownStr`, 30 languages). Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-140]** A countdown reads its name (the card's header, [S-6037]), the duration and the absolute
  local target. A target in the past is not hidden: it reads "… ago" in the critical role, because how
  long ago a deadline went is the thing you want to know.
- **[S-141]** `auto` picks the coarsest unit that still says something: "in a moment", "N mins",
  "Nh Nm" (or "N hrs" on the hour), "Nd Nh" (or "N days" on the day), and past a week just days.
  Under a minute in the past is "just now", never "in a moment ago". The explicit formats keep their
  zero components, because a fixed shape is what makes a column of durations readable.
- **[S-142]** The one-second cadence is bought only by the format that shows seconds; every other
  format re-letters twice a minute, and a countdown with no target does not wake at all.
- **[S-143]** *(Retired: one target per widget, [S-6036]; there are no rows to keep.)*
- **[S-144]** A cleared, impossible or non-existent (DST-skipped) target leaves the countdown pointing
  where it did. Writing "now" is what the original did, and it silently retargeted a timer mid-edit.
- **[S-6030]** The countdown's styles ([S-6000]), `CountdownConfig.Style` (`CountdownStyle`):
  **Text** (the default: the readout as [S-1447]), **Ring** and **Flip**. A style is how the
  duration is drawn, on the timers' face ([S-6739], `CountdownLayout.Plan`); a tile too small for its
  ring (a hole that cannot hold the duration on 28, `TimerFace.HoleHolds`) or its cards (their row on
  28 wider than the content) draws the text. Debug: `--state sample` (a launch in three days, set two
  days ago).
- **[S-6031]** **Ring**: the time left as the Chart block's ring (`ChartRing`, stroke 2 over its
  Track, the arc in the accent) as large as the content's short side, the duration and its date in
  the ring's hole (`ChartRules.Hole`) on the largest Hero step they fit; the ring is the share still
  to go from when the target was set to the target (`CountdownLayout.Left`), so it empties as the day
  comes; without that time ([S-1448]) it draws its track alone. The ring is the progress: no bar
  under it.
- **[S-6032]** **Flip**: split-flap cards ([S-6740], `FlipCard` sized by `FlipRules`) side by side,
  8 apart, each with its unit under it ("days", "hours", "min", "sec") on the cards' line under
  (`FamilyType.Under`, secondary, where it fits): days while there is at least one (as many figures
  as it takes), then hours and minutes, two figures each, and seconds when the format shows them
  (`CountdownLayout.Cards`); a target gone by counts up in the critical role. A card whose figures
  change on a tick turns over; the days card going at the last day re-lays the row.
- **[S-6036]** A countdown widget counts to **one** target (user decision): `CountdownConfig` is its
  name (one line, at most 60 characters; the card's header shows it, the widget's own name while it
  is empty), its target (an instant kept in UTC), its format and when
  the target was set (`Created`, [S-1448]). Several countdowns are several widgets; there is no
  timer list, no row and no menu or key of the widget's own. A new widget has no target and shows
  the Set up button ([S-1860]); setting one (`Retarget`) starts its progress from then. A
  hand-edited file is repaired loudly: an unknown format reads Auto, a name of several lines or
  too long is cut to one short line.
- **[S-6037]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then, each group behind a divider, **Name** (typed, at most 60 characters, empty
  for Untitled), **Date** (typed, [S-3738]; the day in the language's medium form at the end of the
  line; with no target yet the time is midnight, so a typed day counts to its start), **Time** (typed
  in either clock, [S-3738]; greyed while there is no day), each keeping the other part of the
  target, a new target restarting its progress ([S-1448]) and a DST-skipped one changing nothing
  ([S-144]); then **Format** (Auto, Days only, Days · Hours · Min, Days · Hours · Min · Sec).
  (Tests `CountdownMenuTests`.)
- **[S-1445]** *(Retired: one target per widget, [S-6036]; removing a countdown is removing its widget.)*
  The format is chosen on the properties menu only ([S-1746]); original C-60 and C-61 are moot.
- **[S-1446]** *(Retired: one target per widget, [S-6036]; nothing is featured and there is no "Next" line.)*
- **[S-1447]** Countdown face ([S-6739]): the duration is the Readout on the Hero steps (a
  one-readout widget, [S-6737]), the target's date its line under where it fits, sized on the
  reading's shape (its figures as 8s, `CountdownLayout.Shape`), so a tick never moves it and a new
  shape (a unit going) lays it out again; on L and XL (`CountdownLayout.ShowsProgress`, Expanded both
  ways) the progress bar ([S-1448]) stands 8 under it, the content's width. It sits where the widget's
  alignment puts it ([S-6580]), centred by default (a timer centres its readout), in the middle of the
  content's height. No Controls row: there is nothing to start or stop. The whole instant is the
  tooltip.
- **[S-1448]** `CountdownConfig.Created` (set with the target on the properties menu): L/XL draw the
  Chart block's bar (`ChartBar`, the 4 track, the accent) under the readout from then to the target.
  Unknown (unset) or not before the target: no bar.
- **[S-1986]** *(Retired: one target per widget, [S-6036]; with no list the countdown takes no keys.)*

Needs a manual pass: the three styles at every tier on the live desktop (showcase views, [S-1810]).
