"""[S-14341] The renders the widget checks share: one render pass, many judges.

The gap check (scripts/gap-check.py), the item check (scripts/item-check.py) and the widget audit
(.claude/workflows/widget-audit.js) look at the same pictures: a kind in a state at a set of sizes, drawn by
`Themia.exe --widget-png <kind> <png> --sizes <s> --transparent --layout-json <json> [--state <s>]`. This module draws
each of them once per build and keeps it:

- one process for many pictures: the jobs go to `Themia.exe --widget-batch <jobs.json>` ([S-14340]) in chunks, a few
  workers at a time (default 2: the PC hard-hung under ~16 renders at once), so WinUI starts once a chunk, not once a
  picture; a job that hangs is killed by its timeout and reported, and the chunk goes on in a new process from the job
  after it;
- a cache keyed by the build (a stamp of the Debug output's files: their names, sizes and times) and the job (kind,
  state, sizes, theme, flags): a second check on the same build draws nothing. It lives in <repo>/TestResults/renders
  (or --cache / THEMIA_RENDER_CACHE), one folder a build, the newest few kept;
- the kinds and their states from the build's own catalog (`Themia.exe --catalog-json`, [S-14342]), asked once a
  build and kept with the renders: never a folder name or a regex over the sources;
- one sweep drawing at a time on this PC: a second waits for the first (a lock in the user's home folder, released
  when its process ends however it ends).

usage, beside the checks:  python scripts/renders.py [--exe <Themia.exe>] (--kinds a,b | --changed [range] | --all)
                                                     [--state s] [--sizes s1;s2] [--flags="--hover 0.5,0.3"] [--out <dir>]
draws those pictures (every state of each kind, or --state) into the cache and links them into <out> as
<kind>.<state>.<n>.png (n: the size set's place in --sizes), for an audit or a look. --list prints the kinds picked
instead, as JSON ([{kind, project, states, why}]), drawing nothing.
"""
import argparse
import hashlib
import json
import os
import queue
import re
import shutil
import subprocess
import sys
import threading
import time
from dataclasses import dataclass, field

CHUNK = 40  # jobs a batch process draws before the next starts afresh
KEEP = 4  # builds' renders kept in the cache
GRACE = 60  # seconds a batch may take to start before its first job
STAMPED = ('.dll', '.exe', '.pri', '.xbf', '.winmd')


def repo_root():
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def find_exe(root, exe=None):
    """The Debug Themia.exe: the one given, or the one under <repo>/artifacts. Never a guess among several (other
    branches' builds land there too): name it with --exe."""
    if exe:
        if not os.path.exists(exe):
            sys.exit(f'No such Themia.exe: {exe}')
        return os.path.abspath(exe)
    import glob
    found = glob.glob(os.path.join(root, 'artifacts', '**', 'debug_win-x64', 'Themia.exe'), recursive=True)
    if len(found) != 1:
        sys.exit(f'{len(found)} Debug Themia.exe under artifacts/: build src/Themia.App/Themia.App.csproj -c Debug '
                 f'(--artifacts-path <folder>) and pass --exe <folder>/bin/Themia.App/debug_win-x64/Themia.exe.')
    return found[0]


class Lock:
    """One sweep drawing at a time on this PC (the PC hard-hung under ~16 renders at once): the second waits, saying
    whose sweep it waits for. An OS lock on a file in the user's home folder, so a killed sweep never leaves it held."""

    def __init__(self, say):
        self.say = say
        self.path = os.environ.get('THEMIA_RENDER_LOCK') or os.path.join(os.path.expanduser('~'), '.themia-renders.lock')
        self.handle = None

    def __enter__(self):
        import msvcrt
        self.handle = open(self.path, 'a+b')
        waited = False
        while True:
            try:
                self.handle.seek(0)
                msvcrt.locking(self.handle.fileno(), msvcrt.LK_NBLCK, 1)
                break
            except OSError:
                if not waited:
                    self.say(f'renders: another sweep is drawing ({self._holder()}); waiting for it')
                    waited = True
                time.sleep(2)
        with open(self.path + '.who', 'w', encoding='utf-8') as f:
            f.write(f'pid {os.getpid()} in {repo_root()} since {time.strftime("%H:%M:%S")}')
        return self

    def _holder(self):
        try:
            with open(self.path + '.who', encoding='utf-8') as f:
                return f.read().strip()
        except OSError:
            return 'unknown'

    def __exit__(self, *exc):
        import msvcrt
        try:
            self.handle.seek(0)
            msvcrt.locking(self.handle.fileno(), msvcrt.LK_UNLCK, 1)
        finally:
            self.handle.close()


