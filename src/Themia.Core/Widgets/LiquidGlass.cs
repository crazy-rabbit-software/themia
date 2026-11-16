namespace Themia.Core.Widgets;

/// <summary>[S-2123] One stop of a glass gradient: where along it (0 to 1) and the light's alpha there.</summary>
public readonly record struct GlassStop(double Offset, double Alpha);

/// <summary>
/// [S-2123] Liquid glass in one theme, every layer's value, after Apple's Liquid Glass as iOS 26 and macOS 26 ship it
/// (the regular variant, the one for widgets and controls that hold text). The card draws them bottom up:
/// <list type="number">
/// <item>the <b>lift</b>: a large, very faint shadow, black at <see cref="ShadowOpacity"/>, blurred over <see cref="ShadowBlur"/> DIPs, <see cref="ShadowOffsetY"/> down;</item>
/// <item>the <b>frost</b>: the display's blurred wallpaper under the card ([S-2110], always on for glass);</item>
/// <item>the <b>tint</b>: <see cref="Tint"/>, as clear as <see cref="TintMin"/>, raised only as far as the text needs over what lies behind (<see cref="LiquidGlass.TintOpacity"/>), never past <see cref="TintMax"/>;</item>
/// <item>the <b>glow</b>: the highlight's light falling in at the top edge, <see cref="GlowDepth"/> DIPs deep (<see cref="Glow"/>): no sheen over the rest of the surface;</item>
/// <item>the <b>lens</b>: the refraction band inside the rim, <see cref="LensBands"/> bands of <see cref="LensBandWidth"/> DIPs from the rim in, each at its weight times the <see cref="Lens"/> profile down the glass: the background bent and brightened along the edge, strongest where the light falls (top and foot);</item>
/// <item>the <b>rim</b>: the crisp specular edge, a <see cref="RimWidth"/> stroke whose light runs down the glass (<see cref="Rim"/>): bright along the top, fading down the sides, catching again along the foot;</item>
/// <item>the <b>dividers</b>: inside a group that is one glass shape, a hairline (<see cref="Divider"/> at <see cref="DividerAlpha"/>) between members, <see cref="DividerInset"/> short of the glass's outer edge, as iOS's grouped controls.</item>
/// </list>
/// Every stop's offset is down the glass's whole height (a group's: <see cref="CardJoins.Bodies"/>); the corners are <see cref="RadiusPx"/>.
/// </summary>
public sealed record GlassLook(
    RgbColor Tint,
    double TintMin,
    double TintMax,
    IReadOnlyList<GlassStop> Glow,
    double GlowDepth,
    IReadOnlyList<GlassStop> Lens,
    IReadOnlyList<double> LensBands,
    double LensBandWidth,
    IReadOnlyList<GlassStop> Rim,
    double RimWidth,
    RgbColor Divider,
    double DividerAlpha,
    double DividerInset,
    double ShadowOpacity,
    double ShadowBlur,
    double ShadowOffsetY,
    double RadiusPx);

/// <summary>
/// [S-2123] The Liquid glass style, after Apple's as it ships in iOS 26 and macOS 26 Tahoe (HIG "Materials › Liquid
/// Glass", WWDC25 "Meet Liquid Glass" and "Get to know the new design system"): a clear material that lets what is
/// behind it through with little tint, lit by a thin specular edge (bright where the edge faces the light, the top and
/// the opposite foot, fading down the sides), a lensing band just inside the edge where the background is bent and
/// brightened, a large and very faint shadow, continuous corners, and no sheen washed over the surface; adaptive, it
/// clouds over only where what lies behind would drown the text. As WinUI and Composition can draw it without effects
/// that need reflection: what is real (the blurred wallpaper behind, the clear tint, the lit rim, the lift) and what is
/// approximated: the lensing is stepped bands of light along the edge, not a sampled bend of what lies behind; the
/// highlight is fixed to a light from above, not the device's tilt or the pointer; the blur is the display's one frost
/// ([S-2112]); continuous (squircle) corners are circular arcs; the light/dark adaptation is the tint's amount over
/// the theme's own glass (the text keeps the theme's colour), not a switch of the glass's appearance. A group of cards
/// is one glass shape: one rim round it, one glow and lens down its height, one tint over its area, hairline
/// dividers between its members (<see cref="CardJoins.Bodies"/>).
/// </summary>
public static class LiquidGlass
{
    /// <summary>
    /// [S-2124] Whether a card wears the glass: only one that has a card. A card whose own fill is Transparent (a
    /// desktop icon, a widget set to no background) is drawn bare, no glow, lens, rim or lift, as in any other style.
    /// </summary>
    public static bool Wears(CardFill own) => own.Kind != Themia.Core.Model.FillKind.Transparent;

