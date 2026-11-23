# Calendar

The module `calendar` ([S-6911]): the synced calendars in one of five styles (month, week, day,
agenda, year). Its tags are this file's alone; the root `SPEC.md` holds what it shares with the host
and the other calendar widgets, with their root tags: where events come from ([S-560]), the shared
source and seam ([S-588], [S-2701], [S-3602]), the shared menu lines ([S-3601], the SDK's
`OfficeMenuLines`), and every rule it is drawn by (`CalendarRules`, `CalendarFit`, `AgendaRules`,
`CalendarText`, `CalendarLinks`, `CalendarShare`, in the SDK, with their tests in Core).

Owning files: everything in this folder: `CalendarModule.cs` (descriptor, view factory, JSON
context), `CalendarWidgetConfig.cs` (the style, the week's and agenda's own settings),
`CalendarView*.cs` (one partial per style, the links, over `IWidgetHost.Office`), `AgendaSpec.cs`
(the agenda on the kit's list), `CalendarSizing.cs` (each style's sizes and what it draws, from the
config), `MonthEvents.cs`, `MonthPlan.cs` (the month with titles' weeks, lines and "+k more", pure),
`CalendarMenu.cs` (its properties-menu lines: the SDK's calendar lines and the style's own), `Preview/` (Debug renders:
`--state sample|selected|linked|days|stale|empty|titles`), `Localization/Locales/` (its own `CalendarStr`, 30
languages). Tests: the rules' are Core's; the config's are the module host's (`CalendarHostTests`,
`OfficeConfigTests`); the agenda's items (and the narrow week's rows) are `Tests/AgendaSpecTests.cs`; the week's fit is Core's `CalendarFitTests`; the menu's `Tests/CalendarMenuTests.cs`; the sizes'
`Tests/CalendarSizingTests.cs`; the month with titles' `Tests/MonthPlanTests.cs`.

**Sizes** (owner decision 2026-11-12, [S-2438]): each style's size follows its own settings and never
the box; no `Disclosure`, no tier, no width decides what a style shows (the weekday names and a
chip's times still shorten with the column, as text does). How many of the same item a style shows (a day's or a
week's hours, a week's days, the events a month day lists) is the card's (owner, 2026-11-20: "Anything that's just
'how many of the same item' (days, hours, weeks, processes) follows the card's size", [S-14410]): the card shows as
many as it holds at their optimal, stretched to fill it, every axis taking every grid cell ([S-14500]); no menu line
sets the count and a resize stores none ([S-14502]); a list's Rows stay a setting, its natural height.

- **[S-11540]** **Agenda**: `Rows` (1..20, a new one 6) and `Lines` (1, a new one, or 2: the span,
  where and who on the second) on the kit's rows contract ([S-2440], `IKitRows`; the schedule look's
  rows, [S-11541]): its natural height is its rows at their optimal, 260..520 wide (a new one 300); Rows decide its natural height; the card shows the rows it holds at their optimal, stretched to fill it, and a resize stores none ([S-14500], [S-14502]);
  one column at every width, the pages holding the rows the card holds (a day's heading takes a row). Its Style lines add Rows and Lines after the range and the count.
- **[S-11542]** **Year**: twelve months, a square a day (8; a new one's) or, with **Day numbers** on (its
  Style line, a mode, `YearNumbers`), the day numbers under the weekday letters (a Caption line a day): natural (and
  least) 4 × 3 months' cells, 320 × 240 and 520 × 450 (`CalendarSizing.Year`, `YearCell`, `YearSizing`). The card's size
  is the user's (owner, 2026-11-20: "the size should not be settable, it is set by the size of the widget"; the old Size
  line, [S-2449], is gone): a larger card draws the twelve months larger ([S-15672]). Tests `CalendarSizingTests.S11542_`.
- **[S-15672]** *(Replaced by [S-15780]: the year's months shared the card and grew with it, which WR-4 and WR-6 do not
  allow a repeated item; WR-9's "grid of the card" is the month's alone.)*
