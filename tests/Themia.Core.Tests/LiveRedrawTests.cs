using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6332] A read that says what a tile shows already touches nothing.</summary>
public sealed class LiveRedrawTests
{
    private static DetailSheet Sheet(string memory, string row = "12%") => new(
        "42%",
        "4 cores",
        [new DetailFact("Memory", memory), new DetailFact("Uptime", "3 h")],
        [new DetailRow("p1", "Explorer", row, Fraction: 0.12)]);

    [Fact]
    public void S6332_a_detail_sheet_read_again_with_new_lists_is_the_same_sheet()
    {
        var shown = Sheet("8 GB");
        var read = Sheet("8 GB");

        // Every read builds new lists, so the record's own equality never matches; SameAs does.
        Assert.NotEqual(shown, read);
        Assert.True(read.SameAs(shown));
    }

    [Fact]
    public void S6332_a_changed_fact_row_or_state_is_drawn()
    {
        var shown = Sheet("8 GB");

        Assert.False(Sheet("9 GB").SameAs(shown));
        Assert.False(Sheet("8 GB", row: "13%").SameAs(shown));
        Assert.False((Sheet("8 GB") with { Hero = "43%" }).SameAs(shown));
        Assert.False((Sheet("8 GB") with { HeroAvailable = false }).SameAs(shown));
        Assert.False((Sheet("8 GB") with { Absent = true }).SameAs(shown));
        Assert.False((Sheet("8 GB") with { Note = "Why" }).SameAs(shown));
        Assert.False((Sheet("8 GB") with { Rows = [] }).SameAs(shown));
        Assert.False(shown.SameAs(null));
    }
}
