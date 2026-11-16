using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2130]..[S-2143] The look tokens: each token's range, repair and default; that a fresh set draws every style but
/// Liquid glass exactly as before; the rules each token drives (the frost, the glass, the accent and the rendering mode,
/// the text levels and their contrast over the glass, the type, the items' surfaces, the grain); the <c>--design</c>
/// keys; and Liquid glass's own tokens.
/// </summary>
public sealed class LookTokensTests
{
    // ---- [S-2130] the tokens ----

    [Fact]
    public void S2130_every_number_token_has_one_key_a_range_a_step_and_a_fresh_sets_value_as_its_default()
    {
        var fresh = new LookTokens();

        Assert.Equal(LookTokens.Numbers.Count, LookTokens.Numbers.Select(n => n.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(LookTokens.Numbers, n =>
        {
            Assert.True(n.Min < n.Max, n.Key);
            Assert.True(n.Step > 0 && n.Step <= n.Max - n.Min, n.Key);
            Assert.InRange(n.Default, n.Min, n.Max);
            Assert.Equal(n.Default, n.Get(fresh));
            Assert.Same(n, LookTokens.Number(n.Key.ToUpperInvariant()));
        });
        Assert.Null(LookTokens.Number("noSuchToken"));
    }

    [Fact]
    public void S2130_a_number_is_held_to_its_range_and_one_that_is_not_finite_is_its_default()
    {
        foreach (var n in LookTokens.Numbers)
        {
            Assert.Equal(n.Max, n.Clamp(n.Max + 100));
            Assert.Equal(n.Min, n.Clamp(n.Min - 100));
            Assert.Equal(n.Default, n.Clamp(double.NaN));
            Assert.Equal(n.Default, n.Clamp(double.PositiveInfinity));
        }
    }

    [Fact]
    public void S2130_a_fresh_set_needs_no_repair()
        => Assert.Empty(new LookTokens().Repair());

    [Fact]
    public void S2130_repair_holds_every_number_to_its_range_and_every_enum_to_one_loudly()
    {
        var tokens = new LookTokens
        {
            FrostBlur = 500,
            Vibrancy = double.NaN,
            LabelPrimary = 0.1,
            Rendering = (RenderingMode)42,
            HeaderWeight = (TypeWeight)123,
            InnerSurface = (InnerSurface)9,
            RowDividers = (RowDivider)7,
            HeaderTone = (HeaderTone)5,
            HeaderCase = (HeaderCase)5,
        };

        var repairs = tokens.Repair();

        Assert.Equal(80, tokens.FrostBlur);
        Assert.Equal(0, tokens.Vibrancy);
        Assert.Equal(0.5, tokens.LabelPrimary);
        Assert.Equal(RenderingMode.FullColour, tokens.Rendering);
        Assert.Equal(TypeWeight.Semibold, tokens.HeaderWeight);
        Assert.Equal(InnerSurface.Theme, tokens.InnerSurface);
        Assert.Equal(RowDivider.None, tokens.RowDividers);
        Assert.Equal(HeaderTone.Secondary, tokens.HeaderTone);
        Assert.Equal(HeaderCase.AsWritten, tokens.HeaderCase);
        Assert.Equal(9, repairs.Count);
        Assert.Empty(tokens.Repair());
    }

    [Fact]
    public void S2130_load_validation_repairs_the_tokens_and_a_missing_set_is_a_fresh_one()
    {
        var state = AppState.CreateDefault();
        state.Design.Tokens = null!;
        Assert.DoesNotContain(Storage.AppStateStore.Validate(state), r => r.Contains("look token", StringComparison.Ordinal));
        Assert.True(state.Design.Tokens.SameAs(new LookTokens()));

        state.Design.Tokens.TrackingLarge = -400;
        Assert.Contains(Storage.AppStateStore.Validate(state), r => r.Contains("trackingLarge", StringComparison.Ordinal));
        Assert.Equal(-60, state.Design.Tokens.TrackingLarge);
    }

    [Fact]
    public void S2130_the_tokens_survive_a_round_trip_through_the_state_file_and_a_clone()
    {
        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.LiquidGlass);
        design.Tokens.Rendering = RenderingMode.Monochrome;

        var copy = design.Clone();
        Assert.NotSame(design.Tokens, copy.Tokens);
        Assert.True(copy.Tokens.SameAs(design.Tokens));

        var json = System.Text.Json.JsonSerializer.Serialize(design, Storage.AppStateJsonContext.Default.DesignState);
        Assert.Contains("\"rendering\":\"Monochrome\"", json.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, Storage.AppStateJsonContext.Default.DesignState)!;
        Assert.True(back.Tokens.SameAs(design.Tokens));
    }

    [Fact]
    public void S2130_the_file_names_only_the_tokens_a_look_changed_and_a_fresh_set_not_at_all()
    {
        static string Json(DesignState d) => System.Text.Json.JsonSerializer.Serialize(d, Storage.AppStateJsonContext.Default.DesignState);

        Assert.DoesNotContain("\"tokens\"", Json(new DesignState()), StringComparison.Ordinal);

        var design = new DesignState();
        design.Tokens.Vibrancy = 0.5;
        design.Tokens.RowDividers = RowDivider.Hairline;
        var json = Json(design).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"tokens\":{\"vibrancy\":0.5,\"rowDividers\":\"Hairline\"}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S2130_a_token_of_the_wrong_kind_or_name_reads_as_its_default_and_an_unknown_one_is_skipped()
    {
        const string Json = """{ "tokens": { "vibrancy": "lots", "rendering": "Sepia", "headerWeight": "600", "tabularNumerals": 1, "futureToken": [1, 2], "grain": 0.05 } }""";
        var design = System.Text.Json.JsonSerializer.Deserialize(Json, Storage.AppStateJsonContext.Default.DesignState)!;

        Assert.Equal(0, design.Tokens.Vibrancy);
        Assert.Equal(RenderingMode.FullColour, design.Tokens.Rendering);
        Assert.Equal(TypeWeight.Semibold, design.Tokens.HeaderWeight);
        Assert.False(design.Tokens.TabularNumerals);
        Assert.Equal(0.05, design.Tokens.Grain);
    }

    [Fact]
    public void S2130_same_as_tells_every_token()
    {
        foreach (var n in LookTokens.Numbers)
        {
            var other = new LookTokens();
            n.Set(other, n.Default == n.Max ? n.Min : n.Max);
            Assert.False(new LookTokens().SameAs(other), n.Key);
        }

        Assert.False(new LookTokens().SameAs(new LookTokens { TabularNumerals = true }));
        Assert.False(new LookTokens().SameAs(new LookTokens { RowDividers = RowDivider.Inset }));
        Assert.False(new LookTokens().SameAs(null));
    }

    // ---- a fresh set is today's look ----

    [Fact]
    public void S2130_a_fresh_set_draws_windows_and_the_familys_own_look()
    {
        var fresh = new LookTokens();

        Assert.Equal(new FrostLook(BoxBlur.BackdropRadius, 1, 0), LookRules.Frost(fresh));
        Assert.False(LookRules.Frost(fresh).Treats);
        Assert.Equal(TypeLook.Default, fresh.Type());
        Assert.Equal(0, LookRules.AccentShare(fresh));
        Assert.Equal(RowDivider.None, fresh.RowDividers);
        foreach (var dark in new[] { true, false })
        {
            foreach (var tier in Enum.GetValues<ItemTextTier>())
            {
                Assert.Equal(LookRules.ThemeLabel(tier, dark), LookRules.Label(tier, dark, fresh));
            }
        }
    }

    [Fact]
    public void S2138_a_fresh_set_leaves_every_item_fill_as_windows_draws_it()
    {
        FillChoice[] fills = [new FillChoice(), FillChoice.Transparent(), FillChoice.Of("#336699", 0.4)];
        foreach (var fill in fills)
        {
            foreach (var role in Enum.GetValues<ItemRole>())
            {
                foreach (var state in Enum.GetValues<ItemState>())
                {
                    foreach (var dark in new[] { true, false })
                    {
                        Assert.Equal(ItemLook.Ink(role, state, fill, dark), ItemLook.Ink(role, state, fill, dark, new LookTokens()));
                    }
                }
            }
        }
    }

    // ---- [S-2131] the frost ----

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 1)]
    [InlineData(40, 3)]
    [InlineData(48, 4)]
    [InlineData(80, 6)]
    [InlineData(500, 6)]
    public void S2131_the_frosts_blur_is_the_box_radius_nearest_its_sigma(double sigma, int radius)
        => Assert.Equal(radius, LookRules.Frost(new LookTokens { FrostBlur = sigma }).BoxRadius);

