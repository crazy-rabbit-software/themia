# Date (date)

The module `date` ([S-6911]). Its tags are [S-10800]..[S-10899], this file's alone; the root
`SPEC.md` holds the host's sections (the clock's date moved here, [S-12805]; the face fills its card as the
clock's does, [S-12804]).

Owning files: everything in this folder: `DateModule.cs` (descriptor, view factory, the Size line, JSON
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
- **[S-10802]** **Size** (Small, Medium, Large, [S-2449]; the style's one line): a one-line style on Title
  (regular), 28 or 40 (Small is the line a 68 clock had under it), the page's number on 40, 68 or 96 with its
  words on the step's line under. The sizing (`DateLayout.Sizing`) is the face in the inset of 12: as wide as the
  language's widest day in the style (measured over two whole years: every weekday with every month and day) and
  one height; it may be widened to twice that (beside a wider mate, under a clock). Load repair turns a size that
  is none of the three into Medium.
- **[S-10803]** **It fills its card** (root [S-12804]): the face is laid out at its step in its natural box and
  scaled up as one to the card (a `Viewbox`, uniform, never down), placed by the widget's alignment ([S-6581],
  centred by default); the family's inset at the sides and the foot, none on a foot a group mate joins
  ([S-12483]).
- **[S-10804]** Its picture ([S-11850]) is the sample day ([S-2050]); the Debug state `longest` draws a long
  September Wednesday to see the face hold its widest.

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]); the turn at midnight.
