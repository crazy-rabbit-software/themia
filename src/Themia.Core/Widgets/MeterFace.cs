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
/// <param name="TokenBeside">[S-1161] The token stands on the reading's baseline; false puts it on its own line under the reading.</param>
public readonly record struct MeterFacePlan(MeterFaceArrangement Arrangement, TypeStep Reading, double ReadingColumn, double GraphHeight, bool TokenBeside = true)
{
    public bool Graph => Arrangement != MeterFaceArrangement.ReadingOnly;
}

/// <summary>
/// [S-1762] The system meters show three things only: their label (the card's header), the reading
/// and its history. This is the ladder for those two at every size: the history grows to fill the
/// tile, so a large tile is a large chart rather than empty space under a number, and a tiny tile
/// is the reading alone.
/// <para>
/// [S-3860] The reading is the shared hero readout (<see cref="HeroLayout"/>): the number with a
/// smaller unit, its step the tier ladder's (<see cref="HeroLayout.TierStep"/>, as the stock price
/// beside its chart), fitted down to the room; it sits <see cref="HeroLayout.TopInset"/> under the
/// header and <see cref="HeroLayout.SideInset"/> from the sides. With the history switched off the
/// reading is a tile of its own and fills it as the weather's does (Display, then grown past it).
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
    /// The plan for a box of <paramref name="area"/>. <paramref name="bound"/> is the widest
    /// reading, each run measured at the size it is drawn (the number at the step, the unit and the
    /// token smaller); the token never shrinks the number, it moves under it ([S-1161]), so every
    /// meter of a tier draws its number at one step; <paramref name="icon"/> a
    /// glyph before it (the battery's). <paramref name="gap"/> is the space between the reading and
    /// the history under it, <paramref name="graphFloor"/> the least history worth drawing. A
    /// <paramref name="fixedStep"/> keeps the reading at one step (a panel that names its adapter
    /// beside the number); the history still takes the rest.
    /// </summary>
    public static MeterFacePlan Plan(
        WidgetArea area,
        bool graphOn,
        HeroBound bound,
        bool icon,
        double gap,
        double graphFloor,
        TypeStep? fixedStep = null)
    {
        const double inset = HeroLayout.SideInset;
        const double top = HeroLayout.TopInset;

        if (!area.IsMeasured)
        {
            return new MeterFacePlan(graphOn ? MeterFaceArrangement.Stacked : MeterFaceArrangement.ReadingOnly, fixedStep ?? FluentType.Subtitle, area.Width, 0);
        }

        var width = Math.Max(0, area.Width);
        var height = Math.Max(0, area.Height);
        var textWidth = Math.Max(0, width - (2 * inset));
        var tier = HeroLayout.TierStep(area);

        // A tile small both ways is the reading alone: a sliver of history under it says nothing.
        var tiny = area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact;

        if (graphOn && !tiny && area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Expanded)
        {
            var column = Math.Round(Math.Clamp(
                Math.Max(width * SideColumnShare, HeroLayout.ValueWidth(FluentType.Title, bound.Value, bound.Unit) + (2 * inset)),
                0,
                width * SideColumnCap));
            var room = Math.Max(0, column - (2 * inset));
            var step = fixedStep ?? Reading(room, height, bound, icon, tier);
            return new MeterFacePlan(MeterFaceArrangement.Side, step, column, height, TokenFits(step, room, bound, icon));
        }

        var floor = fixedStep ?? FluentType.Subtitle;

        if (graphOn && !tiny && height - top - floor.LineHeight - gap >= graphFloor)
        {
            var promised = Math.Max(graphFloor, Math.Round(height * GraphShare));
            var box = height - top - gap - promised;
            var step = fixedStep ?? Reading(textWidth, box, bound, icon, tier);
            var beside = TokenFits(step, textWidth, bound, icon);

            // A token that does not fit beside is the first line under the rate: its line comes out of the history.
            var token = beside ? 0 : HeroLayout.TrailingStep(step).LineHeight;
            return new MeterFacePlan(MeterFaceArrangement.Stacked, step, width, Math.Max(0, height - top - step.LineHeight - token - gap), beside);
        }

        if (graphOn || fixedStep is not null)
        {
            // The history is on but the tile is too small for it: still a chart widget's tile, so
            // the reading keeps the tier's step rather than becoming a poster.
            var step = fixedStep ?? Reading(textWidth, height, bound, icon, tier);
            return new MeterFacePlan(MeterFaceArrangement.ReadingOnly, step, width, 0, TokenFits(step, textWidth, bound, icon));
        }

        // No history at all: the reading is the whole widget, as the weather's is.
        var alone = Reading(textWidth, height, bound, icon, FluentType.Display);
        var left = icon ? textWidth - HeroLayout.IconSize(alone) - HeroLayout.IconGap : textWidth;
        var grown = HeroLayout.Grow(alone, left, height, bound.Value, bound.Unit);
        return new MeterFacePlan(MeterFaceArrangement.ReadingOnly, grown, width, 0, TokenFits(grown, textWidth, bound, icon));
    }

    /// <summary>
    /// [S-1161] Whether the token still stands on the reading's baseline at <paramref name="step"/>
    /// within <paramref name="width"/>; else it is a line of its own under the reading. The token
    /// never shrinks the number: the number is the step every meter of the tier shares ([S-3860]).
    /// </summary>
    public static bool TokenFits(TypeStep step, double width, HeroBound bound, bool icon)
        => bound.Token <= 0
            || (icon ? HeroLayout.IconSize(step) + HeroLayout.IconGap : 0)
                + HeroLayout.ValueWidth(step, bound.Value, bound.Unit)
                + FluentType.EstimateWidth(HeroLayout.TrailingStep(step), bound.Token) <= width + 1e-9;

    /// <summary>[S-3860] The reading's step for a box: the hero fit from Subtitle (never smaller: the reading is the point of the tile) up to <paramref name="ceiling"/>.</summary>
    public static TypeStep Reading(double width, double height, HeroBound bound, bool icon, TypeStep ceiling)
        => HeroLayout.FitBeside(width, height, bound.Value, bound.Unit, FluentType.Subtitle, ceiling, icon);
}
