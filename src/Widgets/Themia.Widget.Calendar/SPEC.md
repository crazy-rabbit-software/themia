# Calendar

The module `calendar` ([S-6911]): the synced calendars in one of five styles (month, week, day,
agenda, year). Its tags are this file's alone; the root `SPEC.md` holds what it shares with the host
and the other calendar widgets, with their root tags: where events come from ([S-560]), the shared
source and seam ([S-588], [S-2701], [S-3602]), the settings builder ([S-3601], the SDK's
`OfficeSettingsRows`), and every rule it is drawn by (`CalendarRules`, `CalendarFit`, `AgendaRules`,
`CalendarText`, `CalendarLinks`, `CalendarShare`, in the SDK, with their tests in Core).

Owning files: everything in this folder: `CalendarModule.cs` (descriptor, view factory, JSON
context), `CalendarWidgetConfig.cs` (the style, the week's and agenda's own settings),
`CalendarView*.cs` (one partial per style, the links, over `IWidgetHost.Office`), `AgendaSpec.cs`
(the agenda on the kit's list),
`CalendarSettings.cs` (the SDK's calendar rows and the style's own), `Preview/` (Debug renders:
`--state sample|selected|linked|days|stale|empty`), `Localization/Locales/` (its own `CalendarStr`, 30
languages). Tests: the rules' are Core's; the config's are the module host's (`CalendarHostTests`,
`OfficeConfigTests`); the agenda's items are `Tests/AgendaSpecTests.cs`.

- **[S-3650]** The agenda is the kit's paged list ([S-7035], [S-7036]): `AgendaSpec` gives its spec
  (the schedule look, two columns at most, the widest time labels "All day", "Now" and a clock) and
  an agenda row as a kit item: its day group (the selected day's in the accent, [S-3622]), an
  event's part on the day ("All day" between a long event's first and last day, "Now" in the strong
  step while it is under way, start over end, the span for a narrow row), where and who, the
  calendar's colour when colours show; a birthday its gift in the caution hue, its name and
  "Birthday". The view pages it with its header arrows and keys and hooks each event's row for its
  tooltip, its links across widgets and the click that opens it.
- **[S-3651]** The calendar's one stale rule ([S-7042]): each account whose last sync could not improve
  on the events held is a kit outcome (`CalendarFreshness`: its label, its problem in the kit's
  failure vocabulary, `SyncProblems.ToFetchStatus`, when its events are from); the header's problem
  glyph and ring are `KitHeader` before the calendar's own back, forward and today buttons, in every
  style; the card menu's Refresh line says the age of the oldest copy.
- **[S-3652]** The agenda's one open gesture ([S-7065]): a click focuses a row (the ring), a
  double-click or Enter opens its event where it lives ([S-1268]); a row's menu offers Open; a birthday
  or an event with nowhere to open offers nothing. Its cursor keys are its kit list's (Up, Down, End,
  Enter, the Menu key; with nothing focused a key lands on the page on screen, and the cursor turns
  the page with it); Left, Right, Page Up and Page Down page, Home goes to today, Escape lets the
  cursor and the selected day go. The other styles keep their click and Enter ([S-2710]).

- **[S-1265]** Week columns ≥64 px spell the weekday; ≥100 px a timed chip shows start–end and its
  location under the title.
- **[S-1269]** Keys (classic S-31..34) reach `CalendarView.HandleKey(WidgetKey)` through the lease
  ([S-806]); the map is [S-2710]. Up/Down move the cursor day a week in the month (outlined) and the
  week (its column head underlined); Escape lets it go. The cursor day is the selected day, shared
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

Needs a live pass: a signed-in Microsoft, Google and CalDAV calendar in every style (paging, opening
an event in Outlook or the browser, the hover link with Next meeting and the week planner on the
view), and the showcase views on the live desktop.
