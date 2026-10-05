using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The three configs that carry a list the user built — a drawing, a palette, a set of quotes —
/// through the real state file.
/// <para>
/// Not a unit test of a property bag: it goes through <see cref="AppStateStore"/> and the
/// source-generated serializer, because that is where a list with a populated default can quietly
/// double on every load, and where a drawing would quietly be lost.
/// </para>
/// </summary>
public sealed class DeskSketchConfigTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public DeskSketchConfigTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    /// <summary>Put one widget of this kind with this config into a state file, and read it back.</summary>
    private async Task<T> RoundTrip<T>(WidgetKind kind, T config)
        where T : WidgetConfig
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        var widget = WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.3, 0.3));
        widget.Config = config;

        view.Widgets.Clear();
        view.Widgets.Add(widget);

        var store = new AppStateStore(_path, TimeSpan.Zero);
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = new AppStateStore(_path, TimeSpan.Zero).Load();
        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);

        return Assert.IsType<T>(loaded.State.ActiveView().Widgets[0].Config);
    }

    // -----------------------------------------------------------------------------------------
    // Scratchpad
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_drawing_survives_the_state_file()
    {
        var config = new ScratchpadConfig
        {
            PenColor = "#F43F5E",
            PenWidth = 6,
            ShowTools = false,
            Strokes =
            [
                new InkStroke { Color = "#0078D4", Width = 3, Points = InkGeometry.Pack([new(0, 0), new(0.5, 0.5)]) },
                new InkStroke { Color = "#10B981", Width = 8, Points = InkGeometry.Pack([new(1, 0), new(0.25, 0.75)]) },
            ],
        };

        var reloaded = await RoundTrip(WidgetKind.Scratchpad, config);

        Assert.Equal("#F43F5E", reloaded.PenColor);
        Assert.Equal(6, reloaded.PenWidth);
        Assert.False(reloaded.ShowTools);
        Assert.Equal(2, reloaded.Strokes.Count);
        Assert.Equal("#0078D4", reloaded.Strokes[0].Color);
        Assert.Equal(3, reloaded.Strokes[0].Width);

        Assert.Equal(
            new List<InkPoint> { new(0, 0), new(0.5, 0.5) },
            InkGeometry.Unpack(reloaded.Strokes[0].Points));

        Assert.Equal(
            new List<InkPoint> { new(1, 0), new(0.25, 0.75) },
            InkGeometry.Unpack(reloaded.Strokes[1].Points));
    }

    [Fact]
    public async Task A_blank_pad_stays_blank()
    {
        var reloaded = await RoundTrip(WidgetKind.Scratchpad, new ScratchpadConfig());

        Assert.Empty(reloaded.Strokes);
    }

    [Fact]
    public void A_pen_colour_that_no_longer_parses_falls_back_instead_of_reaching_a_brush()
    {
        Assert.Equal(ScratchpadConfig.DefaultPenColor, new ScratchpadConfig { PenColor = "chartreuse" }.EffectivePenColor);
        Assert.Equal(ScratchpadConfig.DefaultPenColor, new ScratchpadConfig { PenColor = string.Empty }.EffectivePenColor);

        // A valid colour is normalised rather than passed through: "#abc" has to reach a brush as
        // something the brush parser will take.
        Assert.Equal("#AABBCC", new ScratchpadConfig { PenColor = "#abc" }.EffectivePenColor);
    }

    [Fact]
    public void Duplicating_a_pad_copies_the_drawing_rather_than_sharing_it()
    {
        var original = new ScratchpadConfig
        {
            Strokes = [new InkStroke { Color = "#0078D4", Points = [0.1, 0.2] }],
        };

        var copy = Assert.IsType<ScratchpadConfig>(original.Clone());
        copy.Strokes[0].Points.Add(0.3);
        copy.Strokes.Add(new InkStroke());

        Assert.Single(original.Strokes);
        Assert.Equal(2, original.Strokes[0].Points.Count);
    }

    // -----------------------------------------------------------------------------------------
    // Quote
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_quote_list_survives_the_state_file_without_doubling()
    {
        var reloaded = await RoundTrip(WidgetKind.Quote, new QuoteConfig());

        Assert.Equal(QuoteRotation.Bundled.Count, reloaded.Quotes.Count);
    }

    [Fact]
    public async Task The_users_own_quotes_and_rotation_come_back_as_they_were()
    {
        var config = new QuoteConfig
        {
            Cadence = QuoteCadence.Hourly,
            Shuffle = true,
            Seed = 42,
            ShowAuthor = false,
            Quotes = [new QuoteItem { Text = "Measure twice.", Author = "Anyone" }],
        };

        var reloaded = await RoundTrip(WidgetKind.Quote, config);

        Assert.Equal(QuoteCadence.Hourly, reloaded.Cadence);
        Assert.True(reloaded.Shuffle);
        Assert.Equal(42, reloaded.Seed);
        Assert.False(reloaded.ShowAuthor);
        var quote = Assert.Single(reloaded.EffectiveQuotes);
        Assert.Equal("Measure twice.", quote.Text);
    }

    [Fact]
    public void Duplicating_a_quote_widget_copies_its_list()
    {
        var original = new QuoteConfig { Quotes = [new QuoteItem { Text = "One", Author = "A" }] };

        var copy = Assert.IsType<QuoteConfig>(original.Clone());
        copy.Quotes.Add(new QuoteItem { Text = "Two", Author = "B" });

        Assert.Single(original.Quotes);
    }
}
