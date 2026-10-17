using System.Globalization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// The seven shades Windows derives from one accent colour, darkest first. WinUI's accent brushes
/// are each one of these (<c>AccentFillColorDefault</c> is <see cref="Light2"/> in dark theme and
/// <see cref="Dark1"/> in light, for example), so an accent override has to supply all seven.
/// </summary>
public readonly record struct AccentShades(
    RgbColor Dark3,
    RgbColor Dark2,
    RgbColor Dark1,
    RgbColor Accent,
    RgbColor Light1,
    RgbColor Light2,
    RgbColor Light3)
{
    /// <summary>The shades in the order <c>UIColorType</c> numbers them past Accent: Dark3..Light3.</summary>
    public RgbColor[] ToArray() => [Dark3, Dark2, Dark1, Accent, Light1, Light2, Light3];
}

/// <summary>How a widget's border is drawn, worked out from the design.</summary>
/// <param name="Thickness">Stroke width in DIPs; 0 draws nothing.</param>
/// <param name="Dashes">
/// The dash pattern in multiples of <see cref="Thickness"/> (WinUI's <c>StrokeDashArray</c> unit),
/// or empty for a solid line.
/// </param>
/// <param name="RoundCaps">Dotted lines are zero-length dashes with round caps.</param>
/// <param name="Opacity">0 to 1.</param>
public readonly record struct BorderLook(double Thickness, IReadOnlyList<double> Dashes, bool RoundCaps, double Opacity)
{
    public bool Visible => Thickness > 0 && Opacity > 0;
}

/// <summary>
/// [S-6481] What a border is drawn in: a colour of the user's own, opaque (the border's opacity is
/// the line's, applied once), or else a theme stroke named by its resource key.
/// </summary>
/// <param name="Colour">The user's colour, or null for the theme's.</param>
/// <param name="ThemeKey">The theme brush when <paramref name="Colour"/> is null; otherwise null.</param>
public readonly record struct BorderInk(RgbColor? Colour, string? ThemeKey);

/// <summary>
/// [S-6563] What a card's background paints: the Fluent base and tint (<see cref="FillKind.Default"/>),
/// nothing (<see cref="FillKind.Transparent"/>), or <see cref="Colour"/> exactly
/// (<see cref="FillKind.Colour"/>), at <see cref="Opacity"/>.
/// </summary>
/// <param name="Kind">Which of the three it is.</param>
/// <param name="Colour">The colour for a colour fill; otherwise null.</param>
/// <param name="Opacity">0 to 1: 1 for the default, 0 for transparent, the choice's own for a colour.</param>
public readonly record struct CardFill(FillKind Kind, RgbColor? Colour, double Opacity)
{
    /// <summary>Whether anything is painted at all.</summary>
    public bool Painted => Kind != FillKind.Transparent && Opacity > 0;
}

/// <summary>
/// The pure half of the appearance engine: colour maths, clamps and the rules that turn a
/// <see cref="DesignState"/> into what a card paints. The App half (<c>Shell/DesignApplier</c>)
/// only turns these answers into brushes. [S-820]..[S-829]
/// </summary>
public static class DesignLook
{
    /// <summary>The font-family presets the original offered, beside any installed family.</summary>
    public const string SystemFont = "system";

    public const string SansFont = "sans";

    public const string SerifFont = "serif";

    public const string MonoFont = "mono";

    /// <summary>The presets in the order the picker lists them.</summary>
    public static IReadOnlyList<string> FontPresets { get; } = [SystemFont, SansFont, SerifFont, MonoFont];

    // How far each light shade moves toward white and each dark shade toward black, as a fraction
    // of the room the accent's own lightness leaves. Read off Windows' palette for its default
    // blue (#0078D4), whose shades sit at these lightness steps; Windows also shifts the hue a
    // little, which an HSL walk does not, and nobody comparing a custom accent can tell.
    private static readonly double[] LightSteps = [0.15, 0.40, 0.65];

    private static readonly double[] DarkSteps = [0.85, 0.65, 0.45];

    /// <summary>[S-820] The seven shades for an accent, hue and saturation kept, lightness walked.</summary>
    public static AccentShades Shades(RgbColor accent)
    {
        var hsl = ColorFormats.ToHsl(accent);

        RgbColor Lighter(double step) => ColorFormats.FromHsl(hsl with { L = hsl.L + ((1 - hsl.L) * step) });
        RgbColor Darker(double factor) => ColorFormats.FromHsl(hsl with { L = hsl.L * factor });

        return new AccentShades(
            Darker(DarkSteps[2]),
            Darker(DarkSteps[1]),
            Darker(DarkSteps[0]),
            accent,
            Lighter(LightSteps[0]),
            Lighter(LightSteps[1]),
            Lighter(LightSteps[2]));
    }

    /// <summary>Shade indexes into <see cref="AccentShades.ToArray"/>.</summary>
    private const int Dark3 = 0, Dark2 = 1, Dark1 = 2, Base = 3, Light2 = 5, Light3 = 6;

