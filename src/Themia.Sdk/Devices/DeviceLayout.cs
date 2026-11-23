using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Devices;

/// <summary>[S-16633] Where one device item stands in its list, in DIPs from the card's top-left edge.</summary>
public readonly record struct DeviceSlot(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-16633] A device list laid in its card: <see cref="Across"/> (side by side) or down, the items' one fixed
/// <see cref="Pitch"/> along that axis with <see cref="Gap"/> between two, and the slot of every item the card reaches
/// (the last of them cut by the card's edge where it does not hold it whole).
/// </summary>
public sealed record DeviceLay(bool Across, double Pitch, double Gap, IReadOnlyList<DeviceSlot> Slots);

/// <summary>
/// [S-16633] [S-16634] The device item's one size and how a list of them is laid (owner, 2026-11-20: "the widget items should
/// actually have a fixed size ... the last item will be cutoff to some extent then"; 2026-11-23: "optionally it should be
/// possible to add a block gap into the widget so that they are dymanic but it looks like different widgets"). An item is
/// the Drive widget's bar face with its name: the inset, the reading's line (Big28), the secondary line (Caption), 8, the
/// bar's track, the inset; 88, in whole grid cells its one height (90 on the default grid). Down (a card of more than one
/// item's height, or too narrow for two side by side): every item the card's whole width, from the card's top edge,
/// touching, as many as the card reaches, the last cut by its foot. Across (a card one item tall and wide enough for two):
/// every item its fixed width from the card's left edge, the last cut by its right edge. The block gap puts one grid cell
/// between two items, so each stands as a card of its own. Pure.
/// </summary>
public static class DeviceLayout
{
    /// <summary>[S-16633] An item's natural height: the inset, the reading (Big28), its line (Caption), 8, the bar's track, the inset.</summary>
    public static double Natural => FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Caption.LineHeight + FamilySpace.S2 + FamilyShape.Track + FamilySpace.Inset;

    /// <summary>[S-16633] An item's natural width side by side: its icon, the reading held to "100%" and a name worth reading.</summary>
    public const double AcrossWidth = 240;

    /// <summary>[S-16634] A list's least width: an item's icon, reading and the start of its name.</summary>
    public const double LeastWidth = 220;

    /// <summary>[S-16634] A new list's width.</summary>
    public const double NaturalWidth = 300;

    /// <summary>[S-16634] The widest a list of items down grows (across it takes every item's width).</summary>
    public const double MostWidth = 520;

    /// <summary>[S-16634] A new list's items.</summary>
    public const int DefaultRows = 3;

    /// <summary>[S-16633] An item's one height on <paramref name="grid"/>: its natural in whole cells.</summary>
    public static double Height(ItemGrid grid) => ItemSize.Optimal(Natural, grid);

    /// <summary>[S-16633] An item's one width side by side on <paramref name="grid"/>: its natural in whole cells.</summary>
    public static double Width(ItemGrid grid) => ItemSize.Optimal(AcrossWidth, grid);

    /// <summary>[S-16634] The block gap: one grid cell between two items where it is on, none where it is off.</summary>
    public static double GapOf(bool gap, ItemGrid grid) => gap ? grid.Pitch : 0;

    /// <summary>
    /// [S-16633] Whether <paramref name="count"/> items stand side by side in a card <paramref name="width"/> ×
    /// <paramref name="height"/> (from its top edge): the card holds one item's height but not two, and two items' width.
    /// </summary>
    public static bool Across(int count, double width, double height, bool gap, ItemGrid grid)
    {
        var g = GapOf(gap, grid);
        return count > 1
            && double.IsFinite(width) && double.IsFinite(height)
            && height + LayoutMath.Tolerance < (2 * Height(grid)) + g
            && width + LayoutMath.Tolerance >= (2 * Width(grid)) + g;
    }

