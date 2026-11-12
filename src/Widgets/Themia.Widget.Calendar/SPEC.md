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
(the agenda on the kit's list),
`CalendarMenu.cs` (its properties-menu lines: the SDK's calendar lines and the style's own), `Preview/` (Debug renders:
`--state sample|selected|linked|days|stale|empty`), `Localization/Locales/` (its own `CalendarStr`, 30
languages). Tests: the rules' are Core's; the config's are the module host's (`CalendarHostTests`,
`OfficeConfigTests`); the agenda's items (and the narrow week's rows) are `Tests/AgendaSpecTests.cs`; the week's fit is Core's `CalendarFitTests`; the menu's `Tests/CalendarMenuTests.cs`.

- **[S-3650]** The agenda is the kit's paged list ([S-7035], [S-7036]): `AgendaSpec` gives its spec
  (the schedule look on the family's rows, two columns at most) and an agenda row as a kit item: its
  day group (the selected day's heading primary, [S-3622]), an
  event's part on the day ("All day" between a long event's first and last day, "Now" in the strong
  step while it is under way, start over end, the span for a narrow row), where and who, the
  calendar's colour (a 2 px bar) when colours show; a birthday its gift (neutral: not a state), its
  name and "Birthday". The view pages it with its header arrows and keys and hooks each event's row for its
  tooltip, its links across widgets and the click that opens it.
- **[S-3651]** The calendar's one stale rule ([S-7042]): each account whose last sync could not improve
  on the events held is a kit outcome (`CalendarFreshness`: its label, its problem in the kit's
  failure vocabulary, `SyncProblems.ToFetchStatus`, when its events are from); the header's problem
  glyph and ring are `KitHeader`'s, its two header buttons back and forward ([S-569]), in every
  style; the card menu's Refresh line says the age of the oldest copy.
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
  (the first day of the week, named, or Today) and **Days shown** (1 to 14, dots); the agenda's
  **Date range** (Today, Today and tomorrow, 3, 7 or 30 days, and Custom…, which asks for 1 to 365
  days and holds the dot while the range is none of the others; the line shows the range) and
  **Agenda events** (1 to 100, asked for as text, [S-3726]). Then Appearance, the calendar widgets'
  **Accounts** and **Calendars** (`OfficeMenuLines.CalendarAccounts`, [S-561]) and, after a divider,
  **Show contact birthdays**, offered only while a chosen account has contacts to read them from
  (`OfficeMenuLines.OffersBirthdays`, [S-575], [S-6070]: a CalDAV calendar has no address book, and
  with no account there is nothing to read). A style laid out in weeks (month, week, year) ends its
  Style lines with a link to **Start of week**, which ⚙ › Language & region alone sets ([S-3749],
  [S-3590]); the day and the agenda have none.

- **[S-1265]** Week columns ≥64 px spell the weekday; ≥100 px a timed chip shows start–end and its
  location under the title.
- **[S-2281]** The week has two looks, chosen by the measured column width alone
  (`CalendarFit.Week`, `Marks`, `WeekTitlesFrom` 96): from 96 a column holds its events' titles
  (the band, chips or the hour grid, [S-2704]), nothing chosen and no rows under it; narrower, it holds
  marks ([S-2282]) and the chosen day's events are rows under the week ([S-2285]). No tier name decides.
- **[S-2282]** A narrow column's marks (`CalendarFit.Marks`, `MarksHeight`): under the day's number,
  one per item of the day (birthdays, then all-day events, then timed ones by start, [S-570]), each a
  round bar a track (`FamilyShape.Track`) high, the column's width less the family's 4 either side, 4
  apart, in its calendar's colour (a birthday's tertiary, the accent with colours off); at most three,
  then "+n" in Caption, tertiary. The head and its marks take the click that chooses the day and list
  the day on hover. Today keeps the accent disc on its number.
- **[S-2283]** The narrow week spends its height through one `Disclosure` (`CalendarFit.Strip`): the
  strip first (the day numbers, the weekday line where it and the marks fit, the marks: the most a day
  of the week shows, and a Caption line when a day has a "+n"), then 8, then the chosen day's rows,
  whole rows only ([S-6712]); a card too short for one row under the strip shows the strip alone.
- **[S-2284]** The chosen day (`CalendarFit.ChosenDay`) is the selected day ([S-3620]) while it is in
  the week, else today while the week holds it, else the week's first day; its column head stands on
  the selection fill (the item layer's selected chip). A click on a day's head or marks selects it (a
  click on the selection lets it go, back to today), Up and Down move it a week ([S-1269]), Escape lets
  it go, Enter opens its first event. The header's name and its two buttons page the week as before.
- **[S-2285]** The chosen day's rows are the agenda's ([S-3650]: one row path, the view's one kit
  list, `AgendaSpec.DayRow`): the calendar's colour bar as the lead (a birthday's gift), the title,
  the start ("All day") as the value, the span, place and who on a second line where `RowRules.Lines`
  gives two; in no group, so no day heading ([S-2280]); the first page's whole rows, the rest on the
  day head's hover. A day with nothing says "No events today" (another day "No events") in the Status
  block's row ([S-6714]).
- **[S-1269]** Keys (classic S-31..34) reach `CalendarView.HandleKey(WidgetKey)` through the lease
  ([S-806]); the map is [S-2710]. Up/Down move the cursor day a week in the month (outlined) and the
  week (its column head underlined, or on the selection fill in a narrow week, [S-2284]); Escape lets it go. The cursor day is the selected day, shared
  with the view's other calendars and kept across pages ([S-3624]).
- **[S-2700]** A calendar widget draws only its own layout (month, week, day, year or agenda: its
  style, [S-3640]), at every size. No size swaps it for another layout, adds an agenda beside a
  month, or shows a single "next event" instead: each layout sheds detail within itself
  (`CalendarFit`). Unmeasured, it draws the layout at its smallest.
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
- **[S-3625]** Paging (back, forward, today) publishes the days now on screen (`VisibleRange`) and moves
  no other widget: two months on a view can show two months, and only a selection is followed.
- **[S-2040]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `CalendarModule.Opens`); an IMAP, CalDAV or CardDAV account opens nothing.
  The day cells, events and the week's day headers keep their clicks ([S-2004]).

Needs a live pass: a signed-in Microsoft, Google and CalDAV calendar in every style (paging, opening
an event in Outlook or the browser, the hover link with Next meeting and the week planner on the
view), and the showcase views on the live desktop.
