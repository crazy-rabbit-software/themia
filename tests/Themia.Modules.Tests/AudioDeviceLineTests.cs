using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1222] The audio widgets' output-device line on the properties menu: what its search answers.</summary>
public sealed class AudioDeviceLineTests
{
    private static readonly AudioDevice[] Devices = [new("{a}", "Speakers"), new("{b}", "Headphones")];

    private readonly List<(string? Id, string? Name)> _picked = [];

    private void Pick(string? id, string? name) => _picked.Add((id, name));

    [Fact]
    public void S1222_default_output_heads_the_devices_and_holds_without_a_choice()
    {
        var answer = AudioDeviceLine.Answer(Devices, string.Empty, null, null, Pick);

        Assert.Equal([Str.WidgetsC_Volume_Settings_DefaultOutput.Text(), "Speakers", "Headphones"], answer.Lines.Select(l => l.Text));
        Assert.Equal([true, false, false], answer.Lines.Select(l => l.Checked == true));
        Assert.All(answer.Lines, l => Assert.NotNull(l.Group));
    }

    [Fact]
    public void S1222_a_pick_gives_the_device_and_the_one_that_holds_gives_nothing()
    {
        var answer = AudioDeviceLine.Answer(Devices, string.Empty, "{b}", "Headphones", Pick);

        answer.Lines[2].Action!();
        Assert.Empty(_picked);

        answer.Lines[1].Action!();
        answer.Lines[0].Action!();
        Assert.Equal([("{a}", "Speakers"), (null, null)], _picked);
    }

    [Fact]
    public void S1222_a_chosen_device_not_connected_stays_listed_by_its_name()
    {
        var answer = AudioDeviceLine.Answer(Devices, string.Empty, "{gone}", "USB headset", Pick);

        var last = answer.Lines[^1];
        Assert.Equal(Str.WidgetsC_Volume_Settings_DeviceNotConnected.Format(("device", "USB headset")), last.Text);
        Assert.True(last.Checked);
    }

    [Fact]
    public void S1222_typing_narrows_the_devices_but_keeps_the_default()
    {
        var answer = AudioDeviceLine.Answer(Devices, "head", null, null, Pick);

        Assert.Equal([Str.WidgetsC_Volume_Settings_DefaultOutput.Text(), "Headphones"], answer.Lines.Select(l => l.Text));
    }
}
