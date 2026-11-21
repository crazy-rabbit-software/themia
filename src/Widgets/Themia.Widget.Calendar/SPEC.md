# Calendar

A family of two kinds ([S-6901], `[assembly: ThemiaWidgetFamily]`): the module `calendar` ([S-6911]), the synced
calendars in one of five styles (month, week, day, agenda, year), and the module `calendarHeader` ([S-14900]), the
period they show with its arrows. Its tags are this file's alone; the root `SPEC.md` holds what it shares with the host
and the other calendar widgets, with their root tags: where events come from ([S-560]), the shared
source and seam ([S-588], [S-2701], [S-3602]), the shared menu lines ([S-3601], the SDK's
`OfficeMenuLines`), and every rule it is drawn by (`CalendarRules`, `CalendarFit`, `AgendaRules`,
`CalendarText`, `CalendarLinks`, `CalendarShare`, in the SDK, with their tests in Core).

Owning files: everything in this folder: `CalendarModule.cs` (both kinds' descriptors, view factories, the JSON
context), `CalendarHeaderConfig.cs`, `CalendarHeaderView.cs` (the header, [S-14900]..[S-14902]), `CalendarWidgetConfig.cs` (the style, the week's and agenda's own settings),
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
- **[S-11542]** **Year**: twelve months, 4 × 3, a square a day (8; a new one's) or, with **Day numbers** on (its
  Style line, a mode, `YearNumbers`), the day numbers under the weekday letters (a Caption line a day): natural (and
  least) 272 × 256 and 496 × 448 (`CalendarSizing.Year`, `YearSizing`). The card's size is the user's (owner,
  2026-11-20: "the size should not be settable, it is set by the size of the widget"; the old Size line, [S-2449], is
  gone): a larger card draws the twelve months larger as one block (`CalendarSizing.YearScale`, a `ScaleBox`, never
  under 1), at the content's top-left in the inset. Tests `CalendarSizingTests.S11542_`.
- **[S-11543]** **Day**: its **hours** (4..16, a new one 8; the card's height, no line, none stored: as many as the
  card holds at an hour's optimal, `CalendarSizing.HoursIn`, [S-14502]), an hour a 28 row at its optimal, over one
  band line for the all-day items (its item's bar, or chips shared among several, "+N"; a day without
  gives that line to its hours): 36 + the band's line and 4 + the hours + 12, 240..480 wide (a new one
  300), the hours stretched to fill it. The hours are the wanted day (07-22 widened to its
  events) where it is no longer, else that many from just before the next event or now
  (`CalendarFit.HoursOf`).
