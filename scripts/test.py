"""[S-14343] The tests: built, then run in this repo's runner (tools/TestRunner: xUnit's engine in-process, the test
collections in parallel, no test host and no loopback socket, which `dotnet test` needs and this PC's firewall blocks).

usage: python scripts/test.py [--changed [RANGE] | --all] [--project core|modules] [--filter TEXT] [--spec TAG]
                              [--artifacts DIR] [--no-build] [--serial] [--full]

--changed (the default): the test classes a change touches (scripts/changes.py: a changed test file's classes, a
module's own Tests/, the classes that name the types a shared change reaches, the tests named after a SPEC tag on a
changed line, the source-scanning guards), each printed with why; RANGE defaults to the branch against winui with the
uncommitted and untracked files. --all: both suites whole (before an install or a merge). --filter keeps the tests
whose Namespace.Class.Method holds TEXT, --spec those named after a SPEC tag (S1480_...): with neither --changed nor
--all they narrow both suites whole. Builds into --artifacts (default artifacts/), Debug, nodeReuse off.
Exits 1 when a test fails.
"""
import argparse
import os
import subprocess
import sys
import tempfile
import time

import changes

PROJECTS = {
    'core': ('tests/Themia.Core.Tests/Themia.Core.Tests.csproj', 'Themia.Core.Tests/debug/Themia.Core.Tests.dll'),
    'modules': ('tests/Themia.Modules.Tests/Themia.Modules.Tests.csproj',
                'Themia.Modules.Tests/debug_win-x64/Themia.Modules.Tests.dll'),
}
RUNNER = ('tools/TestRunner/TestRunner.csproj', 'TestRunner/release/TestRunner.dll')


def build(root, csproj, art, config='Debug'):
    started = time.monotonic()
    p = subprocess.run(['dotnet', 'build', os.path.join(root, csproj), '-c', config, '--artifacts-path', art,
                        '-nodeReuse:false', '-p:UseSharedCompilation=false', '-v:q', '-nologo'],
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
    if p.returncode != 0:
        print(p.stdout[-6000:] + p.stderr[-2000:])
        sys.exit(f'build of {csproj} failed')
    print(f'built {os.path.basename(csproj)} in {time.monotonic() - started:.0f} s')


def main():
    sys.stdout.reconfigure(line_buffering=True)  # in order with the runner's own lines
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    ap.add_argument('--changed', nargs='?', const='', metavar='RANGE')
    ap.add_argument('--all', action='store_true')
    ap.add_argument('--project', choices=sorted(PROJECTS))
    ap.add_argument('--filter')
    ap.add_argument('--spec')
    ap.add_argument('--artifacts', default=os.path.join(root, 'artifacts'))
    ap.add_argument('--no-build', action='store_true')
    ap.add_argument('--serial', action='store_true', help='one test at a time')
    ap.add_argument('--full', action='store_true', help='whole failure messages and stacks')
    a = ap.parse_args()

    started = time.monotonic()
    if a.all or ((a.filter or a.spec) and a.changed is None):
        picked = {p: changes.ALL for p in PROJECTS}
    else:
        rng = a.changed or None
        picked, everything = changes.changed_tests(root, rng)
        what = changes.describe_range(root, rng)
        if everything:
            print(f'every test: {everything}')
        for project, classes in picked.items():
            if classes is not changes.ALL:
                for c, why in sorted(classes.items()):
                    print(f'  {project}: {c.rsplit(".", 1)[-1]}: {why}')
        counts = ', '.join(f'{p} {"all" if c is changes.ALL else len(c)}' for p, c in picked.items())
        print(f'tests touched by {what}: {counts} classes')
    if a.project:
        picked = {a.project: picked[a.project]}
    runs = {p: c for p, c in picked.items() if c is changes.ALL or c}
    if not runs:
        print('no test touched: pass --all for both suites')
        return 0

    art = os.path.abspath(a.artifacts)
    if not a.no_build:
        build(root, RUNNER[0], art, 'Release')
        for p in runs:
            build(root, PROJECTS[p][0], art)
    failed = False
    for p, classes in runs.items():
        dll = os.path.join(art, 'bin', PROJECTS[p][1])
        if not os.path.exists(dll):
            sys.exit(f'{dll} is not built (drop --no-build)')
        cmd = ['dotnet', os.path.join(art, 'bin', RUNNER[1]), dll]
        listing = None
        if classes is not changes.ALL:
            listing = tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False, encoding='utf-8')
            listing.write('\n'.join(sorted(classes)))
            listing.close()
            cmd += ['--classes-file', listing.name]
        for flag, value in (('--filter', a.filter), ('--spec', a.spec)):
            if value:
                cmd += [flag, value]
        cmd += ['--serial'] if a.serial else []
        cmd += ['--full'] if a.full else []
        try:
            failed |= subprocess.run(cmd).returncode != 0
        finally:
            if listing:
                os.remove(listing.name)
    print(f'tests: {"FAILED" if failed else "passed"} in {time.monotonic() - started:.0f} s')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
