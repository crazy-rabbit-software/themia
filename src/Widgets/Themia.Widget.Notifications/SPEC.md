# Notifications

The module `notifications` ([S-6911]): Windows' notification list. Its tags are this file's alone; the
root `SPEC.md` holds the host's sections and what the list shares, with their root tags: the listener
and the words a toast carries ([S-4401]), grouping and Clear all ([S-4402]), access ([S-4403]), the
sender's name ([S-4405]), opening a sender ([S-4406]), dismissals ([S-4407]) and the one feed
([S-4408], the host's `NotificationFeed` behind `IWidgetHost.Notifications`); the rules are the SDK's
`NotificationRules`.

Owning files: everything in this folder: `NotificationsModule.cs` (descriptor, glyph, view factory, its page, JSON
context), `NotificationsConfig.cs` (its rows, order and the item base's arrangement, gap and parts), `NotificationsSpec.cs`
(its kit spec, a notification as the item base's item, its order, its actions), `NotificationsView.cs` (a kit view on the
item base, `ItemKit`, [S-16958]: the binding over its host's feed, the senders' icons through the installed `AppIconSource`
as the items' leads), `Preview/` (Debug renders: `--state sample|many|empty|denied|unspecified|unsupported`),
`Localization/Locales/` (its own `NotificationsStr`, 30 languages). Tests: `Tests/NotificationsTests.cs` (run by
`tests/Themia.Modules.Tests`); that it asks no setting up is the module host's `WindowsPanelsHostTests`.

- **[S-4400]** Kind `Notifications` (121), add menu Calendar & mail ([S-3511]), glyph the bell; never needs setting up.
- **[S-16970]** **On the item base** (owner, 2026-11-23: "notifications and upcoming meetings should get the same base widget
  as the bluetooth and drive devices"; the brief: "lead = app icon, title, body as subtitle, end = time"): a notification is
  an item (`NotificationsSpec.Item`): its sender's icon the lead ([S-2271]), its title (else its app's name), its body on one
  line under it (its own breaks read as spaces), when it came at its end (the kit's compact age), its app's name the note
  (left out by default); every arrangement of the base (List, Row list, Column list, Grid, Table), its gap and Rows (a new
  card four), its parts in order. Newest first, or **Grouped by app** ([S-4402]). Its page: Grouped by app, then the item
  base's lines ([S-16950]..[S-16952]). It takes part in no view link (no app headings any more; [S-11930]). Tests
  `NotificationsTests`.
- **[S-11404]** *(Retired by [S-16970]: rows of one or two lines and the app headings with their "See more" folds are the
  item base's arrangements and the Grouped by app order now; a stored `lines` or `appHeadings` is dropped at load.)*
- **[S-4404]** *(Retired by [S-11404].)*
- **[S-2271]** An item leads with its sender's own icon (`AppIconSource`), at the lead's size in each arrangement; the
  widget's bell stands in only while an icon loads and for a sender Windows gives none. A render reads no notification
  list and so no sender's icon: every preview item shows that fallback, the bell.
- **[S-4409]** Keys while the widget holds the lease, the item base's ([S-16955]): the arrows, Page Up/Down and Home/End
  walk the items, Enter opens the app, Delete dismisses, the Menu key opens the item's menu (Open, Clear all from the app,
  Dismiss), Escape lets go. In edit mode nothing is opened or cleared. Access Windows has not given (Denied: a problem with
  Open settings; Unspecified: Allow) and an unsupported Windows are the kit's state row ([S-7076]); before Windows answers,
  its loading row.

Needs a manual pass: the widget on a live desktop ([S-4409]: real toasts arriving, a sender opened, one,
an app's and all dismissed, access asked for and denied, the keys through the lease) and in the packaged
build (the `userNotificationListener` capability), never in a render or a test, which read no list.
