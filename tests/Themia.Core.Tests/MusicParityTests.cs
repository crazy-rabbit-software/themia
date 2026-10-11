using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1464]..[S-1469] hide when idle, seeking, the keyboard keys and the source line.</summary>
public class MusicParityTests
{
    [Theory]
    [InlineData(MusicHideWhenIdle.Never, MusicState.NoSession, false)]
    [InlineData(MusicHideWhenIdle.Never, MusicState.Loading, false)]
    [InlineData(MusicHideWhenIdle.NoSession, MusicState.NoSession, true)]
    [InlineData(MusicHideWhenIdle.NoSession, MusicState.Loading, true)]
    [InlineData(MusicHideWhenIdle.NoSession, MusicState.Idle, false)]
    [InlineData(MusicHideWhenIdle.NoSession, MusicState.Paused, false)]
    [InlineData(MusicHideWhenIdle.NotPlaying, MusicState.Paused, true)]
    [InlineData(MusicHideWhenIdle.NotPlaying, MusicState.Idle, true)]
    [InlineData(MusicHideWhenIdle.NotPlaying, MusicState.Playing, false)]
    [InlineData(MusicHideWhenIdle.NoSession, MusicState.Playing, false)]
    public void S1464_which_states_hide_the_tile(MusicHideWhenIdle mode, MusicState state, bool hides)
        => Assert.Equal(hides, MusicRules.Hides(mode, state));

    [Fact]
    public void S1464_an_unknown_mode_reads_as_never_the_visible_state()
    {
        var config = Assert.IsType<MusicConfig>(ConfigJson.Read("Music", """{"kind":"music","hideWhenIdle":"sometimes"}"""));
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);
    }

    [Fact]
    public void S1464_defaults_never_hide()
        => Assert.Equal(MusicHideWhenIdle.Never, new MusicConfig().HideWhenIdle);

    [Theory]
    [InlineData(50, 200, 0.25)]
    [InlineData(-10, 200, 0)]
    [InlineData(260, 200, 1)]
    [InlineData(10, 0, 0)]
    [InlineData(double.NaN, 200, 0)]
    public void S1465_a_pointer_maps_to_a_fraction_of_the_bar(double x, double width, double expected)
        => Assert.Equal(expected, MusicRules.ScrubFraction(x, width), 9);

    [Fact]
    public void S1465_a_fraction_maps_into_the_track_and_a_stream_has_nothing_to_seek()
    {
        Assert.Equal(TimeSpan.FromSeconds(90), MusicRules.SeekTarget(0.5, TimeSpan.FromSeconds(180)));
        Assert.Equal(TimeSpan.FromSeconds(180), MusicRules.SeekTarget(4, TimeSpan.FromSeconds(180)));
        Assert.Equal(TimeSpan.Zero, MusicRules.SeekTarget(0.5, TimeSpan.Zero));
    }

    [Fact]
    public void S1468_nudging_stays_inside_the_track()
    {
        var length = TimeSpan.FromSeconds(200);
        Assert.Equal(TimeSpan.FromSeconds(15), MusicRules.Nudge(TimeSpan.FromSeconds(10), MusicRules.NudgeStep, length));
        Assert.Equal(TimeSpan.Zero, MusicRules.Nudge(TimeSpan.FromSeconds(3), -MusicRules.NudgeStep, length));
        Assert.Equal(length, MusicRules.Nudge(TimeSpan.FromSeconds(198), MusicRules.NudgeStep, length));
    }

    [Theory]
    [InlineData(0x25, true, WidgetKey.ShiftLeft)]
    [InlineData(0x27, true, WidgetKey.ShiftRight)]
    [InlineData(0x25, false, WidgetKey.Left)]
    [InlineData(0x27, false, WidgetKey.Right)]
    public void S1468_shift_arrows_are_their_own_keys(int vk, bool shift, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(vk, shift, ctrl: false, alt: false));

    [Fact]
    public void S1469_the_album_line_is_dropped_when_it_repeats_the_artist()
    {
        Assert.Equal(string.Empty, MusicRules.Album("Adele", "adele"));
        Assert.Equal("25", MusicRules.Album("Adele", "25"));
        Assert.Equal(string.Empty, MusicRules.Album("Adele", null));
    }
}
