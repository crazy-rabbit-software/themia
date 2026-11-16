using System.Text.Json.Serialization;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-2135] A weight the family's text may be drawn at, by its OpenType number: what the design's type tokens choose
/// per role (<see cref="TypeLook"/>). Segoe UI Variable draws every one of them (its weight axis runs 300 to 700).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TypeWeight>))]
public enum TypeWeight
{
    Light = 300,
    Semilight = 350,
    Regular = 400,
    Medium = 500,
    Semibold = 600,
    Bold = 700,
}

/// <summary>[S-2139] Whether and how the family's list rows are parted: no line (the family's own), a hairline across the row, or one from the text edge.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RowDivider>))]
public enum RowDivider
{
    None = 0,
    Hairline = 1,
    Inset = 2,
}

/// <summary>
/// [S-2135] [S-2136] The design's type tokens as the family draws them: the weight of each role (the header's
/// and an emphasised line's, a readout's, the body's and caption's), the tracking (letter spacing, in thousandths of an
/// em) at the large steps and at the small ones, and whether figures are tabular. <see cref="Default"/> is the family's
/// own look (600 · 600 · 400, no tracking, the font's own figures), so a design that sets none of them draws exactly as
/// before.
/// </summary>
public sealed record TypeLook(
    TypeWeight HeaderWeight,
    TypeWeight ReadoutWeight,
    TypeWeight BodyWeight,
    double TrackingLarge,
    double TrackingSmall,
    bool TabularNumerals)
{
    /// <summary>The family's own type: semibold header and readouts, regular body, no tracking, the font's figures.</summary>
    public static TypeLook Default { get; } = new(TypeWeight.Semibold, TypeWeight.Semibold, TypeWeight.Regular, 0, 0, false);

    /// <summary>The base size (unscaled) from which a step is a readout: Big28 and up.</summary>
    public const double ReadoutFrom = 28;

    /// <summary>The base size up to which a step tracks as small text: Body and Caption.</summary>
    public const double SmallUpTo = 14;

    /// <summary>
    /// [S-2135] The weight a text on a step of <paramref name="baseSize"/> (unscaled) draws at: a regular one (a
    /// unit, a line under a value, a row) the body's, a strong one the readout's from <see cref="ReadoutFrom"/> up
    /// and the header's under it (the header's name, Title, the one emphasised row).
    /// </summary>
    public int WeightOf(double baseSize, bool strong)
        => (int)(!strong ? BodyWeight : baseSize >= ReadoutFrom - 0.01 ? ReadoutWeight : HeaderWeight);

    /// <summary>
    /// [S-2136] The tracking at a step of <paramref name="baseSize"/> (unscaled), in thousandths of an em: the small
    /// tracking up to <see cref="SmallUpTo"/>, the large one from <see cref="ReadoutFrom"/>, in a straight line
    /// between (SF's own table tightens as the size grows, and so does this).
    /// </summary>
    public double TrackingOf(double baseSize)
    {
        var t = Math.Clamp((baseSize - SmallUpTo) / (ReadoutFrom - SmallUpTo), 0, 1);
        return Math.Round(TrackingSmall + ((TrackingLarge - TrackingSmall) * t), 1);
    }
}

/// <summary>
/// [S-2135]..[S-2139] The design's tokens the family's blocks read as they draw, set by the host's appearance engine on
/// the UI thread (as <see cref="FamilyType.TextScale"/> is): the type (<see cref="TypeLook"/>) and the rows' dividers.
/// Pure state: the WinUI half restyles what is already drawn when it changes.
/// </summary>
public static class FamilyLook
{
    /// <summary>[S-2135] The type tokens in force.</summary>
    public static TypeLook Type { get; private set; } = TypeLook.Default;

    /// <summary>[S-2139] The list rows' dividers in force.</summary>
    public static RowDivider Rows { get; private set; } = RowDivider.None;

    /// <summary>Set the tokens in force; returns whether any changed, so the caller knows to restyle what is drawn. App code only.</summary>
    public static bool Set(TypeLook type, RowDivider rows)
    {
        ArgumentNullException.ThrowIfNull(type);
        var changed = type != Type || rows != Rows;
        (Type, Rows) = (type, rows);
        return changed;
    }
}
