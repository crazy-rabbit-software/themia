using Themia.Core.Geometry;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5591] Where an item that is about to arrive should land: the view shown on the display it was
/// dropped (or pasted, or made with New ›) on, and the point there, as fractions of that display
/// (none: the next free cell of that view, a paste with no press point).
/// <paramref name="Names"/> are the file names the arrival is expected under (a drop's files); empty
/// means "whatever arrives next" (a paste or New › from the background menu, whose names the shell
/// chooses). [S-5552] <paramref name="Renames"/>: the command was one of New â€º's, so what it made
/// opens its rename box once it lands, as Explorer's New â€º does.
/// </summary>
public sealed record DesktopLandingHint(string ViewId, double? X, double? Y, IReadOnlyList<string> Names, bool Renames = false);

/// <summary>
/// [S-5591] Where one arrival lands: the view, and the point its cell is looked for from (null: the next
/// free cell). [S-5552] <paramref name="Renames"/>: it is what New â€º made, to be renamed in place.
/// </summary>
public readonly record struct DesktopLandingSpot(string ViewId, double? X, double? Y, bool Renames = false);

/// <summary>
/// [S-5591] The landings expected, as pure rules: a drop on the desktop says which names it brings and
/// where; the reconcile that sees them arrive asks here where each goes. A hint is used up by the
/// arrivals it names, or (with no names) by the next reconcile that has arrivals at all; a newer hint
/// replaces an older one, so nothing waits on a clock and no stale hint outlives the next gesture.
/// </summary>
public sealed class DesktopLandings
{
    private DesktopLandingHint? _hint;
    private HashSet<string> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The hint waiting, or null.</summary>
    public DesktopLandingHint? Pending => _hint;

    /// <summary>A gesture expects arrivals: it replaces any earlier hint.</summary>
    public void Expect(DesktopLandingHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);
        _hint = hint;
        _waiting = new HashSet<string>(hint.Names.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [S-5591] A new gesture began (a press on the desktop): a hint that names nothing (a paste's, New ›'s)
    /// is dropped, so a command that brought nothing (Display settings) does not land a later download
    /// at its point; a drop's named hint stays until its files arrive.
    /// </summary>
    public void ClearOpen()
    {
        if (_hint is { Names.Count: 0 })
        {
            Clear();
        }
    }

    /// <summary>Forget the hint (the choice changed, the view went).</summary>
    public void Clear()
    {
        _hint = null;
        _waiting.Clear();
    }

    /// <summary>
    /// Where each of <paramref name="arrivals"/> (one reconcile's, in its order) lands: the hint's view
    /// and point for the ones it names (or for all of them, a hint with no names), null for the rest
    /// (the primary display's next free cell, [S-5520]). The hint is used up as described above.
    /// </summary>
    public IReadOnlyList<DesktopLandingSpot?> Place(IReadOnlyList<DesktopItem> arrivals)
    {
        ArgumentNullException.ThrowIfNull(arrivals);
        var spots = new DesktopLandingSpot?[arrivals.Count];
        if (_hint is not { } hint || arrivals.Count == 0)
        {
            return spots;
        }

        // [S-5552] New â€º makes one item: the first arrival it lands is the one renamed.
        var any = hint.Names.Count == 0;
        var renames = hint.Renames;
        for (var i = 0; i < arrivals.Count; i++)
        {
            var name = DesktopRename.FileName(arrivals[i].Key);
            if (any || _waiting.Remove(name))
            {
                spots[i] = new DesktopLandingSpot(hint.ViewId, hint.X, hint.Y, renames);
                renames = false;
            }
        }

        if (any || _waiting.Count == 0)
        {
            Clear();
        }

        return spots;
    }
}

/// <summary>[S-5591] [S-5546] The desktop's cells under a point, for a drop and for a move of item widgets.</summary>
public static class DesktopCells
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// The cell of <paramref name="cell"/>'s size under the point (<paramref name="x"/>, <paramref name="y"/>),
    /// on the grid that starts at <paramref name="area"/>'s top-left corner, kept whole inside the area;
    /// null when no whole cell fits there.
    /// </summary>
    public static FracRect? At(double x, double y, FracRect cell, FracRect area)
    {
        if (cell.W <= 0 || cell.H <= 0 || area.W + Epsilon < cell.W || area.H + Epsilon < cell.H)
        {
            return null;
        }

        var columns = Math.Max(1, (int)Math.Floor((area.W + Epsilon) / cell.W));
        var rows = Math.Max(1, (int)Math.Floor((area.H + Epsilon) / cell.H));
        var column = Math.Clamp((int)Math.Floor((x - area.X) / cell.W), 0, columns - 1);
        var row = Math.Clamp((int)Math.Floor((y - area.Y) / cell.H), 0, rows - 1);
        return new FracRect(area.X + (column * cell.W), area.Y + (row * cell.H), cell.W, cell.H);
    }

