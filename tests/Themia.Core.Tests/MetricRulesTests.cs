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

        public override WidgetConfig Clone() => new Metric(History) { Trend = Trend, Chart = Chart, Detail = Detail };
    }

    // ---- [S-14600] the size ---------------------------------------------------------------------------

    [Fact]
    public void S14600_the_card_is_its_one_row_at_its_optimal_with_no_room_over_or_under_it()
    {
        var sizing = MetricRules.Sizing;

        Assert.Equal(30, MetricRules.RowHeight);
        Assert.True(ItemGrid.Default().IsWhole(MetricRules.RowHeight));
        Assert.True(MetricRules.RowHeight >= FamilyShape.Row1);
        Assert.Equal(new SizeDip(240, 30), sizing.Natural);
        Assert.Equal(new SizeDip(200, 30), sizing.Min);
        Assert.Empty(sizing.Problems());

        // [S-12411] [S-14500] Relaxed on both axes: every cell taller too, its row stretched to fill it.
        Assert.Equal(SizeStretch.Ceiling, SizeStretch.Relax(sizing).Max.Height);
        Assert.Equal(53, SizeStretch.Relax(sizing).FitHeight(53));
        var relaxed = SizeStretch.Relax(sizing);
        Assert.Equal((SizeStretch.Ceiling, SizeStretch.Ceiling), (relaxed.Max.Width, relaxed.Max.Height));
        Assert.Equal(30, relaxed.FitHeight(12));
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

    // ---- [S-14220] [S-14700]..[S-14703] the graph and the face that fits its box -----------------------

    private static readonly ItemGrid Grid10 = ItemGrid.Default();

    [Fact]
    public void S14220_none_and_an_arrow_stand_one_row_a_line_its_row_and_a_graph_row_and_no_room_beside_the_value()
    {
        var none = MetricRules.SizingOf(new Metric(MetricHistory.Series));
        var arrow = MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Arrow });
        var line = MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Line });

        Assert.Equal((30d, 30d), (none.Min.Height, none.Natural.Height));
        Assert.Equal((30d, 30d), (arrow.Min.Height, arrow.Natural.Height));

        // A line: its reading row and the graph's least, a row; [S-14500] every cell taller too.
        Assert.Equal(MetricRules.RowHeight, MetricRules.GraphLeast(ChartDetail.Simple, Grid10));
        Assert.Equal((60d, 60d), (line.Min.Height, line.Natural.Height));
        Assert.Empty(line.Problems());
        Assert.Equal(60, SizeStretch.Relax(line).FitHeight(52));
        Assert.Equal(90, SizeStretch.Relax(line).FitHeight(90));

        // No room beside the value: the graph is under the reading.
        Assert.Equal(0, MetricRules.TrendRoom(MetricTrend.Line));
        Assert.Equal((200d, 240d), (line.Min.Width, line.Natural.Width));

        // A line the metric cannot draw is its arrow: one row.
        Assert.Equal(30, MetricRules.SizingOf(new Metric(MetricHistory.Readings) { Trend = MetricTrend.Line }).Natural.Height);
    }

    [Fact]
    public void S14700_every_metric_s_height_is_resizable_from_its_least_by_one_cell_with_no_most()
    {
        foreach (var trend in new[] { MetricTrend.None, MetricTrend.Arrow, MetricTrend.Line })
        {
            var relaxed = SizeStretch.Relax(MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = trend }));
            var least = relaxed.Min.Height;
            Assert.Equal(relaxed.Natural.Height, least);
            Assert.Equal(SizeStretch.Ceiling, relaxed.Max.Height);
            foreach (var cells in Enumerable.Range(1, 40))
            {
                Assert.Equal(least + (cells * 10), relaxed.FitHeight(least + (cells * 10)));
            }
        }
    }

    [Fact]
    public void S14703_a_detailed_graph_keeps_its_row_for_the_line_and_the_room_for_what_it_writes_so_its_card_is_taller()
    {
        // The high's figure over the line (a Caption line), the low's under it and the foot's line (12 + 16 + 4 + 16).
        var least = MetricRules.GraphLeast(ChartDetail.Detailed, Grid10);
        Assert.Equal(100, least);
        Assert.True(Grid10.IsWhole(least));

        var detailed = MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Line, Detail = ChartDetail.Detailed });
        Assert.Equal((130d, 130d), (detailed.Min.Height, detailed.Natural.Height));

        // Detailed with no graph drawn changes nothing: the card is its row.
        Assert.Equal(30, MetricRules.SizingOf(new Metric(MetricHistory.Series) { Trend = MetricTrend.Arrow, Detail = ChartDetail.Detailed }).Natural.Height);

        // In a Detailed graph's least, the line still has its row between what is written over and under it.
        var frame = ChartPlot.Frame(new ChartModel { Detail = ChartDetail.Detailed, Over = 30 }, 240, 30 + least);
        Assert.True(frame.LineBottom - frame.LineTop >= MetricRules.RowHeight - 1e-9);
    }

    [Fact]
    public void S14701_one_row_tall_the_row_two_rows_or_more_the_readout_the_whole_card()
    {
        Assert.Equal(new MetricLook(MetricFace.Row, 30, 0), MetricRules.Look(30, MetricTrend.None, ChartDetail.Simple, Grid10));

        // A part row over the one stays the row, stretched.
        Assert.Equal(MetricFace.Row, MetricRules.Look(50, MetricTrend.Arrow, ChartDetail.Simple, Grid10).Face);
        foreach (var height in new[] { 60d, 70, 90, 180, 600 })
        {
            Assert.Equal(new MetricLook(MetricFace.Readout, height, 0), MetricRules.Look(height, MetricTrend.None, ChartDetail.Simple, Grid10));
        }

        // On a display's pitch (9.974 at 1536 wide) two rows are still two rows, and so is a card the host laid on its taller
        // down pitch (six cells of 10.047); three of those cells are one row.
        Assert.Equal(MetricFace.Readout, MetricRules.Look(59.844, MetricTrend.Arrow, ChartDetail.Simple, new ItemGrid(9.974)).Face);
        Assert.Equal(MetricFace.Readout, MetricRules.Look(60.282, MetricTrend.Arrow, ChartDetail.Simple, new ItemGrid(9.974)).Face);
        Assert.Equal(MetricFace.Row, MetricRules.Look(30.141, MetricTrend.Arrow, ChartDetail.Simple, new ItemGrid(9.974)).Face);
        Assert.Equal(MetricFace.Row, MetricRules.Look(59.0, MetricTrend.Arrow, ChartDetail.Simple, Grid10).Face);

        // Not laid out yet: the row.
        Assert.Equal(new MetricLook(MetricFace.Row, 0, 0), MetricRules.Look(0, MetricTrend.Line, ChartDetail.Simple, Grid10));
    }

    [Fact]
    public void S14702_with_a_graph_the_reading_takes_the_top_growing_first_to_half_the_graph_the_foot_never_under_its_least()
    {
        // Its least: the row over the graph's row.
        Assert.Equal(new MetricLook(MetricFace.Row, 30, 30), MetricRules.Look(60, MetricTrend.Line, ChartDetail.Simple, Grid10));

        // Taller than two rows but under three: the reading's row grows first, on whole cells, the graph keeps its row.
        Assert.Equal(new MetricLook(MetricFace.Row, 40, 30), MetricRules.Look(70, MetricTrend.Line, ChartDetail.Simple, Grid10));
        Assert.Equal(new MetricLook(MetricFace.Row, 40, 40), MetricRules.Look(80, MetricTrend.Line, ChartDetail.Simple, Grid10));

        // From three rows the reading is the readout over its half (rounded up), the graph the rest.
        Assert.Equal(new MetricLook(MetricFace.Readout, 50, 40), MetricRules.Look(90, MetricTrend.Line, ChartDetail.Simple, Grid10));
        Assert.Equal(new MetricLook(MetricFace.Readout, 60, 60), MetricRules.Look(120, MetricTrend.Line, ChartDetail.Simple, Grid10));
        Assert.Equal(new MetricLook(MetricFace.Readout, 150, 150), MetricRules.Look(300, MetricTrend.Line, ChartDetail.Simple, Grid10));

        // Detailed: the graph keeps its least (its row and its writing) first; the reading grows past its row once there is room.
        Assert.Equal(new MetricLook(MetricFace.Row, 30, 100), MetricRules.Look(130, MetricTrend.Line, ChartDetail.Detailed, Grid10));
        Assert.Equal(new MetricLook(MetricFace.Readout, 50, 100), MetricRules.Look(150, MetricTrend.Line, ChartDetail.Detailed, Grid10));
        Assert.Equal(new MetricLook(MetricFace.Readout, 120, 120), MetricRules.Look(240, MetricTrend.Line, ChartDetail.Detailed, Grid10));

        // The reading and the graph share the card exactly, every size.
        foreach (var cells in Enumerable.Range(6, 60))
        {
            var look = MetricRules.Look(cells * 10, MetricTrend.Line, ChartDetail.Simple, Grid10);
            Assert.Equal(cells * 10, look.Reading + look.Graph, 9);
            Assert.True(look.Graph >= MetricRules.RowHeight);
            Assert.True(look.Reading >= MetricRules.RowHeight);
            Assert.True(Grid10.IsWhole(look.Reading));
        }
    }

    [Fact]
    public void S14701_the_caption_stands_beside_the_value_where_that_scales_larger_else_over_it()
    {
        var caption = new SizeDip(80, 16);
        var value = new SizeDip(100, 36);

        // Wide and short: beside on one line grows larger.
        Assert.Equal(MetricArrangement.Beside, MetricRules.Arrange(456, 36, caption, value, 12));

        // Narrow and tall: the name over the value.
        Assert.Equal(MetricArrangement.Stacked, MetricRules.Arrange(176, 160, caption, value, 12));
        Assert.Equal(MetricArrangement.Stacked, MetricRules.Arrange(216, 66, caption, value, 12));

        // Not laid out: stacked.
        Assert.Equal(MetricArrangement.Stacked, MetricRules.Arrange(0, 0, caption, value, 12));
    }

    [Fact]
    public void S14220_the_graph_s_points_are_the_newest_oldest_first_on_their_own_range_a_gap_left_out()
    {
        var model = MetricRules.Graph([1, 3, null, 2, double.NaN, 5], 240, ChartType.Area, ChartDetail.Simple, 30);

        // The low at the band's foot, the high at its top, the newest last at the right edge, the reading over the plot.
        Assert.Equal([1d, 3, 2, 5], model.Series.Select(p => p.Value));
        Assert.Equal((1d, 5d, 30d), (model.Low, model.High, model.Over));
        Assert.Equal(240 - (FamilyShape.Stroke / 2), model.Series[^1].X, 9);
        Assert.True(model.Series.Zip(model.Series.Skip(1)).All(p => p.First.X < p.Second.X));
        Assert.Equal((ChartType.Area, ChartDetail.Simple), (model.Type, model.Detail));

        // The newest 24 of a long history.
        var long_ = MetricRules.Graph([.. Enumerable.Range(0, 40).Select(i => (double?)i)], 240, ChartType.Line, ChartDetail.Simple, 30);
        Assert.Equal(MetricRules.LinePoints, long_.Series.Count);
        Assert.Equal((16d, 39d), (long_.Low, long_.High));
        Assert.Equal(FamilyShape.Stroke / 2, long_.Series[0].X, 9);

        // A flat history lies level through the middle; [WR-10] too few values: the chart lies at its baseline.
        var flat = MetricRules.Graph([1013, 1013, 1013], 240, ChartType.Area, ChartDetail.Simple, 30);
        Assert.Equal(0.5, flat.Fraction(1013));
        Assert.Empty(MetricRules.Graph([7], 240, ChartType.Area, ChartDetail.Simple, 30).Series);
        Assert.Empty(MetricRules.Graph(null, 240, ChartType.Area, ChartDetail.Simple, 30).Series);
    }

    [Fact]
    public void S14703_columns_stand_a_slot_each_the_lowest_a_short_column_and_a_detailed_graph_writes_the_shown_figure()
    {
        var bars = MetricRules.Graph([2, 4, 8], 240, ChartType.Bars, ChartDetail.Simple, 30);
        Assert.Equal(ChartType.Bars, bars.Type);

        // A column a slot of the 24 from the right: the newest the rightmost.
        Assert.Equal([240 - 25d, 240 - 15d, 240 - 5d], bars.Series.Select(p => p.X));

        // The lowest is a quarter of the band, never no column.
        Assert.Equal(0.25, bars.Fraction(2), 9);
        Assert.Equal(1, bars.Fraction(8), 9);

        Func<double, string> figure = v => $"{v:0} hPa";
        var detailed = MetricRules.Graph([1010, 1013], 240, ChartType.Line, ChartDetail.Detailed, 40, figure);
        Assert.Equal((ChartDetail.Detailed, "1013 hPa"), (detailed.Detail, detailed.Format!(1013)));
    }

    [Fact]
    public void S14703_a_metric_with_a_series_offers_the_histories_chart_types_and_detail_repaired_loudly()
    {
        var series = new Metric(MetricHistory.Series);
        Assert.Equal(ChartLooks.Lines, series.ChartTypes);
        Assert.True(series.OffersDetail);
        Assert.Equal((ChartType.Area, ChartDetail.Simple), (series.Chart, series.Detail));
        Assert.True(SharedSettings.ChartType.Holds(series));
        Assert.True(SharedSettings.ChartDetail.Holds(series));

        // A type the metric does not draw (a history draws no columns or candles) is its own, said.
        var bars = new Metric(MetricHistory.Series) { Chart = ChartType.Bars, Detail = (ChartDetail)7 };
        Assert.Equal(2, bars.Repair().Count);
        Assert.Equal((ChartType.Area, ChartDetail.Simple), (bars.Chart, bars.Detail));

        // Without a series there is no graph: no chart look held.
        var none = new Metric(MetricHistory.Readings);
        Assert.Empty(none.ChartTypes);
        Assert.False(SharedSettings.ChartDetail.Holds(none));
        Assert.False(SharedSettings.ChartType.Holds(none));
    }

    // ---- [S-14706] what a click opens ----------------------------------------------------------------------

    [Fact]
    public void S14706_the_families_sources_are_their_own_widgets_targets()
    {
        var any = new Metric(MetricHistory.None);
        Assert.Equal(System.IO.Path.Combine(Environment.SystemDirectory, "Taskmgr.exe"), MetricOpens.TaskManager.Target(any)!.Value);
        Assert.Equal("ms-settings:network-status", MetricOpens.Network.Target(any)!.Value);
        Assert.Equal("ms-settings:batterysaver", MetricOpens.Battery.Target(any)!.Value);

        // A place's page needs the place; a drive's needs a drive config.
        Assert.Null(MetricOpens.Weather.Target(any));
        Assert.Null(MetricOpens.Drive.Target(any));
        Assert.All(new[] { MetricOpens.TaskManager, MetricOpens.Network, MetricOpens.Battery, MetricOpens.Weather, MetricOpens.Drive },
            open => Assert.False(string.IsNullOrWhiteSpace(open.Label.Text())));
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
