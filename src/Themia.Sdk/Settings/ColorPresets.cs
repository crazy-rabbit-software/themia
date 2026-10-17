using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>A preset in the colour picker: the stored <c>#RRGGBB</c> and the name a screen reader says.</summary>
public readonly record struct ColorPreset(string Hex, Str NameId)
{
    /// <summary>The colour's name in the current language (the palette is static, so it is read when shown).</summary>
    public string Name => NameId.Text();
}

/// <summary>
/// The swatches the shared colour picker offers before "Custom colour". [S-1549]
/// <para>
/// Colours are picked, never typed: a hex box is a field most people cannot fill in and the rest
/// get wrong. The default set is Windows' own accent palette — the 48 colours, in the order and
/// with the names Settings › Personalization › Colours shows them — so the picker reads as the
/// one users already know.
/// </para>
/// </summary>
public static class ColorPresets
{
    /// <summary>Swatches per row, as Windows lays the accent palette out.</summary>
    public const int Columns = 8;

    /// <summary>Windows' accent palette, row by row.</summary>
    public static IReadOnlyList<ColorPreset> Windows { get; } =
    [
        new("#FFB900", Str.Shell_Color_YellowGold), new("#FF8C00", Str.Shell_Color_Gold), new("#F7630C", Str.Shell_Color_OrangeBright), new("#CA5010", Str.Shell_Color_OrangeDark),
        new("#DA3B01", Str.Shell_Color_Rust), new("#EF6950", Str.Shell_Color_PaleRust), new("#D13438", Str.Shell_Color_BrickRed), new("#FF4343", Str.Shell_Color_ModRed),

        new("#E74856", Str.Shell_Color_PaleRed), new("#E81123", Str.Shell_Color_Red), new("#EA005E", Str.Shell_Color_RoseBright), new("#C30052", Str.Shell_Color_Rose),
        new("#E3008C", Str.Shell_Color_PlumLight), new("#BF0077", Str.Shell_Color_Plum), new("#C239B3", Str.Shell_Color_OrchidLight), new("#9A0089", Str.Shell_Color_Orchid),

        new("#0078D4", Str.Shell_Color_DefaultBlue), new("#0063B1", Str.Shell_Color_NavyBlue), new("#8E8CD8", Str.Shell_Color_PurpleShadow), new("#6B69D6", Str.Shell_Color_PurpleShadowDark),
        new("#8764B8", Str.Shell_Color_IrisPastel), new("#744DA9", Str.Shell_Color_IrisSpring), new("#B146C2", Str.Shell_Color_VioletRedLight), new("#881798", Str.Shell_Color_VioletRed),

        new("#0099BC", Str.Shell_Color_CoolBlueBright), new("#2D7D9A", Str.Shell_Color_CoolBlue), new("#00B7C3", Str.Shell_Color_Seafoam), new("#038387", Str.Shell_Color_SeafoamTeal),
        new("#00B294", Str.Shell_Color_MintLight), new("#018574", Str.Shell_Color_MintDark), new("#00CC6A", Str.Shell_Color_TurfGreen), new("#10893E", Str.Shell_Color_SportGreen),

        new("#7A7574", Str.Shell_Color_Gray), new("#5D5A58", Str.Shell_Color_GrayBrown), new("#68768A", Str.Shell_Color_SteelBlue), new("#515C6B", Str.Shell_Color_MetalBlue),
        new("#567C73", Str.Shell_Color_PaleMoss), new("#486860", Str.Shell_Color_Moss), new("#498205", Str.Shell_Color_MeadowGreen), new("#107C10", Str.Shell_Color_Green),

        new("#767676", Str.Shell_Color_Overcast), new("#4C4A48", Str.Shell_Color_Storm), new("#69797E", Str.Shell_Color_BlueGray), new("#4A5459", Str.Shell_Color_GrayDark),
        new("#647C64", Str.Shell_Color_LiddyGreen), new("#525E54", Str.Shell_Color_Sage), new("#847545", Str.Shell_Color_CamouflageDesert), new("#7E735F", Str.Shell_Color_Camouflage),
    ];

    /// <summary>
    /// Surfaces: the accent palette is all saturated colour, and a card fill or a border usually
    /// wants black, white or a grey in between. One row, offered first where a surface is picked.
    /// </summary>
    public static IReadOnlyList<ColorPreset> Neutrals { get; } =
    [
        new("#000000", Str.Shell_Color_Black), new("#191919", Str.Shell_Color_Charcoal), new("#202020", Str.Shell_Color_DarkGray), new("#2C2C2C", Str.Shell_Color_Graphite),
        new("#454545", Str.Shell_Color_Slate), new("#E5E5E5", Str.Shell_Color_LightGray), new("#F3F3F3", Str.Shell_Color_Mist), new("#FFFFFF", Str.Shell_Color_White),
    ];

    /// <summary>The surface picker's swatches: the neutrals, then the accent palette.</summary>
    public static IReadOnlyList<ColorPreset> Surfaces { get; } = [.. Neutrals, .. Windows];

    /// <summary>
    /// The preset a stored value is, or null when it is a custom colour (or empty). Compared on the
    /// normalised value, so <c>#ffb900</c> and <c>#FFB900</c> are the same swatch.
    /// </summary>
    public static ColorPreset? Find(IReadOnlyList<ColorPreset> presets, string? value)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var normalised = Widgets.HexColor.Normalise(value);
        if (normalised.Length == 0)
        {
            return null;
        }

        foreach (var preset in presets)
        {
            if (string.Equals(Widgets.HexColor.Normalise(preset.Hex), normalised, StringComparison.Ordinal))
            {
                return preset;
            }
        }

        return null;
    }
}
