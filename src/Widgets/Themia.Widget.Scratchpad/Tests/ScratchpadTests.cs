using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Scratchpad.Tests;

/// <summary>
/// [S-1497] The scratchpad: the ink bar's presets, and a drawing through the real state file (where
/// a drawing would quietly be lost). Moved with the widget from Core's DeskParityTests and
/// DeskSketchConfigTests, unchanged in what they assert.
/// </summary>
public sealed class ScratchpadTests
{
    private static readonly WidgetId Id = new("scratchpad");

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 2)]
    [InlineData(3.1, 4)]
    [InlineData(6, 4)]
    [InlineData(7, 8)]
    [InlineData(24, 8)]
    public void S1497_a_width_marks_the_nearest_nib(double width, double nib)
        => Assert.Equal(nib, InkPalette.NearestWidth(width));

    [Fact]
    public void S1497_an_ink_matches_its_hex_however_it_was_written()
    {
        var blue = InkPalette.Inks[0];

        Assert.True(InkPalette.Matches(blue, "#0078d4"));
        Assert.True(InkPalette.Matches(blue, "0078D4"));
        Assert.False(InkPalette.Matches(blue, "#F43F5E"));
        Assert.False(InkPalette.Matches(blue, null));
        Assert.Equal(InkPalette.Inks.Count, InkPalette.Inks.Select(ink => ink.Hex).Distinct().Count());
    }

    [Fact]
    public void S1497_the_inks_are_swatches_of_the_host_palette()
    {
        // [S-1549] One palette: every ink is one of the picker's own presets, under its name there.
        foreach (var ink in InkPalette.Inks)
        {
            Assert.Contains(ink, Themia.Core.Settings.ColorPresets.Surfaces);
        }

        Assert.Equal(6, InkPalette.Inks.Count);
        Assert.Equal(InkPalette.Inks[0].Hex, ScratchpadConfig.DefaultPenColor);
    }

    [Theory]
    [InlineData(120, 84, false, false)]   // XS: the pad is the widget
    [InlineData(200, 114, false, false)]  // S: compact height
    [InlineData(480, 124, false, false)]  // Wide: compact height
    [InlineData(200, 364, false, false)]  // Tall: 176 inside the inset holds no 204 of inks and eraser
    [InlineData(320, 204, true, true)]    // M: 296 holds the inks, the eraser and the nibs (204 + 92)
    [InlineData(280, 204, true, false)]   // 256 wide: the inks and the eraser; the nibs need 296
    [InlineData(560, 384, true, true)]    // L: everything
    [InlineData(800, 524, true, true)]    // XL
    public void S1497_the_ink_bar_by_tier(double width, double height, bool bar, bool nibs)
        => Assert.Equal(new InkBarPlan(bar, nibs), ScratchpadLayout.Bar(new WidgetArea(width, height), showTools: true, inks: 6, nibs: 3));

    [Fact]
    public void S1497_show_tools_off_has_no_bar()
        => Assert.False(ScratchpadLayout.Bar(new WidgetArea(800, 524), showTools: false, inks: 6, nibs: 3).Bar);

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

        var reloaded = await StateRoundTrip.Of(Id, config);

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
        var reloaded = await StateRoundTrip.Of(Id, new ScratchpadConfig());

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
