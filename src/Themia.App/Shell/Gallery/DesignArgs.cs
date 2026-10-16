#if DEBUG
using System.Globalization;
using Themia.Core.Model;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug renders only: <c>--design key=value,...</c> sets appearance values on the design a render
/// draws its cards with, so a border, radius or fill can be looked at without the settings window.
/// Keys: <c>border</c> (none|solid|dashed|dotted), <c>borderWidth</c>, <c>borderOpacity</c> (0..1 or
/// a percentage such as 25%), <c>borderColor</c> (#RRGGBB), <c>radius</c>, <c>background</c>,
/// <c>opacity</c>, <c>header</c>, <c>headerOpacity</c>, <c>accent</c>, <c>font</c>, <c>preset</c>
/// (applied first, so later keys refine it). An unknown key or a bad value is printed and skipped.
/// </summary>
internal static class DesignArgs
{
    /// <summary>The design for a render: <paramref name="theme"/>, then the <c>--design</c> values in order.</summary>
    public static DesignState From(string[] args, AppTheme theme)
    {
        var design = new DesignState();
        var at = Array.IndexOf(args, "--design");
        if (at >= 0 && at + 1 < args.Length)
        {
            foreach (var pair in args[at + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0 || !Set(design, pair[..eq].Trim(), pair[(eq + 1)..].Trim()))
                {
                    Console.WriteLine($"design: skipped '{pair}'");
                }
            }
        }

        // The render's --theme wins over a preset's own, so one look can be drawn in both.
        if (theme != AppTheme.System)
        {
            design.Theme = theme;
        }

        return design;
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
            case "bordercolor" or "bordercolour":
                d.BorderColor = value;
                return true;
            case "radius" when Number(value) is { } r:
                d.CornerRadiusPx = r;
                return true;
            case "background":
                d.WidgetBackgroundColor = value;
                return true;
            case "opacity" when Fraction(value) is { } o:
                d.SurfaceOpacity = o;
                return true;
            case "header":
                d.HeaderBackgroundColor = value;
                return true;
            case "headeropacity" when Fraction(value) is { } o:
                d.HeaderOpacity = o;
                return true;
            case "accent":
                d.PrimaryColor = value;
                return true;
            case "font":
                d.FontFamily = value;
                return true;
            default:
                return false;
        }
    }

    private static double? Number(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;

    private static double? Fraction(string value)
        => value.EndsWith('%') ? Number(value[..^1]) / 100 : Number(value);
}
#endif
