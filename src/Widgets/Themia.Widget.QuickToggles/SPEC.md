# Quick-setting toggles (QUICK-TOGGLES)

The family project `Themia.Widget.QuickToggles` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the thirteen quick-setting
toggles, one setting each, a kind each with its own id and config. What the thirteen share is the host's and the SDK's
(root `SPEC.md`, Quick-setting toggles: [S-3900]..[S-3921]; the view `QuickToggleView` on the family's Tile block
([S-6715]), the rules `QuickToggleRules` and `ToggleGate`, the style `QuickToggleStyle` in the SDK, the toggle's name on
the tile in the SDK's table, its source behind `IWidgetHost.Toggles` in the host). Each kind's tag is this file's.

Owning files: everything in this folder: `QuickTogglesKinds.cs` (the kinds as data, their modules, the JSON context),
`QuickTogglesConfigs.cs` (a config each), `Localization/Locales/` (its own `QuickTogglesStr`: each kind's picker label
and description). Tests: `Tests/QuickTogglesTests.cs` (run by `tests/Themia.Modules.Tests`).

- **[S-8200]** `wifiToggle` stands for `QuickToggle.Wifi` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 1 at place 1 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Wi-Fi on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8300]** `bluetoothToggle` stands for `QuickToggle.Bluetooth` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 1 at place 2 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Bluetooth on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8400]** `airplaneModeToggle` stands for `QuickToggle.AirplaneMode` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 1 at place 3 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Airplane mode on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8900]** `soundToggle` stands for `QuickToggle.Sound` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 2 at place 1 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Sound on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-9000]** `microphoneToggle` stands for `QuickToggle.Microphone` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 2 at place 2 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Microphone on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-9100]** `spatialSoundToggle` stands for `QuickToggle.SpatialSound` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 2 at place 3 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Spatial sound on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-9200]** `monoAudioToggle` stands for `QuickToggle.MonoAudio` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 2 at place 4 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Mono audio on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8600]** `nightLightToggle` stands for `QuickToggle.NightLight` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 3 at place 1 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Night light on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-9300]** `colorFiltersToggle` stands for `QuickToggle.ColorFilters` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 3 at place 2 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Color filters on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8700]** `rotationLockToggle` stands for `QuickToggle.RotationLock` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 3 at place 3 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Rotation lock on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8500]** `energySaverToggle` stands for `QuickToggle.EnergySaver` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 4 at place 1 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Energy saver on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8800]** `doNotDisturbToggle` stands for `QuickToggle.DoNotDisturb` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 4 at place 2 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Do not disturb on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-9400]** `gameModeToggle` stands for `QuickToggle.GameMode` (its config's `Toggle`, [S-6906]): its widget
  is in the add menu's Quick settings in run 4 at place 3 ([S-3511]), 120×120, never with a
  header ([S-3901]), named "Game mode on/off" in the picker; its one setting is the toggles' shared style
  ([S-3916]), Tile at first and kept by `Clone`. Its descriptor declares it a
  one-readout widget ([S-6737]).

Needs a manual pass: switching each setting itself on a real desktop (never in a render or a test, [S-1251]).
