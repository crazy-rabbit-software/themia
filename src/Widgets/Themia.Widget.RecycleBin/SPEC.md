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
- **[S-4862]** The ladder (`RecycleBinLayout.Plan`): the size always; then the count, the Empty
  button, the bin's icon over them; from 300 DIPs of height the size in TitleLarge and a 72-DIP
  icon. A wide strip puts the icon at the left and Empty at the right.
  Where the button does not fit, Empty stays in the card menu, with Open Recycle Bin.

Needs a live pass: Empty with Windows' dialog (Yes and No), deleting a file from Explorer (the count
following within 15 s).
