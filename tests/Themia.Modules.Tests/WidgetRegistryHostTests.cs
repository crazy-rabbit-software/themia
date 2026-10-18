using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6902] The registry is the one list of widgets: each check iterates every widget of the build,
/// so a widget that misses a part fails here by name instead of falling back to another widget's.
/// [S-6907] Every widget has a place of its own in the add menu, and Popular's places run without a gap.
/// </summary>
public sealed class WidgetRegistryHostTests
{
    public static TheoryData<string> Ids() => [.. ModuleRegistry.WidgetIds];

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
        // Two descriptors of the build would do; the widgets are modules now, so two made up here.
        var email = ModuleCatalog.Of(new WidgetId("tasks")) with { Id = new WidgetId("first"), ConfigType = typeof(FirstConfig), CreateDefault = () => new FirstConfig() };
        var tasks = ModuleCatalog.Of(new WidgetId("tasks")) with { Id = new WidgetId("second"), ConfigType = typeof(SecondConfig), CreateDefault = () => new SecondConfig() };

        Assert.Empty(ModuleCatalog.Check([email, tasks]));
        Assert.Single(ModuleCatalog.Check([email, null]));
        Assert.Single(ModuleCatalog.Check([email, tasks with { Id = email.Id }]));
        Assert.Single(ModuleCatalog.Check([email, tasks with { ConfigType = typeof(FirstConfig) }]));
        Assert.Single(ModuleCatalog.Check([email, tasks with { Id = default }]));
        Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Install([email]));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void S6902_a_descriptors_kind_is_its_configs_and_its_id_the_configs_discriminator(string id)
    {
        var widget = Of(id);
        var instance = WidgetConfig.CreateDefault(widget.Id);

        Assert.IsType(widget.ConfigType, instance);
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
        if (widget.Providers.Count > 0)
        {
            Assert.True(typeof(ISetupRule).IsAssignableFrom(widget.ConfigType), $"{id} reads {string.Join(", ", widget.Providers)} but never waits for setup");
            Assert.True(WidgetSetup.Needed(widget.CreateDefault(), Themia.Sdk.FeatureSet.None), $"{id} does not wait for an account");
            Assert.True(typeof(Themia.Core.Sync.IInterests).IsAssignableFrom(widget.ConfigType), $"{id} reads an account the sync never fetches for it");
        }
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

    /// <summary>
    /// [S-6907] Regression: a folder no widget of the build is at home in (here every power button
    /// left out) is not in the tree; building the tree threw "an add menu folder with no widget in it".
    /// </summary>
    [Fact]
    public void S6907_a_folder_no_widget_of_the_build_is_at_home_in_is_not_in_the_tree()
    {
        static IEnumerable<AddShelf> Folders(AddShelf shelf) => shelf.Rows.OfType<AddShelf>().SelectMany(inner => Folders(inner).Prepend(inner));

        var widgets = ModuleCatalog.Widgets.Where(w => w.Add.Shelf != AddFolderId.PowerButtons).ToList();
        var (popular, browse) = AddTree.Build(widgets);
        var folders = new[] { popular, browse }.SelectMany(Folders).ToList();

        Assert.DoesNotContain(folders, folder => folder.Id == AddFolderId.PowerButtons);
        Assert.All(folders, folder => Assert.Contains(folder.Rows, row => row is AddLeaf or AddShelf));
        var at = widgets.Select(w => w.Add.Shelf).ToHashSet();
        Assert.All(folders, folder => Assert.True(at.Contains(folder.Id) || folder.Rows.OfType<AddShelf>().Any(), $"{folder.Id} holds no widget"));
    }

    private sealed class FirstConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new FirstConfig();
    }

    private sealed class SecondConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new SecondConfig();
    }
}
