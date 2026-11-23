# Drive lists (drives, usbSticks, networkDrives)

The family `drives`, `usbSticks`, `networkDrives` ([S-6901], [S-6911]): a list of the drives of one kind each, every drive
a device item ([S-16630]); it replaced Removable drives (owner, 2026-11-23: "the usb sticks should actually not be a widget
anymore, it should be called list of drives or similar and should show all of the drives. you should be able to filter
them out or rearrange to your liking"; refined the same day: "there should be individual widgets for the drive, bluetooth
device, usb stick, network drive and then for all of those there should also be the list version of it that basically
shows the list of them ... they should still try to share the same structure in the backen"). Its tags are this file's
alone; the root `SPEC.md` holds the device family's ([S-16630]..[S-16640]) and the drives' walk and rules ([S-4650]..
[S-4657]), the drives coming from the host's one walk (`IWidgetHost.Drives`, [S-4655]), Eject the host's launcher's
([S-4661]).

Owning files: everything in this folder: `DrivesRules.cs` (the kinds as data, `DriveListKind`, and which drives a list
shows), `DrivesModule.cs` (one module over the kinds, their descriptors, the JSON context), `DrivesConfig.cs` (the one
config, `DriveListConfig`, a type a kind), `DrivesView.cs` (a `DeviceList` over the walk), `DriveItemMenu.cs`, `Preview/`
(Debug renders: `--state sample|devices|none`), `Localization/Locales/` (its own `DrivesStr`). Tests: `Tests/DrivesTests.cs`.

- **[S-16650]** Three lists, one shape, each kind data (`DriveListKinds`): **Drives** (the PC's own disks and discs:
  fixed, RAM, a disc in its drive), **USB sticks** (removable drives and hot-plug external disks), **Network drives**
  (mapped shares, connected or not); each lists every drive of its kind there by letter, an empty reader left out as
  Explorer hides empty drives (`DriveRules.OfGroup`). Add menu: Devices, each after its one-drive widget. Tests
  `DrivesTests.S16650_`.
- **[S-16651]** **Shown**: a switch a drive of the kind (the checklist); a hidden drive stays hidden while it comes and
  goes (by its root). Tests `DrivesTests.S16651_`.
- **[S-16652]** **Order**: the shown drives dragged up and down; the ordered ones first, every other after them by letter;
  a stick not plugged in keeps its place for when it is back (`DeviceOrder.Reordered`).
- **[S-16653]** **Gap between items** ([S-16634]): each drive stands as a card of its own ([S-16640]).
- **[S-16654]** **Rows**: how many drives a new card stands, three by default; the card shows the drives its box holds at
  their one size, the last cut. Tests `DrivesTests.S16654_`.
- **[S-16655]** Its page: Shown, Order, Gap between items, Rows (`DeviceMenuLines.List`, [S-16636]). A hand-edited root
  that is no drive or is there twice is dropped, loudly. Tests `DrivesTests.S16655_`.
- **[S-16656]** A placed Removable drives widget becomes the USB sticks' list in place (`MergeFrom`, its rows kept; discs,
  which it also showed, are the Drives list's now). Tests `DrivesTests.S16656_`.
- **[S-16657]** A click opens a drive in File Explorer; its menu: Open in Explorer, Eject (a removable drive: the shell's
  own, [S-4661]), Refresh; Delete ejects the one under the keyboard cursor. A list with no drive keeps the face: one item,
  the kind's glyph, "--" and why ("No drive to show", "Nothing plugged in", "No network drive"; WR-10).
- **[S-4661]** Eject is the shell's own verb on the drive root (`ShellExecute` "eject", as in Karpo and
  Explorer): Windows flushes the volume and says how it went in its own notice. Only a drive root is
  accepted, and it runs on the pool. A render never ejects.
- View links: an item is its drive; the pointer or the keyboard cursor on it publishes the drive pointed at, and an item
  takes the hover fill while its drive is pointed at elsewhere on the view ([S-11950]).
- [S-11027], [S-4662], [S-4664], [S-12452]: *(Retired with Removable drives' rows, 2026-11-23: the device item, [S-16631],
  and the list shell's keys, [S-16635].)*

Needs a manual pass: the lists on a live desktop (a stick plugged in and ejected, a card reader with and without a card, a
mapped share disconnected, the gap with a frosted design and Liquid glass).
