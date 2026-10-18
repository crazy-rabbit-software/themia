# ScreenOff (R7-POWER)

The module `screenOff` ([S-6911]): one of the seven power buttons, one action each. What the seven share
is the host's and the SDK's (root `SPEC.md`, Power and Bluetooth: one widget per action [S-1910],
the button [S-1911]..[S-1916], [S-1919], the Win32 calls [S-1243], previews [S-1251]; the view
`PowerButtonView` and the rules `PowerButton`, `PowerActionRules`, `PowerFace` in the SDK, the
backend on `IWidgetHost.Power`). This file's tag is its own.

Owning files: everything in this folder: `ScreenOffModule.cs` (descriptor, view factory, JSON context),
`ScreenOffConfig.cs`, `Localization/Locales/` (its own `ScreenOffStr`, 30 languages: its description, and
its name copied from the host's table, which the shared view also reads). Tests: `Tests/ScreenOffTests.cs`
(run by `tests/Themia.Modules.Tests`).

- **[S-8000]** `screenOff` stands for `PowerAction.ScreenOff` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 7 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it runs on one click ([S-1913]).

Needs a manual pass: the action itself on a real desktop (never in a render or a test, [S-1251]).
