# Clock

The module `clock` ([S-6911]): the time in seven styles (Digital, Analog, Stacked, Flip, Words,
Minimal, Date), in a zone of its own, with a label, the date and the offset where they fit. Its tags
are this file's alone; the root `SPEC.md` holds the host's sections (styles [S-6000], the flip card
the countdown shares [S-6032], `FlipCard` in the SDK; header defaults [S-700]; alignment [S-6580];
hero readouts [S-641], [S-659]; the regional clock [S-3770]; the ladder rules [S-660]..[S-666]).
The view is XAML (`ClockView.xaml`, compiled in this library).

Owning files: everything in this folder: `ClockModule.cs` (descriptor, view factory, menu lines,
JSON context), `ClockConfig.cs`, `ClockFormatter.cs` (the reading, the offset, the words),
`ClockText.cs` (the text sizes), `WordClock.cs`, `DateLayout.cs` (the Date style), `ZoneCatalog*.cs`
(the zone table and picker), `ClockView.xaml(.cs)`, `ClockMenu.cs` (its properties-menu lines), `Preview/` (Debug
renders), `Localization/Locales/` (its own `ClockStr`, 30 languages, the word clock's rules among
them). Tests: `Tests/` (run by `tests/Themia.Modules.Tests`).

- **[S-50]** 24-hour time is zero-padded; 12-hour time is not, and carries AM/PM. Midnight and noon
  read as **12**, not 0.
- **[S-51]** Hands sweep rather than step: the hour hand advances with the minutes, the minute hand
  with the seconds.
- **[S-52]** A clock without seconds (and with a digital face) asks for a **1-minute** cadence. An
  analog face always needs seconds, because its second hand sweeps. The word and minimal styles
  never draw seconds, so they never wake for them ([S-6015]).
- **[S-53]** An unknown or malformed time-zone id degrades to the local zone rather than throwing —
  this runs on every tick.
- **[S-54]** Formatting is done explicitly, not with culture patterns: the app builds with
  `InvariantGlobalization`, so culture format strings would not honour the config.
- **[S-6011]** The clock's styles ([S-6000]), `ClockConfig.Style` (`ClockStyle`, stored as
  `"style": "digital"`; what was its face): **Digital**, **Analog**, **Stacked** ([S-1483]),
  **Flip**, **Words**, **Minimal** and **Date** ([S-5300]), in that order. Every style but Date
  keeps the clock's ladder ([S-1487]): the face first, then the label, the date and the offset
  where they fit, the face taking what they leave; the settings (seconds, date, zone, label,
  alignment, text size) are the same for those six. Date has its own ladder ([S-5301]) and only
  the alignment and the zone ([S-5303]).
- **[S-6012]** **Flip**: a split-flap card per part of the time (hours, minutes, and seconds when
  shown), side by side, one above the other in a tall column, as large as the face's room allows;
  a rounded card in the subtle fill with a hairline split across its middle, the digits as large
  as the card's figure share holds "88" ([S-659]), AM/PM a caption in the hours card's corner (left out on a card
  under 96 DIPs). A card whose digits change on a tick turns over (folds away, 110 ms, and the new
  digits fold in, 170 ms); a config, language or size change redraws without a turn, and nothing
  turns while Windows' animation effects are off ([S-1010]). One card implementation
  (`FlipCard`) for the clock and the countdown's Flip style ([S-6032]).
- **[S-6013]** **Words**: the time as a sentence in five-minute steps, in the current language
  ("It's twenty to eleven", "Es ist zwanzig vor elf"), in the clock's own zone
  (`ClockFormatter.Words`); the label and date rungs as for the digits. Composed by `WordClock`
  ([S-6020]..[S-6024]).
- **[S-6014]** **Minimal**: the digits in Segoe's light weight (AM/PM and seconds in it too) and
  nothing else at any size: no label, date or offset, and no seconds ([S-6015]). It still stacks in
  a tall column when that draws it larger ([S-1483]).
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
- **[S-6024]** The type: the largest step (the ramp, then `TitleLarge` scaled, as [S-641]) at which
  the language's **longest** sentence of the day, wrapped at words, fits the face's room
  (`WordClock.Fit`, `WordClock.Longest`), so the text keeps its size through the day. Width is
  estimated per character, a Han, kana or Hangul character as 1.75 average advances; a sentence
  without spaces wraps anywhere, as the text block does.
- **[S-645]** The clock insets its readout fully at the sides and by half above and below.
- **[S-665]** Clock: time → date (short "Sun 4 Oct" when narrow; with the offset ([S-1485])
  appended on a wide strip) → the offset line. No week, day of the year or time zone line at any
  size (retired, [S-666]). A tall column stacks hours over minutes when that draws them larger.
  Analog face unchanged.
- **[S-1480]** Time zones are IANA ids (`Asia/Tokyo`) resolved through `ZoneCatalog`: an explicit
  IANA → Windows table frozen from Windows' own `timezoneMapping.xml` (CLDR), because under
  `InvariantGlobalization` .NET cannot convert IANA ids (no ICU) while Windows ids always resolve.
  Unknown or missing zones resolve to null and the clock falls back to local [S-53]. A test checks
  every Windows id in the table resolves on the machine.
