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

- **[S-3105]** Moon ladder on the family's blocks (`MoonLayout`, G3 2026-10-29): the Header (the moon's
  glyph, the widget's name, the place as its label, [S-2260]), then a Readout of how much of the moon is lit ("23%", [S-6702]) with the
  moon drawn at its phase as its lead at the step's size (`SkyMoonDisc`, [S-169]) and the phase's
  name the line under it, then whole fact rows moonrise, moonset, the next full or new moon ([S-1491],
  [S-6735]: their leads the moon at 16, the next rise or set BodyStrong with its countdown the second
  line where the tier gives two lines). The step is the area's (28, 40, 68) for the language's "100%";
  room left goes to the rows, never a bigger moon ([S-666]). Where the readout is too narrow for the
  moon beside even the floor step (XS), the moon gives way, never the figure. On an expanded width the
  readout stands in a column of two fifths of the card and the rows beside it from the top, as the
  detail widgets' figure ([S-2402], `DetailFace`). With one rise/set row it is the next of the two.
  The next full or new moon drops its weekday where the row cannot hold it ("Oct 26"). The readout
  sits at the start by default ([S-6582]).
- **[S-6050]** The moon's styles ([S-6000]), `MoonConfig.Style` (`MoonStyle`): **Disc** (the
  default, [S-3105]) and **Phase month**: one small disc a day of the month holding now, each that
  day's phase at its local noon (`MoonMonth`, [S-6051]; the [S-169] drawing, mirrored for a southern
  observer), reflowed to the content by the dot grid ([S-6061]) from the inset's start, today ringed
  in the accent at the Chart's stroke, the month's name (Caption, secondary) 8 over them while the
  discs stay 16 or larger under it. Each disc's tooltip names its day and phase. Concise: no rise,
  set or percentage rows in this style. Debug: `--state sample` pins 12 October 2026, 21:00.
- **[S-3108]** Every option is on the properties menu ([S-3719], user request 2026-10-19); there is
  no settings page: Appearance ([S-3723]), then behind a divider **Location**, the sky's shared search ([S-3101], as
  the Sun's [S-3107]). The month of phases offers no Longitude line ([S-6074]: one disc a day reads no
  longitude); nothing stands under Style.
- **[S-6051]** `MoonMonth.Days`: every day of the month holding now in the machine's zone, each with
  the phase at the middle of that local day (so a phase turning at 01:00 or 23:00 belongs to the day
  most of it is in), today marked once. Redrawn when the day changes, not on every tick.

Needs a manual pass: both styles at every tier on the live desktop (showcase views, [S-1810]).
