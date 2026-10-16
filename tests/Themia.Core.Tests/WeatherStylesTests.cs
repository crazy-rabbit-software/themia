using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6160]..[S-6170] The styles of the weather, forecast, stock, exchange-rate and air widgets:
/// their menus, and the rules each style draws by, at the seven size tiers (each card less its
/// ~44px header).
/// </summary>
public sealed class WeatherStylesTests
{
    private static readonly WidgetArea Xs = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea Xl = new(800, 516);

    private static readonly WidgetArea[] Tiers = [Xs, S, M, Wide, Tall, L, Xl];

    private static string[] Names(WidgetKind kind) => [.. WidgetStyles.For(kind).Select(s => s.Name)];

    // ---- the menus ---------------------------------------------------------------------------

    [Fact]
    public void S6160_each_kind_offers_its_styles_in_menu_order_the_default_first()
    {
        Assert.Equal(["standard", "largeIcon", "minimal"], Names(WidgetKind.WeatherNow));
        Assert.Equal(["rows", "columns", "curve"], Names(WidgetKind.WeatherForecast));
        Assert.Equal(["chart", "number"], Names(WidgetKind.Stocks));
        Assert.Equal(["list", "chart"], Names(WidgetKind.ExchangeRates));
        Assert.Equal(["number", "gauge"], Names(WidgetKind.AirQuality));

        Assert.Equal(WeatherNowStyle.Standard, new WeatherNowConfig().Style);
        Assert.Equal(ForecastStyle.Rows, new WeatherForecastConfig().Style);
        Assert.Equal(StocksStyle.Chart, new StocksConfig().Style);
        Assert.Equal(ExchangeRatesStyle.List, new ExchangeRatesConfig().Style);
        Assert.Equal(AirQualityStyle.Number, new AirQualityConfig().Style);
    }

    [Fact]
    public void S6160_every_style_survives_a_clone()
    {
        Assert.Equal(WeatherNowStyle.Minimal, ((WeatherNowConfig)new WeatherNowConfig { Style = WeatherNowStyle.Minimal }.Clone()).Style);
        Assert.Equal(StocksStyle.Number, ((StocksConfig)new StocksConfig { Style = StocksStyle.Number }.Clone()).Style);
        Assert.Equal(AirQualityStyle.Gauge, ((AirQualityConfig)new AirQualityConfig { Style = AirQualityStyle.Gauge }.Clone()).Style);
    }

    // ---- weather now [S-6161] [S-6162] -------------------------------------------------------

    [Fact]
    public void S6161_the_large_icon_stands_beside_the_temperature_and_above_it_on_a_column()
    {
        var medium = WeatherNowStyles.LargeIcon(M, hasToday: true);
        var tall = WeatherNowStyles.LargeIcon(Tall, hasToday: true);

        Assert.False(medium.Stacked);
        Assert.True(medium.Condition);
        Assert.True(medium.IconSize >= 64, $"icon {medium.IconSize}");
        Assert.True(tall.Stacked);
        Assert.True(tall.IconSize >= 64, $"icon {tall.IconSize}");
    }

    [Fact]
    public void S6161_the_large_icon_and_the_temperature_fit_the_width_at_every_tier()
    {
        foreach (var tier in Tiers)
        {
            var plan = WeatherNowStyles.LargeIcon(tier, hasToday: true);
            var box = tier.Width - (2 * WeatherNowLayout.Inset);
            var reading = HeroLayout.ValueWidth(plan.Temperature, WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters);
            var used = plan.Stacked ? Math.Max(plan.IconSize, reading) : plan.IconSize + WeatherNowStyles.IconGap + reading;

            Assert.True(plan.IconSize >= WeatherNowStyles.IconMin, $"{tier}: icon {plan.IconSize}");
            Assert.True(used <= box + 1, $"{tier}: {used} of {box}");
        }
    }

    [Fact]
    public void S6161_today_s_range_comes_only_with_the_condition_and_only_when_known()
    {
        Assert.False(WeatherNowStyles.LargeIcon(L, hasToday: false).HighLow);
        Assert.True(WeatherNowStyles.LargeIcon(L, hasToday: true).HighLow);
        Assert.False(WeatherNowStyles.LargeIcon(Xs, hasToday: true).HighLow);
    }

