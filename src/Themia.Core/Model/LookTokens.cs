using System.Text.Json.Serialization;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-2133] How a widget's content is coloured, after macOS's widget rendering modes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RenderingMode>))]
public enum RenderingMode
{
    /// <summary>The content's own colours: the accent, the data's (a calendar's, an app's).</summary>
    FullColour = 0,

    /// <summary>The accent and every data colour in the accent's hue, each at its own lightness; the glass takes the accent too.</summary>
    Tinted = 1,

    /// <summary>No colour at all: the accent is the text's ink and every data colour its own grey, as macOS draws a widget the desktop is not focused on.</summary>
    Monochrome = 2,
}

/// <summary>[S-2138] What the items inside a card (a tile, a chip, a track, a lit row) are filled with by default.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InnerSurface>))]
public enum InnerSurface
{
    /// <summary>Windows' own control and subtle fills, as every style but Liquid glass draws them.</summary>
    Theme = 0,

    /// <summary>The text's ink at a whisper (macOS's quaternary fill): a nested glass that takes its colour from what lies behind.</summary>
    Vibrant = 1,
}

/// <summary>[S-2137] Which text level a widget's header name is drawn in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeaderTone>))]
public enum HeaderTone
{
    /// <summary>The secondary level: the header labels the widget (the family's own).</summary>
    Secondary = 0,

    /// <summary>The primary level, as a macOS widget's title.</summary>
    Primary = 1,
}

/// <summary>[S-2137] How a widget's header name is cased.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeaderCase>))]
public enum HeaderCase
{
    AsWritten = 0,
    Upper = 1,
}

/// <summary>
/// [S-2130] One number token: its key (the settings line's, the <c>--design</c> key and the file's name), its range,
/// the step a slider takes and its default; how it reads and writes a <see cref="LookTokens"/>.
/// </summary>
public sealed record LookNumber(string Key, double Min, double Max, double Step, double Default, Func<LookTokens, double> Get, Action<LookTokens, double> Set)
{
    /// <summary>[S-2130] The value held to the range: not finite is the default.</summary>
    public double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, Min, Max) : Default;
}

/// <summary>
/// [S-2130] The design's look tokens: every axis of a card's look past its fill, border and font, after macOS 26's
/// widgets and Liquid Glass (HIG Materials, vibrancy, widget anatomy). Grouped as the Appearance page shows them:
/// <list type="bullet">
/// <item><b>Material</b> ([S-2131]..[S-2133], [S-2140]): the frost under every translucent card (blur, saturation, brightness), how far the card takes the accent, the rendering mode, the grain; and the glass's tint per theme, glow and lens;</item>
/// <item><b>Shape</b> ([S-2132]): the glass's rim (width, strength) and lift (radius, drop, strength);</item>
/// <item><b>Text</b> ([S-2134]..[S-2137]): the text levels' vibrancy and each level's opacity, the weight per role, the tracking at large and small sizes, tabular figures, the header's tone and case;</item>
/// <item><b>Surfaces</b> ([S-2138]): the items' fills inside a card (theme or vibrant) at rest, lit and selected, and a meter's track;</item>
/// <item><b>Layout</b> ([S-2139]): the list rows' dividers and a divider's opacity.</item>
/// </list>
/// A fresh one draws every style but Liquid glass exactly as before: the frost, text, type, surfaces and rows are
/// Windows' and the family's own; the glass's tokens only matter on glass and their defaults are Liquid glass's.
/// Owned by the styles ([S-2143]) like the rest of the look.
/// </summary>
[JsonConverter(typeof(LookTokensJson))]
public sealed class LookTokens
{
    // ---- Material ----

    /// <summary>[S-2133] How the content is coloured.</summary>
    public RenderingMode Rendering { get; set; } = RenderingMode.FullColour;

    /// <summary>[S-2131] The frost's blur, as a Gaussian's sigma in display DIPs (0 to 80; 40 Themia's own).</summary>
    public double FrostBlur { get; set; } = 40;

    /// <summary>[S-2131] The frost's saturation (0 grey, 1 the wallpaper's own, 2 twice as vivid).</summary>
    public double FrostSaturation { get; set; } = 1;

