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
  duration is drawn, on the timers' sized face ([S-11303], `CountdownLayout.Plan`); every style draws
  its own look at every size. Debug: `--state sample` (a launch in three days, set two
  days ago).
- **[S-6031]** **Ring**: the time left as the Chart block's ring (`ChartRing`, stroke 2 over its
  Track, the arc in the accent), the duration and its date in the ring's hole (`ChartRules.Hole`) one
  step under the Size's, the ring the least that holds them ([S-11300]); the ring is the share still
  to go from when the target was set to the target (`CountdownLayout.Left`), so it empties as the day
  comes; without that time ([S-1448]) it draws its track alone. The ring is the progress: no bar
  under it.
- **[S-6032]** **Flip**: split-flap cards ([S-6740], `FlipCard` sized by `FlipRules`) side by side,
  8 apart, each with its unit under it ("days", "hours", "min", "sec") on the cards' line under
  (`FamilyType.Under`, secondary), the cards one step under the Size's and each as wide as three
  figures ([S-11303]): days while there is at least one (as many figures
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
  has no settings page. Under its Style, **Size** and **Progress bar** ([S-11303]); Appearance ([S-3723]), then, each group behind a divider, **Name** (typed, at most 60 characters, empty
  for Untitled), **Date** (typed, [S-3738]; the day in the language's medium form at the end of the
  line; with no target yet the time is midnight, so a typed day counts to its start), **Time** (typed
  in either clock, [S-3738]; greyed while there is no day), each keeping the other part of the
  target, a new target restarting its progress ([S-1448]) and a DST-skipped one changing nothing
  ([S-144]); then **Format** (Auto, Days only, Days · Hours · Min, Days · Hours · Min · Sec).
  (Tests `CountdownMenuTests`.)
- **[S-1445]** *(Retired: one target per widget, [S-6036]; removing a countdown is removing its widget.)*
  The format is chosen on the properties menu only ([S-1746]); original C-60 and C-61 are moot.
- **[S-1446]** *(Retired: one target per widget, [S-6036]; nothing is featured and there is no "Next" line.)*
- **[S-1447]** *(Retired: the face by the tile's size and the bar on L and XL; the face is the
  Size's and the bar a setting, [S-11303].)* A reading's shape is its figures as 8s
  (`CountdownLayout.Shape`). No Controls row: there is nothing to start or stop. The whole instant is
  the tooltip.
- **[S-11303]** **Its size and its bar are settings** (under its Style): **Size** (Small, Medium,
  Large) puts the Text style's duration on 40, 68 or 96 with the target's date always under it, and
  the Ring's readout and the Flip's cards one step under (28, 40, 68; [S-11300]); **Progress bar**
  (off for a new one; Text and Flip only, a ring is its own progress) stands the bar ([S-1448]) 8
  under the face, the content's width. The face is sized on the widest reading its format writes in
  the language for its own target's day count ([S-11803]), every figure an 8, ahead or gone by
  (`CountdownLayout.Widest`: Auto's widest of its wordings, "88 days ago" in English for a target under
  100 days away), the Flip cards on as many figures each and as many
  cards as the format can show (days, hours, minutes, and seconds with them), so nothing moves as it
  counts. The card follows (`CountdownLayout.Sizing`): under its header (the event's name, on by
  default) the face, the bar where it is on, the inset; one height, up to half again wider; it sits
  where the widget's alignment puts it ([S-6580]), centred by default. A reading wider than that
  (a thousand days and more) is cut. No tier ladder is left. Tests `CountdownLayoutTests.S11303_`.
- **[S-1448]** `CountdownConfig.Created` (set with the target on the properties menu): the bar, where
  it is on ([S-11303]), is the Chart block's (`ChartBar`, the 4 track, the accent) from then to the
  target. Unknown (unset) or not before the target: its track alone.
- **[S-1986]** *(Retired: one target per widget, [S-6036]; with no list the countdown takes no keys.)*

Needs a manual pass: the three styles at their min, natural and max on the live desktop (showcase views, [S-1810]).
- **[S-11803]** **Sized for its own target** (2026-11-12): the readout is sized on the widest wording the
  countdown's own target makes, not on its format's widest ("999 days ago" made an Auto card about 600
  wide at Medium and a large ring): the day count has as many figures as the target was days away when
  it was set (`CountdownLayout.DayFigures`, at least two, so "88 days ago" fits once it is past), within
  a day it is hours and minutes; the flip cards are as wide as those figures. A pure function of the
  config: the card changes when the target or the format changes, never as the clock runs. A target
  more than 99 days past on a card sized for two figures is cut with its ellipsis. A moment has no "ago" form ("just now" says it), so "in a moment ago", once the widest English wording, sizes nothing. Tests
  `CountdownLayoutTests.S11803_`.
