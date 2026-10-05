# Contributing to Themia

Thanks for helping make Themia better.

## Issues

Use the issue forms for bug reports, feature requests and widget ideas. Include your Themia and
Windows versions and what you did right before the problem. For a bug, the log file
(`%LOCALAPPDATA%\Themia\logs\themia.log`) is the best attachment; read it before you paste it.

Report security problems privately to themia@nathaniel-walser.com, not in a public issue.

## Pull requests

- Open an issue first for anything larger than a small fix, so the approach is agreed before you build it.
- Read the Engineering principles in [CLAUDE.md](CLAUDE.md). They apply to every change: no swallowed
  exceptions, validation at trust boundaries, atomic single-writer persistence, handles freed on every
  path, design tokens instead of literals, Native AOT rules, one implementation of anything.
- Behaviour is pinned in [SPEC.md](SPEC.md) with `[S-NN]` tags. Keep the spec and its tests in step
  with your change, and name tests after the tags.
- Build and test before you open the pull request:
  `dotnet build src/Themia.App/Themia.App.csproj -c Debug` (warnings are errors) and
  `dotnet test tests/Themia.Core.Tests/Themia.Core.Tests.csproj`.
- For UI changes, render the result with the Debug `--settings-png` (and other) render flags described
  in CLAUDE.md and attach the PNG. Widgets must hold up at every size tier of the showcase views
  (`--add-showcase-views`), down to 120 by 120; attach a screenshot of the widget's showcase view.
- Put logic in `Themia.Core` and test it. A bug fix comes with a regression test that fails without the fix.
- Keep refactors and behaviour changes in separate commits.
- The app is unreleased, so there is no migration or legacy-format code: change the schema in place.

## Licensing of contributions

Themia is licensed under the [Elastic License 2.0](LICENSE). By submitting a contribution, you agree that:

1. you wrote it, or otherwise have the right to submit it under these terms;
2. it is licensed under the Elastic License 2.0; and
3. Lometics GmbH may also use, license and relicense it under other terms, including commercial ones.

If you can't agree to this, open an issue describing the change instead.
