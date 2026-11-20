using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-8110] [S-8112] The Pomodoro's options on its properties menu: the phases as a list, Repeat, the sound and its level.</summary>
public sealed class PomodoroMenuTests
{
    private int _saves;
    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(PomodoroConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        PomodoroMenu.Lines(lines, config);
        return lines;
    }

    private ItemMenuEntry PhasesLine(PomodoroConfig config) => Build(config).Lines.Single(l => l.Text == PomodoroStr.Pomodoro_Phases_Label.Text());

    private static IReadOnlyList<ItemMenuEntry> Items(ItemMenuEntry phases) => [.. phases.Children!.Where(l => l.Order is not null)];

    [Fact]
    public void S8110_the_phases_then_repeat_then_the_sound_and_its_volume_and_no_size()
    {
        var lines = Build(new PomodoroConfig());

        Assert.Empty(lines.StyleLines);
        Assert.Equal(
            [
                PomodoroStr.Pomodoro_Phases_Label.Text(), PomodoroStr.Pomodoro_Repeat_Label.Text(),
                PomodoroStr.Pomodoro_AlarmSound_Label.Text(), PomodoroStr.Pomodoro_AlarmVolume_Label.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S8110_no_volume_while_the_sound_is_off()
        => Assert.DoesNotContain(Build(new PomodoroConfig { Sound = AlarmSound.Off }).Lines, l => l.Text == PomodoroStr.Pomodoro_AlarmVolume_Label.Text());

    [Fact]
    public void S8112_each_phase_is_a_line_of_its_name_and_length_in_order_and_reorders()
    {
        var items = Items(PhasesLine(new PomodoroConfig()));

        Assert.Equal(8, items.Count);
        Assert.Equal("Focus · 25 min", items[0].Text);
        Assert.Equal("Short break · 5 min", items[1].Text);
        Assert.Equal("Long break · 15 min", items[7].Text);
        Assert.All(items, item => Assert.NotNull(item.Order));
    }

    [Fact]
    public void S8112_a_phase_dragged_to_another_place_moves_there_and_saves()
    {
        var config = new PomodoroConfig();
        var items = Items(PhasesLine(config));

        items[7].Order!.Move(7, 0);

        Assert.Equal(PomodoroPhaseKind.LongBreak, config.Phases[0].Kind);
        Assert.Equal(PomodoroPhaseKind.Focus, config.Phases[1].Kind);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S8112_a_phase_opens_rename_its_kind_its_length_and_remove()
    {
        var item = Items(PhasesLine(new PomodoroConfig()))[0];

        Assert.Equal(
            [Str.Menu_Rename.Text(), PomodoroStr.Pomodoro_Phase_Kind.Text(), PomodoroStr.Pomodoro_Phase_Duration.Text(), Str.Common_Remove.Text()],
            item.Children!.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S8112_a_phases_kind_is_picked_and_its_length_typed_from_1_to_180()
    {
        var config = new PomodoroConfig();
        var item = Items(PhasesLine(config))[1];

        var kind = item.Children!.Single(l => l.Text == PomodoroStr.Pomodoro_Phase_Kind.Text());
        kind.Children!.Single(l => l.Text == "Long break").Action!();
        Assert.Equal(PomodoroPhaseKind.LongBreak, config.Phases[1].Kind);
        Assert.Equal(1, _saves);

        var length = item.Children!.Single(l => l.Text == PomodoroStr.Pomodoro_Phase_Duration.Text());
        Assert.Equal(PomodoroMenu.Minutes(5), length.Value);
        var ask = Typed.Ask(length);
        Assert.NotNull(ask.Refuse("short"));
        ask.Done("12");
        Assert.Equal(12, config.Phases[1].Minutes);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S8112_a_length_out_of_range_on_disk_shows_the_one_in_use()
    {
        var config = new PomodoroConfig { Phases = [new() { Kind = PomodoroPhaseKind.Focus, Minutes = 900 }] };
        Assert.Equal("Focus · 180 min", Items(PhasesLine(config))[0].Text);
    }

    [Fact]
    public void S8112_rename_gives_a_phase_its_own_name_and_a_blank_one_gives_the_kinds_back()
    {
        var config = new PomodoroConfig();
        _asked = Typed.Ask(Items(PhasesLine(config))[0].Children!.Single(l => l.Text == Str.Menu_Rename.Text()));
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.Equal(string.Empty, ask.Initial);

        ask.Done("  Deep work ");
        Assert.Equal("Deep work", config.Phases[0].Name);
        Assert.Equal(1, _saves);

        Assert.False(PomodoroMenu.Rename(config.Phases[0], "Deep work"));
        Assert.True(PomodoroMenu.Rename(config.Phases[0], "   "));
        Assert.Null(config.Phases[0].Name);
    }

    [Fact]
    public void S8112_add_lines_add_a_phase_of_each_kind_at_its_classic_length()
    {
        var config = new PomodoroConfig { Phases = [PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus)] };
        var lines = PhasesLine(config).Children!;

        lines.Single(l => l.Text == PomodoroStr.Pomodoro_Phases_AddLongBreak.Text()).Action!();
        lines.Single(l => l.Text == PomodoroStr.Pomodoro_Phases_AddShortBreak.Text()).Action!();

        Assert.Equal([PomodoroPhaseKind.Focus, PomodoroPhaseKind.LongBreak, PomodoroPhaseKind.ShortBreak], config.Phases.Select(p => p.Kind));
        Assert.Equal([25, 15, 5], config.Phases.Select(p => p.Minutes));
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S8112_the_last_phase_cannot_be_removed_and_sixteen_take_no_more()
    {
        var one = Items(PhasesLine(new PomodoroConfig { Phases = [PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus)] }))[0];
        Assert.True(one.Children!.Single(l => l.Text == Str.Common_Remove.Text()).Disabled);

        var full = new PomodoroConfig { Phases = [.. Enumerable.Range(0, PomodoroConfig.MaxPhases).Select(_ => PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus))] };
        Assert.True(PhasesLine(full).Children!.Single(l => l.Text == PomodoroStr.Pomodoro_Phases_AddFocus.Text()).Disabled);
    }

    [Fact]
    public void S8114_repeat_is_a_switch_on_by_default()
    {
        var config = new PomodoroConfig();
        var repeat = Build(config).Lines.Single(l => l.Text == PomodoroStr.Pomodoro_Repeat_Label.Text());

        Assert.True(repeat.Checked);
        repeat.Action!();
        Assert.False(config.Repeat);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S8110_a_sound_picked_is_stored_once_and_the_one_it_holds_saves_nothing()
    {
        var config = new PomodoroConfig();
        var sound = Build(config).Lines.Single(l => l.Text == PomodoroStr.Pomodoro_AlarmSound_Label.Text());

        sound.Children!.Single(l => l.Checked == true).Action!();
        Assert.Equal(0, _saves);

        sound.Children!.Single(l => l.Text == PomodoroSounds.Label(AlarmSound.Off)).Action!();
        Assert.Equal(AlarmSound.Off, config.Sound);
        Assert.Equal(1, _saves);
    }
}
