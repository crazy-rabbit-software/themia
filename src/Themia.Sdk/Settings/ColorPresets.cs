using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>
/// A preset in the colour picker: the stored <c>#RRGGBB</c> and the name a screen reader says, from
/// whichever table holds it (the host's palette, a widget module's own inks).
/// </summary>
public readonly record struct ColorPreset(string Hex, Sdk.LocalText Label)
{
    /// <summary>The colour's name in the current language (the palette is static, so it is read when shown).</summary>
    public string Name => Label.Text();
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
        new("#FFB900", Str.Shell_Color_YellowGold.Local()), new("#FF8C00", Str.Shell_Color_Gold.Local()), new("#F7630C", Str.Shell_Color_OrangeBright.Local()), new("#CA5010", Str.Shell_Color_OrangeDark.Local()),
        new("#DA3B01", Str.Shell_Color_Rust.Local()), new("#EF6950", Str.Shell_Color_PaleRust.Local()), new("#D13438", Str.Shell_Color_BrickRed.Local()), new("#FF4343", Str.Shell_Color_ModRed.Local()),

        new("#E74856", Str.Shell_Color_PaleRed.Local()), new("#E81123", Str.Shell_Color_Red.Local()), new("#EA005E", Str.Shell_Color_RoseBright.Local()), new("#C30052", Str.Shell_Color_Rose.Local()),
        new("#E3008C", Str.Shell_Color_PlumLight.Local()), new("#BF0077", Str.Shell_Color_Plum.Local()), new("#C239B3", Str.Shell_Color_OrchidLight.Local()), new("#9A0089", Str.Shell_Color_Orchid.Local()),

        new("#0078D4", Str.Shell_Color_DefaultBlue.Local()), new("#0063B1", Str.Shell_Color_NavyBlue.Local()), new("#8E8CD8", Str.Shell_Color_PurpleShadow.Local()), new("#6B69D6", Str.Shell_Color_PurpleShadowDark.Local()),
        new("#8764B8", Str.Shell_Color_IrisPastel.Local()), new("#744DA9", Str.Shell_Color_IrisSpring.Local()), new("#B146C2", Str.Shell_Color_VioletRedLight.Local()), new("#881798", Str.Shell_Color_VioletRed.Local()),

        new("#0099BC", Str.Shell_Color_CoolBlueBright.Local()), new("#2D7D9A", Str.Shell_Color_CoolBlue.Local()), new("#00B7C3", Str.Shell_Color_Seafoam.Local()), new("#038387", Str.Shell_Color_SeafoamTeal.Local()),
        new("#00B294", Str.Shell_Color_MintLight.Local()), new("#018574", Str.Shell_Color_MintDark.Local()), new("#00CC6A", Str.Shell_Color_TurfGreen.Local()), new("#10893E", Str.Shell_Color_SportGreen.Local()),

        new("#7A7574", Str.Shell_Color_Gray.Local()), new("#5D5A58", Str.Shell_Color_GrayBrown.Local()), new("#68768A", Str.Shell_Color_SteelBlue.Local()), new("#515C6B", Str.Shell_Color_MetalBlue.Local()),
        new("#567C73", Str.Shell_Color_PaleMoss.Local()), new("#486860", Str.Shell_Color_Moss.Local()), new("#498205", Str.Shell_Color_MeadowGreen.Local()), new("#107C10", Str.Shell_Color_Green.Local()),

        new("#767676", Str.Shell_Color_Overcast.Local()), new("#4C4A48", Str.Shell_Color_Storm.Local()), new("#69797E", Str.Shell_Color_BlueGray.Local()), new("#4A5459", Str.Shell_Color_GrayDark.Local()),
        new("#647C64", Str.Shell_Color_LiddyGreen.Local()), new("#525E54", Str.Shell_Color_Sage.Local()), new("#847545", Str.Shell_Color_CamouflageDesert.Local()), new("#7E735F", Str.Shell_Color_Camouflage.Local()),
    ];

    /// <summary>
    /// Surfaces: the accent palette is all saturated colour, and a card fill or a border usually
    /// wants black, white or a grey in between. One row, offered first where a surface is picked.
    /// </summary>
    public static IReadOnlyList<ColorPreset> Neutrals { get; } =
    [
        new("#000000", Str.Shell_Color_Black.Local()), new("#191919", Str.Shell_Color_Charcoal.Local()), new("#202020", Str.Shell_Color_DarkGray.Local()), new("#2C2C2C", Str.Shell_Color_Graphite.Local()),
        new("#454545", Str.Shell_Color_Slate.Local()), new("#E5E5E5", Str.Shell_Color_LightGray.Local()), new("#F3F3F3", Str.Shell_Color_Mist.Local()), new("#FFFFFF", Str.Shell_Color_White.Local()),
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
