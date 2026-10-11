using Themia.Core.Widgets;

namespace Themia.Core.Habits;

/// <summary>What the habit list is asked to hold, independent of the tile's size.</summary>
/// <param name="Count">Habits in the list.</param>
/// <param name="Writable">Whether a habit can be added (a list is chosen and the pool is writable).</param>
/// <param name="ShowStreak">The "Show streaks" setting.</param>
/// <param name="HistoryDots">The configured dot count, already clamped.</param>
public readonly record struct HabitLayoutInput(int Count, bool Writable, bool ShowStreak, int HistoryDots);

/// <summary>
/// [S-1427] One layout pass's decisions for the Habits widget, made together so the rows, the add
/// row, the summary and the chart never disagree.
/// </summary>
/// <param name="CompactRows">32px rows rather than 40.</param>
/// <param name="Inset">The rows' side padding: halved on a Compact-wide tile, so a name has room.</param>
/// <param name="Rows">Rows the viewport shows whole; the rest scroll.</param>
/// <param name="AddRow">The "Add a habit" row at the foot of the list.</param>
/// <param name="Summary">The strip above the list: done this period, the rate, a progress bar.</param>
/// <param name="ChartHeight">The completion chart under the list, or 0 for none.</param>
/// <param name="ChartBars">How many past periods the chart shows.</param>
/// <param name="Streak">The current run beside each name.</param>
/// <param name="StreakWords">The run with its unit ("12 days") rather than a bare number.</param>
/// <param name="Dots">History dots per row (0 for none): the configured count, or fewer when narrow.</param>
/// <param name="Rate">A per-row completion rate over the last <see cref="HabitRules.RateWindow"/> periods.</param>
/// <param name="Best">A per-row best run.</param>
/// <param name="SummaryBar">
/// [S-1571] The summary's progress bar under its line. False on a tile one height class down, where
/// the summary is its line alone, in the height the whole rows leave.
/// </param>
public sealed record HabitLayoutPlan(
    bool CompactRows,
    double RowHeight,
    double Inset,
    int Rows,
    bool AddRow,
    bool Summary,
    double ChartHeight,
    int ChartBars,
    bool Streak,
    bool StreakWords,
    int Dots,
    bool Rate,
    bool Best,
    bool SummaryBar = true);

/// <summary>[S-1428] What a key on a focused row does.</summary>
public enum HabitKeyCommand
{
    None,

    /// <summary>Move the focus to the row at the index.</summary>
    Focus,

    /// <summary>Tick or un-tick the row's habit — the same as its box.</summary>
    Toggle,

    Rename,

    Delete,

    /// <summary>Clear the highlight and give the keyboard back.</summary>
    Release,
}

public readonly record struct HabitKeyResult(HabitKeyCommand Command, int Index);

/// <summary>
/// [S-1427]/[S-1428] The Habits widget's size ladder and keys. Pure, so each tier and each edge is
/// pinned by a test rather than by looking at a render.
/// <para>
/// The ladder, from what XS must show to what only XL has room for: the habits (box + name) →
/// the run beside each (from Regular width; spelled out from Expanded) → the history dots (from
/// Regular width, the configured count or as many as fit, oldest dropped first — the dots yield
/// width, never the name) → the add row (from Regular height; always when the list is empty) →
/// a per-row rate (Expanded width) → a per-row best run (Huge width) → the summary strip (Expanded
/// height) → a completion chart in whatever height the rows leave (Expanded height, every row shown).
/// </para>
/// </summary>
public static class HabitLayout
{
    /// <summary>The checkbox and the gap after it.</summary>
    public const double BoxWidth = 32;

    /// <summary>The narrowest a name may get before anything optional is dropped for it.</summary>
    public const double NameMinWidth = 80;

    /// <summary>Space before each trailing column.</summary>
    public const double ColumnGap = 12;

    public const double StreakNumberWidth = 28;

    public const double StreakWordsWidth = 72;

    public const double RateWidth = 40;

    public const double BestWidth = 60;

    /// <summary>One history dot and the gap after it.</summary>
    public const double DotPitch = 12;

    public const double DotSize = 8;

    /// <summary>A caption line, a 4px bar, and the space around them.</summary>
    public const double SummaryHeight = 16 + 4 + 4 + 12;

    /// <summary>[S-1571] The summary as its line alone: a caption line and the space under it.</summary>
    public const double SummaryLineHeight = 16 + 12;

    /// <summary>Under this a chart is a smear, not a chart.</summary>
    public const double ChartMinHeight = 64;

    /// <summary>One bar and its gap.</summary>
    public const double BarPitch = 14;

    /// <summary>The list's own padding under its last row.</summary>
    public const double ListBottomPadding = 8;

