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
- **[S-4404]** The ladder (`NotificationsLayout.Plan`): Compact width or under 64 DIP of height, the bell
  and the count (the bell alone and dimmed with none; a click on it opens the privacy page where access
  is missing); otherwise rows, flat with each row's app icon up to Regular height, grouped from
  Expanded; the body takes no line at Compact height, 1 at Regular, 2 at Expanded, 3 at Huge; "No new notifications" when empty.
- **[S-4409]** Keys while the widget holds the lease: Up/Down, Page Up/Down, Home/End move the row
  cursor (`ListKeys`), Enter opens the app, Delete dismisses, the Menu key opens the row's menu (Open,
  Dismiss, Clear all from the app), Escape lets go. In edit mode nothing is opened or cleared.

Needs a manual pass: the widget on a live desktop ([S-4409]: real toasts arriving, a sender opened, one,
an app's and all dismissed, access asked for and denied, the keys through the lease) and in the packaged
build (the `userNotificationListener` capability), never in a render or a test, which read no list.
