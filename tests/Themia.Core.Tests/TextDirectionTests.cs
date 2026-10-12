using Themia.Core.Localization;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1113] / [S-1114] Which languages run right to left, which icons turn with them, and the layout tags.</summary>
public sealed class TextDirectionTests
{
    [Fact]
    public void S1113_arabic_and_urdu_run_right_to_left_and_nothing_else_does()
    {
        var rtl = LanguageResolver.All.Where(TextDirection.IsRightToLeft).ToList();
        Assert.Equal([Locale.Ar, Locale.Ur], rtl);
    }

    [Theory]
    [InlineData('')] // back
    [InlineData('')] // forward
    [InlineData('')] // chevron left
    [InlineData('')] // chevron right
    [InlineData('')] // send
    [InlineData('')] // reply
    [InlineData('')] // undo
    [InlineData('')] // bulleted list
    public void S1113_directional_glyphs_mirror(char glyph)
        => Assert.True(TextDirection.MirrorsGlyph(glyph.ToString()));

    [Theory]
    [InlineData('')] // play: media transport keeps its direction
    [InlineData('')] // previous track
    [InlineData('')] // next track
    [InlineData('')] // add: symmetric
    [InlineData('')] // clock: a clock face never mirrors
    [InlineData('')] // chevron down: vertical
    public void S1113_media_symmetric_and_vertical_glyphs_do_not(char glyph)
        => Assert.False(TextDirection.MirrorsGlyph(glyph.ToString()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("")]
    [InlineData("Back")]
    public void S1113_anything_that_is_not_one_glyph_does_not_mirror(string? glyph)
        => Assert.False(TextDirection.MirrorsGlyph(glyph));

    [Theory]
    [InlineData(Locale.En, "en")]
    [InlineData(Locale.Ja, "ja")]
    [InlineData(Locale.Zh, "zh-Hans")]
    [InlineData(Locale.Pt, "pt-BR")]
    [InlineData(Locale.Hi, "hi-IN")]
    [InlineData(Locale.Mr, "mr-IN")]
    [InlineData(Locale.Bn, "bn-IN")]
    [InlineData(Locale.Ar, "ar-SA")]
    [InlineData(Locale.Ur, "ur-PK")]
    [InlineData(Locale.Ru, "ru-RU")]
    [InlineData(Locale.Vi, "vi-VN")]
    [InlineData(Locale.Id, "id-ID")]
    public void S1114_layout_tags_carry_the_region_the_font_fallback_needs(Locale locale, string tag)
        => Assert.Equal(tag, TextDirection.LayoutTag(locale));

    [Fact]
    public void S1114_every_layout_tag_names_its_own_language()
    {
        foreach (var locale in LanguageResolver.All)
        {
            Assert.Equal(locale, LanguageResolver.FromTag(TextDirection.LayoutTag(locale)));
        }
    }
}
