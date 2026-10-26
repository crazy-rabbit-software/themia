using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ScreenTimeChart.Tests;

/// <summary>[S-3335] [S-3336] The chart's options on its properties menu: a week or a month, then the shared lines ([S-3323]); bars or a calendar is its style.</summary>
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
    public void S3335_the_first_level_is_appearance_the_period_and_the_shared_lines()
    {
        var lines = Build(new ScreenTimeChartConfig());

        Assert.Equal(Str.Screentime_Cfg_Period.Text(), lines.Lines[0].Text);
        Assert.True(Line(lines.Lines, Str.WidgetsC_ScreenTime_NotRecording.Text()).Note);
        Assert.Empty(lines.StyleLines);
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
    public void S3336_bars_or_the_calendar_is_the_style_and_no_line_of_its_own()
    {
        var config = new ScreenTimeChartConfig();

        Assert.Equal(["bars", "calendar"], WidgetStyles.For(config.Kind).Select(s => s.Name));
        Assert.DoesNotContain(Build(config).Lines, l => l.Children?.Any(c => c.Text == ScreenTimeChartStr.Styles_ScreenTimeChart_Calendar.Text()) == true);

        Assert.True(WidgetStyles.Set(config, (int)ScreenTimeChartStyle.Calendar));
        Assert.Equal(ScreenTimeChartStyle.Calendar, ((ScreenTimeChartConfig)config.Clone()).Style);
    }

    [Fact]
    public void S3336_it_is_stored_as_its_name_and_an_unknown_one_is_repaired_to_bars()
    {
        var config = new ScreenTimeChartConfig { Style = ScreenTimeChartStyle.Calendar };
        Assert.Contains("\"style\": \"calendar\"", System.Text.Json.JsonSerializer.Serialize(config, ScreenTimeChartJsonContext.Default.ScreenTimeChartConfig));

        var read = System.Text.Json.JsonSerializer.Deserialize("{\"style\": \"heat\"}", ScreenTimeChartJsonContext.Default.ScreenTimeChartConfig)!;
        Assert.NotNull(WidgetStyles.Repair(read));
        Assert.Equal(ScreenTimeChartStyle.Bars, read.Style);
    }
}
