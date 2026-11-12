# Disk (reading)

The module `disk` ([S-6911]): a drive's activity, reads and writes per second, the reading alone (the id
stays the reading's; its history is `diskHistory`, and the old card's look the ready-made group
"Disk"). Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the meters share (the meter
framework [S-1120]..[S-1126], [S-1762], [S-1167], the readouts' rule [S-6330], the system hub [S-2500]..[S-2513],
the header the meters start with [S-700]); the disk's rules are the SDK's `DiskRules` with their root
tags ([S-1160] the drive's root, [S-1161] the header's title and no drive in the body, [S-1162] the
counters, [S-1163] all drives, [S-1820] throughput never space, [S-1821] the doubling top, [S-1822] the
scope, [S-6265] and [S-6271] the bars), and the drives come from the host's walk ([S-4655]).

Owning files: everything in this folder: `DiskModule.cs` (descriptor, view factory, menu lines,
JSON context), `DiskConfig.cs` (with its styles, its drive and scope, and what it asks the hub,
`IReadsSystem`), `DiskMeter.cs` (its half of the shared `MeterView` on the SDK's `DiskPart`, which the
disk history shares: the rate, the bars' window, the header's title from the host's drives, Open in
Explorer through the host's launcher), `DiskMenu.cs` (its properties menu: the drive, the SDK's
`DiskMenuLines`),
`Localization/Locales/` (its own `DiskStr`, 30 languages, its style names included). Tests: its
config under the host's disk rules is the module host's `DiskHostTests`; its styles beside the other
I/O meters' are `IoStylesHostTests`.

- **[S-1166]** The card's "Open C: in Explorer" opens the drive through `ShellLauncher`; all drives
  offers no such command.
- **[S-1168]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then Drive (system drive / all drives / each drive Windows lists, a removed one
  offered as "not connected", the one held with the dot). Its styles (`DiskStyle`): **Number** (the rate
  alone, [S-11001]) and **Bars** (the rate over reads and writes as two rows, [S-6271]: one height, 148,
  240..480 wide); the old Graph style is the Disk history atom. No tier ladder is left.
- **[S-2016]** A click on the card ([S-2000]) opens Task Manager (`CardOpen.TaskManager`, the system
  folder's `Taskmgr.exe`); the module decides it (`DiskModule.Opens`, through its part's `Opens`).
- View links: a reading of one drive is that drive as a card: the pointer on it publishes the drive and the card
  takes the hover fill while it is pointed at elsewhere; every drive together takes no part ([S-11950], [S-11953]).

Needs a manual pass: the meter on a live desktop (a copy moving the chart, a USB drive chosen then
removed, all drives, both styles, the header switched off).
