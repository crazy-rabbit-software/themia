using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>Where the air widget puts its readings ([S-5107]).</summary>
public enum AirFacts
{
    /// <summary>The index and its band only.</summary>
    None = 0,

    /// <summary>One stat line per reading under the index (a tall or narrow tile).</summary>
    Lines = 1,

    /// <summary>Stat cells in two or three columns under the index.</summary>
    Cells = 2,

    /// <summary>Stat lines in a column beside the index (a strip).</summary>
    Side = 3,
}

/// <summary>[S-5107] The air widget's ladder for one size.</summary>
/// <param name="HeroRoom">The height the index and its band may grow into.</param>
/// <param name="Count">How many readings are shown, in priority order.</param>
/// <param name="Columns">Cells or side columns.</param>
/// <param name="SideWidth">The side column's width, on a strip.</param>
public sealed record AirPlan(double HeroRoom, AirFacts Facts, int Count, int Columns, double SideWidth)
{
    /// <summary>[S-5109] The band strip under the readings, once every reading is shown.</summary>
    public bool Strip { get; init; }
}

/// <summary>What the gauge style draws at one size ([S-6170]).</summary>
/// <param name="GaugeWidth">The box the half-circle gauge is fitted into (<see cref="GaugeLayout.Fit"/>).</param>
/// <param name="GaugeHeight">Its height.</param>
/// <param name="Band">The band's words, a line under the gauge.</param>
/// <param name="Facts">How the readings stand: none, lines under the gauge, or a column beside it.</param>
/// <param name="Count">How many readings, in priority order.</param>
/// <param name="Columns">Columns of the side readings.</param>
/// <param name="SideWidth">The side column's width, on a strip.</param>
public sealed record AirGaugePlan(double GaugeWidth, double GaugeHeight, bool Band, AirFacts Facts, int Count, int Columns, double SideWidth);

/// <summary>[S-5107] [S-6170] The air widget's ladders: the number style's and the gauge style's.</summary>
public static class AirLayout
{
    /// <summary>A stat cell's narrowest column.</summary>
    public const double CellMinWidth = 120;

    /// <summary>[S-5109] The band strip: a marker, the bar and a Caption line of breakpoints.</summary>
    public const double StripHeight = 36;

    /// <summary>
    /// [S-5107] The index and its band are reserved; the readings take what is left, one stat line
    /// each on a tall or narrow tile, cells in columns where two fit across, a column beside the
    /// index on a strip. Whatever the readings leave grows the index.
    /// </summary>
    public static AirPlan Air(WidgetArea area, double heroFloor, int facts)
    {
        if (!area.IsMeasured)
        {
            return new AirPlan(0, AirFacts.None, 0, 0, 0);
        }

        var box = area.Deflate(OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        facts = Math.Max(0, facts);

        if (facts > 0 && area.Shape == AreaShape.Wide && box.Width >= 360)
        {
            var columns = box.Width >= 640 ? 2 : 1;
            var sideWidth = Math.Min(box.Width * (columns == 2 ? 0.6 : 0.5), columns * 220.0);
            var rows = Disclosure.WholeRows(box.Height, PartMetrics.StatLine);
            var count = Math.Min(facts, rows * columns);
            return count > 0
                ? new AirPlan(box.Height, AirFacts.Side, count, columns, sideWidth)
                : new AirPlan(box.Height, AirFacts.None, 0, 0, 0);
        }

        var room = new Disclosure(box.Width, box.Height, OnlineLayouts.Gap);
        room.Reserve(heroFloor);

        var across = (int)Math.Floor((box.Width + OnlineLayouts.Gap) / (CellMinWidth + OnlineLayouts.Gap));
        var cellColumns = Math.Clamp(across, 1, 3);

        if (facts > 0 && area.Shape != AreaShape.Tall && cellColumns >= 2)
        {
            var wanted = (facts + cellColumns - 1) / cellColumns;
            var rows = room.TakeRows(PartMetrics.StatCell, wanted, OnlineLayouts.Gap);
            var count = Math.Min(facts, rows * cellColumns);
            var strip = count == facts && box.Width >= 240 && room.Take(StripHeight);
            return new AirPlan(heroFloor + room.Remaining, count > 0 ? AirFacts.Cells : AirFacts.None, count, count > 0 ? cellColumns : 0, 0) { Strip = strip };
        }

        var lines = facts > 0 ? room.TakeRows(PartMetrics.StatLine, facts) : 0;
        var stripped = lines == facts && box.Width >= 160 && room.Take(StripHeight);
        return new AirPlan(heroFloor + room.Remaining, lines > 0 ? AirFacts.Lines : AirFacts.None, lines, lines > 0 ? 1 : 0, 0) { Strip = stripped };
    }

    /// <summary>
    /// [S-6170] The gauge style's ladder: the half-circle gauge ([S-6302], the meters' own) is the
    /// hero, the index in its bowl; then the band's words on a line under it; then the readings, in
    /// a column beside it on a strip (at least 360 wide, as the number style's, [S-5107]) or as stat
    /// lines under it on a tall column while the gauge keeps the column's width. What the rungs
    /// leave is the gauge's.
    /// </summary>
    public static AirGaugePlan AirGauge(WidgetArea area, int facts)
    {
        if (!area.IsMeasured)
        {
            return new AirGaugePlan(0, 0, false, AirFacts.None, 0, 0, 0);
        }

        var box = area.Deflate(OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        var band = FluentType.Body.LineHeight + OnlineLayouts.Gap;
        var floor = GaugeLayout.MinRadius * (1 + GaugeLayout.ThicknessShare);
        facts = Math.Max(0, facts);

        if (facts > 0 && area.Shape == AreaShape.Wide && box.Width >= 360)
        {
            var columns = box.Width >= 640 ? 2 : 1;
            var sideWidth = Math.Min(box.Width * (columns == 2 ? 0.6 : 0.5), columns * 220.0);
            var rows = Disclosure.WholeRows(box.Height, PartMetrics.StatLine);
            var count = Math.Min(facts, rows * columns);
            var withBand = box.Height - band >= floor;
            return new AirGaugePlan(box.Width - sideWidth - OnlineLayouts.SideGap, box.Height - (withBand ? band : 0), withBand,
                count > 0 ? AirFacts.Side : AirFacts.None, count, count > 0 ? columns : 0, count > 0 ? sideWidth : 0);
        }

        var room = new Disclosure(box.Width, box.Height, OnlineLayouts.Gap);
        var tall = facts > 0 && area.Shape == AreaShape.Tall;
        // A column: the gauge as wide as the column (its height about half its width), at most half the height.
        room.Reserve(tall ? Math.Max(floor, Math.Min(box.Width * (1 + GaugeLayout.ThicknessShare) / (2 + GaugeLayout.ThicknessShare), box.Height * 0.5)) : floor);
        var shown = room.Take(FluentType.Body.LineHeight);
        var lines = tall && shown ? room.TakeRows(PartMetrics.StatLine, facts) : 0;
        var used = (shown ? band : 0) + (lines > 0 ? (lines * PartMetrics.StatLine) + OnlineLayouts.Gap : 0);
        return new AirGaugePlan(box.Width, Math.Max(0, box.Height - used), shown,
            lines > 0 ? AirFacts.Lines : AirFacts.None, lines, lines > 0 ? 1 : 0, 0);
    }
}