- **[S-15780]** **The year's months and the week strip's days keep their one size** (docs/WIDGET-RULES.md WR-4: "they
  should just keep their size and the last item will be cutoff to some extent then"; WR-6; WR-9: the month alone is a grid
  of the card). A year's month is a cell of one size at every size of the card: its small month and the 8 round it
  (`YearPad`) in whole grid cells (`CalendarSizing.YearCell`, 80 × 80 with squares, 130 × 150 with numbers), the small
  month at its own size centred in it; from the card's top edge (under a state's row, right under it) the cells touch
  from its left edge, as many a line as the width reaches (1..12, the last cut by its right edge, `YearCells` over
  `ItemSize.Cells`), the months in reading order on as many lines as they take, the last line the card reaches cut by
  its foot, the room past the twelfth empty. The week strip's days are columns of 40 in whole cells from the card's left
  edge, every day the width reaches (1..14, `WeekDaysIn`), the last cut by its right edge; a new strip is seven days,
  280. Before, the months shared the card (80 to 173 wide) and the strip's days shared its width (43 to 64). Tests
  `CalendarSizingTests.S15780_`, `S11545_`.
- **[S-11543]** **Day**: its **hours** (4..24, a new one 8; the card's height, no line, none stored: as many as the
  card reaches at an hour's optimal, `CalendarSizing.HoursIn`, [S-14502]), an hour a 28 row at its optimal, under one
  band line for the all-day items (its item's bar, or chips shared among several, "+N"; a day without
  gives that line to its hours): the band's row (12 + its line and 4) + the hours, 240..480 wide (a new one
  300). The hours are the wanted day (07-22 widened to its
  events) where it is no longer, else that many from just before the next event or now
  (`CalendarFit.HoursOf`).
- **[S-15671]** **The day's hours are its items** (docs/WIDGET-RULES.md WR-3..WR-6; owner, 2026-11-20: "they should just
  keep their size and the last item will be cutoff to some extent then"): from the card's top edge (under a state's row,
  right under it) the band's row when the day has all-day items (`CalendarSizing.DayBandRow`: the inset, its line and 4,
  from edge to edge, the line in the inset and the gutter as its padding), then the hours, each a row of the hour's one
  fixed size from the card's left edge to its right (`CalendarSizing.DayHours`), its label centred in it in the gutter
  and a hairline at its top across the timeline (none on the card's top edge), touching, every hour the card reaches,
  the last cut by its foot; a card taller than a day's 24 leaves the room under the last empty. The events stand over
  the rows on the timeline at their times. Tests `CalendarSizingTests.S15671_`.
- **[S-11544]** **Month**: six weeks always (the month's and the next days', dimmed), so every month is
  one height; **Events as** Dots (a new one's: a 28 cell, the number over its dots; the period's line, the weekday
  line and six weeks at their optimal, every cell between, the weeks and the columns stretched to fill the card, 280
  wide new, [S-14101], [S-14500]) or
  Titles ([S-11547]). The weeks fill the
  content edge to edge, the first week under the weekday line and the last week's marks on the foot's inset ([S-12400], [S-12807]): a box taller than their cells spaces the weeks evenly (the month is a grid of the card, not a list: it keeps filling it under [S-14810], WR-9).
  The day readout a narrow month fell back to is gone.
- **[S-11547]** **Month with titles** (owner request 2026-11-15: "a size where he actually shows the
  events", Outlook's and Windows' month view): each day cell its number over its **events a day** (1..6, a
  new one 3; the card's height, no line) chip lines, 20 each (`MonthPlan`, `CalendarFit.Bars`): a span
  (all-day, several days, an overnight train, a birthday) one bar on the chip fill across its days,
  its calendar's colour as the 2 px bar at its start, squared off where it runs on into the next week's
  row; a meeting a line on no fill, its calendar's dot, its start (from a 100 column) and its title,
  trimmed. A day with more items than lines says "+k more" on its number's line ("+k" under an 80
  column), the whole day on hover. The days around the month carry their events too (the month fetches
  all six weeks, [S-568]), dimmed as their numbers are: a chip wholly on them in tertiary text
  (`MonthPlan.Dimmed`), a span reaching into the month not. Today keeps its accent pill, the selected day its fill; a hairline
  parts the weeks. Seven columns of 56..180 (a new one 100) + the inset: 416..1284 wide (a new one 724);
  the height is the period's line with the inset, the weekday line and six times (4 + the number's 16 + the lines' 20
  each), every part whole grid cells ([S-14101]),
  the chip lines are what a week holds ([S-14502]). Debug render:
  `--style month --state titles` (the sample in this look, at its own min, natural and max).
