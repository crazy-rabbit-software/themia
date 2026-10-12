using Themia.Core.Notes;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The note editor's intercepted keys and toolbar commands, ported from the original
/// <c>editing.test.ts</c>. These are the whole behaviour of those keys — the control only applies
/// what they return — so pinning them here pins the editing model.
/// </summary>
public sealed class NoteEditingTests
{
    /// <summary><paramref name="text"/> with <c>|</c> at the caret or <c>[…]</c> round a selection.</summary>
    private static string Mark(string text, int start, int end)
        => start == end
            ? text[..start] + "|" + text[start..]
            : text[..start] + "[" + text[start..end] + "]" + text[end..];

    private static string Mark(NoteEdit edit) => Mark(edit.Text, edit.Start, edit.End);

    /// <summary>The inverse of <see cref="Mark(string,int,int)"/> for a caret.</summary>
    private static (string Text, int Caret) At(string marked)
    {
        var i = marked.IndexOf('|', StringComparison.Ordinal);
        return (marked.Remove(i, 1), i);
    }

    private static NoteEdit Enter(string marked)
    {
        var (text, caret) = At(marked);
        return NoteEditing.PressEnter(text, caret, caret);
    }

    // -- offsets ------------------------------------------------------------------------------

    [Fact]
    public void S1353_line_bounds()
    {
        Assert.Equal((3, 5), NoteEditing.LineBounds("ab\ncd\nef", 4));
        Assert.Equal((0, 2), NoteEditing.LineBounds("ab\ncd", 0));
        Assert.Equal((3, 5), NoteEditing.LineBounds("ab\ncd", 5));
    }

    [Fact]
    public void S1353_a_leading_newline_does_not_pull_offset_zero_onto_the_next_line()
        => Assert.Equal((0, 0), NoteEditing.LineBounds("\nab", 0));

    [Fact]
    public void S1353_offsets_to_line_indices_and_back()
    {
        Assert.Equal(2, NoteEditing.LineIndexAt("a\nb\nc", 4));
        Assert.Equal(4, NoteEditing.LineStart("a\nb\nc", 2));
        Assert.Equal(5, NoteEditing.LineStart("a\nb\nc", 99));
    }

    // -- Enter --------------------------------------------------------------------------------

    [Theory]
    [InlineData("- milk|", "- milk\n- |")]
    [InlineData("1. first|", "1. first\n2. |")]
    [InlineData("9) nine|", "9) nine\n10) |")]
    [InlineData("- [x] done|", "- [x] done\n- [ ] |")]
    [InlineData("  - nested|", "  - nested\n  - |")]
    [InlineData("- a\n- |", "- a\n|")]
    [InlineData("- [ ] |", "|")]
    [InlineData("- mi|lk", "- mi\n- |lk")]
    [InlineData("-| milk", "-\n| milk")]
    [InlineData("hello|world", "hello\n|world")]
    public void S1354_enter_continues_the_list(string before, string after)
    {
        var edit = Enter(before);
        Assert.Equal(after, Mark(edit));
    }

    [Fact]
    public void S1354_enter_replaces_a_selection()
        => Assert.Equal("- one \n- |", Mark(NoteEditing.PressEnter("- one two", 6, 9)));

    // -- Tab ----------------------------------------------------------------------------------

    [Fact]
    public void S1355_tab_indents_the_caret_line_by_two_spaces()
    {
        var (text, caret) = At("- a|");
        Assert.Equal("  - a|", Mark(NoteEditing.ShiftIndent(text, caret, caret, outdent: false)));
    }

    [Fact]
    public void S1355_tab_indents_every_touched_line()
        => Assert.Equal("  - a\n  - b\n- c", NoteEditing.ShiftIndent("- a\n- b\n- c", 1, 6, outdent: false).Text);

