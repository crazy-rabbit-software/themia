# Battery facts (batteryFacts)

The module `batteryFacts` ([S-6911]), which replaces batteryDetails: what the battery itself reports. Its tags are this file's
alone; the root `SPEC.md` holds the host's sections and what the detail widgets share (the facts atoms
[S-11007], the system hub [S-2500]..[S-2513], power events [S-2509]); the sheet's rules
are the SDK's `BatteryDetailRules` with their root tags ([S-2423] the charge and status, [S-2424] the
driver's structures, [S-2425] the status, times and health), read by the host's battery sources.

Owning files: everything in this folder: `BatteryFactsModule.cs` (descriptor, view factory, JSON
context; no settings rows), `BatteryFactsConfig.cs` (what it asks the hub, `IReadsSystem`),
`BatteryFactsPart.cs` (its half of the shared `DetailView`; a laptop's
battery on a desktop is `--machine sample`, [S-1931]), `Localization/Locales/` (its own `BatteryFactsStr`, 30
languages). Tests: `Tests/BatteryFactsTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-11025]** Battery facts: Status (the old caption), Time left or Full in, Health, Full charge
  capacity, Design capacity, Cycle count, Charge or Discharge rate, Voltage, Temperature, Chemistry
  and Manufacturer; a new one shows the first four. No hero: the charge is the Battery reading's. A
  machine without a battery draws the pick, each "--", dimmed ([S-1942]).
- **[S-9800]** *(Retired: Battery facts reads the hub's charge and battery pack feeds and has its pick as its one setting, [S-11025].)*
- **[S-2023]** A click on the card ([S-2000]) opens Windows' Power & battery page
  (`ms-settings:batterysaver`) (a row's click stays the row's, [S-2004]); the module decides it
  (`BatteryFactsModule.Opens`, through its part's `Opens`).

Needs a manual pass: the widget on a laptop (charging, discharging, full) and on a desktop with no
battery (a battery's facts, each "--", dimmed, [S-1942]; `--state unavailable` renders it anywhere).
