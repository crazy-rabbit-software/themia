namespace Themia.Core.Widgets;

/// <summary>
/// [S-663] The slot a card gives its view: the card less its header, in DIPs. A view measures its fixed
/// parts on it (a graph's length, where a text wraps); it never chooses what to show by it: what a widget
/// shows is its config, the same at every size of its range ([S-2445], [S-11800]).
/// </summary>
/// <param name="Width">Usable width in DIPs.</param>
/// <param name="Height">Usable height in DIPs.</param>
/// <param name="Cell">[S-12830] The desktop grid's pitch on the card's display, in DIPs: what its items' optimal lengths are whole numbers of.</param>
/// <param name="Scale">[S-14810] The display's raster scale: the pixels the items' edges fall on.</param>
public readonly record struct WidgetArea(double Width, double Height, double Cell = ItemGrid.DefaultCell, double Scale = 1)
{
    /// <summary>[S-12830] The grid the view's items lay on, at <paramref name="scale"/> (none given: the area's own).</summary>
    public ItemGrid Grid(double scale = 0) => new(Cell, double.IsFinite(scale) && scale > 0 ? scale : Scale);

    /// <summary>False before the first layout pass (or for a degenerate box): there is nothing to lay out in yet.</summary>
    public bool IsMeasured => IsUsable(Width) && IsUsable(Height);

    /// <summary>The area left inside a padding, never negative.</summary>
    public WidgetArea Deflate(double horizontal, double vertical)
        => new(Math.Max(0, Width - (2 * horizontal)), Math.Max(0, Height - (2 * vertical)), Cell, Scale);

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
