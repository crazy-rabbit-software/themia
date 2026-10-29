using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6745] [S-6746] The chrome's measurements and colour roles: the tokens the family lacks, the shell's alone.</summary>
public sealed class ShellTokensTests
{
    [Fact]
    public void S6745_the_menu_keeps_karpos_fixed_rectangle_and_explorers_rows()
    {
        // [S-3734] One rectangle on every level, never moving or resizing: these must not change.
        Assert.Equal((320d, 440d), (ShellMetrics.Menu.Width, ShellMetrics.Menu.Height));
        Assert.Equal((32d, 28d, 2d), (ShellMetrics.Menu.Pitch, ShellMetrics.Menu.Fill, ShellMetrics.Menu.FillInset));
        Assert.Equal(4, ShellMetrics.Menu.RowInset);
        Assert.Equal(16, ShellMetrics.Menu.RowInset + ShellMetrics.Menu.PadStart + FamilyShape.Hairline);
        Assert.Equal((40d, 20d, 12d, 4d), (ShellMetrics.Menu.SwitchWidth, ShellMetrics.Menu.SwitchHeight, ShellMetrics.Menu.Knob, ShellMetrics.Menu.KnobInset));
        Assert.Equal(ShellMetrics.Menu.Pitch, ShellMetrics.Menu.Fill + (2 * ShellMetrics.Menu.FillInset));
        Assert.Equal(ShellMetrics.Menu.Width, ShellMetrics.Details.Width);
    }

    [Fact]
    public void S6745_the_edit_bar_is_karpos_48_tall()
    {
        Assert.Equal((36d, 6d, 48d), (ShellMetrics.EditBar.Button, ShellMetrics.EditBar.Gap, ShellMetrics.EditBar.Height));
        Assert.Equal(20, ShellMetrics.EditBar.Divider);
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
