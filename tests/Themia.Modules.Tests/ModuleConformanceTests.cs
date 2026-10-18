using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6911] What every module project must be, checked over the registry this build generates (so
/// a new folder is checked by being there): its id is its folder's, its texts are its own and whole
/// in every language, its config round-trips through the state documents under its id, it depends
/// on the SDK and providers only, its view's items match its traits, and it carries its SPEC section.
/// </summary>
public sealed partial class ModuleConformanceTests
{
    private static string Src => Path.Combine(RepoFiles.Root, "src");

    /// <summary>Every widget module project folder (src/Widgets/Themia.Widget.*), by its widget id.</summary>
    private static Dictionary<string, string> WidgetFolders()
        => Directory.EnumerateDirectories(Path.Combine(Src, "Widgets"), "Themia.Widget.*")
            .Where(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj")))
            .ToDictionary(d => Camel(Path.GetFileName(d)["Themia.Widget.".Length..]), d => d, StringComparer.Ordinal);

    private static Dictionary<string, string> ProviderFolders()
        => !Directory.Exists(Path.Combine(Src, "Providers"))
            ? []
            : Directory.EnumerateDirectories(Path.Combine(Src, "Providers"), "Themia.Provider.*")
                .Where(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj")))
                .ToDictionary(d => Camel(Path.GetFileName(d)["Themia.Provider.".Length..]), d => d, StringComparer.Ordinal);

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    public static TheoryData<string> Widgets() => [.. ModuleRegistry.WidgetIds];

    /// <summary>xUnit wants a row; a build with no module project yet has none, so a stand-in row is skipped.</summary>
    public static TheoryData<string> WidgetsOrNone() => ModuleRegistry.WidgetIds.Count > 0 ? Widgets() : ["(none)"];

    private static IWidgetModule Module(string id) => ModuleRegistry.Widgets.Single(m => m.Descriptor.Id.Value == id);

    [Fact]
    public void S6911_every_module_folder_is_in_the_registry_and_nothing_else_is()
    {
        Assert.Equal(WidgetFolders().Keys.Order(StringComparer.Ordinal), ModuleRegistry.WidgetIds.Order(StringComparer.Ordinal));

        // The providers here are the provider projects' and Core's own (declared in Core until their sign-in moves).
        var core = typeof(AppState).Assembly.GetCustomAttributes<ThemiaProviderAttribute>().Select(a => a.Id);
        Assert.Equal(ProviderFolders().Keys.Concat(core).Order(StringComparer.Ordinal), ModuleRegistry.ProviderIds.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(WidgetsOrNone))]
    public void S6911_its_name_and_description_are_its_own_and_said_in_english(string id)
    {
        if (id == "(none)")
        {
            return;
        }

        var widget = Module(id).Descriptor;
        Assert.Equal(id, widget.Id.Value);
        Assert.False(string.IsNullOrWhiteSpace(widget.Label.Text(Locale.En)), $"{id} has no English name");
        Assert.False(string.IsNullOrWhiteSpace(widget.Describe(Locale.En)), $"{id} has no English description");
        Assert.NotSame(Strings.Host, widget.Label.Table);
        Assert.Same(widget.Label.Table, widget.Description.Table);
        Assert.False(string.IsNullOrEmpty(widget.Glyph), $"{id} has no glyph");
        Assert.True(widget.DefaultWidth > 0 && widget.DefaultHeight > 0);
        Assert.True(Enum.IsDefined(widget.Add.Shelf) && widget.Add.Shelf is not (AddFolderId.Popular or AddFolderId.Browse or AddFolderId.More),
            $"{id} is at home in {widget.Add.Shelf}");
    }

    [Theory]
    [MemberData(nameof(WidgetsOrNone))]
    public void S6909_its_strings_are_whole_in_every_language_or_english_only_and_their_placeholders_match(string id)
    {
        if (id == "(none)")
        {
            return;
        }

        var table = Module(id).Descriptor.Label.Table!;
        var problems = new List<string>();
        for (var key = 0; key < table.Count; key++)
        {
            var english = table.Raw(Locale.En, key);
            if (string.IsNullOrWhiteSpace(english))
            {
                problems.Add($"{table.Key(key)}: no English");
                continue;
            }

            var placeholders = string.Join(",", Strings.Placeholders(english));
            foreach (var locale in Enum.GetValues<Locale>().Where(l => l != Locale.En))
            {
                var texts = table.RawForms(locale, key) is { } forms ? [.. forms.OfType<string>()] : table.Raw(locale, key) is { } one ? [one] : new List<string>();
                if (texts.Count == 0)
                {
                    if (!table.IsEnglishOnly(key))
                    {
                        problems.Add($"{table.Key(key)}: none in {locale}");
                    }

                    continue;
                }

                foreach (var text in texts)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        problems.Add($"{table.Key(key)}: empty in {locale}");
                    }
                    else if (string.Join(",", Strings.Placeholders(text)) != placeholders && !(table.RawForms(locale, key) is not null && placeholders.Contains("count", StringComparison.Ordinal)))
                    {
                        problems.Add($"{table.Key(key)}: {locale} has {{{string.Join(",", Strings.Placeholders(text))}}}, English {{{placeholders}}}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, $"{table.Name}:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// [S-1116] A module's texts whose Latin digits are not values but names (chart ranges, key names,
    /// formats), by widget id and key: the module's share of Core's allow list, kept beside the rule
    /// that runs over every module's table.
    /// </summary>
    private static readonly HashSet<(string Widget, string Key)> LatinDigitsAllowed =
    [
        ("stocks", "stocks.extendedHoursDesc"),   // the 1D and 5D chart ranges
    ];

    [Theory]
    [MemberData(nameof(WidgetsOrNone))]
    public void S1116_its_native_digit_prose_has_no_typed_latin_digits(string id)
    {
        if (id == "(none)")
        {
            return;
        }

        var table = Module(id).Descriptor.Label.Table!;
        var found = new List<string>();
        for (var key = 0; key < table.Count; key++)
        {
            if (LatinDigitsAllowed.Contains((id, table.Key(key))))
            {
                continue;
            }

            foreach (var locale in new[] { Locale.Ar, Locale.Fa })
            {
                var texts = table.RawForms(locale, key) is { } forms ? [.. forms.OfType<string>()] : table.Raw(locale, key) is { } one ? [one] : new List<string>();
                foreach (var text in texts)
                {
                    // Placeholders are filled at run time through LocaleFormat, in the language's own digits.
                    if (Regex.Replace(text, @"\{[^}]*\}", string.Empty).Any(char.IsAsciiDigit))
                    {
                        found.Add($"{table.Key(key)} ({locale}): {text}");
                    }
                }
            }
        }

        Assert.True(found.Count == 0, $"{table.Name}:\n" + string.Join('\n', found));
    }

    [Fact]
    public void S1116_the_modules_allow_list_names_keys_that_exist_and_still_need_it()
    {
        foreach (var (widget, key) in LatinDigitsAllowed)
        {
            Assert.Contains(widget, ModuleRegistry.WidgetIds);
            var table = Module(widget).Descriptor.Label.Table!;
            var at = Enumerable.Range(0, table.Count).Where(k => table.Key(k) == key).ToList();
            Assert.True(at.Count == 1, $"{widget}: {key} is not in its table");
            Assert.Contains(table.Raw(Locale.En, at[0])!, char.IsAsciiDigit);
        }
    }

    [Theory]
    [MemberData(nameof(WidgetsOrNone))]
    public void S6903_its_default_config_round_trips_through_the_state_under_its_id(string id)
    {
        if (id == "(none)")
        {
            return;
        }

        var widget = Module(id).Descriptor;
        var config = widget.CreateDefault();
        Assert.IsType(widget.ConfigType, config);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        Assert.Contains($"\"kind\": \"{id}\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType(widget.ConfigType, back);
        Assert.Equal(json, JsonSerializer.Serialize(back, StateJson.WidgetConfig));
        Assert.Equal(json, JsonSerializer.Serialize(config.Clone(), StateJson.WidgetConfig));

        // [S-6903] Written as a built-in config is: the host's options.
        var options = Assert.IsAssignableFrom<JsonSerializerContext>(widget.Json).Options;
        var host = StateJson.Options;
        Assert.Equal((host.PropertyNamingPolicy, host.DefaultIgnoreCondition, host.WriteIndented), (options.PropertyNamingPolicy, options.DefaultIgnoreCondition, options.WriteIndented));
    }

    [Fact]
    public void S6910_every_provider_a_widget_of_the_build_names_is_registered()
    {
        var unknown = ModuleCatalog.Widgets
            .SelectMany(w => w.Providers.Where(p => !ProviderCatalog.All.Any(m => m.Id == p)).Select(p => $"{w.Id}: {p}"))
            .ToList();

        Assert.Empty(unknown);
        Assert.Contains(ModuleCatalog.Widgets, w => w.Providers.Count > 0);
    }

    [Fact]
    public void S6907_no_two_widgets_of_the_build_share_a_place_in_the_add_menu()
    {
        var places = ModuleCatalog.Widgets.GroupBy(w => (w.Add.Shelf, w.Add.Group, w.Add.Rank)).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(w => w.Id))}");
        var popular = ModuleCatalog.Widgets.Where(w => w.Add.Popular is not null).GroupBy(w => w.Add.Popular).Where(g => g.Count() > 1)
            .Select(g => $"Popular {g.Key}: {string.Join(", ", g.Select(w => w.Id))}");
        var clashes = places.Concat(popular).ToList();
        Assert.True(clashes.Count == 0, string.Join(Environment.NewLine, clashes));
    }

    /// <summary>The assemblies a module may build on: the SDK, its WinUI half, and providers.</summary>
    private static bool MayUse(string assembly)
        => assembly is "Themia.Sdk" or "Themia.Sdk.WinUI"
            || assembly.StartsWith("Themia.Provider.", StringComparison.Ordinal)
            || !assembly.StartsWith("Themia", StringComparison.Ordinal);

    [Fact]
    public void S6911_a_module_builds_on_the_sdk_and_providers_only_never_on_core_the_app_or_another_widget()
    {
        var problems = new List<string>();
        foreach (var module in ModuleRegistry.Widgets.Select(m => (object)m).Concat(ModuleRegistry.Providers))
        {
            var assembly = module.GetType().Assembly;
            if (assembly == typeof(AppState).Assembly)
            {
                continue; // Core's own providers, until they move
            }

            problems.AddRange(assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(a => !MayUse(a)).Select(a => $"{assembly.GetName().Name} uses {a}"));
        }

        foreach (var folder in WidgetFolders().Values.Concat(ProviderFolders().Values))
        {
            var project = XDocument.Load(Path.Combine(folder, Path.GetFileName(folder) + ".csproj"));
            foreach (var include in project.Descendants("ProjectReference").Select(r => (string?)r.Attribute("Include") ?? string.Empty))
            {
                var target = Path.GetFullPath(Path.Combine(folder, include));
                if (!target.StartsWith(Path.Combine(Src, "Providers") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{Path.GetFileName(folder)} references {include}: a module's project references only provider projects (the SDK comes from Directory.Build.props)");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [GeneratedRegex(@"\*\*\[S-(\d+)\]\*\*")]
    private static partial Regex DefinedTag();

    [GeneratedRegex(@"public (?:async Task|void) S(\d+)_")]
    private static partial Regex TestTag();

    /// <summary>The root SPEC.md and every module's, each with the tags it defines.</summary>
    private static List<(string File, HashSet<int> Tags)> SpecFiles()
    {
        var files = new List<string> { Path.Combine(RepoFiles.Root, "SPEC.md") };
        files.AddRange(WidgetFolders().Values.Concat(ProviderFolders().Values).Select(d => Path.Combine(d, "SPEC.md")).Where(File.Exists));
        return [.. files.Select(f => (f, DefinedTag().Matches(File.ReadAllText(f)).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet()))];
    }

    [Fact]
    public void S6912_every_module_has_its_spec_section_whose_tags_are_its_own_and_its_tests_name_them()
    {
        var specs = SpecFiles();
        var problems = new List<string>();
        foreach (var folder in WidgetFolders().Values.Concat(ProviderFolders().Values))
        {
            var spec = Path.Combine(folder, "SPEC.md");
            if (!File.Exists(spec))
            {
                problems.Add($"{Path.GetFileName(folder)} has no SPEC.md");
                continue;
            }

            var own = specs.Single(s => s.File == spec).Tags;
            if (own.Count == 0)
            {
                problems.Add($"{Path.GetFileName(folder)}/SPEC.md defines no tag");
            }

            var tests = Path.Combine(folder, "Tests");
            var named = Directory.Exists(tests)
                ? Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories).SelectMany(f => TestTag().Matches(File.ReadAllText(f)).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)))
                : [];
            problems.AddRange(named.Distinct().Where(t => !own.Contains(t)).Select(t => $"{Path.GetFileName(folder)} tests S{t}, which its SPEC.md does not define"));
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void S6912_no_tag_is_defined_twice_across_the_spec_files()
    {
        var specs = SpecFiles();
        var twice = specs.SelectMany(s => s.Tags.Select(t => (t, s.File))).GroupBy(p => p.t).Where(g => g.Count() > 1)
            .Select(g => $"[S-{g.Key}] in {string.Join(", ", g.Select(p => Path.GetRelativePath(RepoFiles.Root, p.File)))}").ToList();
        Assert.True(twice.Count == 0, string.Join('\n', twice));
    }

    // ---- [S-6570] items, per project ----------------------------------------------------------

    /// <summary>What paints an item directly: the one path ([S-6513]) and Fluent's row helpers, which call it.</summary>
    [GeneratedRegex(@"\bItemFill\.|\bFluent\.(Row|Hover)\(")]
    private static partial Regex PaintsDirectly();

    [GeneratedRegex(@"^(?:internal |public )?(?:sealed |static |abstract )*(?:partial )?(?:class|struct|record(?: struct)?) (\w+)", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();

    /// <summary>
    /// The SDK's shared parts that paint items (a ring), by type name: those that paint directly, and
    /// those built of them (the meters' shared view lays out the bars and tiles that paint).
    /// </summary>
    private static HashSet<string> PaintingParts()
    {
        var skip = new HashSet<string>(StringComparer.Ordinal) { "ItemFill.cs", "Fluent.cs" };
        var files = Directory.EnumerateFiles(Path.Combine(Src, "Themia.Sdk.WinUI", "Widgets"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !skip.Contains(Path.GetFileName(f)))
            .Select(f => (Text: Code(f), Types: TopLevelType().Matches(Code(f)).Select(m => m.Groups[1].Value).ToList()))
            .ToList();
        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (text, types) in files.Where(f => PaintsDirectly().IsMatch(f.Text)))
        {
            parts.UnionWith(types);
        }

        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var (text, types) in files.Where(f => !f.Types.All(parts.Contains)))
            {
                if (parts.Any(p => !types.Contains(p) && Regex.IsMatch(text, $@"\b{p}\b")))
                {
                    parts.UnionWith(types);
                    grew = true;
                }
            }
        }

        return parts;
    }

    private static string Code(string file) => string.Join('\n', File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [Theory]
    [MemberData(nameof(WidgetsOrNone))]
    public void S6570_its_traits_say_it_has_items_exactly_when_its_view_paints_them(string id)
    {
        if (id == "(none)")
        {
            return;
        }

        var folder = WidgetFolders()[id];
        var tests = Path.Combine(folder, "Tests") + Path.DirectorySeparatorChar;
        var code = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(tests, StringComparison.OrdinalIgnoreCase) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(Code).ToList();
        var parts = PaintingParts();
        var paints = code.Any(c => PaintsDirectly().IsMatch(c) || parts.Any(p => Regex.IsMatch(c, $@"\b{p}\b")));

        Assert.Equal(paints, Module(id).Descriptor.Traits.HasFlag(WidgetTraits.HasItems));
    }
}
