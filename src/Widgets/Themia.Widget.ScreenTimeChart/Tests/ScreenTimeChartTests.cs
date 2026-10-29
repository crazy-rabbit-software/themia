using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeChart.Tests;

/// <summary>[S-3331] [S-3332] The chart's ladder on the family's Readout and Chart, and the calendar's cells.</summary>
public sealed class ScreenTimeChartTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = 44;

    private const string Worst = "888h 59m";

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
            var plan = ScreenTimeChartLayout.Chart(area, style, Worst);
            var spent = plan.Drawing
                + (plan.Total is { } total ? total.LineHeight + FamilySpace.S2 : 0)
                + (plan.Under ? FamilyType.Under(plan.Total!.Value).LineHeight : 0);

            // The labels' row is the drawing's own: the bars above it keep the floor.
            Assert.True(plan.Drawing - (plan.Labels ? ScreenTimeChartLayout.LabelRow : 0) >= ScreenTimeChartLayout.MinDrawing);
            Assert.True(spent <= Math.Max(area.Height - FamilySpace.Inset, ScreenTimeChartLayout.MinDrawing) + 0.5, $"{name} {style}: {spent} > {area.Height}");
        }
    }

    [Fact]
    public void S3332_the_figure_is_the_familys_readout_and_comes_before_the_labels_and_the_line_under()
    {
        var s = ScreenTimeChartLayout.Chart(Content(Tier("S")), ScreenTimeChartStyle.Bars, Worst);
        var m = ScreenTimeChartLayout.Chart(Content(Tier("M")), ScreenTimeChartStyle.Bars, Worst);
        var l = ScreenTimeChartLayout.Chart(Content(Tier("L")), ScreenTimeChartStyle.Bars, Worst);

        Assert.Equal(FamilyType.Big28, s.Total);
        Assert.Equal(FamilyType.Big40, m.Total);
        Assert.Equal(FamilyType.Big68, l.Total);
        Assert.True(m.Labels && m.Under);
        Assert.True(l.Drawing > m.Drawing);
    }

    [Fact]
    public void S3331_a_calendar_carries_no_end_labels()
    {
        Assert.True(ScreenTimeChartLayout.Chart(Content(Tier("L")), ScreenTimeChartStyle.Bars, Worst).Labels);
        Assert.False(ScreenTimeChartLayout.Chart(Content(Tier("L")), ScreenTimeChartStyle.Calendar, Worst).Labels);
    }

    [Fact]
    public void S3331_calendar_cells_are_rings_under_their_letters_dots_when_small_and_the_latest_weeks_when_short()
    {
        // A week on a roomy drawing: rings of at most 40 under the letters.
        var week = ScreenTimeChartLayout.Calendar(528, 300, 1);
        Assert.Equal(ScreenTimeChartLayout.MaxCell, week.Cell);
        Assert.True(week.Header);
        Assert.Equal(1, week.Rows);

        // Too narrow for a 16 ring: dots without letters.
        var narrow = ScreenTimeChartLayout.Calendar(88, 40, 1);
        Assert.InRange(narrow.Cell, ScreenTimeChartLayout.MinCell, ScreenTimeChartLayout.RingCell - 1);
        Assert.False(narrow.Header);

        // A month of five weeks in a short drawing: the latest weeks that fit.
        var month = ScreenTimeChartLayout.Calendar(288, 40, 5);
        Assert.True(month.Rows < 5);
        Assert.True(month.Cell >= ScreenTimeChartLayout.MinCell);
        Assert.True((month.Rows * month.Cell) + ((month.Rows - 1) * FamilySpace.S1) + (month.Header ? ScreenTimeChartLayout.CalendarHeader : 0) <= 40);

        // Every cell's side is on the 4 grid, so seven columns never drift off whole device pixels.
        foreach (var plan in new[] { week, narrow, month, ScreenTimeChartLayout.Calendar(288, 150, 1), ScreenTimeChartLayout.Calendar(167, 300, 5) })
        {
            Assert.Equal(0, plan.Cell % FamilySpace.S1);
        }
    }

    [Fact]
    public void S3331_bars_stand_8_apart_for_a_week_and_4_for_a_month()
    {
        Assert.Equal(FamilySpace.S2, ScreenTimeChartLayout.BarGap(7));
        Assert.Equal(FamilySpace.S1, ScreenTimeChartLayout.BarGap(30));
    }
}
