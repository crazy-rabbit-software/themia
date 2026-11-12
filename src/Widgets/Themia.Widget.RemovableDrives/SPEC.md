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

- **[S-11027]** A list atom ([S-11008]): its rows are its one setting (`Rows`, 1..20, four by default,
  `IKitRows`, `KitMenuLines.Rows` without a lines choice), each two lines (the name, the space line
  under it), its height following them (36 + 40 a row + 12, `KitSizing`, 220..520 wide, a new one
  300 × 208); more devices scroll inside them. No tier ladder is left: the picture and the bar stand
  in every row at every width (`RemovableDrivesPlan.Of`).
- **[S-4661]** Eject is the shell's own verb on the drive root (`ShellExecute` "eject", as in Karpo and
  Explorer): Windows flushes the volume and says how it went in its own notice. Only a drive root is
  accepted, and it runs on the pool. A render never ejects.
- **[S-4662]** A row is the family's Row block (group G6, 2026-10-29; [S-6711], `RemovableDrivesPlan`):
  the name as This PC names it (primary), the used share as its value (secondary), the space line as
  the second line ([S-11027]); the drive's picture a 32 lead; the used share's bar ([S-4653]) inline
  between the name and the share, a third of the content at most 140. The rest scrolls under the fade. Eject is not a button in the
  row (it was "Eject" from 260 DIP, else the stick glyph): it is the row's menu (Open, Eject) and the
  keys ([S-4664]), as no list row carries a control of its own (G2). A double click opens the device.
  A render draws the kind's glyph, not the shell's picture ([S-4657]).
- **[S-4664]** Keys: Up and Down walk the devices, Enter opens one, Delete or E ejects it, Escape lets go.

Needs a manual pass: the widget on a live desktop (a stick plugged in and ejected, a card reader with
and without a card, a disc, the keys).
