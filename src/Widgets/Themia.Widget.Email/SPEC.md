# Email

The module `email` ([S-6911]): recent mail of the signed-in accounts, as a list, compact or a table.
Its tags are this file's alone; the root `SPEC.md` holds what it shares with the host, with their
root tags: the mail service, its folders, IMAP and MIME ([S-545]..[S-556]), opening, the merged list
and the screens ([S-547]..[S-549], the SDK's `MailScreens`), writing ([S-558]) and the Refresh line
([S-559]), the shared list keys and fade ([S-1281], [S-1288]).

Owning files: everything in this folder: `EmailModule.cs` (descriptor, view factory, JSON context),
`EmailConfig.cs` (the style, the folder, the fields and columns, `EmailFolders`), `MailSpec.cs` (a
message as a kit item, its actions, the style's kit spec), `EmailView.cs` (over `IWidgetHost.Mail`,
its rows the kit's `KitList`), `EmailMenu.cs` (its properties-menu lines), `Preview/` (Debug renders:
`--state messages|accounts|empty|hovered|focused|stale|offline`),
`Localization/Locales/` (its own `EmailStr`, 30 languages). Tests: `Tests/MailSpecTests.cs` and `Tests/EmailMenuTests.cs` (run by
`tests/Themia.Modules.Tests`; the ladder's are the kit's, `KitLinedTests`); the config's and the screens' are the module host's (`MailFeedTests`).

- **[S-540]** `EmailConfig` keeps the classic wire names (`folder: inbox|sentitems|drafts|deleteditems|junkemail`,
  `viewMode: list|compact|row`, `sortOrder: desc|asc`, `listFields`, `rowColumns`, `accountIds`).
  Every closed set is read leniently: an unknown or mistyped value becomes the default and is
  logged, never a throw that would refuse the state file. `WidgetConfig.Repair()` runs at load from
  `AppStateStore.Validate`; for mail it clamps the preview lines (1–5) and keeps at least one row
  column. No hearth-era field is read ([S-1050]).
- **[S-541]** `accountIds` empty means every mail account, including ones added later (the classic
  widget showed "select an account" instead); a non-empty list reads those accounts that are still
  signed in. Its menu lists the signed-in mail accounts as switches under "Every mail account" and
  opens ⚙ › Accounts ([S-7512], [S-3558]); no account is ever signed in from the widget. Folders: Inbox,
  Sent, Drafts, Deleted, Junk [S-12].
- **[S-542]** List (sender + time, subject, preview), Compact (sender + time, subject) and Row (one
  line of user-ordered columns under a header row) views [S-14, S-37]; newest or oldest first over
  the same newest N [S-17]. The card title is the folder; the header carries the unread badge
  [S-02], the kit's problem glyph ([S-7064]: one line per account that failed or needs signing in, a
  tap → Accounts where only the user can mend it) and a busy ring only for a refresh the user asked for. The card menu offers Refresh, Write email
  ([S-558]) and the five folders [S-20]; the style ([S-6403]) and the order are settings only ([S-1746]).
- **[S-543]** List fields Subject / Account, default both on (the preview is the List style's own
  line, [S-6403]); the account tag shows whenever chosen [S-43]. Table columns From, Subject, Date,
  Preview, Account, reorderable, at least one on [S-16].
- **[S-6403]** Mail's styles ([S-6000]), `EmailConfig.Style` (`EmailStyle`, `"style": "list"`):
  **List with preview** (sender + time, subject, `PreviewLines` lines of preview), **Compact list**
  (sender + time, subject) and **Table** (one line of the user's columns under a header row), in
  that order: what were the view setting and the list fields' Preview switch (List without the
  preview was Compact, so the switch is gone). The rest are lines under Style, shown only for the style that
  draws with them (`EmailConfig.ShownSettings`, [S-6070], [S-7512]): Subject / Account for List and Compact, Preview
  lines for List, the columns for Table. A hand-edited `"preview"` list field is dropped.
- **[S-544]** Message right-click: Open, Reply, Mark read/unread, Archive, Move to Junk, Copy subject,
  Delete [S-21], built by the kit's one menu builder ([S-7011], [S-7013]: Reply an OpenTo, the marks,
  Archive and Junk changes): Copy subject and Delete are its icon row, Enter, Ctrl+C and Delete are
  shown beside their lines. Gmail is read through `gmail.readonly`, so its messages offer only Open, Reply and
  Copy [S-22]. Delete moves to Deleted (Graph move, IMAP UID MOVE); only a message already in Deleted
  is removed for good (Graph DELETE; IMAP `\Deleted` + `UID EXPUNGE`, UIDPLUS-scoped). A change shows
  at once and waits in the account's queue until the provider has it ([S-2610], [S-2624]); a refusal
  is the header warning's tooltip and the next probe fetches the truth.
