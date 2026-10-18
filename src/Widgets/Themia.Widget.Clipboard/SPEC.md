# Clipboard

The module `clipboard` ([S-6911]): what has been copied recently, most recent first. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections and what is shared: the app's one
history ([S-1458], the host's `ClipboardHistoryService` behind `IWidgetHost.ClipboardHistory`, kept
while a config asks for it through `IKeepsClipboardHistory`), its bounds and text rules (the SDK's
`ClipboardHistory` and `ClipboardFormatting`), the list cursor ([S-1457], the SDK's `ItemCursor`) and
the widgets' one confirmation ([S-1500]).

Owning files: everything in this folder: `ClipboardModule.cs` (descriptor, glyph, view factory,
settings rows, JSON context), `ClipboardConfig.cs` (capacity and timestamps), `ClipboardView.cs` (the
rows through its host, the menu, the keys, the copy back through `IWidgetHost.CopyText`),
`ClipboardLayout.cs` (the ladder, the previews, how a row is marked), `ClipboardSettings.cs` (its
page), `Preview/` (Debug renders: `--state confirm`), `Localization/Locales/` (its own
`ClipboardStr`, 30 languages). Tests: `Tests/ClipboardTests.cs` (run by `tests/Themia.Modules.Tests`);
the host's history under its config is the module host's `ClipboardHostTests`.

- **[S-1440]** Clipboard keeps 1–200 entries, default 50 (original `maxItems`); the slider steps by 1.
- **[S-1441]** A row's preview is up to the plan's lines: the entry's own line breaks kept, blank
  lines dropped, whitespace collapsed inside a line, " …" when more lines follow; wrapped and
  trimmed by the row. One line is exactly the old single-line preview.
- **[S-1442]** Each row is marked by what it looks like (`ClipboardLayout.Classify`): link (http/https/
  ftp), address, number (leading digit/sign), colour (`#hex`, `rgb()`, `hsl()` → a swatch of it),
  several lines, else text; the current entry keeps the accent tick. Display only, never opened.
- **[S-1443]** Clipboard ladder (`ClipboardLayout.Plan`): XS/S one line, compact rows → M + time
  column → Wide 2-line previews + time → Tall 2 lines + detail line ("4m · 32 characters")
  → L/XL 3 lines + detail; no count footer (concise: the entries only). A preview card never
  subscribes to the real clipboard.
- **[S-1444]** Clipboard row menu (shared `ItemMenu`): Copy, Remove from history, Clear history…
  (confirmed through `FluentLayout.ConfirmAsync`, like the header bin). `HandleKey`: Up/Down/Home/End
  cursor, Enter copies, Escape drops it — through the shell's keyboard lease ([S-800]..[S-812]),
  `ClipboardView : IWidgetKeys`; needs a live pass.
- **[S-1561]** Clipboard takes the lease: Up/Down/PgUp/PgDn/Home/End move a cursor, Enter copies the
  entry back, Delete removes it from the history (cursor stays on the row that moved up), Menu opens
  its menu, Escape drops the cursor.

Needs a manual pass: the widget on a live desktop (text copied in another app arriving, a row
clicked and pasted back exactly, Remove and Clear history on two Clipboard widgets at once, the keys
through the lease), never in a render or a test, which never read the real clipboard.
