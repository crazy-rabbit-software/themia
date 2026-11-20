"""[S-14342] The rules that pick what a change touches (scripts/changes.py), on made-up sources.

python -m unittest discover -s scripts/tests
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import changes  # noqa: E402

SOURCES = {
    # The SDK: a planner the kit list uses, the kit list every kit widget names, a clock rule only the clock names.
    'src/Themia.Sdk/Kit/KitPlanner.cs': 'namespace Themia.Sdk.Kit;\npublic static class KitPlanner { }\npublic sealed record KitPlan(int Rows);\n',
    'src/Themia.Sdk.WinUI/Kit/KitList.cs': 'namespace Themia.Sdk.WinUI.Kit;\npublic sealed class KitList { KitPlan _plan = KitPlanner.Plan(); }\n',
    'src/Themia.Sdk/Widgets/ClockRules.cs': 'namespace Themia.Sdk;\npublic static class ClockRules\n{\n    public sealed record Nested(int A);\n}\n',
    'src/Themia.Sdk/Widgets/TextExtensions.cs': 'namespace Themia.Sdk;\npublic static class TextExtensions\n{\n    public static string Shout(this string text) => text;\n}\n',
    'src/Themia.Core/Desktop/DesktopGrid.cs': 'namespace Themia.Core.Desktop;\npublic static class DesktopGrid { }\n',
    'src/Themia.Core/Geometry/SizeFit.cs': 'namespace Themia.Core.Geometry;\npublic static class SizeFit { }\n',
    'src/Themia.App/Shell/WidgetCard.cs': 'namespace Themia.App.Shell;\ninternal sealed partial class WidgetCard { void A() => SizeFit.Of(); }\n',
    'src/Themia.App/Shell/DesktopWindow.cs': 'namespace Themia.App.Shell;\ninternal sealed partial class DesktopWindow { void A() => DesktopGrid.Lay(); }\n',
    'src/Themia.App/Shell/Script.cs': '// only statements\nConsole.WriteLine();\n',
    # Modules: a kit widget, the clock (a family of two kinds), a widget that shouts.
    'src/Widgets/Themia.Widget.Rss/RssView.cs': 'namespace Themia.Widget.Rss;\npublic sealed class RssView { KitList _list; }\n',
    'src/Widgets/Themia.Widget.Clock/ClockView.cs': 'namespace Themia.Widget.Clock;\npublic sealed class ClockView { void A() => ClockRules.Read(); }\n',
    'src/Widgets/Themia.Widget.Clock/Tests/ClockTests.cs': 'namespace Themia.Widget.Clock.Tests;\npublic sealed class ClockTests { [Fact] public void S1480_reads() { } }\n',
    'src/Widgets/Themia.Widget.Notes/NotesView.cs': 'namespace Themia.Widget.Notes;\npublic sealed class NotesView { void A() => "a".Shout(); }\n',
    # Tests.
    'tests/Themia.Core.Tests/KitPlannerTests.cs': 'namespace Themia.Core.Tests;\npublic sealed class KitPlannerTests { [Fact] public void S7000_plans() => KitPlanner.Plan(); }\n',
    'tests/Themia.Core.Tests/DesktopGridTests.cs': 'namespace Themia.Core.Tests;\npublic sealed class DesktopGridTests { [Fact] public void S11875_lays() => DesktopGrid.Lay(); }\n',
    'tests/Themia.Core.Tests/SpecTests.cs': 'namespace Themia.Core.Tests;\npublic sealed class SpecTests { [Fact] public void Reads() => File.ReadAllText("SPEC.md"); }\n',
    'tests/Themia.Modules.Tests/FamilyGuardTests.cs': 'namespace Themia.Modules.Tests;\npublic sealed class FamilyGuardTests { [Fact] public void Scans() => RepoFiles.CSharp(""); }\n',
    'tests/Themia.Modules.Tests/ClockHostTests.cs': 'namespace Themia.Modules.Tests;\npublic sealed class ClockHostTests { [Fact] public void Hosts() => new ClockView(); }\n',
}
KINDS = {'Themia.Widget.Rss': ['rss'], 'Themia.Widget.Clock': ['clock', 'worldClock'], 'Themia.Widget.Notes': ['notes']}


def kinds(*files, old=None):
    picked, why = changes.pick_kinds(list(files), changes.Index(SOURCES, old), KINDS)
    return (changes.ALL if picked is changes.ALL else set(picked)), why, picked


def tests(*files, tags=()):
    picked, why = changes.pick_tests(list(files), set(tags), changes.Index(SOURCES))
    return {p: (v if v is changes.ALL else set(v)) for p, v in picked.items()}, why


class Kinds(unittest.TestCase):
    def test_S14342_a_module_folder_picks_every_kind_its_project_declares(self):
        self.assertEqual({'clock', 'worldClock'}, kinds('src/Widgets/Themia.Widget.Clock/ClockView.cs')[0])
        self.assertEqual({'clock', 'worldClock'}, kinds('src/Widgets/Themia.Widget.Clock/Localization/Locales/de.json')[0])

    def test_S14342_a_modules_tests_and_spec_draw_nothing(self):
        self.assertEqual(set(), kinds('src/Widgets/Themia.Widget.Clock/Tests/ClockTests.cs',
                                      'src/Widgets/Themia.Widget.Clock/SPEC.md')[0])

    def test_S14342_shared_code_picks_the_kinds_whose_module_names_its_types(self):
        picked, _, why = kinds('src/Themia.Sdk/Widgets/ClockRules.cs')
        self.assertEqual({'clock', 'worldClock'}, picked)
        self.assertIn('uses ClockRules', why['clock'])

    def test_S14342_one_step_out_a_planner_reaches_the_kit_widgets_through_the_kit_list(self):
        picked, _, why = kinds('src/Themia.Sdk/Kit/KitPlanner.cs')
        self.assertEqual({'rss'}, picked)
        self.assertIn('KitList, which uses', why['rss'])

    def test_S14342_an_extension_method_is_followed_by_its_name(self):
        self.assertEqual({'notes'}, kinds('src/Themia.Sdk/Widgets/TextExtensions.cs')[0])

    def test_S14342_a_namespace_word_is_not_a_name_to_follow(self):
        sources = dict(SOURCES)
        sources['src/Themia.App/Shell/Kit.cs'] = 'namespace Themia.App.Shell;\ninternal static class Kit { }\n'
        sources['src/Themia.Sdk/Widgets/Uses.cs'] = 'using Themia.Sdk.Kit;\nnamespace Themia.Sdk.Kit;\npublic static class Uses { }\n'
        picked, why = changes.pick_kinds(['src/Themia.App/Shell/Kit.cs'], changes.Index(sources), KINDS)
        self.assertEqual(changes.ALL, picked)
        self.assertIn('declares no type to follow', why)

    def test_S14342_a_nested_type_is_not_a_name_to_follow(self):
        index = changes.Index(SOURCES)
        self.assertEqual({'ClockRules'}, index.types('src/Themia.Sdk/Widgets/ClockRules.cs'))

    def test_S14342_host_code_off_the_render_path_draws_nothing(self):
        self.assertEqual(set(), kinds('src/Themia.Core/Desktop/DesktopGrid.cs')[0])
        self.assertEqual(set(), kinds('src/Themia.App/Shell/DesktopWindow.cs')[0])

    def test_S14342_what_the_card_uses_and_the_card_itself_draw_every_kind(self):
        picked, why, _ = kinds('src/Themia.Core/Geometry/SizeFit.cs')
        self.assertEqual(changes.ALL, picked)
        self.assertIn('WidgetCard.cs uses SizeFit', why)
        self.assertEqual(changes.ALL, kinds('src/Themia.App/Shell/WidgetCard.cs')[0])

    def test_S14342_unsure_is_every_kind(self):
        for path in ('src/Themia.App/Shell/Script.cs', 'src/Themia.Sdk/Localization/Locales/en.json',
                     'src/Themia.App/App.xaml', 'Directory.Build.props', 'src/Widgets/Directory.Build.targets',
                     'src/Themia.Modules.Generator/Generator.cs', 'src/Themia.Sdk/Themia.Sdk.csproj'):
            self.assertEqual(changes.ALL, kinds(path)[0], path)

    def test_S14342_the_checks_own_code_and_rules_are_every_kind(self):
        for path in ('scripts/gap-check.py', 'scripts/item-check.py', 'scripts/renders.py', 'docs/WIDGET-RULES.md'):
            self.assertEqual(changes.ALL, kinds(path)[0], path)

    def test_S14342_docs_tests_and_other_scripts_draw_nothing(self):
        self.assertEqual(set(), kinds('SPEC.md', 'CLAUDE.md', 'docs/STORE.md', 'tests/Themia.Core.Tests/KitPlannerTests.cs',
                                      'scripts/changes.py', 'store/scenes.json')[0])

    def test_S14342_a_deleted_file_is_followed_by_the_types_it_had(self):
        old = {'src/Themia.Sdk/Widgets/Gone.cs': 'namespace Themia.Sdk;\npublic static class ClockRules { }\n'}
        self.assertEqual({'clock', 'worldClock'}, kinds('src/Themia.Sdk/Widgets/Gone.cs', old=old)[0])


class Tests(unittest.TestCase):
    def test_S14342_a_changed_test_file_runs_its_classes(self):
        picked, _ = tests('tests/Themia.Core.Tests/KitPlannerTests.cs')
        self.assertEqual({'Themia.Core.Tests.KitPlannerTests'}, picked['core'])
        self.assertEqual(set(), picked['modules'])

    def test_S14342_a_module_runs_its_own_tests_the_host_tests_naming_it_and_the_guards(self):
        picked, _ = tests('src/Widgets/Themia.Widget.Clock/ClockView.cs')
        self.assertEqual({'Themia.Widget.Clock.Tests.ClockTests', 'Themia.Modules.Tests.ClockHostTests',
                          'Themia.Modules.Tests.FamilyGuardTests'}, picked['modules'])
        self.assertEqual(set(), picked['core'])

    def test_S14342_shared_code_runs_the_classes_naming_what_it_reaches(self):
        picked, _ = tests('src/Themia.Core/Desktop/DesktopGrid.cs')
        self.assertEqual({'Themia.Core.Tests.DesktopGridTests'}, picked['core'])
        self.assertEqual({'Themia.Modules.Tests.FamilyGuardTests'}, picked['modules'])

    def test_S14342_a_spec_tag_on_a_changed_line_runs_the_tests_named_after_it(self):
        picked, _ = tests('src/Themia.App/Shell/DesktopWindow.cs', tags={'11875', '1480'})
        self.assertIn('Themia.Core.Tests.DesktopGridTests', picked['core'])
        self.assertIn('Themia.Widget.Clock.Tests.ClockTests', picked['modules'])

    def test_S14342_a_file_that_is_not_code_runs_the_classes_naming_it(self):
        picked, _ = tests('SPEC.md')
        self.assertEqual({'Themia.Core.Tests.SpecTests'}, picked['core'])

    def test_S14342_a_tool_of_its_own_runs_nothing(self):
        picked, why = tests('tools/TestRunner/TestRunner.csproj', 'tools/TestRunner/Program.cs')
        self.assertEqual({'core': set(), 'modules': set()}, picked)
        self.assertIsNone(why)

    def test_S14342_the_test_projects_shared_code_runs_everything(self):
        for path in ('tests/Shared/RepoFiles.cs', 'tests/Themia.Core.Tests/Themia.Core.Tests.csproj', 'Directory.Build.props'):
            picked, why = tests(path)
            self.assertEqual({'core': changes.ALL, 'modules': changes.ALL}, picked, path)
            self.assertIn(path, why)


if __name__ == '__main__':
    unittest.main()
