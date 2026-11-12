using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Habits;

/// <summary>
/// [S-11530]..[S-11532] The habit tracker's sizes, from its config alone, on the family's tokens: each
/// style its own. The rows' styles (Dots, Checklist) are 36 + the habits' rows + the add footer (28) +
/// 12, stepped by a row; the heatmap one grid for the list (one height) or a grid per habit (stepped by
/// a habit). Pure.
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

    /// <summary>[S-11531] The least width of a dots row: the check, the name's least, the dots and the run as the config shows them.</summary>
    public static double DotsWidth(HabitTrackerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = (2 * FamilySpace.Inset) + HabitLayout.BoxWidth + HabitLayout.NameMinWidth;
        if (config.EffectiveHistoryDots > 0)
        {
            width += HabitLayout.ColumnGap + (config.EffectiveHistoryDots * HabitLayout.DotPitch) - HabitLayout.DotGap;
        }

        if (config.ShowStreak)
        {
            width += HabitLayout.ColumnGap + (config.RunWords ? HabitLayout.StreakWordsWidth : HabitLayout.StreakNumberWidth);
        }

        return Math.Ceiling(width / FamilySpace.S1) * FamilySpace.S1;
    }

    /// <summary>[S-11532] One habit's block in a grid per habit: its name, 4, its grid of seven rows.</summary>
    public static double HabitBlock => FamilyType.Body.LineHeight + HabitHeatmap.NameGap + HabitHeatmap.GridOf(HabitHeatmap.PerHabitCell);

    /// <summary>[S-11530]..[S-11532] The sizing of <paramref name="config"/>, with its header.</summary>
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

            var steps = new SizeSteps(FamilySpace.ContentTop + FamilySpace.Inset - HabitHeatmap.BlockGap, HabitBlock + HabitHeatmap.BlockGap, MinRows, MaxRows, rows);
            return WidgetSizing.Range(new(HeatmapNarrowest, 1), new(NaturalWidth, 1), new(Widest, 1)).WithHeightSteps(steps);
        }

        var least = config.Style == HabitTrackerStyle.Dots ? Math.Max(Narrowest, DotsWidth(config)) : Narrowest;
        var rowSteps = new SizeSteps(FamilySpace.ContentTop + FamilyShape.Row1 + FamilySpace.Inset, FamilyShape.RowHeight(Lines(config)), MinRows, MaxRows, rows);
        return WidgetSizing.Range(new(least, 1), new(Math.Max(least, NaturalWidth), 1), new(Math.Max(least, Widest), 1)).WithHeightSteps(rowSteps);
    }

    /// <summary>[S-2442] The config a card of <paramref name="size"/> means: the habits that height holds. A copy when they change.</summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config is not HabitTrackerConfig habits || Sizing(habits).HeightSteps is not { FollowsConfig: true } steps)
        {
            return config;
        }

        var wanted = steps.Nearest(size.Height);
        if (wanted == habits.Rows)
        {
            return config;
        }

        var copy = (HabitTrackerConfig)habits.Clone();
        copy.Rows = wanted;
        return copy;
    }
}
