using Themia.Sdk.Kit;

namespace Themia.Sdk;

/// <summary>[S-5601] What a desktop cell draws: the icon's size and how many lines of its name.</summary>
public readonly record struct DesktopCellFit(double Icon, int Lines);

/// <summary>
/// [S-5593] The desktop's icon size, Explorer's View › Large, Medium and Small icons: one setting for
/// the whole desktop, never an icon's own. Medium is Explorer's default and a new state's.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DesktopIconSize>))]
public enum DesktopIconSize
{
    /// <summary>Medium icons: 48 DIPs, Explorer's default.</summary>
    Medium,

    /// <summary>Large icons: 96 DIPs.</summary>
    Large,

    /// <summary>Small icons: 32 DIPs.</summary>
    Small,
}

/// <summary>
/// [S-13970] How the desktop lays out each icon and its name (owner, 2026-11-19: "desktop icons should not only have the
/// size settings as they have now, they should also have different types"): one setting for the whole desktop, every
/// view, every display and every fence, never an icon's own, beside the icon size. Text below is Explorer's own and a new
/// state's.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DesktopIconLayout>))]
public enum DesktopIconLayout
{
    /// <summary>The name under the icon, on its 1 or 2 lines: Explorer's icon view.</summary>
    Below,

    /// <summary>[S-13973] The name to the icon's right, on its 1 or 2 lines, in a wide short cell: Explorer's Tiles and List.</summary>
    Beside,

    /// <summary>[S-13974] The icon alone; its name shows while it is pointed at, keyboard-focused or renamed, drawn over its neighbours.</summary>
    IconOnly,
}

/// <summary>
/// [S-5601] [S-5593] [S-11204] The one cell of everything drawn as a desktop icon, **Explorer's own
/// desktop cell**. At medium icons it is measured on Windows 11 (lossless captures at 150 %,
/// 2026-11-14, in DIPs): the cells stand 76 × 100 apart; the highlight starts 3 under the cell's top and
/// is the cell's whole width; inside it the 48 icon stands 3 down, the name 2 under the icon on 16-DIP
/// lines, 1 under the last line, so a two-line name's highlight is 86 tall and the cell keeps 11 clear
/// under it. The name runs to 2 from each side. These are Explorer's numbers, not the family's spacing
/// tokens.
/// <para>
/// **Large (96) and Small (32) are derived, not measured**: Explorer's icon view keeps the room round
/// the icon and grows the cell by exactly what the icon grows (a list view's icon spacing is stated for
/// an icon size and moves with it), while the name stays 9-point text on two 16-DIP lines. So the cell
/// is the icon plus 28 across (76 − 48) and plus 52 down (100 − 48): 124 × 148 at Large, 60 × 84 at
/// Small, with every inset above unchanged. A capture of Explorer at those sizes should replace this
/// sentence.
/// </para>
/// <para>
/// [S-13971] Those are text below's (<see cref="DesktopIconLayout.Below"/>). Text beside and icons only have cells of
/// their own at each size, from the family's steps (<see cref="Size(DesktopIconSize, DesktopIconLayout, int)"/>); the
/// one-argument forms read the layout in force (<see cref="CurrentLayout"/>), as the size's readers read <see cref="Current"/>.
/// </para>
/// </summary>
public static class DesktopCell
{
    /// <summary>The cell's top edge to its highlight.</summary>
    public const double HighlightTop = 3;

    /// <summary>The highlight's top edge to the icon.</summary>
    public const double IconTop = 3;

    /// <summary>The icon to the name.</summary>
    public const double Gap = 2;

    /// <summary>The name's last line to the highlight's bottom edge.</summary>
    public const double NameBottom = 1;

    /// <summary>The highlight's bottom edge to the cell's (with every name line used): what keeps Explorer's 100 between rows at medium icons.</summary>
    public const double Below = 11;

    /// <summary>The cell's left and right edge to the name: Explorer's names run nearly the cell's width.</summary>
    public const double SideInset = 2;

