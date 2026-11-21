namespace Themia.Core.Geometry;

/// <summary>
/// [S-11895] A screen's widget grid as whole numbers (<see cref="GridGeometry.GridDims"/>): <see cref="Cols"/> lines
/// across, <see cref="Rows"/> down, line <c>i</c> at <c>i / Cols</c> of the screen's width ([S-06], [S-06b]). The one place a
/// fraction becomes a line (<see cref="Col"/>, <see cref="Row"/>; in cells <see cref="Nearest"/>, <see cref="Up"/>,
/// <see cref="Down"/>) and a line a fraction (<see cref="X"/>, <see cref="Y"/>): the snap, a block's move, the fit of a
/// group, the grid settle, the desktop grid and the fences round by it alone.
/// <para>
/// The nearest line is half a cell up, as every edge's DIP and device pixel is ([S-11873], <see cref="GridGeometry.WholeDip"/>),
/// and a count within float noise of a line is on it (<see cref="Lattice.Epsilon"/>): until 2026-11-21 the snap rounded half
/// to even, the desktop grid and the fences half away from zero, the fences' and the icons' ranges with their own noise.
/// </para>
/// </summary>
public readonly record struct GridSpace(int Cols, int Rows)
{
    /// <summary>The lines across the screen on one axis: its columns (<paramref name="across"/>) or its rows.</summary>
    public int Lines(bool across) => across ? Cols : Rows;

    /// <summary>Where column line <paramref name="col"/> stands, as a fraction of the screen's width.</summary>
    public double X(int col) => col / (double)Cols;

    /// <summary>Where row line <paramref name="row"/> stands, as a fraction of the screen's height.</summary>
    public double Y(int row) => row / (double)Rows;

    /// <summary>The column line nearest <paramref name="x"/> (a fraction of the width).</summary>
    public int Col(double x) => Nearest(x * Cols);

    /// <summary>The row line nearest <paramref name="y"/> (a fraction of the height).</summary>
    public int Row(double y) => Nearest(y * Rows);

    /// <summary>[S-11895] The line nearest a position of <paramref name="cells"/>: half a cell up, float noise on the line.</summary>
    public static int Nearest(double cells) => (int)Math.Floor(cells + 0.5 + Lattice.Epsilon);

    /// <summary>[S-11895] The first line at or after <paramref name="cells"/>, float noise on the line.</summary>
    public static int Up(double cells) => (int)Math.Ceiling(cells - Lattice.Epsilon);

    /// <summary>[S-11895] The last line at or before <paramref name="cells"/>, float noise on the line.</summary>
    public static int Down(double cells) => (int)Math.Floor(cells + Lattice.Epsilon);
}
