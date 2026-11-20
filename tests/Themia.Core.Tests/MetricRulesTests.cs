using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-14600]..[S-14605] The single metric's base rules: its size, its default, its trends, the arrow and the line.</summary>
public sealed class MetricRulesTests
{
    private sealed class Metric(MetricHistory history) : MetricConfig
    {
        protected override MetricHistory KindHistory { get; } = history;

        public override WidgetConfig Clone() => new Metric(History) { Trend = Trend };
    }

    // ---- [S-14600] the size ---------------------------------------------------------------------------

    [Fact]
    public void S14600_the_card_is_its_one_row_in_whole_grid_cells_with_no_room_over_or_under_it()
    {
        var sizing = MetricRules.Sizing;

        Assert.Equal(30, MetricRules.RowHeight);
        Assert.True(ItemGrid.Default().IsWhole(MetricRules.RowHeight));
        Assert.True(MetricRules.RowHeight >= FamilyShape.Row1);
        Assert.Equal(new SizeDip(240, 30), sizing.Natural);
        Assert.Equal(new SizeDip(200, 30), sizing.Min);
        Assert.Equal(30, sizing.Max.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Empty(sizing.Problems());

        // [S-12411] The width is relaxed; the height keeps its one step (a stepped axis is never relaxed).
        var relaxed = SizeStretch.Relax(sizing);
        Assert.Equal((SizeStretch.Ceiling, 30d), (relaxed.Max.Width, relaxed.Max.Height));
        Assert.Equal(30, relaxed.FitHeight(52));
    }

    [Fact]
    public void S14600_a_trend_widens_the_card_by_its_room_beside_the_value_and_never_its_height()
    {
        Assert.Equal(0, MetricRules.TrendRoom(MetricTrend.None));
        Assert.Equal(16 + 12, MetricRules.TrendRoom(MetricTrend.Arrow));
        Assert.Equal(40 + 12, MetricRules.TrendRoom(MetricTrend.Line));

        var line = MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Line });
        Assert.Equal((252d, 292d, 30d), (line.Min.Width, line.Natural.Width, line.Natural.Height));

