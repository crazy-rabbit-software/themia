using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Volume.Tests;

/// <summary>[S-2734] The volume tile's options on its properties menu.</summary>
public sealed class VolumeMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(VolumeConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        VolumeMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S2734_appearance_then_the_device_allow_changes_and_the_keys()
    {
        var lines = Build(new VolumeConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [Str.WidgetsC_Volume_Settings_Device_Label.Text(), VolumeStr.WidgetsC_Volume_Settings_Allow_Label.Text(), VolumeStr.WidgetsC_Volume_Settings_KeysNote.Text()],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S2734_allow_changes_turns_off_and_saves_once()
    {
        var config = new VolumeConfig { AllowControl = true };
        Build(config).Lines[1].Action!();

        Assert.False(config.AllowControl);
        Assert.Equal(1, _saves);
    }
}
