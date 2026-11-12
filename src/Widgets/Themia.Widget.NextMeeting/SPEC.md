# Next meeting

The module `nextMeeting` ([S-6911]): the one event that matters now from the synced calendars, with
its Join button. The meetings after it are After this's (`upNext`, [S-11408]); the ready-made group
"Next meeting" puts the two together ([S-11409]). Its tags are this file's alone; the root `SPEC.md`
holds the SDK's rules it is drawn by, with their root tags: which event ([S-4821],
`NextMeetingRules.Pick`), the join link ([S-4822], `MeetingLinks`) and when in words ([S-4823]).

Owning files: everything in this folder: `NextMeetingModule.cs` (descriptor, view factory, JSON
context), `NextMeetingConfig.cs`, `NextMeetingJoin.cs` (the end column and the block's sizes),
`NextMeetingView.cs` (the event, Join through its launcher, the calendars' data through the meeting
atoms' feed `MeetingFeed` over `IWidgetHost.Office`, the hover link), `NextMeetingMenu.cs` (its
properties-menu lines: its lines, the SDK's calendar lines), `Preview/` (Debug renders:
`--state sample|running|none|nolink`), `Localization/Locales/` (its own `NextMeetingStr`, 30
languages). Tests: `Tests/NextMeetingTests.cs`, `Tests/NextMeetingJoinTests.cs` and
`Tests/NextMeetingMenuTests.cs` (run by `tests/Themia.Modules.Tests`); its config beside Recent
files' is the module host's `DeskInfoHostTests`.

- **[S-4820]** `NextMeeting` (125): the one event that matters now from the synced calendars. Its
  config is a `CalendarConfig` (`NextMeetingConfig`): accounts and calendars chosen as every
  calendar widget chooses them ([S-562], [S-567]), so it needs setting up without a calendar
  account, its Set up row saying "Add an account to show your next meeting" ([S-1862]). It reads
  through the meeting atoms' feed (`MeetingFeed` over `ICalendarData`, the offline store and the
  shared sync, [S-2701]) for yesterday to fourteen days on, on the calendars' 5-minute poll and the
  accounts' change event; it never fetches on its own. It never asks for birthdays (all-day, never a
  meeting; `Repair` puts `showBirthdays` back to false).
- **[S-11407]** **The meeting block, an atom** (owner decision 2026-11-12: widgets are atoms; the id
  stays `nextMeeting`). It shows the meeting alone: the title (Title, one line, cut with its
  ellipsis), the time range, then the place and the organiser as its config picks them (`ShowPlace`,
  `ShowOrganizer`, both on by default; a meeting without one has no such line), Body lines with their
  16 glyphs, 4 apart; the countdown over Join at the end side ([S-5710]). The meetings after it are
  After this's ([S-11408]); both read one `MeetingFeed`, so they agree on which meeting is next. Its
  size is one height: the inset (12) over and under the taller of its lines and its end column with
  Join (the countdown 20, 4, Join 28), so the title and the time alone are 76 and with both lines 124;
  260..520 wide, a new one 320; it starts headerless ([S-2590]). Nothing appears or disappears with
  its size: the ladder ([S-4824], `NextMeetingLayout`), the following rows ([S-2160], [S-2161]) and
  the end column's move under the lines ([S-2591]'s narrow forms) are gone. Its menu: Place and
  Organiser switches, then the calendar lines ([S-4826]). Tests `NextMeetingTests.S11407_`,
  `NextMeetingMenuTests`.
- **[S-2591]** Join beside the meeting, not under it (owner decision 2026-11-12; `NextMeetingJoin`,
  tests `NextMeetingJoinTests`): the meeting's lines are a column at the start of the content box and
  the end column (the countdown over Join, [S-5710]) stands at its end side, its end on the card's
  inset, the family's column gap (12) between them, at every width of its range ([S-11407]); the title
  is cut with its ellipsis before it would run under it. Right to left mirrors.
- **[S-2592]** A meeting with no join link has no Join: the end column is the countdown alone, on the
  title's line (the lines top-aligned, the countdown 4 down so the two lines' middles meet).
- **[S-5710]** **It stands without a header** (owner decision 2026-11-12: "it should be designed in a
  way that it looks good without header … the in x minutes should be moved somewhere else than the
  top"; it starts headerless, [S-2590]). The title is the first line. How long until it (the
  countdown: "in 18 min.", "Now · ends 11:45 AM", "Tomorrow, 9:00 AM", as [S-4823] words it; BodyStrong,
  primary, end-aligned, never the accent, [S-6706]) stands at the end side over Join, 4 apart, one unit:
  how long, then the action; that end column is vertically centred on the lines. Its width is the wider
  of Join and the countdown's widest wording in its form (`NextMeetingJoin.Wordings`: within the hour
  every minute's, so the column does not move as the minutes go; otherwise the one it says, which does
  not tick), measured in the language on the countdown's step (`NextMeetingJoin.EndWidth`). Nothing
  coming up is the Status row at the content top with the calendar's glyph and "Nothing coming up", as
  are loading and a problem ([S-7039]); with the header turned on the layout is the same under it, no
  line repeats the header's name. Tests `NextMeetingJoinTests.S5710_`.
- **[S-2160]** *(Moved to After this, [S-11408]: the meetings after this one are its own atom.)*
- **[S-2161]** *(Retired by [S-11407]: no rows under the block.)*
- **[S-4825]** On a view ([S-3300]), pointing at the event publishes `HoveredEvent`: the same event
  lights in the calendar widgets there, the others at 0.6 ([S-3623]); when another widget points at
  this one's event the whole card takes the item layer's hover fill ([S-11903]); another event changes
  nothing here.
- **[S-4826]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): its Place and Organiser switches ([S-11407]), then the calendar widgets'
  **Accounts** and **Calendars** (`OfficeMenuLines.CalendarAccounts`, [S-561]) without their colours
  (`colours: false`, [S-6070]: it draws no calendar's colour), so an account with one calendar has
  nothing to choose and no line, and Calendars is there only while a chosen account has two or more;
  no display lines (no birthdays). Debug: `--state sample` (a Teams meeting eighteen minutes away),
  `running`, `none`, `nolink` (the sample without a join link, [S-2592]).
- **[S-2041]** A click on the card ([S-2000]) opens the calendar of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `NextMeetingModule.Opens`); an IMAP, CalDAV or CardDAV account opens
  nothing. A click on the meeting shown still opens that meeting, and Join stays Join; the card
  opens the calendar only where no meeting is shown.

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting
(Join opening the app), a declined invitation, and the hover link with a calendar on the view.
