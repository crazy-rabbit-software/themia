namespace Themia.Core.Widgets;

/// <summary>
/// [S-663] The slot a card gives its view: the card less its header, in DIPs. A view measures its fixed
/// parts on it (a graph's length, where a text wraps); it never chooses what to show by it: what a widget
/// shows is its config, the same at every size of its range ([S-2445], [S-11800]).
/// </summary>
/// <param name="Width">Usable width in DIPs.</param>
/// <param name="Height">Usable height in DIPs.</param>
public readonly record struct WidgetArea(double Width, double Height)
{
    /// <summary>False before the first layout pass (or for a degenerate box): there is nothing to lay out in yet.</summary>
    public bool IsMeasured => IsUsable(Width) && IsUsable(Height);

    /// <summary>The area left inside a padding, never negative.</summary>
    public WidgetArea Deflate(double horizontal, double vertical)
        => new(Math.Max(0, Width - (2 * horizontal)), Math.Max(0, Height - (2 * vertical)));

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
