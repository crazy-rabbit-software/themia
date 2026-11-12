# Notifications

The module `notifications` ([S-6911]): Windows' notification list. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections and what the list shares, with their root tags: the listener
and the words a toast carries ([S-4401]), grouping and Clear all ([S-4402]), access ([S-4403]), the
sender's name ([S-4405]), opening a sender ([S-4406]), dismissals ([S-4407]) and the one feed
([S-4408], the host's `NotificationFeed` behind `IWidgetHost.Notifications`); the rules are the SDK's
`NotificationRules`.

Owning files: everything in this folder: `NotificationsModule.cs` (descriptor, glyph, view factory,
JSON context), `NotificationsConfig.cs` (its rows, lines and app headings, [S-11404]),
`NotificationsView.cs` (the list through its host, the senders' icons through the installed
`AppIconSource`, the keys), `NotificationsLayout.cs` (the plan from the config), `Preview/` (Debug renders:
`--state list|many|open|empty|denied|unspecified|unsupported`), `Localization/Locales/` (its own
`NotificationsStr`, 30 languages). Tests: `Tests/NotificationsTests.cs` (run by
`tests/Themia.Modules.Tests`); that it asks no setting up is the module host's `WindowsPanelsHostTests`.

- **[S-4400]** Kind `Notifications` (121), add menu Calendar & mail ([S-3511]), glyph the bell; its
  settings are [S-11404]'s; never needs setting up.
- **[S-11404]** **It states its rows** ([S-2440]): `Rows` (1..20, default 4) notifications, `Lines`
  per notification (1: the title with when it came; 2: its body under it, default) and **Under app
  headings** (`AppHeadings`, off by default: one list newest first, each row its app's icon; on: the
  apps' headings of [S-4402], a heading a row of 28 inside the rows). Every row has its icon and its
  time at every width. 220..520 wide, a new one 300 × 208; the height follows the rows; dragging the
  bottom edge sets Rows; more scroll inside them (the wheel as before). Nothing appears or disappears
  with the size: [S-4404]'s ladder is gone. Tests `NotificationsTests`.
- **[S-4404]** *(Retired by [S-11404]: lines and headings are the config's.)* The ladder (`NotificationsLayout.Plan`) on the family's rows ([S-6711], [S-6712]): the list
  at every size (the header's neutral count says how many, so the small tile has no bell face of its
  own; G2, 2026-10-29); a notification's title the name, when it came the value, its body on one line
  under it (its own breaks read as spaces) from a regular tile; each row's app icon and the time from a
  column of 180; grouped under app headings from an expanded height ([S-4402]); as many whole lines
  as fit, never ending on a heading, the rest a scroll away under the fade. "No new notifications",
  a missing access ([S-4403]) and an unsupported Windows are the state row's sentence at the top, with
  the one button that fixes it (Open settings for Denied, a problem; Allow for Unspecified).
- **[S-2271]** A row leads with its sender's own icon (`AppIconSource`), so the leads tell the rows
  apart and stay ([S-6711]); the widget's bell stands in only while an icon loads and for a sender
  Windows gives none. A render reads no notification list and so no sender's icon: every preview row
  shows that fallback, the bell, which is what a live row shows for a sender without an icon.
- **[S-4409]** Keys while the widget holds the lease: Up/Down, Page Up/Down, Home/End move the row
  cursor (`ListKeys`), Enter opens the app, Delete dismisses, the Menu key opens the row's menu (Open,
  Dismiss, Clear all from the app), Escape lets go. In edit mode nothing is opened or cleared.
- View links: a row is its sender's app where its id names one (Windows' own toasts none); the pointer or the
  keyboard cursor on it publishes the app pointed at, and a row lights for the app pointed at or picked on the
  view, never picking it ([S-11930], [S-11933]).

Needs a manual pass: the widget on a live desktop ([S-4409]: real toasts arriving, a sender opened, one,
an app's and all dismissed, access asked for and denied, the keys through the lease) and in the packaged
build (the `userNotificationListener` capability), never in a render or a test, which read no list.
