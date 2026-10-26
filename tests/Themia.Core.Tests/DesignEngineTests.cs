using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The pure half of the appearance engine (WP1, [S-820]..[S-833]): accent shades, the card's fill
/// and border rules, font resolution, the scaled type ramp and the design presets.
/// </summary>
public sealed class DesignEngineTests
{
    private static double Lightness(RgbColor c) => ColorFormats.ToHsl(c).L;

    [Fact]
    public void S820_shades_walk_lightness_in_order_and_keep_the_accent()
    {
        var accent = new RgbColor(0x00, 0x78, 0xD4);
        var shades = DesignLook.Shades(accent).ToArray();

        Assert.Equal(accent, shades[3]);

        for (var i = 1; i < shades.Length; i++)
        {
            Assert.True(Lightness(shades[i]) > Lightness(shades[i - 1]), $"shade {i} is not lighter than {i - 1}");
        }
    }

    [Fact]
    public void S820_shades_keep_the_hue()
    {
        var accent = new RgbColor(0x3D, 0xDC, 0x84);
        var hue = ColorFormats.ToHsl(accent).H;

        foreach (var shade in DesignLook.Shades(accent).ToArray())
        {
            Assert.InRange(ColorFormats.ToHsl(shade).H, hue - 3, hue + 3);
        }
    }

    [Fact]
    public void S820_shades_of_black_and_white_stay_in_range()
    {
        var white = DesignLook.Shades(new RgbColor(255, 255, 255));
        Assert.Equal(new RgbColor(255, 255, 255), white.Light3);
        Assert.True(Lightness(white.Dark3) < 0.5);

        var black = DesignLook.Shades(new RgbColor(0, 0, 0));
        Assert.Equal(new RgbColor(0, 0, 0), black.Dark3);
        Assert.True(Lightness(black.Light3) > 0.5);
    }

    [Theory]
    [InlineData("AccentFillColorDefaultBrush", true, 5)]
    [InlineData("AccentFillColorDefaultBrush", false, 2)]
    [InlineData("AccentTextFillColorPrimaryBrush", true, 6)]
    [InlineData("AccentTextFillColorPrimaryBrush", false, 1)]
    [InlineData("AccentTextFillColorSecondaryBrush", false, 0)]
    [InlineData("SystemControlHighlightAccentBrush", false, 3)]
    public void S820_accent_brushes_map_to_their_theme_shade(string key, bool dark, int shade)
        => Assert.Equal(shade, DesignLook.AccentShade(key, dark));

    [Fact]
    public void S820_every_listed_accent_brush_has_a_shade_and_others_have_none()
    {
        foreach (var key in DesignLook.AccentBrushKeys)
        {
            Assert.NotNull(DesignLook.AccentShade(key, true));
            Assert.NotNull(DesignLook.AccentShade(key, false));
        }

        Assert.Null(DesignLook.AccentShade("TextFillColorPrimaryBrush", true));
    }

