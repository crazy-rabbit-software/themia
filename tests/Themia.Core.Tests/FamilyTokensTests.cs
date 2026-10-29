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
        Assert.Equal([(28d, 36d), (40d, 52d), (68d, 92d)], FamilyType.Big.Select(s => (s.FontSize, s.LineHeight)));
        Assert.Equal([(96d, 128d), (136d, 180d), (192d, 252d)], FamilyType.Hero.Select(s => (s.FontSize, s.LineHeight)));
        Assert.All(FamilyType.Big.Concat(FamilyType.Hero), s => Assert.True(s.SemiBold));
        Assert.Equal(400, FamilyType.RegularWeight);
        Assert.Equal(600, FamilyType.StrongWeight);
    }

    [Fact]
    public void S6700_the_fluent_ramp_shares_the_family_sizes()
    {
        // One place: the ramp's caption, body and subtitle are the family's own numbers.
        Assert.Equal(FamilyType.Caption, FluentType.Caption);
        Assert.Equal(FamilyType.Body, FluentType.Body);
        Assert.Equal(FamilyType.BodyStrong, FluentType.BodyStrong);
        Assert.Equal(FamilyType.Title, FluentType.Subtitle);
        Assert.Equal(FamilyType.Big28, FluentType.Title);
        Assert.Equal(FamilyType.Big40, FluentType.TitleLarge);
        Assert.Equal(FamilyType.Big68, FluentType.Display);
    }

    [Fact]
    public void S6700_hero_sizes_are_only_a_one_readout_widgets()
    {
        Assert.True(FamilyType.IsSize(68, oneReadout: false));
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
        Assert.False(FamilyType.IsHero(FamilyType.Big68));
    }

    [Fact]
    public void S6703_the_space_scale_and_its_insets()
    {
        Assert.Equal([0, 4, 8, 12, 16, 24], FamilySpace.Scale);
        Assert.Equal(16, FamilySpace.Inset);
        Assert.Equal(44, FamilySpace.ContentTop);
        Assert.Equal(40, FamilySpace.TextEdge);
        Assert.Equal(8, FamilySpace.GlyphGap);
        Assert.Equal(12, FamilySpace.ColumnGap);
        Assert.True(FamilySpace.IsSpace(-4));
        Assert.True(FamilySpace.IsSpace(44));
        Assert.False(FamilySpace.IsSpace(6));
        Assert.False(FamilySpace.IsSpace(20.5));
    }

    [Fact]
    public void S6704_rows_icons_radius_stroke_and_buttons()
    {
        Assert.Equal([32d, 48d, 64d], Enumerable.Range(1, 3).Select(FamilyShape.RowHeight));
        Assert.Equal(64, FamilyShape.RowHeight(7));
        Assert.Equal([16d, 32d, 48d], FamilyShape.Icons);
        Assert.Equal((32, 32, 24), (FamilyShape.Button, FamilyShape.IconButton, FamilyShape.HeaderButton));

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
    public void S6737_only_a_widget_its_module_declares_one_readout_climbs_the_hero_steps()
    {
        Assert.True(ReadoutRules.OneReadout(Themia.Sdk.WidgetTraits.HasItems | Themia.Sdk.WidgetTraits.OneReadout));
        Assert.False(ReadoutRules.OneReadout(Themia.Sdk.WidgetTraits.HasItems | Themia.Sdk.WidgetTraits.CanHaveHeader));
        Assert.Equal(FamilyType.Big, ReadoutRules.Steps(oneReadout: false));
        Assert.Equal([.. FamilyType.Big, .. FamilyType.Hero], ReadoutRules.Steps(oneReadout: true));
    }
}
