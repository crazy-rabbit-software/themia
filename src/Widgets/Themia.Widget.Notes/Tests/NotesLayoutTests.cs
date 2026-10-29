using Themia.Core.Notes;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-1363] [S-3963] The note widget's body and its header bar.</summary>
public sealed class NotesLayoutTests
{
    private static NotesLayoutPlan Plan(double width, double height)
        => NotesLayout.Plan(new WidgetArea(width, height - 44), lineHeight: 20);

    [Fact]
    public void S1363_xs_and_s_show_the_text_alone_at_least_two_lines()
    {
        foreach (var plan in new[] { Plan(120, 120), Plan(200, 150) })
        {
            Assert.True(plan.TextLines >= NotesLayout.MinTextLines);
        }
    }

    [Fact]
    public void S3963_the_body_gives_no_row_to_a_formatting_bar()
    {
        // [S-3963] The bar lives in the header: at M the body is the text alone, every whole line the
        // view's area holds under the card's bottom inset.
        Assert.Equal((240 - 44 - 16) / 20, Plan(320, 240).TextLines);
    }

    [Fact]
    public void S1363_xl_gives_the_rest_to_the_text()
        => Assert.True(Plan(800, 560).TextLines > Plan(560, 420).TextLines);

    /// <summary>
    /// [S-1363] The widget is the one note at every size: the text takes the whole width less the card's
    /// inset even at XL, where a note list used to sit beside it (user decision: never a list on the left).
    /// </summary>
    [Fact]
    public void S1363_xl_never_gives_width_to_a_note_list()
        => Assert.Equal(800 - 32, NotesLayout.Plan(new WidgetArea(800, 516), lineHeight: 20).TextWidth);

    [Fact]
    public void S1363_unmeasured_shows_the_text_only()
        => Assert.Equal(NotesLayout.MinTextLines, NotesLayout.Plan(default, 20).TextLines);

    [Theory]
    [InlineData(400, true, 9, false)]
    [InlineData(800, true, 9, false)]
    [InlineData(320, true, 6, true)]
    [InlineData(240, true, 2, true)]
    [InlineData(180, true, 0, true)]
    [InlineData(170, false, 0, true)]
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
