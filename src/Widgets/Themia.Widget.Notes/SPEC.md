# Notes

The module `notes` ([S-6911]): one markdown note from the notes kept on this PC, edited in place. Its
tags are this file's alone; the root `SPEC.md` holds the host's sections and the note store's root
tags: a note is a document the app owns ([S-1340]), atomic and single-writer files ([S-1341], the
rename retried, [S-49]), ids by shape ([S-1342]), the index's trust boundary ([S-1343]), titles
([S-1344]), missing told from empty ([S-1345]) and the one pool ([S-1348], the host's `NotePool`
behind `IWidgetHost.Notes` and, for the properties menu, `SettingsHost.Notes`); `NoteStore` and the
files stay Core's.

Owning files: everything in this folder: `NotesModule.cs` (descriptor, view factory, menu lines,
JSON context), `NotesConfig.cs` (the note shown and its text size), `NotesView.cs` (the note through
its host, the debounce, the header's title and bar) with `NotesView.Picker.cs` (the header's note
picker), `MarkdownEditor.cs` (the editor), `Markdown/` (`NoteMarkdown`, `NoteEditing`, `NoteHistory`,
`NoteText`: the grammar, the keys and commands, undo, the lines to redraw), `NotesLayout.cs` (the
ladder and the header bar), `NotesMenu.cs` (its properties-menu lines), `Preview/` (Debug renders:
`--state sample|editing|confirm`), `Localization/Locales/` (its own `NotesStr`, 30 languages). Tests:
`Tests/NoteMarkdownTests.cs`, `Tests/NoteEditingTests.cs`, `Tests/NotesLayoutTests.cs`, `Tests/NotesMenuTests.cs` (run by
`tests/Themia.Modules.Tests`); the store's are Core's `NoteStoreTests` and `LocalStoreFixtureTests`.

- **[S-128]** The note is handed to the note pool on a 400 ms debounce and flushed on blur, on
  switching note and on detach; the debounce is a write-rate limit, not a correctness path. A change
  from elsewhere never overwrites an unflushed local edit ([S-1348]).
- **[S-129]** The editor's chrome is removed through the text control's own resource keys (the
  `RichEditBox` shares the `TextBox`'s), not its `Background`: the stock template's visual states
  assign the border element directly and outrank a local value, so the field would light up solid
  on hover.
- **[S-3962]** No hover state: the pointer-over fill and border are the rest ones (transparent), so
  the field never lights up as the pointer crosses it. Nor on focus (user requirement): the focused
  fill is the rest one too, with no accent frame or line (the focused state's elevation brush and
  thickness are cleared, and no system focus rectangle); the caret alone shows where the keys go. Through the same resource
  keys, theme brushes only.
- **[S-130]** "Clear note" confirms in a dialog. The text is unrecoverable, so one click is not
  enough, and failing to show the dialog leaves the note alone. It empties the note's body (for every
  widget showing it); the note and its title stay.
