using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>[S-12832] What a month's day cell draws: the day's chips under its number, its dots, or its number alone.</summary>
public enum MonthFace
{
    Chips,
    Dots,
    Number,
}

/// <summary>
/// [S-12832] A month laid on the card: the columns' widths (edge to edge, whole grid cells), the weekday line's height
/// (ending on a grid line), the week rows shown (whole cells each, all alike) and the first of the six, each cell's face
/// and its chip lanes, and the number's line (it grows with the cell).
/// </summary>
public sealed record MonthLayout(IReadOnlyList<double> Columns, double HeaderHeight, int Rows, int FirstRow, double RowHeight, MonthFace Face, int ChipLanes, double NumberLine)
{
    /// <summary>The narrowest column.</summary>
    public double LeastColumn => Columns.Count == 0 ? 0 : Columns.Min();
}

/// <summary>
/// [S-12832] The month's one layout rule (owner, 2026-11-19: "the calendar does not follow this design philosophy for
/// example. fix that"; "Bigger numbers too"; "the calendar does not match the fucking design rules i told you"; "and the
/// items dont reach from edge to edge"). The day cells tile the card: seven columns from its left edge to its right, each
/// whole grid cells (the cells the width has shared as evenly as they go, the last column taking what is not a whole
/// cell), the weeks from under the weekday line down, each the same whole cells ([S-12830]), as many of the six as hold
/// a day's number at whole cells (the anchor's week among them), the cells left over at the foot. A cell is filled by
/// its content: its day's events as chips under its number where it has the room for a chip line (Outlook's month view),
/// its dots where it does not, its number alone on a day of nothing; the number grows with the cell, as large as what
/// the chips or dots leave of its height and half its width allow.
/// </summary>
public static class MonthLayouts
{
    /// <summary>The least a week's row holds: a day's number line and the cell's 4.</summary>
    public static double LeastRow => FamilyType.Caption.LineHeight + FamilySpace.S1;

    /// <summary>The narrowest column a chip's title reads in.</summary>
    public const double ChipColumn = 44;

    /// <summary>The largest a day's number line grows.</summary>
    public const double MostNumberLine = 64;

    /// <summary>
    /// The month in a card <paramref name="width"/> wide whose month starts <paramref name="top"/> under the card's top
    /// edge and ends at its foot, <paramref name="cardHeight"/> under it, on <paramref name="grid"/>: the anchor's week is
    /// <paramref name="anchorRow"/> (0..5), and the most items a day shown has is <paramref name="mostItems"/>.
    /// </summary>
    public static MonthLayout Plan(double width, double cardHeight, double top, ItemGrid grid, int anchorRow, int mostItems)
    {
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var pitch = grid.Pitch;
        var columns = ItemStretch.Columns(width, 7, grid);

        var headerEnd = grid.Whole(top + FamilyType.Caption.LineHeight);
        var room = Math.Max(0, cardHeight - headerEnd);
        var least = grid.Cells(LeastRow);
        var rows = Math.Clamp(grid.Within(room) / least, 1, CalendarSizing.MonthWeeks);
        var rowHeight = Math.Max(least, grid.Within(room) / rows) * pitch;
        var first = rows < CalendarSizing.MonthWeeks ? Math.Clamp(anchorRow - ((rows - 1) / 2), 0, CalendarSizing.MonthWeeks - rows) : 0;

        var column = columns.Min();
        var pad = FamilySpace.S1;
        var chipLine = CalendarFit.ChipLine;
        var lanesFit = (int)Math.Floor((rowHeight - pad - FamilyType.Caption.LineHeight + 1e-6) / chipLine);
        var lanes = column >= ChipColumn ? Math.Clamp(Math.Min(lanesFit, Math.Max(0, mostItems)), 0, MonthPlan.MaxLines) : 0;
        var dotRoom = CalendarFit.Dot + FamilySpace.S1;
        var face = lanes >= 1 ? MonthFace.Chips
            : mostItems > 0 && rowHeight - (2 * pad) - FamilyType.Caption.LineHeight >= dotRoom ? MonthFace.Dots
            : MonthFace.Number;
        var tall = face switch
        {
            MonthFace.Chips => rowHeight - pad - (lanes * chipLine),
            MonthFace.Dots => rowHeight - (2 * pad) - dotRoom,
            _ => rowHeight - (2 * pad),
        };
        var number = Math.Floor(Math.Clamp(Math.Min(tall, column / 2), FamilyType.Caption.LineHeight, MostNumberLine));
        return new MonthLayout(columns, headerEnd - top, rows, first, rowHeight, face, lanes, number);
    }
}
