using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6700]..[S-6706] The family's tokens: the type, the space, the shapes and the colour roles.</summary>
public sealed class FamilyTokensTests
{
    [Fact]
    public void S6700_the_family_draws_nine_sizes_in_two_weights()
    {
        Assert.Equal([12, 14, 20, 28, 40, 68, 96, 136, 192], FamilyType.Sizes);
        Assert.Equal((12, 16, false), (FamilyType.Caption.FontSize, FamilyType.Caption.LineHeight, FamilyType.Caption.SemiBold));
        Assert.Equal((14, 20, false), (FamilyType.Body.FontSize, FamilyType.Body.LineHeight, FamilyType.Body.SemiBold));
        Assert.Equal((14, 20, true), (FamilyType.BodyStrong.FontSize, FamilyType.BodyStrong.LineHeight, FamilyType.BodyStrong.SemiBold));
        Assert.Equal((20, 28), (FamilyType.Title.FontSize, FamilyType.Title.LineHeight));
        Assert.Equal([(28d, 36d), (40d, 52d)], FamilyType.Big.Select(s => (s.FontSize, s.LineHeight)));
        Assert.Equal([(68d, 92d), (96d, 128d), (136d, 180d), (192d, 252d)], FamilyType.Hero.Select(s => (s.FontSize, s.LineHeight)));
        Assert.All(FamilyType.Big.Concat(FamilyType.Hero), s => Assert.True(s.SemiBold));
        Assert.Equal(400, FamilyType.RegularWeight);
        Assert.Equal(600, FamilyType.StrongWeight);
    }

    [Fact]
    public void S6748_the_family_type_is_the_only_ramp()
    {
        // One path: the steps at the live text size are the family's, in the order StepsAt lists them.
        Assert.Equal(
            [FamilyType.Caption, FamilyType.Body, FamilyType.BodyStrong, FamilyType.Title, .. FamilyType.Big, .. FamilyType.Hero],
            FamilyType.StepsAt(FamilyType.TextScale));
        Assert.Equal(FamilyType.Sizes, FamilyType.StepsAt(1).Select(s => s.FontSize).Distinct());
    }

    [Fact]
    public void S6700_hero_sizes_are_only_a_one_readout_widgets()
    {
        Assert.True(FamilyType.IsSize(40, oneReadout: false));
        Assert.False(FamilyType.IsSize(68, oneReadout: false));
        Assert.True(FamilyType.IsSize(68, oneReadout: true));
        Assert.False(FamilyType.IsSize(96, oneReadout: false));
        Assert.True(FamilyType.IsSize(96, oneReadout: true));
        Assert.False(FamilyType.IsSize(18, oneReadout: true));
        Assert.False(FamilyType.IsSize(73.2, oneReadout: true));
        Assert.Equal(52, FamilyType.LineOf(40));
        Assert.Equal(0, FamilyType.LineOf(18));
    }

    [Theory]
    [InlineData(28, 14, 20, 12, 16)]
    [InlineData(40, 20, 28, 14, 20)]
    [InlineData(68, 28, 36, 14, 20)]
    [InlineData(96, 40, 52, 20, 28)]
    [InlineData(136, 40, 52, 20, 28)]
    [InlineData(192, 68, 92, 20, 28)]
    public void S6701_a_steps_unit_and_line_under_follow_the_step(double size, double unit, double unitLine, double under, double underLine)
    {
        var step = FamilyType.Big.Concat(FamilyType.Hero).Single(s => s.FontSize == size);

        Assert.Equal((unit, unitLine, false), (FamilyType.Unit(step).FontSize, FamilyType.Unit(step).LineHeight, FamilyType.Unit(step).SemiBold));
        Assert.Equal((under, underLine, false), (FamilyType.Under(step).FontSize, FamilyType.Under(step).LineHeight, FamilyType.Under(step).SemiBold));
    }

    [Fact]
    public void S6701_a_step_off_the_family_sizes_has_no_unit_of_its_own()
    {
        // A step the family does not draw (an off-ramp hero) is its own base, and its unit and line
        // under fall back to Caption rather than inventing a size.
        var offRamp = new TypeStep(73.2, 84, true);
        Assert.Equal(73.2, FamilyType.BaseOf(offRamp));
        Assert.Equal(FamilyType.Caption, FamilyType.Unit(offRamp));
        Assert.Equal(FamilyType.Caption, FamilyType.Under(offRamp));
        Assert.Equal(28, FamilyType.BaseOf(FamilyType.Big28));
        Assert.True(FamilyType.IsHero(FamilyType.Hero96));
        Assert.True(FamilyType.IsHero(FamilyType.Hero68));
        Assert.False(FamilyType.IsHero(FamilyType.Big40));
    }

    [Fact]
    public void S6703_the_space_scale_and_its_insets()
    {
        Assert.Equal([0, 4, 8, 12, 16, 24], FamilySpace.Scale);
        Assert.Equal(12, FamilySpace.Inset);
        Assert.Equal(4, FamilySpace.HeaderGap);
        Assert.Equal(36, FamilySpace.ContentTop);
        Assert.Equal(36, FamilySpace.TextEdge);
        Assert.Equal(8, FamilySpace.GlyphGap);
        Assert.Equal(12, FamilySpace.ColumnGap);
        Assert.True(FamilySpace.IsSpace(-4));
        Assert.True(FamilySpace.IsSpace(36));
        Assert.False(FamilySpace.IsSpace(44));
        Assert.False(FamilySpace.IsSpace(6));
        Assert.False(FamilySpace.IsSpace(20.5));
    }

