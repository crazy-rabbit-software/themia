using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6902] The registry is the one list of widgets: each check iterates it, so a widget that misses
/// a part fails here by name instead of falling back to another widget's.
/// </summary>
public sealed partial class WidgetRegistryTests
{
    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

    public static TheoryData<string> Ids() => [.. ModuleCatalog.Widgets.Select(w => w.Id.Value)];

    private static WidgetDescriptor Of(string id) => ModuleCatalog.TryGet(new WidgetId(id), out var w) ? w : throw new KeyNotFoundException(id);

    [Fact]
    public void S6902_the_registry_holds_each_widget_once_in_id_order()
    {
        Assert.Empty(ModuleCatalog.Check([.. ModuleCatalog.Widgets]));
        Assert.Equal(ModuleCatalog.Widgets.Select(w => w.Id).Order(), ModuleCatalog.Widgets.Select(w => w.Id));
    }

    [Fact]
    public void S6902_a_registry_with_a_missing_descriptor_or_an_id_or_kind_given_twice_is_refused()
    {
        var clock = ClockConfig.Descriptor;
        var cpu = CpuConfig.Descriptor;

        Assert.Empty(ModuleCatalog.Check([clock, cpu]));
        Assert.Single(ModuleCatalog.Check([clock, null]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { Id = new("clock") }]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { ConfigType = typeof(ClockConfig) }]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { Id = default }]));
        Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Install([clock]));
    }

    [Fact]
    public void S6902_every_concrete_config_declares_a_descriptor()
    {
        var configs = typeof(AppState).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(WidgetConfig)) && !t.IsAbstract);
        Assert.Equal(configs.Select(t => t.Name).Order(), TestCatalog.Declared.Select(d => d.Config.Name).Order());
    }

    [Fact]
    public void S6902_the_app_declares_one_module_for_each_descriptor_and_no_other()
    {
        var declared = Directory.EnumerateFiles(Path.Combine(Root(), "src", "Themia.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => ThemiaWidget().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToList();

        Assert.Equal(ModuleCatalog.Widgets.Select(w => w.Id.Value).Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6902_a_descriptors_kind_is_its_configs_and_its_id_the_configs_discriminator(string id)
    {
        var widget = Of(id);
        var (config, _) = TestCatalog.Declared.Single(d => d.Descriptor == widget);
        var instance = WidgetConfig.CreateDefault(widget.Id);

        Assert.IsType(config, instance);
        Assert.Equal(widget.Id, instance.Kind);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(instance, StateJson.WidgetConfig));
        Assert.Equal(id, json.RootElement.GetProperty("kind").GetString());
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6902_every_widget_has_a_name_a_description_a_glyph_and_a_default_size(string id)
    {
        var widget = Of(id);

        Assert.False(string.IsNullOrWhiteSpace(widget.Label.Text(Locale.En)));
        Assert.False(string.IsNullOrWhiteSpace(widget.Describe(Locale.En)));
        Assert.DoesNotContain("{", widget.Describe(Locale.En), StringComparison.Ordinal);
        Assert.Single(widget.Glyph.EnumerateRunes());
        Assert.InRange(widget.DefaultWidth, 80, 800);
        Assert.InRange(widget.DefaultHeight, 80, 800);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6903_a_default_config_round_trips_through_the_state_options_with_its_id_as_discriminator(string id)
    {
        var widget = Of(id);
        var config = WidgetConfig.CreateDefault(widget.Id);
        Assert.IsType(widget.ConfigType, config);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        using (var document = JsonDocument.Parse(json))
        {
            Assert.Equal(id, document.RootElement.GetProperty("kind").GetString());
        }

        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType(widget.ConfigType, back);
        Assert.Equal(json, JsonSerializer.Serialize(back!, StateJson.WidgetConfig));
    }

    [Fact]
    public void S6903_the_union_holds_exactly_the_registrys_configs()
    {
        var union = StateJson.Options.GetTypeInfo(typeof(WidgetConfig)).PolymorphismOptions!;

        Assert.Equal("kind", union.TypeDiscriminatorPropertyName);
        Assert.Equal(
            ModuleCatalog.Widgets.Select(w => (w.ConfigType, (object)w.Id.Value)).OrderBy(p => p.Item2),
            union.DerivedTypes.Select(d => (d.DerivedType, d.TypeDiscriminator!)).OrderBy(p => p.Item2));
    }

    [Fact]
    public void S6903_every_widget_resolver_is_written_with_the_host_options()
    {
        foreach (var resolver in ModuleCatalog.Widgets.Select(w => w.Json).Distinct())
        {
            var options = Assert.IsAssignableFrom<System.Text.Json.Serialization.JsonSerializerContext>(resolver).Options;
            var host = StateJson.Options;
            Assert.Equal(host.PropertyNamingPolicy?.ConvertName("someName"), options.PropertyNamingPolicy?.ConvertName("someName"));
            Assert.Equal(host.DefaultIgnoreCondition, options.DefaultIgnoreCondition);
            Assert.Equal(host.WriteIndented, options.WriteIndented);
        }
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6905_only_a_widget_that_may_have_a_header_starts_without_one(string id)
    {
        var traits = Of(id).Traits;
        Assert.False(traits.HasFlag(WidgetTraits.StartsHeaderless) && !traits.HasFlag(WidgetTraits.CanHaveHeader), $"{id}: {traits}");
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6906_a_widget_that_reads_an_account_has_a_setup_rule_and_interests(string id)
    {
        var widget = Of(id);
        if (widget.ProviderNames.Count > 0)
        {
            Assert.True(typeof(ISetupRule).IsAssignableFrom(widget.ConfigType), $"{id} reads {string.Join(", ", widget.ProviderNames)} but never waits for setup");
            Assert.True(WidgetSetup.Needed(widget.CreateDefault(), Themia.Sdk.FeatureSet.None), $"{id} does not wait for an account");
            Assert.True(typeof(Themia.Core.Sync.IInterests).IsAssignableFrom(widget.ConfigType), $"{id} reads an account the sync never fetches for it");
        }
    }

    [Fact]
    public void S6907_every_widget_has_a_place_of_its_own_in_the_add_menu()
    {
        var widgets = ModuleCatalog.Widgets;

        Assert.All(widgets, w => Assert.DoesNotContain(w.Add.Shelf, new[] { AddFolderId.Popular, AddFolderId.Browse, AddFolderId.More }));
        Assert.Equal(widgets.Count, widgets.Select(w => (w.Add.Shelf, w.Add.Group, w.Add.Rank)).Distinct().Count());
        var popular = widgets.Where(w => w.Add.Popular is not null).Select(w => w.Add.Popular!.Value).ToList();
        Assert.Equal(Enumerable.Range(0, popular.Count), popular.Order());
        Assert.Equal(widgets.Count, AddTree.Homes(AddTree.On([.. widgets.Select(w => w.Id)])).Count);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S943_every_widget_has_its_own_search_words(string id)
    {
        var widget = Of(id);

        Assert.NotEmpty(widget.Keywords);
        Assert.All(widget.Keywords, word => Assert.Equal(word.Trim().ToLowerInvariant(), word));
        Assert.Equal(widget.Keywords.Count, widget.Keywords.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// [S-6904] A widget's settings rows are its module's. A page written but wired to no module was
    /// a silent miss of the old dispatch switch; here it fails by name, and so does a module that
    /// names a page that is not there.
    /// </summary>
    [Fact]
    public void S6904_every_widget_settings_page_is_built_by_a_module_and_every_module_page_exists()
    {
        var config = Path.Combine(Root(), "src", "Themia.App", "Shell", "Config");
        var pages = Directory.GetFiles(config, "WidgetSettings*.cs")
            .SelectMany(f => SettingsPage().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var used = Directory.GetFiles(ModuleSources.ModulesFolder, "*.cs")
            .SelectMany(f => ModulePage().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(pages);
        Assert.Empty(pages.Except(used));
        Assert.Empty(used.Except(pages));
        Assert.DoesNotContain("switch (widget.Config)", File.ReadAllText(Path.Combine(config, "ConfigPages.Widget.cs")), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?:public|internal) static void (\w+)\(List<FrameworkElement> rows, \w+ \w+, Action save")]
    private static partial Regex SettingsPage();

    [GeneratedRegex(@"WidgetSettings\.(\w+)\(rows\.Rows")]
    private static partial Regex ModulePage();

    [GeneratedRegex("""ThemiaWidget\("([^"]*)"\s*,""")]
    private static partial Regex ThemiaWidget();
}
