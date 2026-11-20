using Themia.Core.Notes;
using Themia.Core.Shell;
using Xunit;

namespace Themia.Widget.Notes.Tests;

/// <summary>[S-14771] [S-14772] Which of the note's actions stand where: the selection's menu, the text's menu, the pill's "…" and picker.</summary>
public sealed class NoteMenusTests
{
    private static readonly NoteMarks None = new(false, false, false, false, 0, null);

    private static readonly NoteCommand[] RowOrder =
    [
        NoteCommand.Bold, NoteCommand.Italic, NoteCommand.Strike, NoteCommand.Code,
        NoteCommand.Heading, NoteCommand.Bullet, NoteCommand.Ordered, NoteCommand.Task, NoteCommand.Quote,
    ];

    [Fact]
    public void S14772_every_format_the_toolbar_had_is_the_selections_and_stands_in_its_row()
    {
        var lines = NoteMenus.Text(NoteMenuOpening.Selected, selected: true, readOnly: false, canUndo: true, canRedo: false, None);

        // The nine formats and nothing else: no clipboard, no undo, no list.
        Assert.Equal(RowOrder, lines.Select(l => l.Format!.Value));
        Assert.All(lines, l => Assert.Equal(NoteMenuItem.Format, l.Item));
        Assert.Equal(Enum.GetValues<NoteCommand>().Order(), lines.Select(l => l.Format!.Value).Order());

        // Each in the icon row, in the row's order (the menu's rule orders them by their quick command).
        var split = ContextMenuLayout.Split([.. lines.Select(l => MenuLine.Entry(l.Quick))]);
        Assert.Equal(lines.Count, split.Row.Count);
        Assert.Empty(split.List);
        Assert.Equal(Enumerable.Range(0, lines.Count), split.Row);
        Assert.All(lines, l => Assert.True(ContextMenuLayout.IsFormat(l.Quick!.Value)));
    }

    [Fact]
    public void S14772_the_texts_menu_is_cut_copy_paste_and_the_marks_in_its_row_a_paragraphs_kinds_undo_redo_select_all_in_its_list()
    {
        var lines = NoteMenus.Text(NoteMenuOpening.Context, selected: true, readOnly: false, canUndo: true, canRedo: true, None);
        var split = ContextMenuLayout.Split([.. lines.Select(l => l.Divider ? MenuLine.Separator : MenuLine.Entry(l.Quick))]);

        Assert.Equal(
            [NoteMenuItem.Cut, NoteMenuItem.Copy, NoteMenuItem.Paste, NoteMenuItem.Format, NoteMenuItem.Format, NoteMenuItem.Format, NoteMenuItem.Format],
            split.Row.Select(i => lines[i].Item));
        Assert.Equal([NoteCommand.Bold, NoteCommand.Italic, NoteCommand.Strike, NoteCommand.Code], split.Row.Skip(3).Select(i => lines[i].Format!.Value));
        Assert.Equal(
            [NoteCommand.Heading, NoteCommand.Bullet, NoteCommand.Ordered, NoteCommand.Task, NoteCommand.Quote],
            split.List.Where(i => i != MenuSplit.SeparatorIndex && lines[i].Item == NoteMenuItem.Format).Select(i => lines[i].Format!.Value));
        Assert.Equal(
            [NoteMenuItem.Undo, NoteMenuItem.Redo, NoteMenuItem.SelectAll],
            split.List.Where(i => i != MenuSplit.SeparatorIndex && lines[i].Item != NoteMenuItem.Format).Select(i => lines[i].Item));
        Assert.Equal(2, split.List.Count(i => i == MenuSplit.SeparatorIndex));
        Assert.DoesNotContain(lines, l => l.Disabled);
    }

    /// <summary>
    /// [S-14772] Regression: the text's menu put all twelve icons in its row; the command bar folded Task list and Quote into
    /// its list on its own, Task list still lit as a row command (a solid accent line in the list).
    /// </summary>
    [Fact]
    public void S14772_no_row_holds_more_than_the_command_bar_shows()
    {
        foreach (var opening in Enum.GetValues<NoteMenuOpening>())
        {
            var lines = NoteMenus.Text(opening, selected: true, readOnly: false, canUndo: true, canRedo: true, None);
            var split = ContextMenuLayout.Split([.. lines.Select(l => l.Divider ? MenuLine.Separator : MenuLine.Entry(l.Quick))]);
            Assert.InRange(split.Row.Count, ContextMenuLayout.MinRow, NoteMenus.RowMost);
        }
    }