    [Fact]
    public void S6162_the_minimal_temperature_grows_with_the_tile_and_past_display_on_a_large_one()
    {
        var steps = new[] { Xs, S, M, L, Xl }.Select(WeatherNowStyles.Minimal).ToList();

        for (var i = 1; i < steps.Count; i++)
        {
            Assert.True(steps[i].FontSize >= steps[i - 1].FontSize);
        }

        Assert.True(steps[^1].FontSize > FluentType.Display.FontSize);

        // And it still fits: the reading's width and line inside the inset box.
        var box = Xl.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        Assert.True(HeroLayout.ValueWidth(steps[^1], WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters) <= box.Width + 1);
        Assert.True(steps[^1].LineHeight <= box.Height + 1);
    }

    // ---- forecast curve [S-6164] -------------------------------------------------------------

    [Fact]
    public void S6164_the_warmest_day_is_at_the_top_and_the_coldest_at_the_bottom_on_its_column_s_middle()
    {
        var points = ForecastCurve.Points([10, 20, 15], 10, 20, 50, 4, 104);

        Assert.Equal(new CurvePoint(25, 104), points[0]);
        Assert.Equal(new CurvePoint(75, 4), points[1]);
        Assert.Equal(new CurvePoint(125, 54), points[2]);
    }

    [Fact]
    public void S6164_one_temperature_all_week_is_a_flat_line_in_the_middle()
    {
        var points = ForecastCurve.Points([12, 12], 12, 12, 40, 0, 100);

        Assert.All(points, p => Assert.Equal(50, p!.Value.Y));
    }

    [Fact]
    public void S6164_a_day_without_a_value_breaks_the_curve_and_a_lone_day_is_a_dot()
    {
        var points = ForecastCurve.Points([10, 12, null, 14, double.NaN, 9], 9, 14, 40, 0, 100);
        var runs = ForecastCurve.Smooth(points);

        Assert.Null(points[2]);
        Assert.Null(points[4]);
        Assert.Equal(3, runs.Count);
        Assert.Equal([0, 3, 5], runs.Select(r => r.FirstDay));
        Assert.Single(runs[0].Segments);
        Assert.Empty(runs[1].Segments);
        Assert.Empty(runs[2].Segments);
    }

    [Theory]
    [InlineData(new double[] { 8, 16, 13, 12, 4, 17, 11 })]
    [InlineData(new double[] { 1, 2, 30, 31, 32, -5, -5 })]
    [InlineData(new double[] { 0, 0, 10, 10 })]
    public void S6164_the_curve_passes_through_every_day_and_never_swings_past_its_neighbours(double[] values)
    {
        var points = ForecastCurve.Points([.. values.Select(v => (double?)v)], values.Min(), values.Max(), 60, 10, 110);
        var run = Assert.Single(ForecastCurve.Smooth(points));

        Assert.Equal(points[0], run.Start);

        var previous = run.Start;
        for (var i = 0; i < run.Segments.Count; i++)
        {
            var segment = run.Segments[i];
            Assert.Equal(points[i + 1], segment.End);

            // Monotone: both controls inside the band the piece's two ends span, so the curve
            // between two days rises or falls without a bump past either.
            var low = Math.Min(previous.Y, segment.End.Y) - 1e-9;
            var high = Math.Max(previous.Y, segment.End.Y) + 1e-9;
            Assert.InRange(segment.Control1.Y, low, high);
            Assert.InRange(segment.Control2.Y, low, high);
            Assert.True(segment.Control1.X > previous.X && segment.Control2.X < segment.End.X);
            previous = segment.End;
        }
    }

    [Fact]
    public void S6164_the_curve_shows_the_days_that_have_a_column_and_never_invents_one()
    {
        Assert.Equal(2, ForecastCurve.Plan(Xs, 7, true).Days);
        Assert.Equal(7, ForecastCurve.Plan(Xl, 7, true).Days);
        Assert.Equal(3, ForecastCurve.Plan(Xl, 3, true).Days);
        Assert.Equal(8, ForecastCurve.Plan(new WidgetArea(400, 300), 14, true).Days);
        Assert.All(Tiers, t => Assert.True(ForecastCurve.Plan(t, 7, true).ColumnWidth >= ForecastCurve.ColumnMin - 1e-9));
    }