    /// <summary>[S-5593] The cell's width less its icon, at every size: Explorer's 76 at medium less the 48 icon.</summary>
    public const double Across = 28;

    /// <summary>[S-13973] Text beside: the cell's edges to the icon and the name, all round (the family's step, no Explorer number).</summary>
    public const double BesideInset = FamilySpace.S1;

    /// <summary>[S-13973] Text beside: the icon to its name.</summary>
    public const double BesideGap = FamilySpace.S2;

    /// <summary>[S-13974] Icon only: the room round the icon on every side, so two cells' highlights never touch the icons.</summary>
    public const double IconOnlyInset = FamilySpace.S2;

    /// <summary>[S-13974] Icon only: the most lines the name shown on hover takes (the tip and a screen reader carry it whole).</summary>
    public const int HoverLines = 4;

    /// <summary>[S-5593] Explorer's large icon, which the family has no token for.</summary>
    public const double LargeIcon = 96;

    /// <summary>The narrowest a name is worth showing in.</summary>
    public const double MinNameWidth = FamilyShape.IconLarge;

    /// <summary>
    /// [S-5593] The size in force: the host applies the one setting (<see cref="Apply"/>) before anything
    /// is drawn and whenever it changes, as it does the regional formats, so the kind's sizing, the
    /// host's grid and every view read one value. Medium until applied.
    /// </summary>
    public static DesktopIconSize Current { get; private set; } = DesktopIconSize.Medium;

    /// <summary>
    /// [S-13970] The layout in force, put there as the size is (<see cref="Apply(DesktopIconLayout)"/>): every icon view,
    /// the kind's sizing, the host's grid and the fences read this one value. Text below until applied.
    /// </summary>
    public static DesktopIconLayout CurrentLayout { get; private set; } = DesktopIconLayout.Below;

    /// <summary>[S-5593] Put <paramref name="size"/> in force; an unknown value is Medium. Returns whether it changed.</summary>
    public static bool Apply(DesktopIconSize size)
    {
        var known = Enum.IsDefined(size) ? size : DesktopIconSize.Medium;
        if (known == Current)
        {
            return false;
        }

        Current = known;
        return true;
    }

    /// <summary>[S-13970] Put <paramref name="layout"/> in force; an unknown value is Text below. Returns whether it changed.</summary>
    public static bool Apply(DesktopIconLayout layout)
    {
        var known = Enum.IsDefined(layout) ? layout : DesktopIconLayout.Below;
        if (known == CurrentLayout)
        {
            return false;
        }

        CurrentLayout = known;
        return true;
    }

    /// <summary>[S-5593] The icon's side at <paramref name="size"/>: Explorer's 96, 48 and 32.</summary>
    public static double Icon(DesktopIconSize size) => size switch
    {
        DesktopIconSize.Large => LargeIcon,
        DesktopIconSize.Small => FamilyShape.IconLarge,
        _ => FamilyShape.IconHuge,
    };

    /// <summary>
    /// [S-5601] [S-5593] [S-13971] The cell's width at <paramref name="size"/> in <paramref name="layout"/>: text below 76 at
    /// medium (Explorer's own), 124 at large, 60 at small; text beside the icon, its name's room and the insets (200, 272,
    /// 160); icon only the icon and its room (64, 112, 48).
    /// </summary>
    public static double Width(DesktopIconSize size, DesktopIconLayout layout) => layout switch
    {
        DesktopIconLayout.Beside => BesideInset + Icon(size) + BesideGap + BesideName(size) + BesideInset,
        DesktopIconLayout.IconOnly => Icon(size) + (2 * IconOnlyInset),
        _ => Icon(size) + Across,
    };

    /// <summary>
    /// [S-13973] Text beside: the room the name has to the icon's right, about twenty characters of Caption at medium. A
    /// name of two words and more wraps on its second line, as below the icon.
    /// </summary>
    public static double BesideName(DesktopIconSize size) => size switch
    {
        DesktopIconSize.Large => 160,
        DesktopIconSize.Small => 112,
        _ => 136,
    };