    [Fact]
    public void S14772_with_nothing_selected_cut_and_copy_are_greyed_and_paste_and_the_formats_act_at_the_caret()
    {
        var lines = NoteMenus.Text(NoteMenuOpening.Context, selected: false, readOnly: false, canUndo: false, canRedo: false, None);

        Assert.True(lines.Single(l => l.Item == NoteMenuItem.Cut).Disabled);
        Assert.True(lines.Single(l => l.Item == NoteMenuItem.Copy).Disabled);
        Assert.False(lines.Single(l => l.Item == NoteMenuItem.Paste).Disabled);
        Assert.True(lines.Single(l => l.Item == NoteMenuItem.Undo).Disabled);
        Assert.True(lines.Single(l => l.Item == NoteMenuItem.Redo).Disabled);
        Assert.All(lines.Where(l => l.Item == NoteMenuItem.Format), l => Assert.False(l.Disabled));
    }

    [Fact]
    public void S14772_a_note_that_cannot_be_typed_in_offers_copy_and_select_all_alone()
    {
        var lines = NoteMenus.Text(NoteMenuOpening.Context, selected: true, readOnly: true, canUndo: true, canRedo: true, None);

        Assert.Equal(
            [NoteMenuItem.Copy, NoteMenuItem.SelectAll],
            lines.Where(l => !l.Divider && !l.Disabled).Select(l => l.Item));
    }

    [Fact]
    public void S14772_a_format_in_force_at_the_selection_is_lit_the_others_are_not()
    {
        var marks = new NoteMarks(Bold: true, Italic: false, Strike: false, Code: false, Heading: 2, Block: NoteBlock.Task);
        var lines = NoteMenus.Text(NoteMenuOpening.Selected, selected: true, readOnly: false, canUndo: false, canRedo: false, marks);

        Assert.Equal(
            [NoteCommand.Bold, NoteCommand.Heading, NoteCommand.Task],
            lines.Where(l => l.On == true).Select(l => l.Format!.Value));
        Assert.All(lines, l => Assert.NotNull(l.On));

        // Only the formats say whether they are in force.
        var context = NoteMenus.Text(NoteMenuOpening.Context, selected: true, readOnly: false, canUndo: false, canRedo: false, marks);
        Assert.All(context.Where(l => l.Item != NoteMenuItem.Format), l => Assert.Null(l.On));
    }

    [Theory]
    [InlineData(true, 3, 9, false, true)]
    [InlineData(true, 9, 3, false, true)]
    [InlineData(true, 4, 4, false, false)]
    [InlineData(false, 3, 9, false, false)]
    [InlineData(true, 3, 9, true, false)]
    public void S14772_the_selections_bar_opens_when_the_left_button_lets_go_over_selected_text(bool left, int start, int end, bool readOnly, bool opens)
        => Assert.Equal(opens, NoteMenus.OpensOnRelease(left, start, end, readOnly));

    [Fact]
    public void S14771_the_pills_more_holds_the_notes_own_commands_alone()
    {
        Assert.Equal([NoteMenuItem.Rename, NoteMenuItem.Delete], NoteMenus.More(noteShown: true));
        Assert.Empty(NoteMenus.More(noteShown: false));
    }

    [Fact]
    public void S14771_the_picker_lists_every_note_the_shown_one_checked_then_new_note()
    {
        NoteMeta[] notes =
        [
            new("00000000000000000000000000000001", "Groceries", 5),
            new("00000000000000000000000000000002", " ", 9),
            new("00000000000000000000000000000003", "Work log", 1),
        ];

        var picks = NoteMenus.Picker(notes, notes[2].Id, "Note title", "New note");

        Assert.Equal(
            [
                new NotePick(notes[0].Id, "Groceries", false),
                new NotePick(notes[1].Id, "Note title", false),
                new NotePick(notes[2].Id, "Work log", true),
                new NotePick(null, "New note", false),
            ],
            picks);
        Assert.Equal([new NotePick(null, "New note", false)], NoteMenus.Picker([], null, "Note title", "New note"));
    }

    [Theory]
    [InlineData("Groceries", true, "Groceries")]
    [InlineData("  ", true, "Note title")]
    [InlineData(null, true, "Note title")]
    [InlineData(null, false, "Choose a note")]
    [InlineData("Groceries", false, "Choose a note")]
    public void S14771_the_picker_says_the_shown_notes_name_or_asks_for_one(string? title, bool shown, string said)
        => Assert.Equal(said, NoteMenus.PickerName(title, shown, "Note title", "Choose a note"));
}
