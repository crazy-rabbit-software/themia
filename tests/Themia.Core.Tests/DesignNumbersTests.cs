using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-825] The design's numbers in one table: a fresh design holds its defaults, a loaded one its ranges.</summary>
public sealed class DesignNumbersTests
{
    private static IEnumerable<(DesignNumber Number, Func<DesignState, double> Get, Action<DesignState, double> Set)> OnTheDesign() =>
    [
        (DesignNumbers.CornerRadius, d => d.CornerRadiusPx, (d, v) => d.CornerRadiusPx = v),
        (DesignNumbers.BorderWidth, d => d.BorderWidthPx, (d, v) => d.BorderWidthPx = v),
        (DesignNumbers.BorderOpacity, d => d.BorderOpacity, (d, v) => d.BorderOpacity = v),
        (DesignNumbers.FontScale, d => d.FontScale, (d, v) => d.FontScale = v),
        (DesignNumbers.AnimationMs, d => d.IntroAnimationMs, (d, v) => d.IntroAnimationMs = v),
        (DesignNumbers.AnimationMs, d => d.TransitionAnimationMs, (d, v) => d.TransitionAnimationMs = v),
        (DesignNumbers.AnimationMs, d => d.UnlockAnimationMs, (d, v) => d.UnlockAnimationMs = v),
    ];

    [Fact]
    public void S825_a_fresh_design_holds_every_numbers_default_inside_its_range()
    {
        var fresh = new DesignState();
        foreach (var (number, get, _) in OnTheDesign())
        {
            Assert.Equal(number.Default, get(fresh));
        }

        Assert.All(DesignNumbers.All, n => Assert.InRange(n.Default, n.Min, n.Max));
    }

    [Fact]
    public void S825_a_loaded_design_is_held_to_the_tables_ranges_and_not_a_number_is_the_default()
    {
        foreach (var (number, get, set) in OnTheDesign())
        {
            foreach (var (given, expected) in new[] { (number.Max + 1000, number.Max), (number.Min - 1000, number.Min), (double.NaN, number.Default) })
            {
                var state = new AppState { Views = [ViewState.Create("Desk")] };
                set(state.Design, given);

                StateRepair.Repair(state);

                Assert.Equal(expected, get(state.Design));
            }
        }
    }

    [Fact]
    public void S825_a_display_scale_is_held_to_the_tables_range()
    {
        var state = new AppState { Views = [ViewState.Create("Desk")] };
        state.MonitorScales["A"] = 9;
        state.MonitorScales["B"] = 0.01;

        StateRepair.Repair(state);

        Assert.Equal(DesignNumbers.DisplayScale.Max, state.MonitorScales["A"]);
        Assert.Equal(DesignNumbers.DisplayScale.Min, state.MonitorScales["B"]);
    }
}
