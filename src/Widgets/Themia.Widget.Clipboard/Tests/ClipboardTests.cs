using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clipboard.Tests;

/// <summary>[S-1440] [S-1441] [S-1442] [S-1443] The clipboard's capacity, its previews, how a row is marked and its ladder. Moved with the widget from Core's TrackersLayoutTests; the ladder is on the family's rows.</summary>
public sealed class ClipboardTests
{
    // The size tiers' slots (card less a 36 header).


    [Fact]
    public void S1440_capacity_is_1_to_200_default_50()
    {
        Assert.Equal(1, ClipboardHistory.MinCapacity);
        Assert.Equal(200, ClipboardHistory.MaxCapacity);
        Assert.Equal(50, new ClipboardConfig().EffectiveCapacity);
        Assert.Equal(200, new ClipboardConfig { Capacity = 500 }.EffectiveCapacity);
        Assert.Equal(1, new ClipboardConfig { Capacity = 0 }.EffectiveCapacity);
    }

    [Fact]
    public void S1441_a_rows_preview_is_the_entry_on_one_line()
        => Assert.Equal("line one line two", ClipboardFormatting.Preview("line one\n  line   two\n", 120));

    [Theory]
    [InlineData("https://example.com/a?b=1", ClipboardContent.Link)]
    [InlineData("anna@example.org", ClipboardContent.Email)]
    [InlineData("#60cdff", ClipboardContent.Colour)]
    [InlineData("rgb(1, 2, 3)", ClipboardContent.Colour)]
    [InlineData("123", ClipboardContent.Number)]
    [InlineData("-1,234.5", ClipboardContent.Number)]
    [InlineData("NaN", ClipboardContent.Text)]
    [InlineData("cafe00", ClipboardContent.Text)]
    [InlineData("two\nlines", ClipboardContent.Lines)]
    [InlineData("hello world", ClipboardContent.Text)]
    [InlineData("a@b", ClipboardContent.Text)]
    [InlineData("C:\\Windows", ClipboardContent.Text)]
    public void S1442_entries_are_classified_conservatively(string text, ClipboardContent expected)
        => Assert.Equal(expected, ClipboardLayout.Classify(text));

    [Fact]
    public void S1442_a_colour_entry_yields_its_swatch()
    {
        Assert.True(ClipboardLayout.TryColour(" #ff0000 ", out var red));
        Assert.Equal(new RgbColor(255, 0, 0), red);
        Assert.False(ClipboardLayout.TryColour("ff0000", out _));
    }

    [Fact]
    public void S11405_a_new_clipboard_is_five_one_line_entries_with_marker_and_time()
    {
        var config = new ClipboardConfig();
        Assert.Equal(new ClipboardPlan(1, Lead: true, Time: true), ClipboardLayout.Plan(config));
        Assert.Equal(new Themia.Sdk.SizeDip(300, 36 + (5 * 28) + 12), new ClipboardModule().Descriptor.Sizing(config).Natural);
        Assert.Equal(new ClipboardPlan(2, Lead: true, Time: true), ClipboardLayout.Plan(new ClipboardConfig { Lines = 2 }));
    }

    [Fact]
    public void S11405_dragging_sets_rows_and_out_of_range_is_repaired_and_copied()
    {
        var next = (ClipboardConfig)new ClipboardModule().Descriptor.SizeSets!(new ClipboardConfig(), new Themia.Sdk.SizeDip(300, 36 + (8 * 28) + 12));
        Assert.Equal(8, next.Rows);

        var wild = new ClipboardConfig { Rows = 50, Lines = 0 };
        Assert.NotEmpty(wild.Repair());
        var copy = (ClipboardConfig)wild.Clone();
        Assert.Equal((20, 1), (copy.Rows, copy.Lines));
    }
}