    [Fact]
    public void S2131_saturation_and_brightness_treat_the_picture_and_leave_its_alpha()
    {
        byte[] Pixel() => [0x20, 0x80, 0xE0, 0xFF]; // BGRA: a warm orange

        var same = Pixel();
        LookRules.Treat(same, new FrostLook(3, 1, 0));
        Assert.Equal(Pixel(), same);

        var grey = Pixel();
        LookRules.Treat(grey, new FrostLook(3, 0, 0));
        Assert.Equal(grey[0], grey[1]);
        Assert.Equal(grey[1], grey[2]);
        Assert.Equal(0xFF, grey[3]);

        var vivid = Pixel();
        LookRules.Treat(vivid, new FrostLook(3, 1.6, 0));
        Assert.True(vivid[2] - vivid[0] > Pixel()[2] - Pixel()[0], "more saturated: red and blue apart");

        var brighter = Pixel();
        LookRules.Treat(brighter, new FrostLook(3, 1, 0.2));
        Assert.All(brighter.Take(3).Zip(Pixel().Take(3)), p => Assert.True(p.First >= p.Second));
        var darker = Pixel();
        LookRules.Treat(darker, new FrostLook(3, 1, -0.2));
        Assert.All(darker.Take(3).Zip(Pixel().Take(3)), p => Assert.True(p.First <= p.Second));
    }

