namespace Themia.Core.Widgets;

/// <summary>
/// [S-1216] The measures the three screen-time widgets' ladders share ([S-3332], [S-3342], [S-3352]:
/// each ladder is its module's own). Concise first ([S-666]): each grows its own content, taller bars,
/// a larger ring, more rows, and never gains a part that is another widget's.
/// </summary>
public static class ScreenTimeLayout
{
    /// <summary>The inset the drawings keep from the card's sides.</summary>
    public const double Inset = 16;

    /// <summary>The gap between two shown parts, and above the card's bottom edge.</summary>
    public const double Gap = 8;
}
