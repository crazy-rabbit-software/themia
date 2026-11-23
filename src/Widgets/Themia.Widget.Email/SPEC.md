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
`--state sample|accounts|empty|hovered|focused|stale|offline`),
`Localization/Locales/` (its own `EmailStr`, 30 languages). Tests: `Tests/MailSpecTests.cs` and `Tests/EmailMenuTests.cs` (run by
`tests/Themia.Modules.Tests`; the ladder's are the kit's, `KitLinedTests`); the config's and the screens' are the module host's (`MailFeedTests`).

- **[S-540]** `EmailConfig` keeps the classic wire names (`folder: inbox|sentitems|drafts|deleteditems|junkemail`,
  `viewMode: list|compact|row`, `sortOrder: desc|asc`, `listFields`, `rowColumns`, `accountIds`).
  Every closed set is read leniently: an unknown or mistyped value becomes the default and is
  logged, never a throw that would refuse the state file. `WidgetConfig.Repair()` runs at load from
  `StateRepair.Repair`; for mail it keeps at least one row
  column. No hearth-era field is read ([S-1050]).
- **[S-541]** `accountIds` empty means every mail account, including ones added later (the classic
  widget showed "select an account" instead); a non-empty list reads those accounts that are still
  signed in. Its menu lists the signed-in mail accounts as switches under "Every mail account" and
  opens ⚙ › Accounts ([S-7512], [S-3558]); no account is ever signed in from the widget. Folders: Inbox,
  Sent, Drafts, Deleted, Junk [S-12].
- **[S-542]** The messages on the item base ([S-16971]); newest or oldest first over the same newest N [S-17]. No card
  header ([S-12480]): the kit's status over the content's top end ([S-12481]) carries the kit's problem glyph ([S-7064]:
  one line per account that failed or needs signing in, a tap → Accounts where only the user can mend it) and a busy ring
  only for a refresh the user asked for. The card menu offers Refresh, Write email ([S-558]) and the five folders [S-20];
  the style and the order are settings only ([S-1746]).
- **[S-543]** *(Retired by [S-16971]: the subject, the account and the date are the item's parts, shown and ordered in the item
  base's one checklist ([S-16950]); the table's columns are those parts in that order; the preview is a switch.)*
- **[S-6403]** *(Retired by [S-16971]: Mail's styles are the item base's arrangements; a stored `listFields`, `rowColumns` or
  `lines` is dropped at load, a stored `compact` style is read as the default.)*