- **[S-11544]** **Month**: six weeks always (the month's and the next days', dimmed), so every month is
  one height; **Events as** Dots (a new one's: a 28 cell, the number over its dots; the card's inset, the weekday
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
  the height is the inset, the weekday line and six times (4 + the number's 16 + the lines' 20
  each), every part whole grid cells ([S-14101]),
  the chip lines are what a week holds ([S-14502]). Debug render:
  `--style month --state titles` (the sample in this look, at its own min, natural and max).
- **[S-14101]** **The month fills its card and keeps its dots longer** (owner, 2026-11-20: "the calendar does not
  always stretch the contents to the top and bottom although it would be easily possible."; "the calendar shows the
  individual events too early, the items should stay small for a little longer."). The card takes every grid cell
  ([S-14500], `CalendarSizing.MonthSizing` over `MonthOver`: the card's inset and the weekday line, whole cells as `MonthLayouts.Plan` lays them); its seven columns share the width and its six weeks the
  height under `MonthOver` evenly on whole device pixels, so at every size the weeks fill the card from under the
  weekday line to its foot, alike, none left over;
  a state's row (Set up, an account gone) stands on the card's top edge and the weeks share what it leaves ([S-14903]).
  A dots month's cell shows its day's events as chips only once it holds two chip lines under its number in a column of
  80, where a chip's title reads and "+k more" has its words (`MonthChipRoom.Dots`; before, one chip line in a column of
  44 turned a 40-tall cell into chips of four letters); a month with titles, chosen, shows them from one chip line in its
  least column of 56 (`MonthChipRoom.Titles`).
  Tests `MonthLayoutTests.S14101_`.
- **[S-11545]** **Week, the day strip** (a new one's): each column's head (its weekday, its number) over
  the room its marks take at most ([S-2282]: three, then "+n"), then 8, then the chosen day's **Rows**
  (1..12, a new one 3; Style line) as the agenda's one-line rows ([S-2285]): one height for the
  config, a 40 column a day at its optimal (a new one's seven days 304), as many days as the card's width holds,
  sharing it, and the rows its height holds ([S-14410], [S-14502]: none stored by a resize).
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
  name and "Birthday". The view pages it with the card menu's back and forward (where there is a page that way), the wheel and the keys and hooks each event's row for its
  tooltip, its links across widgets and the click that opens it.
- **[S-3651]** The calendar's one stale rule ([S-7042]): each account whose last sync could not improve
  on the events held is a kit outcome (`CalendarFreshness`: its label, its problem in the kit's
  failure vocabulary, `SyncProblems.ToFetchStatus`, when its events are from); the problem
  glyph and the ring are the kit's status over the content's top end in every style (`KitStatus`, [S-12481], [S-14903]); the card menu's Refresh line says the age of the oldest copy.
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
  it go, Enter opens its first event. The calendar header ([S-14900]), the card menu, the wheel and the keys page the week as before.
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
- **[S-3606]** Debug renders: `--widget-png Calendar <out> --style month|week|day|year|agenda
  --state sample|selected|linked|days` for each style: the fixed sample (two calendars, a one-day and a four-day all-day event, a trip across a
  week's end, an overnight event, overlaps, a long title, birthdays, a year of meetings) through a
  fixture `ICalendarData`; `selected` selects the day after tomorrow as another widget would ([S-3620]);
  `linked` puts another widget's pointer on one event ([S-3623]); `days` sets the style's own range
  (the week: the next four days from today; the agenda: today and tomorrow). A card that needs
  setting up runs its view for the preview (`WidgetCard.RunForPreview`).
- **[S-3625]** *(Retired by [S-3626], 2026-11-21: the period is the view's; a calendar paged moves every calendar and
  calendar header of its view.)* Paging (back, forward, today) publishes the days now on screen (`VisibleRange`) and moves
  no other widget: two months on a view can show two months, and only a selection is followed.
- **[S-2040]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `CalendarModule.Opens`); an IMAP, CalDAV or CardDAV account opens nothing.
  The day cells, events and the week's day headers keep their clicks ([S-2004]).

Needs a live pass: a signed-in Microsoft, Google and CalDAV calendar in every style (paging, opening
an event in Outlook or the browser, the hover link with Next meeting and the week planner on the
view), and the showcase views on the live desktop.
- View links: publishes and follows the day under the pointer in every style without moving ([S-11923]), besides the selected day, the period and the event ([S-3620]..[S-3629]).
- View links: an agenda row is its event alone, never its organiser or its day: one item, one light ([S-11965]).
- No card header ([S-12481]): the period's title and back and forward are the calendar header's, a widget of their own
  ([S-14900]); the calendar draws no line over any style ([S-14903]).

## The calendar header (`calendarHeader`)

- **[S-14900]** **The calendar is split** (owner, 2026-11-21: "split up the calendar into the month with the arrows and
  the body of the calendar too, share the scrolling via the global state that we have."): the period's title with back
  and forward is its own kind, `calendarHeader` (`CalendarHeaderModule`, `CalendarHeaderConfig`, `CalendarHeaderView`),
  and the calendar is the body (its month, week, day, year or agenda), keeping the kind id `calendar`, so a placed
  calendar stays one, now without its line (no migration: it is the same widget). They share the period through the
  view's links, never through each other: the header pages every calendar of the view and follows them ([S-3626]..[S-3629]);
  a header alone pages nothing but itself, a calendar alone pages itself. The header reads no account and never waits for
  one: it draws its period alone. Its card menu holds Today while its period does not hold today (the line has room for
  the two arrows only, [S-6710]); its keys are the calendar's (Left / Page Up back, Right / Page Down forward, Home today,
  [S-2710]); the wheel pages it ([S-3629]). Across midnight a header on today's period stays on today's.
- **[S-14901]** **One line** (WR-1, WR-3; the owner's "a one-line face"): its card is the card's inset over one tool line
  (`ToolLine`: the period's title as its label, back and forward its two buttons, at the side inset), the line's band whole
  grid cells (`CalendarSizing.HeaderHeight`, `LineRules.BandOver` on the grid, 40): nothing over or under the line at its
  own height; a taller card (relaxed, [S-12411]: the user's) keeps the line on the card's inset at its top and leaves the
  room under it empty (WR-1, WR-5: the centring the owner granted the Header widget is not claimed); any width from 200
  (the longest title beside its arrows), a new one 300 (a new month's). Tests `CalendarSizingTests.S14901_`.
- **[S-14902]** **What it pages by** is its style ([S-6000]): **Month** (a new one's), **Week** (seven days from the
  regional first day of the week, [S-3770]), **Day**, **Year**, titled as the calendar titles them (`CalendarRules.Title`:
  "November 2026", "Nov 23 – 29", "Sat, Nov 21", "2026"). A style change keeps the day it is anchored on.
- **[S-14903]** **The calendar has no period's line** in any style: every style is drawn from the card's inset
  (`CalendarSizing.ContentTop`), its sizing counts no line, its state (the problem glyph, the ring) is the kit's status over
  the content's top end, and a calendar alone pages by its card menu's back and forward (an agenda's where there is a page
  that way) and Today, its keys and the wheel. Tests `CalendarSizingTests`.
- **[S-14904]** The ready-made group **Calendar** (Calendar & mail): a Header widget ([S-12485]), the calendar header across
  the top, the month under it and the agenda beside the month, as tall (`ReadyMadeGroups.Calendar`). Debug render:
  `--recipe-png calendar <out> --states calendar=sample,calendarHeader=next`: the header paged forward once, the month and
  the agenda on the same period.
