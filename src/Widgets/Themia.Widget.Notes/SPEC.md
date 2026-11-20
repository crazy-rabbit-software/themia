# Notes

The module `notes` ([S-6911]): one markdown note from the notes kept on this PC, edited in place. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and the note store's root
tags: a note is a document the app owns ([S-1340]), atomic and single-writer files ([S-1341], the
rename retried, [S-49]), ids by shape ([S-1342]), the index's trust boundary ([S-1343]), titles
([S-1344]), missing told from empty ([S-1345]) and the one pool ([S-1348], the host's `NotePool`
behind `IWidgetHost.Notes` and, for the properties menu, `SettingsHost.Notes`); `NoteStore` and the
files stay Core's.

Owning files: everything in this folder: `NotesModule.cs` (descriptor, view factory, menu lines,
JSON context), `NotesConfig.cs` (the note shown), `NotesView.cs` (the note through
its host, the debounce, its pill) with `NotesView.Picker.cs` (the pill's note picker and "…"),
`MarkdownEditor.cs` (the editor and its menus), `Markdown/` (`NoteMarkdown`, `NoteEditing`, `NoteHistory`,
`NoteText`: the grammar, the keys and commands, undo, the lines to redraw), `NotesLayout.cs` (the
body, the pill's room and when it shows: `NotesPill`), `NoteMenus.cs` (which action stands where: the selection's
menu, the text's menu, the pill's picker and "…"), `NotesMenu.cs` (its properties-menu lines), `Preview/` (Debug renders:
`--state sample|selection|context|picker|more|notsaved|confirm`), `Localization/Locales/` (its own `NotesStr`, 40
languages). Tests: `Tests/NoteMarkdownTests.cs`, `Tests/NoteEditingTests.cs`, `Tests/NotesLayoutTests.cs`,
`Tests/NoteMenusTests.cs`, `Tests/NotesMenuTests.cs`, `Tests/NotesSizingTests.cs` (run by `tests/Themia.Modules.Tests`); the
store's are Core's `NoteStoreTests` and `LocalStoreFixtureTests`.

- **[S-128]** The note is handed to the note pool on a 400 ms debounce and flushed on blur, on
  switching note and on detach; the debounce is a write-rate limit, not a correctness path. A change
  from elsewhere never overwrites an unflushed local edit ([S-1348]).
- **[S-129]** The editor's chrome is removed through the text control's own resource keys (the
  `RichEditBox` shares the `TextBox`'s), not its `Background`: the stock template's visual states
  assign the border element directly and outrank a local value, so the field would light up solid
  on hover.
- **[S-3962]** No hover state: the pointer-over fill is the rest one (clear), and the field's border is
  no thickness in every state, so it never lights up as the pointer crosses it. Nor on focus (user
  requirement): the focused fill is the rest one too, with no accent frame or line (the focused state's
  own border thickness is zero, and no system focus rectangle); the caret alone shows where the keys go.
  Through the same resource keys, theme roles only.
- **[S-130]** "Clear note" confirms in a dialog. The text is unrecoverable, so one click is not
  enough, and failing to show the dialog leaves the note alone. It empties the note's body (for every
  widget showing it); the note and its title stay.
- **[S-1346]** A desktop widget with no `NoteId` creates a note of its own on first show (named after
  the widget's label, else "Note") and saves the id. A preview card (a debug render) creates nothing: it
  shows an empty draft that is never saved (original S-32).
- **[S-1347]** A widget whose note was deleted (anywhere) says "This note was deleted" and where to
  pick another (the properties menu, or the pill's picker, which then says "Choose a note", [S-14771]); it is never
  handed another note, and a pending edit to it is dropped (original S-36).
- **[S-1349]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Note** (the note shown at the end of its line;
  its submenu: a note when the shown note was deleted, the notes on this PC as a choice of one, each
  with when it was last written, "no notes" as a note; then Rename… for the note shown, New note
  (makes one and shows it) and Delete note, always confirmed ("Delete this note?", naming it under the question, [S-3727]); without a pool a
  note says notes are not available) and, under a divider, **Clear note** ([S-130], confirmed, greyed while
  the note is empty; not there while no note is shown, none kept on this PC or the one shown deleted,
  [S-6070]). The note has no text size of its own (G3, 2026-10-29: its Font size is gone): it is drawn in
  the family's steps at the text size every widget follows, ⚙ › Appearance › Text size ([S-3590],
  [S-6700]). The menu is built again on every change, so it follows the pool (original S-38). (Tests `NotesMenuTests`.)
- **[S-1350]** Block syntax, one line each: `#`..`###` (a space after the hashes), `- ` `* ` `+ `
  bullets, `1. ` / `1) `, `> `, `---` `***` `___` rules, and fences (every line between two fence
  lines is code). Indent depth: two spaces or a tab a level, capped at 6 (original S-15).
- **[S-1351]** Inline: bold `**`, italic `*` and `_` (word boundary only, so `snake_case` stays
  plain), strike `~~`, code (backticks, never nests) (original S-16).
- **[S-1352]** The spans of a line concatenate to exactly the source line; an unpartnered delimiter is
  literal text. The editor formats spans at their offsets and never rewrites text (original S-14/S-18).
- **[S-1353]** Every intercepted key and command is a pure `(text, selection) → (text, selection)` in
  `NoteEditing`; the control only applies the smallest replacement and places the caret.
- **[S-1354]** Enter continues `- `, `- [ ] ` (unchecked) and `1. ` (numbered on) when the caret is in
  the item's content; on an item holding only its marker it clears the marker. Shift+Enter is a plain
  newline (original S-19).
- **[S-1355]** Tab / Shift+Tab indent / outdent every touched line by two spaces; a tab outdents as one
  unit (original S-20).
- **[S-1356]** Ctrl+B / Ctrl+I wrap in `**` / `*`, unwrap when already wrapped (inside or just outside
  the selection), or insert an empty pair with the caret between (original S-21).
- **[S-1357]** Paste is plain text only, line endings normalised to `\n`. Native drag-and-drop is kept
  (RichEdit moves text correctly); whatever formatting a drop brings is overwritten when the touched
  lines are redrawn (original S-23, adapted).
- **[S-1358]** Tasks: `- [ ] ` / `- [x] ` (`X` too); a done item is struck and dimmed (secondary); a click that
  leaves the caret on the box toggles it, length-preserving. The box is drawn as a mark, tertiary like
  the other marks (never the accent, [S-6706]), rather than as an icon over the text — a `RichEditBox`
  cannot host one (original S-17, simplified).
- **[S-1359]** The formats (the text's menus, [S-14772]): heading cycles H1 → H2 → H3 → body (level from the first
  touched line); bullet / numbered / task / quote apply to every touched line, replace any other block marker,
  and strip when every line already has them; the menus light the formats in force at the selection (a row's icon as
  Windows lights a checked command, a list line checked; a checked task's strike does not light Strike,
  `NoteMenus.IsOn`) (original S-27).
- **[S-1360]** Undo/redo is the editor's own (`NoteHistory`: Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y, the menu):
  runs of single-character typing coalesce, Enter and pastes are steps of their own, no timers, 200
  steps. The control's own stack is off — it would also hold every formatting pass (original S-22).
- **[S-1361]** Only the lines an edit touched, plus lines whose parse changed (a fence opened or
  closed), are redrawn (`NoteText.LinesToRedraw`); replacements never split a surrogate pair.
- **[S-1362]** No word or character count, and no "Saved" (user requirement). The note's last write
  failing (the pool's save events) is the problem glyph in the pill, its tooltip "Not saved", the family's
  rule for content kept through a failure ([S-6710]); the pill then stands at rest and while typing ([S-14771]), the
  name giving the glyph its room; the text keeps all its room.
- **[S-1363]** The body is the text alone, from the card's top inset ([S-14770]), in the card's inset on all four
  sides as the editor's own padding (12, [S-6703]), at least a title's line and one more; no row over it. The widget is
  the one note at every size: there is no note list beside the text, at any size (user decision; the pill's picker
  switches notes). The note is drawn in the family's
  steps ([S-6700]): a first-level heading (the note's title) Title, 20 on its 28 line, at every width
  ([S-11206]); every other line Body,
  14 on 20, lower headings BodyStrong; two weights (a bold span 600); the markdown marks tertiary, a done
  task, code and a quote secondary, everything else primary, given to the RichEdit as the roles' opaque
  colours on the card ([S-6738]). A note it cannot show (deleted, unreadable, no notes on this PC) is the
  Status row at the top ([S-6714]; the warning for an unreadable one).
- **[S-14268]** A picture of the note (its sample on the add page, a render: unbound, no pool) stands as the desktop
  does: the text alone from the top at rest; under the pointer (`--hover 0.9,0.05`) the pill with its picker naming the
  sample's note ("Groceries") and its "…", opening nothing (WR-1: "there should not be a space at the top"). Renders
  `--widget-png notes --state sample|confirm --sizes range`.
- **[S-1364]** The pill's picker (the shown note's name and its chevron, [S-14771]; [S-12481]: no card header, so no
  title row) opens the note picker (every note, the shown one checked, then New note), at every size ([S-11206]);
  Rename… and Delete note… are the pill's "…".
- **[S-11206]** Converted to the sizing ([S-2438]): the text alone in a free range, 160..600 wide and
  80..600 high (a new one 300 × 200, `NotesLayout.Sizing`; [S-14770]: a row lower than with the first line's row, 100
  and 220); the text wraps and scrolls. Nothing appears or disappears with the size: the title is Title at every
  width (its BodyStrong step on a compact width is gone) and the pill's picker is there at every width, its name cut
  to the room the card leaves it.
- **[S-1365]** A format runs on the note's selection: the selection's bar takes no focus, and a format from either
  menu gives the note the focus back first. Bold, italic, strikethrough, code, bulleted list, checklist and quote are
  Segoe Fluent glyphs checked against the font; heading and numbered list are lettered ("H", "1.", in the text's face,
  semibold) because the font has neither.
- **[S-1366]** The editor admits no formatting the markdown does not hold: rich-text accelerators off,
  every Ctrl chord other than clipboard, selection and caret keys swallowed (Ctrl+Alt passes: AltGr),
  no stock selection flyout nor stock context menu (each carries Underline): its own menus ([S-14772]), copy as plain
  text. Keys are not intercepted while an IME composition is open.
- **[S-1367]** *(Retired: the widget never shows a note list beside the note, [S-1363]; the line's
  picker of [S-1364] picks, creates, renames and deletes.)*
- **[S-1368]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-3963]** *(Retired 2026-11-20 by [S-14770]..[S-14772]: no formatting bar on a first line of its own; the
  formats are the text's menus, the picker and "…" the pill over the text's top end.)*
- **[S-14770]** **The text from the top, the edit row overlaid** (owner, 2026-11-20: "for the notes widget, the text
  should start all they way at the top with the correct space of course, the edit bar should be shrunk, actions that
  can be inlined into a menu that opens at the mouse location after selecting text should be. the name of the note
  should also be shown with the dropdown selector. the edit row should bascially be overlayed on the topright with the
  text"). The note's text starts at the card's top inset: the editor is the card's whole box (it owns the top edge,
  `IWidgetTopEdge`) with the inset as its padding on all four sides, and no row stands over it (WR-1). What the row held
  is the pill over the text's top end ([S-14771]) and the text's own menus ([S-14772]). The sizing has no row in it
  ([S-11206]: 80 and 200 high, a row lower). (Tests `NotesSizingTests`; gap and item checks: no top band.)
- **[S-14771]** **The pill**: over the text's top end, the first line's box at its end (a title's 28, a body line's
  20, `NotesLayout.PillHeight`, so a title's words never show half under it and none of the next line's are hidden), its
  buttons standing 2 over and under a line of 20 and the last one's glyph on the inset as a tool line's ([S-6710]), in
  the card's opaque base (`SolidBackgroundFillColorBaseBrush`, a role the family's walk knows) so the words under it are
  hidden and it reads as the card, the one radius. It holds the tool line's
  parts (`ToolLine`): the problem glyph while a write failed ([S-1362]); the **picker**, the shown note's name (Body,
  secondary, "Choose a note" while none is shown, the placeholder for a blank title) and its chevron as one line button
  (`ToolLine.Dropdown`), the name cut with an ellipsis at what the card leaves it (`NotesLayout.NameWidth`: at most 160,
  never so wide that less than 40 of the first line stands beside the pill; none under 32, the chevron alone); and
  **"…"** (`ToolLine.Button`: Rename… and Delete note…, `NoteMenus.More`; what acts on the note itself; Clear note stays
  the widget's page's, [S-130], and nothing is on the card menu twice). **When it shows** (`NotesPill`): while the pointer
  is over the card, until a key is typed in the text (the text under it is then what the user needs) and again when the
  pointer moves; while its picker, its "…" menu, its rename box or its delete question is open; while the last write
  failed. At rest it is not there and the text is wholly seen (chosen over keeping the pill's width free on the first
  lines: a RichEdit cannot wrap round a box, and nothing changes size, WR-12). It fades in and out (Windows' fast fade,
  167 ms; none with animations off) and takes no pointer while gone. A render's `--hover` over the note draws it shown.
  (Tests `NotesLayoutTests`, `NoteMenusTests`.)
