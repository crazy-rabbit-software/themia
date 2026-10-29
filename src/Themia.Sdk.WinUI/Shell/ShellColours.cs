using Windows.UI;

namespace Themia.App.Shell;

/// <summary>
/// [S-6747] The few colours the app draws that are no theme role and no data: a drop shadow's black,
/// the clear a compositor's backdrop is set to, and the motion effects' own palettes (art, not chrome:
/// the hacker tiles' phosphor, the warp's black space and its stars). Beside <c>HexBrush</c> (a data
/// colour, the user's design, a role's wash) the one place a colour is made from numbers; the source
/// guard allows it here and nowhere else.
/// </summary>
public static class ShellColours
{
    /// <summary>A drop shadow's colour (Windows casts its flyouts' shadows in black at an opacity).</summary>
    public static Color Shadow { get; } = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);

    /// <summary>Nothing at all: what a see-through window's system backdrop is set to.</summary>
    public static Color None { get; } = Color.FromArgb(0x00, 0x00, 0x00, 0x00);

    /// <summary>Windows' default accent blue: where the custom colour dialog starts when the choice has no colour and no default of its own.</summary>
    public static Color WindowsAccent { get; } = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);

    /// <summary>[S-1760] The warp's pure black space: the style's identity, not a theme colour.</summary>
    public static Color Space { get; } = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);

    /// <summary>[S-1760] A star of the warp, in the colour its rule gives it.</summary>
    public static Color Star(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);

    /// <summary>The hacker tiles' green phosphor, its dim trace and the screen behind it, as in the original.</summary>
    public static Color Phosphor { get; } = Color.FromArgb(0xFF, 0x39, 0xFF, 0x7A);

    public static Color PhosphorDim { get; } = Color.FromArgb(0xFF, 0x1C, 0x8A, 0x45);

    public static Color PhosphorScreen { get; } = Color.FromArgb(0xFF, 0x03, 0x0B, 0x06);
}
