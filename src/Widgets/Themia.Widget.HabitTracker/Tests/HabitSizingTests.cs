using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-11530]..[S-11532] The habit tracker's sizes: each style its own, from the config alone.</summary>
public sealed class HabitSizingTests
{
    private static WidgetDescriptor Kind => new HabitTrackerModule().Descriptor;

    [Fact]
    public void S11530_four_habits_of_one_line_over_the_add_footer_the_height_following_the_rows()
    {
        var sizing = Kind.Sizing(new HabitTrackerConfig());
        Assert.Equal(new SizeDip(300, (4 * 30) + 30), sizing.Natural);
        Assert.Equal(Themia.Sdk.SizeStretch.Ceiling, sizing.Max.Width); // relaxed, [S-12411]: no practical Max
        Assert.True(sizing.HeightSteps!.FollowsConfig);
        Assert.Equal((1, 12), (sizing.HeightSteps.MinCount, sizing.HeightSteps.MaxCount));

        var two = Kind.Sizing(new HabitTrackerConfig { Rows = 6, Lines = 2 });
        Assert.Equal((6 * 40) + 30, two.Natural.Height);
    }

    [Fact]
    public void S11531_the_least_width_holds_the_check_a_name_and_the_run()
    {
        var config = new HabitTrackerConfig { RunWords = true };
        var least = (2 * FamilySpace.Inset) + HabitLayout.BoxWidth + HabitLayout.NameRoom
            + HabitLayout.ColumnGap + HabitLayout.StreakWordsWidth;
        var sizing = Kind.Sizing(config);
        Assert.True(sizing.Min.Width >= least);
        Assert.Empty(sizing.Problems());

        // A checklist's rows are one line and need no room for dots or a run.
        var checklist = Kind.Sizing(new HabitTrackerConfig { Style = HabitTrackerStyle.Checklist, Lines = 2 });
        Assert.Equal(HabitSizing.Narrowest, checklist.Min.Width);
        Assert.Equal((4 * 30) + 30, checklist.Natural.Height);
    }

    [Fact]
    public void S11531_S14410_the_history_is_as_many_dots_as_the_cards_width_holds()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows
        // the card's size": no History line; the dots take the width past the check, the name's room and the run.
        var config = new HabitTrackerConfig();
        var chrome = HabitSizing.DotsChrome(config) + HabitLayout.ColumnGap - HabitLayout.DotGap;

        Assert.Equal(7, HabitSizing.HistoryFor(config, chrome + (7 * HabitLayout.DotPitch)));
        Assert.Equal(7, HabitSizing.HistoryFor(config, chrome + (8 * HabitLayout.DotPitch) - 1));
        Assert.Equal(HabitRules.MaxHistory, HabitSizing.HistoryFor(config, 4000));

        // Under three dots is no history; a checklist and a heatmap have none at any width.
        Assert.Equal(0, HabitSizing.HistoryFor(config, chrome + (2 * HabitLayout.DotPitch)));
        Assert.Equal(3, HabitSizing.HistoryFor(config, chrome + (3 * HabitLayout.DotPitch)));
        Assert.Equal(0, HabitSizing.HistoryFor(new HabitTrackerConfig { Style = HabitTrackerStyle.Checklist }, 4000));
        Assert.Equal(0, HabitSizing.HistoryFor(new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap }, 4000));

        // A new card's natural width holds some history, ten dots' more width ten more.
        var natural = Kind.Sizing(config).Natural.Width;
        var shown = HabitSizing.HistoryFor(config, natural);
        Assert.InRange(shown, HabitLayout.MinDots, 20);
        Assert.Equal(shown + 10, HabitSizing.HistoryFor(config, natural + (10 * HabitLayout.DotPitch)));
        Assert.Null(typeof(HabitTrackerConfig).GetProperty("HistoryDots"));
    }

    [Fact]
    public void S11532_one_grid_is_one_height_and_a_grid_per_habit_steps_by_a_habit()
    {
        var shared = Kind.Sizing(new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap });
        Assert.Equal(shared.Min.Height, shared.Natural.Height);
        Assert.Equal(SizeStretch.Ceiling, shared.Max.Height); // relaxed, [S-12411]
        Assert.Equal(FamilySpace.ContentTop + HabitHeatmap.GridOf(HabitHeatmap.SharedCell) + FamilySpace.Inset, shared.Natural.Height);

        var each = Kind.Sizing(new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap, PerHabit = true, Rows = 3 });
        Assert.Equal(FamilySpace.ContentTop + (3 * HabitSizing.HabitBlock) + (2 * HabitHeatmap.BlockGap) + FamilySpace.Inset, each.Natural.Height);
    }

    [Fact]
    public void S11530_dragging_the_bottom_edge_sets_rows_on_a_copy_and_a_single_grid_keeps_its_config()
    {
        var config = new HabitTrackerConfig();
        var tall = (7 * 30) + 30;
        Assert.Equal(7, Assert.IsType<HabitTrackerConfig>(Kind.SizeSets!(config, new SizeDip(300, tall))).Rows);
        Assert.Equal(4, config.Rows);

        var heat = new HabitTrackerConfig { Style = HabitTrackerStyle.Heatmap };
        Assert.Same(heat, Kind.SizeSets!(heat, new SizeDip(300, 600)));
    }

    [Fact]
    public void S11530_rows_and_lines_out_of_range_are_repaired_and_a_copy_keeps_the_new_settings()
    {
        var config = new HabitTrackerConfig { Rows = 40, Lines = 3, RunWords = true, PerHabit = true };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal((12, 2), (config.Rows, config.Lines));

        var copy = (HabitTrackerConfig)config.Clone();
        Assert.Equal((12, 2, true, true), (copy.Rows, copy.Lines, copy.RunWords, copy.PerHabit));
    }
}