        // A line the metric cannot draw is its arrow, sized as one.
        Assert.Equal(228, MetricRules.SizingOf(new Metric(MetricHistory.Readings) { Trend = MetricTrend.Line }).Min.Width);
        Assert.Equal(MetricRules.Sizing, MetricRules.SizingOf(new Metric(MetricHistory.None) { Trend = MetricTrend.Arrow }));
    }

    // ---- [S-14602] the default ------------------------------------------------------------------------

    [Fact]
    public void S14602_without_a_reading_the_value_is_the_default_unavailable_with_nothing_to_trend()
    {
        Assert.Equal(new MetricValue("--", null, false), MetricValue.Unread);
        var shown = new MetricShown(MetricValue.Unread) { Why = "Offline" };
        Assert.Equal(("--", "Offline"), (shown.Value.Text, shown.Why));
        Assert.Null(MetricRules.Direction(new MetricValue("12", 12), MetricValue.Unread));
    }

    [Fact]
    public void S14604_a_sheet_s_fact_is_its_metric_and_one_not_read_or_absent_keeps_the_default_and_the_note()
    {
        var sheet = new DetailSheet(null, null, [new DetailFact("Speed", "3.40 GHz", Key: "speed", Number: 3400)], []);
        var speed = MetricRules.Pick(sheet, "speed");
        Assert.Equal((new MetricValue("3.40 GHz", 3400), "Speed"), (speed.Value, speed.Name));
        Assert.Equal(MetricValue.Unread, MetricRules.Pick(sheet, "cores").Value);

        var absent = sheet with { Absent = true, Note = "No battery" };
        var gone = MetricRules.Pick(absent, "speed");
        Assert.Equal((false, (double?)null, "No battery"), (gone.Value.Available, gone.Value.Number, gone.Why));
    }

    // ---- [S-14603] the trends -------------------------------------------------------------------------

    [Fact]
    public void S14603_a_series_offers_none_arrow_and_line_readings_none_and_arrow_and_no_history_nothing()
    {
        Assert.Equal([MetricTrend.None, MetricTrend.Arrow, MetricTrend.Line], MetricRules.Offered(MetricHistory.Series));
        Assert.Equal([MetricTrend.None, MetricTrend.Arrow], MetricRules.Offered(MetricHistory.Readings));
        Assert.Empty(MetricRules.Offered(MetricHistory.None));

        Assert.Equal(MetricTrend.Arrow, MetricRules.Effective(MetricTrend.Line, MetricHistory.Readings));
        Assert.Equal(MetricTrend.None, MetricRules.Effective(MetricTrend.Arrow, MetricHistory.None));
        Assert.Equal(MetricTrend.Line, MetricRules.Effective(MetricTrend.Line, MetricHistory.Series));
    }

    [Fact]
    public void S14603_a_stored_trend_the_kind_cannot_draw_is_repaired_loudly_and_one_it_can_is_kept()
    {
        var line = new Metric(MetricHistory.Readings) { Trend = MetricTrend.Line };
        Assert.Single(line.Repair());
        Assert.Equal(MetricTrend.Arrow, line.Trend);

        var bogus = new Metric(MetricHistory.Series) { Trend = (MetricTrend)9 };
        Assert.Single(bogus.Repair());
        Assert.Equal(MetricTrend.None, bogus.Trend);

        Assert.Empty(new Metric(MetricHistory.Series) { Trend = MetricTrend.Line }.Repair());
    }

    [Fact]
    public void S14603_the_shared_trend_is_held_only_by_a_metric_with_a_history_and_written_as_each_can_draw_it()
    {
        var series = new Metric(MetricHistory.Series) { Trend = MetricTrend.Line };
        var readings = new Metric(MetricHistory.Readings);
        var none = new Metric(MetricHistory.None);

        Assert.True(SharedSettings.MetricTrend.Holds(series));
        Assert.False(SharedSettings.MetricTrend.Holds(none));
        Assert.True(SharedSettings.MetricTrend.Copy(series, readings));
        Assert.Equal(MetricTrend.Arrow, readings.Trend);
        Assert.Contains(SharedSettings.MetricTrend, SharedSettings.All);
    }

    [Fact]
    public void S14603_the_arrow_says_what_the_shown_figure_did_flat_under_its_precision()
    {
        Assert.Equal(TrendDirection.Up, MetricRules.Direction(new MetricValue("1,012 hPa", 1012.2), new MetricValue("1,014 hPa", 1013.6)));
        Assert.Equal(TrendDirection.Down, MetricRules.Direction(new MetricValue("12.1 GB", 12.12e9), new MetricValue("11.9 GB", 11.9e9)));

        // The same text is flat, however the numbers under it moved.
        Assert.Equal(TrendDirection.Flat, MetricRules.Direction(new MetricValue("3.40 GHz", 3401), new MetricValue("3.40 GHz", 3399)));
        Assert.Null(MetricRules.Direction(null, new MetricValue("3.40 GHz", 3400)));
        Assert.Null(MetricRules.Direction(new MetricValue("NTFS"), new MetricValue("NTFS")));
    }

    [Fact]
    public void S14603_the_arrow_without_history_compares_the_last_two_readings_the_widget_saw()
    {
        var readings = new MetricReadings();
        readings.Add(1, new MetricValue("10", 10));
        Assert.Null(readings.Previous);

        // The same reading drawn again is never a move.
        readings.Add(1, new MetricValue("10", 10));
        Assert.Null(readings.Previous);

        readings.Add(2, new MetricValue("12", 12));
        Assert.Equal(new MetricValue("10", 10), readings.Previous);
        Assert.Equal(TrendDirection.Up, MetricRules.Direction(readings.Previous, new MetricValue("12", 12)));

        readings.Clear();
        Assert.Null(readings.Previous);
    }

    [Fact]
    public void S14603_the_line_spans_its_box_oldest_left_newest_on_the_right_edge_scaled_to_its_own_range()
    {
        var model = MetricRules.Line([1, 3, null, 2], 30);

        Assert.Equal(ChartType.Line, model.Type);
        Assert.Equal([0d, 10, 20, 30], model.Series.Select(s => s.X));
        Assert.Equal((1d, 3d), (model.Low, model.High));
        Assert.True(double.IsNaN(model.Series[2].Value)); // a gap breaks the line
        Assert.Equal(2, model.Series[^1].Value);
    }

    [Fact]
    public void S14603_the_line_keeps_its_newest_points_a_flat_history_a_level_line_and_none_its_baseline()
    {
        var long_ = MetricRules.Line([.. Enumerable.Range(0, 40).Select(i => (double?)i)], 46);
        Assert.Equal(MetricRules.LinePoints, long_.Series.Count);
        Assert.Equal(39, long_.Series[^1].Value);
        Assert.Equal(16, long_.Series[0].Value);

        var flat = MetricRules.Line([5, 5, 5], 40);
        Assert.True(flat.Low < 5 && flat.High > 5);

        // [WR-10] Nothing to draw yet: the chart's baseline, never no line.
        Assert.Empty(MetricRules.Line([7], 40).Series);
        Assert.Empty(MetricRules.Line(null, 40).Series);
    }

    [Fact]
    public void S14603_over_a_service_s_hours_the_arrow_is_this_hour_against_the_last_and_the_line_runs_on_from_the_last()
    {
        var start = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
        var hours = Enumerable.Range(0, 30).Select(i => (At: start.AddHours(i), Value: (double?)(1000 + i))).ToList();

        var (direction, series) = MetricRules.Hours(hours, h => h.At, h => h.Value, 0, start.AddHours(5).AddMinutes(20));
        Assert.Equal(TrendDirection.Up, direction);
        Assert.Equal(1004, series[0]);
        Assert.Equal(MetricRules.LinePoints, series.Count);

        // Rounded as shown: 1004.2 against 1003.9 reads 1004 both: flat.
        var close = new List<(DateTimeOffset At, double? Value)> { (start, 1003.9), (start.AddHours(1), 1004.2) };
        Assert.Equal(TrendDirection.Flat, MetricRules.Hours(close, h => h.At, h => h.Value, 0, start.AddHours(1)).Direction);

        // The first hour has none before it; no hours, nothing.
        Assert.Null(MetricRules.Hours(hours, h => h.At, h => h.Value, 0, start.AddMinutes(10)).Direction);
        Assert.Empty(MetricRules.Hours(new List<(DateTimeOffset At, double? Value)>(), h => h.At, h => h.Value, 0, start).Series);
    }
}
