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

- **[S-11406]** **Contacts states its sizes** ([S-2438]), each layout its own, all from the config:
  **List** rows of `Lines` (1..3, default 2: the name; the role, else a way to reach; the address,
  else the number), each led by its 32 monogram; **Compact** rows of one line with the one field as
  their value; **Details** one line a person under the column titles, as wide as its columns need
  (24 + 88 a column, at least two) to 720, a new one 420; these three show `Rows` people (1..20,
  default 5), 220..520 wide for List and Compact (a new one 300 × 284), their height the search box
  where on, the rows and the inset, dragging the bottom edge sets Rows, more scroll inside them.
  **Grid**: `GridColumns` cells a line (2..6, default 3) and `GridRows` lines (1..6, default 2) of
  48 monograms over their names, one size (no handle), more scroll inside it. **Search box**
  (`SearchBox`, on by default): over the people; off, the header's search button opens it. One
  column at every width; no layout falls back to another. The ladder ([S-1270], [S-1271], [S-1273],
  [S-1275], `ContactRules.Effective`) is gone. Tests `ContactsPlanTests`, `ContactsMenuTests.S11406_`,
  `ContactsFitTests.S11406_`.
- **[S-584]** A search box asks for the keyboard while focused (Esc clears). It
  starts empty: the `defaultSearch` setting is gone ([S-3796]).
- **[S-1272]** Details: one row of 28 per person, the chosen columns in the Row block's place for the
  name (`RowBlock.Cells`), 12 apart, the name primary and the rest secondary, their titles (Caption,
  secondary, one line and 8 under it) over them; no monogram (G2: it was 20 px, off the family's
  sizes).
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