    /// <summary>
    /// [S-820] Which accent shade a Fluent accent brush is, in each theme, as WinUI's theme resources
    /// define them (dark theme reads lighter shades, light theme darker), or null for a key that is
    /// not an accent brush. The legacy <c>SystemControl…Accent…</c> brushes are the accent itself in
    /// both. An opacity a brush carries (Secondary 0.9, Tertiary 0.8) is the brush's own and kept.
    /// </summary>
    public static int? AccentShade(string key, bool dark) => key switch
    {
        "AccentFillColorDefaultBrush" or "AccentFillColorSecondaryBrush" or "AccentFillColorTertiaryBrush" => dark ? Light2 : Dark1,
        "AccentTextFillColorPrimaryBrush" => dark ? Light3 : Dark2,
        "AccentTextFillColorSecondaryBrush" => dark ? Light3 : Dark3,
        "AccentTextFillColorTertiaryBrush" => dark ? Light2 : Dark1,
        "AccentFillColorSelectedTextBackgroundBrush" => Base,

        // An accent alias with no colour resource of its own (the informational InfoBar, a notice):
        // Light2 in dark theme, the accent itself in light.
        "SystemFillColorAttentionBrush" => dark ? Light2 : Base,
        "SystemControlHighlightAccentBrush" or "SystemControlForegroundAccentBrush" or "SystemControlBackgroundAccentBrush"
            or "SystemControlHighlightAltAccentBrush" or "SystemControlDisabledAccentBrush"
            or "SystemControlHighlightListAccentLowBrush" or "SystemControlHighlightListAccentMediumBrush"
            or "SystemControlHighlightListAccentHighBrush" => Base,
        _ => null,
    };

    /// <summary>Every key <see cref="AccentShade"/> knows, for the engine to recolour.</summary>
    public static IReadOnlyList<string> AccentBrushKeys { get; } =
    [
        "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
        "AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorTertiaryBrush",
        "AccentFillColorSelectedTextBackgroundBrush", "SystemFillColorAttentionBrush",
        "SystemControlHighlightAccentBrush", "SystemControlForegroundAccentBrush", "SystemControlBackgroundAccentBrush",
        "SystemControlHighlightAltAccentBrush", "SystemControlDisabledAccentBrush",
        "SystemControlHighlightListAccentLowBrush", "SystemControlHighlightListAccentMediumBrush",
        "SystemControlHighlightListAccentHighBrush",
    ];

    /// <summary>
    /// [S-823] [S-6563] What a card's fill is for its effective background choice
    /// (<see cref="FillChoice.Effective"/>): Default is the Fluent base and card tint at full
    /// strength, Transparent nothing, a colour exactly that colour at its opacity. A colour choice
    /// whose colour does not parse (load validation never leaves one) is the default.
    /// </summary>
    public static CardFill Card(FillChoice fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        return fill.Kind switch
        {
            FillKind.Transparent => new CardFill(FillKind.Transparent, null, 0),
            FillKind.Colour when HexColor.TryParse(fill.Colour, out var r, out var g, out var b)
                => new CardFill(FillKind.Colour, new RgbColor(r, g, b), FillChoice.ClampOpacity(fill.Opacity)),
            _ => new CardFill(FillKind.Default, null, 1),
        };
    }

    /// <summary>
    /// [S-1761] How opaque the header strip's fill is. A header colour of its own is drawn at the
    /// header opacity. One that shares the widget's fill is that fill faded once more by the header
    /// opacity, so 100% matches the body and 0% leaves the strip clear over the desktop — the header
    /// opacity always does something, not only once a header colour has been picked.
    /// </summary>
    public static double HeaderFillOpacity(CardFill fill, double headerOpacity, bool ownColour)
    {
        var header = Math.Clamp(double.IsFinite(headerOpacity) ? headerOpacity : 1, 0, 1);
        return ownColour ? header : fill.Opacity * header;
    }

    /// <summary>
    /// [S-824] The border for a design. None, a zero width or a transparent widget draws nothing (a
    /// colour at 0 % is not transparent: its border stays, [S-6563]); dashed is 3-on-2 and dotted is
    /// round dots two widths apart, both in stroke widths so the pattern keeps its proportions at any
    /// thickness.
    /// </summary>
    public static BorderLook Border(CardFill fill, WidgetBorderStyle style, double widthPx, double opacity)
    {
        var width = Math.Clamp(double.IsFinite(widthPx) ? widthPx : 1, 0, 8);
        var alpha = Math.Clamp(double.IsFinite(opacity) ? opacity : 1, 0, 1);

        if (fill.Kind == FillKind.Transparent || style == WidgetBorderStyle.None || width <= 0)
        {
            return new BorderLook(0, [], false, alpha);
        }

        return style switch
        {
            WidgetBorderStyle.Dashed => new BorderLook(width, [3, 2], false, alpha),
            WidgetBorderStyle.Dotted => new BorderLook(width, [0, 2], true, alpha),
            _ => new BorderLook(width, [], false, alpha),
        };
    }

