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
/// [S-12832] [S-14500] A month laid on the card: the columns' widths (edge to edge, sharing the width), the weekday line's
/// height (ending on a grid line), the week rows shown (sharing the room under it, all alike) and the first of the six,
/// each cell's face and its chip lanes, and the number's line (it grows with the cell).
/// </summary>
public sealed record MonthLayout(IReadOnlyList<double> Columns, double HeaderHeight, int Rows, int FirstRow, double RowHeight, MonthFace Face, int ChipLanes, double NumberLine)
{
    /// <summary>The narrowest column.</summary>
    public double LeastColumn => Columns.Count == 0 ? 0 : Columns.Min();
}

/// <summary>
/// [S-14101] The room a month's cell must have before it shows its day's events as chips: the chip lines under its number
/// (<see cref="Lanes"/>) and its column's width (<see cref="Column"/>).
/// </summary>
public readonly record struct MonthChipRoom(int Lanes, double Column)
{
    /// <summary>
    /// [S-14101] [S-15782] A dots month (owner, 2026-11-20: "the calendar shows the individual events too early, the items
    /// should stay small for a little longer"; WR-9: "A day cell keeps its small marks until it clearly holds a readable chip
    /// line"): its cells keep their dots until they hold two chip lines under the number (one more than a month with titles
    /// shows chips from: the "little longer") in a column where a chip's title reads, a month with titles' least (56).
    /// </summary>
    public static MonthChipRoom Dots => new(2, CalendarSizing.MonthTitleColumn);

    /// <summary>[S-11547] A month with titles, chosen: a chip line in a column of its least, 56.</summary>
    public static MonthChipRoom Titles => new(1, CalendarSizing.MonthTitleColumn);
}

/// <summary>
/// [S-12832] The month's one layout rule (owner, 2026-11-19: "the calendar does not follow this design philosophy for
/// example. fix that"; "Bigger numbers too"; "the calendar does not match the fucking design rules i told you"; "and the
/// items dont reach from edge to edge"). The day cells tile the card: seven columns from its left edge to its right,
/// sharing the width evenly, the weeks from under the weekday line to the card's foot, as many of the six as hold a day's
/// number at its optimal (whole cells; the anchor's week among them), sharing that room evenly ([S-14500]: nothing left
/// over at the foot at any size). A cell is filled by its content: its day's events as chips under its number where it
/// has the room for them (<see cref="MonthChipRoom"/>; Outlook's month view),
/// its dots where it does not, its number alone on a day of nothing; the number grows with the cell, as large as what
/// the chips or dots leave of its height and its two figures across the column allow ([S-15782], <see cref="NumberFits"/>).
/// </summary>
public static class MonthLayouts
{
    /// <summary>The least a week's row holds: a day's number line and the cell's 4.</summary>
    public static double LeastRow => FamilyType.Caption.LineHeight + FamilySpace.S1;

    /// <summary>The largest a day's number line grows.</summary>
    public const double MostNumberLine = 64;

    /// <summary>[S-15782] A figure's advance in Segoe UI Variable, a fraction of its size (tabular figures).</summary>
    public const double FigureAdvance = 0.56;

    /// <summary>
    /// [S-15782] The largest number line a day's two figures fit across in a column <paramref name="column"/> wide, inside the
    /// cell's 4 on each side (WR-9, "Bigger numbers too": the number grows with its cell until its width stops it, no longer
    /// at half the column's width).
    /// </summary>
    public static double NumberFits(double column)
        => Math.Max(0, column - (2 * FamilySpace.S1)) / (2 * FigureAdvance) * (FamilyType.Caption.LineHeight / FamilyType.Caption.FontSize);

    /// <summary>
    /// The month in a card <paramref name="width"/> wide whose month starts <paramref name="top"/> under the card's top
    /// edge and ends at its foot, <paramref name="cardHeight"/> under it, on <paramref name="grid"/>: the anchor's week is
    /// <paramref name="anchorRow"/> (0..5), and the most items a day shown has is <paramref name="mostItems"/>.
    /// </summary>
    /// <param name="chips">[S-14101] The room a cell must have before it shows chips: a dots month's (the default) or a month with titles'.</param>
    public static MonthLayout Plan(double width, double cardHeight, double top, ItemGrid grid, int anchorRow, int mostItems, MonthChipRoom? chips = null)
    {
        var need = chips ?? MonthChipRoom.Dots;
        width = double.IsFinite(width) ? Math.Max(0, width) : 0;
        var columns = ItemSize.Columns(width, 7, grid);

        var headerEnd = grid.Whole(top + FamilyType.Caption.LineHeight);
        var room = Math.Max(0, cardHeight - headerEnd);
        var least = ItemSize.Optimal(LeastRow, grid);
        var rows = Math.Clamp((int)Math.Floor((room + LayoutMath.Tolerance) / least), 1, CalendarSizing.MonthWeeks);
        var rowHeight = Math.Max(least, room / rows);
        var first = rows < CalendarSizing.MonthWeeks ? Math.Clamp(anchorRow - ((rows - 1) / 2), 0, CalendarSizing.MonthWeeks - rows) : 0;

        var column = columns.Min();
        var pad = FamilySpace.S1;
        var chipLine = CalendarFit.ChipLine;
        var lanesFit = (int)Math.Floor((rowHeight - pad - FamilyType.Caption.LineHeight + 1e-6) / chipLine);
        var lanes = column >= need.Column && lanesFit >= Math.Max(1, need.Lanes) ? Math.Clamp(Math.Min(lanesFit, Math.Max(0, mostItems)), 0, MonthPlan.MaxLines) : 0;
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
        var number = Math.Floor(Math.Clamp(Math.Min(tall, NumberFits(column)), FamilyType.Caption.LineHeight, MostNumberLine));
        return new MonthLayout(columns, headerEnd - top, rows, first, rowHeight, face, lanes, number);
    }
}
