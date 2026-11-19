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
  that. It scales down to its main reading at 9 DIP in the inset (root [S-12833], `DateLayout.Fit`): a line to its
  widest day at 9 by 32, the page to its number alone (35 × 32), its weekday and month giving way together where the
  card would draw them under 9 (owner, 2026-11-19: "for the widgets that can scale there should basically be no lower
  limit except for one that makes sense so you can still see the widget"). It has no menu lines of its own. A stored
  `size` is an unknown key, dropped loudly on load.
- **[S-10803]** **It fills its card** (root [S-12804]): the face is laid out at its step in its natural box and
  scaled up as one to the card (`InkFit`, uniform; down only as [S-12833] says), placed by the widget's alignment ([S-6581],
  centred by default); the family's inset at the sides and the foot, joined or alone
  ([S-12483]).
- **[S-10804]** Its picture ([S-11850]) is the sample day ([S-2050]); the Debug state `longest` draws a long
  September Wednesday to see the face hold its widest.

Needs a manual pass: its min, natural and max on the live desktop (showcase views, [S-1810]); the turn at midnight.
