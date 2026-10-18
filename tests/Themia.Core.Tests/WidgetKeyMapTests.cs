using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1980]..[S-1995] Type-ahead and the per-widget key maps.</summary>
public sealed class WidgetKeyMapTests
{
    private static readonly string[] Names = ["Alpha", "beta", "Bravo", "charlie", "Beth", "  Delta"];

    // ---- type-ahead ([S-1980]) -------------------------------------------------------------------

    [Fact]
    public void S1980_a_prefix_lands_on_the_first_match_ignoring_case()
    {
        var typeAhead = new TypeAhead();

        Assert.Equal(1, typeAhead.Type('B', 0, Names, -1));
        Assert.Equal(1, typeAhead.Type('e', 100, Names, 1));
        Assert.Equal(1, typeAhead.Type('t', 200, Names, 1));
        Assert.Equal(4, typeAhead.Type('h', 300, Names, 4));
        Assert.Equal("Beth", typeAhead.Prefix, ignoreCase: true);
    }

    [Fact]
    public void S1980_a_growing_prefix_stays_on_the_item_it_already_matches()
    {
        var typeAhead = new TypeAhead();

        Assert.Equal(2, typeAhead.Type('b', 0, Names, 1));
        Assert.Equal(2, typeAhead.Type('r', 50, Names, 2));
    }

    [Fact]
    public void S1980_the_same_letter_again_steps_through_its_items_and_wraps()
    {
        var typeAhead = new TypeAhead();

        Assert.Equal(1, typeAhead.Type('b', 0, Names, -1));
        Assert.Equal(2, typeAhead.Type('b', 100, Names, 1));
        Assert.Equal(4, typeAhead.Type('b', 200, Names, 2));
        Assert.Equal(1, typeAhead.Type('b', 300, Names, 4));
    }

    [Fact]
    public void S1980_a_pause_starts_a_new_prefix()
    {
        var typeAhead = new TypeAhead();

        Assert.Equal(1, typeAhead.Type('b', 0, Names, -1));
        Assert.Equal(3, typeAhead.Type('c', TypeAhead.ResetAfterMs + 1, Names, 1));
        Assert.Equal("c", typeAhead.Prefix);

        // Within the pause it keeps building ("bc" names nothing, so the cursor stays put).
        var fresh = new TypeAhead();
        Assert.Equal(1, fresh.Type('b', 0, Names, -1));
        Assert.Null(fresh.Type('c', TypeAhead.ResetAfterMs, Names, 1));
        Assert.Equal("bc", fresh.Prefix);
    }

    [Fact]
    public void S1980_control_characters_and_a_leading_space_do_not_type()
    {
        var typeAhead = new TypeAhead();

        Assert.Null(typeAhead.Type(' ', 0, Names, -1));
        Assert.Null(typeAhead.Type('\b', 10, Names, -1));
        Assert.Null(typeAhead.Type('\r', 20, Names, -1));
        Assert.Equal(string.Empty, typeAhead.Prefix);

        // Inside a prefix a space is part of the name, and leading blanks of a name are skipped.
        Assert.Equal(new int?(0), TypeAhead.Find(["Mary Ann", "Mary Lou"], -1, "mary a"));
        Assert.Equal(1, TypeAhead.Find(["Mary Ann", "Mary Lou"], -1, "mary l"));
        Assert.Equal(5, typeAhead.Type('d', 30, Names, -1));
    }

    [Fact]
    public void S1980_no_match_and_an_empty_list_leave_the_cursor()
    {
        Assert.Null(TypeAhead.Find(Names, 2, "zz"));
        Assert.Null(TypeAhead.Find([], -1, "a"));
        Assert.Null(TypeAhead.Find(Names, -1, string.Empty));

        var typeAhead = new TypeAhead();
        Assert.Equal(0, typeAhead.Type('a', 0, Names, -1));
        typeAhead.Reset();
        Assert.Equal(string.Empty, typeAhead.Prefix);
    }

    [Fact]
    public void S1980_a_cursor_past_the_end_searches_from_the_top()
        => Assert.Equal(1, TypeAhead.Find(Names, 99, "b"));

    // ---- tasks and habits ([S-1981], [S-1982]) -------------------------------------------------------

