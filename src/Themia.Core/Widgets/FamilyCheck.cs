using System.Globalization;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-6731] What a drawn element is, for the render walk's rules.</summary>
public enum FamilyPartType
{
    /// <summary>A text block (and its runs).</summary>
    Text,

    /// <summary>A font glyph.</summary>
    Glyph,

    /// <summary>A border or a panel: padding, margin, spacing, radius, fill, stroke.</summary>
    Box,

    /// <summary>A shape: stroke, fill, a rectangle's radius.</summary>
    Shape,

    /// <summary>A control (a button, a switch): its own font size, padding, radius and fills.</summary>
    Control,
}

/// <summary>A colour an element paints, the slot it paints it in, and whether it came from the data colour entry point.</summary>
public readonly record struct FamilyColour(string Slot, string Argb, bool Data = false);

/// <summary>
/// [S-6731] One element of a card as the render walk saw it: its type, its text step (size, line,
/// weight; a run is its own fact), its lengths (padding, margin and spacing components, named), its
/// corner radii with its size, its stroke, and its colours as <c>#AARRGGBB</c>.
/// </summary>
public sealed record FamilyFact(
    FamilyPartType Part,
    string Type,
    double FontSize = 0,
    double LineHeight = 0,
    int Weight = FamilyType.RegularWeight,
    IReadOnlyList<(string Name, double Value)>? Lengths = null,
    IReadOnlyList<double>? Radii = null,
    double Width = 0,
    double Height = 0,
    double Stroke = 0,
    IReadOnlyList<FamilyColour>? Colours = null);

/// <summary>
/// [S-6731] The render walk's rules: every text size, line and weight is a family step (a Hero step
/// only for a one-readout widget); every glyph is 16, 32, 48 or a Readout step; every padding, margin
/// and spacing component is on the space scale or one of its derived insets; every radius is 0, 4 or
/// round; every stroke 0, 1, 2 or 4; every colour a role of the current theme (or the item layer's,
/// or a data colour from its entry point, or clear); and no text or glyph is drawn in the accent.
/// Pure: the Debug app walks the trees (<c>--family-check</c>), these rules judge what it saw, and a
/// finding is one short line, the same for the same offence, so a baseline can list it.
/// </summary>
public static class FamilyCheck
{
    /// <summary>
    /// The findings for <paramref name="fact"/> in a widget that is or is not a one-readout widget,
    /// against <paramref name="palette"/> (each allowed <c>#AARRGGBB</c> and the role it is) and
    /// <paramref name="accent"/> (the accent role's colour in the theme).
    /// </summary>
    public static IEnumerable<string> Findings(FamilyFact fact, bool oneReadout, IReadOnlyDictionary<string, string> palette, string accent)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(palette);

        if (fact.Part is FamilyPartType.Text or FamilyPartType.Control && fact.FontSize > 0)
        {
            if (!FamilyType.IsSize(fact.FontSize, oneReadout))
            {
                yield return $"text-size {N(fact.FontSize)}";
            }
            else if (fact.Part == FamilyPartType.Text && fact.LineHeight > 0 && Math.Abs(fact.LineHeight - FamilyType.LineOf(fact.FontSize)) > 0.01)
            {
                yield return $"text-line {N(fact.LineHeight)} on {N(fact.FontSize)}";
            }

            if (fact.Weight is not (FamilyType.RegularWeight or FamilyType.StrongWeight))
            {
                yield return $"text-weight {fact.Weight}";
            }
        }

        if (fact.Part == FamilyPartType.Glyph && fact.FontSize > 0 && !FamilyShape.IsGlyph(fact.FontSize, oneReadout))
        {
            yield return $"glyph-size {N(fact.FontSize)}";
        }

        foreach (var (name, value) in fact.Lengths ?? [])
        {
            if (!FamilySpace.IsSpace(value))
            {
                yield return $"space {name} {N(value)}";
            }
        }

        foreach (var radius in (fact.Radii ?? []).Distinct())
        {
            if (!FamilyShape.IsRadius(radius, fact.Width, fact.Height))
            {
                yield return $"radius {N(radius)}";
            }
        }

        if (!FamilyShape.IsStroke(fact.Stroke))
        {
            yield return $"stroke {N(fact.Stroke)}";
        }

        foreach (var colour in fact.Colours ?? [])
        {
            if (colour.Data || IsClear(colour.Argb))
            {
                continue;
            }

            if (fact.Part is FamilyPartType.Text or FamilyPartType.Glyph && colour.Slot == "fg"
                && string.Equals(colour.Argb, accent, StringComparison.OrdinalIgnoreCase))
            {
                yield return $"accent-{(fact.Part == FamilyPartType.Text ? "text" : "glyph")}";
                continue;
            }

            if (!palette.ContainsKey(colour.Argb.ToUpperInvariant()))
            {
                yield return $"colour {colour.Slot} {colour.Argb.ToUpperInvariant()}";
            }
        }
    }

    /// <summary>
    /// A finding without its values ("TextBlock text-size 18" is "TextBlock text-size"): what a baseline
    /// lists, so a reading that moves a computed margin from 10.06 to 10.46 is the same offence.
    /// </summary>
    public static string Rule(string finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var words = finding.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !(w.StartsWith('#') || w == "on" || double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out _)));
        return string.Join(' ', words);
    }

    /// <summary>A colour no one sees: no alpha.</summary>
    public static bool IsClear(string argb) => argb.Length == 9 && argb.StartsWith("#00", StringComparison.Ordinal);

    private static string N(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
