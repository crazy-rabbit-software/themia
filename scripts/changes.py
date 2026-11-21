"""[S-14342] What a change touches: the widget kinds a diff reaches and the test classes that pin it.

The checks (scripts/gap-check.py, scripts/item-check.py, the widget audit) and the tests (scripts/test.py) take
`--changed [range]`: the files a git range changed (default: the branch against winui, uncommitted and untracked files
included), mapped by a cheap static rule, each pick printed with why:

- a module's folder (src/Widgets/Themia.Widget.X, its Tests/ and SPEC.md aside) -> the kinds that project declares
  (the build's own catalog, `Themia.exe --render catalog`: a family's every kind);
- shared code (src/Themia.Sdk, Sdk.WinUI, Core, App, a provider) -> the top-level types the changed file declares (and
  its extension methods), and the types of the files that name those (one step out: a kit planner's change reaches the
  kit list that every kit widget names); the kinds whose module names any of them. Where the card or the render path
  (the widget card, the render and its batch, the layout dump) names one, every kind;
- the checks' own code and rules (gap-check, item-check, renders, docs/WIDGET-RULES.md), the build's shared files
  (Directory.Build.*, a csproj, the generators), a shared file that is not C# (a locale, XAML), or a file that declares
  no type: unsure, so every kind;
- anything else (docs, SPEC.md, tests, the store) -> no kind.

Tests: a changed test file's classes; a module's change -> its own Tests/ classes and the host tests that name its
types; shared code -> the test classes that name the types above, the classes whose tests are named after a SPEC tag
on a changed line (S1234_), the modules' tests of the kinds it reaches, and the source-scanning guards (they read the
sources); a file that is not code -> the classes that name it (a fixture, SPEC.md); the test projects' shared code, a
csproj or Directory.Build.* -> everything.

Three steps out or more is not followed: before an install or a merge the checks run over every kind changed since
the last full sweep and the full tests run (CLAUDE.md, Definition of done).
"""
import os
import re
import subprocess

ALL = 'ALL'
BASE = 'winui'

TOP = re.compile(r'^(?:(?:public|internal|private|protected|sealed|static|abstract|partial|readonly|file|unsafe|ref|new)\s+)*'
                 r'(?:class|struct|interface|enum|record)\s+(?:class\s+|struct\s+)?([A-Z]\w*)', re.M)
EXTENSION = re.compile(r'\bstatic\s+[\w<>\[\],?.() ]+?\s([A-Z]\w*)\s*(?:<[^>()]*>)?\s*\(\s*this\b')
TOKEN = re.compile(r'\b[A-Z]\w*')
COMMENT = re.compile(r'//[^\n]*|/\*.*?\*/', re.S)
NAMESPACE = re.compile(r'^namespace\s+([\w.]+)\s*;', re.M)
# A file's namespace and plain using lines name namespaces, not types (a `using static` or an alias stays).
DIRECTIVE = re.compile(r'^(?:namespace\s+[\w.]+\s*;|global\s+using\s+(?!static)[\w.]+\s*;|using\s+(?!static)[\w.]+\s*;)', re.M)
TAG = re.compile(r'\bS-?(\d{1,5})\b')
TEST_TAG = re.compile(r'\bvoid\s+S(\d{1,5})_')

SHARED = ('src/Themia.Sdk/', 'src/Themia.Sdk.WinUI/', 'src/Themia.Core/', 'src/Themia.App/', 'src/Providers/')
# The card and the render path: every kind is drawn through them.
RENDER = ('src/Themia.App/App.xaml', 'src/Themia.App/Shell/WidgetCard', 'src/Themia.App/Shell/PreviewCardHost.cs',
          'src/Themia.App/Shell/PngFile.cs',
          'src/Themia.App/Shell/DesignApplier.cs', 'src/Themia.App/Widgets/',
          'src/Themia.App/Shell/Gallery/WidgetRender.cs', 'src/Themia.App/Shell/Gallery/WidgetBatch.cs',
          'src/Themia.App/Shell/Gallery/ItemDump.cs', 'src/Themia.App/Shell/Gallery/RenderPng.cs',
          'src/Themia.App/Shell/Gallery/NoticeRender.cs', 'src/Themia.App/Shell/Gallery/RenderMachine.cs',
          'src/Themia.App/Shell/Gallery/PlaceRender.cs', 'src/Themia.App/Shell/Gallery/DesignArgs.cs')
