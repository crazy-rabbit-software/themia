# Drive

The module `drive` ([S-6911]): one drive's card. Its tags are this file's alone; the root `SPEC.md`
holds the host's sections and what the drive widgets share (the walk and its rules, the SDK's
`DriveRules` and the host's `DriveCatalog`, with their root tags: [S-4650]..[S-4657], the pick of the
named or system drive [S-4670]; the capacity bar, the SDK's `DriveBar`; the ring, the Chart block's
`ChartRing` [S-6266]), the drives coming from the host's one walk
(`IWidgetHost.Drives`, [S-4655]).

Owning files: everything in this folder: `DriveModule.cs` (descriptor, view factory, menu lines,
JSON context), `DriveConfig.cs` (with its styles and its drive), `DriveFace.cs` (the two styles' plans and
the Status row's rule), `DriveView.cs` (the card: the figure, the bar, the facts, Open and Refresh
through its host) and `DriveView.Ring.cs` (the Ring style), `DriveMenu.cs` (its properties menu: the
drive), `Preview/` (Debug renders: `--state local|full|usb|network|disconnected|pending|missing`),
`Localization/Locales/` (its own `DriveStr`, 30 languages, its style names included). Tests:
`Tests/DriveTests.cs` (run by `tests/Themia.Modules.Tests`); its config under the root's drive rule is
the module host's `DriveHostTests`, its styles beside the meters' `IoStylesHostTests`.

- **[S-4671]** The card on the family's blocks (group G6, 2026-10-29): the header names the drive as
  This PC does ([S-4651]) with the widget's glyph, as every data widget's ([S-6710]); the body is the
  used share as a Readout ("55%", [S-6702]) with the space line ("419 GB free of 931 GB", [S-4653])
  as its line under, the Chart block's bar 8 under it in the load's role, and the facts Used, Free,
  Capacity and File system as fact rows (the value primary, [S-6711]); the kind and file system
  ([S-4652]) are in the tooltip. The figure keeps the primary role whatever the load: only the bar
  and the ring take Caution and Critical. A drive without figures is one Status row at the top
  ([S-6714], `DriveFace.Status`): a mapped share Windows cannot reach is a problem ("Disconnected
  network drive", the caution warning; Refresh is on the card menu), a share still answering
  ("Connecting…"), a reader with nothing in it ("No media") and a drive that is gone ("Not
  connected") say so plainly under the kind's glyph. The drive's picture goes: the header names it.
- **[S-4675]** *(Retired in G6: a new Drive card starts with its header like every data widget,
  DECISIONS 7; the body no longer names the drive.)*
- **[S-4672]** The ladder (`DriveFace.Bar`): the share on the step its widest case ("100%") takes in
  the box less the bar (28 at XS and S, 40 at M, Wide and Tall, 68 at L and XL); its line under where
  it fits with the bar; on an expanded width (Wide, L, XL) the figure in a column of two fifths of the
  card and the facts beside it from the top, else the facts 8 under the bar; whole rows only, room
  left empty ([S-666]). The four figures' tile and the 8 DIP bar of a large tile are gone.
- **[S-4673]** *(Retired in G6: the share is the figure at every width, so the narrow "57% used" line went.)*
- **[S-6274]** Styles ([S-6000], `DriveConfig.Style`): **Bar** (the card above) and **Ring**: the used
  share as the Chart block's ring (stroke 2, the bar's role, [S-6717]) with the share in its hole on
  the step its widest case takes in three quarters of the hole's width and half its height, start-
  aligned, as large as its column allows with the space line (Body, secondary) 8 under it while the
  ring stays at least 48 with it; under 48 the share stands alone. On an expanded width the ring
  stands in the two-fifths column and the facts beside it (`DriveFace.Ring`). The options, the menu
  and the drive walk are the same for both.
- **[S-4674]** Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings
  page: Appearance, then behind a divider Drive — the system drive (by name), every drive the walk lists (shares
  included, never waited for), and a chosen drive that is gone as "not connected". The card menu has Open
  in Explorer, Eject for a removable drive, and Refresh.

Needs a manual pass: the card on a live desktop (a USB drive chosen then removed, a mapped share
disconnected, a drive filling past 90 %, both styles).
