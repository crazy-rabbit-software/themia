using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.DesktopItem;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// The desktop icon kind under the root's tags: the key its config is selected under ([S-5540]) and how
/// Add widget offers it ([S-5523]). They need the module's config, so they run here.
/// </summary>
public sealed class DesktopIconKindTests
{
    [Fact]
    public void S5540_an_own_icons_key_is_its_id_and_never_a_desktop_items()
    {
        var own = new DesktopItemConfig();
        var key = DesktopItemKeys.Of(own);

        Assert.Equal(DesktopItemKeys.Own(own.ItemId), key);
        Assert.True(DesktopItemKeys.IsOwn(key));
        Assert.True(DesktopItemKeys.IsValid(key));
        Assert.False(DesktopItemKeys.IsShellItem(key));
        Assert.False(Path.IsPathRooted(key));

        // Stable for the icon's life, its own for a copy, and a desktop item's key is the key itself.
        Assert.Equal(key, DesktopItemKeys.Of(own));
        Assert.NotEqual(key, DesktopItemKeys.Of((DesktopItemConfig)own.Clone()));
        Assert.Equal(@"C:\a.txt", DesktopItemKeys.Of(new DesktopItemConfig { Item = @"C:\a.txt" }));
        Assert.False(DesktopItemKeys.IsOwn(@"C:\a.txt"));
        Assert.False(DesktopItemKeys.IsOwn("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a b")]
    [InlineData("a:b")]
    [InlineData(@"a\b")]
    public void S5540_an_id_that_could_not_make_a_key_is_replaced_loudly(string id)
    {
        var config = new DesktopItemConfig { ItemId = id, Target = "https://a.example/" };

        Assert.Single(config.Repair(), r => r.Contains("id", StringComparison.Ordinal));
        Assert.True(DesktopItemKeys.IsOwnId(config.ItemId));
    }

    [Fact]
    public void S5540_an_own_icons_key_stored_as_a_desktop_item_is_no_key()
    {
        var config = new DesktopItemConfig { Item = DesktopItemKeys.Own("abc") };

        Assert.Single(config.Repair());
        Assert.False(config.IsDesktopItem);
    }

    [Fact]
    public void S5523_the_kind_is_offered_as_shortcut_in_files_and_starts_on_no_card()
    {
        var kind = ModuleCatalog.Of(new WidgetId("desktopItem"));

        // [S-11709] Its tile is Shortcut (it makes an own icon); the kind itself is a desktop icon.
        Assert.Equal("Shortcut", kind.AddLabel!.Value.Text(Locale.En));
        Assert.Equal("Desktop icon", kind.Label.Text(Locale.En));
        Assert.Equal(AddFolderId.Files, kind.Add!.Shelf);
        Assert.True(kind.Traits.HasFlag(WidgetTraits.StartsTransparent));
    }
}
