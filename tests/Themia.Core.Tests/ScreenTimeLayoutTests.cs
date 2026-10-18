using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

public sealed class ScreenTimeLayoutTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = 44;

    /// <summary>A compact list row (Fluent's 32).</summary>
    private const double Row = 32;

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
            var plan = ScreenTimeLayout.Chart(area, 7, style);
            var spent = plan.Drawing
                + (plan.Total is { } total ? total.LineHeight + ScreenTimeLayout.Gap : 0)
                + (plan.Labels ? FluentType.Caption.LineHeight + 2 + ScreenTimeLayout.Gap : 0)
                + (plan.Detail ? FluentType.Caption.LineHeight + ScreenTimeLayout.Gap : 0);

            Assert.True(plan.Drawing >= ScreenTimeLayout.MinDrawing);
            Assert.True(spent <= Math.Max(area.Height - ScreenTimeLayout.Gap, ScreenTimeLayout.MinDrawing) + 0.5, $"{name} {style}: {spent} > {area.Height}");
        }
    }

    [Fact]
    public void S3332_the_figure_comes_before_the_labels_and_the_detail()
    {
        var xs = ScreenTimeLayout.Chart(Content(Tier("XS")), 7, ScreenTimeChartStyle.Bars);
        var m = ScreenTimeLayout.Chart(Content(Tier("M")), 7, ScreenTimeChartStyle.Bars);
        var l = ScreenTimeLayout.Chart(Content(Tier("L")), 7, ScreenTimeChartStyle.Bars);

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
        Assert.False(ScreenTimeLayout.Chart(Content(Tier("L")), 30, ScreenTimeChartStyle.Bars).Labels);
        Assert.False(ScreenTimeLayout.Chart(Content(Tier("L")), 7, ScreenTimeChartStyle.Heatmap).Labels);
    }

    // ---- [S-3342] donut -----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3342_the_ring_fits_the_tile(string name)
    {
        var area = Content(Tier(name));
        var plan = ScreenTimeLayout.Donut(area);

        Assert.True(plan.Ring <= area.Height, $"{name}: ring {plan.Ring} taller than {area.Height}");
        Assert.True(plan.Ring <= area.Width - (2 * ScreenTimeLayout.Inset) || plan.Ring <= ScreenTimeLayout.MinRing);
        Assert.Equal(ScreenTimeLayout.Thickness(plan.Ring), plan.Thickness);
    }

    [Fact]
    public void S3342_a_larger_tile_draws_a_larger_ring_and_names_more_in_the_hole()
    {
        var s = ScreenTimeLayout.Donut(Content(Tier("S")));
        var m = ScreenTimeLayout.Donut(Content(Tier("M")));
        var xl = ScreenTimeLayout.Donut(Content(Tier("XL")));

        Assert.True(m.Ring > s.Ring);
        Assert.True(xl.Ring > m.Ring);
        Assert.False(s.Detail);
        Assert.True(m.Detail);
    }
}
