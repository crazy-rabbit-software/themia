using Themia.Core.Widgets;
using Themia.Core.Localization;

namespace Themia.Core.Model;

/// <summary>
/// A design preset's id, in the order the Style picker lists them ([S-2122]: Karpo's palettes' order, Liquid glass
/// among the see-through ones). Closed set: it is what the picker compares and <c>--design preset=</c> names.
/// </summary>
public enum DesignPresetId
{
    Default = 0,
    Graphite = 1,
    Daylight = 2,
    Midnight = 3,
    Contrast = 4,
    Acrylic = 5,
    Glacier = 6,
    LiquidGlass = 7,
    Paper = 8,
    Harbor = 9,
    Bloom = 10,
    Ember = 11,
    Blueprint = 12,
    Moss = 13,
}

/// <summary>
/// The look a preset sets: the seventeen appearance fields it owns (the look tokens one of them), and nothing else. [S-830]
/// [S-2120] Blur behind widgets ([S-2110]) is one of them: a frosted preset and an opaque one are
/// different looks.
/// [S-6565] The background and the item background are each one fill choice ([S-6560]).
/// [S-6515] The item layer: its background ([S-6510]) and its text colour ([S-6540]); a preset that does
/// not name them leaves the items on their Fluent fills and text.
/// [S-2123] What the cards are made of: solid, or Liquid glass.
/// [S-2143] The look tokens (<see cref="LookTokens"/>): a preset that names none draws with a fresh set, which is
/// Windows' and the family's own look.
/// A preset's fill choices and tokens are templates: <see cref="DesignPresets.Apply"/> writes copies.
/// </summary>
public sealed record DesignPresetValues(
    AppTheme Theme,
    string PrimaryColor,
    string FontFamily,
    FillChoice Background,
    WidgetBorderStyle BorderStyle,
    double BorderWidthPx,
    string BorderColor,
    double BorderOpacity,
    double CornerRadiusPx,
    bool BlurBehind,
    FillChoice? Items = null,
    string ItemTextColor = "",
    CardMaterial Material = CardMaterial.Solid,
    LookTokens? Tokens = null);

/// <summary>One named look.</summary>
public sealed record DesignPreset(DesignPresetId Id, Str LabelId, DesignPresetValues Values)
{
    /// <summary>The preset's name in the current language (the list is static, so it is read when shown).</summary>
    public string Label => LabelId.Text();
}

