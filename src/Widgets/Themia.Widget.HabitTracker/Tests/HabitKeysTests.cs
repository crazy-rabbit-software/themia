using Themia.Core.Habits;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.HabitTracker.Tests;

/// <summary>[S-1982] Space ticks the focused habit and the page keys jump ten rows. Moved with the widget from Core's WidgetKeyMapTests, unchanged in what it asserts.</summary>
public sealed class HabitKeysTests
{
    [Fact]
    public void S1982_space_ticks_a_habit_and_page_keys_jump_ten_rows()
    {
        Assert.Equal(new HabitKeyResult(HabitKeyCommand.Toggle, 1), HabitLayout.Decide(WidgetKey.Space, 1, 3));
        Assert.Equal(new HabitKeyResult(HabitKeyCommand.Focus, 2), HabitLayout.Decide(WidgetKey.PageDown, 0, 3));
        Assert.Equal(new HabitKeyResult(HabitKeyCommand.Focus, 0), HabitLayout.Decide(WidgetKey.PageUp, 2, 3));
        Assert.Equal(HabitKeyCommand.None, HabitLayout.Decide(WidgetKey.Space, -1, 3).Command);
        Assert.Equal(new HabitKeyResult(HabitKeyCommand.Focus, 0), HabitLayout.Decide(WidgetKey.PageDown, -1, 3));
    }
}
