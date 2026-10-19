# Battery details

The module `batteryDetails` ([S-6911]): what the battery itself reports. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the detail
ladder [S-2400]..[S-2404], the system hub [S-2500]..[S-2513], power events [S-2509]); the sheet's rules
are the SDK's `BatteryDetailRules` with their root tags ([S-2423] the charge and status, [S-2424] the
driver's structures, [S-2425] the status, times and health), read by the host's battery sources.

Owning files: everything in this folder: `BatteryDetailsModule.cs` (descriptor, view factory, JSON
context; no settings rows), `BatteryDetailsConfig.cs` (what it asks the hub, `IReadsSystem`),
`BatteryDetailPart.cs` (its half of the shared `DetailView`, and the debug sample: a laptop's
battery on a desktop, `--state sample`), `Localization/Locales/` (its own `BatteryDetailsStr`, 30
languages). Tests: `Tests/BatteryDetailsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-9800]** Battery details as a module: it reads the hub's charge and battery pack feeds
  (`SystemSource.Battery`, `BatteryPack`) and charts nothing; it has nothing to choose, so no settings
  page ([S-1750]).

Needs a manual pass: the widget on a laptop (charging, discharging, full) and on a desktop with no
battery (a battery's facts, each "--", dimmed, [S-1942]; `--state unavailable` renders it anywhere).