# The checks themselves: what they judge by.
CHECKS = ('scripts/gap-check.py', 'scripts/item-check.py', 'scripts/renders.py', 'docs/WIDGET-RULES.md')
# The build every project shares: unsure what it changes.
BUILD = ('Directory.Build.props', 'Directory.Build.targets', 'src/Widgets/Directory.Build.props',
         'src/Widgets/Directory.Build.targets', 'src/Themia.Localization.Generator/', 'src/Themia.Modules.Generator/')
# What the source-scanning tests read the sources through.
GUARDS = {'RepoFiles', 'ModuleSources'}


def module_of(path):
    """'Themia.Widget.X' for a path in a module's folder, else None."""
    m = re.match(r'src/Widgets/(Themia\.Widget\.[^/]+)/', path)
    return m.group(1) if m else None


def is_test_file(path):
    """A test source of one suite (tests/Shared is both suites' code: a change there runs everything)."""
    return path.endswith('.cs') and test_project(path) is not None


def test_project(path):
    """Which test assembly a test source compiles into."""
    if path.startswith('tests/Themia.Core.Tests/'):
        return 'core'
    if path.startswith('tests/Themia.Modules.Tests/') or re.match(r'src/(Widgets|Providers)/[^/]+/Tests/', path):
        return 'modules'
    return None


class Index:
    """What the rules read of the sources: each shared file's top-level types and the names it uses, each module's
    names, each test file's namespace, classes, names and SPEC tags."""

    def __init__(self, texts, old=None):
        """texts: {repo path: source} of every C# file (and test file) the rules read; old: {path: source} of files a
        range deleted, as they were."""
        self.shared, self.modules, self.tests, self.module_types = {}, {}, {}, {}
        self.old = {}
        # The words of every namespace: a type named like one (App, Kit) cannot be told from it by name.
        self.namespace_words = {w for text in texts.values() for ns in NAMESPACE.findall(text) for w in ns.split('.')}
        for path, text in texts.items():
            self._add(path, text)
        for path, text in (old or {}).items():
            self.old[path] = declared(text)

    def _add(self, path, text):
        if not path.endswith('.cs'):
            return
        code = DIRECTIVE.sub(' ', COMMENT.sub(' ', text))
        if path.startswith('tests/') and not is_test_file(path):
            return
        if is_test_file(path):
            ns = NAMESPACE.search(text)
            self.tests[path] = {
                'path': path,
                'project': test_project(path),
                'classes': [(ns.group(1) + '.' if ns else '') + c for c in TOP.findall(code)],
                'tokens': set(TOKEN.findall(code)),
                'tags': set(TEST_TAG.findall(code)),
                'text': text,
            }
            return
        module = module_of(path)
        if module:
            self.modules.setdefault(module, set()).update(TOKEN.findall(code))
            self.module_types[path] = declared(text)
            return
        if path.startswith(SHARED):
            self.shared[path] = (declared(text), set(TOKEN.findall(code)))

    def types(self, path):
        if path in self.shared:
            return self.shared[path][0]
        if path in self.module_types:
            return self.module_types[path]
        return self.old.get(path, set())


def declared(text):
    """A C# file's top-level types and its extension methods: what another file names to use it."""
    code = COMMENT.sub(' ', text)
    return set(TOP.findall(code)) | set(EXTENSION.findall(code))


def reach(index, path):
    """The types a shared file's change reaches: its own (T0), and those of the files that name them, each with the
    type of T0 it came through. And the render-path file that names one, if any. Nothing (unsure) for a file whose types
    are all named like a namespace."""
    own = index.types(path) - index.namespace_words
    reached = {t: t for t in own}
    for other, (types, tokens) in index.shared.items():
        if other == path:
            continue
        hit = tokens & own
        if hit:
            via = sorted(hit)[0]
            for t in types - index.namespace_words:
                reached.setdefault(t, via)
    render = None
    if path.startswith(RENDER):
        render = (path, sorted(own)[0] if own else '')
    else:
        names = set(reached)
        for other, (_, tokens) in sorted(index.shared.items()):
            if other.startswith(RENDER) and tokens & names:
                render = (other, sorted(tokens & names)[0])
                break
    return reached, render


