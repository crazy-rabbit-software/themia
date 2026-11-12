using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6745] [S-6746] The chrome's measurements and colour roles: the tokens the family lacks, the shell's alone.</summary>
public sealed class ShellTokensTests
{
    [Fact]
    public void S2320_the_edit_bar_is_karpos_command_bar()
    {
        Assert.Equal(48, ShellMetrics.Bar.Height);
        Assert.Equal((8d, 32d, 8d), (ShellMetrics.Bar.Pad, ShellMetrics.Bar.Control, ShellMetrics.Bar.Top));
        Assert.Equal(40, ShellMetrics.Bar.IconOnly);
        Assert.Equal((256d, 16d, 80d, 20d), (ShellMetrics.Bar.Selection, ShellMetrics.Bar.Gutter, ShellMetrics.Bar.DoneLeast, ShellMetrics.Bar.Divider));
    }

    [Fact]
    public void S2325_a_flyout_is_580_wide_with_its_cards_inside_16_gutters_and_an_8_beak()
    {
        Assert.Equal(580, ShellMetrics.Flyout.Width);
        Assert.Equal(548, ShellMetrics.Flyout.Cards);
        Assert.Equal((12d, 8d, 16d, 0.7), (ShellMetrics.Flyout.Gap, ShellMetrics.Flyout.BeakHeight, ShellMetrics.Flyout.BeakWidth, ShellMetrics.Flyout.MostShare));
        Assert.Equal((48d, 60d, 16d, 48d, 16d), (ShellMetrics.Page.OneLine, ShellMetrics.Page.TwoLines, ShellMetrics.Page.IconAt, ShellMetrics.Page.NameAt, ShellMetrics.Page.EndInset));
        Assert.Equal((24d, 8d, 4d, 4d), (ShellMetrics.Page.SectionTop, ShellMetrics.Page.HeadingGap, ShellMetrics.Page.CardGap, ShellMetrics.Page.CardRadius));
        Assert.Equal((40d, 20d, 12d, 4d), (ShellMetrics.Page.SwitchWidth, ShellMetrics.Page.SwitchHeight, ShellMetrics.Page.Knob, ShellMetrics.Page.KnobInset));
    }

    [Fact]
    public void S6745_the_view_bar_grows_by_squares_that_hold_its_words()
    {
        Assert.Equal([28d, 32d, 40d], new[] { ShellMetrics.ViewBar.SquareM, ShellMetrics.ViewBar.SquareL, ShellMetrics.ViewBar.SquareXl });

        // Each square holds a chip of its words (the family's Caption, Body, Title lines) padded 4 over and under, its hairline rims counted.
        Assert.True(ShellMetrics.ViewBar.SquareM >= FamilyType.CaptionLine + (2 * FamilySpace.S1) + (2 * FamilyShape.Hairline));
        Assert.True(ShellMetrics.ViewBar.SquareL >= FamilyType.BodyLine + (2 * FamilySpace.S1) + (2 * FamilyShape.Hairline));
        Assert.True(ShellMetrics.ViewBar.SquareXl >= FamilyType.TitleLine + (2 * FamilySpace.S1) + (2 * FamilyShape.Hairline));
    }

    [Fact]
    public void S6746_every_shell_role_has_one_theme_resource_none_a_widgets_role()
    {
        var keys = ShellRoles.All.Select(ShellRoles.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.EndsWith("Brush", k, StringComparison.Ordinal));
        Assert.All(keys, k => Assert.False(ColourRoles.IsRoleKey(k), $"{k} is a widget's colour role, not the shell's"));

        // The card's own brushes are the card's: the shell's roles name them, the render walk allows them on every card.
        Assert.Contains(ShellRoles.Key(ShellRole.CardBase), ColourRoles.CardKeys);
        Assert.Contains(ShellRoles.Key(ShellRole.CardFill), ColourRoles.CardKeys);
        Assert.Contains(ShellRoles.Key(ShellRole.CardStroke), ColourRoles.CardKeys);
    }
}
