using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.ScreenTimeChart.Tests;

/// <summary>[S-3332] The chart's ladder. Moved with the widget from Core's ScreenTimeLayoutTests (the donut's and the list's with theirs), unchanged in what they assert (the ladder is ScreenTimeChartLayout's now).</summary>
public sealed class ScreenTimeChartTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = 44;

    private static WidgetArea Content(WidgetTier tier) => new(tier.Width, tier.Height - Header);

    public static TheoryData<string> Tiers()
    {
        var data = new TheoryData<string>();
        foreach (var tier in WidgetTiers.All)
        {
            data.Add(tier.Name);
        }

        return data;
    }

    private static WidgetTier Tier(string name) => WidgetTiers.All.First(t => t.Name == name);

    // ---- [S-3332] chart -----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3332_the_chart_never_drops_its_drawing_or_overspends(string name)
    {
        var area = Content(Tier(name));

        foreach (var style in Enum.GetValues<ScreenTimeChartStyle>())
        {
            var plan = ScreenTimeChartLayout.Chart(area, 7, style);
            var spent = plan.Drawing
                + (plan.Total is { } total ? total.LineHeight + ScreenTimeLayout.Gap : 0)
                + (plan.Labels ? FluentType.Caption.LineHeight + 2 + ScreenTimeLayout.Gap : 0)
                + (plan.Detail ? FluentType.Caption.LineHeight + ScreenTimeLayout.Gap : 0);

            Assert.True(plan.Drawing >= ScreenTimeChartLayout.MinDrawing);
            Assert.True(spent <= Math.Max(area.Height - ScreenTimeLayout.Gap, ScreenTimeChartLayout.MinDrawing) + 0.5, $"{name} {style}: {spent} > {area.Height}");
        }
    }

    [Fact]
    public void S3332_the_figure_comes_before_the_labels_and_the_detail()
    {
        var xs = ScreenTimeChartLayout.Chart(Content(Tier("XS")), 7, ScreenTimeChartStyle.Bars);
        var m = ScreenTimeChartLayout.Chart(Content(Tier("M")), 7, ScreenTimeChartStyle.Bars);
        var l = ScreenTimeChartLayout.Chart(Content(Tier("L")), 7, ScreenTimeChartStyle.Bars);

        Assert.NotNull(m.Total);
        Assert.True(m.Labels);
        Assert.True(m.Detail);
        Assert.True(l.Total!.Value.FontSize > m.Total!.Value.FontSize);
        Assert.True(l.Drawing > m.Drawing);
        Assert.False(xs.Labels && xs.Total is null);
    }

    [Fact]
    public void S3332_a_month_and_a_calendar_carry_no_weekday_row()
    {
        Assert.False(ScreenTimeChartLayout.Chart(Content(Tier("L")), 30, ScreenTimeChartStyle.Bars).Labels);
        Assert.False(ScreenTimeChartLayout.Chart(Content(Tier("L")), 7, ScreenTimeChartStyle.Heatmap).Labels);
    }
}
