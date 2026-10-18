using Themia.Core.Notes;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-1362] [S-1363] [S-3963] The note widget's ladder, its one status line and its header bar. Moved with the widget from Core's NoteStoreTests, unchanged in what they assert.</summary>
public sealed class NotesLayoutTests
{
    private static NotesLayoutPlan Plan(double width, double height, bool notSaved = false)
        => NotesLayout.Plan(new WidgetArea(width, height - 44), inset: 16, gap: 8, lineHeight: 18, statusHeight: 16, notSaved);

    [Fact]
    public void S1363_xs_and_s_show_text_alone()
    {
        foreach (var plan in new[] { Plan(120, 120), Plan(200, 150) })
        {
            Assert.False(plan.Status);
            Assert.True(plan.TextLines >= NotesLayout.MinTextLines);
        }
    }

    [Fact]
    public void S3963_the_body_gives_no_row_to_a_formatting_bar()
    {
        // [S-3963] The bar lives in the header: at M and on a wide strip the body is the text alone,
        // as many lines as the box holds.
        var m = Plan(320, 240);
        var wide = Plan(480, 160);

        Assert.False(m.Status);
        Assert.False(wide.Status);
        Assert.Equal(NotesLayout.MinTextLines + Disclosure.WholeRows((240 - 44 - 32) - (18 * NotesLayout.MinTextLines), 18), m.TextLines);
    }

    /// <summary>[S-1362] No word or character count and no "Saved" (user requirement): a saved note is its text alone at every size.</summary>
    [Theory]
    [InlineData(120, 120)]
    [InlineData(200, 150)]
    [InlineData(320, 240)]
    [InlineData(480, 160)]
    [InlineData(200, 400)]
    [InlineData(560, 420)]
    [InlineData(800, 560)]
    public void S1362_a_saved_note_has_no_status_line_at_any_size(double width, double height)
        => Assert.False(Plan(width, height).Status);

    [Fact]
    public void S1362_a_failed_save_is_said_under_the_text_where_a_line_is_left()
    {
        var saved = Plan(320, 240);
        var failed = Plan(320, 240, notSaved: true);

        Assert.True(failed.Status);
        Assert.True(failed.TextLines < saved.TextLines);

        // The text keeps its two lines first.
        Assert.False(Plan(120, 120, notSaved: true).Status);
    }

    [Fact]
    public void S1363_xl_gives_the_rest_to_the_text()
        => Assert.True(Plan(800, 560).TextLines > Plan(560, 420).TextLines);

    /// <summary>
    /// [S-1363] The widget is the one note at every size: the text takes the whole width even at
    /// XL, where a note list used to sit beside it (user decision: never a list on the left).
    /// </summary>
    [Fact]
    public void S1363_xl_never_gives_width_to_a_note_list()
    {
        var xl = NotesLayout.Plan(new WidgetArea(800, 516), inset: 16, gap: 8, lineHeight: 18, statusHeight: 16);
        Assert.Equal(800 - 32, xl.TextWidth);
    }

    [Fact]
    public void S1363_unmeasured_shows_the_text_only()
        => Assert.False(NotesLayout.Plan(default, 16, 8, 18, 16).Status);

    [Theory]
    [InlineData(400, true, 9, false)]
    [InlineData(800, true, 9, false)]
    [InlineData(320, true, 5, true)]
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