    /// <summary>[S-6481] Fluent's card stroke: a 6-10% overlay for a 1px line, the default look's edge ([S-835]).</summary>
    public const string HairlineStrokeKey = "CardStrokeColorDefaultBrush";

    /// <summary>[S-6481] Fluent's strong stroke (a check box's outline, ~45-55%): a theme line that can be seen.</summary>
    public const string StrongStrokeKey = "ControlStrongStrokeColorDefaultBrush";

    /// <summary>
    /// [S-6481] The border's ink. A colour of the user's own is drawn exactly. With the theme's
    /// colour, the Fluent card is a solid 1px <see cref="HairlineStrokeKey"/>, and that is what a
    /// solid line of up to 1px gets; a heavier or a dashed or dotted line — which Fluent never draws
    /// round a card — takes <see cref="StrongStrokeKey"/>, because the hairline's few percent drawn
    /// thicker or broken read exactly like no border at all, so every width and style looked alike.
    /// The opacity is not part of the ink: it stays the line's own, so it fades without swapping.
    /// </summary>
    public static BorderInk Ink(BorderLook border, string? colourHex)
    {
        if (HexColor.TryParse(colourHex, out var r, out var g, out var b))
        {
            return new BorderInk(new RgbColor(r, g, b), null);
        }

        var hairline = border.Dashes.Count == 0 && border.Thickness <= 1;
        return new BorderInk(null, hairline ? HairlineStrokeKey : StrongStrokeKey);
    }

    /// <summary>
    /// [S-836] How far the card's Fluent tint stops short of the edge: inside a solid stroke, as a
    /// WinUI <c>Border</c>'s background stops at the stroke's inner edge (<c>BackgroundSizing</c>
    /// InnerBorderEdge), so the stroke composites over the base alone, exactly as a Windows card's
    /// stroke does over the page. A dashed or dotted line keeps the tint full-bleed, or its gaps would
    /// show a ring of bare base.
    /// </summary>
    public static double TintInset(BorderLook border)
        => border.Visible && border.Dashes.Count == 0 ? border.Thickness : 0;

    /// <summary>
    /// [S-836] Source-over compositing of a colour at <paramref name="alpha"/> on an opaque one, per
    /// channel and rounded, which is what a Fluent overlay (a card tint, a stroke) draws on its base.
    /// </summary>
    public static RgbColor Over(byte alpha, RgbColor top, RgbColor under)
    {
        static byte Mix(byte a, byte t, byte u) => (byte)Math.Round(u + ((t - u) * (a / 255.0)), MidpointRounding.AwayFromZero);

        return new RgbColor(Mix(alpha, top.R, under.R), Mix(alpha, top.G, under.G), Mix(alpha, top.B, under.B));
    }

    /// <summary>[S-825] A corner radius from the setting: 0..32, not finite is the default ([S-6482], 4).</summary>
    public static double CornerRadius(double radiusPx)
        => Math.Clamp(double.IsFinite(radiusPx) ? radiusPx : DesignState.DefaultCornerRadiusPx, 0, 32);

    /// <summary>
    /// [S-826] The font family to draw widget text in, or null for the system's own (Segoe UI
    /// Variable). A preset maps to its family with a fallback; any other value is an installed
    /// family's name, accepted only if it looks like one, so a hand-edited state file cannot smuggle
    /// a fallback list or a font URI in.
    /// </summary>
    public static string? FontFamilyName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        switch (name.ToLowerInvariant())
        {
            case SystemFont:
                return null;
            case SansFont:
                return "Arial";
            case SerifFont:
                return "Georgia";
            case MonoFont:
                return "Cascadia Mono, Consolas";
        }

        return IsFamilyName(name) ? name : null;
    }

    /// <summary>
    /// Whether a string can be an installed family's name: letters, marks, digits, spaces and
    /// <c>. _ - + &amp; ( )</c>, at most 64 characters (LOGFONT's own limit is 32).
    /// </summary>
    public static bool IsFamilyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            var ok = char.IsLetterOrDigit(c)
                || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                || c is ' ' or '.' or '_' or '-' or '+' or '&' or '(' or ')';

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [S-827] The installed families the font picker lists: vertical (<c>@</c>) faces and anything
    /// that is not a family name dropped, duplicates (GDI reports a family once per script) folded
    /// case-insensitively, sorted the way a person reads them.
    /// </summary>
    public static IReadOnlyList<string> FontList(IEnumerable<string> installed)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        foreach (var raw in installed)
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name) || name.StartsWith('@') || !IsFamilyName(name) || !seen.Add(name))
            {
                continue;
            }

            list.Add(name);
        }

        list.Sort(StringComparer.CurrentCultureIgnoreCase);
        return list;
    }
}