    [Fact]
    public void S6164_the_glyph_the_lows_and_the_rain_come_with_the_height()
    {
        var small = ForecastCurve.Plan(Xs, 7, true);
        var large = ForecastCurve.Plan(L, 7, true);
        var dry = ForecastCurve.Plan(L, 7, false);

        Assert.False(small.Low);
        Assert.False(small.Precipitation);
        Assert.True(large.Icon && large.Low && large.Precipitation);
        Assert.False(dry.Precipitation);

        // The chart takes what the rungs leave: the parts add up to the column's height.
        var column = L.Height - (2 * ForecastCurve.Padding);
        var used = FluentType.Caption.LineHeight + ForecastCurve.IconSize + large.ChartHeight + FluentType.Caption.LineHeight + (3 * ForecastCurve.Gap);
        Assert.Equal(column, used, 1);
    }

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

    // ---- exchange-rate chart [S-6168] --------------------------------------------------------

    private static RatesPlan RatesChart(WidgetArea area)
        => OnlineLayouts.Rates(area, 4, ExchangeRatesStyle.Chart, FluentType.Subtitle.LineHeight + FluentType.Caption.LineHeight);

    [Fact]
    public void S6168_the_chart_style_is_the_first_currency_over_its_chart_and_no_rows()
    {
        foreach (var tier in new[] { M, Tall, L, Xl })
        {
            var plan = RatesChart(tier);
            Assert.True(plan.Hero);
            Assert.Equal(0, plan.Rows);
            Assert.False(plan.Beside);
            Assert.True(plan.ChartHeight >= OnlineLayouts.HeroChartMin, $"{tier}: chart {plan.ChartHeight}");
        }
    }

    [Fact]
    public void S6168_a_strip_puts_the_chart_beside_the_hero_with_the_strip_s_height()
    {
        var plan = RatesChart(Wide);

        Assert.True(plan.Beside);
        Assert.True(plan.HeroWidth >= 150);
        Assert.True(plan.ChartHeight >= Wide.Height);
    }

    [Fact]
    public void S6168_a_tile_too_small_for_the_chart_is_the_hero_alone()
    {
        var plan = RatesChart(Xs);

        Assert.True(plan.Hero);
        Assert.Equal(0, plan.ChartHeight);
        Assert.Equal(0, plan.Rows);
    }

    // ---- air gauge [S-6170] ------------------------------------------------------------------

    [Theory]
    [InlineData(AirQualityScale.European, 0, 0.0)]
    [InlineData(AirQualityScale.European, 20, 1 / 6.0)]
    [InlineData(AirQualityScale.European, 50, 2.5 / 6)]
    [InlineData(AirQualityScale.Us, 100, 2 / 6.0)]
    [InlineData(AirQualityScale.Us, 300, 5 / 6.0)]
    [InlineData(AirQualityScale.Us, 500, 1.0)]
    public void S6170_the_gauge_fills_to_the_index_s_place_on_six_equal_bands(AirQualityScale scale, double index, double fraction)
        => Assert.Equal(fraction, AirQualityRules.ScalePosition(scale, index), 6);

    [Fact]
    public void S6170_the_gauge_takes_the_tile_and_the_readings_come_beside_on_a_strip_and_under_on_a_column()
    {
        var small = OnlineLayouts.AirGauge(Xs, 7);
        var medium = OnlineLayouts.AirGauge(M, 7);
        var tall = OnlineLayouts.AirGauge(Tall, 7);
        var strip = OnlineLayouts.AirGauge(new WidgetArea(720, 140), 7);

        Assert.Equal(AirFacts.None, small.Facts);
        Assert.NotNull(GaugeLayout.Fit(small.GaugeWidth, small.GaugeHeight));
        Assert.True(medium.Band);
        Assert.Equal(AirFacts.None, medium.Facts);
        Assert.Equal(AirFacts.Lines, tall.Facts);
        Assert.True(tall.Count > 0);
        Assert.True(GaugeLayout.Fit(tall.GaugeWidth, tall.GaugeHeight)!.Value.Width >= Tall.Width - (2 * OnlineLayouts.Inset) - 4, "the gauge keeps the column's width");
        Assert.Equal(AirFacts.Side, strip.Facts);
        Assert.True(strip.SideWidth > 0 && strip.GaugeWidth + strip.SideWidth <= 720 - (2 * OnlineLayouts.Inset));
    }
}
