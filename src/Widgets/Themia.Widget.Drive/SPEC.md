# Drive

The module `drive` ([S-6911]): one drive's card. Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the drive widgets share (the walk and its rules, the SDK's
`DriveRules` and the host's `DriveCatalog`, with their root tags: [S-4650]..[S-4657], the pick of the
named or system drive [S-4670]; the drive's picture and capacity bar, the SDK's `DriveIcon`/`DriveBar`;
the ring face, the SDK's `RingFace` [S-6266]), the drives coming from the host's one walk
(`IWidgetHost.Drives`, [S-4655]).

Owning files: everything in this folder: `DriveModule.cs` (descriptor, view factory, menu lines,
JSON context), `DriveConfig.cs` (with its styles and its drive), `DriveView.cs` (the card: the ladder,
the facts, Open and Refresh through its host) and `DriveView.Ring.cs` (the Ring style), `DriveMenu.cs`
(its properties menu: the drive), `Preview/` (Debug renders: `--state local|full|usb|network|disconnected|pending|missing`),
`Localization/Locales/` (its own `DriveStr`, 30 languages, its style names included). Tests:
`Tests/DriveTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's drive rule is
the module host's `DriveHostTests`, its styles beside the meters' `IoStylesHostTests`.

- **[S-4671]** The body: the picture and the drive's name ([S-4651]; a disconnected share says
  "Disconnected network drive" instead), the used share, the bar and the space line; the kind and
  file system ([S-4652]) are in the tooltip. The header, when shown, carries the name too.
- **[S-4675]** A new Drive card has no header (`WidgetInstance.DefaultHeader`): the body names the drive.
- **[S-4672]** The ladder: the bar and the space line always; the picture row where it fits (24, 32, 48
  DIP by tier); Used, Free, Capacity and File system as four figures on a tile Regular both ways with
  the height for them. A mapped share Windows cannot reach is drawn as Explorer draws it (Karpo §75):
  the cross, "Disconnected network drive", no bar.
- **[S-4673]** Narrow, the space line is "57% used".
- **[S-6274]** Styles ([S-6000], `DriveConfig.Style`): **Bar** (the card above) and **Ring**: the used
  share as a ring in the bar's colours (the shared `RingPart`, [S-6060]) with the share inside it
  ("--" dimmed with no figures), the name (or "Disconnected network drive") and the space line beside
  the ring on a wide strip, under it otherwise, as far as they fit; the ring takes the rest
  (`RingFace`, [S-6266]). The options, the menu and the drive walk are the same for both.
- **[S-4674]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then behind a divider Drive — the system drive (by name), every drive the walk lists (shares
  included, never waited for), and a chosen drive that is gone as "not connected". The card menu has Open
  in Explorer, Eject for a removable drive, and Refresh.

Needs a manual pass: the card on a live desktop (a USB drive chosen then removed, a mapped share
disconnected, a drive filling past 90 %, both styles).
