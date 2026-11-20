using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Pomodoro.Tests;

/// <summary>[S-8115] The cycle's one sectioned progress: the sections, their fills and colours, laid along a bar and round a ring.</summary>
public sealed class PomodoroSectionsTests
{
    private static readonly int[] Classic = PomodoroCycle.Lengths(new PomodoroConfig());
    private static readonly PomodoroPhaseKind[] Kinds = [.. new PomodoroConfig().Phases.Select(p => p.Kind)];

    [Fact]
    public void S8115_a_section_per_phase_its_share_its_length_over_the_cycles()
    {
        var sections = PomodoroSections.Of(Classic, Kinds, 0, 0);

        Assert.Equal(8, sections.Count);
        Assert.Equal(1.0, sections.Sum(s => s.Share), 9);
        Assert.Equal(1500.0 / 7800, sections[0].Share, 9);
        Assert.Equal(300.0 / 7800, sections[1].Share, 9);
        Assert.Equal(900.0 / 7800, sections[7].Share, 9);
    }

    [Fact]
    public void S8115_the_phases_done_are_filled_the_current_by_its_progress_the_rest_empty()
    {
        var sections = PomodoroSections.Of(Classic, Kinds, 2, 0.4);

        Assert.Equal([1, 1, 0.4, 0, 0, 0, 0, 0], sections.Select(s => s.Fill));
    }

    [Fact]
    public void S8115_focus_fills_in_the_accent_a_break_calm()
    {
        var sections = PomodoroSections.Of(Classic, Kinds, 0, 0);

        Assert.Equal(ColourRole.Accent, sections[0].Role);
        Assert.Equal(ColourRole.TextSecondary, sections[1].Role);
        Assert.Equal(ColourRole.TextSecondary, sections[7].Role);
        Assert.NotEqual(ColourRole.Track, PomodoroSections.Role(PomodoroPhaseKind.ShortBreak));
    }

    [Theory]
    [InlineData(-1, 0.0)]
    [InlineData(2, 1.0)]
    [InlineData(double.NaN, 0.0)]
    public void S8115_a_progress_out_of_range_is_held_to_the_section(double progress, double expected)
        => Assert.Equal(expected, PomodoroSections.Of([60, 60], [PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak], 1, progress)[1].Fill);

    [Fact]
    public void S8115_along_a_bar_the_gaps_come_first_the_sections_share_the_rest_end_to_end()
    {
        var bar = PomodoroSections.Bar(312, PomodoroSections.Of([100, 100, 200], [PomodoroPhaseKind.Focus, PomodoroPhaseKind.ShortBreak, PomodoroPhaseKind.Focus], 1, 0.5));

        // 312 less two gaps of 4 is 304: 76, 76 and 152.
        Assert.Equal([0, 80, 160], bar.Select(r => r.Left));
        Assert.Equal([76, 76, 152], bar.Select(r => r.Width));
        Assert.Equal([76, 38, 0], bar.Select(r => r.Filled));
        Assert.Equal(312, bar[^1].Left + bar[^1].Width, 9);
        Assert.Equal(PomodoroSections.Gap, FamilySpace.S1);
    }

    [Fact]
    public void S8115_a_bar_too_short_for_its_gaps_lays_the_sections_touching_and_no_bar_none()
    {
        var sections = PomodoroSections.Of([1, 1, 1], [PomodoroPhaseKind.Focus, PomodoroPhaseKind.Focus, PomodoroPhaseKind.Focus], 0, 0);

        Assert.Equal([0, 2, 4], PomodoroSections.Bar(6, sections).Select(r => r.Left));
        Assert.Empty(PomodoroSections.Bar(0, sections));
        Assert.Empty(PomodoroSections.Bar(double.NaN, sections));
        Assert.Empty(PomodoroSections.Bar(100, []));
    }

    [Fact]
    public void S8115_round_a_ring_twelve_stands_in_a_gap_and_the_sections_and_gaps_make_a_whole_turn()
    {
        var sections = PomodoroSections.Of(Classic, Kinds, 3, 0.25);
        var arcs = PomodoroSections.Ring(140, sections);
        var gap = arcs[1].Start - (arcs[0].Start + arcs[0].Sweep);

        Assert.Equal(8, arcs.Count);
        Assert.Equal(gap / 2, arcs[0].Start, 9);
        Assert.Equal(360 - (gap / 2), arcs[^1].Start + arcs[^1].Sweep, 9);
        Assert.Equal(360, arcs.Sum(a => a.Sweep) + (8 * gap), 9);

        // The gap on the ring's line is the token, the round caps' half strokes added to it.
        var radius = (140 - FamilyShape.Stroke) / 2;
        Assert.Equal((PomodoroSections.Gap + FamilyShape.Stroke) / radius * 180 / Math.PI, gap, 9);
        Assert.Equal(arcs[3].Sweep * 0.25, arcs[3].Filled, 9);
        Assert.Equal(arcs[0].Sweep, arcs[0].Filled, 9);
        Assert.Equal(0, arcs[4].Filled);
    }

    [Fact]
    public void S8115_a_cycle_of_one_phase_is_the_whole_ring_and_the_whole_bar()
    {
        var one = PomodoroSections.Of([1500], [PomodoroPhaseKind.Focus], 0, 0.5);

        Assert.Equal(new SectionArc(0, 360, 180, ColourRole.Accent), Assert.Single(PomodoroSections.Ring(100, one)));
        Assert.Equal(new SectionRect(0, 200, 100, ColourRole.Accent), Assert.Single(PomodoroSections.Bar(200, one)));
    }
}
