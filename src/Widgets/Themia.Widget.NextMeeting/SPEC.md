# Next meeting

The module `nextMeeting` ([S-6911]): the one event that matters now from the synced calendars, with
its Join button. Its tags are this file's alone; the root `SPEC.md` holds the SDK's rules it is
drawn by, with their root tags: which event ([S-4821], `NextMeetingRules.Pick`), the join link
([S-4822], `MeetingLinks`), when in words ([S-4823]) and the ladder ([S-4824], `NextMeetingLayout`).

Owning files: everything in this folder: `NextMeetingModule.cs` (descriptor, view factory, JSON
context), `NextMeetingConfig.cs`, `NextMeetingView.cs` (the event, the meetings after it (`NextMeetingFollowing.cs`), Join through its launcher, the
calendars' data seam over `IWidgetHost.Office`, the hover link), `NextMeetingMenu.cs` (its properties-menu
lines: the SDK's calendar lines), `Preview/` (Debug renders: `--state sample|running|none`),
`Localization/Locales/` (its own `NextMeetingStr`, 30 languages). Tests: `Tests/NextMeetingTests.cs` and `Tests/NextMeetingMenuTests.cs`
(run by `tests/Themia.Modules.Tests`); its config beside Recent files' is the module host's
`DeskInfoHostTests`.

- **[S-4820]** `NextMeeting` (125): the one event that matters now from the synced calendars. Its
  config is a `CalendarConfig` (`NextMeetingConfig`): accounts and calendars chosen as every
  calendar widget chooses them ([S-562], [S-567]), so it needs setting up without a calendar
  account, its Set up row saying "Add an account to show your next meeting" ([S-1862]). It reads through the calendars' seam (`ICalendarData`, the offline store and
  the shared sync, [S-2701]) for yesterday to fourteen days on, on the calendars' 5-minute poll and
  the accounts' change event; it never fetches on its own. It never asks for birthdays (all-day,
  never a meeting; `Repair` puts `showBirthdays` back to false).
- **[S-2160]** What comes after it (owner decision 2026-11-12; `NextMeetingFollowing`, tests
  `NextMeetingFollowingTests`): room left under the meeting's own lines goes to more of the widget's
  own items, the meetings after this one, from what the calendars' seam already reads (yesterday to
  fourteen days on; no new host contract). They are the timed events not declined that start after
  now and not before the one shown (an event under way is not "after"), in the order `Pick` breaks a
  tie, today's first. Each is a one-line Row (`RowBlock`, no lead, [S-6711]: the title the name,
  primary, cut with an ellipsis; the start the value, secondary, on the regional clock), under a
  Caption label in the secondary role, "After this", at the text edge. On an expanded width (L, XL)
  they go on to the next days, their value then the day and the time ("Tomorrow, 9:30 AM", as the
  when line says it); narrower they are today's alone, so a value is a time and never crushes the
  title. A click on a row opens that meeting in its calendar ([S-1268]); the hover link stays the
  shown meeting's. Debug: `--state sample` and `running` have two more meetings today and three
  tomorrow after the first.
- **[S-2161]** Where they stand (`NextMeetingFollowing.Rows`): 8 under the details, the label's 16,
  then as many whole 32 rows as fit over Join (its 32 and 8) or the bottom inset, and only once the
  details reach their range line; none fitting, no label either. At the tiers: XS, S, Wide and M none
  (the details fill them), Tall four, L five, XL nine (as many as there are).
- **[S-4825]** On a view ([S-3300]), pointing at the event publishes `HoveredEvent`: the same event
  lights in the calendar widgets there, the others dim ([S-3623]); one another widget points at
  dims this one unless it is the same event.
- **[S-4826]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance, then the calendar widgets' **Accounts** and **Calendars**
  (`OfficeMenuLines.CalendarAccounts`, [S-561]) without their colours (`colours: false`, [S-6070]:
  it draws no calendar's colour), so an account with one calendar has nothing to choose and no line,
  and Calendars is there only while a chosen account has two or more; no display lines (no birthdays). Debug: `--state sample` (a Teams meeting twelve minutes away), `running`, `none`.
- **[S-2041]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `NextMeetingModule.Opens`); an IMAP, CalDAV or CardDAV account opens
  nothing. A click on the meeting shown still opens that meeting, and Join stays Join; the card
  opens the calendar only where no meeting is shown.

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting
(Join opening the app), a declined invitation, and the hover link with a calendar on the view.
