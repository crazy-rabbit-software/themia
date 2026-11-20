using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-8112] [S-8113] [S-8114] The phases as config: the classic cycle, the repairs, the JSON and the copy.</summary>
public sealed class PomodoroConfigTests
{
    [Fact]
    public void S8113_the_default_is_the_classic_cycle_four_focus_phases_the_last_break_long()
    {
        var phases = new PomodoroConfig().Phases;

        Assert.Equal(
            [PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak, PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak,
             PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak, PomodoroPhaseKind.Focus, PomodoroPhaseKind.LongBreak],
            phases.Select(p => p.Kind));
        Assert.Equal([25, 5, 25, 5, 25, 5, 25, 15], phases.Select(p => p.Minutes));
        Assert.All(phases, p => Assert.Null(p.Name));
        Assert.True(new PomodoroConfig().Repeat);
    }

    [Fact]
    public void S8113_each_new_config_has_its_own_phases()
        => Assert.NotSame(new PomodoroConfig().Phases[0], new PomodoroConfig().Phases[0]);

    [Fact]
    public void S8112_a_config_without_phases_is_repaired_to_the_classic_cycle()
    {
        var config = new PomodoroConfig { Phases = [] };

        Assert.NotEmpty(config.Repair());
        Assert.Equal(8, config.Phases.Count);
        Assert.Empty(new PomodoroConfig().Repair());
    }

    [Fact]
    public void S8112_more_than_sixteen_phases_are_cut_to_the_first_sixteen()
    {
        var config = new PomodoroConfig { Phases = [.. Enumerable.Range(1, 20).Select(i => PomodoroPhaseSpec.Of(PomodoroPhaseKind.Focus, i))] };

        Assert.NotEmpty(config.Repair());
        Assert.Equal(PomodoroConfig.MaxPhases, config.Phases.Count);
        Assert.Equal(16, config.Phases[^1].Minutes);
    }

    [Fact]
    public void S8101_a_length_out_of_range_and_an_unknown_kind_are_repaired()
    {
        var config = new PomodoroConfig { Phases = [new() { Kind = (PomodoroPhaseKind)9, Minutes = 0 }, new() { Kind = PomodoroPhaseKind.LongBreak, Minutes = 900 }] };

        Assert.Equal(2, config.Repair().Count(r => r.Contains("length", StringComparison.Ordinal)));
        Assert.Equal(PomodoroPhaseKind.Focus, config.Phases[0].Kind);
        Assert.Equal([1, 180], config.Phases.Select(p => p.Minutes));
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData("  Reading ", "Reading")]
    [InlineData("A name that is much longer than thirty-two characters", "A name that is much longer than ")]
    public void S8112_a_phase_name_is_trimmed_cut_and_cleared_when_blank(string stored, string? expected)
    {
        var config = new PomodoroConfig { Phases = [new() { Kind = PomodoroPhaseKind.Focus, Name = stored, Minutes = 25 }] };

        Assert.NotEmpty(config.Repair());
        Assert.Equal(expected, config.Phases[0].Name);
    }

    [Fact]
    public void S8112_the_phases_and_repeat_read_from_json_kinds_by_name_leniently()
    {
        var config = Assert.IsType<PomodoroConfig>(ConfigJson.Read("Pomodoro", """
            {"kind":"pomodoro","style":"bar","repeat":false,"phases":[
              {"kind":"focus","minutes":50,"name":"Deep work"},
              {"kind":"LongBreak","minutes":10},
              {"kind":"nap","minutes":20}
            ]}
            """));

        Assert.False(config.Repeat);
        Assert.Equal(PomodoroStyle.Bar, config.Style);
        Assert.Equal([PomodoroPhaseKind.Focus, PomodoroPhaseKind.LongBreak, PomodoroPhaseKind.Focus], config.Phases.Select(p => p.Kind));
        Assert.Equal([50, 10, 20], config.Phases.Select(p => p.Minutes));
        Assert.Equal("Deep work", config.Phases[0].Name);
    }

    [Fact]
    public void S8112_a_kind_is_written_by_its_camel_case_name_and_a_name_only_where_given()
    {
        var json = JsonSerializer.Serialize(new PomodoroConfig { Phases = [PomodoroPhaseSpec.Of(PomodoroPhaseKind.ShortBreak)] }, PomodoroJsonContext.Default.PomodoroConfig);

        Assert.Contains("\"kind\": \"shortBreak\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"size\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("workMinutes", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S8117_the_pomodoro_holds_no_size_setting()
        => Assert.IsNotAssignableFrom<IReadoutSized>(new PomodoroConfig());

    [Fact]
    public void S8112_a_copy_holds_its_own_phases()
    {
        var config = new PomodoroConfig { Repeat = false, Style = PomodoroStyle.Digits };
        config.Phases[0].Name = "Writing";
        var copy = Assert.IsType<PomodoroConfig>(config.Clone());

        copy.Phases[0].Minutes = 50;
        copy.Phases.RemoveAt(1);

        Assert.Equal(25, config.Phases[0].Minutes);
        Assert.Equal(8, config.Phases.Count);
        Assert.Equal("Writing", copy.Phases[0].Name);
        Assert.False(copy.Repeat);
        Assert.Equal(PomodoroStyle.Digits, copy.Style);
    }
}