    /// <summary>[S-2131] The frost lifted toward white (up to 0.3) or lowered toward black (down to -0.3).</summary>
    public double FrostBrightness { get; set; }

    /// <summary>[S-2133] How far a card's own fill (the default card, the glass's tint) is mixed toward the accent, 0 to 0.6.</summary>
    public double AccentTint { get; set; }

    /// <summary>[S-2140] A fine grain over the card's material, its strength 0 to 0.12.</summary>
    public double Grain { get; set; }

    /// <summary>[S-2132] Light glass's tint: as clear as <see cref="GlassTintLightMin"/>, as clouded as <see cref="GlassTintLightMax"/>.</summary>
    public double GlassTintLightMin { get; set; } = 0.16;

    public double GlassTintLightMax { get; set; } = 0.66;

    /// <summary>[S-2132] Dark glass's tint, on the same terms.</summary>
    public double GlassTintDarkMin { get; set; } = 0.24;

    public double GlassTintDarkMax { get; set; } = 0.74;

    /// <summary>[S-2132] The glow falling in at the glass's top edge, times its theme's (0 to 2).</summary>
    public double GlowStrength { get; set; } = 1;

    /// <summary>[S-2132] The lens band inside the rim, times its theme's (0 to 2).</summary>
    public double LensStrength { get; set; } = 1;

    // ---- Shape ----

    /// <summary>[S-2132] The glass's rim, in DIPs (0 to 3).</summary>
    public double RimWidth { get; set; } = 1;

    /// <summary>[S-2132] The rim's light, times its theme's (0 to 2, an alpha never past 1).</summary>
    public double RimStrength { get; set; } = 1;

    /// <summary>[S-2132] The glass's lift: how far its shadow reaches, in DIPs (0 to 80).</summary>
    public double ShadowRadius { get; set; } = 36;

    /// <summary>[S-2132] How far the lift falls under the glass, in DIPs (0 to 40).</summary>
    public double ShadowOffset { get; set; } = 10;

    /// <summary>[S-2132] The lift's darkness, times its theme's (0 to 2).</summary>
    public double ShadowStrength { get; set; } = 1;

    // ---- Text ----

    /// <summary>[S-2134] How far the text levels move from Windows' own toward <see cref="LabelPrimary"/>..<see cref="LabelQuaternary"/> (0 to 1).</summary>
    public double Vibrancy { get; set; }

    /// <summary>[S-2134] The primary level's opacity of the theme's ink (0.5 to 1).</summary>
    public double LabelPrimary { get; set; } = 0.88;

    /// <summary>[S-2134] The secondary level's (0.2 to 1).</summary>
    public double LabelSecondary { get; set; } = 0.6;

    /// <summary>[S-2134] The tertiary level's (0.1 to 1).</summary>
    public double LabelTertiary { get; set; } = 0.4;

    /// <summary>[S-2134] The quaternary level's: what is greyed (0.05 to 1).</summary>
    public double LabelQuaternary { get; set; } = 0.24;

    /// <summary>[S-2135] The header's name, Title and the one emphasised row.</summary>
    public TypeWeight HeaderWeight { get; set; } = TypeWeight.Semibold;

    /// <summary>[S-2135] A readout's number (28 and up).</summary>
    public TypeWeight ReadoutWeight { get; set; } = TypeWeight.Semibold;

    /// <summary>[S-2135] Every regular text: a row, a sentence, a caption, a unit.</summary>
    public TypeWeight BodyWeight { get; set; } = TypeWeight.Regular;

    /// <summary>[S-2136] The letter spacing at 28 and up, in thousandths of an em (-60 to 40).</summary>
    public double TrackingLarge { get; set; }

    /// <summary>[S-2136] The letter spacing at 14 and under, in thousandths of an em (-30 to 30).</summary>
    public double TrackingSmall { get; set; }

    /// <summary>[S-2136] Figures of one width, so a changing number does not shift.</summary>
    public bool TabularNumerals { get; set; }

    /// <summary>[S-2137] The header name's level.</summary>
    public HeaderTone HeaderTone { get; set; } = HeaderTone.Secondary;