/// <summary>
/// [S-830]..[S-833] [S-2122] Named bundles of appearance settings, applied in one write: Karpo's palettes (karpo-app,
/// Karpo.Core/Looks.cs <c>LookPalettes.All</c>, docs/widgets.md §507, §568, §618, §624) in Karpo's order and under its
/// names — Themia's own first, then the neutrals (Graphite, Daylight, Midnight, Contrast), the see-through ones (Acrylic,
/// Glacier, and Liquid glass, [S-2123]), the colours (Paper, Harbor, Bloom, Ember) and the patterned ones (Blueprint,
/// Moss). A Karpo palette is a light and a dark design under one name, the taskbar's mode choosing; a Themia preset owns
/// its theme, so each is its palette's cards in the mode it was made for (the one whose text it reads with); Liquid
/// glass alone follows the system's theme, its glass a pair. Karpo's card colours (background with its alpha, accent,
/// outline) are the preset's background, accent and border (Karpo's near-opaque cards, 98 % and up, are opaque here: no
/// blur lies under an opaque preset, [S-2121]); its fonts are the family alone (Sitka is Themia's serif,
/// Bahnschrift by name; Karpo's semibold weight has no field here); its patterns (Midnight's dots, Glacier's grain,
/// Blueprint's grid, Moss's contours) are not drawn: a card has no pattern layer.
/// <para>
/// A preset owns the <b>look</b>: theme, accent, font family, widget and header fill, border, shape, frost and
/// material. It never owns the widget scale, the text size or any animation setting: those are the user's
/// accessibility and motion choices, and applying a colour scheme must not reset someone's 150% text to 100% or switch
/// motion back on.
/// </para>
/// </summary>
public static class DesignPresets
{
    /// <summary>
    /// [S-831] [S-2122] Default (Themia's own, Karpo's "Karpo") is whatever a fresh <see cref="DesignState"/> holds, read
    /// from it rather than copied, so a fresh install reads as Default and not "Custom" even after a default changes.
    /// </summary>
    public static IReadOnlyList<DesignPreset> All { get; } =
    [
        new(DesignPresetId.Default, Str.Settings_Style_Preset_Name_Themia, ValuesOf(new DesignState())),

        // [S-2122] Graphite, Karpo's calm neutral grey, its dark design: #27292D cards, a periwinkle accent, no outline.
        new(DesignPresetId.Graphite, Str.Settings_Style_Preset_Name_Graphite, new(
            AppTheme.Dark, "#8FA6FF", DesignLook.SystemFont, FillChoice.Of("#27292D", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Daylight, light first: white cards, Windows' blue, a black hairline at 10 %.
        new(DesignPresetId.Daylight, Str.Settings_Style_Preset_Name_Daylight, new(
            AppTheme.Light, "#0067C0", DesignLook.SystemFont, FillChoice.Of("#FFFFFF", 1),
            WidgetBorderStyle.Solid, 1, "#000000", 0.10, 8, BlurBehind: false)),

        // [S-2122] Midnight, dark first: near-black cards, a lavender accent, a white hairline at 12 %.
        new(DesignPresetId.Midnight, Str.Settings_Style_Preset_Name_Midnight, new(
            AppTheme.Dark, "#A99BFF", DesignLook.SystemFont, FillChoice.Of("#121214", 1),
            WidgetBorderStyle.Solid, 1, "#FFFFFF", 0.12, 8, BlurBehind: false)),

        // [S-2122] Contrast: black cards outlined white, a yellow accent.
        new(DesignPresetId.Contrast, Str.Settings_Style_Preset_Name_Contrast, new(
            AppTheme.Dark, "#FFE500", DesignLook.SystemFont, FillChoice.Of("#000000", 1),
            WidgetBorderStyle.Solid, 1, "#FFFFFF", 1, 8, BlurBehind: false)),

        // [S-2122] Acrylic: Windows' own grey three quarters over the blurred wallpaper, a white hairline at 12 %.
        new(DesignPresetId.Acrylic, Str.Settings_Style_Preset_Name_Acrylic, new(
            AppTheme.Dark, "#60CDFF", DesignLook.SystemFont, FillChoice.Of("#2C2C2C", 0.75),
            WidgetBorderStyle.Solid, 1, "#FFFFFF", 0.12, 8, BlurBehind: true)),

        // [S-2122] Glacier: icy white at 80 % over the blur, a white rim at 60 %, a deep teal accent.
        new(DesignPresetId.Glacier, Str.Settings_Style_Preset_Name_Glacier, new(
            AppTheme.Light, "#0A6B9C", DesignLook.SystemFont, FillChoice.Of("#F2F8FC", 0.80),
            WidgetBorderStyle.Solid, 1, "#FFFFFF", 0.60, 8, BlurBehind: true)),

        // [S-2123] Liquid glass: the system's theme, the glass's own clear tint over the blur, its lit rim (no border of
        // the design's), round 18 like Apple's controls.
        new(DesignPresetId.LiquidGlass, Str.Settings_Style_Preset_Name_LiquidGlass, new(
            AppTheme.System, "", DesignLook.SystemFont, new FillChoice(),
            WidgetBorderStyle.None, 1, "", 1, LiquidGlass.Light.RadiusPx, BlurBehind: true, Material: CardMaterial.LiquidGlass,
            Tokens: LiquidGlassTokens)),

        // [S-2122] Paper, warm paper and a serif (Karpo's Sitka): opaque cream cards, a burnt sienna accent.
        new(DesignPresetId.Paper, Str.Settings_Style_Preset_Name_Paper, new(
            AppTheme.Light, "#A84B24", DesignLook.SerifFont, FillChoice.Of("#FAF6EE", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Harbor, navy and teal in Bahnschrift.
        new(DesignPresetId.Harbor, Str.Settings_Style_Preset_Name_Harbor, new(
            AppTheme.Dark, "#4CC8DF", HarborFont, FillChoice.Of("#13283F", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Bloom, plum and raspberry: pink cards, plum text.
        new(DesignPresetId.Bloom, Str.Settings_Style_Preset_Name_Bloom, new(
            AppTheme.Light, "#B0204F", DesignLook.SystemFont, FillChoice.Of("#F7CFDE", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Ember: burnt orange cards with white text, the accent forward.
        new(DesignPresetId.Ember, Str.Settings_Style_Preset_Name_Ember, new(
            AppTheme.Dark, "#FFE0B8", DesignLook.SystemFont, FillChoice.Of("#B23F14", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Blueprint, navy and blue in Bahnschrift (Karpo's grid not drawn).
        new(DesignPresetId.Blueprint, Str.Settings_Style_Preset_Name_Blueprint, new(
            AppTheme.Dark, "#7CC4FF", HarborFont, FillChoice.Of("#13315C", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),

        // [S-2122] Moss, forest greens (Karpo's contours not drawn).
        new(DesignPresetId.Moss, Str.Settings_Style_Preset_Name_Moss, new(
            AppTheme.Dark, "#8FD18A", DesignLook.SystemFont, FillChoice.Of("#1E2E25", 1),
            WidgetBorderStyle.None, 1, "", 1, 8, BlurBehind: false)),
    ];

    /// <summary>
    /// [S-2143] Liquid glass's tokens, after macOS 26's desktop widgets: a frost more vivid than the wallpaper (Apple's
    /// materials boost saturation) and a touch brighter, the glass's own tint, rim, glow, lens and lift (the tokens'
    /// defaults), vibrant text levels (the ink at macOS's label opacities), the header's name in the primary level, regular
    /// readouts tracked tight as SF Display's large sizes are, tabular figures, the items on vibrant fills (a nested
    /// glass: the ink at a whisper) and hairline dividers from the rows' text edge. Full colour, no accent in the glass.
    /// </summary>
    public static LookTokens LiquidGlassTokens => new()
    {
        FrostBlur = 48,
        FrostSaturation = 1.6,
        FrostBrightness = 0.04,
        Grain = 0.02,
        Vibrancy = 1,
        ReadoutWeight = Sdk.Kit.TypeWeight.Regular,
        TrackingLarge = -24,
        TrackingSmall = -4,
        TabularNumerals = true,
        InnerSurface = InnerSurface.Vibrant,
        RowDividers = Sdk.Kit.RowDivider.Inset,
    };

    /// <summary>[S-2122] Karpo's Harbor and Blueprint face, an installed family of Windows'.</summary>
    public const string HarborFont = "Bahnschrift"; // not-ui: a font family's name

    /// <summary>The preset with this id. Every id has one; an undefined value is Default.</summary>
    public static DesignPreset Get(DesignPresetId id)
    {
        foreach (var preset in All)
        {
            if (preset.Id == id)
            {
                return preset;
            }
        }

        return All[0];
    }

    /// <summary>The sixteen owned fields of a design (its fill choices as they are, not copied: read them only).</summary>
    public static DesignPresetValues ValuesOf(DesignState design)
    {
        ArgumentNullException.ThrowIfNull(design);

        return new(
            design.Theme,
            design.PrimaryColor,
            design.FontFamily,
            design.Background ?? new FillChoice(),
            design.BorderStyle,
            design.BorderWidthPx,
            design.BorderColor,
            design.BorderOpacity,
            design.CornerRadiusPx,
            design.BlurBehind,
            design.ItemBackground ?? new FillChoice(),
            design.ItemTextColor ?? string.Empty,
            design.Material,
            design.Tokens ?? new LookTokens());
    }

    /// <summary>
    /// [S-832] Write every field the preset owns into <paramref name="design"/>, in one call (one
    /// mutation, one save, one apply). Fields it does not own are left exactly as they were.
    /// </summary>
    public static void Apply(DesignState design, DesignPresetId id)
    {
        ArgumentNullException.ThrowIfNull(design);
        var v = Get(id).Values;

        design.Theme = v.Theme;
        design.PrimaryColor = v.PrimaryColor;
        design.FontFamily = v.FontFamily;
        design.Background = v.Background.Clone();
        design.BorderStyle = v.BorderStyle;
        design.BorderWidthPx = v.BorderWidthPx;
        design.BorderColor = v.BorderColor;
        design.BorderOpacity = v.BorderOpacity;
        design.CornerRadiusPx = v.CornerRadiusPx;
        design.BlurBehind = v.BlurBehind;
        design.ItemBackground = v.Items?.Clone() ?? new FillChoice();
        design.ItemTextColor = v.ItemTextColor;
        design.Material = v.Material;
        design.Tokens = v.Tokens?.Clone() ?? new LookTokens();
    }

    /// <summary>
    /// [S-833] The preset this design is, or null for "Custom". Only the owned fields are compared,
    /// so changing the text size or the widget scale after picking a preset still reads as that
    /// preset. Colours compare case-insensitively (the picker stores upper case, a file may not),
    /// numbers to within a rounding error a slider can leave.
    /// </summary>
    public static DesignPresetId? Match(DesignState design)
    {
        ArgumentNullException.ThrowIfNull(design);
        var current = ValuesOf(design);

        foreach (var preset in All)
        {
            if (Same(current, preset.Values))
            {
                return preset.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-6483] Whether picking <paramref name="target"/> would throw away a look the user made: the
    /// design matches no preset (it reads as Custom), so its owned fields exist nowhere else and a
    /// preset overwrites them for good. A design that is a preset loses nothing — that preset can be
    /// picked again — and fields no preset owns (text size, widget scale, motion) never count.
    /// </summary>
    public static bool ReplacesCustomLook(DesignState design, DesignPresetId target)
    {
        ArgumentNullException.ThrowIfNull(design);
        return Match(design) is null && !Same(ValuesOf(design), Get(target).Values);
    }

    private static bool Same(DesignPresetValues a, DesignPresetValues b)
        => a.Theme == b.Theme
            && SameText(a.PrimaryColor, b.PrimaryColor)
            && SameText(a.FontFamily, b.FontFamily)
            && a.Background.SameAs(b.Background)
            && a.BorderStyle == b.BorderStyle
            && Near(a.BorderWidthPx, b.BorderWidthPx)
            && SameText(a.BorderColor, b.BorderColor)
            && Near(a.BorderOpacity, b.BorderOpacity)
            && Near(a.CornerRadiusPx, b.CornerRadiusPx)
            && a.BlurBehind == b.BlurBehind
            && (a.Items ?? new FillChoice()).SameAs(b.Items ?? new FillChoice())
            && SameText(a.ItemTextColor, b.ItemTextColor)
            && a.Material == b.Material
            && (a.Tokens ?? new LookTokens()).SameAs(b.Tokens ?? new LookTokens());

    private static bool SameText(string? a, string? b)
        => string.Equals(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.001;
}
