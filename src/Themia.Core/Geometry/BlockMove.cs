namespace Themia.Core.Geometry;

/// <summary>What a pointer step of a <see cref="BlockMove"/> changed: where the block is drawn, where it lands.</summary>
public readonly record struct BlockStep(bool ShownChanged, bool LandingChanged);

/// <summary>
/// [S-11890] [S-11891] A block of cards dragged by the pointer (a selection in edit mode, the locked desktop's move):
/// two translations, kept apart. <see cref="Shown"/> is where the cards are drawn: the pointer's own, whole pixel
/// steps, never snapped (<see cref="BlockGeometry.Follow"/>), so the cards stay glued to it. <see cref="Landing"/> is
/// where they land when it lets go: snapped to the grid as a block (<see cref="BlockGeometry.Move"/>), which the local
/// grid lights. With widgets kept apart ([S-11959]) both go through <see cref="BlockGeometry.Apart"/>: the landing
/// against where it stood, the drawn place against the landing, so a card drawn beside one that stays stops flush
/// where it would land instead of sliding into it.
/// <para>
/// <see cref="Step"/> says which of the two changed, so the surface does only that: the cards' offsets for a new
/// <see cref="Shown"/>, the local grid and the border marks only for a new <see cref="Landing"/> (another cell).
/// Pure; the surface holds one per move.
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

    /// <summary>[S-11891] The translation the cards are drawn at now.</summary>
    public BlockGeometry.Delta Shown { get; private set; }

    /// <summary>[S-1584] The translation the cards land at if the pointer lets go now: on the grid, kept apart.</summary>
    public BlockGeometry.Delta Landing { get; private set; }

    /// <summary>
    /// The pointer is (<paramref name="dxFrac"/>, <paramref name="dyFrac"/>) from where it pressed, in fractions of the
    /// screen: both translations follow, and the step says which of them changed.
    /// </summary>
    public BlockStep Step(double dxFrac, double dyFrac)
    {
        var landing = BlockGeometry.Move(Start, dxFrac, dyFrac, _screen, _rasterScale);
        var shown = BlockGeometry.Follow(Start, dxFrac, dyFrac, _screen, _rasterScale);
        if (Apart is { } apart)
        {
            landing = BlockGeometry.Apart(Start, landing, Landing, apart, _screen, _rasterScale);
            shown = BlockGeometry.Apart(Start, shown, landing, apart, _screen, _rasterScale);
        }

        var step = new BlockStep(shown != Shown, landing != Landing);
        Shown = shown;
        Landing = landing;
        return step;
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