    // ---- [S-2132] the glass ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2132_a_fresh_set_is_the_themes_glass(bool dark)
    {
        var shaped = LiquidGlass.For(dark, new LookTokens());
        var theme = LiquidGlass.For(dark);

        Assert.Equal((theme.Tint, theme.TintMin, theme.TintMax, theme.RimWidth, theme.ShadowBlur, theme.ShadowOffsetY, theme.DividerAlpha), (shaped.Tint, shaped.TintMin, shaped.TintMax, shaped.RimWidth, shaped.ShadowBlur, shaped.ShadowOffsetY, shaped.DividerAlpha));
        Assert.Equal(theme.ShadowOpacity, shaped.ShadowOpacity, 9);
        Assert.Equal(theme.Rim, shaped.Rim);
        Assert.Equal(theme.Glow, shaped.Glow);
        Assert.Equal(theme.LensBands, shaped.LensBands);
    }

    [Fact]
    public void S2132_the_glass_tokens_shape_the_glass()
    {
        var tokens = new LookTokens
        {
            GlassTintLightMin = 0.3, GlassTintLightMax = 0.5, GlassTintDarkMin = 0.6, GlassTintDarkMax = 0.4,
            RimWidth = 2, RimStrength = 2, GlowStrength = 0, LensStrength = 0.5,
            ShadowRadius = 60, ShadowOffset = 20, ShadowStrength = 2, DividerOpacity = 0.3,
        };

        var light = LiquidGlass.For(false, tokens);
        var dark = LiquidGlass.For(true, tokens);

        Assert.Equal((0.3, 0.5), (light.TintMin, light.TintMax));
        Assert.Equal((0.4, 0.6), (dark.TintMin, dark.TintMax)); // the clearer of the two is the least
        Assert.Equal(2, light.RimWidth);
        Assert.All(light.Rim, s => Assert.InRange(s.Alpha, 0, 1));
        Assert.Equal(Math.Min(1, LiquidGlass.Light.Rim[^1].Alpha * 2), light.Rim[^1].Alpha, 9);
        Assert.All(light.Glow, s => Assert.Equal(0, s.Alpha));
        Assert.Equal(LiquidGlass.Light.LensBands.Select(b => b * 0.5), light.LensBands);
        Assert.Equal((60d, 20d, 0.3), (light.ShadowBlur, light.ShadowOffsetY, light.DividerAlpha));
        Assert.Equal(LiquidGlass.Light.ShadowOpacity * 2, light.ShadowOpacity, 9);
        Assert.Equal(LiquidGlass.Light.RadiusPx, light.RadiusPx);
    }

