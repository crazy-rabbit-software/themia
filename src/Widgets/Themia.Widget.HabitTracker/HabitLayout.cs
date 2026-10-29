using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>What the habit list is asked to hold, independent of the tile's size.</summary>
/// <param name="Count">Habits in the list.</param>
/// <param name="Writable">Whether a habit can be added (a list is chosen and the pool is writable).</param>
/// <param name="ShowStreak">The "Show streaks" setting.</param>
/// <param name="HistoryDots">The configured dot count, already clamped.</param>
/// <param name="Checklist">[S-6410] The checklist style: the current period only, so nothing beside the names.</param>
public readonly record struct HabitLayoutInput(int Count, bool Writable, bool ShowStreak, int HistoryDots, bool Checklist = false);

/// <summary>
/// [S-1427] One layout pass's decisions for the Habits widget, made together so the rows and the
/// add row never disagree.
/// </summary>
/// <param name="Lines">[S-6712] The lines each row shows (1 to 3): the name; the rate; the best run.</param>
/// <param name="RowHeight">The rows' pitch, by their lines (32, 48 or 64).</param>
/// <param name="Rows">Rows the viewport shows whole; the rest scroll.</param>
/// <param name="AddRow">The "Add a habit" footer pinned under the list ([S-3850]).</param>
/// <param name="Streak">The current run as each row's value.</param>
/// <param name="StreakWords">The run with its unit ("12 days") rather than a bare number.</param>
/// <param name="Dots">History dots per row (0 for none): the configured count, or fewer when narrow.</param>
public sealed record HabitLayoutPlan(
    int Lines,
    double RowHeight,
    int Rows,
    bool AddRow,
    bool Streak,
    bool StreakWords,
    int Dots);

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

    /// <summary>[S-3802] Move the row's habit one place up; the focus goes with it.</summary>
    MoveUp,

    /// <summary>[S-3802] Move the row's habit one place down.</summary>
    MoveDown,
}

public readonly record struct HabitKeyResult(HabitKeyCommand Command, int Index);

/// <summary>
/// [S-1427]/[S-1428] The Habits widget's size ladder and keys. Pure, so each tier and each edge is
/// pinned by a test rather than by looking at a render.
/// <para>
/// A habit is the family's Row ([S-6711]): its check as the lead, the name, the history dots as the
/// row's inline meter and the current run as its value; the rate and the best run are its second
/// and third lines, as many as the tier gives a row ([S-6712]). The width rungs, from what XS must
/// show to what only a wide tile has room for: the habits (check + name) → the run (from Regular
/// width; spelled out from Expanded) → the history dots (from Regular width, the configured count or
/// as many as fit, at least <see cref="MinDots"/>, oldest dropped first — the dots yield width, never
/// the name) → the add footer ([S-3850]: from Regular height; always when the list is empty). Nothing
/// else: no summary strip, and height the rows do not use stays empty rather than filled with a chart
/// (user decision: a large tile shows more of the widget's own rows, not new parts).
/// </para>
/// </summary>
public static class HabitLayout
{
    /// <summary>The check and the gap after it: the row's 16 lead and its 8.</summary>
    public const double BoxWidth = FamilyShape.Icon + FamilySpace.GlyphGap;

    /// <summary>The narrowest a name may get before anything optional is dropped for it.</summary>
    public const double NameMinWidth = 80;

    /// <summary>Space before each trailing part: the row's name-to-meter and meter-to-value gap.</summary>
    public const double ColumnGap = FamilySpace.ColumnGap;

    public const double StreakNumberWidth = 28;

    public const double StreakWordsWidth = 72;

    /// <summary>One history dot (8, round) and the gap after it (4).</summary>
    public const double DotPitch = DotSize + DotGap;

    public const double DotSize = FamilySpace.S2;

    public const double DotGap = FamilySpace.S1;

    /// <summary>Fewer dots than this are not a history: none are shown instead.</summary>
    public const int MinDots = 3;

    public static HabitLayoutPlan Plan(WidgetArea area, HabitLayoutInput input)
    {
        var count = Math.Max(0, input.Count);

        // ---- width: the parts beside each name, in priority order ---------------------------------
        // [S-6410] A checklist is the check and the name: no run, dots, rate or best run at any size.
        var spare = input.Checklist ? -1 : area.Width - (2 * FamilySpace.Inset) - BoxWidth - NameMinWidth;

        var streak = !input.Checklist && input.ShowStreak && area.WidthClass >= SizeClass.Regular && Spend(ref spare, StreakNumberWidth);
        var words = streak && area.WidthClass >= SizeClass.Expanded && Spend(ref spare, StreakWordsWidth - StreakNumberWidth - ColumnGap);

        var dots = 0;
        if (!input.Checklist && input.HistoryDots > 0 && area.WidthClass >= SizeClass.Regular)
        {
            dots = Math.Clamp((int)Math.Floor((spare - ColumnGap + DotGap) / DotPitch), 0, Math.Min(input.HistoryDots, HabitRules.MaxHistory));
            dots = dots >= Math.Min(MinDots, input.HistoryDots) ? dots : 0;
        }

        // ---- lines: the rate, then the best run (a streak, so only with Show streaks) --------------
        var wanted = input.Checklist ? 1 : input.ShowStreak ? 3 : 2;
        var lines = RowRules.Lines(area, wanted);
        var row = FamilyShape.RowHeight(lines);

        // ---- height: whole rows above the card's bottom inset ---------------------------------------
        var room = new Disclosure(area.Width, area.Height - FamilySpace.Inset);
        room.Reserve(row);

        var addRow = AddFooter.Show(room, area, input.Writable, count == 0, FamilyShape.Row1);
        var rows = count == 0 ? 0 : 1 + room.TakeRows(row, count - 1);

        return new HabitLayoutPlan(lines, row, rows, addRow, streak, words, dots);
    }

    /// <summary>
    /// [S-1428] A key on the row at <paramref name="index"/> of <paramref name="count"/>. The arrows
    /// clamp at the ends; Tab and Shift+Tab wrap ("next one", not a direction); Enter ticks, as the
    /// box does; F2 renames; Delete removes; Escape gives the keyboard back; Alt+Shift+Up / Down moves
    /// the habit ([S-3802]).
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

            // [S-3802] The ends have nowhere further to go.
            WidgetKey.MoveUp when at > 0 => new(HabitKeyCommand.MoveUp, at),
            WidgetKey.MoveDown when at < count - 1 => new(HabitKeyCommand.MoveDown, at),
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
