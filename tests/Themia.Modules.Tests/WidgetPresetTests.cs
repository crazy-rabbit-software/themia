using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16680]..[S-16687] The presets mechanism over made-up kinds: one kind offered as several widgets of its own on the add
/// page's tree, each with its own place, and the registry's check of their keys. What the add page, the gallery, the
/// search and placing make of a real kind's presets is the quick setting's (<c>QuickToggleTests</c> in its module).
/// </summary>
public sealed class WidgetPresetTests
{
    private static WidgetDescriptor Clock => ModuleCatalog.Of(new WidgetId("clock"));

    private static WidgetPreset Preset(string key, int rank, AddFolderId shelf = AddFolderId.QuickSettings, int? popular = null) => new()
    {
        Key = key,
        Label = LocalText.Fixed(key + " name"),
        Description = LocalText.Fixed(key + " caption"),
        Glyph = Themia.App.Shell.Glyphs.Wifi,
        Keywords = [key + "word"],
        Add = new AddPlacement(shelf, 0, rank, popular),
        Apply = _ => { },
        Holds = _ => false,
    };

    /// <summary>A kind no build has, offered as <paramref name="presets"/>.</summary>
    private static WidgetDescriptor Kind(params WidgetPreset[] presets) => Clock with
    {
        Id = new WidgetId("madeUp"),
        ConfigType = typeof(MadeUpConfig),
        CreateDefault = () => new MadeUpConfig(),
        Add = new AddPlacement(AddFolderId.QuickSettings, 9, 9),
        Presets = presets,
    };

    [Fact]
    public void S16681_a_kind_with_presets_is_one_leaf_per_preset_in_its_place_and_never_a_leaf_of_its_own()
    {
        var kind = Kind(Preset("second", 1), Preset("first", 0));
        var (_, browse) = AddTree.Build([.. ModuleCatalog.Widgets, kind]);

        var quick = browse.Rows.OfType<AddShelf>().Single(s => s.Id == AddFolderId.QuickSettings);
        var mine = quick.Rows.OfType<AddLeaf>().Where(l => l.Kind == kind.Id).ToList();
        Assert.Equal(["first", "second"], mine.Select(l => l.Preset));
        Assert.All(mine, leaf => Assert.Equal(new WidgetPick(kind.Id, leaf.Preset), leaf.Pick));
        Assert.DoesNotContain(mine, leaf => leaf.Preset is null);
    }

    [Fact]
    public void S16681_a_preset_may_be_popular_on_its_own()
    {
        var kind = Kind(Preset("plain", 0), Preset("liked", 1, popular: 999));
        var (popular, _) = AddTree.Build([.. ModuleCatalog.Widgets, kind]);

        var leaf = Assert.Single(popular.Rows.OfType<AddLeaf>(), l => l.Kind == kind.Id);
        Assert.Equal("liked", leaf.Preset);
        Assert.True(leaf.Again);
    }

    [Fact]
    public void S16681_a_kind_without_presets_is_its_own_one_pick()
    {
        Assert.Equal([new WidgetPick(Clock.Id)], Clock.Picks);
        Assert.Equal("clock", new WidgetPick(Clock.Id).Key);
        Assert.Equal("kind:clock", new AddWidgetTile(Clock.Id).Key);
        Assert.Equal("kind:madeUp/first", new AddWidgetTile(new WidgetId("madeUp"), "first").Key);
    }

    [Fact]
    public void S16682_a_pick_is_named_by_its_kind_and_preset_with_a_slash()
    {
        Assert.Equal(("quickToggle", "wifi"), WidgetPick.Split("quickToggle/wifi"));
        Assert.Equal(("clock", (string?)null), WidgetPick.Split("clock"));
        Assert.Equal("madeUp/first", new WidgetPick(new WidgetId("madeUp"), "first").Key);

        Assert.True(WidgetPick.TryParse("clock", out var clock));
        Assert.Equal(new WidgetPick(Clock.Id), clock);

        // A kind this build does not have, or a preset its kind does not have, is no pick.
        Assert.False(WidgetPick.TryParse("clock/digital", out _));
        Assert.False(WidgetPick.TryParse("madeUp/first", out _));
        Assert.False(WidgetPick.TryParse("", out _));
        Assert.False(WidgetPick.TryParse("/wifi", out _));
    }

    [Fact]
    public void S16687_the_registry_refuses_a_preset_key_given_twice_or_not_a_word()
    {
        Assert.Empty(ModuleCatalog.Check([Kind(Preset("one", 0), Preset("two", 1))]));
        Assert.Contains(ModuleCatalog.Check([Kind(Preset("one", 0), Preset("one", 1))]), p => p.Contains("twice", StringComparison.Ordinal));
        Assert.Contains(ModuleCatalog.Check([Kind(Preset("a/b", 0))]), p => p.Contains("bad key", StringComparison.Ordinal));
        Assert.Contains(ModuleCatalog.Check([Kind(Preset("", 0))]), p => p.Contains("bad key", StringComparison.Ordinal));
    }

    private sealed class MadeUpConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new MadeUpConfig();
    }
}
