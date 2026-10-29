# Removable drives

The module `removableDrives` ([S-6911]): a row per removable device plugged in. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the drive widgets share (the walk
and its rules, the SDK's `DriveRules` and the host's `DriveCatalog`, with their root tags:
[S-4650]..[S-4657], which devices are removable [S-4660]; the drive's picture, the SDK's `DriveIcon`),
the drives coming from the host's one walk (`IWidgetHost.Drives`, [S-4655]) and Eject being the
host's launcher's ([S-4661]'s verb).

Owning files: everything in this folder: `RemovableDrivesModule.cs` (descriptor, view factory, JSON
context; no settings rows, [S-1750]), `RemovableDrivesConfig.cs`, `RemovableDrivesPlan.cs` (the rows'
look by the tier), `RemovableDrivesView.cs` (the rows, their menu and the keys), `Preview/` (Debug
renders: `--state one|devices|none`), `Localization/Locales/` (its own `RemovableDrivesStr`, 30
languages). Tests: `Tests/RemovableDrivesTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-4661]** Eject is the shell's own verb on the drive root (`ShellExecute` "eject", as in Karpo and
  Explorer): Windows flushes the volume and says how it went in its own notice. Only a drive root is
  accepted, and it runs on the pool. A render never ejects.
- **[S-4662]** A row is the family's Row block (group G6, 2026-10-29; [S-6711], `RemovableDrivesPlan`):
  the name as This PC names it (primary), the used share as its value (secondary), the space line as
  the second line and the kind with its file system as the third, as the tier gives rows lines
  ([S-6712]: one at XS, S and Wide, two at M and Tall, three at L and XL); the drive's picture a 32
  lead from a view of 180 (none on a narrower tile, so every row leads or none does); the used share's
  bar ([S-4653]) inline between the name and the share from a content of 240, a third of it at most
  140. Whole rows only, at least one, the rest scrolls under the fade. Eject is not a button in the
  row (it was "Eject" from 260 DIP, else the stick glyph): it is the row's menu (Open, Eject) and the
  keys ([S-4664]), as no list row carries a control of its own (G2). A double click opens the device.
  A render draws the kind's glyph, not the shell's picture ([S-4657]).
- **[S-4664]** Keys: Up and Down walk the devices, Enter opens one, Delete or E ejects it, Escape lets go.

Needs a manual pass: the widget on a live desktop (a stick plugged in and ejected, a card reader with
and without a card, a disc, the keys).
