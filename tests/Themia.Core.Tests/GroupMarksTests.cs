using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14310] A selection of several shows the selection's frame and no marks of its members (owner, 2026-11-20: "when
/// mutliple widgets are selected the individual widgets corners in the group should not be highlighted / show up");
/// [S-11877] [S-11881] [S-11889] which widget draws its marks otherwise.
/// </summary>
public sealed class GroupMarksTests
{
    /// <summary>The frame of <paramref name="drawn"/> in edit mode or on the locked desktop, as the ids it goes round.</summary>
    private static List<string> Framed(bool editing, params (string Id, bool Selected, bool Icon)[] drawn)
        => SelectionRules.Framed(editing, drawn.Select(d => (d.Id, d.Selected, d.Icon)));

    /// <summary>Whether <paramref name="id"/> draws its marks among <paramref name="drawn"/>, the frame counted by the one rule.</summary>
    private static bool Marks(string id, bool editing, double? moveAlpha, params (string Id, bool Selected, bool Icon)[] drawn)
    {
        var framed = Framed(editing, drawn);
        var (_, selected, icon) = drawn.Single(d => d.Id == id);
        return SelectionRules.MarksShown(editing, selected, moveAlpha, icon, framed.Contains(id) ? framed.Count : 0);
    }

    [Fact]
    public void S14310_regression_two_selected_in_edit_mode_show_no_marks_of_their_own_one_selected_does()
    {
        // Two selected: the frame goes round both and neither draws its brackets; the one not selected keeps its neutral marks.
        (string, bool, bool)[] two = [("a", true, false), ("b", true, false), ("c", false, false)];
        Assert.Equal(["a", "b"], Framed(editing: true, two));
        Assert.False(Marks("a", editing: true, null, two));
        Assert.False(Marks("b", editing: true, null, two));
        Assert.True(Marks("c", editing: true, null, two));

        // One selected: no frame, its own marks as before.
        (string, bool, bool)[] one = [("a", true, false), ("b", false, false), ("c", false, false)];
        Assert.Empty(Framed(editing: true, one));
        Assert.True(Marks("a", editing: true, null, one));
        Assert.True(Marks("b", editing: true, null, one));
    }

    [Fact]
    public void S14310_the_count_crossing_one_and_two_takes_the_marks_away_and_gives_them_back()
    {
        // A card alone selected shows its marks; a second added takes them from both; one taken out gives them back.
        Assert.True(Marks("a", editing: false, null, ("a", true, false), ("b", false, false)));
        Assert.False(Marks("a", editing: false, null, ("a", true, false), ("b", true, false)));
        Assert.True(Marks("a", editing: false, null, ("a", true, false), ("b", false, false)));
    }

    [Fact]
    public void S14310_in_edit_mode_a_selected_desktop_icon_is_one_of_the_frame()
    {
        (string, bool, bool)[] drawn = [("card", true, false), ("icon", true, true)];
        Assert.Equal(["card", "icon"], Framed(editing: true, drawn));
        Assert.False(Marks("icon", editing: true, null, drawn));
    }

    [Fact]
    public void S14310_on_the_locked_desktop_the_frame_goes_round_the_selected_cards_never_an_icon()
    {
        // Two cards and two icons selected, a card and an icon not: the cards are framed and show no corners; the icons keep
        // their highlight alone (no corners either way, [S-11889]) and are not counted.
        (string, bool, bool)[] drawn = [("a", true, false), ("b", true, false), ("i", true, true), ("j", true, true), ("c", false, false), ("k", false, true)];
        Assert.Equal(["a", "b"], Framed(editing: false, drawn));
        Assert.False(Marks("a", editing: false, null, drawn));
        Assert.False(Marks("b", editing: false, null, drawn));
        Assert.False(Marks("i", editing: false, null, drawn));
        Assert.False(Marks("c", editing: false, null, drawn));
    }

    [Fact]
    public void S14310_one_card_and_icons_selected_on_the_locked_desktop_is_no_frame_the_card_keeps_its_corners()
    {
        (string, bool, bool)[] drawn = [("a", true, false), ("i", true, true), ("j", true, true)];
        Assert.Empty(Framed(editing: false, drawn));
        Assert.True(Marks("a", editing: false, null, drawn));
        Assert.False(Marks("i", editing: false, null, drawn));
    }

    [Fact]
    public void S14310_a_locked_move_of_several_shows_no_marks_on_the_moving_members_and_the_neighbours_fade_as_before()
    {
        // "a" and "b" move as the selection (strength 1); "n" stands near where they land (the local grid's strength).
        (string, bool, bool)[] drawn = [("a", true, false), ("b", true, false), ("n", false, false), ("i", false, true)];
        Assert.False(Marks("a", editing: false, 1, drawn));
        Assert.False(Marks("b", editing: false, 1, drawn));
        Assert.True(Marks("n", editing: false, 0.4, drawn));
        Assert.True(Marks("i", editing: false, 0.4, drawn));

        // A single card's move: its corners as before.
        Assert.True(Marks("a", editing: false, 1, ("a", true, false), ("n", false, false)));
    }

    [Fact]
    public void S11889_a_widget_draws_its_marks_in_edit_mode_in_a_locked_move_and_selected_unless_it_is_an_icon()
    {
        Assert.True(SelectionRules.MarksShown(editing: true, selected: false, moveAlpha: null, icon: false, framed: 0));
        Assert.True(SelectionRules.MarksShown(editing: true, selected: false, moveAlpha: null, icon: true, framed: 0));
        Assert.True(SelectionRules.MarksShown(editing: false, selected: false, moveAlpha: 0.3, icon: true, framed: 0));
        Assert.True(SelectionRules.MarksShown(editing: false, selected: true, moveAlpha: null, icon: false, framed: 0));
        Assert.False(SelectionRules.MarksShown(editing: false, selected: true, moveAlpha: null, icon: true, framed: 0));
        Assert.False(SelectionRules.MarksShown(editing: false, selected: false, moveAlpha: null, icon: false, framed: 0));
    }

    [Fact]
    public void S14310_nothing_drawn_or_nothing_selected_frames_nothing()
    {
        Assert.Empty(Framed(editing: true));
        Assert.Empty(Framed(editing: false, ("a", false, false), ("b", false, false)));
    }
}
