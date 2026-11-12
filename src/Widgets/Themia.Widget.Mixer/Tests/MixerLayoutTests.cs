using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Mixer.Tests;

/// <summary>[S-449] The mixer's rows on the family's Row block: lines by the tier, the slider's room, whole rows.</summary>
public sealed class MixerLayoutTests
{
    // The view's area per tier: the card less its 36 header.
    private static readonly WidgetArea Xs = new(120, 84);
    private static readonly WidgetArea S = new(200, 114);
    private static readonly WidgetArea M = new(320, 204);
    private static readonly WidgetArea Wide = new(480, 124);
    private static readonly WidgetArea Tall = new(200, 364);
    private static readonly WidgetArea L = new(560, 384);

    [Fact]
    public void S449_a_named_row_gives_the_slider_three_fifths_of_what_the_lead_the_gaps_and_the_mute_leave()
    {
        // M: 296 inside the inset, less 16 + 8 lead, 12, 8 and the 28 mute: 224, three fifths 134.
        var plan = MixerLayout.Plan(M, icons: true, playing: 5, quiet: 0);

        Assert.True(plan.Names);
        Assert.Equal(134, plan.Slider);
        Assert.Equal(72, MixerLayout.Fixed(icons: true));
        Assert.Equal(48, MixerLayout.Fixed(icons: false));
    }

    [Fact]
    public void S449_a_narrow_row_with_an_icon_drops_the_name_for_the_slider_and_without_one_drops_the_slider()
    {
        var narrow = MixerLayout.Plan(S, icons: true, playing: 5, quiet: 0);
        Assert.False(narrow.Names);
        Assert.True(double.IsNaN(narrow.Slider));
        Assert.Equal(1, MixerLayout.Plan(Tall, icons: true, playing: 5, quiet: 0).Lines);

        var bare = MixerLayout.Plan(S, icons: false, playing: 5, quiet: 0);
        Assert.True(bare.Names);
        Assert.Equal(0, bare.Slider);
        Assert.Equal(0, MixerLayout.Plan(Xs, icons: true, playing: 5, quiet: 0).Slider);
        Assert.True(MixerLayout.Plan(Xs, icons: true, playing: 5, quiet: 0).Names);
    }

    [Fact]
    public void S449_rows_are_one_or_two_lines_by_the_tier_and_only_whole_ones_show()
    {
        Assert.Equal(1, MixerLayout.Plan(Wide, icons: true, playing: 9, quiet: 0).Lines);
        Assert.Equal(4, MixerLayout.Plan(Wide, icons: true, playing: 9, quiet: 0).Rows);
        Assert.Equal(2, MixerLayout.Plan(M, icons: true, playing: 9, quiet: 0).Lines);
        Assert.Equal(4, MixerLayout.Plan(M, icons: true, playing: 9, quiet: 0).Rows);
        Assert.Equal(2, MixerLayout.Plan(L, icons: true, playing: 9, quiet: 0).Lines);
        Assert.Equal(9, MixerLayout.Plan(L, icons: true, playing: 9, quiet: 0).Rows);

        // A tile too short for one whole row still shows one.
        Assert.Equal(1, MixerLayout.Plan(new WidgetArea(320, 20), icons: true, playing: 3, quiet: 0).Rows);
    }

    [Fact]
    public void S449_the_quiet_apps_follow_only_when_every_playing_row_fits()
    {
        Assert.Equal(2, MixerLayout.Plan(L, icons: true, playing: 3, quiet: 2).Quiet);
        Assert.Equal(6, MixerLayout.Plan(L, icons: true, playing: 3, quiet: 9).Quiet);
        Assert.Equal(0, MixerLayout.Plan(Wide, icons: true, playing: 4, quiet: 2).Quiet);
    }
}
