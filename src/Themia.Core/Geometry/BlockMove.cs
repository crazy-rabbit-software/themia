namespace Themia.Core.Geometry;

/// <summary>
/// [S-11890] [S-11891] A block of cards dragged by the pointer (a selection in edit mode, the locked desktop's move):
/// one translation, <see cref="Landing"/>, the move snapped to the grid as a block (<see cref="BlockGeometry.Move"/>,
/// [S-1584]). The cards are drawn there while they are dragged and land there: they always stick to the grid. A move
/// never keeps widgets apart: it may land over another (owner, 2026-11-21: "keep widgets from overlapping should not be a
/// selectable option anymore"; [S-11959] retired).
/// <para>
/// <see cref="Step"/> says whether it changed (another cell), so the surface touches the cards, the local grid and the
/// border marks only then. Pure; the surface holds one per move.
/// </para>
/// <para>
/// [S-13041] With <c>cell</c> the held card's landing is a cell of what it lands in (a fence's body on the desktop
/// icon grid, <c>DesktopFences.DropCell</c>): the block follows the held card there. The one drop computation for the
/// free desktop and a fence: there is no other.
/// </para>
/// </summary>
public sealed class BlockMove
{
    private readonly ScreenMetrics _screen;
    private readonly int _held;
    private readonly Func<FracRect, FracRect>? _cell;

    public BlockMove(IReadOnlyList<FracRect> start, ScreenMetrics screen, int held = -1, Func<FracRect, FracRect>? cell = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        Start = start;
        _screen = screen;
        _held = held;
        _cell = cell;
    }

    /// <summary>The rects the moving cards started at.</summary>
    public IReadOnlyList<FracRect> Start { get; }

    /// <summary>[S-1584] [S-11891] The translation the cards are drawn at and land at: on the grid.</summary>
    public BlockGeometry.Delta Landing { get; private set; }

    /// <summary>
    /// The pointer is (<paramref name="dxFrac"/>, <paramref name="dyFrac"/>) from where it pressed, in fractions of the
    /// screen: the block follows on the grid; true when that put it in another cell.
    /// </summary>
    public bool Step(double dxFrac, double dyFrac)
    {
        var landing = BlockGeometry.Move(Start, dxFrac, dyFrac, _screen);

        // [S-13041] The held card on the cell of what it lands in: the block follows it.
        if (_cell is { } cellOf && _held >= 0 && _held < Start.Count)
        {
            var at = At(Start[_held], landing);
            var cell = cellOf(at);
            landing = new BlockGeometry.Delta(landing.Dx + (cell.X - at.X), landing.Dy + (cell.Y - at.Y));
        }

        var changed = landing != Landing;
        Landing = landing;
        return changed;
    }

    /// <summary>Where the card that started at <paramref name="start"/> stands under <paramref name="delta"/>.</summary>
    public FracRect At(FracRect start, BlockGeometry.Delta delta) => BlockGeometry.Apply(start, delta, _screen);

    /// <summary>The box the block lands in now: what the local grid lights ([S-11874]).</summary>
    public FracRect LandingBounds() => BlockGeometry.Bounds([.. Start.Select(r => At(r, Landing))]);
}

/// <summary>
/// [S-11890] Pointer moves coalesced to one step a rendered frame: a mouse reports several moves a frame, and only
/// the latest is where the cards should be when the frame is drawn. <see cref="Offer"/> keeps the latest and says
/// when a frame must be asked for (the first move since the last <see cref="Take"/>); the frame takes it.
/// </summary>
public sealed class PointerFrames
{
    private (double Dx, double Dy)? _pending;

    /// <summary>Whether a move waits for its frame.</summary>
    public bool Pending => _pending is not null;

    /// <summary>The pointer is at (<paramref name="dx"/>, <paramref name="dy"/>): true when no frame is asked for yet, so the caller asks for one.</summary>
    public bool Offer(double dx, double dy)
    {
        var ask = _pending is null;
        _pending = (dx, dy);
        return ask;
    }

    /// <summary>The latest move, and nothing waits any more; null when nothing waited.</summary>
    public (double Dx, double Dy)? Take()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }
}
