# Night light on/off (QUICK-TOGGLES)

The module `nightLightToggle` ([S-6911]): one of the thirteen quick-setting toggles, one setting each. What the
thirteen share is the host's and the SDK's (root `SPEC.md`, Quick-setting toggles: [S-3900]..[S-3921];
the view `QuickToggleView`, the rules `QuickToggleRules`, `ToggleGate`, `ToggleFace`, `SwitchFace` and
the style `QuickToggleStyle` in the SDK, the toggle's name on the tile in the SDK's table, its source
behind `IWidgetHost.Toggles` in the host). This file's tag is its own.

Owning files: everything in this folder: `NightLightToggleModule.cs` (descriptor, view factory, JSON context),
`NightLightToggleConfig.cs`, `Localization/Locales/` (its own `NightLightToggleStr`, 30 languages: its picker label and
description). Tests: `Tests/NightLightToggleTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-8600]** `nightLightToggle` stands for `QuickToggle.NightLight` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 3 at place 1 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Night light on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`.

Needs a manual pass: switching the setting itself on a real desktop (never in a render or a test, [S-1251]).