    /// <summary>[S-2137] The header name's case.</summary>
    public HeaderCase HeaderCase { get; set; } = HeaderCase.AsWritten;

    // ---- Surfaces ----

    /// <summary>[S-2138] Windows' fills for the items, or vibrant ones.</summary>
    public InnerSurface InnerSurface { get; set; } = InnerSurface.Theme;

    /// <summary>[S-2138] A vibrant tile's or chip's fill at rest: the ink's opacity (0 to 0.4).</summary>
    public double InnerFill { get; set; } = 0.08;

    /// <summary>[S-2138] A vibrant item under the pointer (0 to 0.4).</summary>
    public double InnerHover { get; set; } = 0.06;

    /// <summary>[S-2138] A vibrant item selected or pressed (0 to 0.4).</summary>
    public double InnerSelected { get; set; } = 0.12;

    /// <summary>[S-2138] A vibrant meter's track, a ring's, a rail (0 to 0.5).</summary>
    public double TrackOpacity { get; set; } = 0.12;

    // ---- Layout ----

    /// <summary>[S-2139] The list rows' dividers.</summary>
    public RowDivider RowDividers { get; set; } = RowDivider.None;

    /// <summary>[S-2139] A divider's ink opacity: the rows' and a glass group's between its members (0 to 0.4).</summary>
    public double DividerOpacity { get; set; } = 0.1;

    /// <summary>[S-2130] Every number token, in the page's order: the one table the settings, the parser, load validation and the tests read.</summary>
    public static IReadOnlyList<LookNumber> Numbers { get; } =
    [
        new("frostBlur", 0, 80, 4, 40, t => t.FrostBlur, (t, v) => t.FrostBlur = v),
        new("frostSaturation", 0, 2, 0.05, 1, t => t.FrostSaturation, (t, v) => t.FrostSaturation = v),
        new("frostBrightness", -0.3, 0.3, 0.01, 0, t => t.FrostBrightness, (t, v) => t.FrostBrightness = v),
        new("accentTint", 0, 0.6, 0.02, 0, t => t.AccentTint, (t, v) => t.AccentTint = v),
        new("grain", 0, 0.12, 0.01, 0, t => t.Grain, (t, v) => t.Grain = v),
        new("glassTintLightMin", 0, 1, 0.02, 0.16, t => t.GlassTintLightMin, (t, v) => t.GlassTintLightMin = v),
        new("glassTintLightMax", 0, 1, 0.02, 0.66, t => t.GlassTintLightMax, (t, v) => t.GlassTintLightMax = v),
        new("glassTintDarkMin", 0, 1, 0.02, 0.24, t => t.GlassTintDarkMin, (t, v) => t.GlassTintDarkMin = v),
        new("glassTintDarkMax", 0, 1, 0.02, 0.74, t => t.GlassTintDarkMax, (t, v) => t.GlassTintDarkMax = v),
        new("glowStrength", 0, 2, 0.1, 1, t => t.GlowStrength, (t, v) => t.GlowStrength = v),
        new("lensStrength", 0, 2, 0.1, 1, t => t.LensStrength, (t, v) => t.LensStrength = v),
        new("rimWidth", 0, 3, 1, 1, t => t.RimWidth, (t, v) => t.RimWidth = v),
        new("rimStrength", 0, 2, 0.1, 1, t => t.RimStrength, (t, v) => t.RimStrength = v),
        new("shadowRadius", 0, 80, 4, 36, t => t.ShadowRadius, (t, v) => t.ShadowRadius = v),
        new("shadowOffset", 0, 40, 1, 10, t => t.ShadowOffset, (t, v) => t.ShadowOffset = v),
        new("shadowStrength", 0, 2, 0.1, 1, t => t.ShadowStrength, (t, v) => t.ShadowStrength = v),
        new("vibrancy", 0, 1, 0.05, 0, t => t.Vibrancy, (t, v) => t.Vibrancy = v),
        new("labelPrimary", 0.5, 1, 0.01, 0.88, t => t.LabelPrimary, (t, v) => t.LabelPrimary = v),
        new("labelSecondary", 0.2, 1, 0.01, 0.6, t => t.LabelSecondary, (t, v) => t.LabelSecondary = v),
        new("labelTertiary", 0.1, 1, 0.01, 0.4, t => t.LabelTertiary, (t, v) => t.LabelTertiary = v),
        new("labelQuaternary", 0.05, 1, 0.01, 0.24, t => t.LabelQuaternary, (t, v) => t.LabelQuaternary = v),
        new("trackingLarge", -60, 40, 2, 0, t => t.TrackingLarge, (t, v) => t.TrackingLarge = v),
        new("trackingSmall", -30, 30, 1, 0, t => t.TrackingSmall, (t, v) => t.TrackingSmall = v),
        new("innerFill", 0, 0.4, 0.01, 0.08, t => t.InnerFill, (t, v) => t.InnerFill = v),
        new("innerHover", 0, 0.4, 0.01, 0.06, t => t.InnerHover, (t, v) => t.InnerHover = v),
        new("innerSelected", 0, 0.4, 0.01, 0.12, t => t.InnerSelected, (t, v) => t.InnerSelected = v),
        new("trackOpacity", 0, 0.5, 0.01, 0.12, t => t.TrackOpacity, (t, v) => t.TrackOpacity = v),
        new("dividerOpacity", 0, 0.4, 0.01, 0.1, t => t.DividerOpacity, (t, v) => t.DividerOpacity = v),
    ];

