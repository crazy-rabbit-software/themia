# Moon (SKY-SPLIT)

The module `moon` ([S-6911]): the moon where the observer is, its disc, phase, rise and set, or this
month's phases. Its tags are this file's alone; the root `SPEC.md` holds the host's sections and the
pair's shared rules (Sun and moon: the astronomy [S-160]..[S-169], the two kinds and their shared
place [S-3100], [S-3101], moonrise and moonset [S-3104], the rise/set text [S-3106], the next full or
new moon [S-1491]; styles [S-6000], the dot grid [S-6061], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `MoonModule.cs` (descriptor, view factory, menu lines,
JSON context), `MoonConfig.cs`, `MoonLayout.cs` (the ladder), `MoonMonth.cs` (the month of phases),
`MoonView.cs`, `MoonMenu.cs` (its properties menu: the shared place line, `SkyPlaceRows`), `Preview/` (Debug
renders), `Localization/Locales/` (its own `MoonStr`, 30 languages). Tests: `Tests/MoonTests.cs`,
`Tests/MoonMenuTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-3105]** Moon ladder (`MoonLayout`, on one `Disclosure`): the disc (never dropped, 24 px floor)
  → the phase's name (measured at the language's longest name) → the lit percentage → whole rows
  moonrise, moonset, the next full or new moon ([S-1491]). The disc takes what the rungs leave, up
  to 192 px, so a larger tile only draws a larger moon. On a wide strip (Wide shape, width ≥
  Regular) the text and rows stand beside the disc, which gives way to them: the column climbs the
  ladder with the disc at its floor, then the disc grows only as far as the name and a row's words
  still fit beside it. A row is shown only where its words fit beside its glyph (the language's
  widest: a short rise or set label and a time on another day, or a full or new moon and its
  shortest date; `MoonLayout.RowRoom`), never clipped at the card's edge (at 268 × 134 the disc
  took two fifths of the strip and "Rise Tue 11:06 AM" ran off it). With one rise/set row it is the next of the two;
  the next one carries its countdown in the accent ([S-163]). The next full or new moon drops its
  weekday where the row cannot hold it ("Oct 26").
- **[S-6050]** The moon's styles ([S-6000]), `MoonConfig.Style` (`MoonStyle`): **Disc** (the
  default, [S-3105]) and **Phase month**: one small disc a day of the month holding now, each that
  day's phase at its local noon (`MoonMonth`, [S-6051]; the [S-169] drawing, mirrored for a southern
  observer), reflowed to the tile by the dot grid ([S-6061]), today ringed in the accent, the month's
  name over them while the discs stay 14 DIPs or larger under it. Each disc's tooltip names its day
  and phase. Concise: no rise, set or percentage rows in this style. Debug: `--state sample` pins
  12 October 2026, 21:00.
- **[S-3108]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), then behind a divider **Location**, the sky's shared search ([S-3101], as
  the Sun's [S-3107]). The month of phases offers no Longitude line ([S-6074]: one disc a day reads no
  longitude); nothing stands under Style.
- **[S-6051]** `MoonMonth.Days`: every day of the month holding now in the machine's zone, each with
  the phase at the middle of that local day (so a phase turning at 01:00 or 23:00 belongs to the day
  most of it is in), today marked once. Redrawn when the day changes, not on every tick.

Needs a manual pass: both styles at every tier on the live desktop (showcase views, [S-1810]).
