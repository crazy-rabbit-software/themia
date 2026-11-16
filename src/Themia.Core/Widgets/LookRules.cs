using Themia.Core.Desktop;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-2131] What the frost does to the display's picture: the box blur's radius and the colour treatment after it.</summary>
/// <param name="BoxRadius">The box blur's radius at the picture's scale (three passes); 0 blurs nothing.</param>
/// <param name="Saturation">1 the picture's own, 0 grey, 2 twice as vivid.</param>
/// <param name="Brightness">Toward white above 0, toward black below, as a fraction of the way.</param>
public readonly record struct FrostLook(int BoxRadius, double Saturation, double Brightness)
{
    /// <summary>Whether the colour treatment changes any pixel.</summary>
    public bool Treats => Math.Abs(Saturation - 1) > 0.001 || Math.Abs(Brightness) > 0.001;
}

/// <summary>
/// [S-2130]..[S-2140] The pure half of the look tokens (<see cref="LookTokens"/>): the frost, the text levels, the
/// accent's share in a card and the rendering mode's tones, the items' vibrant fills and the grain. The App half only
/// turns these answers into brushes and pixels.
/// </summary>
public static class LookRules
{
    // ---- [S-2131] the frost ----

    /// <summary>
    /// [S-2131] The frost for a design's tokens: the box radius whose three passes at the picture's scale
    /// (<see cref="WallpaperLayout.Downscale"/>) come nearest a Gaussian of <see cref="LookTokens.FrostBlur"/> display
    /// pixels (a box of radius r three times is σ² = (2r + 1)² − 1) / 4; 40 is radius 3, the frost Themia always had),
    /// and its saturation and brightness held to their ranges.
    /// </summary>
    public static FrostLook Frost(LookTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var sigma = LookTokens.Number("frostBlur")!.Clamp(tokens.FrostBlur) / WallpaperLayout.Downscale;
        var radius = (int)Math.Round((Math.Sqrt((4 * sigma * sigma) + 1) - 1) / 2, MidpointRounding.AwayFromZero);
        return new FrostLook(
            Math.Max(0, radius),
            LookTokens.Number("frostSaturation")!.Clamp(tokens.FrostSaturation),
            LookTokens.Number("frostBrightness")!.Clamp(tokens.FrostBrightness));
    }

    /// <summary>
    /// [S-2131] Treat a blurred BGRA picture in place: each pixel's colour moved from its grey (Rec. 709 luma) by the
    /// saturation, then toward white or black by the brightness. Alpha is kept. Nothing changes at saturation 1 and
    /// brightness 0.
    /// </summary>
    public static void Treat(byte[] bgra, FrostLook frost)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (!frost.Treats)
        {
            return;
        }

        var (s, b) = (frost.Saturation, frost.Brightness);
        for (var at = 0; at + 3 < bgra.Length; at += 4)
        {
            double blue = bgra[at], green = bgra[at + 1], red = bgra[at + 2];
            var luma = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            bgra[at] = Shade(luma + ((blue - luma) * s), b);
            bgra[at + 1] = Shade(luma + ((green - luma) * s), b);
            bgra[at + 2] = Shade(luma + ((red - luma) * s), b);
        }

