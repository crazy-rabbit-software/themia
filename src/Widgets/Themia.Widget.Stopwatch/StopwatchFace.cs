using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-4605] The stopwatch's face at one size: the timers' face ([S-6739]) in its box, and the laps'
/// column beside it (0 wide where there is none) with the lines and whole rows it shows.
/// </summary>
/// <param name="Face">The time, its lap line and the Controls row.</param>
/// <param name="FaceWidth">The face's content width.</param>
/// <param name="LapColumn">The laps' column at the view's end edge, its rows' own inset included; 0 without one.</param>
/// <param name="LapLines">The lines a lap's row shows: its number and length, then its total.</param>
/// <param name="LapRows">How many whole lap rows the column holds.</param>
public readonly record struct StopwatchPlan(TimerPlan Face, double FaceWidth, double LapColumn, int LapLines, int LapRows);

/// <summary>
/// [S-4605] The stopwatch's ladder on the family's blocks: the time is a one-readout Readout, sized for
/// the widest reading of its hour, centred over the Controls row (Start, Lap, Reset); the lap under way
/// is its line under. From an expanded width the laps stand in a column of two fifths of the view at
/// its end edge, newest first, Row blocks of 32 (48 with the total from a regular tile both ways),
/// whole rows only; narrower, the lap line alone says where the laps are ([S-666]).
/// </summary>
public static class StopwatchFace
{
    /// <summary>The laps' column's share of the view, as a detail widget's figure column has.</summary>
    public const double LapShare = 0.4;

    /// <summary>The widest reading of the hour: every figure an 8 with its tenth ("88:88.8", "88:88:88.8" from an hour).</summary>
    public static string Widest(bool hours) => hours ? StopwatchText.Widest : StopwatchText.Widest[3..];

    /// <summary>[S-4605] The face for a view of <paramref name="area"/> reading <paramref name="hours"/> or more.</summary>
    public static StopwatchPlan Plan(WidgetArea area, bool hours)
    {
        var worst = new ReadoutText(Widest(hours), LineUnder: true, OneReadout: true);
        var height = area.Height - FamilySpace.Inset;
        var column = area.WidthClass >= SizeClass.Expanded ? Math.Floor(area.Width * LapShare) : 0;

        // The column's rows carry their own inset, which is the gap to the face.
        var faceWidth = area.Width - column - (column > 0 ? FamilySpace.Inset : 2 * FamilySpace.Inset);
        var face = TimerFace.Plan(area, faceWidth, height, worst, controls: true);
        var lines = column > 0 ? RowRules.Lines(area, 2) : 1;
        var rows = column > 0 ? RowRules.Fit(height, lines) : 0;
        return new StopwatchPlan(face, faceWidth, column, lines, rows);
    }
}