def build_stamp(exe):
    """What tells one build from another: every binary beside the exe by its path, size and time."""
    base = os.path.dirname(os.path.abspath(exe))
    h = hashlib.sha1()
    for folder, dirs, files in os.walk(base):
        dirs.sort()
        for name in sorted(files):
            if name.lower().endswith(STAMPED):
                path = os.path.join(folder, name)
                st = os.stat(path)
                h.update(f'{os.path.relpath(path, base)}|{st.st_size}|{st.st_mtime_ns}\n'.encode('utf-8'))
    return h.hexdigest()[:16]


@dataclass(frozen=True)
class Job:
    """One picture: a kind in a state (empty: its default) at a set of sizes, with the render flags it takes."""
    kind: str
    state: str = ''
    sizes: str = 'range'
    flags: tuple = ('--transparent',)

    def key(self, theme):
        text = json.dumps([self.kind, self.state, self.sizes, theme, list(self.flags)])
        return hashlib.sha1(text.encode('utf-8')).hexdigest()[:20]

    def args(self, png):
        """What follows `--widget-png` for this job, as the checks have always drawn it."""
        args = [self.kind, png, '--sizes', self.sizes, *self.flags, '--layout-json', png + '.layout.json']
        if self.state:
            args += ['--state', self.state]
        return args


@dataclass
class Result:
    job: Job
    png: str  # in the cache; may not exist when the render failed
    layout: str
    output: str = ''
    timed_out: bool = False
    crashed: str = ''  # why the process ended under this job, when it did

    @property
    def drawn(self):
        return os.path.exists(self.png)

    @property
    def laid_out(self):
        return os.path.exists(self.layout)


