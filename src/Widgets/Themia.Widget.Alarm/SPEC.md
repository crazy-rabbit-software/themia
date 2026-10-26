# Alarm

The module `alarm` ([S-6911]): one alarm. Its tags are this file's alone; the root `SPEC.md` holds the
host's sections and what the alarm service and the rules share, with their root tags: the times and
days ([S-4621]), the next ring ([S-4622]), the alarm service and its book ([S-4623], the host's
`AlarmService` behind `IWidgetHost.Alarms`, reading every view's `IAlarm` configs), the words
([S-4624]), the ring ([S-4625], [S-4626]) and ringing only while Themia runs ([S-4627]); the rules are
the SDK's `AlarmRules`.

Owning files: everything in this folder: `AlarmModule.cs` (descriptor, glyph, view factory, menu
lines, JSON context), `AlarmConfig.cs` (the alarm, its repair, `IAlarm`), `AlarmView.cs` (the readout,
the switch, Snooze and Dismiss through its host, the ladder, the keys), `AlarmMenu.cs` (its properties-menu lines),
`Preview/` (Debug renders: `--state set|ringing|snoozed|off`), `Localization/Locales/` (its own
`AlarmStr`, 30 languages). Tests: `Tests/AlarmConfigTests.cs`, `Tests/AlarmMenuTests.cs` (run by `tests/Themia.Modules.Tests`); the
rules and the book under its config are the module host's `AlarmTests`.

- **[S-4620]** An alarm widget (add menu: Time, [S-3511]) is **one** alarm (user decision; several
  alarms are several widgets): a time (`"HH:mm"`, 24-hour in the file), the days it repeats on
  (`"mon"` … `"sun"`, Monday first; none means it rings once), on or off, a name, the sound (the
  timer's ringtones, default Bell), its level and the snooze (1–30 min, default 10), all in
  `AlarmConfig`. A new widget is 07:00, ringing once (no days), off, and needs no setting up.
- **[S-4628]** The widget is the time as its readout, "name · hint" under it, and the switch; while it
  rings (or is snoozed) Snooze and Dismiss stand where the switch was (worded from 300 DIP, else
  glyphs). Off, the time is in the tertiary ink. The card menu has Snooze and Dismiss while it rings.
  There is no alarm list *(the list's rows, [S-1281] fade and per-row keys retired with it)*.
- **[S-4629]** The ladder: the time is reserved, then the switch row, then the words (Caption, Body from
  the Regular height), each as it fits; the time takes the rest (`FluentType.Hero`), centred. A strip
  (2:1, at least 260 wide) puts the switch beside the time and its words, read from the left.
- **[S-4630]** Keys: Space or Enter switch the alarm, or dismiss it while it rings or is snoozed; S
  snoozes it while it rings; Escape lets go. The minute tick turns "Today" into "Tomorrow".
- **[S-4631]** Its options are all on the properties menu ([S-3719], user request 2026-10-19); it
  has no settings page. Appearance ([S-3723]), then in groups apart (dividers, user request
  2026-10-19): when it rings: **Time** (typed in either clock, [S-3738]; anything else refused with
  the reason; the line shows it in the language's clock), **Repeats on** (a switch per day of the
  week from the start of the week ⚙ › Language & region sets, [S-3770], [S-3590]; the days at the end
  of the line, a note that none rings once) and **Switched on**
  (a switch); what it says: **Name** (typed, at most 40 characters, empty clears it); how it sounds:
  **Sound** (a choice of off and the twelve ringtones, previewed once), **Volume** (0–100 % in tens,
  only while a sound is chosen, previewed once: silent, a level changes nothing) and **Snooze for**
  (typed, 1–30 min, [S-3726]); the note of [S-4627] last.
  (Tests `AlarmMenuTests`.)

Needs a manual pass: the widget on a live desktop ([S-4628]..[S-4631]: the readout and switch at every
size, a ring with its sound and its notification's Snooze and Dismiss, the keys through the lease, the
properties menu's sound preview), never in a render or a test, which ring nothing.
