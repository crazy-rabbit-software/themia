# Drive facts (driveFacts)

The module `driveFacts` ([S-6911]): one drive's facts as fact rows. Its tags are this file's alone; the
root `SPEC.md` holds what the facts atoms share ([S-11007]) and the drive's rules, the SDK's
`DriveRules` ([S-4650]..[S-4657], the pick of the named or system drive [S-4670]). The drives come
from the host's one walk (`IWidgetHost.Drives`, [S-4655]), as the Drive reading's.

Owning files: everything in this folder: `DriveFactsModule.cs` (descriptor, config, view, menu lines,
JSON context), `Preview/` (Debug renders: `--state local|network|missing`), `Localization/Locales/`
(its own `DriveFactsStr`; the facts' names are the SDK's, moved from the Drive's with their
translations). Tests: `Tests/DriveFactsTests.cs`.

- **[S-11026]** Drive facts (the Drive card's facts as their own atom): Used, Free, Capacity, File
  system and Type (`DriveRules.Facts`, `DriveRules.Sheet`), a new one the first four (a Header widget
  over them names the drive, [S-12481]). A drive without figures (gone, disconnected, an empty
  reader) keeps its rows, each it cannot read "--" dimmed. Its options: which drive (the reading's line,
  [S-4674], `DriveMenuLines`), then Facts ([S-11007]).
- View links: the card is its drive; the pointer on it publishes the drive pointed at, and the whole card takes the
  hover fill, over its whole box, under the pointer and while its drive is pointed at elsewhere on the view ([S-11950],
  [S-11962], [S-11963]).

Needs a manual pass: a USB drive chosen then removed, a mapped share disconnected.
