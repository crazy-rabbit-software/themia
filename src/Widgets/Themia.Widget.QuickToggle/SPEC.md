# The quick setting (QUICK-TOGGLE)

The module project `Themia.Widget.QuickToggle`: one kind, `quickToggle` ("Quick setting"), whose config targets one of the
thirteen quick settings (owner, 2026-11-23: *"the toggle buttons should be possible to be even smaller, you should be able
to select what they toggle, this means that they should actually be merged into one widget and not be multiple ones. in
the gallery and everywhere else they should still appear as if they were multiple widgets though. but in the backend it is
the same one."*; earlier, *"Toggles should stay apart"*: each stays its own card). What every target shares is the host's
and the SDK's (root `SPEC.md`, Quick-setting toggles: [S-3900]..[S-3921]; the view `QuickToggleView` on the family's Tile
block ([S-6715]), the rules `QuickToggleRules` with each target's row (`QuickToggleTarget`), `ToggleGate`, the style
`QuickToggleStyle` and the config `QuickToggleConfig` in the SDK, its source behind `IWidgetHost.Toggles` in the host); the
presets it is offered as are the registry's ([S-16680]..[S-16687]).

Owning files: everything in this folder: `QuickToggleModule.cs` (the targets as data, `QuickToggleTargets`: each one's
key, add-page name, caption, search words and place; its presets and the folds of the old kinds; the module and its
Toggles line; the JSON context), `Localization/Locales/` (its own `QuickToggleStr`). Tests: `Tests/QuickToggleTests.cs`
(run by `tests/Themia.Modules.Tests`).

- **[S-16700]** **One kind, its target a setting.** `quickToggle`'s config holds `Target` (a `QuickToggle`, written by its
  camelCase name: `wifi`, `airplaneMode`; Wi-Fi at first; a name this build does not know is repaired to Wi-Fi and said) and
  `Style` (Tile at first, [S-3916]), both kept by `Clone`. Its page's own line is **Toggles**, a choice of the thirteen by
  their names on the tile ("Wi-Fi", "Do not disturb"), wearing the target's glyph; a pick switches the card in place: the
  view lets go of the old setting's source and follows the new one ([S-3907]). It never needs setting up and never has a
  header ([S-3901]); its descriptor declares it a one-readout widget ([S-6737]).
- **[S-16701]** **Each target's facts are one row.** The SDK's `QuickToggleRules.Targets` holds each setting's name on the
  tile, its glyph on and off ([S-3902]: sound and microphone struck through while muted) and the reason it is most often
  unavailable for (a preview's true sentence); the module's `QuickToggleTargets` adds its add-page words and place. Nothing
  else switches on a target.
- **[S-16702]** **The checks draw every target.** Its preview states are the toggle's (on, off, unavailable, unknown, hover,
  pressed, refused, on Wi-Fi) and one per other target, named by its key (`bluetooth` … `gameMode`: that setting on), so the
  item and gap checks and the audit see every glyph and name; any target in any state is `--config {"target":"…"}`.
- **[S-16703]** **It is offered as thirteen widgets.** Each target is a preset ([S-16680]): the add page's Quick settings
  subject lists them run by run as the thirteen kinds stood (Wi-Fi, Bluetooth, Airplane mode | Sound, Microphone, Spatial
  sound, Mono audio | Night light, Color filters, Rotation lock | Energy saver, Do not disturb, Game mode), each named "<name>
  on/off" with its caption, glyph and search words, so it never reads as another widget ("Bluetooth on/off" beside the
  Bluetooth devices widget); the gallery's Quick settings tab shows each at its size and its least ([S-16562]); the search
  finds each on its own words; the ready-made Quick settings group places `quickToggle/wifi`, `/bluetooth`,
  `/doNotDisturb`, `/nightLight` and the first-run Home its six. The kind itself has no tile.
- **[S-16704]** **A placed old toggle becomes the quick setting of its target.** Each removed kind (`wifiToggle` …
  `gameModeToggle`, the target's key and "Toggle") is folded on load ([S-16690]): the card keeps its id, box, style and look
  and becomes `quickToggle` with that target, one logged line each.

The thirteen kinds it replaced, each its own module and config until 2026-11-23 (owner, that day: *"they should actually be
merged into one widget and not be multiple ones … but in the backend it is the same one"*):

- **[S-8200]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `wifiToggle` is the quick setting targeting wifi, [S-16704].)*
- **[S-8300]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `bluetoothToggle` is the quick setting targeting bluetooth, [S-16704].)*
- **[S-8400]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `airplaneModeToggle` is the quick setting targeting airplaneMode, [S-16704].)*
- **[S-8900]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `soundToggle` is the quick setting targeting sound, [S-16704].)*
- **[S-9000]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `microphoneToggle` is the quick setting targeting microphone, [S-16704].)*
- **[S-9100]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `spatialSoundToggle` is the quick setting targeting spatialSound, [S-16704].)*
- **[S-9200]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `monoAudioToggle` is the quick setting targeting monoAudio, [S-16704].)*
- **[S-8600]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `nightLightToggle` is the quick setting targeting nightLight, [S-16704].)*
- **[S-9300]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `colorFiltersToggle` is the quick setting targeting colorFilters, [S-16704].)*
- **[S-8700]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `rotationLockToggle` is the quick setting targeting rotationLock, [S-16704].)*
- **[S-8500]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `energySaverToggle` is the quick setting targeting energySaver, [S-16704].)*
- **[S-8800]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `doNotDisturbToggle` is the quick setting targeting doNotDisturb, [S-16704].)*
- **[S-9400]** *(Retired 2026-11-23 by [S-16700]: "they should actually be merged into one widget and not be multiple ones"; `gameModeToggle` is the quick setting targeting gameMode, [S-16704].)*

Needs a manual pass: switching each setting itself on a real desktop (never in a render or a test, [S-1251]).
