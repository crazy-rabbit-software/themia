using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Icon.Tests;

/// <summary>[S-15010]..[S-15012] The Icon widget: an icon kind, its pick, its repair.</summary>
public sealed class IconTests
{
    [Fact]
    public void S15010_it_is_an_icon_kind_under_notes_beside_the_header_and_opens_nothing()
    {
        var module = new IconWidgetModule();

        Assert.IsAssignableFrom<IconModule>(module);
        Assert.Equal("icon", module.Descriptor.Id.Value);
        Assert.Equal(IconRules.Sizing().Natural, module.Descriptor.Natural);
        Assert.Equal(AddFolderId.Notes, module.Descriptor.Add.Shelf);
        Assert.Null(module.Kind.Opens);
    }

    [Fact]
    public void S15011_a_new_one_shows_the_star_and_a_pick_shows_the_glyph_picked()
    {
        Assert.Equal(Glyphs.Important, IconChoice.Drawn(new IconWidgetConfig().Glyph, IconWidgetModule.Default));
        Assert.Equal(Glyphs.Sun, IconChoice.Drawn(Glyphs.Sun, IconWidgetModule.Default));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("", true)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData("", false)]
    [InlineData("\U000F0001", true)]
    [InlineData("\U0001F600", false)]
    public void S15011_a_glyph_is_one_character_of_the_icon_font(string glyph, bool one)
        => Assert.Equal(one, IconChoice.IsGlyph(glyph));

    [Fact]
    public void S15011_a_stored_glyph_that_is_no_icon_is_cleared_loudly_and_a_copy_keeps_the_pick()
    {
        var bad = new IconWidgetConfig { Glyph = "hello" };
        Assert.Single(bad.Repair());
        Assert.Equal(string.Empty, bad.Glyph);

        var good = new IconWidgetConfig { Glyph = Glyphs.Sun };
        Assert.Empty(good.Repair());
        Assert.Equal(Glyphs.Sun, ((IconWidgetConfig)good.Clone()).Glyph);
        Assert.NotSame(good, good.Clone());
    }
}
