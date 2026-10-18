using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>
/// [S-6035] [S-1987] The pomodoro's styles and its keys. Moved with the widget from Core's
/// TimeStylesTests and WidgetKeyMapTests, unchanged in what they assert.
/// </summary>
public sealed class PomodoroStyleAndKeyTests
{
    [Fact]
    public void S6035_the_pomodoro_ring_style_alone_draws_a_ring_and_digits_draws_neither()
    {
        Assert.True(new PomodoroConfig().DrawsRing);
        Assert.True(new PomodoroConfig().DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsRing);
        Assert.True(new PomodoroConfig { Style = PomodoroStyle.Bar }.DrawsBar);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsRing);
        Assert.False(new PomodoroConfig { Style = PomodoroStyle.Digits }.DrawsBar);
    }

    [Theory]
    [InlineData(WidgetKey.Space, ControlKey.Toggle)]
    [InlineData(WidgetKey.Enter, ControlKey.Toggle)]
    [InlineData(WidgetKey.Right, ControlKey.Next)]
    [InlineData(WidgetKey.Home, ControlKey.Reset)]
    [InlineData(WidgetKey.Left, ControlKey.None)]
    [InlineData(WidgetKey.Delete, ControlKey.None)]
    [InlineData(WidgetKey.Escape, ControlKey.None)]
    public void S1987_pomodoro_keys_are_its_three_buttons(WidgetKey key, ControlKey expected)
        => Assert.Equal(expected, PomodoroKeys.Of(key));
}