    // ---- [S-2133] the accent and the rendering mode ----

    [Fact]
    public void S2133_the_glass_and_a_default_card_take_the_accents_share_and_the_tinted_mode_at_least_its_own()
    {
        var accent = new RgbColor(0x00, 0x78, 0xD4);
        Assert.Equal(LiquidGlass.Light.Tint, LiquidGlass.For(false, new LookTokens(), accent).Tint);

        var tinted = LiquidGlass.For(false, new LookTokens { AccentTint = 0.4 }, accent).Tint;
        Assert.Equal(DesignLook.Over(FillChoice.Alpha(0.4), accent, LiquidGlass.Light.Tint), tinted);

        Assert.Equal(LookRules.TintedGlass, LookRules.AccentShare(new LookTokens { Rendering = RenderingMode.Tinted }));
        Assert.Equal(0.5, LookRules.AccentShare(new LookTokens { Rendering = RenderingMode.Tinted, AccentTint = 0.5 }));
        Assert.Equal(0.6, LookRules.AccentShare(new LookTokens { AccentTint = 9 }));
    }

    [Fact]
    public void S2133_monochrome_is_each_colours_own_grey_and_tinted_the_accents_hue_at_its_own_lightness()
    {
        var accent = new RgbColor(0x00, 0x78, 0xD4);
        var red = new RgbColor(0xE8, 0x11, 0x23);

        Assert.Equal(red, LookRules.Tone(red, RenderingMode.FullColour, accent));

        var grey = LookRules.Tone(red, RenderingMode.Monochrome, accent);
        Assert.Equal((grey.R, grey.R), (grey.G, grey.B));
        Assert.Equal(LiquidGlass.Luminance(red), LiquidGlass.Luminance(grey), 2);

        var toned = LookRules.Tone(red, RenderingMode.Tinted, accent);
        Assert.InRange(ColorFormats.ToHsl(toned).H, ColorFormats.ToHsl(accent).H - 2, ColorFormats.ToHsl(accent).H + 2);
        Assert.Equal(ColorFormats.ToHsl(red).L, ColorFormats.ToHsl(toned).L, 2);
    }

    // ---- [S-2134] the text levels ----

    [Fact]
    public void S2134_vibrancy_moves_each_level_from_windows_toward_its_token()
    {
        var tokens = new LookTokens { Vibrancy = 0.5, LabelSecondary = 0.5 };
        var windows = LookRules.ThemeLabel(ItemTextTier.Secondary, dark: true);

        Assert.Equal(windows + ((0.5 - windows) * 0.5), LookRules.Label(ItemTextTier.Secondary, true, tokens), 9);
        Assert.Equal(0.5, LookRules.Label(ItemTextTier.Secondary, true, new LookTokens { Vibrancy = 1, LabelSecondary = 0.5 }), 9);
        Assert.Equal(new LookTokens().LabelQuaternary, LookRules.Label(ItemTextTier.Disabled, false, new LookTokens { Vibrancy = 1 }), 9);
    }

