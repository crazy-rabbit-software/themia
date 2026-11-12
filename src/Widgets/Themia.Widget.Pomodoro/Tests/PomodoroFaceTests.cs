using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-1466] [S-6035] [S-644] The pomodoro on the timers' face: the ring, the bar, the digits and the controls at its Size.</summary>
public sealed class PomodoroFaceTests
{
    private static PomodoroConfig Of(PomodoroStyle style) => new() { Style = style };

    [Fact]
    public void S1466_the_readout_is_sized_on_the_longer_phases_widest_time()
    {
        Assert.Equal("88:88", PomodoroFace.Worst(new PomodoroConfig()).Value);
        Assert.Equal("888:88", PomodoroFace.Worst(new PomodoroConfig { WorkMinutes = 120 }).Value);
        Assert.True(PomodoroFace.Worst(new PomodoroConfig()).LineUnder);
    }

    [Fact]
    public void S11304_the_ring_is_the_ring_style_at_every_size_the_bar_the_bar_style_digits_neither()
    {
        var ring = PomodoroFace.Plan(Of(PomodoroStyle.Ring));
        Assert.Equal(PomodoroLook.Ring, ring.Look);
        Assert.Equal(FamilyType.Big40, ring.Step);
        Assert.True(ring.Ring > 0);
        Assert.Equal(PomodoroLook.Ring, PomodoroFace.Plan(new PomodoroConfig { Size = ReadoutSize.Small }).Look);

        Assert.Equal(PomodoroLook.Bar, PomodoroFace.Plan(Of(PomodoroStyle.Bar)).Look);
        Assert.Equal(FamilyType.Hero68, PomodoroFace.Plan(Of(PomodoroStyle.Bar)).Step);
        Assert.Equal(PomodoroLook.Digits, PomodoroFace.Plan(Of(PomodoroStyle.Digits)).Look);
        Assert.Equal(0, PomodoroFace.Plan(Of(PomodoroStyle.Digits)).Ring);
    }

    [Fact]
    public void S11304_the_card_is_the_face_the_bar_on_its_style_and_the_controls_one_height()
    {
        var digits = PomodoroFace.Sizing(Of(PomodoroStyle.Digits), Locale.En);
        var bar = PomodoroFace.Sizing(Of(PomodoroStyle.Bar), Locale.En);
        var small = PomodoroFace.Sizing(new PomodoroConfig { Style = PomodoroStyle.Digits, Size = ReadoutSize.Small }, Locale.En);

        Assert.True(digits.FixedHeight && !digits.FixedWidth);
        Assert.Equal(12 + 92 + 20 + 8 + 28 + 12 + 2, digits.Natural.Height);
        Assert.Equal(digits.Natural.Height + 12, bar.Natural.Height);

        // At Small the five controls are icon buttons, wider than the 40 time.
        Assert.Equal((5 * 28) + (4 * 8) + 24 + 2, small.Natural.Width);
    }
}