- **[S-14101]** **The month fills its card and keeps its dots longer** (owner, 2026-11-20: "the calendar does not
  always stretch the contents to the top and bottom although it would be easily possible."; "the calendar shows the
  individual events too early, the items should stay small for a little longer."). The card takes every grid cell
  ([S-14500], `CalendarSizing.MonthSizing` over `MonthOver`: the period's line with the inset over it and the weekday
  line, each whole cells as `MonthLayouts.Plan` lays them); its seven columns share the width and its six weeks the
  height under `MonthOver` evenly on whole device pixels, so at every size the weeks fill the card from under the
  weekday line to its foot, alike, none left over;
  a state's row (Set up, an account gone) stands in the period line's band, so the weeks keep the room the sizing made.
  A dots month's cell shows its day's events as chips only once it holds two chip lines under its number in a column
  where a chip's title reads, 56 ([S-15782], `MonthChipRoom.Dots`; before, one chip line in a column of
  44 turned a 40-tall cell into chips of four letters); a month with titles, chosen, shows them from one chip line in its
  least column of 56 (`MonthChipRoom.Titles`).
  Tests `MonthLayoutTests.S14101_`.
- **[S-15782]** **Bigger numbers, chips once a cell clearly holds them** (WR-9: "Bigger numbers too"; "A day cell keeps
  its small marks until it clearly holds a readable chip line"): a day's number grows with its cell until what the chips
  or dots leave of its height or its two figures across the column inside the cell's 4 stop it
  (`MonthLayouts.NumberFits`: figures of 0.56 em; at most a line of 64), no longer at half the column's width (a 520 × 600
  month's '1' stood 19 DIP tall in a 97 DIP week); a dots month shows chips from two chip lines in a column of 56, where a
  month with titles reads them (one more line than a month with titles: the "little longer"), no longer from 80 (a 74 wide
  cell with room for four chip lines showed dots). Tests `MonthLayoutTests.S15782_`, `S14101_S15782_`.
- **[S-15789]** **The day's band is an hour's size; a list's least is one item** (WR-6, WR-14): the day's all-day band row is
  an hour row's one size (30 on the default grid, its line centred in it, `DayBandRow`), so the hours under it stand on
  the grid (before: 44, every hour at 44 + 30k); the day's least is its band and one hour (the hours every one the card
  reaches, at least one), the year's one month's cell (80 × 80).
- **[S-15791]** **The event pointed at elsewhere is the one lit** (WR-13): every part of the calendar dims while an event it
  draws is pointed at in another widget, the parts drawn before that event's too (they had been drawn whole while it was
  not yet drawn: the 22nd's Standup, days 1..21), a birthday's chip with them (`LinkOther`); a dots month's dots are their
  events' marks (`LinkEvent`), the event's dot whole and every other dot dimmed, so the small month lights it too.
- **[S-15792]** **Chips only where they clearly read** (WR-9: "A day cell keeps its small marks until it clearly holds a
  readable chip line"; replaces [S-15782]'s 56, which a second audit read as too early, titles of three to six
  letters): a dots month shows chips from two chip lines in a column of 100 (`CalendarFit.ChipTimeFrom`: a chip's start
  and a title of words), a month with titles from one line in that column; narrower, both keep their dots. Tests
  `MonthLayoutTests.S14101_S15792_`.
