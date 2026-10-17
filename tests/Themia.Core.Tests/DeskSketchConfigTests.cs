using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The configs that carry a list the user built (a drawing) through the real state file; the quote
/// list's are its module's.
/// <para>
/// Not a unit test of a property bag: it goes through <see cref="AppStateStore"/> and the
/// source-generated serializer, because that is where a list with a populated default can quietly
/// double on every load, and where a drawing would quietly be lost.
/// </para>
/// </summary>
public sealed class DeskSketchConfigTests
{
    /// <summary>Put one widget of this kind with this config into a state file, and read it back.</summary>
    private static Task<T> RoundTrip<T>(WidgetId kind, T config)
        where T : WidgetConfig
        => StateRoundTrip.Of(kind, config);

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

        var reloaded = await RoundTrip(ScratchpadConfig.Descriptor.Id, config);

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
        var reloaded = await RoundTrip(ScratchpadConfig.Descriptor.Id, new ScratchpadConfig());

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
}
