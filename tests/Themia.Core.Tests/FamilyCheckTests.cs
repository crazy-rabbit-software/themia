using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6731] The render walk's rules over the facts it collects.</summary>
public sealed class FamilyCheckTests
{
    private const string Accent = "#FF0067C0";

    private static readonly Dictionary<string, string> Palette = new(StringComparer.Ordinal)
    {
        ["#E4000000"] = "TextPrimary",
        ["#9E000000"] = "TextSecondary",
        [Accent] = "Accent",
    };

    private static List<string> Check(FamilyFact fact, bool oneReadout = false) => [.. FamilyCheck.Findings(fact, oneReadout, Palette, Accent)];

    [Fact]
    public void S6731_a_text_on_a_family_step_in_a_role_is_clean()
    {
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 14, 20, 600, Colours: [new("fg", "#E4000000")])));
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 40, 0, 600)));
    }

    [Fact]
    public void S6731_an_off_ramp_size_line_or_weight_is_a_finding()
    {
        Assert.Equal(["text-size 18"], Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 18, 24)));
        Assert.Equal(["text-line 24 on 14"], Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 14, 24)));
        Assert.Equal(["text-weight 300"], Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 14, 20, 300)));
        Assert.Equal(["text-size 96"], Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 96, 128)));
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 96, 128), oneReadout: true));
    }

    [Fact]
    public void S6731_glyphs_lengths_radii_and_strokes_are_held_to_the_tokens()
    {
        Assert.Equal(["glyph-size 12"], Check(new FamilyFact(FamilyPartType.Glyph, "FontIcon", 12)));
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Glyph, "FontIcon", 40)));

        // [S-2220] 68 is a Hero step now: a data widget's glyph never takes it.
        Assert.Equal(["glyph-size 68"], Check(new FamilyFact(FamilyPartType.Glyph, "FontIcon", 68)));

        Assert.Equal(["space margin 6", "space padding 2.5"], Check(new FamilyFact(FamilyPartType.Box, "Border", Lengths: [("margin", 6), ("padding", 2.5), ("spacing", 8), ("margin", -4)])));
        Assert.Equal(["radius 6"], Check(new FamilyFact(FamilyPartType.Box, "Border", Radii: [6, 6, 4, 0], Width: 100, Height: 32)));
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Box, "Border", Radii: [8], Width: 40, Height: 16)));
        Assert.Equal(["stroke 3"], Check(new FamilyFact(FamilyPartType.Shape, "Path", Stroke: 3)));
    }

    [Fact]
    public void S6731_a_colour_off_the_roles_or_text_in_the_accent_is_a_finding()
    {
        Assert.Equal(["colour fill #FF00FF00"], Check(new FamilyFact(FamilyPartType.Shape, "Rectangle", Colours: [new("fill", "#ff00ff00")])));
        Assert.Equal(["accent-text"], Check(new FamilyFact(FamilyPartType.Text, "TextBlock", 14, 20, Colours: [new("fg", Accent)])));
        Assert.Equal(["accent-glyph"], Check(new FamilyFact(FamilyPartType.Glyph, "FontIcon", 16, Colours: [new("fg", Accent)])));

        // The accent as a fill is its job; a data colour from its entry point and a clear one are allowed.
        Assert.Empty(Check(new FamilyFact(FamilyPartType.Box, "Border", Colours: [new("bg", Accent), new("bg", "#FF123456", Data: true), new("bg", "#00FFFFFF")])));
    }

    [Fact]
    public void S6731_a_baseline_lists_the_offence_not_its_values()
    {
        Assert.Equal("TextBlock text-size", FamilyCheck.Rule("TextBlock text-size 18"));
        Assert.Equal("TextBlock text-line", FamilyCheck.Rule("TextBlock text-line 24 on 14"));
        Assert.Equal("Border colour bg", FamilyCheck.Rule("Border colour bg #FF00FF00"));
        Assert.Equal("StackPanel space margin", FamilyCheck.Rule("StackPanel space margin -10.06"));
        Assert.Equal("FontIcon accent-glyph", FamilyCheck.Rule("FontIcon accent-glyph"));
    }
}