- **[S-1346]** A desktop widget with no `NoteId` creates a note of its own on first show (named after
  the widget's label, else "Note") and saves the id. A preview card (a debug render) creates nothing: it
  shows an empty draft that is never saved (original S-32).
- **[S-1347]** A widget whose note was deleted (anywhere) says "This note was deleted" and where to
  pick another (the properties menu, or the header's picker); it is never handed another note, and a pending edit to
  it is dropped (original S-36).
- **[S-1349]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Note** (the note shown at the end of its line;
  its submenu: a note when the shown note was deleted, the notes on this PC as a choice of one, each
  with when it was last written, "no notes" as a note; then Rename… for the note shown, New note
  (makes one and shows it) and Delete note, always confirmed ("Delete this note?", naming it under the question, [S-3727]); without a pool a
  note says notes are not available), under a divider **Font size** (typed, 8–48 px, [S-3726]) and,
  under another, **Clear note** ([S-130], confirmed, greyed while the note is empty; not there while
  no note is shown, none kept on this PC or the one shown deleted, [S-6070]). The menu is built again on
  every change, so it follows the pool (original S-38). (Tests `NotesMenuTests`.)
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
- **[S-1358]** Tasks: `- [ ] ` / `- [x] ` (`X` too); a done item is struck and dimmed; a click that
  leaves the caret on the box toggles it, length-preserving. The box is drawn in the accent colour
  rather than as an icon over the text — a `RichEditBox` cannot host one (original S-17, simplified).
- **[S-1359]** Toolbar commands: heading cycles H1 → H2 → H3 → body (level from the first touched
  line); bullet / numbered / task / quote apply to every touched line, replace any other block marker,
  and strip when every line already has them; buttons light for the marks at the caret (a checked
  task's strike does not light Strike) (original S-27).
- **[S-1360]** Undo/redo is the editor's own (`NoteHistory`: Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y, the menu):
  runs of single-character typing coalesce, Enter and pastes are steps of their own, no timers, 200
  steps. The control's own stack is off — it would also hold every formatting pass (original S-22).
- **[S-1361]** Only the lines an edit touched, plus lines whose parse changed (a fence opened or
  closed), are redrawn (`NoteText.LinesToRedraw`); replacements never split a surrogate pair.
- **[S-1362]** No word or character count, and no "Saved" (user requirement). The one status line
  is "Not saved" under the text, only while the note's last write failed (the pool's save events),
  where a line is left after the text's two.
- **[S-1363]** Ladder (`NotesLayout.Plan`): the text always (at least two lines; headings at body size
  on a compact width) → the "Not saved" line while a write failed ([S-1362]); the text takes the
  rest. The formatting bar is not a row of the body ([S-3963]). The widget is the one
  note at every size: there is no note list beside the text, at any size (user decision; the
  header's picker switches notes).
- **[S-1364]** The header title is the note's title (the user's own widget label still wins). A
  chevron in the header opens the note picker (every note, New note, Rename…, Delete note…), except at
  XS (it would push the title out).
- **[S-1365]** The bar's buttons never take focus, so a command runs on the note's selection. Bold,
  italic, strikethrough, bulleted list and checklist are Segoe Fluent glyphs checked against the font;
  heading and numbered list are lettered ("H", "1.") because the font has neither.
- **[S-1366]** The editor admits no formatting the markdown does not hold: rich-text accelerators off,
  every Ctrl chord other than clipboard, selection and caret keys swallowed (Ctrl+Alt passes: AltGr),
  no selection flyout, its own context menu (undo, redo, cut, copy, paste, select all), copy as plain
  text. Keys are not intercepted while an IME composition is open.
- **[S-1367]** *(Retired: the widget never shows a note list beside the note, [S-1363]; the header
  picker of [S-1364] picks, creates, renames and deletes.)*
- **[S-1368]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-3963]** The formatting bar stands in the card's header, on the title line between the title
  and the note picker, while the note is being edited: the caret is in the note, or the bar's "More"
  menu is open. At rest the header is the title and the picker, as every card's is; the bar comes
  with the caret, where formatting is wanted, and goes with it. Compact 24 DIP buttons that keep the
  header its height (so focusing never moves the text). `NotesLayout.HeaderBar`: every button where
  the header has room after its insets, icon, the title's 64 DIP minimum ([S-655]) and the picker;
  else as many as fit, by rank (bold, italic, task, bullet, heading, strike, code, numbered, quote),
  and a "More" button whose menu holds the rest (checked for the marks at the caret); nothing where
  not even "More" fits. Like the bar's buttons, "More" never takes focus. Debug renders:
  `--widget-png Notes <out> --state sample|editing` draw a fixed note at rest and while edited.

Needs a manual pass: the widget on a live desktop ([S-128]..[S-130], [S-1346], [S-1347], [S-1349],
[S-1364]..[S-1366]: typing saved and shown in a second widget of the same note, no fill or accent line on
the focused text box, the header's picker and bar, Clear and Delete with their questions, an IME
composition, the properties menu's notes), never in a render or a test, which save nothing.