    [Fact]
    public void S1355_shift_tab_outdents_by_up_to_two_spaces()
    {
        Assert.Equal("  - a", NoteEditing.ShiftIndent("    - a", 7, 7, outdent: true).Text);
        Assert.Equal("- a", NoteEditing.ShiftIndent(" - a", 4, 4, outdent: true).Text);
    }

    [Fact]
    public void S1355_outdenting_an_unindented_line_is_a_no_op()
    {
        var edit = NoteEditing.ShiftIndent("- a", 3, 3, outdent: true);
        Assert.Equal("- a", edit.Text);
        Assert.Equal(3, edit.Start);
    }

    [Fact]
    public void S1355_a_tab_outdents_as_one_unit()
        => Assert.Equal("- a", NoteEditing.ShiftIndent("\t- a", 4, 4, outdent: true).Text);

    // -- wrap ---------------------------------------------------------------------------------

    [Fact]
    public void S1356_wraps_the_selection()
        => Assert.Equal("make **[bold]** now", Mark(NoteEditing.ToggleWrap("make bold now", 5, 9, "**")));

    [Fact]
    public void S1356_unwraps_markers_inside_the_selection()
        => Assert.Equal("a [bold] b", Mark(NoteEditing.ToggleWrap("a **bold** b", 2, 10, "**")));

    [Fact]
    public void S1356_unwraps_markers_just_outside_the_selection()
        => Assert.Equal("a [bold] b", Mark(NoteEditing.ToggleWrap("a **bold** b", 4, 8, "**")));

    [Fact]
    public void S1356_an_empty_pair_with_the_caret_between()
        => Assert.Equal("a*|*b", Mark(NoteEditing.ToggleWrap("ab", 1, 1, "*")));

    [Fact]
    public void S1356_wrap_then_unwrap_round_trips()
    {
        var first = NoteEditing.ToggleWrap("word", 0, 4, "*");
        Assert.Equal("word", NoteEditing.ToggleWrap(first.Text, first.Start, first.End, "*").Text);
    }

    [Fact]
    public void S1356_a_selection_at_the_start_does_not_look_before_the_text()
        => Assert.Equal("**[ab]**", Mark(NoteEditing.ToggleWrap("ab", 0, 2, "**")));

    // -- paste --------------------------------------------------------------------------------

    [Fact]
    public void S1357_insert_replaces_the_selection_and_leaves_the_caret_after()
        => Assert.Equal("hello there|", Mark(NoteEditing.InsertText("hello world", 6, 11, "there")));

    [Fact]
    public void S1357_pasted_line_endings_become_newlines()
    {
        Assert.Equal("a\nb\nc|", Mark(NoteEditing.InsertText(string.Empty, 0, 0, "a\r\nb\rc")));
        Assert.Equal("x\ny", NoteEditing.NormalizeNewlines("x\r\ny"));
    }

    // -- task box -----------------------------------------------------------------------------

    [Fact]
    public void S1358_toggle_flips_the_box_both_ways()
    {
        Assert.Equal("- [x] milk", NoteEditing.ToggleTaskLine("- [ ] milk", 0));
        Assert.Equal("- [ ] milk", NoteEditing.ToggleTaskLine("- [x] milk", 0));
    }

    [Fact]
    public void S1358_toggle_touches_the_addressed_line_only()
        => Assert.Equal("- [ ] a\n- [x] b", NoteEditing.ToggleTaskLine("- [ ] a\n- [ ] b", 1));

    [Fact]
    public void S1358_toggle_preserves_length()
    {
        const string source = "  - [ ] indented";
        Assert.Equal(source.Length, NoteEditing.ToggleTaskLine(source, 0)!.Length);
    }

    [Fact]
    public void S1358_toggle_is_null_off_a_task_line()
    {
        Assert.Null(NoteEditing.ToggleTaskLine("- plain bullet", 0));
        Assert.Null(NoteEditing.ToggleTaskLine("text", 0));
    }