    [Fact]
    public void S2134_windows_text_levels_are_its_own_alphas()
    {
        Assert.Equal(1, LookRules.ThemeLabel(ItemTextTier.Primary, true));
        Assert.Equal(0xC5 / 255.0, LookRules.ThemeLabel(ItemTextTier.Secondary, true));
        Assert.Equal(0xE4 / 255.0, LookRules.ThemeLabel(ItemTextTier.Primary, false));
        Assert.Equal(0x5C / 255.0, LookRules.ThemeLabel(ItemTextTier.Disabled, false));
    }

    /// <summary>
    /// [S-2134] The contrast guard: over any backdrop, in either theme, Liquid glass's own vibrant text reads on its glass
    /// at 4.5:1 (primary) and 3:1 (secondary), the tint rising as far as that takes (and its most is enough for every
    /// backdrop there is).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S2134_liquid_glass_text_reads_over_its_glass_at_4_5_primary_and_3_secondary_over_every_backdrop(bool dark)
    {
        var tokens = DesignPresets.LiquidGlassTokens;
        var look = LiquidGlass.For(dark, tokens);
        var (primary, secondary) = (LookRules.Label(ItemTextTier.Primary, dark, tokens), LookRules.Label(ItemTextTier.Secondary, dark, tokens));

        for (var behind = 0.0; behind <= 1.0001; behind += 0.02)
        {
            var alpha = LiquidGlass.TintOpacity(look, dark, behind, primary, secondary);
            Assert.InRange(alpha, look.TintMin, look.TintMax);
            var glass = Mix(Srgb(behind), Srgb(LiquidGlass.Luminance(look.Tint)), alpha);
            var ink = Srgb(LiquidGlass.Luminance(LookRules.Ink(dark)));
            Assert.True(LiquidGlass.Contrast(Linear(glass), Linear(Mix(glass, ink, primary))) >= 4.5 - 0.02, $"primary over {behind:0.00}");
            Assert.True(LiquidGlass.Contrast(Linear(glass), Linear(Mix(glass, ink, secondary))) >= 3 - 0.02, $"secondary over {behind:0.00}");
        }
    }

    [Fact]
    public void S2134_vibrant_text_asks_more_of_the_glass_than_windows_text()
    {
        var look = LiquidGlass.For(false, new LookTokens());
        var windows = LiquidGlass.TintOpacity(look, false, 0.2, LookRules.ThemeLabel(ItemTextTier.Primary, false), LookRules.ThemeLabel(ItemTextTier.Secondary, false));
        var vibrant = LiquidGlass.TintOpacity(look, false, 0.2, 0.7, 0.4);

        Assert.True(vibrant > windows);
    }

    private static double Srgb(double l) => l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double Mix(double from, double to, double t) => from + ((to - from) * t);

    // ---- [S-2135] [S-2136] the type ----

    [Theory]
    [InlineData(14, false, 400)]
    [InlineData(12, false, 400)]
    [InlineData(28, false, 400)]
    [InlineData(14, true, 600)]
    [InlineData(20, true, 600)]
    [InlineData(28, true, 600)]
    [InlineData(192, true, 600)]
    public void S2135_the_familys_own_weights_are_its_two(double size, bool strong, int weight)
        => Assert.Equal(weight, TypeLook.Default.WeightOf(size, strong));

    [Fact]
    public void S2135_each_role_takes_its_tokens_weight()
    {
        var look = new TypeLook(TypeWeight.Bold, TypeWeight.Light, TypeWeight.Medium, 0, 0, false);

        Assert.Equal(700, look.WeightOf(14, strong: true));   // the header's name
        Assert.Equal(700, look.WeightOf(20, strong: true));   // Title
        Assert.Equal(300, look.WeightOf(28, strong: true));   // a readout
        Assert.Equal(300, look.WeightOf(136, strong: true));  // a hero
        Assert.Equal(500, look.WeightOf(12, strong: false));  // a caption
        Assert.Equal(500, look.WeightOf(40, strong: false));  // a unit beside a hero
    }