class Renders:
    """The shared render pass over one Debug build."""

    def __init__(self, exe, cache=None, workers=2, timeout=120, theme='dark', chunk=CHUNK, say=None):
        self.exe = exe
        self.theme = theme
        self.workers = max(1, workers)
        self.timeout = timeout
        self.chunk = max(1, chunk)
        self.say = say or (lambda text: print(text, file=sys.stderr, flush=True))
        self.stamp = build_stamp(exe)
        root = cache or os.environ.get('THEMIA_RENDER_CACHE') or os.path.join(repo_root(), 'TestResults', 'renders')
        self.dir = os.path.join(os.path.abspath(root), self.stamp)
        os.makedirs(self.dir, exist_ok=True)
        self._prune(os.path.dirname(self.dir))
        self.drawn = 0  # pictures this run drew (not from the cache)
        self.seconds = 0.0  # time spent drawing them

    def _prune(self, root):
        """The newest builds' renders stay; the cache never grows past KEEP builds."""
        try:
            builds = sorted((os.path.join(root, d) for d in os.listdir(root) if os.path.isdir(os.path.join(root, d))),
                            key=os.path.getmtime, reverse=True)
        except OSError as e:
            self.say(f'renders: cannot list the cache {root}: {e}')
            return
        os.utime(self.dir)
        for old in builds[KEEP:]:
            if old != self.dir:
                shutil.rmtree(old, ignore_errors=True)

    def _paths(self, job):
        key = job.key(self.theme)
        png = os.path.join(self.dir, key + '.png')
        return png, png + '.layout.json', os.path.join(self.dir, key + '.json')

    def _cached(self, job):
        png, layout, meta = self._paths(job)
        try:
            with open(meta, encoding='utf-8') as f:
                data = json.load(f)
        except (OSError, ValueError):
            return None
        return Result(job, png, layout, data.get('output', ''))

    def _keep(self, result):
        """A render that wrote its picture goes into the cache (a failure, a timeout or a crash does not: it may not
        happen again)."""
        _, _, meta = self._paths(result.job)
        tmp = meta + '.tmp'
        with open(tmp, 'w', encoding='utf-8') as f:
            json.dump({'kind': result.job.kind, 'state': result.job.state, 'sizes': result.job.sizes,
                       'flags': list(result.job.flags), 'theme': self.theme, 'output': result.output}, f)
        os.replace(tmp, meta)

    def catalog(self):
        """[S-14342] Every kind of this build: [{id, project, traits, styles, states}], asked once a build."""
        path = os.path.join(self.dir, 'catalog.json')
        if not os.path.exists(path):
            tmp = path + '.tmp.json'
            try:
                p = subprocess.run([self.exe, '--catalog-json', tmp], capture_output=True, text=True, encoding='utf-8',
                                   errors='replace', timeout=300)
            except subprocess.TimeoutExpired:
                sys.exit(f'{self.exe} --catalog-json timed out')
            if p.returncode != 0 or not os.path.exists(tmp):
                sys.exit(f'{self.exe} --catalog-json failed (exit {p.returncode}): {(p.stdout + p.stderr).strip()[:400]}'
                         '\n(a build from before --catalog-json? rebuild src/Themia.App)')
            os.replace(tmp, path)
        with open(path, encoding='utf-8') as f:
            return json.load(f)['kinds']

    def kinds_of(self):
        """{module project: [its kinds]}, in the catalog's order."""
        out = {}
        for k in self.catalog():
            out.setdefault(k['project'], []).append(k['id'])
        return out

    def states(self, kinds):
        """Every kind's states, from the catalog: [''] (its default alone) for a kind that offers none."""
        by_id = {k['id']: k['states'] for k in self.catalog()}
        return {k: list(by_id.get(k) or ['']) for k in kinds}

    def render(self, jobs):
        """Every job's result, drawn now where the cache has none."""
        jobs = list(dict.fromkeys(jobs))
        results, todo = {}, []
        for job in jobs:
            hit = self._cached(job)
            if hit is not None:
                results[job] = hit
            else:
                todo.append(job)
        if not todo:
            return results
        with Lock(self.say):
            self._draw(todo, results)
        return results

    def _draw(self, todo, results):
        started = time.monotonic()
        chunks = queue.Queue()
        for i in range(0, len(todo), self.chunk):
            chunks.put(todo[i:i + self.chunk])
        lock = threading.Lock()
        progress = {'done': 0, 'failed': 0, 'said': time.monotonic()}

        def finished(result):
            with lock:
                results[result.job] = result
                progress['done'] += 1
                if not result.drawn:
                    progress['failed'] += 1
                if time.monotonic() - progress['said'] > 15 or progress['done'] == len(todo):
                    progress['said'] = time.monotonic()
                    self.say(f"renders: {progress['done']} of {len(todo)} drawn ({progress['failed']} failed), "
                             f'{time.monotonic() - started:.0f} s')

        def worker():
            while True:
                try:
                    chunk = chunks.get_nowait()
                except queue.Empty:
                    return
                self._run(chunk, finished)

        threads = [threading.Thread(target=worker, daemon=True) for _ in range(min(self.workers, chunks.qsize()))]
        for t in threads:
            t.start()
        for t in threads:
            t.join()
        self.drawn += len(todo)
        self.seconds += time.monotonic() - started

    def _run(self, chunk, finished):
        """One chunk through batch processes: a job past its timeout, or a process that dies under one, costs that
        job alone; the rest go on in a new process."""
        remaining = list(chunk)
        while remaining:
            remaining = self._batch(remaining, finished)

    def _batch(self, jobs, finished):
        ids = {str(i): job for i, job in enumerate(jobs)}
        listing = os.path.join(self.dir, f'_batch-{threading.get_ident()}-{time.monotonic_ns()}.json')
        with open(listing, 'w', encoding='utf-8') as f:
            json.dump({'jobs': [{'id': i, 'args': job.args(self._paths(job)[0])} for i, job in ids.items()]}, f)
        # What an earlier, unfinished render of these jobs left: their pictures, layouts and popups.
        stems = tuple(os.path.basename(self._paths(job)[0])[:-len('.png')] for job in jobs)
        for name in os.listdir(self.dir):
            if name.startswith(stems):
                os.remove(os.path.join(self.dir, name))
        cmd = [self.exe, '--widget-batch', listing, '--theme', self.theme, '--timeout', str(self.timeout)]
        lines = queue.Queue()
        try:
            proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                    encoding='utf-8', errors='replace')
        except OSError as e:
            for job in jobs:
                png, layout, _ = self._paths(job)
                finished(Result(job, png, layout, f'cannot start {self.exe}: {e}', crashed=str(e)))
            return []

        def read():
            for line in proc.stdout:
                lines.put(line.rstrip('\r\n'))
            lines.put(None)

        threading.Thread(target=read, daemon=True).start()
        current, out, seen, started_any = None, [], set(), False
        deadline = time.monotonic() + GRACE + self.timeout
        try:
            while True:
                try:
                    line = lines.get(timeout=max(0.1, deadline - time.monotonic()))
                except queue.Empty:
                    # The UI thread is held, or the process is gone quiet: kill it, the job under it timed out.
                    proc.kill()
                    stuck = current if current is not None else next((i for i in ids if i not in seen), None)
                    if stuck is not None:
                        seen.add(stuck)
                        png, layout, _ = self._paths(ids[stuck])
                        finished(Result(ids[stuck], png, layout, '\n'.join(out), timed_out=True))
                    break
                if line is None:
                    break
                m = re.match(r'batch: (start|done|timeout) (\S+)(?: (ok|failed))?$', line)
                if m and m.group(2) in ids:
                    what, i = m.group(1), m.group(2)
                    if what == 'start':
                        current, out, started_any = i, [], True
                        deadline = time.monotonic() + self.timeout + 15
                        continue
                    seen.add(i)
                    current = None
                    png, layout, _ = self._paths(ids[i])
                    result = Result(ids[i], png, layout, '\n'.join(out), timed_out=what == 'timeout')
                    if what == 'done' and m.group(3) == 'ok':
                        self._keep(result)
                    finished(result)
                    out = []
                    continue
                out.append(line)
        finally:
            try:
                proc.wait(timeout=30)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()
            try:
                os.remove(listing)
            except OSError:
                pass  # ignore: a leftover jobs list costs nothing
        if not started_any:
            # The batch never started a job (an old build without --widget-batch, a refused list): say why, once a job.
            why = '\n'.join(out) or f'exit code {proc.returncode}'
            for i, job in ids.items():
                if i not in seen:
                    png, layout, _ = self._paths(job)
                    finished(Result(job, png, layout, why, crashed=f'the batch did not start: {why}'))
            return []
        if current is not None and current not in seen:
            seen.add(current)
            png, layout, _ = self._paths(ids[current])
            finished(Result(ids[current], png, layout, '\n'.join(out),
                            crashed=f'the batch process ended (exit code {proc.returncode})'))
        return [job for i, job in ids.items() if i not in seen]


