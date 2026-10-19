using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1222] [S-1236] The audio widgets' output-device line on the properties menu: what its search answers.</summary>
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

    [Fact]
    public void S1236_the_device_line_is_offered_only_where_it_changes_something()
    {
        MenuLoaded<IReadOnlyList<AudioDevice>> Held(params AudioDevice[] devices) => new(devices, false);

        Assert.True(AudioDeviceLine.Offers(Held(Devices), null));
        Assert.False(AudioDeviceLine.Offers(Held(Devices[0]), null));
        Assert.False(AudioDeviceLine.Offers(Held(), null));

        // A chosen device can always go back to the default; a failed listing keeps the choice working.
        Assert.True(AudioDeviceLine.Offers(Held(Devices[0]), "{gone}"));
        Assert.True(AudioDeviceLine.Offers(new(null, true), null));

        // Until the first answer: not there (the answer builds the menu again).
        Assert.False(AudioDeviceLine.Offers(new(null, false), null));
    }

    [Fact]
    public void S1236_a_line_not_offered_is_not_built()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });

        Assert.Null(AudioDeviceLine.Line(menu, new([Devices[0]], false), null, null, Pick));
        Assert.NotNull(AudioDeviceLine.Line(menu, new(Devices, false), null, null, Pick));
    }
}