- **[S-15794]** **A larger card never draws smaller numbers** (WR-9: "Bigger numbers too"; a regression of [S-15792] and
  [S-15782] the third audit measured: 300 × 220 drew 9 DIP numbers against 12 at 300 × 210, a month with titles 9 at
  700 × 510 against 13.5 at 392 × 270): a day's dots stand in its cell's foot padding under the number's line, so a cell
  with dots draws its number as large as one without; with chips the number takes what the chip lines the cell must
  show leave of its row, up to a line of 32 (`MonthLayouts.MostChipNumberLine`), and more chip lines only past that.
  Within a face the number line never shrinks as the card grows. Tests `MonthLayoutTests.S15794_`, `CalendarSizingTests.S11547_`.
- **[S-15795]** **Edges in the week and the day** (WR-1, WR-3): the week strip's day head carries the inset over its words
  as its own padding, so the chosen day's fill starts on the card's top edge with its item (it stood 12.7 under it); the
  day's timeline, its events and its hours' rules run to the card's right edge (12 short of it before), the hours' labels
  holding the gutter at its left. With the status's glyph in the weekday line's last column (a stale sync), the weekday
  letters take the form a column holds beside it, so none is cut or pushed out of its column (at 260 wide "Sa" stood
  14 DIP off its column, its "S" cut).
- **[S-16442]** **A day's dots keep its cell's padding under them** (WR-3: "the inset is padding inside the item, never a
  margin round the items"): the dots stand under the number's figures, sinking into the cell's foot padding only as far
  as the room under the figures is short of a dot and its 2 (`MonthLayouts.DotSink`, `DotGap`), so a last week's dots no
  longer stand on the card's foot and its border (they stood in the padding's whole 4, at 596..600 of a 600 card). Test
  `MonthLayoutTests.S16442_`.
- **[S-16446]** *(Retired 2026-11-23, the same day: the owner chose for the year's months, as for the forecast's grid,
  "Fixed size, gap at right" (docs/WIDGET-RULES.md, exemptions). The months keep their own cell at every card size in the
  wrapped cells of [S-15780] and [S-16438], the room past the last whole column and under the last line empty; they no
  longer share the card nor scale up with it.)*
- **[S-16448]** **Chips only where they read** (WR-9, owner 2026-11-20: "the calendar shows the individual events too early,
  the items should stay small for a little longer"): a month shows its events as chips only in columns of 130 or more
  (`CalendarFit.ChipReadsFrom`: a start and about ten letters of a title in Caption; at 100 a chip read "09:00 Stand…"),
  its dots narrower; a new month with titles is 130 a column. Test `MonthLayoutTests.S16448_`.
- **[S-16449]** **A day's marks stand whole in its cell** (WR-9; the least month, 160 × 210, cut its dots in half at both
  edges of a 22.7 column and today's disc flat with "23" past it): as many dots a line as stand whole in the cell's padding,
  the "more" dot among them (`MonthLayouts.DotSlots`); today's pill may reach into the cell's side padding (a fill, never
  text), at most the column wide, so it holds its figures whole. Test `MonthLayoutTests.S16449_`.
- **[S-16443]** **The period's line carries the state from the first draw** (WR-9, WR-10): whether the line or the kit's
  status shows a stale sync's problem glyph is decided before the face is laid, so the month's weekday heads never leave
  room for a glyph that stands on the line (on the first draw they did: at 330 × 210 "Sa" stood 13.7 off its column and
  the heads shortened to "S M"; at 160 × 210 the Saturday head was dropped).
- **[S-16444]** **The period's line inks from the card's inset** (WR-1: "there should not be a space at the top"): the
  line is drawn raised by the room over its first ink (`ToolLine.InkOnTop`: the arrows' glyphs, or the period's capitals
  where the arrows gave way), so a week strip 40 wide no longer stood its title's capitals 18 down.
- **[S-15798]** **The month's least is where its numbers still read** (WR-14: "sized down to where its smallest text is
  still read"): a dots month goes down to a Caption number line of 16 in its cell's 4 over and under, six weeks under the
  weekday line (180 tall), in columns its two figures fit across (160 wide), no longer 260 × 210. Tests
  `MonthLayoutTests.S15798_`.
- **[S-11545]** **Week, the day strip** (a new one's): a row of day items on the card's top edge (WR-1..WR-3,
  [S-12815]): the days stand from its left edge at their one column ([S-15780]), touching, each its
  head (its weekday, its number) at the card's inset over the room its marks take at most ([S-2282]: three, then "+n");
  the strip's band ends on a grid line (`CalendarSizing.StripBand`, 100 on the default grid) and the chosen day's
  **Rows** (1..12, a new one 3; Style line) stand right under it as the agenda's one-line rows ([S-2285]), the last cut
  by the card's foot: one height for the config, a 40 column a day at its optimal (a new one's seven days 280), as many
  days as the card's width reaches, the last cut by its right edge, and the rows its height reaches ([S-14410], [S-14502]: none stored by a
  resize). Under a state's row the strip starts right under it. Before, the strip stood in the inset with 8 under it,
  its first row 40 DIP under the last ink over it.
