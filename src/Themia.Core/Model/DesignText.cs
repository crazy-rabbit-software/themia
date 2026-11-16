using System.Globalization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-6480] Appearance values written as text, <c>key=value,...</c>: what the debug renders'
/// <c>--design</c> takes, read by this one parser.
/// Keys: <c>preset</c> (applied where it stands, so later keys refine it), <c>border</c>
/// (none|solid|dashed|dotted), <c>borderWidth</c>, <c>borderOpacity</c> (0..1 or a percentage such as
/// 25%), <c>borderColor</c> (#RRGGBB), <c>radius</c>, <c>background</c>
/// (default|transparent|#RRGGBB[@percent], [S-6569]), <c>opacity</c> (the background colour's),
/// <c>header</c>, <c>headerOpacity</c>, <c>accent</c>, <c>font</c>, <c>items</c> and
/// <c>itemOpacity</c> (the item background, on the same terms, [S-6510]), <c>itemText</c> (its text
/// colour, [S-6547]), <c>gap</c> (between neighbouring cards, DIPs, [S-04]), <c>blur</c> (on|off: blur
/// behind widgets, [S-2110]), <c>material</c> (solid|liquidGlass, [S-2123]) and every look token by its own key
/// ([S-2142], <see cref="LookTokens"/>: <c>frostBlur=48,vibrancy=1,innerSurface=vibrant,rowDividers=inset,...</c>). A colour is #RRGGBB, or
/// empty for the design's own; anything else is refused, not passed on ([S-1075]). Pure.
/// </summary>
public static class DesignText
{
    /// <summary>
    /// Apply <paramref name="text"/>'s pairs to <paramref name="design"/> in order. Returns each pair
    /// that was not applied (an unknown key, a bad value, no <c>=</c>), as written; empty when every
    /// pair was.
    /// </summary>
    public static IReadOnlyList<string> Apply(DesignState design, string? text)
    {
        ArgumentNullException.ThrowIfNull(design);

        var refused = new List<string>();
        foreach (var pair in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0 || !Set(design, pair[..eq].Trim(), pair[(eq + 1)..].Trim()))
            {
                refused.Add(pair);
            }
        }

        return refused;
    }

    /// <summary>
    /// [S-6569] A fill choice written as text: <c>default</c>, <c>transparent</c> (or <c>none</c>),
    /// or <c>#RRGGBB</c> with an optional <c>@percent</c> opacity (else <paramref name="opacity"/>).
    /// Null for anything else.
    /// </summary>
    public static FillChoice? Fill(string? value, double opacity = 1)
    {
        var text = value?.Trim() ?? string.Empty;
        switch (text.ToLowerInvariant())
        {
            case "default":
                return FillChoice.Default();
            case "transparent" or "none":
                return FillChoice.Transparent();
        }

        var at = text.IndexOf('@');
        if (at > 0 && Fraction(text[(at + 1)..]) is { } own)
        {
            opacity = own;
            text = text[..at];
        }

        return HexColor.TryParse(text, out _, out _, out _) ? FillChoice.Of(text, opacity) : null;
    }

    private static bool Set(DesignState d, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "preset" when Enum.TryParse<DesignPresetId>(value, true, out var id) && Enum.IsDefined(id):
                DesignPresets.Apply(d, id);
                return true;
            case "border" when Enum.TryParse<WidgetBorderStyle>(value, true, out var style) && Enum.IsDefined(style):
                d.BorderStyle = style;
                return true;
            case "borderwidth" when Number(value) is { } w:
                d.BorderWidthPx = w;
                return true;
            case "borderopacity" when Fraction(value) is { } o:
                d.BorderOpacity = o;
                return true;
            case "bordercolor" or "bordercolour" when Colour(value) is { } border:
                d.BorderColor = border;
                return true;
            case "radius" when Number(value) is { } r:
                d.CornerRadiusPx = r;
                return true;
            case "background" when Fill(value, d.Background.Opacity) is { } fill:
                d.Background = fill;
                return true;
            case "opacity" when Fraction(value) is { } o:
                d.Background.Opacity = o;
                return true;
            case "header" when Colour(value) is { } header:
                d.HeaderBackgroundColor = header;
                return true;
            case "headeropacity" when Fraction(value) is { } o:
                d.HeaderOpacity = o;
                return true;
            case "accent" when Colour(value) is { } accent:
                d.PrimaryColor = accent;
                return true;
            case "font":
                d.FontFamily = value;
                return true;
            case "items" or "itemcolor" or "itemcolour" when Fill(value, d.ItemBackground.Opacity) is { } fill:
                d.ItemBackground = fill;
                return true;
            case "itemopacity" when Fraction(value) is { } o:
                d.ItemBackground.Opacity = o;
                return true;
            case "itemtext" or "itemtextcolor" or "itemtextcolour" when Colour(value) is { } text:
                d.ItemTextColor = text;
                return true;
            case "blur" when OnOff(value) is { } on:
                d.BlurBehind = on;
                return true;
            case "material" when Enum.TryParse<CardMaterial>(value, true, out var material) && Enum.IsDefined(material):
                d.Material = material;
                return true;
            default:
                return Token(d.Tokens ??= new LookTokens(), key, value);
        }
    }

    /// <summary>
    /// [S-2142] A look token by its own key (<see cref="LookTokens.Numbers"/>' keys, held to the token's range; a fraction
    /// may be a percentage), an enum token by a value's name, <c>tabularNumerals</c> on|off.
    /// </summary>
    private static bool Token(LookTokens t, string key, string value)
    {
        if (LookTokens.Number(key) is { } number)
        {
            if ((number.Max <= 2 ? Fraction(value) : Number(value)) is not { } n)
            {
                return false;
            }

            number.Set(t, number.Clamp(n));
            return true;
        }

        static bool Pick<T>(string value, Action<T> set)
            where T : struct, Enum
        {
            if (!Enum.TryParse<T>(value, true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            set(parsed);
            return true;
        }

        return key.ToLowerInvariant() switch
        {
            "rendering" => Pick<RenderingMode>(value, v => t.Rendering = v),
            "headerweight" => Pick<Sdk.Kit.TypeWeight>(value, v => t.HeaderWeight = v),
            "readoutweight" => Pick<Sdk.Kit.TypeWeight>(value, v => t.ReadoutWeight = v),
            "bodyweight" => Pick<Sdk.Kit.TypeWeight>(value, v => t.BodyWeight = v),
            "headertone" => Pick<HeaderTone>(value, v => t.HeaderTone = v),
            "headercase" => Pick<HeaderCase>(value, v => t.HeaderCase = v),
            "innersurface" => Pick<InnerSurface>(value, v => t.InnerSurface = v),
            "rowdividers" => Pick<Sdk.Kit.RowDivider>(value, v => t.RowDividers = v),
            "tabularnumerals" when OnOff(value) is { } on => Set(() => t.TabularNumerals = on),
            _ => false,
        };

        static bool Set(Action set)
        {
            set();
            return true;
        }
    }

    /// <summary>A colour as stored (<c>#RRGGBB</c>), empty for the design's own, null for anything else.</summary>
    private static string? Colour(string value)
        => value.Length == 0 ? string.Empty : HexColor.TryParse(value, out _, out _, out _) ? HexColor.Normalise(value) : null;

    private static bool? OnOff(string value) => value.ToLowerInvariant() switch
    {
        "on" or "true" => true,
        "off" or "false" => false,
        _ => null,
    };

    private static double? Number(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;

    private static double? Fraction(string value)
        => value.EndsWith('%') ? Number(value[..^1]) / 100 : Number(value);
}
