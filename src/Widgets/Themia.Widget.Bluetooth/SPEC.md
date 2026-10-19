# Bluetooth

The module `bluetooth` ([S-6911]): the paired Bluetooth devices, which are connected, their battery
and signal; port of `main`'s Bluetooth (`widgets/system/bluetooth`, [S-01]..[S-16]) plus the round-3
ladder. Its tags are this file's alone; the root `SPEC.md` holds the host's sections (the shared
widget settings [S-3790], the ladder rules [S-660]..[S-666]).

Owning files: everything in this folder: `BluetoothModule.cs` (descriptor, view factory, menu
lines, JSON context), `BluetoothConfig.cs`, `BluetoothInventory.cs` (Windows' device, radio, GATT and
advertisement APIs: the widget's own reading, no host service), `BluetoothRules.cs`,
`BluetoothLayout.cs`, `BluetoothView.cs`, `BluetoothMenu.cs` (its properties-menu lines), `Preview/` (Debug
renders), `Localization/Locales/` (its own `BluetoothStr`, 30 languages). Tests: `Tests/` (run by
`tests/Themia.Modules.Tests`).

The rules a list is drawn by (`BluetoothRules`; written down at the move from the tests that pinned
them, `BluetoothRulesTests`, which carried these tags with no SPEC line):

- **[S-240]** A device's icon comes from its name (headphones and buds are audio, then mouse,
  keyboard, phone, controller, pen, watch; anything else the generic device), ignoring case; no name
  is the generic device.
- **[S-241]** A device that advertises no name still gets a readable row ("Unknown device"); a name
  off the radio is collapsed to one line and capped at 64 characters.
- **[S-242]** A battery the device will not report (none, 255, out of 0–100) is no column at all;
  the connection state is carried by words ("Connected", "Paired") as well as by the icon's colour.
- **[S-243]** The same device seen by both radios (Classic and LE, one name) is one row keeping the
  richer answer of each (the connection of one, the battery of the other); nameless devices are never
  folded into each other. Connected devices come first, the rest alphabetical, the same order on two
  identical polls; Connected only drops the rest; the row count is a ceiling.
- **[S-244]** Every empty list says which it is: no radio, the radio off, no paired devices,
  reading, no access to the radios, or a failed read.

- **[S-1254]** Bluetooth ladder (`BluetoothLayout`): the list takes every whole row the tile has
  (snapped, the rest scrolls); `BluetoothConfig.MaxDevices` (30) is a ceiling, not a target and not a setting ([S-3794]). Battery %
  from a 180 px column ([S-3253]); status word from a 260 px column; signal from a 400 px column,
  where a two-line row also keeps battery and signal in columns (`FactColumns`); "2 connected" in the
  header from Expanded width; two columns at Huge width when one cannot hold the list.
- **[S-1255]** Regular×Expanded and up: two-line rows — name, then kind · connection · battery ·
  signal (each only when enabled and reported; a wide row keeps battery and signal in columns).
- **[S-1256]** `ShowSignal` (default off): RSSI from the association endpoint's
  `System.Devices.Aep.SignalStrength`, plus a passive 3 s LE advertisement scan per refresh matched by
  device address. Shown as "-67 dBm"; no reading or an implausible one (≥ 0, < -127) is no column
  (orig. [S-16]).
- **[S-1257]** `ShowIcon` and `ShowConnection` (default on) are the original's `type` and
  `connection` metrics; the name is always shown (orig. [S-14]/[S-15]).
- **[S-1258]** *(Retired: the debug widget gallery and its fixtures are gone, [S-1863].)*
- **[S-3255]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Connected only** (paired but not connected
  left out), a divider, and what a row shows: **Device icon**, **Connection** ([S-1257]; not with
  Connected only, [S-3256]), **Battery** ([S-3252]: slower, it connects to each device) and
  **Signal strength** ([S-1256]: a short scan), all switches. (Tests `BluetoothMenuTests`.)
- **[S-3256]** With **Connected only** on, every row is a connected device, so the connection word
  and the second line's state would say "Connected" on every row: they are not drawn
  (`BluetoothConfig.DrawsConnection`; the icon's colour still says it, [S-3961]) and the
  **Connection** switch is not offered (`ShownSettings`, [S-6070]). Its value is kept: with every
  paired device listed again the switch is back as it was (user request 2026-10-19: no option that
  changes nothing).
- **[S-1259]** Context menu "Refresh" enumerates now (orig. [S-09]).
- **[S-3252]** `ShowBattery` (default on): each **connected** device's battery, refreshed with the
  list on its interval. First the level Windows itself holds (`DEVPKEY_Bluetooth_Battery`,
  `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`, a byte): the paired endpoints are enumerated with
  `System.Devices.Aep.ContainerId`, and one `DeviceInformation` query over the device nodes of those
  containers reads the property (the highest valid reading per container; hands-free headsets carry
  it on a service node). This is what was missing: only a GATT read was tried, which Classic headsets
  never answer. Then, within the 8 s budget, a GATT Battery Service read for a connected LE device the
  property left without one. A disconnected device shows none (the property keeps a stale value);
  255, out-of-range or a non-integer value is no reading. No reading is no column and nothing in the
  second line, never "–" or 0%. A failed property query is logged and the list still shows.
- **[S-3253]** The battery % rides on every one-line row from a 180 px column (was 260): the name
  ellipsises first. Narrower (XS) only the tooltip carries it.
- **[S-3254]** 20% and below is low: the battery number takes the theme's critical fill
  (`SystemFillColorCriticalBrush`), as the tray does (`BluetoothRules.IsLowBattery`).
  Debug renders: `--widget-png Bluetooth <out> --state devices` draws a fixed set of devices.
- **[S-3961]** No battery bar and no connection dot: the battery is its percentage as text, and the
  state is the device icon's colour (primary connected, secondary paired), the status word where the
  row has room and the second line. `ShowConnection` governs the word and the second line's state.
  No count badge in the header either: the connected count is the "2 connected" words where
  [S-1254] gives the header room, and nothing on smaller tiles.
- **[S-1569]** *(Retired: no Bluetooth detail panel. A short list on a large tile stays the list;
  the rows' second line and tooltip carry each device's facts.)*
- **[S-1988]** *(Retired with the detail panel: the Bluetooth list takes no lease keys.)*
- **[S-3794]** Bluetooth: no device-count and no refresh setting; the list holds at most 30 devices
  and fills the tile.

Not ported: Bluetooth's 1–30 s refresh choices (kept 5–300 s: enumeration and GATT reads take
seconds); per-metric `visibleMetrics` ordering (fixed ladder order).

Needs a manual pass: a live list with a Classic headset and an LE device (battery and signal), the
radio off and no radio, at every tier on the live desktop (showcase views, [S-1810]).
