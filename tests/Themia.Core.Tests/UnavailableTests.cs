using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1940]..[S-1942] A widget whose subject is not on this machine draws its own content dimmed: the
/// figures that content stands at, and the sheets the detail widgets draw for it.
/// </summary>
public sealed class UnavailableTests
{
    [Fact]
    public void S1941_a_level_stands_at_the_middle_only_for_a_thing_not_on_the_machine()
    {
        Assert.Equal(Unavailable.Share, Unavailable.Fraction(MeterReading.Missing("No battery")));

        // Present but unreadable right now is no level at all ([S-82]), not the middle.
        Assert.Null(Unavailable.Fraction(MeterReading.Unavailable("Drive C: is not answering")));

        // Available, the reading's own share, clamped.
        Assert.Equal(0.64, Unavailable.Fraction(new MeterReading(0.64, "64%", true)));
        Assert.Equal(1, Unavailable.Fraction(new MeterReading(1.3, "130%", true)));
        Assert.Null(Unavailable.Fraction(new MeterReading(double.NaN, "--", true)));
    }

    [Fact]
    public void S1941_the_middle_never_makes_up_a_number()
    {
        // The stand-in is a level, never a figure: an absent reading still reads "--".
        Assert.Equal("--", MeterReading.Missing("No battery").Primary);
        Assert.False(MeterReading.Missing("No battery").Available);
    }

    [Fact]
    public void S1941_a_slider_without_a_level_stands_at_the_middle()
    {
        Assert.Equal(50, Unavailable.Slider(null));
        Assert.Equal(50, Unavailable.Slider(double.NaN));
        Assert.Equal(70, Unavailable.Slider(70));
        Assert.Equal(100, Unavailable.Slider(140));
        Assert.Equal(0, Unavailable.Slider(-3));
    }

    [Fact]
    public void S1941_a_history_is_a_flat_line_at_the_middle_across_the_chart()
    {
        var points = Unavailable.History(60);

        Assert.Equal(60, points.Count);
        Assert.All(points, p => Assert.Equal(Unavailable.Share, p.First));
        Assert.Empty(Unavailable.History(-1));
    }

    [Fact]
    public void S1942_a_desktop_draws_a_batterys_facts_each_dimmed()
    {
        var sheet = BatteryDetailRules.Sheet(Locale.En, new BatteryPower(false, null, true, false, null), null);

        Assert.True(sheet.Absent);
        Assert.Equal("--", sheet.Hero);
        Assert.False(sheet.HeroAvailable);
        Assert.Equal("No battery", sheet.Note);
        Assert.Equal(["Time left", "Health", "Full charge capacity", "Design capacity", "Cycle count", "Voltage"], sheet.Facts.Select(f => f.Label));
        Assert.All(sheet.Facts, f => Assert.Equal(("--", false), (f.Value, f.Available)));
        Assert.Empty(sheet.Rows);
    }

    [Fact]
    public void S1942_no_adapter_draws_the_gpu_facts_each_dimmed()
    {
        var sheet = GpuDetailRules.Absent(Locale.En, "No graphics adapter");

        Assert.True(sheet.Absent);
        Assert.Equal("--", sheet.Hero);
        Assert.Equal("No graphics adapter", sheet.Note);
        Assert.Equal(4, sheet.Facts.Count);
        Assert.All(sheet.Facts, f => Assert.False(f.Available));
    }

    [Fact]
    public void S1940_a_mixer_with_no_device_stands_in_the_system_sounds_at_the_middle()
    {
        var row = MixerRules.StandIn;

        Assert.True(row.IsSystem);
        Assert.Equal(MixerRules.SystemSoundsKey, row.Key);
        Assert.Equal(Unavailable.Share, row.Level);
        Assert.False(row.Muted);
    }
}
