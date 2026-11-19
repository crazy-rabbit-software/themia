using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Header.Tests;

/// <summary>[S-9600]..[S-9603] The Header widget's rules.</summary>
public sealed class HeaderTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("header"));

    [Fact]
    public void S9600_a_clone_keeps_title_glyph_and_caption()
    {
        var config = new HeaderConfig { Text = "Weather", Glyph = "", Caption = "Zürich" };
        var copy = Assert.IsType<HeaderConfig>(config.Clone());
        Assert.NotSame(config, copy);
        Assert.Equal((config.Text, config.Glyph, config.Caption), (copy.Text, copy.Glyph, copy.Caption));
    }

    [Fact]
    public void S9600_repair_keeps_one_short_line_and_drops_a_glyph_that_is_not_one()
    {
        var config = new HeaderConfig { Text = " My\r\nday ", Caption = new string('x', 120), Glyph = "AB" };
        Assert.Equal(3, config.Repair().Count);
        Assert.Equal("My day", config.Text);
        Assert.Equal(HeaderLayout.MostText, config.Caption.Length);
        Assert.Equal(string.Empty, config.Glyph);

        var fine = new HeaderConfig { Text = "This PC", Glyph = "" };
        Assert.Empty(fine.Repair());
    }

    [Fact]
    public void S9601_a_thin_strip_as_a_cards_header_was_and_one_caption_line_taller_with_one()
    {
        var plain = Kind.SizingOf(new HeaderConfig { Text = "Weather" });
        Assert.Equal(FamilySpace.Inset + FamilySpace.HeaderLine + FamilySpace.HeaderGap, plain.Natural.Height);
        Assert.Equal(36, plain.Natural.Height);
        Assert.Equal((HeaderLayout.Narrowest, HeaderLayout.NaturalWidth), (plain.Min.Width, plain.Natural.Width));

        var captioned = Kind.SizingOf(new HeaderConfig { Text = "Weather", Caption = "Zürich" });
        Assert.Equal(plain.Natural.Height + FamilyType.Caption.LineHeight, captioned.Natural.Height);
    }

    [Fact]
    public void S9602_title_and_caption_are_typed_in_place_S9604_and_the_place_is_its_location_alone()
    {
        var saves = 0;
        var asked = 0;
        var header = new HeaderConfig { Text = "Weather" };
        var menu = new Themia.Sdk.WinUI.WidgetMenuLines(() => saves++, _ => asked++);
        new HeaderModule().MenuLines(menu, header);

        // Title, Caption, Icon; then the Location alone: no Show the place, no Use my location (owner, 2026-11-19).
        var lines = menu.Lines.Where(l => !l.IsSeparator).ToList();
        Assert.Equal(4, lines.Count);
        var title = lines[0].Field!;
        var caption = lines[1].Field!;
        Assert.NotNull(lines[2].Icon);
        Assert.NotNull(lines[3].Search);
        Assert.Equal(SharedSettings.Place.Key, lines[3].Shares);
        Assert.True(menu.Lines[3].IsSeparator);

        Assert.Equal("Weather", title.Current);
        title.Keep(" Weather now ");
        caption.Keep("Zürich");
        title.Keep("Weather");
        Assert.Equal(("Weather now", "Zürich"), (header.Text, header.Caption));
        Assert.Equal(2, saves);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void S9600_it_aligns_its_block_and_starts_on_a_card_like_any_widget()
    {
        Assert.IsAssignableFrom<IContentAligned>(new HeaderConfig());
        Assert.Equal(WidgetTraits.None, Kind.Traits);
        Assert.False(HeaderLayout.IsGlyph("ab"));
        Assert.True(HeaderLayout.IsGlyph(string.Empty));
        Assert.True(HeaderLayout.IsGlyph(""));
    }
}
