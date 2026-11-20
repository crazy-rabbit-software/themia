using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-11206] [S-14770] The note is its text alone in a free range: nothing else appears with its size.</summary>
public sealed class NotesSizingTests
{
    [Fact]
    public void S14770_a_note_keeps_a_free_range_a_row_lower_with_no_row_over_its_text()
    {
        var sizing = ModuleCatalog.Of(new WidgetId("notes")).SizingOf();

        // Was 160 × 100 .. 300 × 220 while a row of 20 stood over the text ([S-3963], retired).
        Assert.Equal((new SizeDip(160, 80), new SizeDip(300, 200), new SizeDip(Themia.Sdk.SizeStretch.Ceiling, Themia.Sdk.SizeStretch.Ceiling)), (sizing.Min, sizing.Natural, sizing.Max)); // relaxed, [S-12411]: no practical Max
        Assert.Empty(sizing.Problems());
    }
}
