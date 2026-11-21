# Clipboard

The module `clipboard` ([S-6911]): what has been copied recently, most recent first. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what is shared: the app's one
history ([S-1458], the host's `ClipboardHistoryService` behind `IWidgetHost.ClipboardHistory`, kept
while a config asks for it through `IKeepsClipboardHistory`), its bounds and text rules (the SDK's
`ClipboardHistory` and `ClipboardFormatting`), the list cursor ([S-1457], the SDK's `ItemCursor`) and
the widgets' one confirmation ([S-1500]).

Owning files: everything in this folder: `ClipboardModule.cs` (descriptor, glyph, view factory,
menu lines, JSON context), `ClipboardConfig.cs` (capacity and timestamps; an `IKitConfig`), `ClipboardSpec.cs` (its kit spec, an entry as a kit
item and its actions), `ClipboardView.cs` (a kit list, [S-7074]: the binding over the shared history as its source, the
marker as the rich look's lead), `ClipboardLayout.cs` (the previews, how a row is marked), `ClipboardMenu.cs` (its
properties-menu lines), `Preview/` (Debug renders: `--state sample|confirm`), `Localization/Locales/` (its own
`ClipboardStr`, 30 languages). Tests: `Tests/ClipboardTests.cs`, `Tests/ClipboardMenuTests.cs` (run by `tests/Themia.Modules.Tests`);
the host's history under its config is the module host's `ClipboardHostTests`.

- **[S-1440]** Clipboard keeps 1–200 entries, default 50 (original `maxItems`), in steps of 1.
- **[S-1441]** A row's preview is the entry on one line (`ClipboardFormatting.Preview`: whitespace and line
  breaks collapsed, ending in an ellipsis), trimmed by the row; how much it is ("32 characters", "3
  lines") is its second line ([S-1443]).
- **[S-1442]** Each row is marked by what it looks like (`ClipboardLayout.Classify`), a 16 lead in the
  secondary role: link (http/https/ftp), address, number (leading digit/sign), colour (`#hex`, `rgb()`,
  `hsl()` → a 16 dot of it, a data colour, [S-6705]), several lines, else text; the entry on the clipboard
  now keeps the tick and its preview in BodyStrong (no accent: [S-6706]). Display only, never opened.
- **[S-11405]** **It states its rows** ([S-2440]): `Rows` (1..20, default 5) entries and `Lines` per
  entry (1: the preview with when it was copied, default; 2: how much it is under it); the marker and
  the time at every width. 220..520 wide, a new one 300 × 188; Rows decide its natural height; the card shows the rows it holds at their optimal, stretched to fill it, and a resize stores none ([S-14500], [S-14502]); the rest of what is kept ([S-1440]) scrolls inside them (the wheel as before).
  Nothing appears or disappears with the size: [S-1443]'s ladder is gone, its other lines hold. Tests
  `ClipboardTests.S11405_`, `ClipboardMenuTests`.
- **[S-1443]** *(Its ladder retired by [S-11405].)* Clipboard ladder (`ClipboardLayout.Plan`) on the family's rows ([S-6711], [S-6712]): one
  row per entry, the preview the name, when it was copied the value (when shown), how much it is on the
  second line from a regular tile (M, Tall, L, XL); the marker and the time from a column of 180; as
  many whole rows as fit, the rest a scroll away under the fade; no count footer (concise: the entries
  only). Clear history is the card menu's line ([S-12481]), there only while there is something to clear.
  An empty or unwatched history is the state row's sentence at the top. A preview card never subscribes
  to the real clipboard; its `sample` state is a fixed history.
- **[S-1444]** It is a kit list ([S-7074], `ClipboardSpec`): a row's menu is the kit's (`KitMenu`): Copy, then Remove
  (its Delete); Clear history is the card menu's line alone, confirmed ([S-1500]). A click copies the entry back
  (`KitSpec.ClickRuns`, [S-7075]), as do a double-click and Enter; an entry's id is its text's fingerprint
  (`ClipboardSpec.Id`), never the text. Its time is the kit's compact age ("4m", "3h", "2d"). Needs a live pass.
- **[S-1561]** Clipboard takes the lease with the kit's one key map ([S-7020]): Up/Down/PgUp/PgDn/Home/End move a
  cursor, Enter copies the entry back, Ctrl+C copies it, Delete removes it from the history (the cursor stays on the
  row that moved up), Menu opens its menu, Escape drops the cursor.
- **[S-1450]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Show when it was copied** (a switch),
  under a divider (what is kept, apart from how it reads, user request 2026-10-19) **Entries**
  (typed, 1–200, [S-3726]; the number at the end of its line) and the privacy sentence as a note ([S-3728]: copied text stays in memory only, files and images are never
  read). No switch writes the history to disk, and clearing is the widget's own (its card menu's Clear history and
  row menu, [S-1444]), so the menu has no Clear line. (Tests `ClipboardMenuTests`.)

Needs a manual pass: the widget on a live desktop (text copied in another app arriving, a row
clicked and pasted back exactly, Remove and Clear history on two Clipboard widgets at once, the keys
through the lease), never in a render or a test, which never read the real clipboard.
