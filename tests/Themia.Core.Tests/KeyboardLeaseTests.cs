using Themia.Core.Habits;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-800]..[S-812] The keyboard lease and the key map it delivers.</summary>
public sealed class KeyboardLeaseTests
{
    private sealed class W(string name)
    {
        public override string ToString() => name;
    }

    private static readonly W List = new("list");
    private static readonly W Other = new("other-list");
    private static readonly W Clock = new("clock");
    private static readonly W Calc = new("calculator");

    // ---- [S-800] a click grants, a click elsewhere ends ----------------------------------------

    [Fact]
    public void S800_a_click_on_a_widget_with_keys_takes_the_keyboard()
    {
        var lease = new KeyboardLease<W>();

        Assert.Equal(new LeaseChange<W>(null, List, false), lease.Press(List, takesKeys: true));
        Assert.Same(List, lease.Holder);
        Assert.False(lease.Typing);
    }

    [Fact]
    public void S800_a_click_on_a_widget_without_keys_takes_nothing()
    {
        var lease = new KeyboardLease<W>();

        Assert.True(lease.Press(Clock, takesKeys: false).IsNone);
        Assert.Null(lease.Holder);
    }

    [Fact]
    public void S800_a_second_click_inside_the_holder_changes_nothing()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.True(lease.Press(List, takesKeys: true).IsNone);
        Assert.Same(List, lease.Holder);
    }

    [Fact]
    public void S800_a_click_on_another_list_moves_the_lease()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.Equal(new LeaseChange<W>(List, Other, false), lease.Press(Other, takesKeys: true));
        Assert.Same(Other, lease.Holder);
    }

    [Fact]
    public void S800_a_click_on_a_widget_without_keys_is_a_click_elsewhere()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.Equal(new LeaseChange<W>(List, null, false), lease.Press(Clock, takesKeys: false));
        Assert.Null(lease.Holder);
    }

    // ---- [S-801] text boxes ride the same lease ------------------------------------------------

    [Fact]
    public void S801_a_focused_text_box_takes_the_keyboard_and_gives_it_back_on_blur()
    {
        var lease = new KeyboardLease<W>();

        Assert.Equal(new LeaseChange<W>(null, Calc, false), lease.Type(Calc, typing: true, takesKeys: false));
        Assert.True(lease.Typing);
        Assert.Equal(new LeaseChange<W>(Calc, null, false), lease.Type(Calc, typing: false, takesKeys: false));
        Assert.Null(lease.Holder);
    }

    [Fact]
    public void S801_a_list_whose_editor_closes_keeps_the_arrows()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.True(lease.Type(List, typing: true, takesKeys: true).IsNone);
        Assert.True(lease.Typing);
        Assert.True(lease.Type(List, typing: false, takesKeys: true).IsNone);
        Assert.Same(List, lease.Holder);
        Assert.False(lease.Typing);
    }

    [Fact]
    public void S801_a_press_on_the_typing_widget_before_or_after_its_focus_keeps_it()
    {
        // The box's GotFocus and the card's press arrive in either order; neither may drop the other.
        var lease = new KeyboardLease<W>();
        lease.Type(Calc, typing: true, takesKeys: false);

        Assert.True(lease.Press(Calc, takesKeys: false).IsNone);
        Assert.Same(Calc, lease.Holder);
    }

    [Fact]
    public void S801_a_late_blur_from_the_previous_holder_does_not_end_the_new_lease()
    {
        var lease = new KeyboardLease<W>();
        lease.Type(Calc, typing: true, takesKeys: false);
        lease.Press(List, takesKeys: true);

        Assert.True(lease.Type(Calc, typing: false, takesKeys: false).IsNone);
        Assert.Same(List, lease.Holder);
    }

    [Fact]
    public void S801_focus_moving_between_two_text_widgets_hands_over_in_either_order()
    {
        var lease = new KeyboardLease<W>();
        lease.Type(Calc, typing: true, takesKeys: false);

        Assert.Equal(new LeaseChange<W>(Calc, Other, false), lease.Type(Other, typing: true, takesKeys: false));
        Assert.True(lease.Type(Calc, typing: false, takesKeys: false).IsNone);
        Assert.Same(Other, lease.Holder);
    }

    [Fact]
    public void S801_a_blur_without_a_focus_changes_nothing()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.True(lease.Type(List, typing: false, takesKeys: false).IsNone);
        Assert.Same(List, lease.Holder);
    }

    // ---- [S-802] every way it ends ---------------------------------------------------------------

    [Fact]
    public void S802_escape_ends_it_and_gives_the_keyboard_back()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.Equal(new LeaseChange<W>(List, null, true), lease.Escape());
        Assert.Null(lease.Holder);
        Assert.True(lease.Escape().IsNone);
    }

    [Fact]
    public void S802_clear_ends_it_without_giving_the_keyboard_anywhere()
    {
        var lease = new KeyboardLease<W>();
        lease.Type(Calc, typing: true, takesKeys: false);

        Assert.Equal(new LeaseChange<W>(Calc, null, false), lease.Clear());
        Assert.False(lease.Typing);
        Assert.True(lease.Clear().IsNone);
    }

    [Fact]
    public void S802_removing_the_holder_ends_it_and_removing_another_does_not()
    {
        var lease = new KeyboardLease<W>();
        lease.Press(List, takesKeys: true);

        Assert.True(lease.Remove(Other).IsNone);
        Assert.Same(List, lease.Holder);
        Assert.Equal(new LeaseChange<W>(List, null, false), lease.Remove(List));
    }

    // ---- [S-805] never taken without the user asking -----------------------------------------------

    [Fact]
    public void S805_nothing_but_a_press_or_a_focused_box_grants()
    {
        var lease = new KeyboardLease<W>();

        Assert.True(lease.Escape().IsNone);
        Assert.True(lease.Clear().IsNone);
        Assert.True(lease.Remove(List).IsNone);
        Assert.True(lease.Type(List, typing: false, takesKeys: true).IsNone);
        Assert.Null(lease.Holder);
    }

    // ---- [S-803] the key map -----------------------------------------------------------------------

    [Theory]
    [InlineData(0x25, false, WidgetKey.Left)]
    [InlineData(0x27, false, WidgetKey.Right)]
    [InlineData(0x26, false, WidgetKey.Up)]
    [InlineData(0x28, false, WidgetKey.Down)]
    [InlineData(0x28, true, WidgetKey.Down)]
    [InlineData(0x21, false, WidgetKey.PageUp)]
    [InlineData(0x22, false, WidgetKey.PageDown)]
    [InlineData(0x24, false, WidgetKey.Home)]
    [InlineData(0x23, false, WidgetKey.End)]
    [InlineData(0x09, false, WidgetKey.Tab)]
    [InlineData(0x09, true, WidgetKey.ShiftTab)]
    [InlineData(0x0D, false, WidgetKey.Enter)]
    [InlineData(0x1B, false, WidgetKey.Escape)]
    [InlineData(0x20, false, WidgetKey.Space)]
    [InlineData(0x2E, false, WidgetKey.Delete)]
    [InlineData(0x71, false, WidgetKey.Rename)]
    [InlineData(0x5D, false, WidgetKey.Menu)]
    [InlineData(0x79, true, WidgetKey.Menu)]
    public void S803_keys_map_to_widget_keys(int vk, bool shift, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(vk, shift, ctrl: false, alt: false));

    [Theory]
    [InlineData(0x41, false, false, false)] // a letter: type-ahead is not a widget key
    [InlineData(0x79, false, false, false)] // F10 alone is the menu bar
    [InlineData(0x2E, true, false, false)] // Shift+Delete: never a permanent delete by accident
    [InlineData(0x28, false, true, false)] // Ctrl+Down
    [InlineData(0x25, false, false, true)] // Alt+Left
    [InlineData(0x1B, false, true, false)] // Ctrl+Esc is the Start menu
    public void S803_chords_and_other_keys_are_not_widget_keys(int vk, bool shift, bool ctrl, bool alt)
        => Assert.Null(WidgetKeys.FromVirtualKey(vk, shift, ctrl, alt));

    // ---- [S-812] a lease with no row focused yet -------------------------------------------------

    [Theory]
    [InlineData(WidgetKey.Down, TaskKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Up, TaskKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Home, TaskKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.End, TaskKeyCommand.Focus, 4)]
    [InlineData(WidgetKey.Enter, TaskKeyCommand.None, -1)]
    [InlineData(WidgetKey.Delete, TaskKeyCommand.None, -1)]
    [InlineData(WidgetKey.Rename, TaskKeyCommand.None, -1)]
    [InlineData(WidgetKey.Escape, TaskKeyCommand.Release, -1)]
    public void S812_tasks_without_a_focused_row(WidgetKey key, TaskKeyCommand command, int index)
        => Assert.Equal(new TaskKeyResult(command, index), TaskLayout.Decide(key, -1, 5));

    [Theory]
    [InlineData(WidgetKey.Down, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.Tab, HabitKeyCommand.Focus, 0)]
    [InlineData(WidgetKey.ShiftTab, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.End, HabitKeyCommand.Focus, 2)]
    [InlineData(WidgetKey.Enter, HabitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Delete, HabitKeyCommand.None, -1)]
    [InlineData(WidgetKey.Escape, HabitKeyCommand.Release, -1)]
    public void S812_habits_without_a_focused_row(WidgetKey key, HabitKeyCommand command, int index)
        => Assert.Equal(new HabitKeyResult(command, index), HabitLayout.Decide(key, -1, 3));

    [Fact]
    public void S812_keys_the_lists_do_not_answer_do_nothing()
    {
        Assert.Equal(TaskKeyCommand.None, TaskLayout.Decide(WidgetKey.Left, 1, 3).Command);
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.Left, 1, 3).Command);
    }
}