    /// <summary>[S-2123] The light theme's glass: a white tint barely there, a crisp bright rim, a faint lift.</summary>
    public static GlassLook Light { get; } = new(
        Tint: new RgbColor(0xFF, 0xFF, 0xFF),
        TintMin: 0.16,
        TintMax: 0.66,
        Glow: [new(0, 0.16), new(0.35, 0.05), new(1, 0)],
        GlowDepth: 22,
        Lens: [new(0, 0.80), new(0.12, 0.40), new(0.5, 0.16), new(0.88, 0.34), new(1, 0.66)],
        LensBands: [0.34, 0.18, 0.08],
        LensBandWidth: 2,
        Rim: [new(0, 0.92), new(0.06, 0.58), new(0.3, 0.16), new(0.55, 0.08), new(0.9, 0.24), new(1, 0.62)],
        RimWidth: 1,
        Divider: new RgbColor(0x00, 0x00, 0x00),
        DividerAlpha: 0.10,
        DividerInset: 14,
        ShadowOpacity: 0.10,
        ShadowBlur: 36,
        ShadowOffsetY: 10,
        RadiusPx: 24);

    /// <summary>[S-2123] The dark theme's glass: a graphite tint, the light dimmer, the lift a little deeper.</summary>
    public static GlassLook Dark { get; } = new(
        Tint: new RgbColor(0x1C, 0x1C, 0x1E),
        TintMin: 0.24,
        TintMax: 0.74,
        Glow: [new(0, 0.09), new(0.35, 0.03), new(1, 0)],
        GlowDepth: 22,
        Lens: [new(0, 0.60), new(0.12, 0.26), new(0.5, 0.10), new(0.88, 0.22), new(1, 0.46)],
        LensBands: [0.34, 0.18, 0.08],
        LensBandWidth: 2,
        Rim: [new(0, 0.56), new(0.06, 0.34), new(0.3, 0.10), new(0.55, 0.05), new(0.9, 0.13), new(1, 0.36)],
        RimWidth: 1,
        Divider: new RgbColor(0xFF, 0xFF, 0xFF),
        DividerAlpha: 0.10,
        DividerInset: 14,
        ShadowOpacity: 0.24,
        ShadowBlur: 36,
        ShadowOffsetY: 10,
        RadiusPx: 24);

    /// <summary>[S-2123] How many rings the lift is drawn as.</summary>
    public const int LiftRingCount = 10;

    /// <summary>
    /// [S-2123] The lift as rings round the glass, the outermost first: each <c>Spread</c> DIPs past the glass's edge
    /// (the last <see cref="GlassLook.ShadowBlur"/>), each a black at <c>Alpha</c>. Stacked, they darken a point
    /// <c>d</c> DIPs out by about <see cref="GlassLook.ShadowOpacity"/> × (1 − d / blur)², a soft falloff that is
    /// gone at the blur's end: plain translucent shapes, drawn the same live and in a picture of the desktop (a
    /// composition shadow came out as a hard dark box in the pictures).
    /// </summary>
    public static IReadOnlyList<(double Spread, double Alpha)> LiftRings(GlassLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        double Falloff(double d) => look.ShadowOpacity * Math.Pow(Math.Max(0, 1 - (d / look.ShadowBlur)), 2);
        var rings = new List<(double Spread, double Alpha)>(LiftRingCount);
        for (var k = LiftRingCount; k >= 1; k--)
        {
            var (inner, outer) = (look.ShadowBlur * (k - 1) / LiftRingCount, look.ShadowBlur * k / LiftRingCount);
            rings.Add((outer, Falloff(inner) - Falloff(outer)));
        }

        return rings;
    }

    /// <summary>
    /// [S-2123] One lens band's stops: the <see cref="GlassLook.Lens"/> profile at that band's weight (band 0 the
    /// outermost, against the rim), so the band of light fades in from the edge.
    /// </summary>
    public static IReadOnlyList<GlassStop> LensStops(GlassLook look, int band)
    {
        ArgumentNullException.ThrowIfNull(look);
        var weight = band >= 0 && band < look.LensBands.Count ? look.LensBands[band] : 0;
        return [.. look.Lens.Select(s => s with { Alpha = s.Alpha * weight })];
    }

    /// <summary>[S-2123] The lift's colour, at its rings' alphas: black.</summary>
    public static RgbColor Shade { get; } = new(0x00, 0x00, 0x00);

    /// <summary>[S-2123] The light the rim, the glow and the lens are drawn in, at their stops' alphas: white.</summary>
    public static RgbColor Highlight { get; } = new(0xFF, 0xFF, 0xFF);