def why_reached(t, via, path):
    name = os.path.basename(path)
    return f'uses {t} ({name})' if t == via else f'uses {t}, which uses {via} ({name})'


def pick_kinds(files, index, kinds_of):
    """{kind: why} for the changed files, or ALL with why. kinds_of: {module project: [its kinds]}."""
    picked = {}
    for path in files:
        if path.startswith(CHECKS):
            return ALL, f'the checks changed ({path})'
        if path.startswith(BUILD) or (path.startswith('src/') and path.endswith(('.csproj', '.props', '.targets'))
                                      and module_of(path) is None):
            return ALL, f'the shared build changed ({path})'
        module = module_of(path)
        if module:
            if '/Tests/' in path or path.endswith('/SPEC.md'):
                continue
            for kind in kinds_of.get(module, []):
                picked.setdefault(kind, f'its module changed ({path})')
            continue
        if not path.startswith(SHARED) or '/Tests/' in path:
            continue
        if not path.endswith('.cs'):
            return ALL, f'unsure: a shared file that is not C# changed ({path})'
        reached, render = reach(index, path)
        if not reached:
            return ALL, f'unsure: {path} declares no type to follow'
        if render:
            return ALL, f'the card or the render path changed: {render[0]} uses {render[1]} ({path})'
        for module, tokens in index.modules.items():
            hit = sorted(tokens & set(reached))
            if hit:
                for kind in kinds_of.get(module, []):
                    picked.setdefault(kind, why_reached(hit[0], reached[hit[0]], path))
    return picked, None


def pick_tests(files, tags, index):
    """{'core': {class: why} or ALL, 'modules': ...} for the changed files. tags: SPEC tag numbers on changed lines."""
    picked = {'core': {}, 'modules': {}}
    everything = None

    def add(path_info, why):
        project = path_info['project']
        if picked[project] is ALL:
            return
        for c in path_info['classes']:
            picked[project].setdefault(c, why)

    def by(predicate, why):
        for info in index.tests.values():
            if predicate(info):
                add(info, why(info))

    changed_shared = False
    for path in files:
        if path.startswith(('tests/Shared/', *BUILD)) or (path.startswith(('src/', 'tests/'))
                                                          and path.endswith(('.csproj', '.props', '.targets'))):
            everything = f'the shared test or build code changed ({path})'
            break
        if path in index.tests:
            add(index.tests[path], f'changed ({path})')
            continue
        name = os.path.basename(path)
        module = module_of(path)
        if module:
            if '/Tests/' in path:
                continue
            own = f'src/Widgets/{module}/Tests/'
            by(lambda i: i['path'].startswith(own), lambda i: f'its module changed ({path})')
            own_types = index.types(path)
            if own_types:
                by(lambda i: i['tokens'] & own_types, lambda i: f"names {sorted(i['tokens'] & own_types)[0]} ({name})")
            continue
        if path.startswith(SHARED) and path.endswith('.cs') and '/Tests/' not in path:
            changed_shared = True
            reached, _ = reach(index, path)
            if not reached:
                everything = f'unsure: {path} declares no type to follow'
                break
            names = set(reached)
            def why(info, names=names, reached=reached, path=path):
                t = sorted(info['tokens'] & names)[0]
                return why_reached(t, reached[t], path).replace('uses', 'names', 1)

            by(lambda i: i['tokens'] & names, why)
            continue
        if path.startswith('src/') and not path.endswith('.cs'):
            changed_shared = True
        if path.startswith(('scripts/', 'tools/', '.claude/')):
            continue
        # Anything else (a fixture, SPEC.md, a locale): the classes that name the file.
        stem = os.path.splitext(name)[0]
        by(lambda i: name in i['text'] or (len(stem) > 3 and f'"{stem}' in i['text']), lambda i: f'names {name}')
    if everything:
        return {'core': ALL, 'modules': ALL}, everything
    if tags:
        by(lambda i: i['tags'] & tags, lambda i: f"tests S-{sorted(i['tags'] & tags)[0]}, a tag on a changed line")
    if changed_shared or any(module_of(p) for p in files):
        by(lambda i: i['tokens'] & GUARDS, lambda i: 'reads the sources (a guard)')
    return picked, None


