namespace Themia.Core.Geometry;

/// <summary>
/// [S-11890] [S-11891] A block of cards dragged by the pointer (a selection in edit mode, the locked desktop's move):
/// one translation, <see cref="Landing"/>, the move snapped to the grid as a block (<see cref="BlockGeometry.Move"/>,
/// [S-1584]) and, with widgets kept apart ([S-11959]), kept apart (<see cref="BlockGeometry.Apart"/>). The cards are
/// drawn there while they are dragged and land there: they always stick to the grid.
/// <para>
/// <see cref="Step"/> says whether it changed (another cell), so the surface touches the cards, the local grid and the
/// border marks only then. Pure; the surface holds one per move.
/// </para>
/// </summary>
public sealed class BlockMove
{
    private readonly ScreenMetrics _screen;
    private readonly double _rasterScale;

    public BlockMove(IReadOnlyList<FracRect> start, IReadOnlyList<FracRect>? apart, ScreenMetrics screen, double rasterScale)
    {
        ArgumentNullException.ThrowIfNull(start);
        Start = start;
        Apart = apart;
        _screen = screen;
        _rasterScale = rasterScale;
    }

    /// <summary>The rects the moving cards started at.</summary>
    public IReadOnlyList<FracRect> Start { get; }

    /// <summary>[S-11959] The rects of the cards that stay, with widgets kept apart; null when they may overlap.</summary>
    public IReadOnlyList<FracRect>? Apart { get; }

    /// <summary>[S-1584] [S-11891] The translation the cards are drawn at and land at: on the grid, kept apart.</summary>
    public BlockGeometry.Delta Landing { get; private set; }

    /// <summary>
    /// The pointer is (<paramref name="dxFrac"/>, <paramref name="dyFrac"/>) from where it pressed, in fractions of the
    /// screen: the block follows on the grid; true when that put it in another cell.
    /// </summary>
    public bool Step(double dxFrac, double dyFrac)
    {
        var landing = BlockGeometry.Move(Start, dxFrac, dyFrac, _screen, _rasterScale);
        if (Apart is { } apart)
        {
            landing = BlockGeometry.Apart(Start, landing, Landing, apart, _screen, _rasterScale);
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
