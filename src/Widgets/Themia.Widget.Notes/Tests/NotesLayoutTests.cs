using Themia.Core.Notes;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-14770] [S-14771] The note's body from the card's top, and its pill over the text's top end.</summary>
public sealed class NotesLayoutTests
{
    [Fact]
    public void S14771_the_pill_is_not_there_at_rest_the_text_is_wholly_seen()
    {
        Assert.False(default(NotesPill).Shown);
    }

    [Fact]
    public void S14771_the_pointer_over_the_card_shows_the_pill_and_leaving_it_hides_it()
    {
        var over = default(NotesPill).Moved();
        Assert.True(over.Shown);
        Assert.False(over.Left().Shown);
    }

    [Fact]
    public void S14771_typing_sends_the_pill_away_until_the_pointer_moves_again()
    {
        var typing = default(NotesPill).Moved().Typed();
        Assert.False(typing.Shown);
        Assert.False(typing.Typed().Shown);
        Assert.True(typing.Moved().Shown);

        // Typed while the pointer was elsewhere, then the pointer comes onto the card: it shows.
        Assert.True(default(NotesPill).Typed().Moved().Shown);
    }

    [Fact]
    public void S14771_an_open_menu_of_the_pill_keeps_it_while_the_pointer_is_on_the_menu()
    {
        var open = default(NotesPill).Moved().WithMenu(true).Left();
        Assert.True(open.Shown);
        Assert.True(open.Typed().Shown);
        Assert.False(open.WithMenu(false).Shown);
    }

    [Fact]
    public void S14771_a_failed_write_keeps_the_pill_and_its_problem_glyph_at_rest_and_while_typing()
    {
        var failed = default(NotesPill).WithProblem(true);
        Assert.True(failed.Shown);
        Assert.True(failed.Moved().Typed().Shown);
        Assert.False(failed.WithProblem(false).Shown);
    }

    [Theory]
    [InlineData(600, 160)]
    [InlineData(300, 160)]
    [InlineData(260, 136)]
    [InlineData(200, 76)]
    [InlineData(160, 36)]
    [InlineData(150, 0)]
    [InlineData(0, 0)]
    [InlineData(double.NaN, 0)]
    public void S14771_the_notes_name_takes_what_the_card_leaves_it_up_to_its_most(double cardWidth, double name)
        => Assert.Equal(name, NotesLayout.NameWidth(cardWidth));

    [Theory]
    [InlineData(400, 160)]
    [InlineData(300, 152)]
    [InlineData(200, 52)]
    [InlineData(160, 0)]
    public void S14771_the_problem_glyph_takes_its_room_from_the_name(double cardWidth, double name)
        => Assert.Equal(name, NotesLayout.NameWidth(cardWidth, problem: true));

    [Fact]
    public void S14771_the_pill_always_leaves_some_of_the_first_line_beside_it()
    {
        foreach (var problem in new[] { false, true })
        {
            for (var width = 160.0; width <= 900; width += 7)
            {
                var name = NotesLayout.NameWidth(width, problem);
                var pill = name + NotesLayout.PillParts + (problem ? NotesLayout.ProblemPart : 0);
                Assert.True(name is 0 or >= NotesLayout.NameLeast, $"{width}: {name}");
                Assert.True(name <= NotesLayout.NameMax, $"{width}: {name}");
                Assert.True(width - (2 * FamilySpace.Inset) - pill >= NotesLayout.TextKept - 0.001, $"{width}, {problem}: {pill}");
            }
        }
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(28, 28)]
    [InlineData(12, 20)]
    [InlineData(double.NaN, 20)]
    public void S14771_the_pill_stands_over_the_whole_first_line_its_last_glyph_on_the_inset(double firstLine, double height)
    {
        Assert.Equal(height, NotesLayout.PillHeight(firstLine));

        if (double.IsFinite(firstLine) && firstLine >= 20)
        {
            // The first line's box, which starts at the inset: all of its words, none of the next line's.
            Assert.Equal(FamilySpace.Inset, NotesLayout.PillTop);
            Assert.Equal(FamilySpace.Inset + firstLine, NotesLayout.PillTop + height);
        }

        // Its last button's 16 glyph ends on the inset.
        Assert.Equal(FamilySpace.Inset, NotesLayout.PillEnd + ((FamilyShape.LineButton - FamilyShape.Icon) / 2));
    }
}
