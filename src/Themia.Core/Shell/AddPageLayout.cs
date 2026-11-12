using M = Themia.Sdk.Kit.ShellMetrics.AddPage;

namespace Themia.Core.Shell;

/// <summary>
/// [S-11707] The add page's tiles laid out, pure: as many tiles of at least 200 as fit across, 8 apart,
/// the row filling the width evenly; a picture fitted into its box, scaled down, never up.
/// </summary>
public static class AddPageLayout
{
    /// <summary>How many tiles stand across <paramref name="width"/> DIPs: as many of at least 200 as fit, never fewer than one.</summary>
    public static int Columns(double width)
        => Math.Max(1, (int)Math.Floor((Math.Max(0, width) + M.TileGap) / (M.TileLeast + M.TileGap)));

    /// <summary>One tile's width when <paramref name="columns"/> fill <paramref name="width"/>, 8 apart (never under 0).</summary>
    public static double TileWidth(double width, int columns)
        => Math.Max(0, Math.Floor((width - (M.TileGap * (Math.Max(1, columns) - 1))) / Math.Max(1, columns)));

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