    [Fact]
    public void S1981_space_ticks_a_task_and_page_keys_jump_ten_rows()
    {
        Assert.Equal(new KitKeyResult(KitKeyCommand.Toggle, 2), KitKeys.Decide(WidgetKey.Space, 2, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 12), KitKeys.Decide(WidgetKey.PageDown, 2, 30));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 4), KitKeys.Decide(WidgetKey.PageDown, 2, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 3), KitKeys.Decide(WidgetKey.PageUp, 13, 30));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.PageUp, 3, 30));
    }

    [Fact]
    public void S1981_without_a_focused_task_space_ticks_nothing_and_page_keys_land_on_the_first()
    {
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.Space, -1, 5).Command);
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.PageDown, -1, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.PageUp, -1, 5));
    }

    // ---- contacts, calendar, the shared cursor ([S-1983]..[S-1985]) ----------------------------------

    [Fact]
    public void S1983_page_keys_jump_ten_people()
    {
        var ids = Enumerable.Range(0, 25).Select(i => $"p{i}").ToList();

        Assert.Equal("p13", ContactRules.MoveSelection(ids, "p3", WidgetKey.PageDown));
        Assert.Equal("p24", ContactRules.MoveSelection(ids, "p20", WidgetKey.PageDown));
        Assert.Equal("p0", ContactRules.MoveSelection(ids, "p7", WidgetKey.PageUp));
        Assert.Equal("p0", ContactRules.MoveSelection(ids, null, WidgetKey.PageUp));
        Assert.Equal("p0", ContactRules.MoveSelection(ids, null, WidgetKey.PageDown));
    }

    [Theory]
    [InlineData(CalendarStyle.Month, WidgetKey.PageUp, CalendarKeyCommand.Back)]
    [InlineData(CalendarStyle.Month, WidgetKey.PageDown, CalendarKeyCommand.Forward)]
    [InlineData(CalendarStyle.Week, WidgetKey.PageDown, CalendarKeyCommand.Forward)]
    [InlineData(CalendarStyle.Day, WidgetKey.PageUp, CalendarKeyCommand.Back)]
    [InlineData(CalendarStyle.Agenda, WidgetKey.PageUp, CalendarKeyCommand.Back)]
    [InlineData(CalendarStyle.Year, WidgetKey.PageDown, CalendarKeyCommand.Forward)]
    public void S2710_page_keys_page_every_calendar_layout(CalendarStyle layout, WidgetKey key, CalendarKeyCommand expected)
        => Assert.Equal(expected, CalendarFit.Key(key, layout));

    [Theory]
    [InlineData(30, 3, WidgetKey.PageDown, 13)]
    [InlineData(30, 25, WidgetKey.PageDown, 29)]
    [InlineData(30, 13, WidgetKey.PageUp, 3)]
    [InlineData(30, 4, WidgetKey.PageUp, 0)]
    [InlineData(30, null, WidgetKey.PageUp, 0)]
    [InlineData(30, null, WidgetKey.PageDown, 0)]
    [InlineData(0, null, WidgetKey.PageDown, null)]
    public void S1985_the_shared_cursor_pages_ten_items(int count, int? current, WidgetKey key, int? expected)
        => Assert.Equal(expected, ItemCursor.Move(count, current, key));

    // ---- one-control widgets ([S-1987], [S-1990]..[S-1992]) ---------------------------------------

    [Fact]
    public void S1991_only_space_pauses_the_stream()
    {
        Assert.Equal(ControlKey.Toggle, ControlKeys.Stream(WidgetKey.Space));
        Assert.Equal(ControlKey.None, ControlKeys.Stream(WidgetKey.Enter));
        Assert.Equal(ControlKey.None, ControlKeys.Stream(WidgetKey.Right));
    }

    [Theory]
    [InlineData(WidgetKey.Left, 4, ControlKey.Previous)]
    [InlineData(WidgetKey.Right, 4, ControlKey.Next)]
    [InlineData(WidgetKey.Space, 4, ControlKey.Toggle)]
    [InlineData(WidgetKey.Home, 4, ControlKey.First)]
    [InlineData(WidgetKey.End, 4, ControlKey.Last)]
    [InlineData(WidgetKey.Enter, 4, ControlKey.Open)]
    [InlineData(WidgetKey.Up, 4, ControlKey.None)]
    [InlineData(WidgetKey.Right, 0, ControlKey.None)]
    public void S1992_slideshow_keys(WidgetKey key, int pictures, ControlKey expected)
        => Assert.Equal(expected, ControlKeys.Slideshow(key, pictures));

    [Fact]
    public void S3802_alt_shift_up_and_down_are_the_reorder_keys_and_nothing_else_is()
    {
        Assert.Equal(WidgetKey.MoveUp, WidgetKeys.FromVirtualKey(0x26, shift: true, ctrl: false, alt: true));
        Assert.Equal(WidgetKey.MoveDown, WidgetKeys.FromVirtualKey(0x28, shift: true, ctrl: false, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x26, shift: true, ctrl: true, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x26, shift: false, ctrl: false, alt: true));
        Assert.Null(WidgetKeys.FromVirtualKey(0x25, shift: true, ctrl: false, alt: true));
        Assert.Equal(WidgetKey.Up, WidgetKeys.FromVirtualKey(0x26, shift: true, ctrl: false, alt: false));
    }

}