def popups(result):
    """The popups a render drew beside its picture (a confirmation a state opens: <name>-popup<n>.png), as it said."""
    folder = os.path.dirname(result.png)
    return [os.path.join(folder, os.path.basename(p))
            for p in re.findall(r'^widget: wrote (.+-popup\d+\.png)$', result.output, re.M)]


def place(result, png):
    """The cached picture, its layout and its popups under <png> too (a hard link, or a copy where none can be made)."""
    os.makedirs(os.path.dirname(png), exist_ok=True)
    stem, cached = png[:-len('.png')], result.png[:-len('.png')]
    pairs = [(result.png, png), (result.layout, png + '.layout.json')]
    pairs += [(p, stem + p[len(cached):]) for p in popups(result)]
    for source, target in pairs:
        if os.path.exists(target):
            os.remove(target)
        if not os.path.exists(source):
            continue
        try:
            os.link(source, target)
        except OSError:
            shutil.copyfile(source, target)


def add_arguments(ap):
    """The options every check takes for its renders and its kinds."""
    ap.add_argument('--exe', help='the Debug Themia.exe (default: the one under artifacts/, if only one)')
    ap.add_argument('--kinds', help='these kinds only, a,b')
    ap.add_argument('--changed', nargs='?', const='', metavar='RANGE',
                    help='the kinds a git range touches (the default; with no range the branch against winui, '
                         'uncommitted changes included; "since-sweep": since the last full sweep in this checkout)')
    ap.add_argument('--all', action='store_true', help='every kind: the full sweep (recorded for --changed since-sweep)')
    ap.add_argument('--jobs', type=int, default=2, help='render processes at once (default 2)')
    ap.add_argument('--timeout', type=int, default=120, help='seconds a picture may take')
    ap.add_argument('--cache', help='the render cache (default TestResults/renders, or THEMIA_RENDER_CACHE)')


