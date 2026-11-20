# Battery facts (BATTERY-FACTS)

The family project `Themia.Widget.BatteryFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): what the battery itself reports, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the sheet's rules, the SDK's `BatteryDetailRules` ([S-2423] the charge and status, [S-2424] the driver's structures, [S-2425] the times and health), and power events ([S-2509]).

Owning files: everything in this folder: `BatteryKinds.cs`, `BatteryMetric.cs` (module and config bases, the part over the
hub's charge and battery pack feeds, the JSON context), `Localization/Locales/` (its own `BatteryFactsStr`). Tests:
`Tests/BatteryMetricTests.cs`.

- **[S-11025]** *(Retired by [S-14660]: no card of picked facts.)*
- **[S-9800]** *(Retired: battery facts read the hub's charge and battery pack feeds, [S-14660].)*
- **[S-14660]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `batteryStatus` "Battery status", `batteryTime` "Battery time", `batteryHealth` "Battery health", `batteryFullCapacity` "Full charge capacity", `batteryDesignCapacity` "Design capacity", `batteryCycles` "Cycle count", `batteryRate` "Charge rate", `batteryVoltage` "Battery voltage", `batteryTemperature` "Battery temperature", `batteryChemistry` "Battery chemistry", `batteryManufacturer` "Battery maker", the family **Battery facts** under Devices &
  power (`AddFolderId.BatteryFacts`). Each draws its fact of the battery's sheet, named as the sheet names it ("Full in"
  while charging, "Charge rate" or "Discharge rate"); a machine without a battery keeps the row, "--" dimmed, why its
  tooltip ([S-1942]). Trends ([S-14603]): battery time, charge rate, battery voltage and battery temperature an arrow from the last two readings the card saw (no history kept); battery status, battery health, full charge capacity, design capacity, cycle count, battery chemistry and battery maker none (a text, a constant or a time: no Trend line). 
- **[S-2023]** A click on the card ([S-2000]) opens Windows' Power & battery page (`ms-settings:batterysaver`); every kind declares it (`BatteryKinds`, `MetricKind.Opens`, [S-14706]), as do the card menu's first line and Enter ([S-14707]).

Needs a manual pass: the cards on a laptop (charging, discharging, full) and on a desktop (each "--", dimmed; `--state unavailable` renders it anywhere).
