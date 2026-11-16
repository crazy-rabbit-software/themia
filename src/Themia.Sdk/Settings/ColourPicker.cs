using Themia.Core.Widgets;

namespace Themia.Core.Settings;

/// <summary>[S-1574] Where one swatch of the colour picker stands, in DIPs from the picker's body's top left, and which line it is on.</summary>
public readonly record struct SwatchSpot(int Index, double X, double Y, int Line);

/// <summary>
/// [S-1574] The colour picker laid out: whether the mixer stands beside the swatches or under them, how
/// many swatches to a line, how many of the first line's are shown (it is one line: the oldest recent
/// colours past it are left out), the swatches' spots (the first line's, then the presets', by index in
/// that order), and the mixer's box (spectrum, then its sliders).
/// </summary>
public sealed record ColourPickerPlan(
    bool Beside,
    int Across,
    int FirstShown,
    IReadOnlyList<SwatchSpot> Swatches,
    double MixerX,
    double MixerY,
    double MixerWidth,
    double Height);

/// <summary>
/// [S-1574]..[S-1577] Karpo's colour picker (karpo-app docs/widgets.md §523, §627, §670, §682, §690;
/// <c>SettingsPanel.Picker.cs</c>) as pure rules: the swatches in two groups, each from a line of its own
/// — the first line the "no colour" choices (Default, a fill's Transparent) and the recent colours,
/// however few, the presets under it in two lines of six — and beside them, where the body is at least
/// <see cref="BesideLeast"/> wide, the mixer (a spectrum 96 tall, the hue's and an opacity's sliders),
/// else the mixer under them. The hex box is a row of its own under the body. Sizes are Karpo's.
/// </summary>
public static class ColourPicker
{
    /// <summary>Swatches a line, as Karpo's (its ColorPalette.Columns): the recent colours' line and each presets' line.</summary>
    public const int Columns = 6;

    /// <summary>The presets: two lines.</summary>
    public const int PresetLines = 2;

    /// <summary>The most recent colours kept: one line's, less nothing (the first line's "no colour" choices stand before them and may push the newest off it).</summary>
    public const int MostRecent = Columns;

    /// <summary>A swatch's side, and the gap between two.</summary>
    public const double Swatch = 36;

    public const double Gap = 4;

    /// <summary>Between the swatches' column and the mixer's, and under the body.</summary>
    public const double Pad = 12;

    /// <summary>The narrowest body that stands the mixer beside the swatches.</summary>
    public const double BesideLeast = 420;

    /// <summary>The spectrum's height, a slider row's, and the space after the spectrum.</summary>
    public const double Spectrum = 96;

    public const double Slider = 28;

    public const double SpectrumGap = 8;

    /// <summary>
    /// [S-1575] The recent colours as the picker shows them: <paramref name="stored"/> normalised to
    /// <c>#RRGGBB</c>, what is no colour dropped, each once, newest first, at most <see cref="MostRecent"/>.
    /// </summary>
    public static IReadOnlyList<string> Recent(IEnumerable<string?>? stored)
        => stored is null ? [] : [.. stored.Select(HexColor.Normalise).Where(hex => hex.Length > 0).Distinct(StringComparer.Ordinal).Take(MostRecent)];

    /// <summary>[S-1575] <paramref name="hex"/> kept from the mixer, the hex box or the eyedropper: first of the recent colours, the oldest dropped past the line.</summary>
    public static IReadOnlyList<string> Remember(IEnumerable<string?>? stored, string? hex)
    {
        var colour = HexColor.Normalise(hex);
        return colour.Length == 0 ? Recent(stored) : Recent(new[] { colour }.Concat(stored ?? []));
    }

    /// <summary>
    /// [S-1576] The presets a picker shows of the colour table it names its colours from: Windows' accent
    /// palette shows <see cref="ColorPresets.Accents"/>, the surfaces' table <see cref="ColorPresets.SurfaceSwatches"/>,
    /// any other (a module's own inks) its first two lines.
    /// </summary>
    public static IReadOnlyList<ColorPreset> Presets(IReadOnlyList<ColorPreset> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return ReferenceEquals(table, ColorPresets.Windows) ? ColorPresets.Accents
            : ReferenceEquals(table, ColorPresets.Surfaces) ? ColorPresets.SurfaceSwatches
            : [.. table.Take(Columns * PresetLines)];
    }

