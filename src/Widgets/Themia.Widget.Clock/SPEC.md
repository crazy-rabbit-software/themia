# Clock

The module `clock` ([S-6911]): the time in seven styles (Digital, Analog, Stacked, Flip, Words,
Minimal, Date), in a zone of its own, with the zone's city, the date and the offset where they fit. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections (styles [S-6000], the flip card
the countdown shares [S-6032], `FlipCard` in the SDK; header defaults [S-700]; alignment [S-6580];
the family's steps and blocks [S-6700]..[S-6740]; the regional clock [S-3770]; the ladder rules [S-660]..[S-666]).
The view is XAML (`ClockView.xaml`, compiled in this library).

Owning files: everything in this folder: `ClockModule.cs` (descriptor, view factory, menu lines,
JSON context), `ClockConfig.cs`, `ClockFormatter.cs` (the reading, the offset, the words),
`ClockFace.cs` (the faces' steps and the line under), `WordClock.cs`, `DateText.cs` (the Date style's words), `ZoneCatalog*.cs`
(the zone table and picker), `ClockView.xaml(.cs)`, `ClockMenu.cs` (its properties-menu lines), `Preview/` (Debug
renders), `Localization/Locales/` (its own `ClockStr`, 30 languages, the word clock's rules among
them). Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-50]** 24-hour time is zero-padded; 12-hour time is not, and carries AM/PM. Midnight and noon
  read as **12**, not 0.
- **[S-51]** Hands sweep rather than step: the hour hand advances with the minutes, the minute hand
  with the seconds while the second hand is shown. A face without its second hand stands its minute
  hand on the whole minute, so the face drawn on the minute is right until the next one.
- **[S-52]** A clock asks for a **1-second** cadence only while it draws seconds ([S-6015]): the
  digits' or the analog face's second hand. Every other clock, an analog face with seconds off
  included (it woke every second for a hand it did not draw, CPU audit 2026-10-26), asks for a
  **1-minute** cadence, aligned to the minute by the shared ticker. The word and minimal styles
  never draw seconds, so they never wake for them.
- **[S-53]** An unknown or malformed time-zone id degrades to the local zone rather than throwing —
  this runs on every tick.
- **[S-54]** Formatting is done explicitly, not with culture patterns: the app builds with
  `InvariantGlobalization`, so culture format strings would not honour the config.
- **[S-6011]** The clock's styles ([S-6000]), `ClockConfig.Style` (`ClockStyle`, stored as
  `"style": "digital"`; what was its face): **Digital**, **Analog**, **Stacked** ([S-1483]),
  **Flip**, **Words**, **Minimal** and **Date** ([S-5300]), in that order. Every style is one block in
  the card's inset of 16 (the clock has no header), centred top to bottom and placed by the
  alignment, on the largest fixed family step that fits, never filling the box ([S-6741]..[S-6744]);
  every style but Minimal and Date draws one line under (beside a dial on a strip) its face
  ([S-6741]). The settings (seconds, date, zone, offset, alignment) are the same for the six time
  styles; Date has only the alignment and the zone ([S-5303]).
- **[S-6012]** **Flip**: a split-flap card per part of the time (hours, minutes, and seconds when
  shown), sized and placed by [S-6742] (`FlipCard`, `FlipRules`, root [S-6740]). A card whose digits
  change on a tick turns over (folds away, 110 ms, and the new digits fold in, 170 ms); a config,
  language or size change redraws without a turn, and nothing turns while Windows' animation effects
  are off ([S-1010]). One card implementation for the clock and the countdown's Flip style ([S-6032]).
- **[S-6013]** **Words**: the time as a sentence in five-minute steps, in the current language
  ("It's twenty to eleven", "Es ist zwanzig vor elf"), in the clock's own zone
  (`ClockFormatter.Words`), with the line under it ([S-6741]). Composed by `WordClock`
  ([S-6020]..[S-6024]).
