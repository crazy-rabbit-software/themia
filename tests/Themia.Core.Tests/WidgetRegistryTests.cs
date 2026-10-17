using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Core.Model;
using Themia.Core.Storage;
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
    public void S6902_every_kind_has_exactly_one_descriptor()
    {
        Assert.Empty(ModuleCatalog.Check([.. ModuleCatalog.Widgets]));
        Assert.Equal(Enum.GetValues<WidgetKind>().Order(), ModuleCatalog.Widgets.Select(w => w.Kind).Order());
    }

    [Fact]
    public void S6902_a_registry_with_a_missing_descriptor_or_an_id_or_kind_given_twice_is_refused()
    {
        var clock = new WidgetDescriptor { Id = new("clock"), Kind = WidgetKind.Clock };
        var cpu = new WidgetDescriptor { Id = new("cpu"), Kind = WidgetKind.Cpu };

        Assert.Empty(ModuleCatalog.Check([clock, cpu]));
        Assert.Single(ModuleCatalog.Check([clock, null]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { Id = new("clock") }]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { Kind = WidgetKind.Clock }]));
        Assert.Single(ModuleCatalog.Check([clock, cpu with { Id = default }]));
        Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Install([clock]));
    }

    [Fact]
    public void S6902_every_concrete_config_declares_a_descriptor()
    {
        var configs = typeof(WidgetConfig).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(WidgetConfig)) && !t.IsAbstract);
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
        var instance = WidgetConfig.CreateDefault(widget.Kind);

        Assert.IsType(config, instance);
        Assert.Equal(widget.Kind, instance.Kind);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(instance, AppStateJsonContext.Default.WidgetConfig));
        Assert.Equal(id, json.RootElement.GetProperty("kind").GetString());
    }

    [GeneratedRegex("""ThemiaWidget\("([^"]*)"\s*,""")]
    private static partial Regex ThemiaWidget();
}
