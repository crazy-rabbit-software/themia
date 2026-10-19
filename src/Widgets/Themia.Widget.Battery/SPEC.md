# Battery (meter)

The module `battery` ([S-6911]): the battery's charge. Its tags are this file's alone; the root
`SPEC.md` holds the host's sections and what the meters share (the meter framework [S-1120]..[S-1123],
[S-1762], the glides [S-6323], the system hub [S-2500]..[S-2513]); the reading's rules are the SDK's
`BatteryRules` with their root tags ([S-1180] the charge and the no-battery decision, [S-1187] the
glyph), the battery and ring faces the SDK's (`BatteryShape`, `RingFace`: [S-6261], [S-6262], [S-6266]).

Owning files: everything in this folder: `BatteryModule.cs` (descriptor, view factory, menu lines,
JSON context), `BatteryConfig.cs` (with its styles and what it asks the hub, `IReadsSystem`),
`BatteryMeter.cs` (its half of the shared `MeterView`: the pressure, the badge, the look per style,
the absent battery), `Localization/Locales/` (its own `BatteryStr`, 30 languages, its style names included). Tests: its
styles side by side with the other I/O meters' are the module host's `IoStylesHostTests`.

- **[S-6272]** Battery: `BatteryConfig.Style`, **Graph** ([S-1187]), **Battery** ([S-6262]) and
  **Ring** ([S-6266]). No battery is the empty state in every style ([S-82a]).
- Its options are on the properties menu ([S-3719], user request 2026-10-19), no settings page:
  Appearance, and under Style in the Graph style the Chart span every meter has ([S-1122]).

Needs a manual pass: the meter on a laptop (charging, discharging, each style) and on a desktop with
no battery (the empty state).
