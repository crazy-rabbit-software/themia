# Moon (SKY-SPLIT)

The module `moon` ([S-6911]): the moon where the observer is, as a **reading** (how much of it is lit
and its phase) or this month's phases. An atom ([S-11170]..[S-11172]): its rise, set and next full and
new moon are the `moonTimes` atom, the old card the ready-made group "Moon"; its id stays `moon`. Its
tags are this file's alone; the root `SPEC.md` holds the pair's shared rules (the astronomy
[S-160]..[S-169], the shared place [S-3100], [S-3101]; styles [S-6000]).

Owning files: everything in this folder: `MoonModule.cs` (descriptor, view factory, menu lines,
JSON context), `MoonConfig.cs`, `MoonLayout.cs` (both looks' sizes), `MoonMonth.cs` (the month of
phases), `MoonView.cs`, `MoonMenu.cs` (the shared place line, `SkyPlaceRows`), `Preview/` (Debug
renders), `Localization/Locales/` (its own `MoonStr`). Tests: `Tests/MoonTests.cs`, `Tests/MoonMenuTests.cs`.

- **[S-3105]** *(Retired by [S-11120]: no rows and no ladder; the rows are the moon times' atom, [S-11125].)*
- **[S-11120]** **What it shows**, the same at every size in its range: in the **Disc** style a Readout
  of how much of the moon is lit on the family's 28 ("23%", [S-2449]), the moon drawn at its phase as
  its lead at the step's size (`SkyMoonDisc`, [S-169]), the phase's name the Caption under it, at the
  start by default ([S-6582]); in **Phase month** the month's discs ([S-6050]). A Header widget over it
  names it ([S-12481]); in a group it stands against a side a mate joins ([S-12483]).
- **[S-11121]** **Sizes** (`MoonLayout.Sizing`): the disc one height (76: the 28's
  line, the Caption, the insets), 140..320 wide, a new one 160; the month one size: seven columns and
  five rows of 28 cells (a 20 disc in each), the month's name 8 over them (212 × 180: the grid its discs' ink, [S-15673]).
- **[S-6050]** The moon's styles ([S-6000]), `MoonConfig.Style` (`MoonStyle`): **Disc** (the default)
  and **Phase month**: one small disc a day of the month holding now, each that day's phase at its
  local noon (`MoonMonth`, [S-6051]; the [S-169] drawing, mirrored for a southern observer), in reading
  order on the fixed grid from the inset's start, today ringed in the accent at the Chart's stroke, the
  month's name (Caption, secondary) over them. Each disc's tooltip names its day and phase. Debug:
  `--state sample` pins the sample place at the sample moment.
- **[S-15673]** **The month's grid is its ink** (`MoonLayout.GridOf`): its box runs from its outer discs' edges, today's
  ring (4 past its disc) where it stands on an edge, and its lines are the month's days' (a February of 28 four), never
  the pitch's gap round the outer discs nor an empty line: the face scales and centres by its parts' boxes
  ([S-12808]), and the 4 DIP the outer discs stood inside the grid's box stood the face off centre at twice its size and
  short of a wide card's foot. Tests `MoonTests.S15673_`.
- **[S-3108]** Every option is on the properties menu ([S-3719]); there is no settings page: Appearance
  ([S-3723]), then behind a divider **Location**, the sky's shared search ([S-3101], as the Sun's
  [S-3107]). The month of phases offers no Longitude line ([S-6074]); nothing stands under Style.
- **[S-6051]** `MoonMonth.Days`: every day of the month holding now in the machine's zone, each with
  the phase at the middle of that local day (so a phase turning at 01:00 or 23:00 belongs to the day
  most of it is in), today marked once. Redrawn when the day changes, not on every tick.

Needs a manual pass: both styles at min, natural and max on the live desktop (showcase views, [S-1810]).
