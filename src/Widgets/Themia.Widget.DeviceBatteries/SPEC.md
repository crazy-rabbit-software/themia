# DeviceBatteries (deviceBatteries)

The module `deviceBatteries` ([S-6911]): one list of the battery of every connected peripheral
Windows reports one for (issue 146). Its tags are [S-10000]..[S-10099], this file's alone; the root
`SPEC.md` holds the host's battery source (Peripheral batteries (DEVICE-BATTERIES), [S-4470]..[S-4473])
and the kit ([S-7000]..[S-7073]).

Owning files: everything in this folder: `DeviceBatteriesModule.cs` (descriptor, view factory, menu
lines, JSON context), `DeviceBatteriesConfig.cs` (an `IKitConfig`), `DeviceBatteriesRules.cs` (the
fold, the order, the threshold), `DeviceBatteriesSpec.cs` (the kit spec, a device as a kit item, a
row's actions), `DeviceBatteriesView.cs` (a kit view over its `BatterySource` on
`IWidgetHost.DeviceBatteries`), `DeviceBatteriesMenu.cs`, `Preview/` (Debug renders: `--state
sample|empty|failed|loading`; without a state this PC's own devices), `Localization/Locales/` (its
own `DeviceBatteriesStr`, English only for now). Tests: `Tests/DeviceBatteriesTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-10000]** Device batteries lists every connected peripheral Windows reports a battery for
  (the host's one read, [S-4470]: Bluetooth Classic and LE, device containers carrying a level,
  game controllers); a device that reports none is not listed. It needs no setup. With none the
  Status row says "No connected device reports its battery"; a failed read "Couldn't read the
  batteries" (the warning); before the first read "Loading". Add menu: Devices, after Bluetooth.
- **[S-10001]** A kit list ([S-7060]) of the rich look held to two lines: per device a Row, the kind's
  icon as the lead (headphones, mouse, keyboard, phone, controller, pen, watch, any other device),
  its name, its battery as the value (a reading, [S-7072], kept at every width, [S-7073]; the name
  ellipsises first), what it is under the name ("Controller · Charging", charging only where its
  source says so) where the tier gives two lines ([S-6712]), and all of it in the tooltip. Ordered
  lowest battery first, then by name, then by id: two identical reads give one order. At most 30
  rows (a ceiling, not a setting); the rest scrolls; two columns from 720 wide ([S-7031]).
- **[S-10002]** One row per device: readings of one container fold, and a reading without a
  container folds into one of the same name (ignoring case); two containers are two devices whatever
  they are called, and a nameless device never folds. The most trusted source's level wins
  (Bluetooth's property, then the device container, then GATT, then the controller's report,
  `DeviceBatterySource`); the charging state, the kind and the name fill in from the others. A name
  is one line of at most 64 characters; none is "Unknown device".
- **[S-10003]** At or below the low threshold (10, 15, 20, 25 or 30 %; 20 by default, where Windows
  calls a battery low) the level is in the Critical role. A hand-edited threshold off those steps is
  repaired to 20 and logged.
- **[S-10004]** **Show charging devices** (on by default): off, a device whose source says it is
  charging is left out.
- **[S-10005]** A click focuses a row; double-click or Enter opens Windows' Bluetooth & devices page
  (`ms-settings:bluetooth`, through the host's launcher; a preview opens nothing). A row's menu: Open
  Bluetooth & devices, Refresh. The card's Refresh reads Windows again now; the kit header's ring
  turns while a read is out.
- **[S-10006]** Its options are on the properties menu ([S-3719]) after Appearance: **Low battery
  at** (a choice of the five thresholds) and **Show charging devices** (a switch). Nothing else: how
  often Windows is asked is the host's ([S-4472], [S-4473]).

Verified live on the development PC (2026-11-07): a Sony WH-1000XM4 headset over Bluetooth Classic
(its level, 50 %, from `DEVPKEY_Bluetooth_Battery`; listed while connected, gone when it
disconnected). Not verified live (no such device here): Bluetooth LE GATT levels, device containers
with `System.Devices.BatteryLife` (and their charging state), game controllers. A Logitech Unifying
receiver here reports no battery to Windows and is correctly not listed.
- **[S-11028]** A list atom ([S-11008]): its rows are a setting (`Rows`, 1..20, four by default,
  `IKitRows`, `KitMenuLines.Rows` without a lines choice: every row has its second line), its height
  following them (36 + 40 a row + 12, `KitSizing`, 220..520 wide, a new one 300 × 208); more devices
  scroll inside them. Its menu: Low battery at, Show charging devices, Rows.

Needs a manual pass: every size tier on the live desktop (showcase views, [S-1810]), with a headset
connecting and disconnecting while the widget shows (the device watchers, [S-4472]).