    public static HabitLayoutPlan Plan(WidgetArea area, HabitLayoutInput input, double rowHeight, double compactRowHeight)
    {
        var compact = area.HeightClass == SizeClass.Compact || area.WidthClass == SizeClass.Compact;
        var row = compact ? compactRowHeight : rowHeight;
        var inset = area.WidthClass == SizeClass.Compact ? 8 : 16;
        var count = Math.Max(0, input.Count);

        // ---- width: the parts beside each name, in priority order ---------------------------------
        var spare = area.Width - (2 * inset) - BoxWidth - NameMinWidth;

        var streak = input.ShowStreak && area.WidthClass >= SizeClass.Regular && Spend(ref spare, StreakNumberWidth);
        var words = streak && area.WidthClass >= SizeClass.Expanded && Spend(ref spare, StreakWordsWidth - StreakNumberWidth - ColumnGap);

        var dots = 0;
        if (input.HistoryDots > 0 && area.WidthClass >= SizeClass.Regular)
        {
            dots = Math.Clamp((int)Math.Floor((spare - ColumnGap + (DotPitch - DotSize)) / DotPitch), 0, Math.Min(input.HistoryDots, HabitRules.MaxHistory));
            if (dots > 0)
            {
                spare -= ColumnGap + (dots * DotPitch) - (DotPitch - DotSize);
            }
        }

        var rate = area.WidthClass >= SizeClass.Expanded && Spend(ref spare, RateWidth);
        var best = area.WidthClass >= SizeClass.Huge && Spend(ref spare, BestWidth);

        // ---- height ------------------------------------------------------------------------------
        var room = new Disclosure(area.Width, area.Height - ListBottomPadding);
        room.Reserve(row);

        var addRow = input.Writable && (count == 0 || (area.HeightClass >= SizeClass.Regular && room.Take(row)));
        var summary = count > 0 && area.HeightClass >= SizeClass.Expanded && room.Take(SummaryHeight);
        var rows = count == 0 ? 0 : 1 + room.TakeRows(row, count - 1);

        // [S-1571] One height class down the rows rarely come out even: the height no whole row can
        // use (about a row's worth on an M tile) carries the summary's line, without its bar.
        var summaryLine = !summary && count > 0 && area.HeightClass >= SizeClass.Regular && room.Take(SummaryLineHeight);

        var chart = 0.0;
        var bars = 0;
        if (count > 0 && rows == count && area.HeightClass >= SizeClass.Expanded && area.WidthClass >= SizeClass.Regular
            && room.Remaining + Disclosure.Tolerance >= ChartMinHeight)
        {
            chart = room.Remaining;
            bars = Math.Clamp((int)Math.Floor((area.Width - (2 * inset) + (BarPitch / 2)) / BarPitch), 7, HabitRules.MaxHistory);
        }

        return new HabitLayoutPlan(compact, row, inset, rows, addRow, summary || summaryLine, chart, bars, streak, words, dots, rate, best, SummaryBar: summary);
    }

    /// <summary>
    /// [S-1428] A key on the row at <paramref name="index"/> of <paramref name="count"/>. The arrows
    /// clamp at the ends; Tab and Shift+Tab wrap ("next one", not a direction); Enter ticks, as the
    /// box does; F2 renames; Delete removes; Escape gives the keyboard back.
    /// [S-1982] Space ticks too, and Page Up / Page Down jump <see cref="ListKeys.PageStep"/> rows.
    /// </summary>
    /// <remarks>
    /// [S-812] With no row focused (<paramref name="index"/> negative) a movement key lands on an end
    /// (Up, Down, Home, Tab the first row; End, Shift+Tab the last) and the rest do nothing.
    /// </remarks>
    public static HabitKeyResult Decide(WidgetKey key, int index, int count)
    {
        if (count <= 0 || index < 0)
        {
            return key switch
            {
                WidgetKey.Escape => new(HabitKeyCommand.Release, -1),
                _ when count <= 0 => new(HabitKeyCommand.None, -1),
                WidgetKey.Up or WidgetKey.Down or WidgetKey.Home or WidgetKey.Tab or WidgetKey.PageUp or WidgetKey.PageDown => new(HabitKeyCommand.Focus, 0),
                WidgetKey.End or WidgetKey.ShiftTab => new(HabitKeyCommand.Focus, count - 1),
                _ => new(HabitKeyCommand.None, -1),
            };
        }

        var at = Math.Clamp(index, 0, count - 1);

        return key switch
        {
            WidgetKey.Up => new(HabitKeyCommand.Focus, Math.Max(0, at - 1)),
            WidgetKey.Down => new(HabitKeyCommand.Focus, Math.Min(count - 1, at + 1)),
            WidgetKey.Home => new(HabitKeyCommand.Focus, 0),
            WidgetKey.End => new(HabitKeyCommand.Focus, count - 1),
            WidgetKey.PageUp => new(HabitKeyCommand.Focus, Math.Max(0, at - ListKeys.PageStep)),
            WidgetKey.PageDown => new(HabitKeyCommand.Focus, Math.Min(count - 1, at + ListKeys.PageStep)),
            WidgetKey.Tab => new(HabitKeyCommand.Focus, (at + 1) % count),
            WidgetKey.ShiftTab => new(HabitKeyCommand.Focus, (at - 1 + count) % count),
            WidgetKey.Enter or WidgetKey.Space => new(HabitKeyCommand.Toggle, at),
            WidgetKey.Rename => new(HabitKeyCommand.Rename, at),
            WidgetKey.Delete => new(HabitKeyCommand.Delete, at),
            WidgetKey.Escape => new(HabitKeyCommand.Release, at),
            _ => new(HabitKeyCommand.None, at),
        };
    }

    /// <summary>
    /// The row the focus goes to after the one at <paramref name="index"/> was deleted from a list of
    /// <paramref name="count"/> (before the delete): the one that took its place, or the new last.
    /// </summary>
    public static int AfterDelete(int index, int count) => count <= 1 ? -1 : Math.Clamp(index, 0, count - 2);

    private static bool Spend(ref double spare, double width)
    {
        var cost = ColumnGap + width;

        if (spare + Disclosure.Tolerance < cost)
        {
            return false;
        }

        spare -= cost;
        return true;
    }
}
