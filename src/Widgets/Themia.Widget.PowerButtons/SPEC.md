# Power buttons (R7-POWER)

The family project `Themia.Widget.PowerButtons` ([S-6901], `[assembly: ThemiaWidgetFamily]`): the seven power buttons,
one action each, a kind each with its own id and config. What the seven share is the host's and the SDK's (root
`SPEC.md`, Power and Bluetooth: one widget per action [S-1910], the button [S-1911]..[S-1916], [S-1919], the Win32 calls
[S-1243], previews [S-1251]; the view `PowerButtonView` (the family's Tile block, ringed, [S-6736]) and the rules
`PowerButton` and `PowerActionRules` in the SDK, the backend on `IWidgetHost.Power`). Each kind's tag is this file's.

Owning files: everything in this folder: `PowerButtonsKinds.cs` (the kinds as data, their modules, the JSON context),
`PowerButtonsConfigs.cs` (a config each), `Localization/Locales/` (its own `PowerButtonsStr`: each kind's description,
and its name copied from the host's table, which the shared view also reads). Tests: `Tests/PowerButtonsTests.cs` (run by
`tests/Themia.Modules.Tests`).

- **[S-7400]** `shutdown` stands for `PowerAction.Shutdown` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 1 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it arms on the first click and runs on the second ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-7500]** `restart` stands for `PowerAction.Restart` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 2 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it arms on the first click and runs on the second ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-7600]** `sleep` stands for `PowerAction.Sleep` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 3 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it runs on one click ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-7700]** `hibernate` stands for `PowerAction.Hibernate` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 4 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it arms on the first click and runs on the second ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-7800]** `lock` stands for `PowerAction.Lock` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 5 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it runs on one click ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-7900]** `signOut` stands for `PowerAction.SignOut` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 6 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it arms on the first click and runs on the second ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).
- **[S-8000]** `screenOff` stands for `PowerAction.ScreenOff` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 7 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it runs on one click ([S-1913]). Its descriptor declares it a
  one-readout widget ([S-6737]).

Needs a manual pass: each action itself on a real desktop (never in a render or a test, [S-1251]).