    /// <summary>
    /// [S-16633] Lay <paramref name="count"/> items in a card <paramref name="width"/> × <paramref name="height"/> (the room
    /// from the card's top edge to its foot): each its one fixed size, from the card's start, the gap between two, every
    /// one the card reaches (at least one), the last cut by the card's edge. Every edge on a whole device pixel.
    /// </summary>
    public static DeviceLay Lay(int count, double width, double height, bool gap, ItemGrid grid)
    {
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        height = double.IsFinite(height) ? Math.Max(0, height) : 0;
        var g = GapOf(gap, grid);
        var across = Across(count, width, height, gap, grid);
        var pitch = across ? Width(grid) : Height(grid);
        var room = across ? width : height;
        var slots = new List<DeviceSlot>(Math.Max(0, count));
        for (var i = 0; i < count; i++)
        {
            var start = ItemSize.OnPixels(i * (pitch + g), grid.Raster);
            if (i > 0 && start >= room - LayoutMath.Tolerance)
            {
                break;
            }

            var length = ItemSize.OnPixels((i * (pitch + g)) + pitch, grid.Raster) - start;
            slots.Add(across ? new DeviceSlot(start, 0, length, Height(grid)) : new DeviceSlot(0, start, width, length));
        }

        return new DeviceLay(across, pitch, g, slots);
    }

    /// <summary>
    /// [S-16637] [S-15749] [S-15759] One device's face's sizing (a Drive, a USB stick, a network drive, a Bluetooth device):
    /// one height, the item's (88), 160..320 wide, a new one 200; scaled down as one block to where its line (Caption) is
    /// still read at 9 (WR-14) in the card's inset, which does not scale, and never narrower than its widest line beside
    /// its icon at that scale (<paramref name="widestLine"/>: the line's widest form at its own size, in DIPs).
    /// </summary>
    public static WidgetSizing Face(double widestLine)
    {
        var sizing = WidgetSizing.Range(new(160, Natural), new(200, Natural), new(320, Natural));
        var k = ScaleFloor.Least(FamilyType.Caption.FontSize);
        var natural = sizing.Natural;
        var scaled = sizing.ScalesDownTo(new SizeDip(
            ((natural.Width - (2 * FamilySpace.Inset)) * k) + (2 * FamilySpace.Inset),
            ((natural.Height - FamilySpace.ContentTop - FamilySpace.Inset) * k) + FamilySpace.ContentTop + FamilySpace.Inset));
        var least = Math.Ceiling(((FamilyType.Big28.LineHeight + FamilySpace.GlyphGap + widestLine) * k) + (2 * FamilySpace.Inset));
        var min = new SizeDip(Math.Max(scaled.Min.Width, least), scaled.Min.Height);
        return scaled with
        {
            Min = min,
            Natural = new SizeDip(Math.Max(scaled.Natural.Width, min.Width), scaled.Natural.Height),
            Max = new SizeDip(Math.Max(scaled.Max.Width, min.Width), scaled.Max.Height),
        };
    }

    /// <summary>[S-16637] The widest of <paramref name="lines"/> on the item's line (Caption), estimated in DIPs.</summary>
    public static double WidestLine(IEnumerable<string> lines) => (lines ?? []).Select(HeroFit.Ems).DefaultIfEmpty(0).Max() * FamilyType.Caption.FontSize;

    /// <summary>
    /// [S-16634] A list's sizing: <paramref name="rows"/> items down at its natural (the gap between two where it is on),
    /// one at its least, twenty at its most; 220..520 wide, a new one 300. Every cell between (a resize steps one cell).
    /// </summary>
    public static WidgetSizing Sizing(int rows, bool gap)
    {
        var grid = ItemGrid.Default();
        var h = Height(grid);
        var g = GapOf(gap, grid);
        double Tall(int n) => (n * h) + (Math.Max(0, n - 1) * g);
        return WidgetSizing.Range(
            new(LeastWidth, h),
            new(NaturalWidth, Tall(KitSizing.ClampRows(rows))),
            new(MostWidth, Tall(KitSizing.MaxRows))) with { Room = StretchRoom.List };
    }
}