- **[S-1280]** Mail's ladder (the kit's lined looks, [S-7032]..[S-7034]): the first row always; the row
  view's column titles from 140px of height; then as many whole rows as fit. Stacked rows shed account
  tag, preview, extra preview lines and time as before ([S-556]); a wide strip and a big tile go one
  line per message ([S-1283]); the envelope joins from 300px ([S-1282]); the largest tile adds the
  quick actions ([S-1284]). No provider name and no folder summary ([S-1285], retired).
- **[S-1282]** Envelope: a closed envelope (accent) in front of unread mail, an open one (tertiary)
  in front of read mail, from 300px wide, in every view; the unread accent bar stays.
- **[S-1283]** One-line rows (list and compact views) from 640px wide, or from 420px on a strip under
  220px tall: sender column (24% of the line, 96-200px), subject with the preview after an en dash
  (preview from 440px), account from 560px, time. The row view is unchanged. A one-line row shows
  one preview line whatever `PreviewLines` says (that setting shapes the stacked rows).
- **[S-1284]** The account tag and the tooltip name the account alone at every size (the provider
  name, "Work (Microsoft)", is retired, [S-666]). On one-line rows from 720px the pointer, or the
  keyboard focus ([S-1288]), swaps a row's time for quick actions: mark read or unread, archive (not
  in Junk), delete — only where the account allows changes ([S-544]). The swap moves nothing: the
  time's slot is always as wide as the actions (`KitLadder.LineBoxes`, [S-7033]), the hidden time keeps
  its box, and the buttons overhang the line instead of growing it, so every other part of the row
  keeps its position and size and the row its height.
- **[S-6420]** Mail on the kit (`MailSpec`): List and Compact are the kit's stacked look (going one
  line on a strip), Table its table ([S-7032]..[S-7034]); a message is a `KitItem` (the sender its
  title, the subject its subtitle, the account's name its badge, unread its emphasis, accent tone and
  closed envelope) and its menu lines `KitAction`s; its keys are the kit's ([S-7020]: Left and Right
  step like Up and Down, Ctrl+C copies the subject).
- **[S-6421]** Mail is a kit view ([S-7061]): `MailKitSource` reads the host's one mail source for
  the config's accounts, folder and count (the feed is pushed; Refresh is the feed's), its messages as
  items, each account read as an outcome (signed out: sign in again; not answered: loading; failed:
  its status, stamped with when its list is from), the accounts' states, the unread count for the
  badge, and a change waiting, refused or a command that failed as the header's notes until the next
  update. The binding draws the kit's screens, state panel and header: one stale rule ([S-7042]: the
  problem glyph, one line per account, the age on the Refresh line), the compose (from 240px) and sort
  (from 200px, on the list) buttons, the badge. The ring shows only for a refresh the user asked for.
- **[S-1286]** How many messages is not a setting (user decision): every Mail widget asks for the
  whole shelf the sync keeps per folder (`EmailConfig.FetchCount` = `MailShelves.MaxPerFolder`, 50),
  newest first, and the list scrolls past the rows that fit, so a big tile is never half empty and
  resizing never refetches. A `maxCount` left in a hand-edited file is ignored.
- **[S-1287]** Row view from 700px: when the user's columns leave Subject as the only flexible column
  and it would take more than 40% of the line, Preview joins after Subject. A chosen Preview keeps
  the user's order; without Subject nothing is added.
- **[S-1289]** Mail's header carries a sort button (from 200px wide) that turns the list round and
  saves `SortOrder`, like the menu's toggle; its tooltip says the current order. The compose button
  joins before it from 240px ([S-558]).

- **[S-7512]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone). Under Style: Show subject and Show account (List and Compact), Preview
  lines 1 to 5 (List), and for the Table its **Columns**, the menu's list ([S-3729]): the columns
  that are on in their order, dragged to reorder ([S-3732]), each with Remove (greyed on the last: one always
  stays), the ones that are off to add at the end, and what a narrow table drops first as a note.
  Then Appearance, **Accounts** (the kit's Accounts submenu over the signed-in mail accounts: Every
  mail account, a switch each, the last one greyed, Manage accounts) and **Folder** (Inbox, Sent,
  Drafts, Deleted, Junk).

Needs a live pass: a signed-in Microsoft, Google and IMAP account (the three styles, opening and
replying, the quick actions, Mark read and Delete landing on the server, Write email), and the
showcase views on the live desktop.