    [Theory]
    [InlineData(12, -4)]
    [InlineData(14, -4)]
    [InlineData(20, -12.6)]
    [InlineData(21, -14)]
    [InlineData(28, -24)]
    [InlineData(192, -24)]
    public void S2136_tracking_runs_from_the_small_token_to_the_large_one_between_14_and_28(double size, double tracking)
        => Assert.Equal(tracking, new TypeLook(TypeWeight.Semibold, TypeWeight.Regular, TypeWeight.Regular, -24, -4, true).TrackingOf(size), 1);

    // ---- [S-2138] the items' surfaces ----

    [Fact]
    public void S2138_vibrant_items_are_the_ink_at_their_tokens()
    {
        var tokens = new LookTokens { InnerSurface = InnerSurface.Vibrant, InnerFill = 0.1, InnerHover = 0.05, InnerSelected = 0.2, TrackOpacity = 0.15 };

        Assert.Equal(new ItemInk(LookRules.Ink(true), null, 0.1), ItemLook.Ink(ItemRole.Tile, ItemState.Rest, new FillChoice(), true, tokens));
        var chip = ItemLook.Ink(ItemRole.Chip, ItemState.Hover, new FillChoice(), false, tokens);
        Assert.Equal((LookRules.Ink(false), (string?)null), (chip.Colour!.Value, chip.ThemeKey));
        Assert.Equal(0.15, chip.Opacity, 9);
        Assert.Equal(0.3, ItemLook.Ink(ItemRole.Tile, ItemState.Selected, new FillChoice(), true, tokens).Opacity, 9);
        Assert.Equal(0, ItemLook.Ink(ItemRole.Row, ItemState.Rest, new FillChoice(), true, tokens).Opacity);
        Assert.Equal(0.05, ItemLook.Ink(ItemRole.Row, ItemState.Hover, new FillChoice(), true, tokens).Opacity, 9);
        Assert.Equal(0.15, ItemLook.Ink(ItemRole.Track, ItemState.Rest, new FillChoice(), true, tokens).Opacity, 9);
        Assert.Equal(0.05, ItemLook.Ink(ItemRole.Tile, ItemState.Disabled, new FillChoice(), true, tokens).Opacity, 9);

        // A transparent layer's lit rows are vibrant too; a colour of the user's own stays theirs.
        Assert.Equal(new ItemInk(LookRules.Ink(true), null, 0.2), ItemLook.Ink(ItemRole.Row, ItemState.Selected, FillChoice.Transparent(), true, tokens));
        Assert.Equal(ItemLook.Ink(ItemRole.Tile, ItemState.Rest, FillChoice.Of("#336699", 0.5), true), ItemLook.Ink(ItemRole.Tile, ItemState.Rest, FillChoice.Of("#336699", 0.5), true, tokens));
    }

    // ---- [S-2140] the grain ----

    [Fact]
    public void S2140_the_grain_is_the_same_every_time_premultiplied_and_fine()
    {
        var a = LookRules.Grain(64);
        var b = LookRules.Grain(64);

        Assert.Equal(a, b);
        Assert.Equal(64 * 64 * 4, a.Length);
        for (var at = 0; at < a.Length; at += 4)
        {
            Assert.True(a[at] <= a[at + 3] && a[at] == a[at + 1] && a[at + 1] == a[at + 2]);
        }

        var alphas = Enumerable.Range(0, 64 * 64).Select(i => (int)a[(i * 4) + 3]).ToList();
        Assert.InRange(alphas.Average(), 100, 156);
        Assert.True(alphas.Distinct().Count() > 200);
        Assert.Throws<ArgumentOutOfRangeException>(() => LookRules.Grain(0));
    }

    // ---- [S-2142] the --design keys ----

