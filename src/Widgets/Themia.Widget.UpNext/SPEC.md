# After this (upNext)

The module `upNext` ([S-6911]): the meetings after the next one, split from Next meeting (owner
decision 2026-11-12: widgets are atoms). Its tags are this file's alone; the root `SPEC.md` holds the
SDK's rules it is drawn by, with their root tags: which meeting is next ([S-4821],
`NextMeetingRules.Pick`) and the feed both meeting atoms read (`MeetingFeed`, [S-11407]).

Owning files: everything in this folder: `UpNextModule.cs` (descriptor, menu lines, JSON context),
`UpNextConfig.cs` (an `IKitConfig` over its calendar config), `UpNextSpec.cs` (its kit spec, a meeting as a kit item),
`UpNextView.cs` (a kit list, [S-7074], over `IWidgetHost.Office` through the meeting feed, [S-7077]),
`Preview/` (Debug renders: `--state sample|none`), `Localization/Locales/` (its own `UpNextStr`; "After
this" moved here from Next meeting with its key, English-only as it was). Tests: `Tests/UpNextTests.cs`
(run by `tests/Themia.Modules.Tests`).

- **[S-11408]** **After this** (`upNext`): the meetings after the one Next meeting shows ([S-2160]):
  the timed events not declined that start after now and not before it (an event under way is not
  "after"), today's first, then the next days' (`NextMeetingRules.After`, the tie broken as `Pick`
  breaks it). A one-line Row each (`RowBlock`, no lead, [S-6711]): the title the name, primary, cut
  with its ellipsis; the start the value, secondary, on the regional clock, with the day where it is
  not today ("Tomorrow, 9:30 AM", `NextMeetingRules.Start`). Its config is a `CalendarConfig` (accounts
  and calendars as every calendar widget chooses them, [S-562], [S-567]; never birthdays) and `Rows`
  (1..20, default 3); its height follows the rows (the kit's [S-2440]), 260..520 wide, a new one
  320 × 132 (as wide as the Next meeting block it stands under); Rows decide its natural height; the card shows the rows it holds at their optimal, stretched to fill it, and a resize stores none ([S-14500], [S-14502]);
  more meetings scroll inside them under the fade. It keeps its header by default, named "After this".
  Nothing after the next meeting is the Status row's "Nothing after this"; no account, loading and a
  problem are the office feed's screens in the kit's state row ([S-7077]); an account whose last sync failed while meetings
  show is the kit's problem glyph, and its card menu's Refresh carries the age ([S-7042]). It is a kit list ([S-7074]):
  a click on a row focuses it and opens that meeting in its calendar ([S-1268], [S-7075]), as do a double-click and
  Enter; the row's menu is the kit's (Open). Its
  menu: Rows, then the calendar accounts and calendars without colours ([S-4826]'s lines). Add menu:
  Calendar & mail. Debug: `--state sample` (two more meetings today, three tomorrow, after the design
  review), `none`. Tests `UpNextTests`.

Needs a live pass: a signed-in calendar account with meetings today and tomorrow, a row clicked open.
- View links: a row publishes its meeting and lights for the same meeting pointed at elsewhere on the view, never for
  its day or its organiser ([S-11935], [S-11965]).