    /// <summary>
    /// [S-5591] Where an arrival dropped at a point lands: the cell under the point when it is free,
    /// else the next free cell after it down its column and then the next columns, else the first free
    /// cell anywhere (<see cref="DesktopGrid.NextFree"/>); null when the view has none.
    /// </summary>
    public static FracRect? FreeFrom(IReadOnlyList<FracRect> occupied, double x, double y, FracRect cell, FracRect area)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        if (At(x, y, cell, area) is not { } start)
        {
            return DesktopGrid.NextFree(occupied, cell, area);
        }

        for (var cx = start.X; cx + cell.W <= area.Right + Epsilon; cx += cell.W)
        {
            for (var cy = cx == start.X ? start.Y : area.Y; cy + cell.H <= area.Bottom + Epsilon; cy += cell.H)
            {
                var candidate = new FracRect(cx, cy, cell.W, cell.H);
                if (!occupied.Any(taken => Overlaps(taken, candidate)))
                {
                    return candidate;
                }
            }
        }

        return DesktopGrid.NextFree(occupied, cell, area);
    }

    /// <summary>
    /// [S-5546] Item widgets dragged inside the desktop: the held one's cell goes under the drop point
    /// and every other keeps its offset from it in whole cells, as a dragged group of icons keeps its
    /// shape on the grid; one that would leave the area or cover a widget that stays takes the next
    /// free cell from where it would have gone. Returns the new rect per moved widget's index (null:
    /// no room, it stays where it was). <paramref name="others"/> are the rects of the widgets that do
    /// not move.
    /// </summary>
    public static IReadOnlyList<FracRect?> Move(IReadOnlyList<FracRect> moving, int held, double x, double y, IReadOnlyList<FracRect> others, FracRect cell, FracRect area)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(others);
        var result = new FracRect?[moving.Count];
        if (held < 0 || held >= moving.Count || At(x, y, cell, area) is not { } target)
        {
            return result;
        }

        var occupied = new List<FracRect>(others);
        var origin = moving[held];

        // The held one first, so it gets the cell under the pointer.
        foreach (var i in Enumerable.Range(0, moving.Count).OrderBy(i => i == held ? 0 : 1))
        {
            var dx = Math.Round((moving[i].X - origin.X) / cell.W) * cell.W;
            var dy = Math.Round((moving[i].Y - origin.Y) / cell.H) * cell.H;
            var wanted = new FracRect(target.X + dx, target.Y + dy, moving[i].W, moving[i].H);
            var inside = wanted.X >= area.X - Epsilon && wanted.Y >= area.Y - Epsilon
                && wanted.Right <= area.Right + Epsilon && wanted.Bottom <= area.Bottom + Epsilon;
            var spot = inside && !occupied.Any(taken => Overlaps(taken, wanted))
                ? wanted
                : FreeFrom(occupied, wanted.X + (cell.W / 2), wanted.Y + (cell.H / 2), new FracRect(0, 0, moving[i].W, moving[i].H), area);

            if (spot is { } placed)
            {
                result[i] = placed;
                occupied.Add(placed);
            }
        }

        return result;
    }

    private static bool Overlaps(FracRect a, FracRect b)
        => a.X < b.Right - Epsilon && b.X < a.Right - Epsilon && a.Y < b.Bottom - Epsilon && b.Y < a.Bottom - Epsilon;
}

/// <summary>
/// [S-5547] Explorer's view settings the desktop follows: hidden items and protected operating-system
/// files, as the enumeration flags of the desktop's namespace (the shown names follow "File name
/// extensions" by themselves: the shell's display names do).
/// </summary>
public static class DesktopViewSettings
{
    /// <summary>SHCONTF_FOLDERS | SHCONTF_NONFOLDERS.</summary>
    public const uint Always = 0x20 | 0x40;

    /// <summary>SHCONTF_INCLUDEHIDDEN.</summary>
    public const uint IncludeHidden = 0x80;

    /// <summary>SHCONTF_INCLUDESUPERHIDDEN.</summary>
    public const uint IncludeSuperHidden = 0x10000;

    /// <summary>
    /// The flags for Explorer's settings: hidden items when "Hidden items" is on, and protected system
    /// files only when "Hide protected operating system files" is off too (Explorer shows them only then).
    /// </summary>
    public static uint EnumFlags(bool showHidden, bool showSuperHidden)
        => Always | (showHidden ? IncludeHidden : 0) | (showHidden && showSuperHidden ? IncludeSuperHidden : 0);
}
