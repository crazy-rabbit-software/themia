# Disk (meter)

The module `disk` ([S-6911]): a drive's activity, reads and writes per second. Its tags are this
file's alone; the root `SPEC.md` holds the host's sections and what the meters share (the meter
framework [S-1120]..[S-1126], [S-1762], [S-1167], the glides [S-6323], the system hub [S-2500]..[S-2513],
the header the meters start with [S-700]); the disk's rules are the SDK's `DiskRules` with their root
tags ([S-1160] the drive's root, [S-1161] the header's title and no drive in the body, [S-1162] the
counters, [S-1163] all drives, [S-1820] throughput never space, [S-1821] the doubling top, [S-1822] the
scope, [S-6265] and [S-6271] the bars), and the drives come from the host's walk ([S-4655]).

Owning files: everything in this folder: `DiskModule.cs` (descriptor, view factory, menu lines,
JSON context), `DiskConfig.cs` (with its styles, its drive and scope, and what it asks the hub,
`IReadsSystem`), `DiskMeter.cs` (its half of the shared `MeterView`: the rate, the doubling scale,
the bars' window, the header's title from the host's drives, Open in Explorer through the host's
launcher), `DiskMenu.cs` (its properties menu: the drive, then the span every meter has),
`Localization/Locales/` (its own `DiskStr`, 30 languages, its style names included). Tests: its
config under the host's disk rules is the module host's `DiskHostTests`; its styles beside the other
I/O meters' are `IoStylesHostTests`.

- **[S-1166]** The card's "Open C: in Explorer" opens the drive through `ShellLauncher`; all drives
  offers no such command.
- **[S-1168]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then Drive (system drive / all drives / each drive Windows lists, a removed one
  offered as "not connected", the one held with the dot); under Style in the Graph style the Chart span
  ([S-1122]).

Needs a manual pass: the meter on a live desktop (a copy moving the chart, a USB drive chosen then
removed, all drives, both styles, the header switched off).
