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
  default 5), 220..520 wide for List and Compact, their height the search box where on (with the titles one band of
  whole cells under the card's inset) and the rows at their optimal; Rows decide its natural height; the card shows the rows it holds at their optimal, stretched to fill it, and a resize stores none ([S-14500], [S-14502]); more scroll inside them.
  **Grid**: cells of 48 monograms over their names, as many a line as the card's width holds (a new one three) and
  `GridRows` lines (1..6, default 2) its natural height, as many lines as its height holds, more scroll inside it;
  its Rows line sets the lines, and the cells a line are the card's alone (owner, 2026-11-20: "Anything that's just
  'how many of the same item' ... follows the card's size", [S-14410]: the Columns line is gone, and with it the
  stored `GridColumns`, [S-14502]). **Search box**
  (`SearchBox`, on by default): over the people; off, the card menu's Search opens it ([S-12481]). One
  column at every width; no layout falls back to another. The ladder ([S-1270], [S-1271], [S-1273],
  [S-1275], `ContactRules.Effective`) is gone. Tests `ContactsPlanTests.S11406_S14410_`, `ContactsMenuTests.S11406_`,
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
- View links: a row or cell is its person (address, else name); the pointer or the keys' selection on it
  publishes them, and their row takes the hover fill while they are pointed at elsewhere on the view (another contacts
  widget, the next meeting's organiser line); a mail or a meeting of theirs never lights it ([S-11940], [S-11965]).
- **[S-12450]** **The rows reach the foot's inset** ([S-12807]): the list's, the compact list's and the
  details table's rows lay their text from under the search box and the titles to the foot's inset, up to half
  again their height (`ContactsPlan.Of`'s `Pitch`, `Over`, `Gap`, whole device pixels); the cards keep their size.
  Tests `ContactsPitchTests.S12450_`.
- **[S-14817]** **The grid's cells stand from the card's edges** (owner, 2026-11-21: "the weather forecast widget grid
  view items do not go from edge to edge, they have space everywhere. ... fix this, also for other widgets if
  applicable."; docs/WIDGET-RULES.md WR-2, WR-3, WR-4). A person's cell is one fixed size (the 96 monogram-and-name box
  and 6 round it, 110 × 80 on the default grid, `ContactsPlan.CellNaturalWidth`/`CellNaturalHeight` in whole cells) from
  the card's left edge and its top edge (under the search box where it shows), the cells touching, no inset round them and
  no gap between them: every column the width reaches, the last cut by its right edge (2 at least, no most, the shared rule `ItemSize.Cells`, [S-14816]), the
  lines past the card's foot a scroll away, the last in sight cut by it. A new grid is 330 wide, its lines 80 each. The
  `compact`, `details` and `grid` renders show each layout ([S-14814]). Tests `ContactsPitchTests.S14817_`.
