# Date (date)

The module `date` ([S-6911]). Its tags are [S-10800]..[S-10899], this file's alone; the root
`SPEC.md` holds the host's sections (the clock's date moved here, [S-12805]; the face fills its card as the
clock's does, [S-12804]).

Owning files: everything in this folder: `DateModule.cs` (descriptor, view factory, JSON
context), `DateConfig.cs` (`DateWidgetStyle`, the config), `DateLayout.cs` (the words and the sizes, pure),
`DateView.cs`, `Samples/`, `Preview/`. Tests: `Tests/DateTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-10800]** Today's date, a widget of its own (owner, 2026-11-17: "the clock widget should not have a date
  style as this should be moved into its own widget"). A one-readout widget ([S-6737]) among the time's widgets
  on the add page, beside the clock. It turns over within a minute of midnight (one tick a minute on the shared
  ticker); its tooltip is the full date ("Tuesday, November 17, 2026"). No zone of its own: today on this PC.
- **[S-10801]** **Styles** ([S-6000], `DateWidgetStyle`, `"style": "long"`): **Long** ("Tuesday, November 17":
  the weekday and the day in the language's order, `date.long` over `LocaleFormat`'s month and day), **Short**
  ("Tue, Nov 17"), **Numbers** (the language's numeric date, "11/17/2026", "17.11.2026") and **Page** (a calendar
  page: the weekday over the day of the month, large, over the month). Words through `LocaleFormat` only, never
  culture data (`DateLayout.Line`, `.Page`).
- **[S-10802]** **No Size** (owner, 2026-11-20: "the size should not be settable, it is set by the size of the
  widget"; [S-2449] retired): a one-line style is planned on 28, the page's number on 68 with its words on the
  step's line under (`DateLayout.LineStep`, `NumberStep`). The natural size (`DateLayout.Sizing`) is the face in the
  inset of 12: as wide as the language's widest day in the style (measured over two whole years: every weekday with
  every month and day), widened up to twice that (beside a wider mate, under a clock); the card is the user's past
  that. It scales down to its main reading at 9 DIP in the inset (root [S-12833], decided by its `ReadingFit` on the face as drawn): a line to its
  widest day at 9 by 32, the page to its number alone (35 × 32), its weekday and month giving way together where the
  card would draw them under 9 (owner, 2026-11-19: "for the widgets that can scale there should basically be no lower
  limit except for one that makes sense so you can still see the widget"). It has no menu lines of its own. A stored
  `size` is an unknown key, dropped loudly on load.
- **[S-10803]** **It fills its card** (root [S-12804]): the face is laid out at its step, held to the language's widest
  day ([S-15804]), and scaled up as one to the card (`ReadingFit`, uniform; down only as [S-12833] says), so the widest
  day's line reaches the sides where the width stops it, today's standing in its box by the widget's alignment ([S-6581],
  centred by default); the family's inset at the sides and the foot, joined or alone
  ([S-12483]).
- **[S-15804]** **The date never changes size with the day** (WR-15, owner 2026-11-20: "the clock right now changes its
  layout depending on the time that it shows"). Each line is held to its template, the language's widest text of it over
  two whole years (`DateLayout.HeldLine`, `HeldPage`: the widest weekday, day number and month for the page): today's
  text is drawn in a cell as wide as the template, which stands there unseen (`HoldMark`), so the fit sizes and stands the
  face by the template every day, as the clock's figures by "88:88" ([S-14319]); the gap check judges its sides there.
  Before, each day was its own box ([S-14308], retired here): at 388 × 60 "Sunday, November 22" stood 36 tall and
  "Wednesday, September 30" 29.4. Tests `DateTests.S15804_`; the Debug state `longest` beside `sample` shows one box.
  The page's weekday and month giving way where they would be drawn under 9 (300 × 100, 350 × 70) is the floor
  ([S-12833], WR-14: "sized down to where its smallest text is still read"), the number left filling the room
  ([S-14318], WR-1).
- **[S-15700]** **The page's three lines each stand on their own ink** (WR-3, [S-14301]): the weekday, the number and the
  month are the fit's parts, each placed across by its own ink where the alignment puts it, not the three in one box
  centred by its widest. A number whose figures ink off their box's middle (the 22nd's "22", 3 DIP right at 900 x 300)
  stood off centre while its box was centred. The weekday and month still give way together, each in its own box.
- **[S-10804]** Its picture ([S-11850]) is the sample day ([S-2050]); the Debug state `longest` draws a long
  September Wednesday to see the face hold its widest.

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]); the turn at midnight.