    [Fact]
    public void S6704_rows_icons_radius_stroke_and_buttons()
    {
        Assert.Equal([28d, 40d, 56d], Enumerable.Range(1, 3).Select(FamilyShape.RowHeight));
        Assert.Equal(56, FamilyShape.RowHeight(7));
        Assert.Equal([16d, 32d, 48d], FamilyShape.Icons);
        Assert.Equal((28, 28, 24, 12), (FamilyShape.Button, FamilyShape.IconButton, FamilyShape.HeaderButton, FamilyShape.ButtonPadding));

        Assert.True(FamilyShape.IsRadius(4, 100, 32));
        Assert.True(FamilyShape.IsRadius(8, 40, 16));      // a pill: half its short side
        Assert.True(FamilyShape.IsRadius(0, 10, 10));
        Assert.False(FamilyShape.IsRadius(6, 100, 32));

        Assert.True(FamilyShape.IsStroke(2));
        Assert.True(FamilyShape.IsStroke(4));
        Assert.True(FamilyShape.IsStroke(1));
        Assert.False(FamilyShape.IsStroke(3));

        Assert.True(FamilyShape.IsGlyph(16, oneReadout: false));
        Assert.True(FamilyShape.IsGlyph(40, oneReadout: false));
        Assert.False(FamilyShape.IsGlyph(20, oneReadout: false));
        Assert.False(FamilyShape.IsGlyph(68, oneReadout: false));
        Assert.False(FamilyShape.IsGlyph(136, oneReadout: false));
        Assert.True(FamilyShape.IsGlyph(136, oneReadout: true));
        Assert.False(FamilyShape.IsGlyph(14, oneReadout: true));
    }

    [Fact]
    public void S6705_every_role_has_one_theme_resource()
    {
        var keys = ColourRoles.All.Select(ColourRoles.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.EndsWith("Brush", k, StringComparison.Ordinal));
        Assert.All(ColourRoles.All, r => Assert.Equal(r, ColourRoles.Of(ColourRoles.Key(r))));
        Assert.True(ColourRoles.IsRoleKey("TextFillColorSecondaryBrush"));
    }

    [Fact]
    public void S6706_no_success_green_and_no_accent_text()
    {
        // The accent is a fill, a line or a ring; the roles a text is drawn in never include it, and green is no role.
        Assert.False(ColourRoles.IsRoleKey("SystemFillColorSuccessBrush"));
        Assert.False(ColourRoles.IsRoleKey("AccentTextFillColorPrimaryBrush"));
        Assert.Null(ColourRoles.Of("SystemFillColorSuccessBrush"));
    }

    [Fact]
    public void S2220_a_rows_lines_get_even_room_above_and_below()
    {
        // One Body line in a 28 row: 4 over and under; two and three lines (Body, then Caption) 2 over and under.
        Assert.Equal(2 * 4, FamilyShape.Row1 - FamilyType.Body.LineHeight);
        Assert.Equal(2 * 2, FamilyShape.Row2 - FamilyType.Body.LineHeight - FamilyType.Caption.LineHeight);
        Assert.Equal(2 * 2, FamilyShape.Row3 - FamilyType.Body.LineHeight - (2 * FamilyType.Caption.LineHeight));

        // A button and a content icon button are as tall as a one-line row.
        Assert.Equal(FamilyShape.Row1, FamilyShape.Button);
        Assert.Equal(FamilyShape.Row1, FamilyShape.IconButton);
    }

    [Fact]
    public void S2220_a_data_widgets_number_never_takes_68()
    {
        // A huge box: a data reading stops at 40, a one-readout widget's goes on through the Hero steps.
        Assert.Equal(40, FamilyType.BaseOf(ReadoutRules.Step(1200, 900, new ReadoutText("88"))));
        Assert.Equal(192, FamilyType.BaseOf(ReadoutRules.Step(1200, 900, new ReadoutText("88", OneReadout: true))));
        Assert.Equal(68, FamilyType.BaseOf(ReadoutRules.Step(1200, 100, new ReadoutText("88", OneReadout: true))));
        Assert.DoesNotContain(ReadoutRules.Steps(oneReadout: false), s => FamilyType.BaseOf(s) > 40);
    }

    [Fact]
    public void S6737_only_a_widget_its_module_declares_one_readout_climbs_the_hero_steps()
    {
        Assert.True(ReadoutRules.OneReadout(Themia.Sdk.WidgetTraits.HasItems | Themia.Sdk.WidgetTraits.OneReadout));
        Assert.False(ReadoutRules.OneReadout(Themia.Sdk.WidgetTraits.HasItems | Themia.Sdk.WidgetTraits.CanHaveHeader));
        Assert.Equal(FamilyType.Big, ReadoutRules.Steps(oneReadout: false));
        Assert.Equal([.. FamilyType.Big, .. FamilyType.Hero], ReadoutRules.Steps(oneReadout: true));
    }
}
