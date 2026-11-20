using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>
/// [S-11530]..[S-11532] The habit tracker's sizes, from its config alone, on the family's tokens: each
/// style its own. The rows' styles (Dots, Checklist) are the habits' rows and the add row (28) at their optimal (whole
/// grid cells), the config's rows a new one's height; the heatmap one grid for the list (one height) or a grid per habit
/// (the config's rows a new one's). [S-14500] Every cell between: the rows the card holds stretch to fill it. Pure.
/// </summary>
public static class HabitSizing
{
    public const int MinRows = 1;

    public const int MaxRows = 12;

    public const int DefaultRows = 4;

    /// <summary>The narrowest a rows' card reads at: the check, a name worth reading.</summary>
    public const double Narrowest = 220;

    /// <summary>A new one's width.</summary>
    public const double NaturalWidth = 300;

    /// <summary>The widest a card grows: past it a row is a sentence across a room.</summary>
    public const double Widest = 520;

    /// <summary>The narrowest a heatmap reads at: a few months of weeks.</summary>
    public const double HeatmapNarrowest = 200;

    public static int ClampRows(int rows) => Math.Clamp(rows, MinRows, MaxRows);

    /// <summary>[S-11531] The lines a row draws: one in a checklist, the config's (1 or 2) in the dots.</summary>
    public static int Lines(HabitTrackerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Style == HabitTrackerStyle.Dots ? Math.Clamp(config.Lines, 1, 2) : 1;
    }

    /// <summary>[S-11531] A dots row's parts but its dots: the inset either side, the check, the name's room and the run as the config shows it.</summary>
    public static double DotsChrome(HabitTrackerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = (2 * FamilySpace.Inset) + HabitLayout.BoxWidth + HabitLayout.NameRoom;
        if (config.ShowStreak)
        {
            width += HabitLayout.ColumnGap + (config.RunWords ? HabitLayout.StreakWordsWidth : HabitLayout.StreakNumberWidth);
        }

        return width;
    }

    /// <summary>
    /// [S-1426] [S-14410] How many past periods a dots row shows in a card <paramref name="width"/> wide: as many dots as
    /// the room past the check, the name's room and the run holds, 0..<see cref="HabitRules.MaxHistory"/>; fewer than
    /// <see cref="HabitLayout.MinDots"/> are no history (none). The card's, never a setting (owner, 2026-11-20:
    /// "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows the card's size").
    /// </summary>
    public static int HistoryFor(HabitTrackerConfig config, double width)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style != HabitTrackerStyle.Dots || !double.IsFinite(width))
        {
            return 0;
        }

        var room = width - DotsChrome(config) - HabitLayout.ColumnGap + HabitLayout.DotGap;
        var dots = Math.Clamp((int)Math.Floor((room + LayoutMath.Tolerance) / HabitLayout.DotPitch), 0, HabitRules.MaxHistory);
        return dots < HabitLayout.MinDots ? 0 : dots;
    }

    /// <summary>[S-11532] One habit's block in a grid per habit: its name, 4, its grid of seven rows.</summary>
    public static double HabitBlock => FamilyType.Body.LineHeight + HabitHeatmap.NameGap + HabitHeatmap.GridOf(HabitHeatmap.PerHabitCell);

    /// <summary>[S-11530]..[S-11532] The sizing of <paramref name="config"/>.</summary>
    public static WidgetSizing Sizing(HabitTrackerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = ClampRows(config.Rows);

        if (config.Style == HabitTrackerStyle.Heatmap)
        {
            if (!config.PerHabit)
            {
                var height = FamilySpace.ContentTop + HabitHeatmap.GridOf(HabitHeatmap.SharedCell) + FamilySpace.Inset;
                return WidgetSizing.Range(new(HeatmapNarrowest, height), new(NaturalWidth, height), new(Widest, height));
            }

            double Blocks(int n) => FamilySpace.ContentTop + FamilySpace.Inset - HabitHeatmap.BlockGap + (n * (HabitBlock + HabitHeatmap.BlockGap));
            return WidgetSizing.Range(new(HeatmapNarrowest, Blocks(MinRows)), new(NaturalWidth, Blocks(rows)), new(Widest, Blocks(MaxRows)));
        }

        var least = config.Style == HabitTrackerStyle.Dots ? Math.Max(Narrowest, Math.Ceiling(DotsChrome(config) / FamilySpace.S1) * FamilySpace.S1) : Narrowest;

        // [S-14500] The habits' rows and the add row under them at their optimal, from the card's top edge to its foot edge.
        var grid = ItemGrid.Default();
        var row = ItemStretch.Optimal(FamilyShape.RowHeight(Lines(config)), grid);
        var add = ItemStretch.Optimal(FamilyShape.Row1, grid);
        return WidgetSizing.Range(new(least, add + row), new(Math.Max(least, NaturalWidth), add + (rows * row)), new(Math.Max(least, Widest), add + (MaxRows * row)));
    }
}
