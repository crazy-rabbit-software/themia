using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;
using static Themia.Core.Tests.StyleTiers;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6166] The stock widget's number style:
/// the rules each style draws by, at the seven size tiers (each card less its
/// ~44px header).
/// </summary>
public sealed class WeatherStylesTests
{
    // ---- stocks number [S-6166] --------------------------------------------------------------

    private static StockPlan Number(WidgetArea area)
        => StockLayout.Plan(area, new StockInputs(6, 10, Chart: false, Extended: false, MinMax: true, Number: true));

    [Fact]
    public void S6166_the_number_style_has_no_chart_and_its_change_on_a_line_of_its_own()
    {
        foreach (var tier in Tiers)
        {
            var plan = Number(tier);
            Assert.False(plan.Chart);
            Assert.True(plan.ExtrasUnder);
            Assert.True(plan.Price.LineHeight + StockLayout.Gap + plan.Change.LineHeight <= tier.Height, $"{tier}");
        }
    }

    [Fact]
    public void S6166_the_price_and_the_change_grow_with_the_tile()
    {
        var large = Number(Xl);

        Assert.True(large.Price.FontSize > FluentType.Display.FontSize, $"price {large.Price.FontSize}");
        Assert.True(large.Change.FontSize >= FluentType.Title.FontSize, $"change {large.Change.FontSize}");
        Assert.True(Number(M).Price.FontSize > StockLayout.Plan(M, new StockInputs(6, 10, true, false, true)).Price.FontSize);
    }

    [Fact]
    public void S6166_the_change_is_the_largest_step_within_half_the_price_and_never_under_body()
    {
        Assert.Equal(FluentType.Title, StockLayout.NumberChangeStep(FluentType.Display));
        Assert.Equal(FluentType.Body, StockLayout.NumberChangeStep(FluentType.Subtitle));
        Assert.Equal(FluentType.TitleLarge, StockLayout.NumberChangeStep(FluentType.Scaled(FluentType.Display, 1.5)));
    }
}