    /// <summary>
    /// [S-1574] The picker across <paramref name="width"/> DIPs: <paramref name="first"/> swatches on the
    /// first line (the "no colour" choices and the recent colours), <paramref name="presets"/> under it,
    /// the mixer of <paramref name="sliders"/> slider rows under its spectrum. Beside: the swatches in the
    /// left half (less the pad), <see cref="Columns"/> to a line, fewer where fewer fit; the mixer the rest
    /// from the top. Under: the swatches across the whole, the mixer a pad under
    /// them. The first line stands even when empty, so the presets never move.
    /// </summary>
    public static ColourPickerPlan Lay(double width, int first, int presets, int sliders)
    {
        width = double.IsFinite(width) ? Math.Max(Swatch, width) : BesideLeast;
        first = Math.Max(0, first);
        presets = Math.Max(0, presets);
        sliders = Math.Max(0, sliders);

        var beside = width >= BesideLeast;
        var column = beside ? Math.Floor((width - Pad) * 0.5) : width;
        var step = Swatch + Gap;
        var most = Math.Max(1, (int)Math.Floor((column + step - Swatch) / step));
        var across = Math.Min(most, Columns);

        // The first line is one line, however many the recent colours are.
        var shown = Math.Min(first, across);
        var spots = new List<SwatchSpot>(shown + presets);
        for (var i = 0; i < shown; i++)
        {
            spots.Add(new SwatchSpot(spots.Count, i * step, 0, 0));
        }

        const int line = 1;
        for (var i = 0; i < presets; i++)
        {
            var at = line + (i / across);
            spots.Add(new SwatchSpot(spots.Count, i % across * step, at * step, at));
        }

        var lines = line + (int)Math.Ceiling(presets / (double)across);
        var swatchesTall = (lines * step) - Gap;
        var mixerTall = Spectrum + (sliders > 0 ? SpectrumGap + (sliders * Slider) : 0);
        return beside
            ? new ColourPickerPlan(true, across, shown, spots, column + Pad, 0, width - column - Pad, Math.Max(swatchesTall, mixerTall))
            : new ColourPickerPlan(false, across, shown, spots, 0, swatchesTall + Pad, width, swatchesTall + Pad + mixerTall);
    }

    /// <summary>
    /// [S-1574] A colour from its hue (degrees, any; wrapped to 0..360), saturation and value (0..1, held
    /// there): the spectrum's point and the hue slider's place as the colour they stand for.
    /// </summary>
    public static (byte R, byte G, byte B) FromHsv(double hue, double saturation, double value)
    {
        var h = double.IsFinite(hue) ? ((hue % 360) + 360) % 360 : 0;
        var s = double.IsFinite(saturation) ? Math.Clamp(saturation, 0, 1) : 0;
        var v = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        var m = v - c;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return (Byte(r + m), Byte(g + m), Byte(b + m));
    }

    /// <summary>
    /// [S-1574] A colour's hue, saturation and value; a grey has no hue of its own, so it keeps
    /// <paramref name="heldHue"/> (the slider stays where it stood, as Karpo's mixer keeps it).
    /// </summary>
    public static (double Hue, double Saturation, double Value) ToHsv(byte r, byte g, byte b, double heldHue = 0)
    {
        var (rf, gf, bf) = (r / 255d, g / 255d, b / 255d);
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;
        double hue;
        if (delta <= 0)
        {
            hue = double.IsFinite(heldHue) ? ((heldHue % 360) + 360) % 360 : 0;
        }
        else if (max == rf)
        {
            hue = 60 * (((gf - bf) / delta % 6 + 6) % 6);
        }
        else if (max == gf)
        {
            hue = 60 * (((bf - rf) / delta) + 2);
        }
        else
        {
            hue = 60 * (((rf - gf) / delta) + 4);
        }

        return (hue, max <= 0 ? 0 : delta / max, max);
    }

    /// <summary>
    /// [S-1574] The saturation and value at a point of the spectrum <paramref name="width"/> ×
    /// <paramref name="height"/>: saturation across from its left, value up from its foot, held at its edges.
    /// </summary>
    public static (double Saturation, double Value) SpectrumAt(double x, double y, double width, double height)
    {
        var s = width > 0 && double.IsFinite(x) ? Math.Clamp(x / width, 0, 1) : 0;
        var v = height > 0 && double.IsFinite(y) ? Math.Clamp(1 - (y / height), 0, 1) : 1;
        return (s, v);
    }

    private static byte Byte(double unit) => (byte)Math.Clamp(Math.Round(unit * 255), 0, 255);

    /// <summary>
    /// [S-1577] What the hex box's words are as a colour: <c>#RRGGBB</c>, with or without the "#", either
    /// case, three digits as CSS; null for anything else (what the box then puts back on leaving it).
    /// </summary>
    public static string? Typed(string? text)
    {
        var hex = HexColor.Normalise(text);
        return hex.Length == 0 ? null : hex;
    }
}
