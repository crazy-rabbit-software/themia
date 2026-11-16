using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3526] Appearance ›: the look of every widget, in the groups the Appearance page had ([S-3830]):
/// Style (the presets, [S-834]), Theme and colours, Item background, Cards (the gap between them
/// with them), Text and size; Motion is General's ([S-2300]). Snap is General's too ([S-3516]) and a scale
/// a display's or a widget's ([S-3590]): neither is here. Every line writes the very field the page wrote, through one
/// <see cref="ISettingsMenuHost.Mutate"/>, and the appearance engine applies it live.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3526] The Appearance line and its submenu.</summary>
    public static SettingsSubmenu Appearance(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        const string at = Keys.Appearance;
        return new SettingsSubmenu(at, Str.Settings_Section_Appearance.Text(), Glyphs.Palette,
        [
            Style(host, at + "/style"),
            new SettingsSeparator(at + "/-style"),
            ThemeAndColours(host, at + "/theme"),
            Items(host, at + "/items"),
            Cards(host, at + "/cards"),
            Material(host, at + "/material"),
            EdgeAndLift(host, at + "/edge"),
            TextAndSize(host, at + "/text"),
            Surfaces(host, at + "/surfaces"),
            Layout(host, at + "/layout"),
        ]);
    }

    // ---- [S-2141] the look tokens ----

    /// <summary>[S-2141] A number token's slider: its range and step are the token's (<see cref="LookTokens.Numbers"/>), one write each.</summary>
    private static SettingsSlider TokenSlider(ISettingsMenuHost host, string key, string token, Str text, string glyph, Func<double, string> format)
    {
        var number = LookTokens.Number(token) ?? throw new ArgumentOutOfRangeException(nameof(token), token, "not a look token");
        var tokens = host.State.Design.Tokens ?? new LookTokens();
        return new SettingsSlider(key + "/" + token, text.Text(), glyph, number.Clamp(number.Get(tokens)), number.Min, number.Max, number.Step, format,
            v => host.Mutate(s => number.Set(s.Design.Tokens ??= new LookTokens(), number.Clamp(v))));
    }

    /// <summary>[S-2141] An enum token's choice, one write each.</summary>
    private static SettingsChoice TokenChoice<T>(ISettingsMenuHost host, string key, Str text, string glyph, IReadOnlyList<(T Value, Str Text)> options, Func<LookTokens, T> get, Action<LookTokens, T> set)
        where T : struct, Enum
        => Choice(host, key, text.Text(), glyph, [.. options.Select(o => (o.Value, o.Text.Text()))], get(host.State.Design.Tokens ?? new LookTokens()),
            (s, v) => set(s.Design.Tokens ??= new LookTokens(), v));

    /// <summary>The weights a type token may take, lightest first.</summary>
    private static IReadOnlyList<(TypeWeight, Str)> Weights { get; } =
    [
        (TypeWeight.Light, Str.SettingsPages_Look_WeightLight), (TypeWeight.Semilight, Str.SettingsPages_Look_WeightSemilight),
        (TypeWeight.Regular, Str.SettingsPages_Look_WeightRegular), (TypeWeight.Medium, Str.SettingsPages_Look_WeightMedium),
        (TypeWeight.Semibold, Str.SettingsPages_Look_WeightSemibold), (TypeWeight.Bold, Str.SettingsPages_Look_WeightBold),
    ];

    /// <summary>
    /// [S-2141] Material: the rendering mode, the frost under every translucent card (blur, saturation, brightness), the
    /// accent in the card and the grain; the glass's own (its tint per theme, its glow and lens) in an expander.
    /// </summary>
    private static SettingsSubmenu Material(ISettingsMenuHost host, string key)
    {
        var glass = key + "/glass";
        return new SettingsSubmenu(key, Str.SettingsPages_Look_Material.Text(), Glyphs.Surface,
        [
            TokenChoice(host, key + "/rendering", Str.SettingsPages_Look_Rendering, Glyphs.Palette,
                [(RenderingMode.FullColour, Str.SettingsPages_Look_FullColour), (RenderingMode.Tinted, Str.SettingsPages_Look_Tinted), (RenderingMode.Monochrome, Str.SettingsPages_Look_Monochrome)],
                t => t.Rendering, (t, v) => t.Rendering = v),
            TokenSlider(host, key, "frostBlur", Str.SettingsPages_Look_FrostBlur, Glyphs.Photo, v => Unit(v, "px")),
            TokenSlider(host, key, "frostSaturation", Str.SettingsPages_Look_FrostSaturation, Glyphs.Palette, Percent),
            TokenSlider(host, key, "frostBrightness", Str.SettingsPages_Look_FrostBrightness, Glyphs.Brightness, Percent),
            TokenSlider(host, key, "accentTint", Str.SettingsPages_Look_AccentTint, Glyphs.Theme, Percent),
            TokenSlider(host, key, "grain", Str.SettingsPages_Look_Grain, Glyphs.Opacity, Percent),
            new SettingsSubmenu(glass, Str.SettingsPages_Look_Glass.Text(), Glyphs.Surface,
            [
                TokenSlider(host, glass, "glassTintLightMin", Str.SettingsPages_Look_GlassTintLightMin, Glyphs.Sun, Percent),
                TokenSlider(host, glass, "glassTintLightMax", Str.SettingsPages_Look_GlassTintLightMax, Glyphs.Sun, Percent),
                TokenSlider(host, glass, "glassTintDarkMin", Str.SettingsPages_Look_GlassTintDarkMin, Glyphs.Moon, Percent),
                TokenSlider(host, glass, "glassTintDarkMax", Str.SettingsPages_Look_GlassTintDarkMax, Glyphs.Moon, Percent),
                TokenSlider(host, glass, "glowStrength", Str.SettingsPages_Look_Glow, Glyphs.Brightness, Percent),
                TokenSlider(host, glass, "lensStrength", Str.SettingsPages_Look_Lens, Glyphs.Opacity, Percent),
            ]),
        ]);
    }

    /// <summary>[S-2141] Edge and lift: the glass's rim (its width and light) and its lift (how far, how low, how dark).</summary>
    private static SettingsSubmenu EdgeAndLift(ISettingsMenuHost host, string key)
        => new(key, Str.SettingsPages_Look_EdgeAndLift.Text(), Glyphs.Fit,
        [
            TokenSlider(host, key, "rimWidth", Str.SettingsPages_Look_RimWidth, Glyphs.Fit, v => Unit(v, "px")),
            TokenSlider(host, key, "rimStrength", Str.SettingsPages_Look_RimStrength, Glyphs.Brightness, Percent),
            TokenSlider(host, key, "shadowRadius", Str.SettingsPages_Look_ShadowRadius, Glyphs.Opacity, v => Unit(v, "px")),
            TokenSlider(host, key, "shadowOffset", Str.SettingsPages_Look_ShadowOffset, Glyphs.MoveDown, v => Unit(v, "px")),
            TokenSlider(host, key, "shadowStrength", Str.SettingsPages_Look_ShadowStrength, Glyphs.Opacity, Percent),
        ]);

    /// <summary>[S-2141] Surfaces: the items' fills (Windows' or vibrant) and a vibrant fill's opacity at rest, lit, selected and as a track.</summary>
    private static SettingsSubmenu Surfaces(ISettingsMenuHost host, string key)
        => new(key, Str.SettingsPages_Look_Surfaces.Text(), Glyphs.Surface,
        [
            TokenChoice(host, key + "/innerSurface", Str.SettingsPages_Look_InnerSurface, Glyphs.Surface,
                [(InnerSurface.Theme, Str.SettingsPages_Look_SurfaceWindows), (InnerSurface.Vibrant, Str.SettingsPages_Look_SurfaceVibrant)],
                t => t.InnerSurface, (t, v) => t.InnerSurface = v),
            TokenSlider(host, key, "innerFill", Str.SettingsPages_Look_InnerFill, Glyphs.Opacity, Percent),
            TokenSlider(host, key, "innerHover", Str.SettingsPages_Look_InnerHover, Glyphs.Opacity, Percent),
            TokenSlider(host, key, "innerSelected", Str.SettingsPages_Look_InnerSelected, Glyphs.Opacity, Percent),
            TokenSlider(host, key, "trackOpacity", Str.SettingsPages_Look_Track, Glyphs.Gauge, Percent),
        ]);

    /// <summary>[S-2141] Layout: the list rows' dividers and a divider's strength (a glass group's too).</summary>
    private static SettingsSubmenu Layout(ISettingsMenuHost host, string key)
        => new(key, Str.SettingsPages_Look_Layout.Text(), Glyphs.ListLayout,
        [
            TokenChoice(host, key + "/rowDividers", Str.SettingsPages_Look_RowDividers, Glyphs.ListLayout,
                [(RowDivider.None, Str.Settings_Style_None), (RowDivider.Hairline, Str.SettingsPages_Look_DividerHairline), (RowDivider.Inset, Str.SettingsPages_Look_DividerInset)],
                t => t.RowDividers, (t, v) => t.RowDividers = v),
            TokenSlider(host, key, "dividerOpacity", Str.SettingsPages_Look_DividerOpacity, Glyphs.Opacity, Percent),
        ]);

    /// <summary>
    /// [S-834] [S-3527] Style: the presets, and Custom (greyed, the dot on it) while the design matches
    /// none. A pick writes the preset's fields in one change; over a look the user made it asks
    /// first ([S-6483]), and Cancel changes nothing.
    /// </summary>
    private static SettingsChoice Style(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var match = DesignPresets.Match(design);
        var options = new List<SettingsRadio>();
        foreach (var preset in DesignPresets.All)
        {
            var id = preset.Id;
            options.Add(new SettingsRadio(key + "/" + id, preset.Label, match == id, () => PickPreset(host, id)));
        }

        if (match is null)
        {
            options.Add(new SettingsRadio(key + "/custom", Str.Settings_Style_Preset_Custom.Text(), true, () => { }) { Disabled = true });
        }

        return new SettingsChoice(key, Str.Settings_Nav_Style.Text(), Glyphs.Palette, options);
    }

    /// <summary>[S-6483] Apply a preset; asked first where it would overwrite a look of the user's own.</summary>
    private static void PickPreset(ISettingsMenuHost host, DesignPresetId id)
    {
        var design = host.State.Design;
        if (DesignPresets.Match(design) == id)
        {
            return;
        }

        void Apply() => host.Mutate(s => DesignPresets.Apply(s.Design, id));

        if (DesignPresets.ReplacesCustomLook(design, id))
        {
            host.Confirm(new SettingsConfirm(
                Str.Shell_Appearance_ReplaceLook_Title.Text(),
                Str.Shell_Appearance_ReplaceLook_Action.Text(),
                Apply,
                Str.Shell_Appearance_ReplaceLook_Body.Format(("preset", DesignPresets.Get(id).Label))));
            return;
        }

        Apply();
    }

    /// <summary>
    /// [S-3830] Theme and colours: the theme, the accent, the widget background, blur behind widgets
    /// ([S-2110]) and the header's fill and opacity ([S-828]).
    /// </summary>
    private static SettingsSubmenu ThemeAndColours(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var theme = Choice(host, key + "/theme", Str.Settings_Theme_Label.Text(), Glyphs.Theme,
            [(AppTheme.System, Str.Shell_Appearance_UseSystem.Text()), (AppTheme.Light, Str.Settings_Theme_Light.Text()), (AppTheme.Dark, Str.Settings_Theme_Dark.Text())],
            design.Theme, (s, v) => s.Design.Theme = v);

        return new SettingsSubmenu(key, Str.Shell_Appearance_ThemeColours.Text(), Glyphs.Theme,
        [
            theme,
            new SettingsSeparator(key + "/-colours"),
            new SettingsColour(key + "/accent", Str.Shell_Appearance_Accent.Text(), design.PrimaryColor,
                v => host.Mutate(s => s.Design.PrimaryColor = v), Str.Shell_Appearance_WindowsAccent.Text(), ColorPresets.Windows) { DefaultIsWindowsAccent = true },

            // [S-6568] The widget background: the Windows card, none, or a colour at an opacity. Its
            // group says whose: "Background" here, as on the page's group, so the words fit the menu.
            new SettingsFill(key + "/background", Str.Settings_Style_BgLabel.Text(), design.Background,
                v => host.Mutate(s => s.Design.Background = v), Str.Shell_Appearance_ThemeColour.Text()),

            // [S-2110] The wallpaper, blurred, under every translucent card: the background's own glass.
            new SettingsToggle(key + "/blur", Str.Shell_Appearance_BlurBehind.Text(), Glyphs.Photo, design.BlurBehind,
                v => host.Mutate(s => s.Design.BlurBehind = v)),
            new SettingsSeparator(key + "/-header"),

            // [S-828] The header strip, on its own terms: its fill (the line "Header", its value the colour) and its opacity.
            new SettingsColour(key + "/headerBackground", Str.App_ConfigHeaderLabel.Text(), design.HeaderBackgroundColor,
                v => host.Mutate(s => s.Design.HeaderBackgroundColor = v), Str.Shell_Appearance_SameAsWidget.Text(), ColorPresets.Surfaces),
            new SettingsSlider(key + "/headerOpacity", Str.Settings_Style_HeaderOpacity_Label.Text(), Glyphs.Opacity, design.HeaderOpacity, 0, 1, 0.05, Percent,
                v => host.Mutate(s => s.Design.HeaderOpacity = v)),
        ]);
    }

    /// <summary>[S-6516] [S-6545] Item background: what the rows, tiles and chips inside every card are filled with, and the text on them.</summary>
    private static SettingsSubmenu Items(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        return new SettingsSubmenu(key, Str.Shell_Items_Background.Text(), Glyphs.Surface,
        [
            new SettingsFill(key + "/background", Str.Settings_Style_BgLabel.Text(), design.ItemBackground,
                v => host.Mutate(s => s.Design.ItemBackground = v), Str.Shell_Appearance_ThemeColour.Text()),
            new SettingsColour(key + "/text", Str.Shell_Items_Text.Text(), design.ItemTextColor,
                v => host.Mutate(s => s.Design.ItemTextColor = v), Str.Shell_Appearance_ThemeColour.Text(), ColorPresets.Surfaces),
        ]);
    }

    /// <summary>[S-3830] [S-1722] Cards: the border's style (the line's value), the corners, the gap between the cards, the border's width, colour and opacity.</summary>
    private static SettingsSubmenu Cards(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var border = Choice(host, key + "/border", Str.Settings_Style_Border.Text(), Glyphs.Grid,
            [
                (WidgetBorderStyle.None, Str.Settings_Style_None.Text()),
                (WidgetBorderStyle.Solid, Str.Settings_Style_Solid.Text()),
                (WidgetBorderStyle.Dashed, Str.Settings_Style_Dashed.Text()),
                (WidgetBorderStyle.Dotted, Str.Settings_Style_Dotted.Text()),
            ],
            design.BorderStyle, (s, v) => s.Design.BorderStyle = v);

        return new SettingsSubmenu(key, Str.Shell_Appearance_Cards.Text(), Glyphs.Grid,
        [
            // [S-2123] What the cards are made of: solid, or Liquid glass (a style sets it too).
            Choice(host, key + "/material", Str.SettingsPages_Appearance_Material.Text(), Glyphs.Surface,
                [(CardMaterial.Solid, Str.SettingsPages_Appearance_Solid.Text()), (CardMaterial.LiquidGlass, Str.Settings_Style_Preset_Name_LiquidGlass.Text())],
                design.Material, (s, v) => s.Design.Material = v),
            new SettingsSlider(key + "/radius", Str.Settings_Style_CornerRadius_Label.Text(), Glyphs.Surface, design.CornerRadiusPx, 0, 32, 1, v => Unit(v, "px"),
                v => host.Mutate(s => s.Design.CornerRadiusPx = v)),
            new SettingsSeparator(key + "/-border"),
            border,
            new SettingsSlider(key + "/borderWidth", Str.Settings_Style_BorderWidth_Label.Text(), Glyphs.Fit, design.BorderWidthPx, 0, 8, 1, v => Unit(v, "px"),
                v => host.Mutate(s => s.Design.BorderWidthPx = v)),
            new SettingsColour(key + "/borderColour", Str.Shell_Appearance_BorderColour.Text(), design.BorderColor,
                v => host.Mutate(s => s.Design.BorderColor = v), Str.Shell_Appearance_ThemeColour.Text(), ColorPresets.Surfaces),
            new SettingsSlider(key + "/borderOpacity", Str.Settings_Style_BorderOpacity_Label.Text(), Glyphs.Opacity, design.BorderOpacity, 0, 1, 0.05, Percent,
                v => host.Mutate(s => s.Design.BorderOpacity = v)),
        ]) { Value = border.Value };
    }

    /// <summary>
    /// [S-3830] Text and size: the font ([S-3528]) and the text size, the user's accessibility choice; [S-2141] the type
    /// tokens (the vibrancy of the text levels, the weight per role, the tracking, tabular figures, the header's tone and
    /// case) and each level's own opacity in an expander.
    /// </summary>
    private static SettingsSubmenu TextAndSize(ISettingsMenuHost host, string key)
    {
        var design = host.State.Design;
        var levels = key + "/levels";
        return new SettingsSubmenu(key, Str.Shell_Appearance_TextAndSize.Text(), Glyphs.Font,
        [
            new SettingsFont(key + "/font", Str.Settings_Font_FamilyLabel.Text(), Glyphs.Font, design.FontFamily,
                v => host.Mutate(s => s.Design.FontFamily = v)),
            new SettingsSlider(key + "/size", Str.Shell_Appearance_TextSize.Text(), Glyphs.TextSize, design.FontScale, FamilyType.MinTextScale, FamilyType.MaxTextScale, 0.05, Percent,
                v => host.Mutate(s => s.Design.FontScale = v)),
            TokenSlider(host, key, "vibrancy", Str.SettingsPages_Look_Vibrancy, Glyphs.Opacity, Percent),
            TokenChoice(host, key + "/headerWeight", Str.SettingsPages_Look_HeaderWeight, Glyphs.Bold, Weights, t => t.HeaderWeight, (t, v) => t.HeaderWeight = v),
            TokenChoice(host, key + "/readoutWeight", Str.SettingsPages_Look_ReadoutWeight, Glyphs.Bold, Weights, t => t.ReadoutWeight, (t, v) => t.ReadoutWeight = v),
            TokenChoice(host, key + "/bodyWeight", Str.SettingsPages_Look_BodyWeight, Glyphs.Bold, Weights, t => t.BodyWeight, (t, v) => t.BodyWeight = v),
            TokenSlider(host, key, "trackingLarge", Str.SettingsPages_Look_TrackingLarge, Glyphs.Fit, v => Unit(v, "‰")),
            TokenSlider(host, key, "trackingSmall", Str.SettingsPages_Look_TrackingSmall, Glyphs.Fit, v => Unit(v, "‰")),
            new SettingsToggle(key + "/tabularNumerals", Str.SettingsPages_Look_Tabular.Text(), Glyphs.Count, (design.Tokens ?? new LookTokens()).TabularNumerals,
                v => host.Mutate(s => (s.Design.Tokens ??= new LookTokens()).TabularNumerals = v)),
            TokenChoice(host, key + "/headerTone", Str.SettingsPages_Look_HeaderTone, Glyphs.Font,
                [(HeaderTone.Secondary, Str.SettingsPages_Look_ToneQuiet), (HeaderTone.Primary, Str.SettingsPages_Look_ToneStrong)],
                t => t.HeaderTone, (t, v) => t.HeaderTone = v),
            TokenChoice(host, key + "/headerCase", Str.SettingsPages_Look_HeaderCase, Glyphs.Font,
                [(HeaderCase.AsWritten, Str.SettingsPages_Look_CaseAsWritten), (HeaderCase.Upper, Str.SettingsPages_Look_CaseUpper)],
                t => t.HeaderCase, (t, v) => t.HeaderCase = v),
            new SettingsSubmenu(levels, Str.SettingsPages_Look_TextLevels.Text(), Glyphs.FontColour,
            [
                TokenSlider(host, levels, "labelPrimary", Str.SettingsPages_Look_LabelPrimary, Glyphs.FontColour, Percent),
                TokenSlider(host, levels, "labelSecondary", Str.SettingsPages_Look_LabelSecondary, Glyphs.FontColour, Percent),
                TokenSlider(host, levels, "labelTertiary", Str.SettingsPages_Look_LabelTertiary, Glyphs.FontColour, Percent),
                TokenSlider(host, levels, "labelQuaternary", Str.SettingsPages_Look_LabelQuaternary, Glyphs.FontColour, Percent),
            ]),
        ]);
    }

    /// <summary>[S-826] The four font presets, under their names in the UI's language, before the installed families.</summary>
    public static IReadOnlyList<FontOption> FontPresets() =>
    [
        new(DesignLook.SystemFont, Str.Shell_Appearance_SystemFont.Text()),
        new(DesignLook.SansFont, Str.Shell_Appearance_SansFont.Text()),
        new(DesignLook.SerifFont, Str.Settings_Font_Serif.Text()),
        new(DesignLook.MonoFont, Str.Settings_Font_Mono.Text()),
    ];

    /// <summary>[S-6441] The name of the font <paramref name="current"/> picks: a preset's name, a family's own, the system font's for none.</summary>
    public static string FontName(string? current)
    {
        var options = FontSearch.Options(FontPresets(), InstalledFonts.None, current);
        var chosen = FontSearch.Chosen(options, current);
        return options.First(o => o.Value == chosen).Label;
    }

    /// <summary>
    /// [S-3718] A choice of one over an enum: the dot on the value that holds, a pick of another writes
    /// it in one change, the one that holds changes nothing.
    /// </summary>
    private static SettingsChoice Choice<T>(ISettingsMenuHost host, string key, string text, string? glyph, IReadOnlyList<(T Value, string Text)> options, T current, Action<AppState, T> set)
        where T : struct, Enum
    {
        var radios = new List<SettingsRadio>(options.Count);
        foreach (var (value, label) in options)
        {
            var holds = EqualityComparer<T>.Default.Equals(value, current);
            radios.Add(new SettingsRadio(key + "/" + value, label, holds, () =>
            {
                if (!holds)
                {
                    host.Mutate(s => set(s, value));
                }
            }));
        }

        return new SettingsChoice(key, text, glyph, radios);
    }
}
