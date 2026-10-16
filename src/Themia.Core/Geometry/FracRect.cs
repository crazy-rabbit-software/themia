namespace Themia.Core.Geometry;

/// <summary>
/// A widget rect stored as <b>fractions of its screen</b>: <c>X</c>/<c>W</c> are fractions of
/// screen width, <c>Y</c>/<c>H</c> of screen height. Mirrors Themia [S-01]/[S-02]: geometry is
/// resolution independent, so the same layout renders proportionally on any monitor. There is
/// no stored pixel geometry and no stored grid.
/// <para>
/// [S-840] "The screen" is the <b>whole monitor</b>, taskbar included — never the work area. The
/// work area changes whenever the taskbar is resized, moved or set to auto-hide, and fractions of
/// it would shift and rescale every widget each time.
/// </para>
/// </summary>
public readonly record struct FracRect(double X, double Y, double W, double H)
{
    /// <summary>The whole screen.</summary>
    public static FracRect Full => new(0, 0, 1, 1);

    public double Right => X + W;
    public double Bottom => Y + H;

    public static FracRect FromEdges(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);

    /// <summary>
    /// [S-10] Convert a widget definition's default <b>pixel</b> size into a fraction of the screen
    /// it is dropped on, so a new widget has a consistent physical size on every monitor and
    /// scales per [S-02] thereafter.
    /// </summary>
    public static FracRect FromPixels(double x, double y, double wPx, double hPx, ScreenMetrics screen)
        => new(x, y, wPx / screen.Width, hPx / screen.Height);

    public bool IsFinite =>
        double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(W) && double.IsFinite(H);
}

/// <summary>
/// Logical size of the surface a layout renders into, in device-independent pixels: the whole
/// monitor ([S-840]).
/// </summary>
public readonly record struct ScreenMetrics(double Width, double Height)
{
    private readonly FracRect? _allowed;

    /// <summary>
    /// [S-841] The part of the screen <b>new</b> geometry may occupy — the display's work area, as a
    /// fraction of the whole screen. Placement, drag, resize, paste and drop keep at least the
    /// visible minimum on it ([S-6608]), so nothing is lost under the taskbar or past the screen's
    /// edge; rendering ignores it, so a taskbar change never moves what is already there. Defaults to the whole screen; anything unusable (non-finite, empty, wholly
    /// off-screen) also means the whole screen rather than a rect nothing fits in.
    /// </summary>
    public FracRect Allowed
    {
        get => _allowed ?? FracRect.Full;
        init => _allowed = GridGeometry.NormaliseAllowed(value);
    }

    public bool IsUsable => Width > 0 && Height > 0 && double.IsFinite(Width) && double.IsFinite(Height);
}

/// <summary>
/// [S-31] The eight resize handles. A corner drags one edge on each axis; a side handle drags
/// only its own edge.
/// </summary>
public enum ResizeHandle
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}
