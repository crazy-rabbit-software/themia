namespace Themia.Core.Model;

/// <summary>A part on the shelf: its box in the shelf's grid cells (its caption stands in the cells under it).</summary>
public readonly record struct ShelfBox(int X, int Y, int W, int H);

/// <summary>The shelf laid: each part's box, in the order given, and the shelf's size in cells.</summary>
public sealed record ShelfLayout(IReadOnlyList<ShelfBox> Boxes, int Width, int Height);

/// <summary>
/// [S-13917] The parts' shelf beside a container whose layout is edited (owner, 2026-11-22: "the menu that comes up with the
/// items that you can place onto the row should be on the background grid already. the items should appear aligned to the
/// background grid, somewhat like the not on home page does it"): the parts offered, each at the size it lands with in whole
/// cells of the desktop's grid, laid in reading order on that grid: a cell round the shelf and between parts, a part's name in
/// the cells under it, a line as tall as its tallest part. Pure.
/// </summary>
public static class PartShelf
{
    /// <summary>The cells round the shelf and between two parts.</summary>
    public const int Gap = 1;

    /// <summary>The cells under a part its name stands in.</summary>
    public const int Caption = 2;

    /// <summary>The narrowest the shelf is: its heading and Done fit.</summary>
    public const int LeastWidth = 24;

    /// <summary>
    /// The parts of <paramref name="sizes"/> (cells) laid on a shelf <paramref name="width"/> cells wide (never under the
    /// widest part and its gaps, nor <see cref="LeastWidth"/>): left to right, wrapping where the next does not fit.
    /// </summary>
    public static ShelfLayout Lay(IReadOnlyList<(int W, int H)> sizes, int width)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var widest = sizes.Count == 0 ? 0 : sizes.Max(s => Math.Max(1, s.W));
        width = Math.Max(Math.Max(width, LeastWidth), widest + (2 * Gap));
        var boxes = new List<ShelfBox>(sizes.Count);
        var (x, y, line) = (Gap, Gap, 0);
        foreach (var (w0, h0) in sizes)
        {
            var (w, h) = (Math.Max(1, w0), Math.Max(1, h0));
            if (x > Gap && x + w > width - Gap)
            {
                (x, y, line) = (Gap, y + line + Caption + Gap, 0);
            }

            boxes.Add(new ShelfBox(x, y, w, h));
            x += w + Gap;
            line = Math.Max(line, h);
        }

        return new ShelfLayout(boxes, width, sizes.Count == 0 ? 2 * Gap : y + line + Caption + Gap);
    }

    /// <summary>
    /// The shelf for a room <paramref name="most"/> cells tall: the narrowest (from the least width, a part's width more each
    /// try, up to <paramref name="widest"/> cells) whose lines fit it; the widest tried where none does.
    /// </summary>
    public static ShelfLayout Fit(IReadOnlyList<(int W, int H)> sizes, int most, int widest)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var laid = Lay(sizes, LeastWidth);
        for (var width = laid.Width; laid.Height > most && width < widest;)
        {
            width = Math.Min(widest, width + Math.Max(4, laid.Width / 2));
            laid = Lay(sizes, width);
        }

        return laid;
    }
}
