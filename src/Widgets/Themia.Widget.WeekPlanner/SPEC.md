# Week planner

The module `weekPlanner` ([S-6911]): the days ahead, each with its events, and under today the tasks
due. Its tags are this file's alone; the root `SPEC.md` holds the SDK's rules it is drawn by, with
their root tags: the rolling week and its columns ([S-5321], `WeekPlannerRules.Range`), a day's rows
([S-5322], `WeekPlannerRules.Build`) and whole rows only ([S-5323], `Columns`, `Stack`).

Owning files: everything in this folder: `WeekPlannerModule.cs` (descriptor, view factory, JSON
context), `WeekPlannerConfig.cs`, `WeekPlannerView.cs` (events through the calendars' seam over
`IWidgetHost.Office`, today's tasks through `IWidgetHost.Tasks`), `WeekPlannerMenu.cs` (its properties-menu
lines: the SDK's calendar lines, then Today's tasks), `Preview/` (Debug renders: `--state sample|free`),
`Localization/Locales/` (its own `WeekPlannerStr`, 30 languages). Tests: `Tests/WeekPlannerTests.cs` and `Tests/WeekPlannerMenuTests.cs`
(run by `tests/Themia.Modules.Tests`); the rules' are Core's (`WeekPlannerTests`). Issue #160.

- **[S-5320]** `WeekPlanner` (kind 133, `WeekPlannerConfig : CalendarConfig`, discriminator
  `weekPlanner`, glyph E762 MultiSelect, 480×280): the days ahead, each with its events, and under
  today the tasks due. One planner: a day's tasks are rows of that day after its events, not a task
  list beside a calendar. Calendar & mail subject, after next meeting ([S-3511]). Events through the
  calendar widgets' seam (`ICalendarData`, [S-2701]); tasks through the shared `TaskService`
  ([S-591]); the widget fetches nothing itself (a 5-minute poll of the synced store, the accounts'
  change events, a minute tick to redraw).
- **[S-5324]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance, every calendar's lines (Accounts, Calendars with their
  colours, Show contact birthdays, [S-561]), then Show today's tasks (on by default) and, while they
  show, Tasks from (the first account with tasks, or one
  account; a chosen account that is signed out stays listed by its id, never swapped, [S-590]).
  Lists kept on this PC are not offered: the planner is the synced schedule.
- **[S-5325]** Setup and sync: it asks to be set up only while neither a calendar account nor (when
  it shows tasks) a task account is signed in; with tasks alone it is a week of tasks.
  `SyncInterests` takes its calendars as a calendar interest and its task account's Today view as a
  task interest, so both sync with everything else.

Needs a live pass: a signed-in Microsoft and Google account (events, colours, opening an event,
ticking a task off and seeing it leave every Tasks widget), a day turning over at midnight, and the
showcase views on the live desktop.
