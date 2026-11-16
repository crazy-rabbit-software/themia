using Themia.Core.Model;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2123] Liquid glass's design values (iOS 26's: clear, a crisp edge lit at the top and the foot, a lensing band
/// inside it, a faint large lift, no sheen over the surface), its adaptive tint, and a group as one glass shape.
/// </summary>
public sealed class LiquidGlassTests
{
    [Fact]
    public void S2124_a_card_with_no_fill_wears_no_glass()
    {
        Assert.False(LiquidGlass.Wears(new CardFill(FillKind.Transparent, null, 0)));
        Assert.True(LiquidGlass.Wears(new CardFill(FillKind.Default, null, 0.6)));
        Assert.True(LiquidGlass.Wears(new CardFill(FillKind.Colour, new RgbColor(10, 20, 30), 1)));
    }

    [Fact]
    public void S2123_each_theme_has_its_glass_clear_round_24_and_the_dark_one_dimmer_and_lifted_deeper()
    {
        var (light, dark) = (LiquidGlass.Light, LiquidGlass.Dark);

        Assert.Same(light, LiquidGlass.For(dark: false));
        Assert.Same(dark, LiquidGlass.For(dark: true));
        Assert.Equal(new RgbColor(0xFF, 0xFF, 0xFF), light.Tint);
        Assert.True(LiquidGlass.Luminance(dark.Tint) < 0.02);
        Assert.True(dark.Rim[0].Alpha < light.Rim[0].Alpha);
        Assert.True(dark.Glow[0].Alpha < light.Glow[0].Alpha);
        Assert.True(dark.ShadowOpacity > light.ShadowOpacity);
        Assert.Equal((24d, 1d), (light.RadiusPx, light.RimWidth));
        Assert.Equal(light.RadiusPx, dark.RadiusPx);

        // [S-2132] Clear: the least tint lets most of what is behind through (the wallpaper's colour shows), the lift
        // faint and large; the defaults are the look tokens' own.
        Assert.True(light.TintMin <= 0.2 && dark.TintMin <= 0.3);
        Assert.True(light.ShadowOpacity <= 0.12 && light.ShadowBlur >= 32);
        var fresh = new Model.LookTokens();
        Assert.Equal((fresh.GlassTintLightMin, fresh.GlassTintLightMax, fresh.GlassTintDarkMin, fresh.GlassTintDarkMax), (light.TintMin, light.TintMax, dark.TintMin, dark.TintMax));
        Assert.Equal((fresh.ShadowRadius, fresh.ShadowOffset, fresh.RimWidth, fresh.DividerOpacity), (light.ShadowBlur, light.ShadowOffsetY, light.RimWidth, light.DividerAlpha));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2123_the_rim_is_bright_along_the_top_fades_down_the_sides_and_catches_again_along_the_foot(bool isDark)
    {
        var rim = LiquidGlass.For(isDark).Rim;

        Assert.Equal(0, rim[0].Offset);
        Assert.Equal(1, rim[^1].Offset);
        Assert.Equal(rim.Max(s => s.Alpha), rim[0].Alpha);
        var faintest = rim.MinBy(s => s.Alpha);
        Assert.InRange(faintest.Offset, 0.3, 0.7);
        Assert.True(rim[^1].Alpha > faintest.Alpha * 4);
        Assert.All(rim.Zip(rim.Skip(1)), p => Assert.True(p.First.Offset < p.Second.Offset));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2123_no_sheen_over_the_surface_only_a_glow_at_the_top_edge(bool isDark)
    {
        var look = LiquidGlass.For(isDark);

        Assert.Equal(look.Glow.Max(s => s.Alpha), look.Glow[0].Alpha);
        Assert.Equal(0, look.Glow[^1].Alpha);
        Assert.InRange(look.GlowDepth, 8, 32);
        Assert.True(look.Glow[0].Alpha <= 0.2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2123_the_lens_is_a_band_inside_the_rim_fading_inward_and_strongest_where_the_light_falls(bool isDark)
    {
        var look = LiquidGlass.For(isDark);

        Assert.True(look.LensBands.Count >= 2);
        Assert.All(look.LensBands.Zip(look.LensBands.Skip(1)), p => Assert.True(p.First > p.Second));
        var outer = LiquidGlass.LensStops(look, 0);
        var inner = LiquidGlass.LensStops(look, look.LensBands.Count - 1);
        Assert.All(outer.Zip(inner), p => Assert.True(p.First.Alpha > p.Second.Alpha));
        Assert.DoesNotContain(LiquidGlass.LensStops(look, look.LensBands.Count), s => s.Alpha > 0);

        // Down the glass: bright at the top and the foot, faint along the middle of the sides.
        var middle = look.Lens.MinBy(s => s.Alpha);
        Assert.InRange(middle.Offset, 0.3, 0.7);
        Assert.True(look.Lens[0].Alpha > middle.Alpha * 3 && look.Lens[^1].Alpha > middle.Alpha * 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2123_the_lift_is_rings_that_darken_the_edge_by_its_opacity_and_fade_to_nothing_at_its_blur(bool isDark)
    {
        var look = LiquidGlass.For(isDark);
        var rings = LiquidGlass.LiftRings(look);

        Assert.Equal(LiquidGlass.LiftRingCount, rings.Count);
        Assert.Equal(look.ShadowBlur, rings[0].Spread, 9);
        Assert.All(rings.Zip(rings.Skip(1)), p => Assert.True(p.First.Spread > p.Second.Spread && p.First.Alpha < p.Second.Alpha));
        Assert.Equal(look.ShadowOpacity, rings.Sum(r => r.Alpha), 9);
        Assert.All(rings, r => Assert.InRange(r.Alpha, 0, look.ShadowOpacity));
    }

    // ---- a group is one glass shape ----

    private static GlassPart[] Bodies(PixelRect[] painted, string?[] groups, int?[]? sets = null)
        => CardJoins.Bodies(painted, sets ?? [.. painted.Select(_ => (int?)0)], groups, 1, LiquidGlass.Light.DividerInset);

    [Fact]
    public void S2123_a_groups_members_that_meet_are_one_body_round_them_all()
    {
        PixelRect[] painted = [new(10, 10, 300, 84), new(10, 94, 300, 64), new(10, 158, 300, 84), new(400, 10, 100, 100)];
        var parts = Bodies(painted, ["g", "g", "g", null]);

        Assert.All(parts.Take(3), p => Assert.Equal(new PixelRect(10, 10, 300, 232), p.Body));
        Assert.Equal(painted[3], parts[3].Body);
        Assert.Empty(parts[3].Dividers);
    }

    [Fact]
    public void S2123_each_pair_that_meets_draws_one_divider_on_the_upper_card_short_of_the_outer_edge()
    {
        var inset = LiquidGlass.Light.DividerInset;
        PixelRect[] painted = [new(10, 10, 300, 84), new(10, 94, 300, 64), new(10, 158, 300, 84)];
        var parts = Bodies(painted, ["g", "g", "g"]);

        Assert.Equal([new DividerLine(inset, 84, 300 - inset, 84)], parts[0].Dividers);
        Assert.Equal([new DividerLine(inset, 64, 300 - inset, 64)], parts[1].Dividers);
        Assert.Empty(parts[2].Dividers);
    }

    [Fact]
    public void S2123_a_divider_that_ends_on_another_inside_the_body_runs_on_to_it()
    {
        // Two side by side over one across: the line between the two ends on the one under them.
        var inset = LiquidGlass.Light.DividerInset;
        PixelRect[] painted = [new(0, 0, 150, 80), new(150, 0, 150, 80), new(0, 80, 300, 60)];
        var parts = Bodies(painted, ["g", "g", "g"]);

        Assert.Contains(new DividerLine(150, inset, 150, 80), parts[0].Dividers);
        Assert.Contains(new DividerLine(inset, 80, 150, 80), parts[0].Dividers);
        Assert.Equal([new DividerLine(0, 80, 150 - inset, 80)], parts[1].Dividers);
    }

    [Fact]
    public void S2123_cards_of_two_groups_lone_cards_and_cards_of_two_sets_are_bodies_of_their_own()
    {
        PixelRect[] painted = [new(0, 0, 100, 50), new(100, 0, 100, 50), new(0, 50, 100, 50), new(100, 50, 100, 50)];
        var parts = Bodies(painted, ["g", "h", null, "g"], [0, 0, 0, 1]);

        Assert.All(parts.Select((p, i) => (p, i)), x => Assert.Equal(painted[x.i], x.p.Body));
        Assert.All(parts, p => Assert.Empty(p.Dividers));
    }

    [Fact]
    public void S2123_light_glass_stays_clear_over_a_bright_wallpaper_and_clouds_over_a_dark_one()
    {
        var bright = LiquidGlass.TintOpacity(dark: false, behind: 0.85);
        var middle = LiquidGlass.TintOpacity(dark: false, behind: 0.05);
        var black = LiquidGlass.TintOpacity(dark: false, behind: 0.0);

        Assert.Equal(LiquidGlass.Light.TintMin, bright);
        Assert.True(middle > bright);
        Assert.True(black >= middle);
        Assert.True(black <= LiquidGlass.Light.TintMax);
    }

    [Fact]
    public void S2123_dark_glass_stays_clear_over_a_dark_wallpaper_and_clouds_over_a_bright_one()
    {
        Assert.Equal(LiquidGlass.Dark.TintMin, LiquidGlass.TintOpacity(dark: true, behind: 0.02));
        Assert.True(LiquidGlass.TintOpacity(dark: true, behind: 0.9) > LiquidGlass.TintOpacity(dark: true, behind: 0.3));
    }

    [Theory]
    [InlineData(false, 0.05)]
    [InlineData(false, 0.3)]
    [InlineData(true, 0.4)]
    [InlineData(true, 1.0)]
    public void S2123_where_the_tint_can_make_the_text_read_it_does(bool isDark, double behind)
    {
        var alpha = LiquidGlass.TintOpacity(isDark, behind);
        var look = LiquidGlass.For(isDark);
        if (alpha >= look.TintMax)
        {
            return; // the most the glass clouds over; past it the text is the wallpaper's to fight
        }

        // The glass mixed over the backdrop in sRGB, as the compositor mixes it, and Windows' primary text (the theme's
        // ink at its own alpha, [S-2134]) over that: it reads at 4.5:1.
        static double Srgb(double l) => l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var glass = Srgb(behind) + ((Srgb(LiquidGlass.Luminance(look.Tint)) - Srgb(behind)) * alpha);
        var ink = Srgb(LiquidGlass.Luminance(LookRules.Ink(isDark)));
        var text = glass + ((ink - glass) * LookRules.ThemeLabel(ItemTextTier.Primary, isDark));
        Assert.True(LiquidGlass.Contrast(Linear(glass), Linear(text)) >= LiquidGlass.TextContrast - 0.01);
    }

    [Fact]
    public void S2123_with_nothing_known_behind_the_glass_clouds_over_as_far_as_it_goes()
    {
        Assert.Equal(LiquidGlass.Light.TintMax, LiquidGlass.TintOpacity(dark: false, behind: null));
        Assert.Equal(LiquidGlass.Dark.TintMax, LiquidGlass.TintOpacity(dark: true, behind: double.NaN));
    }

    [Fact]
    public void S2123_a_backdrops_light_is_a_grid_of_its_cells_and_a_box_reads_the_cells_it_covers()
    {
        // A picture 4 × 2, its left half black, its right half white (BGRA).
        var pixels = new byte[4 * 2 * 4];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 2; x < 4; x++)
            {
                var at = ((y * 4) + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (0xFF, 0xFF, 0xFF, 0xFF);
            }
        }

        var grid = LiquidGlass.Grid(pixels, 4, 2, 2, 1);

        Assert.Equal([0f, 1f], grid);
        Assert.Equal(0, LiquidGlass.Mean(grid, 2, 1, 0, 0, 0.5, 1)!.Value, 3);
        Assert.Equal(1, LiquidGlass.Mean(grid, 2, 1, 0.5, 0, 1, 1)!.Value, 3);
        Assert.Equal(2 / 3d, LiquidGlass.Mean(grid, 2, 1, 0.25, 0, 1, 1)!.Value, 3);
        Assert.Equal(0.5, LiquidGlass.Mean(grid, 2, 1, -1, -1, 2, 2)!.Value, 3);
        Assert.Null(LiquidGlass.Mean(grid, 2, 1, 1.2, 0, 1.5, 1));
    }

    [Fact]
    public void S2123_a_picture_too_small_for_its_size_is_refused()
        => Assert.Throws<ArgumentException>(() => LiquidGlass.Grid(new byte[8], 4, 2, 2, 1));
}
