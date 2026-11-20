using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14322] A folded icon picker's head shows what it holds (owner, 2026-11-21: "the icon in the icon selector
/// configuration section does not show when it is not extended").
/// </summary>
public sealed class IconHeadTests
{
    [Fact]
    public void S14322_a_chosen_icon_is_its_heads_picture_and_says_nothing_more()
        => Assert.Equal(new IconHead("", null), IconHead.Of("", "Initials", "WK"));

    [Theory]
    [InlineData(null, "Initials", "WK", "Initials (WK)")]
    [InlineData("", "Initials", "WK", "Initials (WK)")]
    [InlineData(null, "Its own icon", null, "Its own icon")]
    [InlineData(null, "Its own icon", "", "Its own icon")]
    public void S14322_the_default_is_said_as_its_cell_in_the_picker_says_it(string? current, string name, string? text, string words)
        => Assert.Equal(new IconHead(null, words), IconHead.Of(current, name, text));

    [Fact]
    public void S14322_a_views_icon_line_heads_with_its_chosen_glyph()
    {
        var line = new SettingsIcon("views/v/icon", "Icon", "", "", "WK", "Initials", _ => { });
        Assert.Equal("", line.Head.Glyph);
        Assert.Null(line.Head.Words);
    }
}
