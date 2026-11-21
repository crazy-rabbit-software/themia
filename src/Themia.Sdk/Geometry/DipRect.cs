namespace Themia.Core.Geometry;

/// <summary>
/// [S-11894] A box in device-independent pixels (DIPs): a card's cell on its display, a face part, a flyout, the work
/// area as the surface lays it out. What XAML lays out in. Never a physical pixel's box (<see cref="DeviceRect"/>): the
/// two differ by the display's raster scale, and one type for both (PixelRect until 2026-11-21) let a monitor's pixels
/// pass for DIPs.
/// </summary>
public readonly record struct DipRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public static DipRect FromEdges(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);
}

/// <summary>
/// [S-11894] A box in a raster's own pixels: a display's physical pixels (virtual-screen coordinates, what Win32 reports:
/// a monitor, its work area, a window), or a picture's. Brought to DIPs only through the display's raster scale
/// (<c>GridGeometry.DisplayScreen</c>), never taken for DIPs.
/// </summary>
public readonly record struct DeviceRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public static DeviceRect FromEdges(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);
}