- **[S-11546]** **Titles in the week** (a Style switch, off by default): the columns hold their events'
  titles, each 96 wide at least ([S-2281]), so a new card is the days × 96 + the hours' labels (44) + the inset wide;
  one band line of all-day bars over an hour grid of its **hours** (4..16, a new one 8); the days are what the card's
  width holds and the hours what its height holds, stretched to fill it ([S-14410], [S-14502]: no line sets either and
  a resize stores neither). The per-column chip lists the week drew between the two looks are gone.

- **[S-3650]** The agenda is the kit's paged list ([S-7035], [S-7036]): `AgendaSpec` gives its spec
  (the schedule look on the family's rows, two columns at most) and an agenda row as a kit item: its
  day group (the selected day's heading primary, [S-3622]), an
  event's part on the day ("All day" between a long event's first and last day, "Now" in the strong
  step while it is under way, start over end, the span for a narrow row), where and who, the
  calendar's colour (a 2 px bar) when colours show; a birthday its gift (neutral: not a state), its
  name and "Birthday". The view pages it with the card menu's back and forward ([S-12481]: the agenda has no period line) and the keys and hooks each event's row for its
  tooltip, its links across widgets and the click that opens it.
- **[S-3651]** The calendar's one stale rule ([S-7042]): each account whose last sync could not improve
  on the events held is a kit outcome (the office feed's, `OfficeScreens.Outcomes`, [S-7077]: its label, its problem in the kit's
  failure vocabulary, `SyncProblems.ToFetchStatus`, when its events are from); the problem
  glyph and the ring stand on the period's line beside back and forward ([S-569]) in every style but
  the agenda, whose state is the kit's status over its rows' top end (`KitStatus`, [S-12481]); the card menu's Refresh line says the age of the oldest copy.
- **[S-3652]** The agenda's one open gesture ([S-7065]): a click focuses a row (the ring), a
  double-click or Enter opens its event where it lives ([S-1268]); a row's menu offers Open; a birthday
  or an event with nowhere to open offers nothing. Its cursor keys are its kit list's (Up, Down, End,
  Enter, the Menu key; with nothing focused a key lands on the page on screen, and the cursor turns
  the page with it); Left, Right, Page Up and Page Down page, Home goes to today, Escape lets the
  cursor and the selected day go. The other styles keep their click and Enter ([S-2710]).