# ---- git and the file system ------------------------------------------------------------------------------------


def git(root, *args):
    return subprocess.run(['git', '-C', root, *args], capture_output=True, text=True, encoding='utf-8',
                          errors='replace', check=True).stdout


def base_of(root, rng):
    """The revision a range starts from: its left end, or the branch's fork from winui."""
    if rng and '..' in rng:
        left = rng.split('...')[0] if '...' in rng else rng.split('..')[0]
        return left or 'HEAD'
    if rng:
        return rng
    return git(root, 'merge-base', BASE, 'HEAD').strip()


def describe_range(root, rng):
    if rng and '..' in rng:
        return rng
    return f'{rng or BASE}..(working tree)' if rng else f'the branch against {BASE} ({base_of(root, None)[:8]}..working tree)'


def changed_files(root, rng=None):
    """Repo paths a range changed; without one, the branch against winui with the uncommitted and untracked files."""
    if rng and '..' in rng:
        out = git(root, 'diff', '--name-only', rng)
    else:
        out = git(root, 'diff', '--name-only', base_of(root, rng))
        out += git(root, 'ls-files', '--others', '--exclude-standard')
    return sorted({line.strip() for line in out.splitlines() if line.strip()})


def changed_tags(root, rng=None):
    """The SPEC tag numbers on the lines a range added or removed."""
    diff = git(root, 'diff', '-U0', rng) if rng and '..' in rng else git(root, 'diff', '-U0', base_of(root, rng))
    tags = set()
    for line in diff.splitlines():
        if line.startswith(('+', '-')) and not line.startswith(('+++', '---')):
            tags.update(TAG.findall(line))
    return tags


def read_index(root, files=(), rng=None):
    """The index over the working tree, and the deleted files among <files> as the range's start had them."""
    texts = {}
    for top in ('src', 'tests'):
        for folder, dirs, names in os.walk(os.path.join(root, top)):
            dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
            for name in names:
                if name.endswith('.cs'):
                    path = os.path.join(folder, name)
                    rel = os.path.relpath(path, root).replace(os.sep, '/')
                    with open(path, encoding='utf-8', errors='replace') as f:
                        texts[rel] = f.read()
    old = {}
    base = base_of(root, rng) if files else None
    for path in files:
        if path.endswith('.cs') and path not in texts:
            try:
                old[path] = git(root, 'show', f'{base}:{path}')
            except subprocess.CalledProcessError:
                pass  # ignore: added and removed again inside the range, nothing to follow
    return Index(texts, old)


def kinds_in(root):
    """Every kind the modules declare by source (`[assembly: ThemiaWidget("id", ...)]`), each module's in its folder;
    the checks read the build's own catalog instead (renders.Catalog), this is the fallback without a build."""
    out = {}
    base = os.path.join(root, 'src', 'Widgets')
    for d in sorted(os.listdir(base)):
        folder = os.path.join(base, d)
        if not d.startswith('Themia.Widget.') or not os.path.exists(os.path.join(folder, d + '.csproj')):
            continue
        declared_ids = []
        for name in sorted(os.listdir(folder)):
            if name.endswith('.cs'):
                with open(os.path.join(folder, name), encoding='utf-8') as src:
                    declared_ids += re.findall(r'\[assembly: ThemiaWidget\("(\w+)"', src.read())
        out[d] = declared_ids
    return out


def changed_kinds(root, kinds_of, rng=None):
    """{kind: why} the range touches (every kind when unsure)."""
    files = changed_files(root, rng)
    picked, everything = pick_kinds(files, read_index(root, files, rng), kinds_of)
    if picked is ALL:
        return {k: everything for ks in kinds_of.values() for k in ks}
    return picked


def changed_tests(root, rng=None):
    files = changed_files(root, rng)
    return pick_tests(files, changed_tags(root, rng), read_index(root, files, rng))
