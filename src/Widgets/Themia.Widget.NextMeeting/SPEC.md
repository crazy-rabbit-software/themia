# Next meeting

The module `nextMeeting` ([S-6911]): the one event that matters now from the synced calendars, with
its Join button. Its tags are this file's alone; the root `SPEC.md` holds the SDK's rules it is
drawn by, with their root tags: which event ([S-4821], `NextMeetingRules.Pick`), the join link
([S-4822], `MeetingLinks`), when in words ([S-4823]) and the ladder ([S-4824], `NextMeetingLayout`).

Owning files: everything in this folder: `NextMeetingModule.cs` (descriptor, view factory, JSON
context), `NextMeetingConfig.cs`, `NextMeetingJoin.cs` (where Join stands), `NextMeetingView.cs` (the event, the meetings after it (`NextMeetingFollowing.cs`), Join through its launcher, the
calendars' data seam over `IWidgetHost.Office`, the hover link), `NextMeetingMenu.cs` (its properties-menu
lines: the SDK's calendar lines), `Preview/` (Debug renders: `--state sample|running|none|nolink`),
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
- **[S-2591]** Join beside the meeting, not under it (owner decision 2026-11-12; `NextMeetingJoin`,
  tests `NextMeetingJoinTests`): the meeting's lines (the title, the time, the place and the organiser,
  as the ladder [S-4824] shows them) are a column at the start of the content box and the end column
  (the countdown over Join, [S-5710]) stands at its end side, its end on the card's inset, the family's
  column gap (12) between them; the title is cut with its ellipsis before it would run under it. The
  ladder is then planned in the column's width and spends no height on it. Where the column beside it
  would be under 160 (`NextMeetingJoin.MinTextColumn`, measured on the content box less the end
  column's width and the gap, so a longer word moves it sooner), the end column goes under the lines as
  one row at the foot (8 over it): Join at its start, the countdown at its end; the ladder spends its
  height on that row; where even that row does not hold Join, 12 and the countdown, they stack there as
  they stand beside the lines, at the start (`EndPlace.Stacked`). Right to left mirrors: the end column at the left, the lines at the right. The
  meetings after this one ([S-2160]) stay under the block at the full width. At the tiers in English:
  XS stacked, S and Tall under; Wide, M, L and XL beside; 520 × 230 and 320 × 200 beside.
- **[S-2592]** A meeting with no join link has no Join anywhere: the end column is the countdown alone,
  on the title's line (the lines top-aligned, the countdown 4 down so the two lines' middles meet), or
  alone at the end of the row under the lines where the card is too narrow.
- **[S-5710]** **It stands without a header** (owner decision 2026-11-12: "it should be designed in a
  way that it looks good without header … the in x minutes should be moved somewhere else than the
  top"; it starts headerless, [S-2590]). The title is the first line. How long until it (the
  countdown: "in 18 min.", "Now · ends 11:45 AM", "Tomorrow, 9:00 AM", as [S-4823] words it; BodyStrong,
  primary, end-aligned, never the accent, [S-6706]) stands at the end side over Join, 4 apart, one unit:
  how long, then the action; that end column is vertically centred on the lines. Its width is the wider
  of Join and the countdown's widest wording in its form (`NextMeetingJoin.Wordings`: within the hour
  every minute's, so the column does not move as the minutes go; otherwise the one it says, which does
  not tick), measured in the language on the countdown's step (`NextMeetingJoin.EndWidth`, `Place`).
  Nothing coming up is the Status row at the content top with the calendar's glyph and "Nothing coming
  up", as are loading and a problem ([S-7039]); with the header turned on the layout is the same under
  it, no line repeats the header's name. The block is the taller of the lines and the end column, and
  the meetings after it stand under that (`NextMeetingFollowing.Rows`). "After this" is English-only
  for now (the `$englishOnly` rule for a new string). Tests `NextMeetingJoinTests.S5710_`,
  `NextMeetingFollowingTests.S5710_`, `DeskInfoWidgetsTests.S4824_`.
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
  then as many whole 28 rows as fit over the end column's row (where it stands under the lines) or the
  bottom inset, and only once the details reach their range line; none fitting, no label either. At the
  tiers, headerless as it starts: XS, S and Wide none (the details fill them), M three, Tall seven, L
  nine, XL as many as there are (fourteen fit).
- **[S-4825]** On a view ([S-3300]), pointing at the event publishes `HoveredEvent`: the same event
  lights in the calendar widgets there, the others dim ([S-3623]); one another widget points at
  dims this one unless it is the same event.
- **[S-4826]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance, then the calendar widgets' **Accounts** and **Calendars**
  (`OfficeMenuLines.CalendarAccounts`, [S-561]) without their colours (`colours: false`, [S-6070]:
  it draws no calendar's colour), so an account with one calendar has nothing to choose and no line,
  and Calendars is there only while a chosen account has two or more; no display lines (no birthdays). Debug: `--state sample` (a Teams meeting twelve minutes away), `running`, `none`, `nolink` (the
  sample without a join link, [S-2592]).
- **[S-2041]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `NextMeetingModule.Opens`); an IMAP, CalDAV or CardDAV account opens
  nothing. A click on the meeting shown still opens that meeting, and Join stays Join; the card
  opens the calendar only where no meeting is shown.

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting
(Join opening the app), a declined invitation, and the hover link with a calendar on the view.