    /// <summary>[S-11204] The name's lines: 1 or 2, a new one 2.</summary>
    public static int ClampLines(int lines) => Math.Clamp(lines, 1, 2);

    /// <summary>[S-11204] What the cell draws at <paramref name="size"/> for <paramref name="lines"/> name lines, in the layout in force.</summary>
    public static DesktopCellFit Fit(DesktopIconSize size, int lines) => Fit(size, CurrentLayout, lines);

    /// <summary>
    /// [S-11204] [S-13974] What the cell draws at <paramref name="size"/> in <paramref name="layout"/> for
    /// <paramref name="lines"/> name lines: the icon and them; in icon only the lines of the name shown on hover
    /// (<see cref="HoverLines"/>), which is never drawn in the cell.
    /// </summary>
    public static DesktopCellFit Fit(DesktopIconSize size, DesktopIconLayout layout, int lines)
        => new(Icon(size), layout == DesktopIconLayout.IconOnly ? HoverLines : ClampLines(lines));

    /// <summary>[S-11204] [S-5593] The one cell at <paramref name="size"/> for <paramref name="lines"/> name lines, in the layout in force.</summary>
    public static SizeDip Size(DesktopIconSize size, int lines) => Size(size, CurrentLayout, lines);

    /// <summary>
    /// [S-11204] [S-5593] [S-13971] The one cell at <paramref name="size"/> in <paramref name="layout"/> for
    /// <paramref name="lines"/> name lines. Text below, with two: 76 × 100 at medium, 124 × 148 at large, 60 × 84 at
    /// small; with one, 16 less tall. Text beside: as tall as the icon or the name's lines, whichever is taller, and the
    /// insets (56, 104, 40, with either). Icon only: square, the icon and its room, whatever the lines.
    /// </summary>
    public static SizeDip Size(DesktopIconSize size, DesktopIconLayout layout, int lines)
    {
        var name = ClampLines(lines) * FamilyType.CaptionLine;
        return layout switch
        {
            DesktopIconLayout.Beside => new(Width(size, layout), (2 * BesideInset) + Math.Max(Icon(size), name)),
            DesktopIconLayout.IconOnly => new(Width(size, layout), Icon(size) + (2 * IconOnlyInset)),
            _ => new(Width(size, layout), HighlightTop + HighlightHeight(size, name) + Below),
        };
    }

    /// <summary>[S-5588] The icon grid's cell at <paramref name="size"/> in the layout in force: an icon with both name lines, whatever an icon's own lines are.</summary>
    public static SizeDip Grid(DesktopIconSize size) => Grid(size, CurrentLayout);

    /// <summary>[S-5588] [S-13971] The icon grid's cell at <paramref name="size"/> in <paramref name="layout"/>: an icon with both name lines.</summary>
    public static SizeDip Grid(DesktopIconSize size, DesktopIconLayout layout) => Size(size, layout, 2);

    /// <summary>[S-5601] The room an icon of <paramref name="size"/> and a name <paramref name="nameHeight"/> tall take from the cell's top inset to under the name: what the cell's height counts.</summary>
    public static double HighlightHeight(DesktopIconSize size, double nameHeight) => IconTop + Icon(size) + Gap + Math.Max(0, nameHeight) + NameBottom;

    /// <summary>
    /// [S-5612] The hover, selected and highlighted fill of a desktop icon whose card is <paramref name="width"/> ×
    /// <paramref name="height"/> DIPs, in the card's own DIPs: the whole card box, the very rect its corner marks trace
    /// (its whole grid cells, [S-11875]), never a smaller mark inside it (owner, 2026-11-19: "the hover and highlight of
    /// a desktop icon should always be the same area that the widget itself has (where the corners are)").
    /// </summary>
    public static Themia.Core.Geometry.PixelRect Highlight(double width, double height)
        => new(0, 0, double.IsFinite(width) ? Math.Max(0, width) : 0, double.IsFinite(height) ? Math.Max(0, height) : 0);