- **[S-3653]** The calendar is drawn from the family's tokens only (G2, 2026-10-29): every grid inside
  the card's 12 inset, Caption and Body texts at their own lines, today's accent pill or disc, the
  marks as 2 px bars, 4 dots or fills in the calendar's data colour (`Family.Data`), the accent never
  on a text, the one radius, spacings and paddings on the 4 scale; the states are the kit's state row
  (a problem's warning, the Accounts button); with no account the Set up row says "Add an account to
  show your calendar" ([S-1862]).
- **[S-7513]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone). Under Style only what the style uses ([S-3601]): the week's **Starts on**
  (the first day of the week, named, or Today); the agenda's
  **Date range** (Today, Today and tomorrow, 3, 7 or 30 days, and Custom…, which asks for 1 to 365
  days and holds the dot while the range is none of the others; the line shows the range) and
  **Agenda events** (1 to 100, asked for as text, [S-3726]), Rows and Lines ([S-11540]); the month's Events as
  ([S-11544]); the year's Day numbers ([S-11542]); the week's Titles in the week, then (without them) its Rows
  ([S-11545], [S-11546]). A day's and a week's hours, a week's days and a month day's events are the card's
  ([S-14410]): no line. Then Appearance, the calendar widgets'
  **Accounts** and **Calendars** (`OfficeMenuLines.CalendarAccounts`, [S-561]) and, after a divider,
  **Show contact birthdays**, offered only while a chosen account has contacts to read them from
  (`OfficeMenuLines.OffersBirthdays`, [S-575], [S-6070]: a CalDAV calendar has no address book, and
  with no account there is nothing to read). A style laid out in weeks (month, week, year) follows
  ⚙ › Language & region's **Start of week**, no link row ([S-3590], [S-3749]).

- **[S-1265]** Week columns ≥64 px spell the weekday. *(Its chips' start–end retired with the per-column lists, [S-11546].)*
- **[S-2281]** The week has two looks, chosen by its config ([S-11545], [S-11546]; the column width
  no longer decides): titles in the week, its columns at least 96 (`WeekTitlesFrom`), the band and the
  hour grid ([S-2704]), nothing chosen and no rows under it; or the day strip, its columns' marks
  ([S-2282]) and the chosen day's events as rows under the week ([S-2285]).
- **[S-2282]** A narrow column's marks (`CalendarFit.Marks`, `MarksHeight`): under the day's number,
  one per item of the day (birthdays, then all-day events, then timed ones by start, [S-570]), each a
  round bar a track (`FamilyShape.Track`) high, the column's width less the family's 4 either side, 4
  apart, in its calendar's colour (a birthday's tertiary, the accent with colours off); at most three,
  then "+n" in Caption, tertiary. The head and its marks take the click that chooses the day and list
  the day on hover. Today keeps the accent disc on its number.
