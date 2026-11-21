using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>What the habit list is asked to hold: its config's, never the card's size ([S-11530]).</summary>
/// <param name="Count">Habits in the list.</param>
/// <param name="Writable">Whether a habit can be added (a list is chosen and the pool is writable).</param>
/// <param name="ShowStreak">The "Show streaks" setting.</param>
/// <param name="HistoryDots">The dots the card's width holds (<see cref="HabitSizing.HistoryFor"/>).</param>
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

    /// <summary>[S-14410] The room a dots row keeps for its name before the history's dots take the rest of the width.</summary>
    public const double NameRoom = 120;

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
}
