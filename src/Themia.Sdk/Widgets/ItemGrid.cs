namespace Themia.Core.Widgets;

/// <summary>
/// [S-12830] The desktop grid a widget's items lay on (owner, 2026-11-19: "items inside widgets should be designed in a
/// way that they match the size of the grid on the desktop, for example they should be designed so that they are 3
/// blocks high or wide, that means that a widget that is 10 blocks hight should not show 4 items because then the items
/// would have an uneven size which i dont want. do this for all the widgets."). <see cref="Cell"/> is the grid's drawn
/// pitch on the widget's display in DIPs ([S-06b]: about 10, screen / columns), <see cref="Scale"/> the display's
/// raster scale. Every repeated item is a whole number of cells along the axis it repeats (<see cref="ItemStretch"/>).
/// </summary>
public readonly record struct ItemGrid(double Cell, double Scale = 1)
{
    /// <summary>[S-06b] The cell's target, and the pitch a view uses before it knows its display's.</summary>
    public const double DefaultCell = 10;

    /// <summary>The default grid at <paramref name="scale"/>.</summary>
    public static ItemGrid Default(double scale = 1) => new(DefaultCell, scale);

    /// <summary>The pitch, a usable one (the default for a degenerate value).</summary>
    public double Pitch => double.IsFinite(Cell) && Cell > 0 ? Cell : DefaultCell;

    /// <summary>The raster scale, a usable one.</summary>
    public double Raster => double.IsFinite(Scale) && Scale > 0 ? Scale : 1;

    /// <summary>The fewest whole cells that hold <paramref name="length"/> (at least one).</summary>
    public int Cells(double length)
        => !double.IsFinite(length) || length <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((length / Pitch) - 1e-6));

    /// <summary>How many whole cells <paramref name="length"/> holds (rounded down).</summary>
    public int Within(double length)
        => !double.IsFinite(length) || length <= 0 ? 0 : (int)Math.Floor((length / Pitch) + 1e-6);

    /// <summary><paramref name="length"/> rounded up to whole cells.</summary>
    public double Whole(double length) => Cells(length) * Pitch;

    /// <summary>Whether <paramref name="length"/> is a whole number of cells, within <paramref name="tolerance"/> DIP.</summary>
    public bool IsWhole(double length, double tolerance = 0.5)
    {
        if (!double.IsFinite(length) || length <= 0)
        {
            return false;
        }

        var cells = Math.Round(length / Pitch);
        return cells >= 1 && Math.Abs(length - (cells * Pitch)) <= tolerance;
    }
}
