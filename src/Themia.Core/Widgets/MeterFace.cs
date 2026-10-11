namespace Themia.Core.Widgets;

/// <summary>[S-1762] Where a meter's history goes beside its reading.</summary>
public enum MeterFaceArrangement
{
    /// <summary>The reading on top, the history filling everything under it.</summary>
    Stacked = 0,

    /// <summary>A wide strip: the reading in a column on the left, the history the full height on the right.</summary>
    Side = 1,

    /// <summary>No history (switched off, or a tile too small for one): the reading alone, filling the tile.</summary>
    ReadingOnly = 2,
}

/// <summary>
/// [S-1762] One meter's face for a box: the reading's type step, and the room the history gets.
/// </summary>
/// <param name="Arrangement">Where the history goes.</param>
/// <param name="Reading">The reading's step.</param>
/// <param name="ReadingColumn">The outer width of the reading's column (the whole box unless <see cref="MeterFaceArrangement.Side"/>).</param>
/// <param name="GraphHeight">The history's height: everything the reading and the gap leave when stacked, the box's whole height beside it; 0 with none.</param>
public readonly record struct MeterFacePlan(MeterFaceArrangement Arrangement, TypeStep Reading, double ReadingColumn, double GraphHeight)
{
    public bool Graph => Arrangement != MeterFaceArrangement.ReadingOnly;
}

/// <summary>
/// [S-1762] The system meters show three things only: their label (the card's header), the reading
/// and its history. This is the ladder for those two at every size: the history grows to fill the
/// tile, so a large tile is a large chart rather than empty space under a number, and a tiny tile
/// is the reading alone.
/// <para>
/// The reading grows by ramp step, never past <see cref="FluentType.Display"/> beside a history, and
/// the history is promised <see cref="GraphShare"/> of the height before the reading may grow into
/// it. Without a history the reading fills the tile.
/// </para>
/// <para>
/// [S-1824] The history is the card's bottom band, edge to edge: no inset at its sides or under it
/// (the card's rounded corners clip it). Only the reading keeps the text's inset.
/// </para>
/// </summary>
public static class MeterFace
{
    /// <summary>The least share of the box's height the history is promised once it is shown.</summary>
    public const double GraphShare = 0.5;

    /// <summary>On a strip, the reading's column takes at least this share of the width.</summary>
    public const double SideColumnShare = 0.35;

    /// <summary>And at most this share: past it the history beside is the narrower story.</summary>
    public const double SideColumnCap = 0.5;

    /// <summary>
    /// The plan for a box of <paramref name="area"/>. <paramref name="inset"/> is the content inset:
    /// the reading's from both sides (the history has none, [S-1824]); <paramref name="gap"/> the space between
    /// the reading and the history under it, <paramref name="graphFloor"/> the least history worth
    /// drawing. A <paramref name="fixedStep"/> keeps the reading at one step (a panel that names its
    /// adapter beside the number); the history still takes the rest.
    /// </summary>
    public static MeterFacePlan Plan(
        WidgetArea area,
        bool graphOn,
        double readingCharacters,
        double inset,
        double gap,
        double graphFloor,
        TypeStep? fixedStep = null)
    {
        var characters = Math.Max(1, readingCharacters);

        if (!area.IsMeasured)
        {
            return new MeterFacePlan(graphOn ? MeterFaceArrangement.Stacked : MeterFaceArrangement.ReadingOnly, fixedStep ?? FluentType.Subtitle, area.Width, 0);
        }

        var width = Math.Max(0, area.Width);
        var height = Math.Max(0, area.Height);
        var textWidth = Math.Max(0, width - (2 * inset));

        // A tile small both ways is the reading alone: a sliver of history under it says nothing.
        var tiny = area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact;

        if (graphOn && !tiny && area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Expanded)
        {
            var column = Math.Round(Math.Clamp(
                Math.Max(width * SideColumnShare, FluentType.EstimateWidth(FluentType.Title, characters) + (2 * inset)),
                0,
                width * SideColumnCap));
            var step = fixedStep ?? Reading(Math.Max(0, column - (2 * inset)), Math.Min(height, FluentType.Display.LineHeight), characters);
            return new MeterFacePlan(MeterFaceArrangement.Side, step, column, height);
        }

        var floor = fixedStep ?? FluentType.Subtitle;

        if (graphOn && !tiny && height - floor.LineHeight - gap >= graphFloor)
        {
            var promised = Math.Max(graphFloor, Math.Round(height * GraphShare));
            var box = Math.Min(height - gap - promised, FluentType.Display.LineHeight);
            var step = fixedStep ?? Reading(textWidth, box, characters);
            return new MeterFacePlan(MeterFaceArrangement.Stacked, step, width, Math.Max(0, height - step.LineHeight - gap));
        }

        return new MeterFacePlan(MeterFaceArrangement.ReadingOnly, fixedStep ?? Reading(textWidth, height, characters), width, 0);
    }

    /// <summary>The reading's step for a box. Never below Subtitle: the reading is the point of the tile.</summary>
    public static TypeStep Reading(double width, double height, double characters)
    {
        var hero = FluentType.Hero(width, height, characters);
        return hero.FontSize < FluentType.Subtitle.FontSize ? FluentType.Subtitle : hero;
    }
}