- **[S-16971]** **Mail on the item base** (owner, 2026-11-23: "same should be done for email and contacts too"; "there should
  also be a base style where this data is shown as a table"): a message is an item (`MailSpec.Base`): its sender the title
  (unread strong and primary, read plain and secondary: no lead, [S-2270]), its subject under it (its preview after it,
  "Subject – preview", where **Preview** is on), when it came at its end (the kit's compact age), its account's name the
  note (left out by default, [S-12801]); every arrangement (List, Row list, Column list, Grid, Table: its columns From,
  Subject, Account, Date in the parts' order, its column titles on by default), its gap and Rows (a new card five). It
  stays a kit view through `ItemKit` ([S-16958]): a click opens a message, the Menu key and a right click its menu
  ([S-544]), Delete deletes it where its account allows. Tests `EmailItemTests`, `EmailMenuTests`.
- **[S-544]** Message right-click: Open, Reply, Mark read/unread, Archive, Move to Junk, Copy subject,
  Delete [S-21], built by the kit's one menu builder ([S-7011], [S-7013]: Reply an OpenTo, the marks,
  Archive and Junk changes): Copy subject and Delete are its icon row, Enter, Ctrl+C and Delete are
  shown beside their lines. Gmail is read through `gmail.readonly`, so its messages offer only Open, Reply and
  Copy [S-22]. Delete moves to Deleted (Graph move, IMAP UID MOVE); only a message already in Deleted
  is removed for good (Graph DELETE; IMAP `\Deleted` + `UID EXPUNGE`, UIDPLUS-scoped). A change shows
  at once and waits in the account's queue until the provider has it ([S-2610], [S-2624]); a refusal
  is the status's warning tooltip and the next probe fetches the truth.
- **[S-11402]** *(Retired by [S-16971]: its rows are the item base's: Rows (1..20, a new one 5) decide its natural height,
  one face a message, the table's widths the base's.)*
- **[S-1280]** *(Retired by [S-11402]: what a row shows is its lines, never the tile.)* Mail's ladder (the kit's message and table looks on the family's rows, [S-7032],
  [S-7034]): a Row block per message, the sender the name (BodyStrong while unread), the time the
  value, the subject with the account after it ("Quarterly numbers · Work") on the second line
  from a regular tile, the preview on the third from an expanded one; the rows that fit whole, the
  rest a scroll away. The table's column titles from 140px of height. No provider name and no
  folder summary ([S-1285], retired). With no mail account the Set up row says "Add an account to
  show your mail" ([S-1862]).
- **[S-1282]** *(Retired by [S-2270]: no envelope leads a row.)* Unread is the strong name alone: no
  accent envelope, no accent bar, no accent time.
- **[S-2270]** No row leads: an envelope on every row tells nothing ([S-6711]: a lead only where it
  tells rows apart), so the sender's name starts at the inset, in line with the content's text edge. Unread
  is told by the name alone: BodyStrong in the primary role unread, Body in the secondary role read
  (`KitSpec.Unread`, the kit's quiet row, `RowRules.Roles(quiet)`), in the stacked looks and the
  table's From column alike; the second line, the badge and the time are as before.
- **[S-1283]** Retired (G2, 2026-10-29): one-line rows with a sender column, the subject and preview
  after an en dash and an account column on a strip or a wide tile. Every row is the Row block; a
  strip shows one line, the sender and the time; Compact holds the rows to the sender and subject.
- **[S-1284]** The account tag and the tooltip name the account alone at every size (the provider
  name, "Work (Microsoft)", is retired, [S-666]). Mark read or unread, archive and delete are the
  item menu's and the keys' (the quick actions a one-line row swapped in for its time under the
  pointer are retired with [S-7033]).
- **[S-6420]** Mail on the kit (`MailSpec`): a message is a `KitItem` (the sender its title, the subject its subtitle, the
  preview, the account's name its badge, unread its emphasis) mapped onto the item base's item ([S-16971]), its menu lines
  `KitAction`s; its keys are the item base's ([S-16955]).
- **[S-6421]** Mail is a kit view ([S-7061]): `MailKitSource` reads the host's one mail source for
  the config's accounts, folder and count (the feed is pushed; Refresh is the feed's), its messages as
  items, each account read as an outcome (signed out: sign in again; not answered: loading; failed:
  its status, stamped with when its list is from), the accounts' states, the unread count for the
  badge, and a change waiting, refused or a command that failed as the header's notes until the next
  update. The binding draws the kit's screens, state panel and status: one stale rule ([S-7042]: the
  problem glyph, one line per account, the age on the Refresh line). The ring shows only for a refresh the user asked for.
- **[S-1286]** How many messages is not a setting (user decision): every Mail widget asks for the
  whole shelf the sync keeps per folder (`EmailConfig.FetchCount` = `MailShelves.MaxPerFolder`, 50),
  newest first, and the list scrolls past the rows that fit, so a big tile is never half empty and
  resizing never refetches. A `maxCount` left in a hand-edited file is ignored.
- **[S-1287]** *(Retired by [S-16971]: the table's columns are the parts shown, the user's alone.)*
- **[S-1289]** *(Retired by [S-12481]: no card header, so no header buttons. The order is the page's
  setting; writing a new email is the card menu's line, one per account, [S-558].)*

- **[S-7512]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the settings page is gone): the
  item base's lines (**Each item shows**: From, Subject, Account, Date in order with **Preview**; **Gap between items**, or a
  table's **Column titles**; **Rows**), then **Accounts** (the kit's Accounts submenu over the signed-in mail accounts:
  Every mail account, a switch each, the last one greyed, Manage accounts) and **Folder** (Inbox, Sent, Drafts, Deleted,
  Junk).

- **[S-2043]** A click on the card ([S-2000]) opens the mail of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `EmailModule.Opens`); an IMAP, CalDAV or CardDAV account opens nothing. A
  message's row keeps its click ([S-2004]).

Needs a live pass: a signed-in Microsoft, Google and IMAP account (the three styles, opening and
replying, the quick actions, Mark read and Delete landing on the server, Write email), and the
showcase views on the live desktop.
- View links: a row is its message, never its sender: the pointer on it lights that row alone and publishes the
  message, and the row that is the same message in another mail widget of the view takes the hover fill; another
  mail from the same sender stays as it is ([S-11942], [S-11965], [S-11966]).
