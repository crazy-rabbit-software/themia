using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Volume.Tests;

/// <summary>[S-2734] The volume tile's options on its properties menu.</summary>
public sealed class VolumeMenuTests
{
    private static readonly MenuLoaded<IReadOnlyList<AudioDevice>> Two = new([new("{a}", "Speakers"), new("{b}", "Headphones")], false);

    private static readonly MenuLoaded<IReadOnlyList<AudioDevice>> One = new([new("{a}", "Speakers")], false);

    private int _saves;

    private WidgetMenuLines Build(VolumeConfig config, MenuLoaded<IReadOnlyList<AudioDevice>>? outputs = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        VolumeMenu.Lines(lines, config, outputs ?? Two);
        return lines;
    }

    private static IEnumerable<string> Texts(WidgetMenuLines lines) => lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text);

    [Fact]
    public void S2734_appearance_then_the_device_allow_changes_and_the_keys()
    {
        var lines = Build(new VolumeConfig());

        Assert.Equal(
            [Str.WidgetsC_Volume_Settings_Device_Label.Text(), VolumeStr.WidgetsC_Volume_Settings_Allow_Label.Text(), VolumeStr.WidgetsC_Volume_Settings_KeysNote.Text()],
            Texts(lines));
        Assert.True(lines.Lines[1].IsSeparator);
    }

    [Fact]
    public void S2734_allow_changes_turns_off_and_saves_once()
    {
        var config = new VolumeConfig { AllowControl = true };
        Build(config).Lines.Single(l => l.Text == VolumeStr.WidgetsC_Volume_Settings_Allow_Label.Text()).Action!();

        Assert.False(config.AllowControl);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S2734_the_keys_are_told_only_while_the_tile_may_change_the_volume()
        => Assert.DoesNotContain(VolumeStr.WidgetsC_Volume_Settings_KeysNote.Text(), Texts(Build(new VolumeConfig { AllowControl = false })));

    [Fact]
    public void S2734_one_output_device_offers_no_device_line_until_one_is_chosen()
    {
        var device = Str.WidgetsC_Volume_Settings_Device_Label.Text();

        Assert.DoesNotContain(device, Texts(Build(new VolumeConfig(), One)));
        Assert.False(Build(new VolumeConfig(), One).Lines[0].IsSeparator);
        Assert.Contains(device, Texts(Build(new VolumeConfig { DeviceId = "{gone}", DeviceName = "USB headset" }, One)));
    }
}
