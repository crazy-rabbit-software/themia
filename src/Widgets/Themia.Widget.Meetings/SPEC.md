# Meeting and Meetings (MEETINGS)

The module family `meeting` and `meetings` ([S-6911], [S-6901]): the meetings of the synced calendars on the item base
(root SPEC ITEMS, [S-16940]), replacing Next meeting and After this (owner, 2026-11-23: "notifications and upcoming meetings
should get the same base widget as the bluetooth and drive devices ... the meetings should get a join button on the right
side for example instead of the percent thingy. there should also be a single meeting that you can place (you can choose
it to show your next meeting for example) or you can have a list of meetings that you can also filter by account, order,
have them show past meetings, have them show future meetings, etc. same component as the bluetooth one as well. also with
spacing capability."; on the old widgets: "Replace them"). Its tags are this file's alone; the root `SPEC.md` holds the
SDK's rules it is drawn by, with their root tags: which meeting is next ([S-4821], `NextMeetingRules.Pick`), the join link
([S-4822], `MeetingLinks`), when in words ([S-4823]) and the feed both read (`MeetingFeed`).

Owning files: everything in this folder: `MeetingsModule.cs` (the two descriptors, their menus, the meetings a Meeting's
page offers, the JSON context), `MeetingsConfig.cs` (`MeetingConfig`, `MeetingsConfig` over their shared
`MeetingsBaseConfig`, a `CalendarConfig`), `MeetingItems.cs` (pure: which meetings, which one, a meeting as an item),
`MeetingView.cs` (one meeting on `ItemFace`), `MeetingsView.cs` and `.Links.cs` (the list, a kit view on `ItemKit`),
`Samples/`, `Preview/` (Debug renders: Meeting `--state sample|running|none|nolink`, Meetings `--state
sample|running|none`), `Localization/Locales/` (its own `MeetingsStr`; Next meeting's and After this's strings moved here).
Tests: `Tests/MeetingsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-16960]** **Meeting** (`meeting`): one meeting on the item base's face ([S-16957]): the next one (`Pick` Next: the one
  that matters now, [S-4821]) or one the user picked (`Pick` Chosen, its fingerprint in `Chosen`; once it is over, the next
  one again). Its page: **Meeting shown** (the next one, or one of the meetings coming up the meeting widgets read last,
  `MeetingsSeen`; a picked one no longer read is offered as "A meeting that is over"), **Each item shows** (its parts in
  order with its place and organiser, [S-16950]), then the calendar accounts and calendars without colours ([S-4826]). Its
  card one item tall, 220..520 wide; a click on the meeting opens it in its calendar ([S-1268]), Join joins it, with no
  meeting the card opens the calendar ([S-2041]). The card lights as its meeting, also for the same meeting pointed at on
  the view ([S-4825]). No meeting keeps the face: "--" and why (nothing coming up, loading, a problem; WR-10). Tests
  `MeetingsTests.S16960_`.
- **[S-16961]** **Meetings** (`meetings`): the meetings of the chosen accounts and calendars as an item list on the kit
  ([S-16958]): the office feed's screens (no account, loading, a problem) and the stale rule are the kit's, the items the
  item base's in the config's arrangement (List, Row list, Column list, Grid, Table), its gap and rows; a click opens a
  meeting in its calendar, its menu Open and Join, Join on its end joins it. Nothing in its time says which: "Nothing coming
  up", "Nothing after this" with the next one left out, "No meetings in this time" for the past.
- **[S-16962]** **What the list shows** (`MeetingItems.Select`): the timed meetings not declined: **Coming up** (not over: one
  under way too) to the end of the last day of its reach, **Past** (over) back to the start of the first day of it, or
  **Both**; its reach **Time span** (Today, 2 days, 7 days, 14 days, a new one 14); **Order** soonest or latest first (a tie as
  `Pick` breaks it); **Leave out the next meeting** (After this's rule, [S-11408]: only those still to start after it). The
  feed asks for the days its reach covers (`MeetingFeed`'s window: from yesterday ahead, back as far for the past). Tests
  `MeetingsTests.S16962_`.
- **[S-16963]** **A meeting as an item** (`MeetingItems.Item`): its title the subject ("No title"), its lead the meeting's glyph
  (an online meeting's, else the calendar's; left out by default: "they dont neccesarily need to have an icon"), its
  subtitle when (the time range, "Tomorrow · 9:30 – 10:00" on another day, "in 18 min · 11:00 – 11:45" soon or under way
  beside Join), its end **Join** where it has a join link and is not over, else the start or how long until it (none once
  over), its bar how far one under way has run (left out by default), its note the place and the organiser as **Place** and
  **Organiser** show them; one under way strong, one over secondary. Its parts' words: Meeting, When, Join or starts,
  Progress, Where. Tests `MeetingsTests.S16963_`.
- **[S-16964]** **The old meeting widgets replaced in place** (owner: "Replace them"): a placed Next meeting becomes a Meeting
  showing the next meeting (`MergeFrom` `{ "pick": "Next" }`, its place and organiser switches, accounts and calendars kept);
  a placed After this becomes a Meetings list coming up with the next one left out, in compact rows (`{ "afterNext": true,
  "style": "columnList" }`, its rows, accounts and calendars kept). The ready-made groups My day and Next meeting stand a
  Meeting over such a list. Tests `MeetingsTests.S16964_`.

What Next meeting and After this said that still holds, and what went:

- **[S-4825]** On a view ([S-3300]), pointing at a meeting publishes `HoveredEvent`: the same event lights in the calendar
  widgets there; when another widget points at the Meeting's meeting its whole card takes the item layer's hover fill
  ([S-11903], [S-11962], [S-11963]), a list's item that meeting's item alone.
- **[S-4826]** The calendar lines: the calendar widgets' **Accounts** and **Calendars** (`OfficeMenuLines.CalendarAccounts`,
  [S-561]) without their colours (`colours: false`, [S-6070]); no birthdays line (never a meeting: `Repair` puts
  `showBirthdays` back to false).
- **[S-2041]** A click on the Meeting's card where no meeting is shown opens the calendar of the account it reads on the web
  ([S-2005]): Outlook's for a Microsoft account, Google's for a Google one; an IMAP, CalDAV or CardDAV account opens nothing.
- **[S-11408]** After this's rule, kept as the list's **Leave out the next meeting** ([S-16962]): the meetings after the one a
  Meeting shows, those not under way that start after now and not before it (`NextMeetingRules.After`).
- **[S-14262]** No meeting keeps the face (WR-10): "--" and why under it, never a status line alone.
- **[S-4820]**, **[S-11407]**, **[S-2591]**, **[S-2592]**, **[S-14793]**, **[S-14792]**, **[S-5710]**, **[S-2160]**, **[S-2161]**:
  *(Retired by [S-16960]..[S-16964]: the meeting block, its lines giving way, its countdown over Join and its sizes are the
  item base's item now; Join stands at the item's end.)*

Needs a live pass: a signed-in Microsoft and Google account with a Teams, Zoom and Meet meeting (Join opening the app), a
declined invitation, past meetings, and the hover link with a calendar on the view.