    /// <summary>[S-2123] How many cells of a backdrop's light <see cref="Grid"/> keeps across and down.</summary>
    public const int GridColumns = 32, GridRows = 18;

    /// <summary>The glass of a theme, as its base values hold it (the tokens' defaults).</summary>
    public static GlassLook For(bool dark) => dark ? Dark : Light;

    /// <summary>
    /// [S-2132] [S-2133] The glass of a theme as a design's tokens shape it: the theme's tint held between its own
    /// clearest and most clouded (<see cref="Model.LookTokens.GlassTintLightMin"/> and the rest), mixed toward
    /// <paramref name="accent"/> by the card's accent share (<see cref="LookRules.AccentShare"/>); the glow, the lens
    /// and the rim at their strengths (an alpha never past 1); the rim's width; the lift's reach, drop and darkness;
    /// a divider's opacity. The stops' places and the corners are the theme's.
    /// </summary>
    public static GlassLook For(bool dark, Model.LookTokens tokens, RgbColor? accent = null)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var look = For(dark);
        double N(string key, double value) => Model.LookTokens.Number(key)!.Clamp(value);
        static IReadOnlyList<GlassStop> Times(IReadOnlyList<GlassStop> stops, double k) => [.. stops.Select(s => s with { Alpha = Math.Min(1, s.Alpha * k) })];

