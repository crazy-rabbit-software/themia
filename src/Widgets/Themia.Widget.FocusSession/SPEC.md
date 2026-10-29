# Focus session

The module `focusSession` ([S-6911]): the app's one focus session, a countdown that holds Windows' do
not disturb while it runs. Its tags are this file's alone; the root `SPEC.md` holds the host's sections
and what the session shares, with their root tags: one session for the app ([S-4421], the host's
`FocusSessionService` behind `IWidgetHost.Focus`), its clock and tick ([S-4422]), do not disturb held
and handed back ([S-4423], host-side through the quick-setting hub, never the widget), Windows' own
focus followed ([S-4424], [S-4425]); the rules are the SDK's `FocusSessionRules`.

Owning files: everything in this folder: `FocusSessionModule.cs` (descriptor, glyph, view factory,
menu lines, JSON context), `FocusSessionConfig.cs` (length, do not disturb), `FocusSessionView.cs`
(the time, the ring, the button and steppers through its host, the keys), `FocusSessionLayout.cs` (the
ladder), `FocusSessionMenu.cs` (its properties-menu lines), `Preview/` (Debug renders:
`--state idle|running|quiet|unavailable|windows|long`), `Localization/Locales/` (its own
`FocusSessionStr`, 30 languages). Tests: `Tests/FocusSessionTests.cs`, `Tests/FocusSessionMenuTests.cs` (run by
`tests/Themia.Modules.Tests`); its config under the root's length rule is the module host's
`WindowsPanelsHostTests`.

- **[S-4420]** Kind `FocusSession` (122), add menu Time ([S-3511]), 240×200, glyph Karpo's focus
  target (F272). Config: `Minutes` (5–240, default 30, Windows' own) and `DoNotDisturb` (default on);
  a length out of range in a hand-edited file is pulled into it and logged.
- **[S-4426]** The ladder (`FocusSessionLayout.Plan`) is the timers' face ([S-6739]): the time as a
  Readout on the Hero steps, sized for the widest reading of the length ("00:00", "0:00:00" from an
  hour), centred both ways; the status line (do not disturb, Windows' focus, or what Start will do) its
  line under where it fits; the Controls row, centred at the bottom inset where the 28 line, 8 and the
  row fit: Start (the one main action, worded at every size) with − and + while idle (they give way
  from the last on a narrow tile), End session (a standard button) while it runs; without the row
  the time alone, a click on it starting or ending. From Expanded both ways the Chart block's ring of
  the time gone (stroke 2, its track in the Track role, the arc the accent; idle the track alone)
  stands around the time, as large as the box's short side, the time and its line in its hole.
- **[S-4427]** Keys: Space/Enter start or end; Up/Right/+ and Down/Left/− change the length while idle.
- **[S-4428]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then **Length** (typed in minutes, 5–240, rounded to
  5-minute steps, [S-3726]; the length at the end of its line) and **Turn on do not disturb** (a
  switch). (Tests `FocusSessionMenuTests`.)

Needs a manual pass: the widget on a live desktop ([S-4420], [S-4427], [S-4428]: a session started and
ended from two focus widgets, do not disturb turned on and handed back, Windows' own focus shown, the
keys through the lease), never in a render or a test, which start nothing.