- **[S-6014]** **Minimal**: the digital readout at the regular weight (400) of its step (the light
  weight is gone: the family has 400 and 600 only, [S-6700]) and nothing else at any size: no label,
  date or offset, and no seconds ([S-6015]). It stacks where the digital readout would ([S-6741]).
- **[S-6015]** `ClockConfig.DrawsSeconds`: seconds are drawn (and asked for, [S-52]) only when they
  are on and the style is not Words, Minimal or Date.
- **[S-6016]** Any style may follow any other, in any order, with seconds and the date on or
  off: the view lets go of what the last style built (the flip cards and the AM/PM caption they
  carry included) before the next one is laid, and never shows its error card for a style
  change. Covered by the Debug run `Themia.exe --clock-style-check <outDir>`, which hands one live
  card every style after every other, both ways and to and from Flip, the seconds toggled on Flip
  at four sizes, and exits 1 on a fault (it did on Flip, another style, Flip again, before the fix).
- **[S-6020]** `WordClock.Compose`: each language writes its own rules into its locale strings
  (its own `Localization/Locales/<code>.json`), never code. `wordClock.hours` is one or more hour forms (`;`
  between forms, `|` between entries), each 12 entries (index 0 is twelve, then one to eleven;
  indexed by the hour mod 12) or 24 (0 is midnight; so a language can say noon, midnight or a part
  of the day: 午前/午後, Thai's six-hour periods, Swahili's hours from 7 am). `wordClock.m00` ..
  `wordClock.m55` are one sentence per five minutes; in them `[h]` is the hour in form 1, `[n]` the
  next hour in form 1, `[h2]`/`[n2]` form 2, and so on. Grammar a word cannot carry alone (case,
  gender, article and verb agreement: "Es la una" / "Son las dos", the Turkish accusative and
  dative, the Russian genitive ordinal, Hindi's डेढ़/ढाई) lives in the form entries. The markers
  are not `{placeholders}`, so a language may name any form in any sentence ([S-1105]).
- **[S-6021]** A time reads as the **nearest** five minutes (10:13 is "quarter past ten", 10:58
  "eleven o'clock", 23:58 midnight's "twelve").
- **[S-6022]** The sentence starts with a capital (Turkish `i` becomes `İ`); scripts without case
  are left as written.
- **[S-6023]** A malformed language (a form not 12 or 24 non-empty entries, a marker naming a form
  that does not exist) is never shown half-filled: that sentence is English, logged. Every
  language's strings are whole and fill all 288 steps of a day with no marker left (tested over all
  thirty).
- **[S-6024]** The type: the largest of Body, Title and the Readout's 28, 40 and 68 (a sentence is
  never set on a Hero step) at which the language's **longest** sentence of the day, wrapped at words,
  and the line under it (Caption under Body, Title and 28; Body under 40 and 68) fit the content box
  (`WordClock.Fit`, `WordClock.Steps`, `WordClock.Longest`), so the text keeps its size through the
  day; Body at least. Width is estimated per character, a Han, kana or Hangul character as 1.75
  average advances; a sentence without spaces wraps anywhere, as the text block does.
- **[S-645]** *(Retired by [S-6703], group G5: the clock's inset is the card's 16 on every side; its
  half inset above and below went with the filled readout, [S-659].)*
- **[S-665]** Clock: the face, then its one line ([S-6741]): the city of a world clock, the date (short
  "Thu, Oct 29" when the long one does not fit) and the offset ([S-1485]). No week, day of the year or
  time zone line at any size (retired, [S-666]).
- **[S-1480]** Time zones are IANA ids (`Asia/Tokyo`) resolved through `ZoneCatalog`: an explicit
  IANA → Windows table frozen from Windows' own `timezoneMapping.xml` (CLDR), because under
  `InvariantGlobalization` .NET cannot convert IANA ids (no ICU) while Windows ids always resolve.
  Unknown or missing zones resolve to null and the clock falls back to local [S-53]. A test checks
  every Windows id in the table resolves on the machine.
- **[S-1481]** The zone picker is a search over every listed city (one per place: old names such as
  `Asia/Calcutta` and bare `Etc/GMT±N` resolve but are not offered), cities starting with the query
  first, with "Current: <places>" (unset = local) always first. Enter takes the best match.