- **[S-14772]** **The selection's menu at the pointer**: letting go of the left button over text it selected (a drag, a
  double-click; not a click, which places the caret or ticks a box, [S-1358]; not in a note that cannot be typed in)
  opens the selection's bar where the pointer let go, down and to the right of it ([S-1748]): the one menu look
  (`ItemMenu.ShowBar`, a `CommandBarFlyout`'s icon row) with every format the toolbar had, all of which act on the
  selection or its lines: Bold, Italic, Strikethrough, Code, Heading, Bulleted list, Numbered list, Task list, Quote
  (`NoteMenus.Formats`), each lit while in force at the selection ([S-1359]). It takes no focus, so typing goes on in
  the text and closes it; so do a press in the text, Escape (which does nothing else) and the focus leaving for
  anything but a menu. A right-click in the text, Shift+F10 or the Menu key opens the text's menu (`NoteMenus.Text`,
  Context): Cut, Copy, Paste and the marks (Bold, Italic, Strikethrough, Code) in its row, as Windows' text menus are; a
  paragraph's kinds (checked while in force), Undo, Redo and Select all in its list, each with its key; twelve icons
  were more than the row holds, and the command bar folded two into its list on its own, one still lit
  (`NoteMenus.RowMost`). The keys stay (Ctrl+B, Ctrl+I, Tab, the list continuations). The formats in a menu's row are
  `QuickCommand`s after Paste ([S-1740], `ContextMenuLayout.IsFormat`). Renders `--state selection|context` draw the
  sample with "Coffee beans" selected and each menu open (`<png>-popup<n>.png`). (Tests `NoteMenusTests`, Core's
  `ContextMenuTests`.)

Needs a manual pass: the widget on a live desktop ([S-128]..[S-130], [S-1346], [S-1347], [S-1349],
[S-1364]..[S-1366], [S-14771], [S-14772]: typing saved and shown in a second widget of the same note, no fill or accent
line on the focused text box, the pill coming with the pointer and going with a key and with the pointer leaving the
card, its picker, "…", rename box and question, the selection's bar after a drag and a double-click and gone on typing,
a click and Escape, a format from it landing on the selection, the right-click and Shift+F10 menus, Clear and Delete with
their questions, an IME
composition, the properties menu's notes; G3: no border or accent line on hover and focus now that only the
border's thickness is cleared, [S-3962]; the marks tertiary and a done task secondary on a light card, which the
light renders draw in the primary role although the roles' opaque colours are right, [S-1363]), never in a
render or a test, which save nothing.
