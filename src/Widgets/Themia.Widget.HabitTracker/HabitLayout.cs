using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>What the habit list is asked to hold: its config's, never the card's size ([S-11530]).</summary>
/// <param name="Count">Habits in the list.</param>
/// <param name="Writable">Whether a habit can be added (a list is chosen and the pool is writable).</param>
/// <param name="ShowStreak">The "Show streaks" setting.</param>
/// <param name="HistoryDots">The configured dot count, already clamped.</param>
/// <param name="Checklist">[S-6410] The checklist style: the current period only, so nothing beside the names.</param>
/// <param name="Rows">[S-11530] The habits shown; more scroll inside them.</param>
/// <param name="Lines">[S-11531] Lines per row (1 or 2; a checklist's one).</param>
/// <param name="RunWords">[S-11531] The run spelled out.</param>
/// <param name="Heatmap">[S-6409] The heatmap: no rows, so the add footer only under an empty list.</param>
public readonly record struct HabitLayoutInput(int Count, bool Writable, bool ShowStreak, int HistoryDots, bool Checklist = false, int Rows = HabitSizing.DefaultRows, int Lines = 1, bool RunWords = false, bool Heatmap = false);

/// <summary>
/// [S-1427] One layout pass's decisions for the Habits widget, made together so the rows and the
/// add row never disagree.
/// </summary>
/// <param name="Lines">[S-11531] The lines each row shows (1 or 2): the name; the rate and the best run.</param>
/// <param name="RowHeight">The rows' pitch, by their lines (28, 40 or 56).</param>
/// <param name="Rows">Rows the viewport shows whole: the config's; the rest scroll.</param>
/// <param name="AddRow">The "Add a habit" footer pinned under the list ([S-3850]): always, where a habit can be added.</param>
/// <param name="Streak">The current run as each row's value.</param>
/// <param name="StreakWords">The run with its unit ("12 days") rather than a bare number.</param>
/// <param name="Dots">History dots per row (0 for none): the configured count.</param>
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
/// [S-1427] [S-11530] [S-1428] The Habits widget's rows and keys. Pure, so each edge is pinned by a test.
/// <para>
/// A habit is the family's Row ([S-6711]): its check as the lead, the name, the history dots as the
/// row's inline meter and the current run as its value; the rate (and, with the streaks, the best
/// run) its second line where its Lines are two. What a row shows is the config's at every width: the
/// card's least width holds them all ([S-11531], <see cref="HabitSizing.DotsWidth"/>), and the name
/// takes the rest, ending in an ellipsis.
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

    /// <summary>[S-11530] The rows as the config says: its rows, its lines, the run and the dots it asks for, the add footer always.</summary>
    public static HabitLayoutPlan Plan(HabitLayoutInput input)
    {
        var checklist = input.Checklist;
        var lines = checklist ? 1 : Math.Clamp(input.Lines, 1, 2);
        var streak = !checklist && input.ShowStreak;
        var dots = checklist ? 0 : Math.Clamp(input.HistoryDots, 0, HabitRules.MaxHistory);
        var addRow = input.Writable && (!input.Heatmap || input.Count == 0);
        return new HabitLayoutPlan(lines, FamilyShape.RowHeight(lines), HabitSizing.ClampRows(input.Rows), addRow, streak, streak && input.RunWords, dots);
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
}