    [Theory]
    [InlineData(1.0, 255)]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 128)]
    [InlineData(2.0, 255)]
    [InlineData(-1.0, 0)]
    [InlineData(double.NaN, 255)]
    public void S822_alpha_clamps_and_treats_garbage_as_opaque(double opacity, byte expected)
        => Assert.Equal(expected, FillChoice.Alpha(opacity));

    /// <summary>The card fill the default design paints: the Fluent card at full strength.</summary>
    private static readonly CardFill Fluent = DesignLook.Card(new FillChoice());

    [Fact]
    public void S6563_default_is_the_fluent_card_at_full_strength_transparent_is_nothing()
    {
        Assert.Equal(new CardFill(FillKind.Default, null, 1), Fluent);
        Assert.True(Fluent.Painted);

        var none = DesignLook.Card(FillChoice.Transparent());
        Assert.Equal(new CardFill(FillKind.Transparent, null, 0), none);
        Assert.False(none.Painted);
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.7, 1.0)]
    [InlineData(double.NaN, 1.0)]
    public void S6563_a_colour_is_exactly_that_colour_at_its_opacity(double opacity, double expected)
    {
        var fill = DesignLook.Card(new FillChoice { Kind = FillKind.Colour, Colour = "#1b2030", Opacity = opacity });

        Assert.Equal(FillKind.Colour, fill.Kind);
        Assert.Equal(new RgbColor(0x1B, 0x20, 0x30), fill.Colour);
        Assert.Equal(expected, fill.Opacity, 3);
        Assert.Equal(expected > 0, fill.Painted);
    }

    [Fact]
    public void S6563_a_colour_choice_without_a_colour_paints_the_default()
        => Assert.Equal(Fluent, DesignLook.Card(new FillChoice { Kind = FillKind.Colour, Colour = "teal", Opacity = 0.3 }));

    [Theory]
    [InlineData(1.0, 1.0, 1.0)]
    [InlineData(1.0, 0.5, 0.5)]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(0.8, 0.5, 0.4)]
    public void S1761_header_opacity_fades_a_header_that_shares_the_widget_fill(double fill, double header, double expected)
        => Assert.Equal(expected, DesignLook.HeaderFillOpacity(DesignLook.Card(FillChoice.Of("#202020", fill)), header, ownColour: false), 3);

    [Fact]
    public void S1761_a_header_sharing_a_default_or_transparent_fill()
    {
        Assert.Equal(0.5, DesignLook.HeaderFillOpacity(Fluent, 0.5, ownColour: false), 3);
        Assert.Equal(0, DesignLook.HeaderFillOpacity(DesignLook.Card(FillChoice.Transparent()), 1, ownColour: false), 3);
    }

    [Theory]
    [InlineData(FillKind.Default, 0.3)]
    [InlineData(FillKind.Transparent, 0.7)]
    public void S1761_a_header_colour_of_its_own_takes_the_header_opacity_alone(FillKind kind, double header)
    {
        var fill = DesignLook.Card(new FillChoice { Kind = kind });
        Assert.Equal(header, DesignLook.HeaderFillOpacity(fill, header, ownColour: true), 3);
        Assert.Equal(1, DesignLook.HeaderFillOpacity(fill, double.NaN, ownColour: true), 3);
        Assert.Equal(0, DesignLook.HeaderFillOpacity(fill, -2, ownColour: true), 3);
    }

    [Fact]
    public void S824_border_styles()
    {
        var solid = DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 2, 0.5);
        Assert.True(solid.Visible);
        Assert.Equal(2, solid.Thickness);
        Assert.Empty(solid.Dashes);
        Assert.Equal(0.5, solid.Opacity);

        var dashed = DesignLook.Border(Fluent, WidgetBorderStyle.Dashed, 1, 1);
        Assert.Equal([3.0, 2.0], dashed.Dashes);
        Assert.False(dashed.RoundCaps);

        var dotted = DesignLook.Border(Fluent, WidgetBorderStyle.Dotted, 3, 1);
        Assert.Equal([0.0, 2.0], dotted.Dashes);
        Assert.True(dotted.RoundCaps);
    }

    [Theory]
    [InlineData(true, 5)]
    [InlineData(false, 3)]
    public void S820_attention_fill_is_the_accent_in_both_themes(bool dark, int shade)
    {
        // WinUI: SystemFillColorAttentionBrush = SystemAccentColorLight2 (dark), SystemAccentColor (light).
        Assert.Equal(shade, DesignLook.AccentShade("SystemFillColorAttentionBrush", dark));
        Assert.Contains("SystemFillColorAttentionBrush", DesignLook.AccentBrushKeys);
    }

    [Theory]
    // CardBackgroundFillColorDefault over SolidBackgroundFillColorBase (WinUI 1.8 theme resources).
    [InlineData(0x0D, 0xFF, 0x20, 0x2B)] // dark:  #0DFFFFFF over #202020 = #2B2B2B
    [InlineData(0xB3, 0xFF, 0xF3, 0xFB)] // light: #B3FFFFFF over #F3F3F3 = #FBFBFB
    // CardStrokeColorDefault over the same base: the card's 1px edge.
    [InlineData(0x19, 0x00, 0x20, 0x1D)] // dark:  #19000000 over #202020 = #1D1D1D
    [InlineData(0x0F, 0x00, 0xF3, 0xE5)] // light: #0F000000 over #F3F3F3 = #E5E5E5
    public void S836_a_fluent_overlay_composites_over_its_base(byte alpha, byte top, byte under, byte expected)
    {
        var result = DesignLook.Over(alpha, new RgbColor(top, top, top), new RgbColor(under, under, under));
        Assert.Equal(new RgbColor(expected, expected, expected), result);
    }

    [Fact]
    public void S836_the_card_tint_sits_inside_a_solid_stroke_only()
    {
        // A Border's background stops at the stroke's inner edge (BackgroundSizing InnerBorderEdge),
        // so the stroke is drawn over the base alone, as a WinUI card's is over the page.
        Assert.Equal(1, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 1, 1)));
        Assert.Equal(3, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 3, 0.5)));

        // Dash gaps would show a ring of bare base: the tint stays full-bleed under a broken line.
        Assert.Equal(0, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.Dashed, 1, 1)));
        Assert.Equal(0, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.Dotted, 2, 1)));
        Assert.Equal(0, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.None, 1, 1)));
        Assert.Equal(0, DesignLook.TintInset(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 1, 0)));
    }

    [Fact]
    public void S835_the_default_design_is_windows_11s_card()
    {
        var design = new DesignState();

        // [S-6482] 4px corners (the user's choice over Fluent's OverlayCornerRadius 8), a 1px
        // CardStrokeColorDefault stroke at full strength, the Fluent fill (no colour of the user's
        // own), opaque, and the Windows accent.
        Assert.Equal(4, design.CornerRadiusPx);
        Assert.Equal(WidgetBorderStyle.Solid, design.BorderStyle);
        Assert.Equal(1, design.BorderWidthPx);
        Assert.Equal(1, design.BorderOpacity);
        Assert.Equal(string.Empty, design.BorderColor);
        Assert.Equal(FillKind.Default, design.Background.Kind);
        Assert.Equal(FillKind.Default, design.ItemBackground.Kind);
        Assert.Equal(string.Empty, design.HeaderBackgroundColor);
        Assert.Equal(1, design.HeaderOpacity);
        Assert.Equal(string.Empty, design.PrimaryColor);
        Assert.Equal(DesignLook.SystemFont, design.FontFamily);
        Assert.Equal(AppTheme.System, design.Theme);
    }

    // ---- [S-6481] the border's ink -----------------------------------------------------------------

    [Fact]
    public void S6481_the_default_border_is_the_fluent_card_hairline()
    {
        var design = new DesignState();
        var border = DesignLook.Border(Fluent, design.BorderStyle, design.BorderWidthPx, design.BorderOpacity);

        var ink = DesignLook.Ink(border, design.BorderColor);

        Assert.Null(ink.Colour);
        Assert.Equal(DesignLook.HairlineStrokeKey, ink.ThemeKey);
    }

    [Theory]
    [InlineData(WidgetBorderStyle.Solid, 2)]
    [InlineData(WidgetBorderStyle.Solid, 4)]
    [InlineData(WidgetBorderStyle.Solid, 8)]
    [InlineData(WidgetBorderStyle.Dashed, 1)]
    [InlineData(WidgetBorderStyle.Dotted, 1)]
    [InlineData(WidgetBorderStyle.Dashed, 4)]
    public void S6481_a_theme_border_heavier_than_the_hairline_takes_the_strong_stroke(WidgetBorderStyle style, double width)
    {
        // CardStrokeColorDefault is a 6-10% overlay meant for a 1px line on Mica: a thicker or broken
        // line drawn in it looked the same as none at all, so every width and style read alike.
        var ink = DesignLook.Ink(DesignLook.Border(Fluent, style, width, 1), "");

        Assert.Null(ink.Colour);
        Assert.Equal(DesignLook.StrongStrokeKey, ink.ThemeKey);
    }

    [Theory]
    [InlineData("#FF0000", 0xFF, 0x00, 0x00)]
    [InlineData("#3ddc84", 0x3D, 0xDC, 0x84)]
    [InlineData(" #000000 ", 0x00, 0x00, 0x00)]
    public void S6481_a_colour_of_the_users_own_is_drawn_exactly(string hex, byte r, byte g, byte b)
    {
        foreach (var width in new[] { 1.0, 4.0 })
        {
            var ink = DesignLook.Ink(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, width, 0.5), hex);
            Assert.Equal(new RgbColor(r, g, b), ink.Colour);
            Assert.Null(ink.ThemeKey);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData(null)]
    public void S6481_no_or_a_bad_colour_is_the_themes(string? hex)
    {
        var ink = DesignLook.Ink(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 1, 1), hex);
        Assert.Null(ink.Colour);
        Assert.Equal(DesignLook.HairlineStrokeKey, ink.ThemeKey);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void S6481_opacity_is_carried_once_and_never_changes_the_ink(double opacity)
    {
        // The opacity is the element's (BorderLook.Opacity) and the ink is opaque or the theme's own,
        // so the setting fades the line exactly once; it never swaps the hairline for the strong stroke.
        var border = DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 1, opacity);
        Assert.Equal(opacity, border.Opacity, 3);
        Assert.Equal(DesignLook.HairlineStrokeKey, DesignLook.Ink(border, "").ThemeKey);
    }

    [Theory]
    [InlineData(WidgetBorderStyle.Solid, 4, 4.0, 0.0)]
    [InlineData(WidgetBorderStyle.Dashed, 4, 12.0, 8.0)]
    [InlineData(WidgetBorderStyle.Dotted, 2, 0.0, 4.0)]
    public void S6481_dashes_keep_their_proportions_at_every_width(WidgetBorderStyle style, double width, double onDips, double offDips)
    {
        var border = DesignLook.Border(Fluent, style, width, 1);
        Assert.Equal(width, border.Thickness);

        if (border.Dashes.Count == 0)
        {
            Assert.Equal(0.0, offDips);
            return;
        }

        // StrokeDashArray is in stroke widths: 3-on-2 at 4px is 12 DIPs on, 8 off.
        Assert.Equal(onDips, border.Dashes[0] * border.Thickness);
        Assert.Equal(offDips, border.Dashes[1] * border.Thickness);
    }

    [Fact]
    public void S824_no_border_for_none_zero_width_or_a_transparent_widget()
    {
        Assert.False(DesignLook.Border(Fluent, WidgetBorderStyle.None, 2, 1).Visible);
        Assert.False(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 0, 1).Visible);
        Assert.False(DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 2, 0).Visible);
        Assert.False(DesignLook.Border(DesignLook.Card(FillChoice.Transparent()), WidgetBorderStyle.Solid, 2, 1).Visible);

        // [S-6563] A colour at 0 % is not transparent: its border stays.
        Assert.True(DesignLook.Border(DesignLook.Card(FillChoice.Of("#202020", 0)), WidgetBorderStyle.Solid, 2, 1).Visible);
        Assert.Equal(8, DesignLook.Border(Fluent, WidgetBorderStyle.Solid, 50, 1).Thickness);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(-4, 0)]
    [InlineData(99, 32)]
    [InlineData(double.PositiveInfinity, 4)]
    [InlineData(double.NaN, 4)]
    public void S825_corner_radius_clamps(double setting, double expected)
        => Assert.Equal(expected, DesignLook.CornerRadius(setting));

    [Theory]
    [InlineData("system", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("SERIF", "Georgia")]
    [InlineData("sans", "Arial")]
    [InlineData("mono", "Cascadia Mono, Consolas")]
    [InlineData("  Bahnschrift  ", "Bahnschrift")]
    [InlineData("Segoe UI Semibold", "Segoe UI Semibold")]
    [InlineData("ms-appx:///evil.ttf#Evil", null)]
    [InlineData("Arial, Wingdings", null)]
    public void S826_font_family_resolves_presets_and_refuses_what_is_not_a_name(string? value, string? expected)
        => Assert.Equal(expected, DesignLook.FontFamilyName(value));

    [Fact]
    public void S827_font_list_drops_vertical_faces_folds_duplicates_and_sorts()
    {
        var list = DesignLook.FontList(["Segoe UI", "@MS Gothic", "arial", "Arial", "", "Bad/Name", "Bahnschrift"]);
        Assert.Equal(["arial", "Bahnschrift", "Segoe UI"], list);
    }

    [Fact]
    public void S821_steps_scale_size_and_line_height_together()
    {
        var at1 = FluentType.StepsAt(1);
        var at15 = FluentType.StepsAt(1.5);

        Assert.Equal(12, at1[0].FontSize);
        Assert.Equal(18, at15[0].FontSize);
        Assert.Equal(24, at15[0].LineHeight);
        Assert.Equal(at1[7].SemiBold, at15[7].SemiBold);
    }

    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(5, 2.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(0, 1.0)]
    [InlineData(1.25, 1.25)]
    public void S821_text_scale_clamps(double setting, double expected)
        => Assert.Equal(expected, FluentType.ClampTextScale(setting));

    [Fact]
    public void S831_a_fresh_design_is_the_default_preset()
        => Assert.Equal(DesignPresetId.Default, DesignPresets.Match(new DesignState()));

    [Fact]
    public void S832_applying_a_preset_sets_its_fields_and_nothing_else()
    {
        var design = new DesignState { FontScale = 1.25, IntroAnimation = IntroAnimation.Fade, GridSnap = false };

        DesignPresets.Apply(design, DesignPresetId.Terminal);

        Assert.Equal(DesignPresetId.Terminal, DesignPresets.Match(design));
        Assert.Equal(AppTheme.Dark, design.Theme);
        Assert.Equal("mono", design.FontFamily);
        Assert.Equal(0, design.CornerRadiusPx);

        // The user's own accessibility and motion choices survive.
        Assert.Equal(1.25, design.FontScale);
        Assert.Equal(IntroAnimation.Fade, design.IntroAnimation);
        Assert.False(design.GridSnap);
    }

    [Fact]
    public void S833_match_ignores_unowned_fields_and_colour_case_but_not_owned_ones()
    {
        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.Paper);

        design.FontScale = 1.75;
        design.PrimaryColor = design.PrimaryColor.ToLowerInvariant();
        Assert.Equal(DesignPresetId.Paper, DesignPresets.Match(design));

        design.CornerRadiusPx += 1;
        Assert.Null(DesignPresets.Match(design));
    }

    [Fact]
    public void S6482_the_default_preset_has_4px_corners_and_the_others_keep_their_own()
    {
        Assert.Equal(4, DesignPresets.Get(DesignPresetId.Default).Values.CornerRadiusPx);
        Assert.Equal(6, DesignPresets.Get(DesignPresetId.Midnight).Values.CornerRadiusPx);
        Assert.Equal(12, DesignPresets.Get(DesignPresetId.Daylight).Values.CornerRadiusPx);
        Assert.Equal(18, DesignPresets.Get(DesignPresetId.Glass).Values.CornerRadiusPx);
        Assert.Equal(0, DesignPresets.Get(DesignPresetId.Terminal).Values.CornerRadiusPx);
        Assert.Equal(4, DesignPresets.Get(DesignPresetId.Paper).Values.CornerRadiusPx);
    }

    // ---- [S-6483] confirm before a preset replaces a custom look ------------------------------------

    [Fact]
    public void S6483_a_design_that_is_a_preset_loses_nothing_to_another()
    {
        var design = new DesignState();
        Assert.False(DesignPresets.ReplacesCustomLook(design, DesignPresetId.Midnight));

        DesignPresets.Apply(design, DesignPresetId.Glass);
        Assert.False(DesignPresets.ReplacesCustomLook(design, DesignPresetId.Paper));
        Assert.False(DesignPresets.ReplacesCustomLook(design, DesignPresetId.Default));
    }

    [Theory]
    [InlineData(nameof(DesignState.BorderColor))]
    [InlineData(nameof(DesignState.BorderWidthPx))]
    [InlineData(nameof(DesignState.CornerRadiusPx))]
    [InlineData(nameof(DesignState.Background))]
    [InlineData(nameof(DesignState.ItemBackground))]
    [InlineData(nameof(DesignState.Theme))]
    [InlineData(nameof(DesignState.WidgetGapPx))]
    public void S6483_a_customised_design_is_replaced_by_any_preset(string field)
    {
        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.Midnight);
        switch (field)
        {
            case nameof(DesignState.BorderColor): design.BorderColor = "#FF0000"; break;
            case nameof(DesignState.BorderWidthPx): design.BorderWidthPx = 3; break;
            case nameof(DesignState.CornerRadiusPx): design.CornerRadiusPx = 20; break;
            case nameof(DesignState.Background): design.Background = FillChoice.Of("#0B0B0F", 0.5); break;
            case nameof(DesignState.ItemBackground): design.ItemBackground = FillChoice.Transparent(); break;
            case nameof(DesignState.Theme): design.Theme = AppTheme.Light; break;
            case nameof(DesignState.WidgetGapPx): design.WidgetGapPx = 30; break;
        }

        foreach (var preset in DesignPresets.All)
        {
            Assert.True(DesignPresets.ReplacesCustomLook(design, preset.Id), $"{field} → {preset.Id}");
        }
    }

    [Fact]
    public void S6483_unowned_fields_are_not_a_custom_look()
    {
        // Text size and motion are the user's own and no preset touches them.
        var design = new DesignState { FontScale = 1.5, IntroAnimation = IntroAnimation.Fade, GridSnap = false };
        Assert.False(DesignPresets.ReplacesCustomLook(design, DesignPresetId.Terminal));
    }

    [Fact]
    public void S830_every_preset_is_distinct_and_matches_itself()
    {
        foreach (var preset in DesignPresets.All)
        {
            var design = new DesignState();
            DesignPresets.Apply(design, preset.Id);
            Assert.Equal(preset.Id, DesignPresets.Match(design));
        }

        Assert.Equal(DesignPresets.All.Count, DesignPresets.All.Select(p => p.Values).Distinct().Count());
    }
}
