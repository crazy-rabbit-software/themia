# Battery (device item)

The module `battery` ([S-6911]): this PC's battery as a device item on the item base (the root's ITEMS, DEVICES:
[S-16630]..[S-16673], [S-16940]..[S-16957], [S-17180]); its history is the battery charge metric's graph (`batteryCharge`,
[S-14713]), its facts `batteryFacts`, and the ready-made groups "Battery" and "Battery details" put it over them. Its tags
are this file's alone; the root `SPEC.md` holds the SDK's `BatteryRules` ([S-1180] the charge and the no-battery decision,
[S-1187] the glyph) and `BatteryDetailRules` ([S-2425] the state in words, the times to full and to empty).

Owning files: everything in this folder: `BatteryModule.cs` (descriptor, sizing, menu lines, view factory, JSON context),
`BatteryConfig.cs` (what its item shows, what it asks the hub, `IReadsSystem`), `BatteryItems.cs` (the battery mapped onto
the item), `BatteryView.cs` (the face over the hub's battery), `Samples/` (the gallery's sample: a made-up battery on battery), `Preview/` (Debug renders: `--state
sample|charging|low|unavailable`), `Localization/Locales/` (its own `BatteryStr`). Tests: `Tests/BatteryTests.cs`,
`Tests/BatteryOpenTests.cs`, the module host's `SystemStateFixtureTests.S17183_`.

- **[S-17182]** **The battery is a device item** (owner, 2026-11-23: "battery widget should get the same base container as
  teh bluetooth device one and should also have the percentage progress as well"): one item on the device family's face
  (`ItemFace`, [S-16637]) as the Bluetooth device widget's: the battery glyph filled to the nearest tenth, the bolt while
  charging ([S-1187]), its name ("Battery"), its line its state in words and its time ("Charging · Full in 1h 2m" where the
  pack's rate gives one, "On battery · 3h 10m left" from Windows' own estimate else the pack's, "Plugged in, not charging",
  "Fully charged"), its charge at 28 right-aligned and its bar standing at the item's end ([S-17180]): the accent, Critical
  at or below 20 % (`BatteryItems.Low`, Windows' battery saver's default), never Caution for being full ([S-16632]). No
  battery (a desktop, [S-1940]) or a charge Windows will not say has no percentage and no bar, its line saying why ("No
  battery"; "Loading…" until the hub has read, [S-12813]). The card is one item tall (60), 220..520 wide and never narrower
  than its widest line (`ItemLayout.Face`). **Each item shows** is its page's one section ([S-16672]); it has no style
  (the Number, Battery and Ring looks went with the meter). It reads the hub's battery, and the pack only while there is a
  battery (`IReadsSystem`). Tests `BatteryTests.S17182_`.
- **[S-17183]** **A placed battery keeps its card**: the kind's id stays `battery`; a card that still holds its old `style`
  loads as the device item with that key dropped, one repair line ([S-1050]: no migration). Test
  `SystemStateFixtureTests.S17183_`.
- **[S-2022]** A click on the card ([S-2000]) opens Windows' Power & battery page (`ms-settings:batterysaver`); the module
  decides it (`BatteryModule.Opens`); the card lights as the one item.

Retired with the meter: [S-6272] (the Number, Battery and Ring styles).

Needs a manual pass: the item on a laptop (charging with a rate, discharging, full on mains) and on a desktop with no
battery.
