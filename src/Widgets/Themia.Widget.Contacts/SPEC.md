# Contacts

The module `contacts` ([S-6911]): the people of the signed-in accounts, as a list, a compact list, a
details table or cards, searched and opened. Its tags are this file's alone; the root `SPEC.md`
holds what it shares, with their root tags: where the people come from and the config ([S-580]), the
layouts ([S-581], `ContactRules.Effective`, `ContactsPlan`), the details columns ([S-582]), the count ([S-583]), the
search ([S-585], `ContactSearch`), the avatars ([S-586]), the person's menu ([S-587]), the shared
source ([S-588]), the ladder and the keys ([S-1270], [S-1271], [S-1273], [S-1275], [S-1277],
`ContactRules`, in the SDK).

Owning files: everything in this folder: `ContactsModule.cs` (descriptor, view factory, JSON
context), `ContactsConfig.cs` (with its Set up sentence), `ContactsPlan.cs` (each layout's plan and sizes on the family's
rows and cells), `ContactsView.cs` (the layouts drawn from the family's blocks, the search box, the
selection and the keys, the person's menu, over `IWidgetHost.Office`), `ContactsMenu.cs` (its
properties-menu lines: the SDK's account lines, the layout, the columns, the count), `Preview/` (Debug
renders: `--state sample`, a fixed address book on the reserved `.example` domain),
`Localization/Locales/` (its own `ContactsStr`, 30 languages). Tests: the shared rules' are Core's
(`ContactsAndBirthdaysTests`, `ContactsFitTests`); the ladder's `Tests/ContactsPlanTests.cs`; the
config's are the module host's (`ContactsHostTests`, `OfficeConfigTests`); the menu's are
`Tests/ContactsMenuTests.cs`.

- **[S-16972]** **Contacts on the item base** (owner, 2026-11-23: "same should be done for email and contacts too, although
  contacts has the search box that should be kept"): a person is an item (`ContactItems.Item`): their monogram the lead
  ([S-586]; the people glyph in a 16 slot, where two initials do not read), their name, their role ("Engineer · Contoso")
  under it, their address the note, their number at the end (left out by default); every arrangement of the base (List,
  Row list, Column list, Grid, Table: its columns Name, Role, Email, Phone in the parts' order under their titles), its
  gap and Rows (a new card five) under the search field's band where **Search box** is on (`ContactsModule.Sizing`: the
  base's sizing and the band's 30). A click writes to them, else calls them ([S-1277]); the keys are the item base's
  ([S-16955]) and type-ahead puts the cursor on a name ([S-1983]); their menu is [S-587]'s. With the box off the card
  menu's Search opens the field over them until it is left empty. Its page: Accounts, then the item base's lines, Search
  box and People. Its old Layout and Columns, Lines and Grid rows are gone with `ContactsLayout` and `ContactColumn`; a stored
  one is dropped at load. Tests `ContactItemsTests`, `ContactsMenuTests`.
- **[S-16973]** **Contact** (`contact`, owner: "contact should get a single too"): one person the user picks (**Person**: one of
  the people the contacts widgets read last, `ContactsSeen`), on the item base's face ([S-16957]); no one picked, or the one
  picked no longer there, keeps the face: "--" and why (WR-10). A click writes to them, a right-click their menu; the card
  lights as the person ([S-11940]). Its page: Person, its parts, the accounts. Tests `ContactItemsTests.S16973_`,
  `ContactsMenuTests.S16973_`.
- **[S-11406]** *(Retired by [S-16972]: each layout's sizes are the item base's arrangements'.)*
- **[S-584]** A search box asks for the keyboard while focused (Esc clears). It
  starts empty: the `defaultSearch` setting is gone ([S-3796]).
- **[S-1272]** *(Retired by [S-16972]: the item base lays the people.)*
- **[S-1276]** *(Was: the People count a number box on the settings page; now the menu's number,
  [S-7511].)*
- **[S-7511]** Its options are on the properties menu ([S-3719], user request 2026-10-19; the
  settings page is gone): Appearance first, then **Accounts** (the kit's Accounts submenu over the
  signed-in contacts accounts, `OfficeMenuLines.Accounts`: Every contacts account, a switch per
  account, the last one greyed, Manage accounts), then after a divider how they are listed: **Layout** (List, Compact, Details, Grid), on the
  details layout only **Columns** (a switch per column in canonical order, the last one on greyed,
  [S-582]) and **People**, a number asked for as text (`KitMenuLines.Count`, [S-3726]): 5 to 1000,
  rounded to a step of 5, anything else refused with the range as the reason (the count had no upper
  bound; 1000 is past what the widget lists, [S-583]).
- **[S-2045]** A click on the card ([S-2000]) opens the contacts of the account it reads on the web:
  Outlook's for a Microsoft account, Google's for a Google one, the first chosen account that has
  such a page ([S-2005], `ContactsModule.Opens`); an IMAP, CalDAV or CardDAV account opens nothing.
  A contact's row keeps its click ([S-2004]).

Needs a live pass: a Microsoft, Google and CardDAV address book with a few hundred people (search,
the menu's email, call and message hand-offs), and the search box under the keyboard lease.
- View links: a row or cell is its person (address, else name); the pointer or the keys' selection on it
  publishes them, and their row takes the hover fill while they are pointed at elsewhere on the view (another contacts
  widget, the next meeting's organiser line); a mail or a meeting of theirs never lights it ([S-11940], [S-11965]).
- **[S-12450]** *(Retired by [S-16972]: the item base lays the people.)*
- **[S-14817]** *(Retired by [S-16972]: the item base lays the people.)*
- **[S-15775]** **Nothing stands over the field and the titles** (docs/WIDGET-RULES.md WR-1, WR-4, WR-5): the search
  field stands in a band on the card's top edge (`ContactsPlan.Bands`: the family's control of 28 in whole grid cells,
  30 on the default grid; the field at the card's side inset in it), the details table's column titles in a band of
  whole cells (their Caption line and 8, 30) on the top edge or right under the field's, each title centred in it (the
  table's head row, an item band edge to edge as a kit table's); the rows start right under the last band and run to
  the card's foot, the last cut by it. Before, the field stood 12 under the top edge and the details' rows started
  under the titles' band by the inset's 12 too early, under the titles, and stopped 12 over the foot. Tests
  `ContactsPlanTests.S11406_S15775_`.
- **[S-15776]** **The empty, no-account and offline faces are rendered** (WR-10): `none` (an account with no people: the
  kit's state row "No contacts" in the first row's place over the empty rows), `noAccount` (the card's Set up line over
  the empty rows) and `offline` (the account's "No network" in the first row's place, the rows kept empty under it).
- **[S-15785]** **The face stays in every state, its rows one size** (WR-6, WR-10, WR-11): the search field's band stands
  where the config has it in every state (loading, no account, a problem, nobody), at a row's one size (`ContactsPlan.FieldBand`:
  the list's rows of its lines, 40 for two, a one-line row elsewhere; the search line opened in the first line is that
  size too), the field the family's 30 centred in it; the state's row and its empty rows stand under it at the rows'
  one size (`KitStatePanel.RowPitch`). With no account contacts sets up in place, as the calendar does
  (`ISetupRule.SetsUpInPlace`): its own line "Add an account to show your contacts" (its setup sentence) with the way to
  Accounts over the empty rows, never the rows alone. Tests `ContactsPlanTests.S11406_S15785_`, `WidgetLivenessTests`.
- **[S-15797]** *(Retired by [S-16972]: the item base lays the people.)*