    /// <summary>The width a name wraps at in a cell of <paramref name="size"/> drawn <paramref name="actualWidth"/> wide (not measured yet: the cell's own), in the layout in force.</summary>
    public static double NameWidth(DesktopIconSize size, double actualWidth) => NameWidth(size, CurrentLayout, actualWidth);

    /// <summary>
    /// [S-13971] The width a name wraps at in a cell of <paramref name="size"/> in <paramref name="layout"/> drawn
    /// <paramref name="actualWidth"/> wide (not measured yet: the cell's own): below the icon the cell's width less its
    /// sides; beside it what is right of the icon less the insets; in icon only the name shown on hover, which is not the
    /// cell's (as wide as text beside's name).
    /// </summary>
    public static double NameWidth(DesktopIconSize size, DesktopIconLayout layout, double actualWidth)
    {
        var width = actualWidth > 0 ? actualWidth : Width(size, layout);
        return layout switch
        {
            DesktopIconLayout.Beside => Math.Max(MinNameWidth, width - (2 * BesideInset) - Icon(size) - BesideGap),
            DesktopIconLayout.IconOnly => BesideName(size),
            _ => Math.Max(MinNameWidth, width - (2 * SideInset)),
        };
    }

    /// <summary>
    /// [S-13974] Whether a cell in <paramref name="layout"/> draws its name now: always below or beside the icon; in icon
    /// only while the icon is pointed at (<paramref name="hovered"/>), has the keyboard's focus (<paramref name="focused"/>,
    /// after keyboard use) or is being renamed (<paramref name="renaming"/>: the box stands where the name would).
    /// </summary>
    public static bool ShowsName(DesktopIconLayout layout, bool hovered, bool focused, bool renaming)
        => layout != DesktopIconLayout.IconOnly || hovered || focused || renaming;

    /// <summary>
    /// [S-13974] [S-13975] Whether the name (and its rename box) is drawn past the cell, over the neighbours and above every
    /// icon, as Windows' expanded label: icon only's, whose cell has no room for it.
    /// </summary>
    public static bool NameOverhangs(DesktopIconLayout layout) => layout == DesktopIconLayout.IconOnly;

    /// <summary>
    /// [S-5610] [S-13974] Whether the tip leads with the name in full: a name cut short as drawn, and every name in icon
    /// only, whose cell never draws one at rest.
    /// </summary>
    public static bool TipNames(DesktopIconLayout layout, bool nameCut) => nameCut || layout == DesktopIconLayout.IconOnly;

    /// <summary>
    /// [S-13974] Icon only: where the name shown on hover starts, from the top of a cell <paramref name="cellHeight"/> tall
    /// (not measured yet: the cell's own) with the icon centred in it: <see cref="Gap"/> under the icon, as below the icon,
    /// so it never covers the icon.
    /// </summary>
    public static double HoverNameTop(DesktopIconSize size, double cellHeight)
    {
        var height = cellHeight > 0 ? cellHeight : Width(size, DesktopIconLayout.IconOnly);
        return ((height - Icon(size)) / 2) + Icon(size) + Gap;
    }

    /// <summary>
    /// [S-5593] The size one notch of Ctrl + mouse wheel away from <paramref name="size"/>: up towards
    /// Large, down towards Small, staying at the ends. Explorer steps through more sizes; these are the
    /// menu's three.
    /// </summary>
    public static DesktopIconSize Step(DesktopIconSize size, int notches)
    {
        DesktopIconSize[] order = [DesktopIconSize.Small, DesktopIconSize.Medium, DesktopIconSize.Large];
        var at = Math.Max(0, Array.IndexOf(order, size));
        return order[Math.Clamp(at + Math.Sign(notches), 0, order.Length - 1)];
    }
}
