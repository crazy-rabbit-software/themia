# Recycle Bin (RECYCLE-BIN)

The module `recycleBin` ([S-6911]): the Recycle Bin's size and item count, with Empty. Its tags are
this file's alone; the root `SPEC.md` holds the host's sections: the host's one answer and its
Empty (the Recycle Bin section, [S-4863], [S-4864]; the Shortcuts badge [S-1389]), the host's
services on `IWidgetHost` ([S-6913]), the ladder rules [S-660]..[S-666].

Owning files: everything in this folder: `RecycleBinModule.cs` (descriptor, view factory, JSON
context), `RecycleBinConfig.cs`, `RecycleBinRules.cs` (the texts, the ladder), `RecycleBinView.cs`,
`Preview/` (Debug renders), `Localization/Locales/` (its own `RecycleBinStr`, 30 languages). Tests:
`Tests/RecycleBinTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4860]** `RecycleBin` (127): the Recycle Bin's size and item count, with Empty. No settings.
- **[S-4861]** The answer is the host's one answer (`IWidgetHost.RecycleBin`, the shared `RecycleBinStatus` the Shortcuts badge reads, [S-1389]):
  `SHQueryRecycleBinW` over every drive on the thread pool, single-flight, asked when the shell says
  the bin changed while a widget shows it. The size line is the bytes ("–" before the first answer);
  the count "37 items", "No items" when empty.
- **[S-4862]** On the family's blocks (group G6, 2026-10-29; `RecycleBinLayout.Plan`): the header
  (the bin's glyph and name); the size as a Readout at the view's top, start-aligned ([S-6702]: on its
  widest case "1023 MB", 28 at XS, S and Wide, 40 at M and Tall, 68 at L and XL), the count as its line
  under; Empty as the one main action in the Controls row at the bottom inset ([S-6716]), kept where it
  fits under the floor step and its line under (not at XS and S), the size taking the largest step the
  rest holds. Where Empty does not fit it stays in the card menu, with Open Recycle Bin. An empty bin is
  the Status row at the top ("No items" under the bin's glyph, [S-6714]); before the first answer the
  size is "–" with no Empty. The bin's 40 and 72 icon, the centred stack, the wide strip and the
  Subtitle and TitleLarge sizes are gone.
- **[S-2034]** A click on the card ([S-2000]) opens the Recycle Bin in File Explorer
  (`shell:RecycleBinFolder`); Empty keeps its click ([S-2004]); the module decides it
  (`RecycleBinModule.Opens`).

Needs a live pass: Empty with Windows' dialog (Yes and No), deleting a file from Explorer (the count
following within 15 s).
