using M = Themia.Sdk.Kit.ShellMetrics.AddPage;

namespace Themia.Core.Shell;

/// <summary>[S-13612] How much of a family's stack shows: its tiles shown, and how many the "+N" tile stands for (0: none).</summary>
public readonly record struct AddStack(int Shown, int Hidden)
{
    /// <summary>The tiles the stack draws: its shown tiles and its "+N" tile when it has one.</summary>
    public int Stops => Shown + (Hidden > 0 ? 1 : 0);
}

/// <summary>
/// [S-13610] [S-13616] The add page's width shared between the rail, the tiles and the preview pane; [S-11707]
/// [S-13601] the tiles laid out (as many of at least 200 as fit across the list, 4 apart, the row filling it
/// evenly); [S-13612] a family's stack; a picture fitted into its box, scaled down, never up. Pure.
/// </summary>
public static class AddPageLayout
{
    /// <summary>How many tiles stand across <paramref name="width"/> DIPs: as many of at least 200 as fit, never fewer than one.</summary>
    public static int Columns(double width)
        => Math.Max(1, (int)Math.Floor((Math.Max(0, width) + M.TileGap) / (M.TileLeast + M.TileGap)));

    /// <summary>One tile's width when <paramref name="columns"/> fill <paramref name="width"/>, 4 apart (never under 0).</summary>
    public static double TileWidth(double width, int columns)
        => Math.Max(0, Math.Floor((width - (M.TileGap * (Math.Max(1, columns) - 1))) / Math.Max(1, columns)));

    /// <summary>The least a list of two tiles across takes.</summary>
    private const double TwoAcross = (2 * M.TileLeast) + M.TileGap;

    /// <summary>
    /// [S-13616] [S-13603] The page's <paramref name="width"/> shared out: the rail its 240 and 16 off the tiles;
    /// the preview pane its 360 and 16 off them wherever the list keeps two tiles across beside both, else no
    /// pane; where even that leaves less than two tiles across, the rail its icons alone (48). The list takes the rest.
    /// </summary>
    public static (double Rail, double List, double Preview) Split(double width)
    {
        width = Math.Max(0, width);
        var rail = width - M.Rail - M.RailGap >= TwoAcross ? M.Rail : M.RailCompact;
        var rest = Math.Max(0, width - rail - M.RailGap);
        var list = rest - M.Preview - M.PreviewGap;
        return list >= TwoAcross ? (rail, list, M.Preview) : (rail, rest, 0);
    }

    /// <summary>Whether the rail shows its icons alone at the page's <paramref name="width"/> ([S-13616]).</summary>
    public static bool Compact(double width) => Split(width).Rail < M.Rail;

    /// <summary>
    /// [S-13612] A family of <paramref name="count"/> widgets in a list of <paramref name="columns"/> across:
    /// <paramref name="open"/>, every tile; else its first row, its last cell the "+N" tile for the rest where
    /// they do not fit in one row (one tile and the "+N" under it where only one stands across).
    /// </summary>
    public static AddStack Stack(int count, int columns, bool open)
    {
        count = Math.Max(0, count);
        columns = Math.Max(1, columns);
        if (open || count <= columns)
        {
            return new AddStack(count, 0);
        }

        var shown = Math.Max(1, columns - 1);
        return new AddStack(shown, count - shown);
    }

    /// <summary>
    /// The scale a picture of <paramref name="width"/> × <paramref name="height"/> DIPs is drawn at in a box of
    /// <paramref name="boxWidth"/> × <paramref name="boxHeight"/>: the largest that fits, never over 1.
    /// </summary>
    public static double Fit(double width, double height, double boxWidth, double boxHeight)
    {
        if (width <= 0 || height <= 0)
        {
            return 1;
        }

        return Math.Clamp(Math.Min(boxWidth / width, boxHeight / height), 0, 1);
    }
}
