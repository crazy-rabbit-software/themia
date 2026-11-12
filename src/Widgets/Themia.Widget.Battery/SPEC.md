# Battery (reading)

The module `battery` ([S-6911]): the battery's charge, the reading alone (the id stays the reading's; its
history is `batteryHistory`, its facts `batteryFacts`, and the old cards' looks the ready-made groups
"Battery" and "Battery details"). Its tags are this file's alone; the root `SPEC.md` holds what the
system atoms share ([S-11000]..[S-11009]) and the reading's rules, the SDK's `BatteryRules` ([S-1180] the
charge and the no-battery decision, [S-1187] the glyph), the battery and ring faces ([S-6262], [S-6266]).

Owning files: everything in this folder: `BatteryModule.cs` (descriptor, sizing, view factory, JSON
context), `BatteryConfig.cs` (its styles and what it asks the hub, `IReadsSystem`), `BatteryMeter.cs`
(its half of the shared `MeterView`: the pressure, the badge, the drawing per style, the absent
battery), `Localization/Locales/` (its own `BatteryStr`). Tests: `Tests/BatteryTests.cs`, the module
host's `IoStylesHostTests`.

- **[S-6272]** Battery: `BatteryConfig.Style`, **Number** (the charge at 28 led by the battery glyph,
  [S-1187], [S-11001]: one height, 84), **Battery** ([S-6262], [S-11004]: the picture under the charge,
  its body 24..64 tall as the card is) and **Ring** ([S-6266], [S-11003]). No battery draws the same
  reading in every style in the disabled look ([S-1940]): the glyph and the battery half full, the ring
  half swept ([S-1941]), the figure "--", "No battery" as the tooltip. Render `--state unavailable`.
  The old Graph style is the Battery history atom. No tier ladder is left.
- Its options are on the properties menu ([S-3719]): Style and Appearance; nothing of its own.
- **[S-2022]** A click on the card ([S-2000]) opens Windows' Power & battery page
  (`ms-settings:batterysaver`); the module decides it (`BatteryModule.Opens`, through its part's
  `Opens`).

Needs a manual pass: the reading on a laptop (charging, discharging, each style) and on a desktop with
no battery (the disabled reading).
