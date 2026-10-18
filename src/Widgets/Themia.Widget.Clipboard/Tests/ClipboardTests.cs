using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clipboard.Tests;

/// <summary>[S-1440] [S-1441] [S-1442] [S-1443] The clipboard's capacity, its previews, how a row is marked and its ladder. Moved with the widget from Core's TrackersLayoutTests, unchanged in what they assert.</summary>
public sealed class ClipboardTests
{
    // The size tiers' slots (card less a 44px header).
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);


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
    public void S1441_one_line_preview_is_the_collapsed_preview()
    {
        const string text = "line one\n  line   two\n";
        Assert.Equal(ClipboardFormatting.Preview(text, 120), ClipboardLayout.PreviewLines(text, 1, 120));
    }

    [Fact]
    public void S1441_multi_line_preview_keeps_breaks_drops_blank_lines_and_marks_more()
    {
        const string text = "Street 1\r\n\r\n  8000   Zurich \nSwitzerland\nEurope";

        Assert.Equal("Street 1\n8000 Zurich", ClipboardLayout.PreviewLines(text, 2, 120).Split(" …")[0]);
        Assert.EndsWith(" …", ClipboardLayout.PreviewLines(text, 2, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland …", ClipboardLayout.PreviewLines(text, 3, 120));
        Assert.Equal("Street 1\n8000 Zurich\nSwitzerland\nEurope", ClipboardLayout.PreviewLines(text, 4, 120));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("abc", 0)]
    public void S1441_nothing_to_preview_is_empty(string? text, int lines)
        => Assert.Equal(string.Empty, ClipboardLayout.PreviewLines(text, lines, 120));

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
    public void S1443_clipboard_ladder_by_tier()
    {
        // L and XL give the rows a third line; no count footer under them (concise: the entries only).
        Assert.Equal(new ClipboardPlan(1, false, false, true), ClipboardLayout.Plan(XS));
        Assert.Equal(new ClipboardPlan(1, false, false, true), ClipboardLayout.Plan(S));
        Assert.Equal(new ClipboardPlan(1, false, true, false), ClipboardLayout.Plan(M));
        Assert.Equal(new ClipboardPlan(2, false, true, false), ClipboardLayout.Plan(Wide));
        Assert.Equal(new ClipboardPlan(2, true, false, false), ClipboardLayout.Plan(Tall));
        Assert.Equal(new ClipboardPlan(3, true, false, false), ClipboardLayout.Plan(L));
        Assert.Equal(new ClipboardPlan(3, true, false, false), ClipboardLayout.Plan(XL));
    }

    [Fact]
    public void S1443_unmeasured_area_is_one_plain_line()
        => Assert.Equal(1, ClipboardLayout.Plan(default).PreviewLines);
}
