namespace Themia.Core.Widgets;

/// <summary>
/// [S-12703] Where a slot of a plot of equal cells stands: the rain's quarter hours ([S-5125]). The weather graphs that drew
/// hours on a frame of their own are the weather metrics' one graph now ([S-14713]).
/// </summary>
public static class HourGraphRules
{
    /// <summary>[S-12703] A slot's middle across a plot <paramref name="width"/> wide of <paramref name="count"/> equal cells.</summary>
    public static double X(int slot, int count, double width) => count <= 0 ? 0 : (slot + 0.5) * width / count;
}
