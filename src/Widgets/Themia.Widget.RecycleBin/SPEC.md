# Recycle Bin (RECYCLE-BIN)

The module `recycleBin` ([S-6911]): the Recycle Bin's size and item count, with Empty. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections: the host's one answer and its
Empty (the Recycle Bin section, [S-4863], [S-4864]; the Shortcuts badge [S-1389]), the host's
services on `IWidgetHost` ([S-6913]), the ladder rules [S-660]..[S-666].

Owning files: everything in this folder: `RecycleBinModule.cs` (descriptor, view factory, JSON
context), `RecycleBinConfig.cs`, `RecycleBinRules.cs` (the texts, the one size), `RecycleBinView.cs`,
`Preview/` (Debug renders), `Localization/Locales/` (its own `RecycleBinStr`, 30 languages). Tests:
`Tests/RecycleBinTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4860]** `RecycleBin` (127): the Recycle Bin's size and item count, with Empty. No settings.
- **[S-4861]** The answer is the host's one answer (`IWidgetHost.RecycleBin`, the shared `RecycleBinStatus` the Shortcuts badge reads, [S-1389]):
  `SHQueryRecycleBinW` over every drive on the thread pool, single-flight, asked when the shell says
  the bin changed while a widget shows it. The size line is the bytes ("–" before the first answer);
  the count "37 items", "No items" when empty.
- **[S-4862]** On the family's blocks (group G6, 2026-10-29): the header (the bin's glyph and name);
  the size as the family's 28 Readout at the view's top, start-aligned ([S-6702]), the count as its
  line under; Empty as the one main action in the Controls row at the bottom inset ([S-6716]). The card
  menu has Open Recycle Bin and Empty too.
- **[S-11201]** Converted to the sizing ([S-2438]): **one fixed size**, 180 × 136 (the header, the
  reading on its widest case "1023 MB" with its line under, the Controls row and the bottom inset;
  `RecycleBinLayout.Size`), no handle. All three parts are always there: an empty bin reads "0 B" and
  "No items" with Empty disabled (no Status row any more); before the first answer the size is "–"
  with Empty disabled. No ladder is left.
- **[S-2034]** A click on the card ([S-2000]) opens the Recycle Bin in File Explorer
  (`shell:RecycleBinFolder`); Empty keeps its click ([S-2004]); the module decides it
  (`RecycleBinModule.Opens`).

Needs a live pass: Empty with Windows' dialog (Yes and No), deleting a file from Explorer (the count
following within 15 s).