    [Fact]
    public void S2142_every_number_token_is_a_design_key()
    {
        foreach (var n in LookTokens.Numbers)
        {
            var design = new DesignState();
            var value = n.Default == n.Max ? n.Min : n.Max;
            Assert.Empty(DesignText.Apply(design, $"{n.Key}={value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            Assert.Equal(value, n.Get(design.Tokens));
        }
    }

    [Fact]
    public void S2142_enum_tokens_go_by_their_names_and_bad_values_are_refused()
    {
        var design = new DesignState();
        var refused = DesignText.Apply(design,
            "rendering=monochrome,headerWeight=bold,readoutWeight=light,bodyWeight=medium,headerTone=primary,headerCase=upper,"
            + "innerSurface=vibrant,rowDividers=inset,tabularNumerals=on,vibrancy=50%,frostBlur=999,material=liquidGlass,"
            + "rendering=7,headerWeight=heavy,vibrancy=lots,tabularNumerals=maybe");

        Assert.Equal(["rendering=7", "headerWeight=heavy", "vibrancy=lots", "tabularNumerals=maybe"], refused);
        var t = design.Tokens;
        Assert.Equal((RenderingMode.Monochrome, TypeWeight.Bold, TypeWeight.Light, TypeWeight.Medium), (t.Rendering, t.HeaderWeight, t.ReadoutWeight, t.BodyWeight));
        Assert.Equal((HeaderTone.Primary, HeaderCase.Upper, InnerSurface.Vibrant, RowDivider.Inset, true), (t.HeaderTone, t.HeaderCase, t.InnerSurface, t.RowDividers, t.TabularNumerals));
        Assert.Equal((0.5, 80d), (t.Vibrancy, t.FrostBlur));
        Assert.Equal(CardMaterial.LiquidGlass, design.Material);
    }

    // ---- [S-2143] the styles own the tokens ----

    [Fact]
    public void S2143_liquid_glass_is_vibrant_vivid_tracked_tight_on_nested_glass_with_inset_dividers()
    {
        var t = DesignPresets.Get(DesignPresetId.LiquidGlass).Values.Tokens!;

        Assert.Equal(1, t.Vibrancy);
        Assert.True(t.FrostSaturation > 1.3 && t.FrostBlur >= 40);
        Assert.Equal(InnerSurface.Vibrant, t.InnerSurface);
        Assert.Equal(RowDivider.Inset, t.RowDividers);
        Assert.Equal(HeaderTone.Primary, t.HeaderTone);
        Assert.True(t.TrackingLarge < 0 && t.TrackingSmall <= 0 && t.TabularNumerals);
        Assert.Equal(RenderingMode.FullColour, t.Rendering);
        Assert.Equal(0, t.AccentTint);
        Assert.Empty(t.Clone().Repair());
    }

    [Fact]
    public void S2143_every_other_style_draws_with_a_fresh_set()
        => Assert.All(DesignPresets.All.Where(p => p.Id != DesignPresetId.LiquidGlass), p => Assert.True((p.Values.Tokens ?? new LookTokens()).SameAs(new LookTokens()), p.Id.ToString()));

    [Fact]
    public void S2143_a_style_writes_its_tokens_as_a_copy_and_a_changed_token_is_a_custom_look()
    {
        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.LiquidGlass);
        Assert.Equal(DesignPresetId.LiquidGlass, DesignPresets.Match(design));
        Assert.NotSame(DesignPresets.Get(DesignPresetId.LiquidGlass).Values.Tokens, design.Tokens);

        design.Tokens.Vibrancy = 0.4;
        Assert.Null(DesignPresets.Match(design));
        Assert.True(DesignPresets.ReplacesCustomLook(design, DesignPresetId.Default));
        Assert.Equal(1, DesignPresets.Get(DesignPresetId.LiquidGlass).Values.Tokens!.Vibrancy);

        DesignPresets.Apply(design, DesignPresetId.Midnight);
        Assert.True(design.Tokens.SameAs(new LookTokens()));
    }
}
