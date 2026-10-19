# Next meeting

The module `nextMeeting` ([S-6911]): the one event that matters now from the synced calendars, with
its Join button. Its tags are this file's alone; the root `SPEC.md` holds the SDK's rules it is
drawn by, with their root tags: which event ([S-4821], `NextMeetingRules.Pick`), the join link
([S-4822], `MeetingLinks`), when in words ([S-4823]) and the ladder ([S-4824], `NextMeetingLayout`).

Owning files: everything in this folder: `NextMeetingModule.cs` (descriptor, view factory, JSON
context), `NextMeetingConfig.cs`, `NextMeetingView.cs` (the event, Join through its launcher, the
calendars' data seam over `IWidgetHost.Office`, the hover link), `NextMeetingMenu.cs` (its properties-menu
lines: the SDK's calendar lines), `Preview/` (Debug renders: `--state sample|running|none`),
`Localization/Locales/` (its own `NextMeetingStr`, 30 languages). Tests: `Tests/NextMeetingTests.cs` and `Tests/NextMeetingMenuTests.cs`
(run by `tests/Themia.Modules.Tests`); its config beside Recent files' is the module host's
`DeskInfoHostTests`.

- **[S-4820]** `NextMeeting` (125): the one event that matters now from the synced calendars. Its
  config is a `CalendarConfig` (`NextMeetingConfig`): accounts and calendars chosen as every
  calendar widget chooses them ([S-562], [S-567]), so it needs setting up without a calendar
  account ([S-1862]). It reads through the calendars' seam (`ICalendarData`, the offline store and
  the shared sync, [S-2701]) for yesterday to fourteen days on, on the calendars' 5-minute poll and
  the accounts' change event; it never fetches on its own. It never asks for birthdays (all-day,
  never a meeting; `Repair` puts `showBirthdays` back to false).
- **[S-4825]** On a view ([S-3300]), pointing at the event publishes `HoveredEvent`: the same event
  lights in the calendar widgets there, the others dim ([S-3623]); one another widget points at
  dims this one unless it is the same event.
- **[S-4826]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance, then the calendar widgets' **Accounts** and **Calendars**
  (`OfficeMenuLines.CalendarAccounts`, [S-561]) without their colours (`colours: false`, [S-6070]:
  it draws no calendar's colour), so an account with one calendar has nothing to choose and no line,
  and Calendars is there only while a chosen account has two or more; no display lines (no birthdays). Debug: `--state sample` (a Teams meeting twelve minutes away), `running`, `none`.

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting
(Join opening the app), a declined invitation, and the hover link with a calendar on the view.