- **[S-1482]** The label (original S-07/S-18/S-19): the city of a chosen zone, else none (a clock on
  the machine's own zone has no label), the first part of the line under the time ([S-6741]).
  Deliberate difference: choosing any city labels the clock, even one in the machine's own Windows
  zone (a Windows zone covers many cities).
  *(Retired, user request 2026-10-19: a label of the user's own over the time, `customLabel`, its
  menu line and its 40-character cap. A stored `customLabel` is an unknown property and is not
  read.)*
- **[S-1483]** Style Digital / Analog / Stacked (original S-10..S-13; three of the seven styles now,
  [S-6011]). Stacked always stacks the hours over the minutes (over the seconds), the meridiem after
  the last line on the step's unit; a digital face stacks on its own where that draws a larger step
  ([S-6741]). The AM/PM marker counts towards the readout's width by its own glyphs
  (`ClockFormatter.WidestMeridiem`, `HeroFit.Ems`): Tamil's "பிற்பகல்" is about four times as wide
  as "PM".
- **[S-1484]** Alignment Left / Centre / Right for the whole face and its line (original S-25/S-26),
  centred by default; set on the properties panel as every widget's alignment is ([S-6580]), not on
  the clock's page. Beside a dial or a day on a strip the words read from it outwards, left-aligned.
- **[S-1485]** Offset line None / From UTC ("UTC+2", "UTC+5:30", "UTC−3") / From local ("+8h local",
  "same as local"), minus as U+2212 (original S-08/S-16/S-23), the last part of the line under the
  time ([S-6741]).
- **[S-1486]** `ClockConfig.Repair`: unknown enum values and unknown zone ids are repaired to
  defaults on load, each logged.
- **[S-1487]** Clock ladder: the face on its step, then its one line ([S-6741]); the face never takes
  the room the line leaves (no fill, [S-6702]). No week, day-of-year or zone line (retired,
  [S-665]). Not ported: an XL list of extra world-clock rows (each world clock is its own widget, as
  in the original).
- **[S-1488]** The analog dial at every tier ([S-6743]): the dial alone where its line does not fit,
  the line under it on panels and columns, beside it on a wide strip. The second hand still sweeps
  (S-51, deliberate).
- **[S-1476]** *(Retired, group G5, 2026-10-29: the clock's own Label and date size, Small / Default /
  Large, `ClockConfig.TextSize`, its menu line and its strings. The line under the time is the
  readout's step's line under ([S-6701], [S-6741]), and the size of text is ⚙ › Appearance's text size
  for every widget (one setting, one level, [S-3590]). A stored `textSize` is an unknown property and is
  not read, [S-1050].)*
- **[S-1477]** *(Retired with [S-1476]: `ClockText` is gone.)*
- **[S-1478]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style, what the style draws ([S-6074]): Seconds and Date (switches); then
  Appearance ([S-3723]), a divider, and **Timezone**, a search
  ([S-3722]): the machine's own zone first as "Current: …", the cities that match after it (40 at
  most, each with its offset now), the chosen one with the dot, a pick stores the IANA id; after it
  on the same level Offset (None / From UTC / From local; user request 2026-10-19: what is shown is
  chosen on the first level, never inside a search); after a divider a link to **Time format**, the
  12- or 24-hour clock every widget tells time on, which ⚙ › Language & region alone sets ([S-3749],
  [S-3590]; its value what is in force). (Tests `ClockMenuTests`.)

