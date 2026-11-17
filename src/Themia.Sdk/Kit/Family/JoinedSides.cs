namespace Themia.Sdk.Kit;

/// <summary>[S-12483] Where a fixed block stands in a box taller than it, along the box's height.</summary>
public enum BlockEdge
{
    /// <summary>In the middle: the spare above and under it alike.</summary>
    Centre,

    /// <summary>At the top: the spare under it.</summary>
    Top,

    /// <summary>At the foot: the spare above it.</summary>
    Bottom,
}

/// <summary>
/// [S-12482] [S-12483] The sides of a card where a member of its own group joins it ([S-2249]: one panel),
/// as the card tells its view. On such a side the member's content runs to the edge: no inset, so the
/// items of neighbouring members meet across the seam as the items of one widget meet (owner, 2026-11-16:
/// "there is still a little space vertically which i dont want there to be"). The outer sides keep the
/// family's inset. Only the top and the foot: across a seam on the left or the right a member's text
/// would run into its mate's ([S-12484]), so those keep the inset.
/// </summary>
public readonly record struct JoinedSides(bool Top, bool Bottom)
{
    /// <summary>A card joined to nothing: the inset on every side.</summary>
    public static JoinedSides Free => default;

    /// <summary>Whether a mate joins either side.</summary>
    public bool Any => Top || Bottom;

    /// <summary>
    /// [S-12482] How far under the card's top edge its view starts: the inset ([S-5700]) on a free top,
    /// nothing where a mate joins it or the view owns its top edge (a picture drawn to it).
    /// </summary>
    public double TopInset(bool ownsTopEdge) => Top || ownsTopEdge ? 0 : FamilySpace.Inset;

    /// <summary>[S-12483] What a view keeps under its content: the inset on a free foot, nothing where a mate joins it.</summary>
    public double Foot => Bottom ? 0 : FamilySpace.Inset;

    /// <summary>
    /// [S-12483] Where a fixed block (a reading, a block held at its natural height) stands in a taller box:
    /// at the joined side when one side is joined, so it meets its mate; in the middle when free or joined
    /// on both.
    /// </summary>
    public BlockEdge Block => (Top, Bottom) switch
    {
        (true, false) => BlockEdge.Top,
        (false, true) => BlockEdge.Bottom,
        _ => BlockEdge.Centre,
    };

    /// <summary>
    /// [S-12800] Where rows that start at the top (a list, facts, a week's days) stand in their box: at the foot when
    /// only the foot is joined, so the last row meets the mate under it and the spare (the free top's inset, a part
    /// row) goes over them; at the top otherwise, the spare under them as a free card's foot.
    /// </summary>
    public BlockEdge Rows => (Top, Bottom) == (false, true) ? BlockEdge.Bottom : BlockEdge.Top;

    /// <summary>
    /// [S-12483] How <paramref name="spare"/> (the box's height less the block's) falls above the block, by
    /// <see cref="Block"/>: none at the top, all of it at the foot, half (whole DIPs, the odd one under) in
    /// the middle. Never negative.
    /// </summary>
    public double Above(double spare)
    {
        var room = double.IsFinite(spare) ? Math.Max(0, spare) : 0;
        return Block switch
        {
            BlockEdge.Top => 0,
            BlockEdge.Bottom => room,
            _ => Math.Floor(room / 2),
        };
    }
}