    [Fact]
    public void S1358_a_click_lands_on_the_box_only_between_its_brackets()
    {
        const string text = "intro\n  - [ ] milk";
        var box = text.IndexOf('[', StringComparison.Ordinal);
        Assert.Equal(1, NoteEditing.TaskBoxAt(text, box));
        Assert.Equal(1, NoteEditing.TaskBoxAt(text, box + 3));
        Assert.Null(NoteEditing.TaskBoxAt(text, box - 1));
        Assert.Null(NoteEditing.TaskBoxAt(text, box + 5));
        Assert.Null(NoteEditing.TaskBoxAt(text, 2));
    }

    // -- block commands -----------------------------------------------------------------------

    [Fact]
    public void S1359_a_bullet_applies_and_strips()
    {
        var on = NoteEditing.ToggleBlock("milk", 4, 4, NoteBlock.Bullet);
        Assert.Equal("- milk", on.Text);
        Assert.Equal("milk", NoteEditing.ToggleBlock(on.Text, on.Start, on.End, NoteBlock.Bullet).Text);
    }

    [Fact]
    public void S1359_numbers_from_one_across_the_selection()
        => Assert.Equal("1. a\n2. b\n3. c", NoteEditing.ToggleBlock("a\nb\nc", 0, 5, NoteBlock.Ordered).Text);

    [Fact]
    public void S1359_replaces_the_marker_instead_of_stacking()
    {
        Assert.Equal("- [ ] milk", NoteEditing.ToggleBlock("- milk", 6, 6, NoteBlock.Task).Text);
        Assert.Equal("> milk", NoteEditing.ToggleBlock("- [ ] milk", 10, 10, NoteBlock.Quote).Text);
        Assert.Equal("- milk", NoteEditing.ToggleBlock("> milk", 6, 6, NoteBlock.Bullet).Text);
        Assert.Equal("- Title", NoteEditing.ToggleBlock("## Title", 8, 8, NoteBlock.Bullet).Text);
    }

    [Fact]
    public void S1359_keeps_the_indent_of_a_nested_line()
        => Assert.Equal("    - milk", NoteEditing.ToggleBlock("    milk", 8, 8, NoteBlock.Bullet).Text);

    [Fact]
    public void S1359_strips_only_when_every_touched_line_has_it()
    {
        Assert.Equal("- a\n- b", NoteEditing.ToggleBlock("- a\nb", 0, 5, NoteBlock.Bullet).Text);
        Assert.Equal("a\nb", NoteEditing.ToggleBlock("- a\n- b", 0, 7, NoteBlock.Bullet).Text);
    }

    [Fact]
    public void S1359_keeps_the_selection_over_the_same_words()
    {
        var edit = NoteEditing.ToggleBlock("milk", 0, 4, NoteBlock.Bullet);
        Assert.Equal("milk", edit.Text[edit.Start..edit.End]);
    }

    [Fact]
    public void S1359_heading_cycles_h1_h2_h3_body()
    {
        var text = "Title";
        var seen = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            text = NoteEditing.CycleHeading(text, 0, 0).Text;
            seen.Add(text);
        }