def open_renders(a):
    root = repo_root()
    return Renders(find_exe(root, a.exe), cache=a.cache, workers=a.jobs, timeout=a.timeout)


def pick(a, renders):
    """The kinds a check judges, and why each: --kinds, --all, or what --changed (the default) touches."""
    import changes
    root = repo_root()
    kinds_of = renders.kinds_of()
    every = [k for ks in kinds_of.values() for k in ks]
    if a.kinds:
        names = [k for k in a.kinds.split(',') if k]
        unknown = [k for k in names if k not in every]
        if unknown:
            sys.exit(f'no such kind in this build: {", ".join(unknown)}')
        return names, {k: 'asked for' for k in names}
    if a.all:
        return every, {k: 'the full sweep' for k in every}
    rng = a.changed or None
    if rng == 'since-sweep':
        try:
            with open(os.path.join(root, SWEPT), encoding='utf-8') as f:
                rng = f.read().split()[0]
        except (OSError, IndexError):
            sys.exit(f'no full sweep recorded in this checkout ({SWEPT}): run the check with --all')
    picked = changes.changed_kinds(root, kinds_of, rng)
    for kind, why in picked.items():
        print(f'  {kind}: {why}', file=sys.stderr)
    names = [k for k in every if k in picked]
    print(f'checking {len(names)} of {len(every)} kinds touched by {changes.describe_range(root, rng)}'
          + ('' if names else ' (none: pass --all for a full sweep)'), file=sys.stderr)
    return names, picked


SWEPT = os.path.join('TestResults', 'last-full-sweep.txt')


def swept(a):
    """After a full sweep: the commit it judged, for `--changed since-sweep` (every kind changed since)."""
    if not a.all:
        return
    import changes
    root = repo_root()
    os.makedirs(os.path.join(root, 'TestResults'), exist_ok=True)
    with open(os.path.join(root, SWEPT), 'w', encoding='utf-8') as f:
        f.write(changes.git(root, 'rev-parse', 'HEAD').strip() + '\n')


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    add_arguments(ap)
    ap.add_argument('--state', help='this state only (default: every state of each kind)')
    ap.add_argument('--sizes', default='range', help='size sets separated by ";" (default: range)')
    ap.add_argument('--flags', default='', help='more render flags, e.g. --flags="--hover 0.5,0.3"')
    ap.add_argument('--out', help='link the pictures here as <kind>.<state>.<n>.png')
    ap.add_argument('--list', action='store_true', help='print the kinds picked as JSON and draw nothing')
    a = ap.parse_args()
    renders = open_renders(a)
    kinds, why = pick(a, renders)
    if a.list:
        projects = {k: p for p, ks in renders.kinds_of().items() for k in ks}
        states = renders.states(kinds)
        print(json.dumps([{'kind': k, 'project': projects[k], 'states': states[k], 'why': why[k]} for k in kinds], indent=1))
        return
    states = {k: [a.state] for k in kinds} if a.state else renders.states(kinds)
    flags = tuple(['--transparent', *a.flags.split()]) if a.flags else ('--transparent',)
    sets = a.sizes.split(';')
    jobs = [(k, s, n, Job(k, s, sizes, flags)) for k in kinds for s in states[k] for n, sizes in enumerate(sets)]
    drawn = renders.render([j for *_, j in jobs])
    failed = 0
    for k, s, n, job in jobs:
        r = drawn[job]
        if not r.drawn:
            failed += 1
            print(f'  {k} {s or "default"} {job.sizes}: render {"timed out" if r.timed_out else "failed"} {r.crashed}')
        if a.out:
            place(r, os.path.join(a.out, f'{k}.{s or "default"}.{n}.png'))
    print(f'renders: {len(jobs)} pictures of {len(kinds)} kinds, {renders.drawn} drawn now in {renders.seconds:.0f} s, '
          f'{len(jobs) - renders.drawn} from the cache; {failed} failed. Build {renders.stamp}: {renders.dir}')
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