- **[S-1481]** The zone picker is a search over every listed city (one per place: old names such as
  `Asia/Calcutta` and bare `Etc/GMT±N` resolve but are not offered), cities starting with the query
  first, with "Current: <places>" (unset = local) always first. Enter takes the best match.
- **[S-1482]** Label over the time (original S-07/S-18/S-19): the custom label, else the city of a
  chosen zone, else none. Deliberate difference: choosing any city labels the clock, even one in the
  machine's own Windows zone (a Windows zone covers many cities). Labels are capped at 40 characters.
- **[S-1483]** Style Digital / Analog / Stacked (original S-10..S-13; three of the seven styles now, [S-6011]). Stacked always stacks hours
  over minutes; seconds and AM/PM ride a step down beside the minutes. A digital face still stacks on
  its own in a tall column when that draws the digits larger. The AM/PM marker counts towards the readout's width by its own
  glyphs (`ClockFormatter.WidestMeridiem`, measured by [S-659]): Tamil's "பிற்பகல்" is
  about four times as wide as "PM".
- **[S-1484]** Alignment Left / Centre / Right for the whole column (original S-25/S-26), centred by
  default; set on the properties panel as every widget's alignment is ([S-6580]), not on the clock's
  page. Beside an analog dial the text stays left-aligned to the dial.
- **[S-1485]** Offset line None / From UTC ("UTC+2", "UTC+5:30", "UTC−3") / From local ("+8h local",
  "same as local"), minus as U+2212 (original S-08/S-16/S-23). On a wide digital strip it joins the
  date line.
- **[S-1486]** `ClockConfig.Repair`: unknown enum values, unknown zone ids and over-long labels are
  repaired to defaults on load, each logged.
- **[S-1487]** Clock ladder: face → label → date (short when narrow; + offset on a wide strip)
  → offset; the face takes the rest. No week, day-of-year or zone line (retired, [S-665]). Not ported: an XL list of
  extra world-clock rows (each world clock is its own widget, as in the original).
- **[S-1488]** The analog dial at every tier, sized by the ladder (40 px floor, rebuilt only when the
  diameter changes): dial alone on XS, text under it on panels and columns, text beside it on a wide
  strip. The second hand still sweeps (S-51, deliberate).
- **[S-1476]** Label and date size (issue #170: the place and the date were hard to read): a
  per-widget `ClockConfig.TextSize` Small / Default / Large (under Style on its properties menu, [S-1478]). It moves the
  label's, the date's and the offset line's floor and ceiling along the type ramp (`ClockText`: Large
  lifts the floor one step, off Caption, and the ceiling two, so a date can reach Title; Small lowers
  the ceiling one) and scales the height share each line may ask for (×1.6 / ×0.75). Default is the
  ladder of [S-1487] unchanged. The lines stay regular weight (support, not a second title); the
  short-date switch measures against the size's floor; the time takes what the lines leave.
- **[S-1477]** `ClockText.Shift` walks the ramp and stops at its ends, so no size invents a font size.
- **[S-1478]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Under Style, what the style draws ([S-6074]): Seconds, Date (switches) and
  Text size (Small / Default / Large); then Appearance ([S-3723]) and **Timezone**, a search
  ([S-3722]): the machine's own zone first as "Current: …", the cities that match after it (40 at
  most, each with its offset now), the chosen one with the dot, a pick stores the IANA id; under the
  answers Label (asked beside the menu, [S-3721]: what is there now in the box, empty clears it) and
  Offset (None / From UTC / From local). (Tests `ClockMenuTests`.)

- **[S-1489]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-5300]** *(Retired: the Date widget, kind 131, is gone; the weekday and the date, no time,
  made for the wallpaper, is now the clock's **Date** style (`ClockStyle.Date`, glyph E8D1
  GotoToday, [S-6011]). A state file holding a `Date` widget drops it at load like any unknown kind,
  [S-1900], [S-1050].)* The style keeps the rules below, in the clock's own zone: a world clock's
  date is that place's.
- **[S-5301]** Ladder (`DateLayout.Plan`, on `Disclosure` like the clock's [S-1487]): the day of the
  month is reserved and takes what the rungs leave (`FluentType.Hero`, two figures); the weekday
  (accent, semibold, up to Title) is the first rung, the month line (secondary, regular, up to
  Subtitle) the second. A column (weekday, day, month) on every tile but a strip; on a Wide strip of
  Regular width or more the day stands as tall as the strip with the weekday over the month beside
  it, the pair placed by the alignment. Concise ([S-666]): a larger tile sets the date larger and
  adds nothing else.
- **[S-5302]** Words through `LocaleFormat` only (`DateText`): the weekday wide or short, the day in
  the language's digits, the month by name. Widths are judged on worst-case names (13 characters
  for a weekday, 11 for a month): a line too narrow for its wide form takes the short name. The
  tooltip is the full date.
- **[S-5303]** *(Retired: the Date widget's own settings. Show year is gone: the year is rarely the
  news, and the other clock styles' dates leave it out too.)* The clock's menu offers the Date style
  only the alignment ([S-1484]) and the zone ([S-1481]); seconds, the date line, the text size, the
  label and the offset do not apply to it and are not shown.
- **[S-5304]** One tick a minute on the shared ticker (`ClockConfig.TickInterval`): the date turns
  over within a minute of midnight.

Needs a manual pass: every style at every tier on the live desktop (showcase views, [S-1810]); a
flip turning over on the minute; the zone picker with a live search.
