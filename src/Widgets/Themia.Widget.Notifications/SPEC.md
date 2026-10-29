# Notifications

The module `notifications` ([S-6911]): Windows' notification list. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections and what the list shares, with their root tags: the listener
and the words a toast carries ([S-4401]), grouping and Clear all ([S-4402]), access ([S-4403]), the
sender's name ([S-4405]), opening a sender ([S-4406]), dismissals ([S-4407]) and the one feed
([S-4408], the host's `NotificationFeed` behind `IWidgetHost.Notifications`); the rules are the SDK's
`NotificationRules`.

Owning files: everything in this folder: `NotificationsModule.cs` (descriptor, glyph, view factory,
JSON context), `NotificationsConfig.cs` (empty: nothing to set, no settings page, [S-1750]),
`NotificationsView.cs` (the list through its host, the senders' icons through the installed
`AppIconSource`, the keys), `NotificationsLayout.cs` (the ladder), `Preview/` (Debug renders:
`--state list|many|open|empty|denied|unspecified|unsupported`), `Localization/Locales/` (its own
`NotificationsStr`, 30 languages). Tests: `Tests/NotificationsTests.cs` (run by
`tests/Themia.Modules.Tests`); that it asks no setting up is the module host's `WindowsPanelsHostTests`.

- **[S-4400]** Kind `Notifications` (121), add menu Calendar & mail ([S-3511]), 320×320, glyph the bell. No
  settings (`NotificationsConfig` is empty, so no page, [S-1750]); never needs setting up.
- **[S-4404]** The ladder (`NotificationsLayout.Plan`) on the family's rows ([S-6711], [S-6712]): the list
  at every size (the header's neutral count says how many, so the small tile has no bell face of its
  own; G2, 2026-10-29); a notification's title the name, when it came the value, its body on one line
  under it (its own breaks read as spaces) from a regular tile; each row's app icon and the time from a
  column of 180; grouped under app headings from an expanded height ([S-4402]); as many whole lines
  as fit, never ending on a heading, the rest a scroll away under the fade. "No new notifications",
  a missing access ([S-4403]) and an unsupported Windows are the state row's sentence at the top, with
  the one button that fixes it (Open settings for Denied, a problem; Allow for Unspecified).
- **[S-4409]** Keys while the widget holds the lease: Up/Down, Page Up/Down, Home/End move the row
  cursor (`ListKeys`), Enter opens the app, Delete dismisses, the Menu key opens the row's menu (Open,
  Dismiss, Clear all from the app), Escape lets go. In edit mode nothing is opened or cleared.

Needs a manual pass: the widget on a live desktop ([S-4409]: real toasts arriving, a sender opened, one,
an app's and all dismissed, access asked for and denied, the keys through the lease) and in the packaged
build (the `userNotificationListener` capability), never in a render or a test, which read no list.
