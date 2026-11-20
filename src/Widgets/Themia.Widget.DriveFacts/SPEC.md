# Drive facts (DRIVE-FACTS)

The family project `Themia.Widget.DriveFacts` ([S-6901], `[assembly: ThemiaWidgetFamily]`): one drive's facts, **one widget a fact**, each a
single metric on the one base (the root `SPEC.md`'s single metric, [S-14600]..[S-14607]: its one row, its default, its
trend, its sizing). Its tags are this file's; the root holds the drive's rules, the SDK's `DriveRules` ([S-4650]..[S-4657], the pick of the named or system drive [S-4670]); the drives come from the host's one walk (`IWidgetHost.Drives`, [S-4655]).

Owning files: everything in this folder: `DriveKinds.cs`, `DriveMetric.cs` (module, config base, view over the drive walk,
the JSON context), `Samples/` (a made-up system disk), `Preview/` (Debug renders: `--state local|network|missing`),
`Localization/Locales/` (its own `DriveFactsStr`). Tests: `Tests/DriveMetricTests.cs`.

- **[S-11026]** *(Retired by [S-14670]: no card of picked facts.)*
- **[S-14670]** **A widget a fact** (owner, 2026-11-20: "split all the other widgets that are alike to the weather and air facts into individual metrics too. use the same base component for all of them. you should also have the option to show trendlines as well or just the trend with arrows."): `driveUsed` "Drive used", `driveFree` "Drive free", `driveCapacity` "Drive capacity", `driveFileSystem` "File system", `driveType` "Drive type", the family **Drive facts** under Devices & power
  (`AddFolderId.DriveFacts`). Each draws its fact of its drive (a Header widget over a group names the drive, [S-12481]); a
  drive without figures (gone, disconnected, an empty reader) keeps its row, "--" dimmed. Each walk is a new reading.
  Trends ([S-14603]): drive used and drive free an arrow from the last two readings the card saw (no history kept); drive capacity, file system and drive type none (a text, a constant or a time: no Trend line). Its options: which drive (the drive atoms' shared line, [S-4674]), then
  Trend where it moves. A placed `driveFacts` card is replaced in place by its rows, with its drive ([S-14606]). View links:
  the card is its drive; the pointer on it publishes the drive pointed at, and the whole card takes the hover fill under the
  pointer and while its drive is pointed at elsewhere on the view ([S-11950], [S-11962], [S-11963]).

Needs a manual pass: a USB drive chosen then removed, a mapped share disconnected.
