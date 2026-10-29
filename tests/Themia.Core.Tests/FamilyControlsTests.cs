using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6716] The Controls row's main icon action and its switch (group G5: the transports' Play, the alarm's on/off).</summary>
public sealed class FamilyControlsTests
{
    [Fact]
    public void S6716_a_switch_is_40_wide_and_stays_with_the_main_action()
    {
        ControlPart[] parts = [new(ControlKind.Switch, ControlsRules.SwitchWidth), new(ControlKind.Button, 72), new(ControlKind.Icon, 32)];

        Assert.Equal(40, ControlsRules.SwitchWidth);
        Assert.Equal(40 + 72 + 32 + (2 * 8), ControlsRules.Needs(parts, [true, true, true]));
        Assert.Equal([true, true, false], ControlsRules.Shown(130, parts));
        Assert.Equal([true, false, false], ControlsRules.Shown(60, parts));
        Assert.Equal([true, false, false], ControlsRules.Shown(10, parts));
    }

    [Fact]
    public void S6716_a_transport_keeps_its_play_and_drops_its_icons_from_the_last()
    {
        // Previous, Play (the main action as a 32 icon button), Next.
        ControlPart[] parts = [new(ControlKind.Icon, 32), new(ControlKind.Main, 32), new(ControlKind.Icon, 32)];

        Assert.Equal(112, ControlsRules.Needs(parts, [true, true, true]));
        Assert.Equal([true, true, false], ControlsRules.Shown(100, parts));
        Assert.Equal([false, true, false], ControlsRules.Shown(60, parts));
        Assert.Throws<ArgumentException>(() => ControlsRules.Check([new(ControlKind.Main, 32), new(ControlKind.Main, 72)]));
    }
}
