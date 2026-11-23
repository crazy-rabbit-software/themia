using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-14912] [S-16551] [S-16562] What a piece of the gallery asks of the bento on a display (<paramref name="screen"/>, at the
/// content scale new widgets get there, in a view <paramref name="viewWidth"/> cells wide), in whole cells of its grid: the
/// gallery's view and the tests ask the one rule.
/// </summary>
public sealed class GallerySizes(ScreenMetrics screen, double contentScale, int viewWidth)
{
    private readonly Dictionary<string, IReadOnlyList<int>> _groupWidths = new(StringComparer.Ordinal);

    /// <summary>[S-16551] How many times its natural length a piece is made at the most, each way.</summary>
    public const int Grown = 2;

    /// <summary>[S-16551] How small a share of its natural length a piece is made at the least, each way.</summary>
    public const double Least = 0.75;

    private PlacementSizes Sizes => new(contentScale);

    /// <summary>
    /// [S-16551] A widget its natural cells (in its style), the least and most its sizing takes (never more than
    /// <see cref="Grown"/> times its natural each way, nor less than <see cref="Least"/> of it) and the axes it may be another length on; [S-16562] a quick setting at
    /// its least that size alone; a ready-made group the cells it is laid at, wider only to the widths its common step lands
    /// on (<see cref="GroupWidths"/>), never taller; [S-16724] a square piece (<see cref="GalleryPiece.Square"/>) kept at its
    /// natural size (or its least) made square on the display's cells where its sizing takes that (<see cref="Squared"/>).
    /// </summary>
    public PieceSize Of(GalleryPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        var (cols, rows) = GridGeometry.GridDims(screen);

        if (piece.Group is { } group)
        {
            var laid = ReadyMadeGroups.Laid(group, screen, Sizes);
            var w = Math.Max(1, (int)Math.Round(laid.Max(r => r.Right) * cols));
            var h = Math.Max(1, (int)Math.Round(laid.Max(r => r.Bottom) * rows));
            return new PieceSize(w, h, w, h, true, false) { Widths = GroupWidths(group) };
        }

        var kind = piece.Pick ?? throw new ArgumentException("a piece is a widget or a group", nameof(piece));
        var (nw, nh) = Cells(kind, piece.Style);
        if (SizeFit.Limits(SizeFit.Of(kind, piece.Style), screen, contentScale) is not { } limits)
        {
            return new PieceSize(nw, nh, nw, nh, false, false);
        }

        var (minW, minH) = ((int)Math.Ceiling((limits.Width.Min * cols) - 1e-6), (int)Math.Ceiling((limits.Height.Min * rows) - 1e-6));
        var (maxW, maxH) = (Math.Max(minW, (int)Math.Floor((limits.Width.Max * cols) + 1e-6)), Math.Max(minH, (int)Math.Floor((limits.Height.Max * rows) + 1e-6)));
        if (piece.Square)
        {
            var (sw, sh) = Squared(piece.Least ? (minW, minH) : (nw, nh), (minW, minH), (maxW, maxH));
            return new PieceSize(sw, sh, sw, sh, false, false);
        }

        if (piece.Least)
        {
            return new PieceSize(minW, minH, minW, minH, false, false);
        }

        // Never more than twice its natural each way, nor under three quarters of it (its least where that is more): a
        // column's width is a piece's, never a giant's or a crumb's; a quick setting's least stays the smaller one shown.
        var (fewW, fewH) = (Math.Min(nw, Math.Max(minW, (int)Math.Ceiling(nw * Least))), Math.Min(nh, Math.Max(minH, (int)Math.Ceiling(nh * Least))));
        return new PieceSize(nw, nh, fewW, fewH, !limits.Width.Fixed, !limits.Height.Fixed) { MaxW = Math.Min(maxW, Grown * nw), MaxH = Math.Min(maxH, Grown * nh) };
    }

    /// <summary>
    /// [S-16724] <paramref name="cells"/> made square on this display's cells (as many DIPs down as across: the longer side
    /// kept, the other made as long, whole cells the nearest), held to <paramref name="least"/> and <paramref name="most"/>: a
    /// piece whose sizing keeps one side (a switch's height) keeps it.
    /// </summary>
    public (int W, int H) Squared((int W, int H) cells, (int W, int H) least, (int W, int H) most)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (pw, ph) = (screen.Width / cols, screen.Height / rows);
        var side = Math.Max(cells.W * pw, cells.H * ph);
        var w = Math.Clamp((int)Math.Round(side / pw), Math.Max(1, least.W), Math.Max(least.W, most.W));
        var h = Math.Clamp((int)Math.Round(side / ph), Math.Max(1, least.H), Math.Max(least.H, most.H));
        return (w, h);
    }

    /// <summary>[S-16721] The rows a widget's title takes over its card (<see cref="G.Title"/> in whole rows of this display's cells).</summary>
    public int TitleRows => Rows(G.Title);

    /// <summary>[S-16725] The rows a section's heading takes in Quick settings (<see cref="G.Heading"/>).</summary>
    public int HeadingRows => Rows(G.Heading);

    private int Rows(double dips)
    {
        var (_, rows) = GridGeometry.GridDims(screen);
        return Math.Max(1, (int)Math.Ceiling((dips / (screen.Height / rows)) - 1e-6));
    }

    /// <summary>[S-14912] A kind's natural cells in a style: what placing it new makes (<see cref="PlacementPayload.NewSize"/>).</summary>
    public (int W, int H) Cells(WidgetPick kind, int? style)
    {
        var natural = SizeFit.Of(kind, style).Natural;
        var box = PlacementPayload.NewSize(kind, natural.Width, natural.Height, screen, Sizes);
        var grid = GridGeometry.GridDims(screen);
        return (grid.Col(box.W), grid.Row(box.H));
    }

    /// <summary>
    /// [S-16403] The widths a ready-made group can be made: its own, and each its common step lands on when it is made wider
    /// as one block (<see cref="BlockResize"/>, [S-2460]: every member in its own sizing, every edge on a cell) without
    /// growing taller, up to the view's width.
    /// </summary>
    public IReadOnlyList<int> GroupWidths(ReadyMadeGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (_groupWidths.TryGetValue(group.Id, out var known))
        {
            return known;
        }

        var (cols, rows) = GridGeometry.GridDims(screen);
        var laid = ReadyMadeGroups.Laid(group, screen, Sizes);
        var (w, h) = ((int)Math.Round(laid.Max(r => r.Right) * cols), (int)Math.Round(laid.Max(r => r.Bottom) * rows));
        var widths = new SortedSet<int> { w };
        var most = Math.Min(viewWidth, Grown * w);
        if (ReadyMadeGroups.Build(group, screen) is { } made && made.Count == laid.Count)
        {
            SizeLimits?[] limits = [.. made.Select(m => SizeFit.Limits(m, screen, contentScale))];
            for (var want = w + 1; want <= most; want++)
            {
                if (Widened(laid, limits, want) is { } grown && grown.W <= most && grown.H == h)
                {
                    widths.Add(grown.W);
                }
            }
        }

        IReadOnlyList<int> all = [.. widths];
        _groupWidths[group.Id] = all;
        return all;
    }

    /// <summary>[S-2460] A ready-made group's widgets laid as one block made <paramref name="want"/> cells wide, where its common step lands; null where it cannot be.</summary>
    public IReadOnlyList<FracRect>? Widen(ReadyMadeGroup group, int want)
    {
        ArgumentNullException.ThrowIfNull(group);
        var laid = ReadyMadeGroups.Laid(group, screen, Sizes);
        if (ReadyMadeGroups.Build(group, screen) is not { } made || made.Count != laid.Count)
        {
            return null;
        }

        SizeLimits?[] limits = [.. made.Select(m => SizeFit.Limits(m, screen, contentScale))];
        var (cols, _) = GridGeometry.GridDims(screen);
        var grown = BlockResize.Resize(laid, limits, ResizeHandle.Right, (want / (double)cols) - laid.Max(r => r.Right), 0, screen);
        return grown.Count == laid.Count && grown.Min(r => r.X) > -1e-9 && grown.Min(r => r.Y) > -1e-9 ? grown : null;
    }

    private (int W, int H)? Widened(IReadOnlyList<FracRect> laid, SizeLimits?[] limits, int want)
    {
        var (cols, rows) = GridGeometry.GridDims(screen);
        var grown = BlockResize.Resize(laid, limits, ResizeHandle.Right, (want / (double)cols) - laid.Max(r => r.Right), 0, screen);
        if (grown.Count != laid.Count || grown.Min(r => r.X) < -1e-9 || grown.Min(r => r.Y) < -1e-9)
        {
            return null;
        }

        return ((int)Math.Round(grown.Max(r => r.Right) * cols), (int)Math.Round(grown.Max(r => r.Bottom) * rows));
    }
}
