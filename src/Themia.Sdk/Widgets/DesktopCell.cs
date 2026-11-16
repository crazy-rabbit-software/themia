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
    public const double Beside = 28;

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

    /// <summary>[S-5593] The icon's side at <paramref name="size"/>: Explorer's 96, 48 and 32.</summary>
    public static double Icon(DesktopIconSize size) => size switch
    {
        DesktopIconSize.Large => LargeIcon,
        DesktopIconSize.Small => FamilyShape.IconLarge,
        _ => FamilyShape.IconHuge,
    };

    /// <summary>[S-5601] [S-5593] The cell's width at <paramref name="size"/>: 76 at medium (Explorer's own), 124 at large, 60 at small.</summary>
    public static double Width(DesktopIconSize size) => Icon(size) + Beside;

    /// <summary>[S-11204] The name's lines: 1 or 2, a new one 2.</summary>
    public static int ClampLines(int lines) => Math.Clamp(lines, 1, 2);

    /// <summary>[S-11204] What the cell draws at <paramref name="size"/> for <paramref name="lines"/> name lines: the icon and them.</summary>
    public static DesktopCellFit Fit(DesktopIconSize size, int lines) => new(Icon(size), ClampLines(lines));

    /// <summary>
    /// [S-11204] [S-5593] The one cell at <paramref name="size"/> for <paramref name="lines"/> name lines.
    /// With two: 76 × 100 at medium, 124 × 148 at large, 60 × 84 at small; with one, 16 less tall.
    /// </summary>
    public static SizeDip Size(DesktopIconSize size, int lines)
        => new(Width(size), HighlightTop + HighlightHeight(size, ClampLines(lines) * FamilyType.CaptionLine) + Below);

    /// <summary>[S-5588] The icon grid's cell at <paramref name="size"/>: an icon with both name lines, whatever an icon's own lines are.</summary>
    public static SizeDip Grid(DesktopIconSize size) => Size(size, 2);

    /// <summary>[S-5601] The highlight's height round an icon of <paramref name="size"/> whose name is <paramref name="nameHeight"/> tall: it hugs the icon and the lines the name really takes, as Explorer's does.</summary>
    public static double HighlightHeight(DesktopIconSize size, double nameHeight) => IconTop + Icon(size) + Gap + Math.Max(0, nameHeight) + NameBottom;

    /// <summary>The width a name wraps at in a cell of <paramref name="size"/> drawn <paramref name="actualWidth"/> wide (not measured yet: the cell's own).</summary>
    public static double NameWidth(DesktopIconSize size, double actualWidth)
        => Math.Max(MinNameWidth, (actualWidth > 0 ? actualWidth : Width(size)) - (2 * SideInset));

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
