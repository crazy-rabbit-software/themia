using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6317]..[S-6319] The CPU meter's Core graphs: a history per core, kept by the hub for the span shown.</summary>
public sealed class CoreGraphsTests
{
    private static AppState State(params WidgetConfig[] configs) => new()
    {
        Views =
        [
            new ViewState
            {
                Id = "v0",
                Name = "View",
                Widgets = [.. configs.Select((c, i) => new WidgetInstance { Id = $"w{i}", Config = c })],
            },
        ],
    };

    [Fact]
    public void S6318_a_cores_history_is_what_the_span_and_its_slide_need_not_an_hour()
    {
        Assert.Equal(0, CoreHistory.CapacityFor(0));

        // A minute at a point a second, plus the slide's lead on the narrowest tile (12 slots: folds of 5).
        Assert.Equal(60 + SparkScroll.Lead(5), CoreHistory.CapacityFor(60));
        Assert.True(CoreHistory.CapacityFor(3600) > 3600);
        Assert.True(CoreHistory.CapacityFor(60) < 100);
    }

    [Fact]
    public void S6318_the_ring_keeps_every_cores_newest_loads_in_order()
    {
        var history = new CoreHistory();
        history.Resize(3);

        history.Add([0.1, 0.5]);
        history.Add([0.2, null]);
        history.Add([0.3, 0.7]);
        history.Add([0.4, 1.5]);

        Assert.Equal(2, history.Cores);
        Assert.Equal(3, history.Count);
        Assert.Equal(4, history.Added);
        Assert.Equal([0.2f, 0.3f, 0.4f], history.Newest(0, 10).Select(p => (float)p.First));

        var second = history.Newest(1, 10);
        Assert.True(double.IsNaN(second[0].First));
        Assert.Equal(0.7f, (float)second[1].First);
        Assert.Equal(1f, (float)second[2].First);

        Assert.Equal([0.3f, 0.4f], history.Newest(0, 2).Select(p => (float)p.First));
        Assert.Empty(history.Newest(2, 10));
        Assert.Empty(history.Newest(-1, 10));
    }

    [Fact]
    public void S6318_a_shorter_span_keeps_the_newest_and_none_keeps_nothing()
    {
        var history = new CoreHistory();
        history.Resize(5);

        foreach (var load in new[] { 0.1, 0.2, 0.3, 0.4, 0.5 })
        {
            history.Add([load]);
        }

        history.Resize(2);
        Assert.Equal([0.4f, 0.5f], history.Newest(0, 10).Select(p => (float)p.First));

        history.Resize(4);
        history.Add([0.6]);
        Assert.Equal([0.4f, 0.5f, 0.6f], history.Newest(0, 10).Select(p => (float)p.First));

        history.Resize(0);
        history.Add([0.7]);
        Assert.Equal(0, history.Count);
        Assert.Empty(history.Newest(0, 10));
    }

    [Fact]
    public void S6318_another_number_of_processors_starts_the_history_again()
    {
        var history = new CoreHistory();
        history.Resize(4);
        history.Add([0.1, 0.2]);
        history.Add([0.3, 0.4]);

        history.Add([0.5, 0.6, 0.7]);

        Assert.Equal(3, history.Cores);
        Assert.Equal(1, history.Count);
        Assert.Equal(1, history.Added);
        Assert.Equal([0.7f], history.Newest(2, 10).Select(p => (float)p.First));

        // An empty read (no counter) adds nothing.
        history.Add([]);
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void S6319_a_tile_shows_its_figure_only_where_it_fits_and_a_larger_one_on_a_large_tile()
    {
        const double chars = 4; // "100%"

        Assert.Null(CoreGraphLayout.Tile(20, 12, chars).Figure);
        Assert.Null(CoreGraphLayout.Tile(14, 80, chars).Figure);

        var small = CoreGraphLayout.Tile(60, 45, chars).Figure;
        Assert.Equal(FluentType.Caption, small);

        var large = CoreGraphLayout.Tile(200, 160, chars).Figure;
        Assert.NotNull(large);
        Assert.True(large!.Value.FontSize > FluentType.Caption.FontSize);
        Assert.True(large.Value.LineHeight <= 160 * CoreGraphLayout.FigureShare);
    }
}