        Assert.Equal(["# Title", "## Title", "### Title", "Title"], seen);
    }

    [Fact]
    public void S1359_heading_level_comes_from_the_first_touched_line()
        => Assert.Equal("## a\n## b", NoteEditing.CycleHeading("# a\nb", 0, 5).Text);

    [Fact]
    public void S1359_heading_replaces_a_list_marker()
        => Assert.Equal("# milk", NoteEditing.CycleHeading("- milk", 6, 6).Text);

    // -- marks --------------------------------------------------------------------------------

    [Fact]
    public void S1359_marks_around_a_caret()
    {
        Assert.True(NoteEditing.ActiveMarks("a **bold** b", 6, 6).Bold);
        Assert.False(NoteEditing.ActiveMarks("a **bold** b", 1, 1).Bold);
        Assert.True(NoteEditing.ActiveMarks("**bold**", 2, 2).Bold);
    }

    [Fact]
    public void S1359_marks_over_a_selection()
    {
        var marks = NoteEditing.ActiveMarks("~~gone~~ and `x`", 2, 6);
        Assert.True(marks.Strike);
        Assert.False(marks.Code);
    }

    [Fact]
    public void S1359_marks_block_and_heading_of_the_caret_line()
    {
        Assert.Equal(NoteBlock.Bullet, NoteEditing.ActiveMarks("- milk", 3, 3).Block);
        Assert.Equal(NoteBlock.Ordered, NoteEditing.ActiveMarks("1. milk", 4, 4).Block);
        Assert.Equal(NoteBlock.Task, NoteEditing.ActiveMarks("- [ ] milk", 7, 7).Block);
        Assert.Equal(NoteBlock.Quote, NoteEditing.ActiveMarks("> quoted", 4, 4).Block);
        Assert.Null(NoteEditing.ActiveMarks("plain", 3, 3).Block);
        Assert.Equal(1, NoteEditing.ActiveMarks("# a", 3, 3).Heading);
        Assert.Equal(3, NoteEditing.ActiveMarks("### a", 5, 5).Heading);
        Assert.Equal(0, NoteEditing.ActiveMarks("a", 1, 1).Heading);
    }

    [Fact]
    public void S1359_a_checked_task_is_not_strikethrough()
    {
        Assert.False(NoteEditing.ActiveMarks("- [x] done", 8, 8).Strike);
        Assert.True(NoteEditing.ActiveMarks("~~done~~", 4, 4).Strike);
    }

    [Fact]
    public void S1359_marks_read_the_caret_line_not_the_first()
    {
        Assert.Equal(NoteBlock.Bullet, NoteEditing.ActiveMarks("# head\n- item", 10, 10).Block);
        Assert.Equal(0, NoteEditing.ActiveMarks("# head\n- item", 10, 10).Heading);
    }

    // -- history ------------------------------------------------------------------------------

    [Fact]
    public void S1360_a_run_of_typing_is_one_step()
    {
        var history = new NoteHistory(string.Empty);
        foreach (var text in new[] { "a", "ab", "abc" })
        {
            history.Record(text, text.Length, text.Length);
        }

        Assert.Equal(string.Empty, history.Undo()?.Text);
        Assert.Null(history.Undo());
    }

    [Fact]
    public void S1360_a_newline_breaks_the_run()
    {
        var history = new NoteHistory("a");
        history.Record("a\n", 2, 2);
        history.Record("a\nb", 3, 3);
        Assert.Equal("a\n", history.Undo()?.Text);
        Assert.Equal("a", history.Undo()?.Text);
    }

    [Fact]
    public void S1360_a_paste_is_its_own_step()
    {
        var history = new NoteHistory("a");
        history.Record("a and more", 10, 10);
        history.Record("a and more!", 11, 11);
        Assert.Equal("a and more", history.Undo()?.Text);
        Assert.Equal("a", history.Undo()?.Text);
    }

    [Fact]
    public void S1360_undo_restores_the_selection_too()
    {
        var history = new NoteHistory("hello");
        history.Record("hello world", 11, 11);
        history.Record("goodbye", 7, 7);
        Assert.Equal(new NoteSnapshot("hello world", 11, 11), history.Undo());
    }

    [Fact]
    public void S1360_redo_and_a_new_edit_drops_redo()
    {
        var history = new NoteHistory("a");
        history.Record("ab!", 3, 3);
        history.Undo();
        Assert.Equal("ab!", history.Redo()?.Text);
        history.Undo();
        history.Record("xyz", 3, 3);
        Assert.Null(history.Redo());
    }

    [Fact]
    public void S1360_reset_drops_both_stacks()
    {
        var history = new NoteHistory("a");
        history.Record("ab!", 3, 3);
        history.Reset(string.Empty);
        Assert.Null(history.Undo());
        Assert.Equal(string.Empty, history.State.Text);
    }

    [Fact]
    public void S1360_a_caret_move_is_not_a_step()
    {
        var history = new NoteHistory("abc");
        history.Record("abc", 0, 0);
        Assert.Null(history.Undo());
        Assert.Equal(0, history.State.Start);
    }

    [Fact]
    public void S1360_the_stack_is_capped()
    {
        var history = new NoteHistory(string.Empty);
        for (var i = 0; i < NoteHistory.Limit + 50; i++)
        {
            history.Record(new string('x', (i + 1) * 2), 0, 0);
        }

        var steps = 0;
        while (history.Undo() is not null)
        {
            steps++;
        }

        Assert.Equal(NoteHistory.Limit, steps);
    }

    [Theory]
    [InlineData("ab", "abc", true)]
    [InlineData("ab", "axb", true)]
    [InlineData("ab", "ab\n", false)]
    [InlineData("ab", "abcd", false)]
    [InlineData("ab", "a", false)]
    [InlineData("ab", "ba", false)]
    public void S1360_typing_run_is_exactly_one_inserted_character(string before, string after, bool expected)
        => Assert.Equal(expected, NoteHistory.IsTypingRun(before, after));

    // -- diff and redraw ----------------------------------------------------------------------

    [Theory]
    [InlineData("hello", "hello", 5, 0, "")]
    [InlineData("hello", "help", 3, 2, "p")]
    [InlineData("abc", "aXbc", 1, 0, "X")]
    [InlineData("- [ ] a", "- [x] a", 3, 1, "x")]
    [InlineData("", "new", 0, 0, "new")]
    [InlineData("gone", "", 0, 4, "")]
    public void S1361_diff_is_the_smallest_replacement(string before, string after, int start, int remove, string insert)
    {
        var change = NoteText.Diff(before, after);
        Assert.Equal(new TextChange(start, remove, insert), change);
        Assert.Equal(after, before[..change.Start] + change.Insert + before[(change.Start + change.RemoveLength)..]);
    }

    [Fact]
    public void S1361_diff_never_splits_a_surrogate_pair()
    {
        // Two emoji sharing a high surrogate: the shared half must travel with its partner.
        var change = NoteText.Diff("x\U0001F600", "x\U0001F601");
        Assert.Equal(1, change.Start);
        Assert.Equal(2, change.RemoveLength);
        Assert.Equal("\U0001F601", change.Insert);
    }

    [Fact]
    public void S1361_only_the_touched_line_is_redrawn_after_typing()
    {
        const string before = "# a\nb\nc";
        const string after = "# a\nbx\nc";
        Assert.Equal([1], NoteText.LinesToRedraw(before, NoteMarkdown.Parse(before), after, NoteMarkdown.Parse(after)));
    }

    [Fact]
    public void S1361_lines_below_an_inserted_line_keep_their_formatting()
    {
        const string before = "a\n**b**\n*c*";
        const string after = "a\nnew\n**b**\n*c*";
        Assert.Equal([1], NoteText.LinesToRedraw(before, NoteMarkdown.Parse(before), after, NoteMarkdown.Parse(after)));
    }

    [Fact]
    public void S1361_opening_a_fence_redraws_every_line_it_turns_into_code()
    {
        const string before = "a\n**b**\nc";
        const string after = "```\na\n**b**\nc";
        Assert.Equal([0, 1, 2, 3], NoteText.LinesToRedraw(before, NoteMarkdown.Parse(before), after, NoteMarkdown.Parse(after)));
    }

    [Fact]
    public void S1361_no_change_redraws_nothing()
        => Assert.Empty(NoteText.LinesToRedraw("a", NoteMarkdown.Parse("a"), "a", NoteMarkdown.Parse("a")));
}
