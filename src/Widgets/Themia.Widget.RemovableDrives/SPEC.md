# Removable drives

The module `removableDrives` ([S-6911]): a row per removable device plugged in. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the drive widgets share (the walk
and its rules, the SDK's `DriveRules` and the host's `DriveCatalog`, with their root tags:
[S-4650]..[S-4657], which devices are removable [S-4660]; the drive's picture, the SDK's `DriveIcon`),
the drives coming from the host's one walk (`IWidgetHost.Drives`, [S-4655]) and Eject being the
host's launcher's ([S-4661]'s verb).

Owning files: everything in this folder: `RemovableDrivesModule.cs` (descriptor, view factory, JSON
context; no settings rows, [S-1750]), `RemovableDrivesConfig.cs`, `RemovableDrivesView.cs` (the rows,
Eject and the keys), `Preview/` (Debug renders: `--state one|devices|none`), `Localization/Locales/`
(its own `RemovableDrivesStr`, 30 languages).

- **[S-4661]** Eject is the shell's own verb on the drive root (`ShellExecute` "eject", as in Karpo and
  Explorer): Windows flushes the volume and says how it went in its own notice. Only a drive root is
  accepted, and it runs on the pool. A render never ejects.
- **[S-4662]** A row: the picture, the name and Eject (worded from 260 DIP, else the stick glyph); from
  Regular height and width also what it holds and a thin bar. A double click opens the device.
- **[S-4664]** Keys: Up and Down walk the devices, Enter opens one, Delete or E ejects it, Escape lets go.

Needs a manual pass: the widget on a live desktop (a stick plugged in and ejected, a card reader with
and without a card, a disc, the keys).