        var (min, max) = dark
            ? (N("glassTintDarkMin", tokens.GlassTintDarkMin), N("glassTintDarkMax", tokens.GlassTintDarkMax))
            : (N("glassTintLightMin", tokens.GlassTintLightMin), N("glassTintLightMax", tokens.GlassTintLightMax));
        var lens = N("lensStrength", tokens.LensStrength);
        return look with
        {
            Tint = accent is { } a ? LookRules.AccentTinted(look.Tint, a, LookRules.AccentShare(tokens)) : look.Tint,
            TintMin = Math.Min(min, max),
            TintMax = Math.Max(min, max),
            Glow = Times(look.Glow, N("glowStrength", tokens.GlowStrength)),
            LensBands = [.. look.LensBands.Select(b => Math.Min(1, b * lens))],
            Rim = Times(look.Rim, N("rimStrength", tokens.RimStrength)),
            RimWidth = N("rimWidth", tokens.RimWidth),
            DividerAlpha = N("dividerOpacity", tokens.DividerOpacity),
            ShadowOpacity = Math.Min(1, look.ShadowOpacity * N("shadowStrength", tokens.ShadowStrength)),
            ShadowBlur = N("shadowRadius", tokens.ShadowRadius),
            ShadowOffsetY = N("shadowOffset", tokens.ShadowOffset),
        };
    }

    /// <summary>[S-2123] The theme's text the tint keeps readable: Windows' primary text (near-black in light, white in dark).</summary>
    public static RgbColor Text(bool dark) => dark ? new RgbColor(0xFF, 0xFF, 0xFF) : new RgbColor(0x1A, 0x1A, 0x1A);

    /// <summary>[S-2123] WCAG's ratio for body text: what the tint holds the primary text to.</summary>
    public const double TextContrast = 4.5;

    /// <summary>[S-2134] WCAG's ratio for large text and parts: what the tint holds the secondary text to.</summary>
    public const double SecondaryContrast = 3;

    /// <summary>[S-2123] How opaque the theme's base glass is under Windows' own text levels: <see cref="TintOpacity(GlassLook, bool, double?, double, double)"/>.</summary>
    public static double TintOpacity(bool dark, double? behind)
        => TintOpacity(For(dark), dark, behind, LookRules.ThemeLabel(ItemTextTier.Primary, dark), LookRules.ThemeLabel(ItemTextTier.Secondary, dark));

    /// <summary>
    /// [S-2123] [S-2134] How opaque <paramref name="look"/>'s tint is over a backdrop of relative luminance
    /// <paramref name="behind"/> (0 black to 1 white; null where none is known, which takes the tint's most): the least,
    /// from <see cref="GlassLook.TintMin"/> up, at which the theme's ink at the primary level's opacity
    /// (<paramref name="primary"/>) reads at <see cref="TextContrast"/> on the glass and at the secondary level's
    /// (<paramref name="secondary"/>) at <see cref="SecondaryContrast"/>: the tint, the backdrop and the text mixed in
    /// sRGB as the compositor mixes them; <see cref="GlassLook.TintMax"/> where even that does not reach it. Over a dark
    /// wallpaper light glass clouds over, over a bright one it stays clear; dark glass the other way. Vibrant text (a
    /// level well below the ink) asks more of the glass, so the guard holds whatever the levels are.
    /// </summary>
    public static double TintOpacity(GlassLook look, bool dark, double? behind, double primary, double secondary)
    {
        ArgumentNullException.ThrowIfNull(look);
        if (behind is not { } luminance || !double.IsFinite(luminance))
        {
            return look.TintMax;
        }

        var back = ToSrgb(Math.Clamp(luminance, 0, 1));
        var tint = ToSrgb(Luminance(look.Tint));
        var ink = ToSrgb(Luminance(LookRules.Ink(dark)));
        bool Reads(double alpha)
        {
            var glass = back + ((tint - back) * alpha);
            return Contrast(ToLinear(glass), ToLinear(glass + ((ink - glass) * primary))) >= TextContrast
                && Contrast(ToLinear(glass), ToLinear(glass + ((ink - glass) * secondary))) >= SecondaryContrast;
        }

        if (Reads(look.TintMin))
        {
            return look.TintMin;
        }

        if (!Reads(look.TintMax))
        {
            return look.TintMax;
        }

        // The contrast grows with the tint between the two: the least that reads, to a hundredth, rounded up (rounded
        // to the nearest it fell short of the ratio by a hair).
        var (low, high) = (look.TintMin, look.TintMax);
        while (high - low > 0.005)
        {
            var mid = (low + high) / 2;
            (low, high) = Reads(mid) ? (low, mid) : (mid, high);
        }

        return Math.Min(look.TintMax, Math.Ceiling((high * 100) - 1e-9) / 100);
    }

    /// <summary>WCAG relative luminance of a colour.</summary>
    public static double Luminance(RgbColor colour)
        => (0.2126 * ToLinear(colour.R / 255.0)) + (0.7152 * ToLinear(colour.G / 255.0)) + (0.0722 * ToLinear(colour.B / 255.0));

    /// <summary>WCAG contrast ratio of two relative luminances.</summary>
    public static double Contrast(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    /// <summary>
    /// [S-2123] A blurred backdrop's light as a grid of <paramref name="columns"/> × <paramref name="rows"/> cells, each
    /// its pixels' mean relative luminance, row after row: what <see cref="Mean"/> reads under a card. The picture is
    /// BGRA, opaque, row after row ([S-2112]); a few pixels per cell are enough on a blurred picture.
    /// </summary>
    public static float[] Grid(byte[] bgra, int width, int height, int columns, int rows)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0 || bgra.Length < width * height * 4)
        {
            throw new ArgumentException("a picture of width × height BGRA pixels and a grid of at least one cell", nameof(bgra));
        }

        const int Samples = 4;
        var grid = new float[columns * rows];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                double sum = 0;
                for (var sy = 0; sy < Samples; sy++)
                {
                    var y = Math.Min(height - 1, (int)(((row + ((sy + 0.5) / Samples)) * height) / rows));
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var x = Math.Min(width - 1, (int)(((column + ((sx + 0.5) / Samples)) * width) / columns));
                        var at = ((y * width) + x) * 4;
                        sum += Luminance(new RgbColor(bgra[at + 2], bgra[at + 1], bgra[at]));
                    }
                }

                grid[(row * columns) + column] = (float)(sum / (Samples * Samples));
            }
        }

        return grid;
    }

    /// <summary>
    /// [S-2123] The mean light of <paramref name="grid"/> under a box given as fractions of the picture (left, top,
    /// right, bottom, held to it), each cell weighed by how much of it the box covers; null for a box outside it.
    /// </summary>
    public static double? Mean(float[] grid, int columns, int rows, double left, double top, double right, double bottom)
    {
        ArgumentNullException.ThrowIfNull(grid);
        (left, right) = (Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
        (top, bottom) = (Math.Clamp(top, 0, 1), Math.Clamp(bottom, 0, 1));
        if (right <= left || bottom <= top || grid.Length < columns * rows)
        {
            return null;
        }

        double sum = 0, weight = 0;
        for (var row = 0; row < rows; row++)
        {
            var h = Math.Min(bottom, (row + 1.0) / rows) - Math.Max(top, (double)row / rows);
            if (h <= 0)
            {
                continue;
            }

            for (var column = 0; column < columns; column++)
            {
                var w = Math.Min(right, (column + 1.0) / columns) - Math.Max(left, (double)column / columns);
                if (w <= 0)
                {
                    continue;
                }

                sum += grid[(row * columns) + column] * w * h;
                weight += w * h;
            }
        }

        return weight > 0 ? sum / weight : null;
    }

    private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double ToSrgb(double l) => l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
}