    /// <summary>The number token of <paramref name="key"/> (case-insensitive), or null.</summary>
    public static LookNumber? Number(string key)
        => Numbers.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));

    public LookTokens Clone() => (LookTokens)MemberwiseClone();

    /// <summary>
    /// [S-2130] Bring loaded tokens inside their invariants, saying what changed: a number out of its range is held to
    /// it, one that is not finite is its default, an enum value that is not one is its default. Pure and total.
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        foreach (var number in Numbers)
        {
            var was = number.Get(this);
            var now = number.Clamp(was);
            if (!was.Equals(now))
            {
                number.Set(this, now);
                repairs.Add($"look token '{number.Key}' {was} is out of {number.Min}..{number.Max}; using {now}");
            }
        }

        T Defined<T>(T value, T fallback, string key)
            where T : struct, Enum
        {
            if (Enum.IsDefined(value))
            {
                return value;
            }

            repairs.Add($"look token '{key}' {value} is not one; using {fallback}");
            return fallback;
        }

        Rendering = Defined(Rendering, RenderingMode.FullColour, "rendering");
        HeaderWeight = Defined(HeaderWeight, TypeWeight.Semibold, "headerWeight");
        ReadoutWeight = Defined(ReadoutWeight, TypeWeight.Semibold, "readoutWeight");
        BodyWeight = Defined(BodyWeight, TypeWeight.Regular, "bodyWeight");
        HeaderTone = Defined(HeaderTone, HeaderTone.Secondary, "headerTone");
        HeaderCase = Defined(HeaderCase, HeaderCase.AsWritten, "headerCase");
        InnerSurface = Defined(InnerSurface, InnerSurface.Theme, "innerSurface");
        RowDividers = Defined(RowDividers, RowDivider.None, "rowDividers");
        return repairs;
    }

    /// <summary>[S-2143] Whether two token sets are the same look: every enum and flag equal, every number within a slider's rounding.</summary>
    public bool SameAs(LookTokens? other)
    {
        if (other is null)
        {
            return false;
        }

        foreach (var number in Numbers)
        {
            if (Math.Abs(number.Get(this) - number.Get(other)) >= 0.001)
            {
                return false;
            }
        }

        return Rendering == other.Rendering && HeaderWeight == other.HeaderWeight && ReadoutWeight == other.ReadoutWeight
            && BodyWeight == other.BodyWeight && TabularNumerals == other.TabularNumerals && HeaderTone == other.HeaderTone
            && HeaderCase == other.HeaderCase && InnerSurface == other.InnerSurface && RowDividers == other.RowDividers;
    }

    /// <summary>[S-2135] [S-2136] The type tokens as the family draws them.</summary>
    public TypeLook Type() => new(HeaderWeight, ReadoutWeight, BodyWeight, TrackingLarge, TrackingSmall, TabularNumerals);
}