- **[S-6741]** **The digital, stacked and minimal readout** (`ClockFace.Readout`, group G5): the
  Readout block on the largest Hero or Readout step ([S-6737]) on which the value ("88:88", ":88" more
  with seconds) and the meridiem as its unit (the step's unit, secondary), and the line under, fit the
  content box; never a size of its own, never filling the box. Stacked sets the hours over the
  minutes (over the seconds), each line the step's; Digital and Minimal stack where that draws a
  larger step, or where the row is too wide even on 28 and the stack is not. Where neither fits 28,
  the meridiem gives way first, then the seconds (and the clock then wakes once a minute, [S-52]). The
  one line under it is the step's line under (Caption under 28, Body under 40 and 68, Title at 400
  under a Hero step), secondary, cut with an ellipsis: the city, the date and the offset, " · "
  between them, as its room holds them: the long date, else the short one, else without the offset,
  else the city alone (`ClockFace.Under`); a box too short for it sheds it.
- **[S-6742]** **Flip on the cards' steps** (`ClockFace.Flip` over `FlipRules`): the cards ("88"
  each) on the largest step on which they, 8 apart, and the meridiem 8 after the last card on the
  step's unit fit the width and the line under the cards fits the height; one above the other where
  that draws a larger step (a tall column) or where a row does not fit even on 28. A meridiem that
  does not fit beside one 28 card gives way. The card is `FlipCard` (root [S-6740]): the item layer's
  chip, the one radius, the figures primary, the split a hairline in the Track role.
- **[S-6743]** **The analog dial from the family's strokes and roles** (`ClockFace.Dial`): the rim a
  hairline in the control stroke; twelve hour marks of stroke 2, secondary; from a dial of 160 the
  minute marks, hairlines, tertiary; the hour hand a track (4) and the minute hand a stroke (2) in the
  primary role, round; the second hand a stroke (2) in the accent, the dial's one moving meter; the
  hub an 8 dot, primary. Its line (Body, secondary) stands 8 under the dial while the dial keeps 96,
  else the dial is alone; on a strip of a regular width or more the line stands 16 beside a dial of
  the content's height (at most 0.45 of its width). A dial is never under 40.
- **[S-6744]** **The Date style on the steps** (`ClockFace.Date`): the day of the month a Readout
  ("88") on the largest step on which it fits the width and its weekday over it and its month under
  it, both on the step's line under, secondary, fit the height (on a strip: the day's line and the
  two words beside it, 12 after the day); the weekday is no longer the accent (no text in the
  accent, [S-6706]). A word too wide for its line in its wide name takes the short one; where the
  lines do not fit even 28 the month gives way, then the weekday.
- **[S-1489]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-5300]** *(Retired: the Date widget, kind 131, is gone; the weekday and the date, no time,
  made for the wallpaper, is now the clock's **Date** style (`ClockStyle.Date`, glyph E8D1
  GotoToday, [S-6011]). A state file holding a `Date` widget drops it at load like any unknown kind,
  [S-1900], [S-1050].)* The style keeps the rules below, in the clock's own zone: a world clock's
  date is that place's.
- **[S-5301]** Ladder: [S-6744]. A column (weekday, day, month) on every tile but a strip; on a Wide
  strip of Regular width or more the day stands with the weekday over the month beside it, the pair
  placed by the alignment. Concise ([S-666]): a larger tile sets the date on a larger step and adds
  nothing else.
- **[S-5302]** Words through `LocaleFormat` only (`DateText`): the weekday wide or short, the day in
  the language's digits, the month by name. Widths are judged on worst-case names (13 characters
  for a weekday, 11 for a month): a line too narrow for its wide form takes the short name. The
  tooltip is the full date.
- **[S-5303]** *(Retired: the Date widget's own settings. Show year is gone: the year is rarely the
  news, and the other clock styles' dates leave it out too.)* The clock's menu offers the Date style
  only the alignment ([S-1484]) and the zone ([S-1481]); seconds, the date line and the offset do not
  apply to it and are not shown.
- **[S-5304]** One tick a minute on the shared ticker (`ClockConfig.TickInterval`): the date turns
  over within a minute of midnight.
- **[S-2028]** A click on the card ([S-2000]) opens Windows' Clock app (`CardOpen.ClockApp`, its
  Apps-folder item); the module decides it (`ClockModule.Opens`).

Needs a manual pass: every style at every tier on the live desktop (showcase views, [S-1810]); a
flip turning over on the minute; the zone picker with a live search.