        static byte Shade(double c, double b)
        {
            c = b >= 0 ? c + ((255 - c) * b) : c * (1 + b);
            return (byte)Math.Clamp(Math.Round(c), 0, 255);
        }
    }

    // ---- [S-2134] the text levels ----

    /// <summary>The theme's ink: what every text level is a share of (white in dark, black in light).</summary>
    public static RgbColor Ink(bool dark) => dark ? new RgbColor(0xFF, 0xFF, 0xFF) : new RgbColor(0x00, 0x00, 0x00);

    /// <summary>
    /// [S-2134] Windows' own opacity of each text level's ink (WinUI <c>TextFillColor*</c>: dark #FF, #C5, #87, #5D
    /// white; light #E4, #9E, #72, #5C black).
    /// </summary>
    public static double ThemeLabel(ItemTextTier tier, bool dark) => (dark
        ? tier switch { ItemTextTier.Secondary => 0xC5, ItemTextTier.Tertiary => 0x87, ItemTextTier.Disabled => 0x5D, _ => 0xFF }
        : tier switch { ItemTextTier.Secondary => 0x9E, ItemTextTier.Tertiary => 0x72, ItemTextTier.Disabled => 0x5C, _ => 0xE4 }) / 255.0;

    /// <summary>
    /// [S-2134] The opacity of the ink a text level draws at: Windows' own (<see cref="ThemeLabel"/>) moved toward the
    /// level's token (primary, secondary, tertiary, quaternary for the greyed level) by <see cref="LookTokens.Vibrancy"/>.
    /// At no vibrancy it is exactly Windows'.
    /// </summary>
    public static double Label(ItemTextTier tier, bool dark, LookTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var theme = ThemeLabel(tier, dark);
        var v = LookTokens.Number("vibrancy")!.Clamp(tokens.Vibrancy);
        if (v <= 0)
        {
            return theme;
        }

        var target = tier switch
        {
            ItemTextTier.Secondary => LookTokens.Number("labelSecondary")!.Clamp(tokens.LabelSecondary),
            ItemTextTier.Tertiary => LookTokens.Number("labelTertiary")!.Clamp(tokens.LabelTertiary),
            ItemTextTier.Disabled => LookTokens.Number("labelQuaternary")!.Clamp(tokens.LabelQuaternary),
            _ => LookTokens.Number("labelPrimary")!.Clamp(tokens.LabelPrimary),
        };
        return theme + ((target - theme) * v);
    }

    // ---- [S-2133] the accent in the card, the rendering mode ----

    /// <summary>[S-2133] How far a card's own fill takes the accent: the token, at least <see cref="TintedGlass"/> in the tinted mode.</summary>
    public static double AccentShare(LookTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var own = LookTokens.Number("accentTint")!.Clamp(tokens.AccentTint);
        return tokens.Rendering == RenderingMode.Tinted ? Math.Max(own, TintedGlass) : own;
    }

    /// <summary>[S-2133] The accent's least share of the card in the tinted mode, as macOS's tinted widgets take it.</summary>
    public const double TintedGlass = 0.3;

    /// <summary>[S-2133] <paramref name="card"/> mixed toward <paramref name="accent"/> by <paramref name="share"/> (0 the card itself).</summary>
    public static RgbColor AccentTinted(RgbColor card, RgbColor accent, double share)
        => share <= 0 ? card : DesignLook.Over(FillChoice.Alpha(share), accent, card);

    /// <summary>
    /// [S-2133] A colour as the rendering mode draws it: as it is in full colour; its grey (the same relative luminance)
    /// in monochrome; the accent's hue and saturation at its own lightness when tinted.
    /// </summary>
    public static RgbColor Tone(RgbColor colour, RenderingMode mode, RgbColor accent)
    {
        switch (mode)
        {
            case RenderingMode.Monochrome:
                var grey = Grey(colour);
                return new RgbColor(grey, grey, grey);
            case RenderingMode.Tinted:
                var own = ColorFormats.ToHsl(colour);
                var hue = ColorFormats.ToHsl(accent);
                return ColorFormats.FromHsl(own with { H = hue.H, S = hue.S });
            default:
                return colour;
        }
    }

    /// <summary>The sRGB grey of a colour's relative luminance.</summary>
    public static byte Grey(RgbColor colour)
    {
        var l = LiquidGlass.Luminance(colour);
        var srgb = l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
        return (byte)Math.Clamp(Math.Round(srgb * 255), 0, 255);
    }

    // ---- [S-2138] the items' vibrant fills ----

    /// <summary>
    /// [S-2138] The opacity of the ink a vibrant item is filled with (<see cref="InnerSurface.Vibrant"/>): a row clear at
    /// rest and lit by the hover's and the selection's; a tile or a chip on <see cref="LookTokens.InnerFill"/> with those
    /// added; a track on its own; a disabled item at half its rest.
    /// </summary>
    public static double Inner(ItemRole role, ItemState state, LookTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var fill = LookTokens.Number("innerFill")!.Clamp(tokens.InnerFill);
        var hover = LookTokens.Number("innerHover")!.Clamp(tokens.InnerHover);
        var selected = LookTokens.Number("innerSelected")!.Clamp(tokens.InnerSelected);
        if (role == ItemRole.Track)
        {
            return LookTokens.Number("trackOpacity")!.Clamp(tokens.TrackOpacity) * (state == ItemState.Disabled ? ItemLook.DisabledFade : 1);
        }

        var rest = role == ItemRole.Row ? 0 : fill;
        var lit = state switch
        {
            ItemState.Hover => hover,
            ItemState.Pressed or ItemState.Selected => selected,
            ItemState.SelectedHover => selected + hover,
            _ => 0,
        };

        return state == ItemState.Disabled ? rest * ItemLook.DisabledFade : Math.Min(1, rest + lit);
    }

    // ---- [S-2140] the grain ----

    /// <summary>[S-2140] The grain picture's side, in pixels: drawn unscaled from the card's corner, enough for any card.</summary>
    public const int GrainSize = 512;

    /// <summary>
    /// [S-2140] The grain as a BGRA picture, <paramref name="size"/> square, premultiplied: each pixel white or black at
    /// an alpha that a fixed hash of its place gives (no two runs differ, nothing random), so the grain is fine, even
    /// and the same in every render. The card draws it at <see cref="LookTokens.Grain"/>.
    /// </summary>
    public static byte[] Grain(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "a grain of at least one pixel");
        }

        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var h = (uint)((x * 374761393) + (y * 668265263));
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                var alpha = (byte)(h & 0xFF);
                var white = (h & 0x100) != 0;
                var at = ((y * size) + x) * 4;
                var c = white ? alpha : (byte)0;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (c, c, c, alpha);
            }
        }

        return pixels;
    }
}
