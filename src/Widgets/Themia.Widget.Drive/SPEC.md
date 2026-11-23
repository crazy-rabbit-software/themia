# Drive (reading)

The module `drive` ([S-6911]): one drive's used share, the reading alone (the id stays the reading's;
its facts are `driveFacts`, and the old card's look the ready-made group "Drive"). Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the drive widgets share (the walk and its rules, the SDK's
`DriveRules` and the host's `DriveCatalog`, with their root tags: [S-4650]..[S-4657], the pick of the
named or system drive [S-4670]; the capacity bar, the SDK's `DriveBar`; the ring, the Chart block's
`ChartRing` [S-6266]), the drives coming from the host's one walk
(`IWidgetHost.Drives`, [S-4655]).

Owning files: everything in this folder: `DriveModule.cs` (descriptor, view factory, menu lines,
JSON context), `DriveConfig.cs` (with its styles and its drive), `DriveFace.cs` (the two styles' sizes and
the Status row's rule), `DriveView.cs` (the card: the figure, the bar, Open and Refresh
through its host) and `DriveView.Ring.cs` (the Ring style), `DriveMenu.cs` (its properties menu: the
drive), `Preview/` (Debug renders: `--state sample|full|usb|network|disconnected|pending|missing`),
`Localization/Locales/` (its own `DriveStr`, 30 languages, its style names included). Tests:
`Tests/DriveTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's drive rule is
the module host's `DriveHostTests`, its styles beside the meters' `IoStylesHostTests`.

- **[S-4671]** The reading on the family's blocks: a Header widget over it names the drive
  ([S-12481]: no card has a header), the drive's name on the reading's hover; the body is the used share at 28 ("55%", [S-11001]) with the space
  line ("419 GB free of 931 GB", [S-4653]) as its line under and the Chart block's bar 8 under it in the
  load's role; the kind and file system ([S-4652]) are in the tooltip. The figure keeps the primary role
  whatever the load: only the bar and the ring take Caution and Critical. A drive without figures is
  one Status row at the top ([S-6714], `DriveFace.Status`): a mapped share Windows cannot reach is a
  problem ("Disconnected network drive", the caution warning; Refresh is on the card menu), a share
  still answering ("Connecting…"), a reader with nothing in it ("No media") and a drive that is gone
  ("Not connected") say so plainly under the kind's glyph. Its facts (Used, Free, Capacity, File
  system) are the Drive facts atom's.
- **[S-4675]** *(Retired in G6: a new Drive card starts with its header like every data widget,
  DECISIONS 7; the body no longer names the drive.)*
- **[S-4672]** The Bar style's sizing (`DriveFace.Sizing`): one height (88: the inset, the share and
  its line, 8, the bar's track, the inset), 160..320 wide, a new one 200; a wider card is a longer bar.
  No tier ladder is left.
- **[S-4673]** *(Retired in G6: the share is the figure at every width, so the narrow "57% used" line went.)*
- **[S-6274]** Styles ([S-6000], `DriveConfig.Style`): **Bar** (above) and **Ring**: the used share as
  the Chart block's ring (stroke 2, the bar's role, [S-6717]) with the share at 28 in its hole, as large
  as the box takes with the space line (Body, secondary) 8 under it ([S-11003]); 160..320 wide, a new one
  200 × 180. The options, the menu and the drive walk are the same for both.
- **[S-15749]** **The share and its line are one block at every size, down to the line at 9** (docs/WIDGET-RULES.md WR-7:
  "A reading and its caption stay together and scale up"; WR-10: a drive without figures says why; WR-14: "sized down to
  where its smallest text is still read (9 DIP)"). Both styles scale as one block in the card's inset (`ReadingFit`), and
  no part gives way: the least card is the content box at the line's 9 (Caption under the bar, Body under the ring) in
  the inset, which does not scale (`DriveFace.Scaled`). The bar style had dropped its space line (and a disconnected
  share's reason) under 88 tall, down to 54 × 40, its icon flush with the card's top. The ring style is planned at the
  natural ring (104, `DriveFace.PlannedRing`) with its share at 28 and its line 8 under it, and scales as one, centred
  where one axis stops it; the ring had grown alone while the share and its line kept their size (and its line was cut at
  its least). Tests `DriveTests.S15749_`.
- **[S-15759]** **The bar's share stands centred over its bar, never cut** (owner, 2026-11-20, of a reading that fills one
  axis: "Centred is fine"; WR-10): the icon and the share stand centred across the bar that spans the card (all the
  room had stood on their right at 350 × 72), and the bar style is never narrower than its widest line (its space line's
  forms and every reason a drive has no figures, `DriveFace.WidestLine`) beside its icon at the line's 9: "Disconnected
  network drive" had been cut mid-word at 156 × 72. Test `DriveTests.S15749_`.
- **[S-15750]** **A filling drive never moves its face** (WR-15; WR-10): the share is held to its widest ("100%",
  `ReadoutBlock.Hold`) and the space line stands in the box of its widest forms in the language (`DriveFace.SpaceTemplates`:
  every unit pair, figures as 8s), so the block keeps its size and place as the free space changes. A drive that is not
  there keeps its face with its kind's glyph (the shell gives no picture of it; a share or a stick not there its kind's stock picture). Tests `DriveTests.S15750_`.
- **[S-4674]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then behind a divider Drive — the system drive (by name), every drive the walk lists (shares
  included, never waited for), and a chosen drive that is gone as "not connected". The card menu has Open
  in Explorer, Eject for a removable drive, and Refresh.
- **[S-16645]** **On the device design** ([S-16630], owner, 2026-11-23: "create a base design that all of those widgets
  can use. it should look very similar to how todays drive widget looks like"): the drive is the device family's item
  (`DeviceItems.Drive`) on its one face (`DeviceFace`, [S-16637]: the bar style; the ring keeps its own parts over the same
  item): the same icon, reading, line, bar and states as every device widget; its sizing the face's (`DeviceLayout.Face`).
- **[S-16646]** **USB stick and Network drive** (`usbStick`, `networkDrive`; owner, 2026-11-23: "there should be individual
  widgets for the drive, bluetooth device, usb stick, network drive"): this module is a family of three one-drive widgets,
  one view, one config shape (`OneDriveConfig`: the drive, the style) and one module over its kinds as data
  (`OneDriveKind`): the Drive any drive (the system drive by default), the USB stick a removable drive or external disk,
  the Network drive a mapped share. A kind's widget shows the drive named while it is there (not there: its face, "Not
  connected"), else the first of its kind there by letter, else its face and why ("Nothing plugged in", "No network
  drive"; WR-10) (`DriveKindRules.Pick`). Its page: the drive (the first one there, or one of its kind by name). Its
  sample is a stick or a share. Bar and Ring, Open, Eject and Refresh as the Drive's.
- **[S-2032]** A click on the card ([S-2000]) opens the drive's root in File Explorer: its own
  drive, or the system drive when it shows that (`DriveModule.Opens`).
- View links: the card is its drive; the pointer on it publishes the drive pointed at, and the whole card takes the
  hover fill, over its whole box, under the pointer and while its drive is pointed at elsewhere on the view ([S-11950],
  [S-11962], [S-11963]).

Needs a manual pass: the card on a live desktop (a USB drive chosen then removed, a mapped share
disconnected, a drive filling past 90 %, both styles).
