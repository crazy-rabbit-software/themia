namespace Themia.Core.Geometry;

/// <summary>
/// [S-854] A display's surface as everything that writes geometry sees it: the view it shows, its
/// metrics, its widget scale ([S-850]) and whether those metrics are <b>settled</b>. A surface first lays
/// out at provisional metrics (the monitor's pixels taken as DIPs at scale 1, before its XAML root knows
/// the scale it really renders at), then at the real ones. A layout may be drawn at provisional metrics;
/// nothing is written by them: an icon aligned or a widget refitted there lands on a grid 1.5 times too
/// fine on a 150 % display, and the real pass cannot tell that from the user's own arrangement.
/// </summary>
public readonly record struct SurfaceMetrics(string ViewId, ScreenMetrics Screen, double DisplayScale, bool Settled)
{
    /// <summary>[S-854] Geometry may be written by these metrics: they are settled and usable.</summary>
    public bool Writable => Settled && Screen.IsUsable;

    /// <summary>
    /// [S-854] Every live surface has settled (and there is one): what settling the desktop across the
    /// displays waits for, since a view an unsettled surface shows would look shown nowhere.
    /// </summary>
    public static bool AllSettled(IReadOnlyList<SurfaceMetrics> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        return surfaces.Count > 0 && surfaces.All(s => s.Writable);
    }

    /// <summary>
    /// [S-854] The scale a surface really renders at, once it is known: its root loaded and its XAML root's
    /// rasterization scale a positive number. Null before, whatever the fallback the layout draws at.
    /// </summary>
    public static double? SettledScale(bool loaded, double? rasterScale)
        => loaded && rasterScale is { } scale && double.IsFinite(scale) && scale > 0 ? scale : null;
}
