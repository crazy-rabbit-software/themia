using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>[S-6711] Which of a row's two texts is the information.</summary>
public enum RowKind
{
    /// <summary>A list's item (a task, a mail, a file, a contact, a feed's article): the name is read, a value (time, size) labels it.</summary>
    List,

    /// <summary>A fact (Humidity 71 %, Upload 38 KB/s): the value is read, the name labels it.</summary>
    Fact,
}

/// <summary>[S-6711] The colour roles of a row's parts.</summary>
public readonly record struct RowRoles(ColourRole Name, ColourRole Value, ColourRole Lead, ColourRole Second, ColourRole Third);

/// <summary>
/// [S-6711] The Row block: on one line, [lead: a 16 glyph, a 32 avatar, app or file icon, a check]
/// 8 [name, Body, at the widget's one text edge] 12 [an optional inline meter] 12 [value, Body,
/// end-aligned, tabular]; a second line in Caption secondary, a third in Caption tertiary; 32, 48 or
/// 64 tall at every tier. No dividers; hover and selection through the item layer.
/// <para>
/// One rule for colour: what is read is primary. A fact row's value is primary and its name
/// secondary; a list row's name is primary and its value secondary. A widget's rows all lead or
/// none do, so every name starts at one x: 40 from the card's edge with 16 glyphs, 16 without.
/// </para>
/// </summary>
public static class RowRules
{
    /// <summary>The roles of a row of <paramref name="kind"/>; a row of a part that is missing on this PC is all disabled ([S-1940]).</summary>
    public static RowRoles Roles(RowKind kind, bool available = true)
    {
        if (!available)
        {
            return new RowRoles(ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled);
        }

        return kind == RowKind.Fact
            ? new RowRoles(ColourRole.TextSecondary, ColourRole.TextPrimary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextTertiary)
            : new RowRoles(ColourRole.TextPrimary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextTertiary);
    }

    /// <summary>
    /// [S-6712] How many lines a row may show in <paramref name="area"/>, at most <paramref name="wanted"/>
    /// (1 to 3): one while the area is compact either way (XS, S, Wide), two from regular both ways
    /// (M, Tall), three from expanded both ways (L, XL). The pitch follows the lines, never the tier.
    /// </summary>
    public static int Lines(WidgetArea area, int wanted)
    {
        var allowed = area.Least switch
        {
            SizeClass.Compact => 1,
            SizeClass.Regular => 2,
            _ => 3,
        };

        return Math.Clamp(Math.Min(wanted, allowed), 1, 3);
    }

    /// <summary>[S-6712] How many whole rows of <paramref name="lines"/> lines fit <paramref name="height"/>; never part of one.</summary>
    public static int Fit(double height, int lines)
        => double.IsFinite(height) && height > 0 ? (int)Math.Floor((height + 0.01) / FamilyShape.RowHeight(lines)) : 0;

    /// <summary>
    /// [S-6711] Where a row's name starts inside the row's own inset: after a 16 lead and its gap, after a
    /// 32 lead and its gap, or at the start.
    /// </summary>
    public static double NameStart(double lead) => lead > 0 ? lead + FamilySpace.GlyphGap : 0;

    /// <summary>
    /// [S-6713] A cell (a grid of items, as Shortcuts' or a folder's icons): the lead over a Caption
    /// name, 12 between cells. Its icon is 32, or 48 from a width of 400 (the expanded class).
    /// </summary>
    public static double CellIcon(WidgetArea area) => area.WidthClass >= SizeClass.Expanded ? FamilyShape.IconHuge : FamilyShape.IconLarge;

    /// <summary>[S-6713] A cell's height: its icon, 4, the name's Caption line.</summary>
    public static double CellHeight(double icon, bool named)
        => icon + (named ? FamilySpace.S1 + FamilyType.Caption.LineHeight : 0);
}
