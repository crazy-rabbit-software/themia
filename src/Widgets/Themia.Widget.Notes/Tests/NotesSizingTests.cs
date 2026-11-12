using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-11206] The note is its text alone in a free range: nothing else appears with its size.</summary>
public sealed class NotesSizingTests
{
    [Fact]
    public void S11206_a_note_keeps_a_free_range_and_lands_at_its_old_default()
    {
        var sizing = ModuleCatalog.Of(new WidgetId("notes")).SizingOf();

        Assert.Equal((new SizeDip(160, 100), new SizeDip(300, 220), new SizeDip(600, 600)), (sizing.Min, sizing.Natural, sizing.Max));
        Assert.Null(sizing.HeightSteps);
        Assert.Empty(sizing.Problems());
    }
}
