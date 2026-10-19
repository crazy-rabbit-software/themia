using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ScreenTimeChart.Tests;

/// <summary>[S-3335] The chart's options on its properties menu: a week or a month, bars or a calendar, then the shared lines ([S-3323]).</summary>
public sealed class ScreenTimeChartMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(ScreenTimeChartConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        ScreenTimeChartMenu.Lines(lines, config, null);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S3335_the_first_level_is_appearance_the_period_and_the_look()
    {
        var lines = Build(new ScreenTimeChartConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(Str.Screentime_Cfg_Period.Text(), lines.Lines[0].Text);
        Assert.Equal(ScreenTimeChartStr.Screentime_Cfg_Style.Text(), lines.Lines[1].Text);
        Assert.True(Line(lines.Lines, Str.WidgetsC_ScreenTime_NotRecording.Text()).Note);
    }

    [Fact]
    public void S3335_a_day_is_not_offered_and_reads_as_the_week()
    {
        var period = Line(Build(new ScreenTimeChartConfig { Period = ScreenTimePeriod.Day }).Lines, Str.Screentime_Cfg_Period.Text());

        Assert.Equal(
            [ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week), ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month)],
            period.Children!.Select(c => c.Text));
        Assert.True(period.Children![0].Checked);
    }

    [Fact]
    public void S3335_the_calendar_is_picked_once_and_bars_again_saves_nothing()
    {
        var config = new ScreenTimeChartConfig();
        var look = Line(Build(config).Lines, ScreenTimeChartStr.Screentime_Cfg_Style.Text()).Children!;

        Line(look, ScreenTimeChartStr.Screentime_Cfg_StyleBars.Text()).Action!();
        Assert.Equal(0, _saves);

        Line(look, ScreenTimeChartStr.WidgetsC_ScreenTime_Settings_StyleCalendar.Text()).Action!();
        Assert.Equal(ScreenTimeChartStyle.Heatmap, config.Style);
        Assert.Equal(1, _saves);
    }
}
