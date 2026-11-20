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
    public void S14600_an_arrow_widens_the_card_by_its_room_beside_the_value_and_never_its_height()
    {
        Assert.Equal(0, MetricRules.TrendRoom(MetricTrend.None));
        Assert.Equal(16 + 12, MetricRules.TrendRoom(MetricTrend.Arrow));

        var arrow = MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Arrow });
        Assert.Equal((228d, 268d, 30d), (arrow.Min.Width, arrow.Natural.Width, arrow.Natural.Height));

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

    // ---- [S-14220] the line on the history band ------------------------------------------------------

    [Fact]
    public void S14220_a_line_takes_no_room_beside_the_value_it_is_the_band_behind_the_row()
    {
        Assert.Equal(0, MetricRules.TrendRoom(MetricTrend.Line));
        Assert.Equal(MetricRules.Sizing, MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Line }));
    }

    [Fact]
    public void S14220_the_line_keeps_to_the_room_under_the_row_s_text_and_glyph_never_through_them()
    {
        var body = FamilyType.StepsAt(1)[1];
        Assert.Equal(new TypeStep(FamilyType.BodySize, FamilyType.BodyLine, false), body);

        // The 30 row: its Body line (20) centred stands 5 to 25, the descenders inside it; the line's room is 25 to 30.
        Assert.Equal(25, MetricRules.LineOver(MetricRules.RowHeight, body));
        Assert.True(MetricRules.LineOver(MetricRules.RowHeight, body) >= (MetricRules.RowHeight + FamilyShape.Icon) / 2);

        // A taller row on another grid's pitch keeps the line under its centred text all the same.
        Assert.Equal(28, MetricRules.LineOver(36, body));

        // A larger text size: under its taller line, but never less than a stroke's room at the foot.
        Assert.Equal(30 - FamilyShape.Stroke, MetricRules.LineOver(30, FamilyType.StepsAt(1.5)[1]));

        // Not laid out yet: nothing over the plot.
        Assert.Equal(0, MetricRules.LineOver(0, body));
    }

    [Fact]
    public void S14220_the_band_s_points_are_oldest_first_each_its_share_of_their_own_range_a_gap_left_out()
    {
        var points = MetricRules.Band([1, 3, null, 2, double.NaN, 5]);

        // The low at the band's foot, the high at its top, the newest last (the band's right edge).
        Assert.Equal([0d, 0.5, 0.25, 1], points.Select(p => p.First));
        Assert.All(points, p => Assert.True(double.IsNaN(p.Second)));
    }

    [Fact]
    public void S14220_the_band_keeps_the_newest_points_a_flat_history_a_level_line_and_too_few_its_baseline()
    {
        var long_ = MetricRules.Band([.. Enumerable.Range(0, 40).Select(i => (double?)i)]);
        Assert.Equal(MetricRules.LinePoints, long_.Count);
        Assert.Equal((0d, 1d), (long_[0].First, long_[^1].First));

        // Shares of the newest 24 (16..39), not of the whole history.
        Assert.Equal(1d / 23, long_[1].First, 9);

        // A flat history lies level through the middle.
        Assert.All(MetricRules.Band([1013, 1013, 1013]), p => Assert.Equal(0.5, p.First));

        // [WR-10] Fewer than two values: the band draws its baseline (it needs two to draw a line), never nothing.
        Assert.Single(MetricRules.Band([7]));
        Assert.Empty(MetricRules.Band([null, null]));
        Assert.Empty(MetricRules.Band(null));
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
