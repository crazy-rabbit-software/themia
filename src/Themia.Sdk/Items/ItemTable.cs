using Themia.Sdk.Kit;

namespace Themia.Sdk.Items;

/// <summary>[S-16956] One column of a table: its part, where it starts in the row's inner width and how wide it is.</summary>
public readonly record struct ItemColumn(ItemPart Part, double X, double Width)
{
    public double Right => X + Width;
}

/// <summary>
/// [S-16945] [S-16956] [S-16590] A table's columns (owner, 2026-11-23: "there should also be a base style where this data is shown as a
/// table and not the list"; "the current weather forecast row view will be the new table view"): each part shown a column in
/// the parts' order, every row's the same, so a bar starts and ends at the same x on every row ("the temperature bars should
/// be aligned to each other"). The lead its glyph's 16, every text column its widest text, the bar what the row leaves and
/// never under its floor (a share of the row); with no bar the title takes what is left, so the end stands on the row's end.
/// Where the texts do not fit, they give way together, each in proportion to its width (cut with its ellipsis). The
/// family's column gap between two columns, the glyph gap after the lead. Pure.
/// </summary>
public static class ItemTable
{
    /// <summary>[S-725] A bar's share of the row and its cap: its floor where it shares the row with texts.</summary>
    public const double BarShare = 0.32, BarMost = 140;

    /// <summary>[S-12403] The bar's floor in a row <paramref name="inner"/> wide: its share, at most <see cref="BarMost"/>.</summary>
    public static double BarFloor(double inner) => Math.Min(BarMost, Math.Floor(Math.Max(0, inner) * BarShare));

    /// <summary>
    /// [S-16956] Lay <paramref name="parts"/> (each with its widest content, the lead and the bar's ignored) across a row
    /// <paramref name="inner"/> wide (inside its side insets).
    /// </summary>
    public static IReadOnlyList<ItemColumn> Columns(IReadOnlyList<(ItemPart Part, double Widest)> parts, double inner, double? barFloor = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        inner = double.IsFinite(inner) ? Math.Max(0, inner) : 0;
        var floor = barFloor ?? BarFloor(inner);
        var widths = new double[parts.Count];
        var gaps = 0.0;
        for (var i = 0; i < parts.Count; i++)
        {
            var (part, widest) = parts[i];
            widths[i] = part switch
            {
                ItemPart.Lead => FamilyShape.Icon,
                ItemPart.Bar => 0,
                _ => Math.Ceiling(double.IsFinite(widest) ? Math.Max(0, widest) : 0),
            };
            if (i > 0)
            {
                gaps += parts[i - 1].Part == ItemPart.Lead ? FamilySpace.GlyphGap : FamilySpace.ColumnGap;
            }
        }

        var bar = Index(parts, ItemPart.Bar);
        var rest = inner - gaps - widths.Sum();
        if (bar >= 0)
        {
            widths[bar] = Math.Max(floor, Math.Floor(rest));
            rest = inner - gaps - widths.Sum();
        }
        else if (Index(parts, ItemPart.Title) is var title and >= 0 && rest > 0)
        {
            widths[title] += rest;
            rest = 0;
        }

        // The texts give way where the row cannot hold them, each in proportion to its width, so every one stays (cut).
        if (rest < 0)
        {
            var texts = Enumerable.Range(0, parts.Count).Where(i => parts[i].Part is ItemPart.Title or ItemPart.Subtitle or ItemPart.Note).ToList();
            var room = texts.Sum(i => widths[i]);
            if (room > 0)
            {
                var keep = Math.Max(0, room + rest) / room;
                foreach (var i in texts)
                {
                    widths[i] = Math.Floor(widths[i] * keep);
                }
            }
        }

        var columns = new List<ItemColumn>(parts.Count);
        var x = 0.0;
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                x += parts[i - 1].Part == ItemPart.Lead ? FamilySpace.GlyphGap : FamilySpace.ColumnGap;
            }

            columns.Add(new ItemColumn(parts[i].Part, x, widths[i]));
            x += widths[i];
        }

        return columns;
    }

    private static int Index(IReadOnlyList<(ItemPart Part, double Widest)> parts, ItemPart part)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Part == part)
            {
                return i;
            }
        }

        return -1;
    }
}
