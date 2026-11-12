using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-1466] [S-6035] [S-644] The pomodoro on the timers' face: the ring, the bar, the digits, the controls by the tier.</summary>
public sealed class PomodoroFaceTests
{
    // The tiers' views: the card less its 36 header.
    private static readonly WidgetArea XS = new(120, 84);
    private static readonly WidgetArea S = new(200, 114);
    private static readonly WidgetArea M = new(320, 204);
    private static readonly WidgetArea Tall = new(200, 364);
    private static readonly WidgetArea XL = new(800, 524);

    private static PomodoroConfig Of(PomodoroStyle style) => new() { Style = style };

    [Fact]
    public void S1466_the_readout_is_sized_on_the_longer_phases_widest_time()
    {
        Assert.Equal("88:88", PomodoroFace.Worst(new PomodoroConfig()).Value);
        Assert.Equal("888:88", PomodoroFace.Worst(new PomodoroConfig { WorkMinutes = 120 }).Value);
        Assert.True(PomodoroFace.Worst(new PomodoroConfig()).LineUnder);
    }

    [Fact]
    public void S6035_the_ring_style_draws_a_ring_wherever_one_holds_the_time_else_the_bar()
    {
        var m = PomodoroFace.Plan(Of(PomodoroStyle.Ring), M);
        Assert.Equal(PomodoroLook.Ring, m.Look);
        Assert.Equal(156, m.Ring);
        Assert.True(m.Controls);

        var xl = PomodoroFace.Plan(Of(PomodoroStyle.Ring), XL);
        Assert.Equal(FamilyType.Hero136, xl.Step);
        Assert.True(xl.Under);

        // S: a 50 ring's hole cannot hold "88:88" on 28: the bar's face instead.
        Assert.Equal(PomodoroLook.Bar, PomodoroFace.Plan(Of(PomodoroStyle.Ring), S).Look);
    }

    [Fact]
    public void S6035_bar_never_rings_and_digits_draw_neither()
    {
        Assert.Equal(PomodoroLook.Bar, PomodoroFace.Plan(Of(PomodoroStyle.Bar), Tall).Look);
        Assert.Equal(PomodoroLook.Digits, PomodoroFace.Plan(Of(PomodoroStyle.Digits), Tall).Look);
        Assert.Equal(0, PomodoroFace.Plan(Of(PomodoroStyle.Digits), XL).Ring);
    }

    [Fact]
    public void S644_a_short_tile_keeps_the_time_the_controls_going_first_then_the_bar()
    {
        // 8 shorter than XS (96 × 64 of content): no room for the 28 row under the time; the bar still fits.
        var xs = PomodoroFace.Plan(Of(PomodoroStyle.Bar), new WidgetArea(120, 76));
        Assert.False(xs.Controls);
        Assert.Equal(PomodoroLook.Bar, xs.Look);
        Assert.Equal(FamilyType.Big28, xs.Step);

        // S (176 × 102): the time, the phase under it, the bar and the row fit.
        var s = PomodoroFace.Plan(Of(PomodoroStyle.Bar), S);
        Assert.True(s.Controls);
        Assert.Equal(PomodoroLook.Bar, s.Look);
        Assert.True(s.Under);

        // A strip too short for the time, the bar and the row keeps the time and the row.
        var strip = PomodoroFace.Plan(Of(PomodoroStyle.Bar), new WidgetArea(480, 84));
        Assert.True(strip.Controls);
        Assert.Equal(PomodoroLook.Digits, strip.Look);
    }
}
