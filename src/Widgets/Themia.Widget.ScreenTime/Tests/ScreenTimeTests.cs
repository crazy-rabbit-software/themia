using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ScreenTime.Tests;

/// <summary>[S-11500]..[S-11502] The screen-time reading: its one height, its words, its menu.</summary>
public sealed class ScreenTimeTests
{
    private static WidgetDescriptor Kind => new ScreenTimeModule().Descriptor;

    [Fact]
    public void S11500_one_height_the_28_and_its_line_under_160_to_320_wide()
    {
        var sizing = Kind.Sizing(new ScreenTimeConfig());
        var height = FamilySpace.ContentTop + FamilyType.Big28.LineHeight + FamilyType.Under(FamilyType.Big28).LineHeight + FamilySpace.Inset;
        Assert.Equal(new SizeDip(200, height), sizing.Natural);
        Assert.Equal((160, 320), (sizing.Min.Width, sizing.Max.Width));
        Assert.True(sizing.FixedHeight);
        Assert.Empty(sizing.Problems());
        Assert.Null(Kind.SizeSets);
        Assert.False(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));

        // The widest figure fits its least width inside the inset.
        Assert.True(ReadoutRules.Width(FamilyType.Big28, new ReadoutText(ScreenTimeRules.FormatDuration(ScreenTimeReading.WorstSeconds))) <= sizing.Min.Width - (2 * FamilySpace.Inset));
    }

    [Fact]
    public void S11501_a_window_says_its_average_and_limit_a_day_its_limit()
    {
        var hour = 3600L;
        var week = ScreenTimeReading.Headline(window: true, total: 28 * hour, days: 7, dayLimit: 6 * hour);
        Assert.Equal(28 * hour, week.Seconds);
        Assert.Equal($"{Str.Screentime_PerDay.Format(("value", ScreenTimeRules.FormatDuration(4 * hour)))} · {ScreenTimeStr.Screentime_GoalPerDay.Format(("value", ScreenTimeRules.FormatLimit(6 * hour)))}", week.Line);
        Assert.False(week.Over);
        Assert.True(ScreenTimeReading.Headline(true, 49 * hour, 7, 6 * hour).Over);

        var day = ScreenTimeReading.Headline(window: false, total: 7 * hour, days: 1, dayLimit: 6 * hour);
        Assert.Equal(ScreenTimeStr.Screentime_OfGoal.Format(("value", ScreenTimeRules.FormatLimit(6 * hour))), day.Line);
        Assert.True(day.Over);
        Assert.Equal(string.Empty, ScreenTimeReading.Headline(false, hour, 1, null).Line);
    }

    [Fact]
    public void S11502_its_menu_is_the_period_then_the_shared_lines_and_a_new_one_reads_the_week()
    {
        var config = new ScreenTimeConfig();
        Assert.Equal(ScreenTimePeriod.Week, config.Period);
        Assert.Equal(ScreenTimePeriod.Month, ((ScreenTimeConfig)new ScreenTimeConfig { Period = ScreenTimePeriod.Month }.Clone()).Period);

        var lines = new WidgetMenuLines(() => { }, _ => { });
        new ScreenTimeModule().MenuLines(lines, config);
        Assert.Equal(Str.Screentime_Cfg_Period.Text(), lines.Lines[0].Text);
        Assert.Equal(3, lines.Lines[0].Children!.Count);
        Assert.Contains(lines.Lines, l => l.Text == Str.WidgetsC_ScreenTime_NotRecording.Text() && l.Note);
    }
}