- **[S-2283]** *(Retired by [S-11545]: the strip and the rows are the config's, one height.)*
- **[S-2284]** The chosen day (`CalendarFit.ChosenDay`) is the selected day ([S-3620]) while it is in
  the week, else today while the week holds it, else the week's first day; its column head stands on
  the selection fill (the item layer's selected chip). A click on a day's head or marks selects it (a
  click on the selection lets it go, back to today), Up and Down move it a week ([S-1269]), Escape lets
  it go, Enter opens its first event. The period's line (its title and its two buttons) pages the week as before.
- **[S-2285]** The chosen day's rows are the agenda's ([S-3650]: one row path, the view's one kit
  list, `AgendaSpec.DayRow`): the calendar's colour bar as the lead (a birthday's gift), the title,
  the start ("All day") as the value; one line a row; in no group, so no day heading ([S-2280]); the
  config's rows ([S-11545]), the rest on the day head's hover. A day with nothing says "No events today" (another day "No events") in the Status
  block's row ([S-6714]).
- **[S-1269]** Keys (classic S-31..34) reach `CalendarView.HandleKey(WidgetKey)` through the lease
  ([S-806]); the map is [S-2710]. Up/Down move the cursor day a week in the month (outlined) and the
  week (its column head underlined, or on the selection fill in a narrow week, [S-2284]); Escape lets it go. The cursor day is the selected day, shared
  with the view's other calendars and kept across pages ([S-3624]).
- **[S-2700]** A calendar widget draws only its own layout (month, week, day, year or agenda: its
  style, [S-3640]), at every size. No size swaps it for another layout, adds an agenda beside a
  month, or shows a single "next event" instead; what each layout shows is its config's
  ([S-11540]..[S-11546]).
- **[S-2706]** Day: the all-day band's shared line ([S-2718]) splits its width in chips of at least
  56 px, the rest "+N" (on hover); a day with nothing says "No events today".
- **[S-2708]** Month names in the year follow the small month's width: "September", "Sep", "S"
  (`LocaleFormat.MonthName`); the current month's in the accent.
- **[S-3606]** Debug renders: `--render widget Calendar <out> --style month|week|day|year|agenda
  --state sample|selected|linked|days` for each style: the fixed sample (two calendars, a one-day and a four-day all-day event, a trip across a
  week's end, an overnight event, overlaps, a long title, birthdays, a year of meetings) through a
  fixture `ICalendarData`; `selected` selects the day after tomorrow as another widget would ([S-3620]);
  `linked` puts another widget's pointer on one event ([S-3623]); `days` sets the style's own range
  (the week: the next four days from today; the agenda: today and tomorrow). A card that needs
  setting up runs its view for the preview (`WidgetCard.RunForPreview`).
- **[S-3625]** Paging (back, forward, today) publishes the days now on screen (`VisibleRange`) and moves
  no other widget: two months on a view can show two months, and only a selection is followed.
- **[S-2040]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `CalendarModule.Opens`); an IMAP, CalDAV or CardDAV account opens nothing.
  The day cells, events and the week's day headers keep their clicks ([S-2004]).

Needs a live pass: a signed-in Microsoft, Google and CalDAV calendar in every style (paging, opening
an event in Outlook or the browser, the hover link with Next meeting and the week planner on the
view), and the showcase views on the live desktop.
- View links: publishes and follows the day under the pointer in every style without moving ([S-11923]), besides the selected day, the range and the event ([S-3620]..[S-3625]).
- View links: an agenda row is its event alone, never its organiser or its day: one item, one light ([S-11965]).
- No card header ([S-12481]): the period's title and back and forward are the calendar's own first line, a
  tool line (`ToolLine`: the title as its label, the problem glyph, the ring, the two buttons) at the side
  inset with 4 under it, in every style but the agenda; each of those styles' sizing counts it
  (`CalendarSizing.NavLine`, 20 + 4, `CalendarSizing.ContentTop`). The agenda has no line: its pages are the
  card menu's back and forward where there is a page that way, and its state the kit's status over its
  rows' top end. The year's months, the day's band and hours and the week's day strip stand from the line's band foot
  as from a card's top edge (`CalendarSizing.LineBand`, `Lined`: their own sizing that band taller; owner, 2026-11-22:
  "basically reset them to what they were a few days ago"). Tests `CalendarSizingTests`.
- **[S-16231]** **The period's line is back over every style but the agenda** (owner, 2026-11-22: "basically reset them
  to what they were a few days ago"; "before 19th"): its band (`CalendarSizing.LineBand`, the card's inset, the tool
  line and its gap in whole grid cells, 40 on the default grid) stands on the card's top edge; the month's weekday line
  right under it (`MonthOver`), the year's months, the day's band row and hours and the week's day strip from its foot as
  they stood from the card's top edge ([S-15780], [S-15671], [S-11545]: each its own sizing that band taller,
  `CalendarSizing.Lined`); a state's row stands in the band's place. The least of a month is 160 by 210 ([S-15798] under
  the line), a year one month's cell under it. The line's problem glyph and ring are the state over a style with a
  line; the agenda keeps the kit's status. Back and forward on the line, the keys and the card menu page the calendar
  alone: no wheel ([S-3629] retired). Tests `CalendarSizingTests.S16231_`.

## The calendar header (`calendarHeader`, retired)

- **[S-14900]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The calendar split into a header widget (`calendarHeader`) and its body. The calendar is one widget
  again with its period's line; a placed header folds into the calendar under it on load ([S-16230]).
- **[S-14901]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The header's one line.
- **[S-14902]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The header's style, the period it paged by.
- **[S-14903]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The calendar without a period's line.
- **[S-14904]** *(Retired 2026-11-22: the owner reset the widget to its pre-19-Nov form: "before 19th. but the single metric widgets stay")* The ready-made group Calendar (the header, a month and an agenda): it only put the split calendar
  back together.
