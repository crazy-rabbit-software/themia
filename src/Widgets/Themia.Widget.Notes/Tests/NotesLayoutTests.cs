using Themia.Core.Notes;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-1363] [S-3963] The note widget's body and its header bar.</summary>
public sealed class NotesLayoutTests
{
    [Theory]
    [InlineData(400, true, 9, false)]
    [InlineData(800, true, 9, false)]
    [InlineData(320, true, 6, true)]
    [InlineData(240, true, 3, true)]
    [InlineData(180, true, 0, true)]
    [InlineData(170, false, 1, true)]
    [InlineData(150, false, 0, true)]
    [InlineData(120, false, 0, false)]
    public void S3963_the_header_bar_keeps_what_fits_and_folds_the_rest_into_more(double width, bool picker, int inline, bool overflow)
    {
        var bar = NotesLayout.HeaderBar(width, picker);

        Assert.Equal(inline, bar.Inline);
        Assert.Equal(overflow, bar.Overflow);
    }

    [Fact]
    public void S3963_the_header_bar_never_takes_the_titles_minimum()
    {
        for (var width = 100.0; width <= 900; width += 7)
        {
            var bar = NotesLayout.HeaderBar(width, picker: true);
            var used = bar.Width;

            Assert.True(used <= Math.Max(0, width - NotesLayout.HeaderReserve(picker: true)), $"{width}: {used}");
            Assert.InRange(bar.Inline, 0, NotesLayout.AllButtons);
            Assert.True(bar.Overflow == (bar.Inline < NotesLayout.AllButtons && bar.Shown), $"{width}");
        }

        Assert.False(NotesLayout.HeaderBar(double.NaN, picker: true).Shown);
        Assert.False(NotesLayout.HeaderBar(0, picker: true).Shown);
    }
}
