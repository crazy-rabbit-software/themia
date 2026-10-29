using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6738] A translucent text role as the opaque colour it shows on the card, for a control that drops alpha.</summary>
public class HexColorOverTests
{
    [Fact]
    public void S6738_an_opaque_colour_is_itself()
        => Assert.Equal("#C42B1C", HexColor.Over(255, 0xC4, 0x2B, 0x1C, "#FBFBFB"));

    [Fact]
    public void S6738_a_clear_colour_is_what_lies_under_it()
        => Assert.Equal("#2B2B2B", HexColor.Over(0, 0xFF, 0xFF, 0xFF, "#2B2B2B"));

    [Fact]
    public void S6738_a_translucent_colour_mixes_with_the_card_by_its_alpha()
    {
        // The dark theme's secondary text (white at 0xC5) on the dark card.
        Assert.Equal("#CFCFCF", HexColor.Over(0xC5, 0xFF, 0xFF, 0xFF, "#2B2B2B"));
        Assert.Equal("#808080", HexColor.Over(128, 0xFF, 0xFF, 0xFF, "#000000"));
    }

    [Fact]
    public void S6738_no_card_colour_leaves_the_colour_opaque()
        => Assert.Equal("#FFFFFF", HexColor.Over(0x61, 0xFF, 0xFF, 0xFF, "not a colour"));
}
