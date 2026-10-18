# Shutdown (R7-POWER)

The module `shutdown` ([S-6911]): one of the seven power buttons, one action each. What the seven share
is the host's and the SDK's (root `SPEC.md`, Power and Bluetooth: one widget per action [S-1910],
the button [S-1911]..[S-1916], [S-1919], the Win32 calls [S-1243], previews [S-1251]; the view
`PowerButtonView` and the rules `PowerButton`, `PowerActionRules`, `PowerFace` in the SDK, the
backend on `IWidgetHost.Power`). This file's tag is its own.

Owning files: everything in this folder: `ShutdownModule.cs` (descriptor, view factory, JSON context),
`ShutdownConfig.cs`, `Localization/Locales/` (its own `ShutdownStr`, 30 languages: its description, and
its name copied from the host's table, which the shared view also reads). Tests: `Tests/ShutdownTests.cs`
(run by `tests/Themia.Modules.Tests`).

- **[S-7400]** `shutdown` stands for `PowerAction.Shutdown` (its config's `Action`, [S-6906]): its widget is
  in the add menu's Devices & power › Power buttons at place 1 ([S-3511]), 120×120, headerless at
  first ([S-700]), and it arms on the first click and runs on the second ([S-1913]).

Needs a manual pass: the action itself on a real desktop (never in a render or a test, [S-1251]).
