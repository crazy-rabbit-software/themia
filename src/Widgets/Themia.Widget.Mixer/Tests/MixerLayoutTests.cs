using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Mixer.Tests;

/// <summary>[S-449] [S-11309] The mixer's rows on the family's Row block: its rows and lines a setting, the slider's room, the card.</summary>
public sealed class MixerLayoutTests
{
    [Fact]
    public void S449_a_row_gives_the_slider_three_fifths_of_what_the_lead_the_gaps_and_the_mute_leave()
    {
        // 320 wide: 296 inside the inset, less 16 + 8 lead, 12, 8 and the 28 mute: 224, three fifths 134.
        var plan = MixerLayout.Plan(320, icons: true, lines: 1, rows: 4, playing: 5, quiet: 0);
        Assert.Equal(134, plan.Slider);
        Assert.True(plan.Names);
    }

    [Fact]
    public void S11309_at_its_least_width_the_slider_keeps_its_least_travel_beside_the_name()
    {
        var plan = MixerLayout.Plan(MixerLayout.Narrowest, icons: true, lines: 1, rows: 4, playing: 5, quiet: 0);
        Assert.True(plan.Slider >= Themia.Sdk.Kit.ControlsRules.MinSlider);
        Assert.True(plan.Names);
    }

    [Fact]
    public void S11309_the_rows_are_the_cards_the_lines_the_configs_and_the_quiet_apps_fill_the_rows_left()
    {
        var plan = MixerLayout.Plan(300, icons: true, lines: 2, rows: 4, playing: 2, quiet: 5);
        Assert.Equal((2, 4, 2), (plan.Lines, plan.Rows, plan.Quiet));
        Assert.Equal(0, MixerLayout.Plan(300, icons: true, lines: 1, rows: 4, playing: 6, quiet: 5).Quiet);

        // The rows a tall card holds, however many (the rows a new card stands are the config's).
        Assert.Equal(40, MixerLayout.Plan(300, true, 1, 40, 1, 0).Rows);
        Assert.Equal(1, MixerLayout.Plan(300, true, 1, 0, 1, 0).Rows);
    }

    [Fact]
    public void S11309_S14500_its_rows_are_a_new_cards_height_every_cell_from_one_row_up()
    {
        var config = new MixerConfig();
        var sizing = MixerLayout.Sizing(config);

        Assert.Equal((4, 1), (config.Rows, config.Lines));
        Assert.Equal(4 * 30, sizing.Natural.Height);
        Assert.Equal((240d, 300d, 520d), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Equal(12 * 30, sizing.Max.Height);
        Assert.Equal(4 * 40, MixerLayout.Sizing(new MixerConfig { Lines = 2 }).Natural.Height);

        Assert.Equal(30, sizing.Min.Height);
        Assert.Equal(137, sizing.FitHeight(137));
    }

    [Fact]
    public void S11309_rows_and_lines_out_of_range_are_repaired_and_copied()
    {
        var config = new MixerConfig { Rows = 99, Lines = 5 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((12, 2), (config.Rows, config.Lines));
        Assert.Equal((12, 2), (((MixerConfig)config.Clone()).Rows, ((MixerConfig)config.Clone()).Lines));
    }

    [Fact]
    public void S11805_a_one_line_rows_mute_is_inset_so_stacked_buttons_stand_apart()
    {
        Assert.Equal(24, MixerLayout.Mute(1));
        Assert.Equal(28, MixerLayout.Mute(2));
        Assert.Equal(4, Themia.Sdk.Kit.FamilyShape.RowHeight(1) - MixerLayout.Mute(1));
        Assert.True(Themia.Sdk.Kit.FamilyShape.RowHeight(2) - MixerLayout.Mute(2) >= 4);
    }
}
