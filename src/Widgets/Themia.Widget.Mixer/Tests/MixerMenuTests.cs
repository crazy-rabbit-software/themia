using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Mixer.Tests;

/// <summary>[S-1235] The mixer's options on its properties menu.</summary>
public sealed class MixerMenuTests
{
    private static readonly MenuLoaded<IReadOnlyList<AudioDevice>> Two = new([new("{a}", "Speakers"), new("{b}", "Headphones")], false);

    private int _saves;

    private WidgetMenuLines Build(MixerConfig config, MenuLoaded<IReadOnlyList<AudioDevice>>? outputs = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        MixerMenu.Lines(lines, config, outputs ?? Two);
        return lines;
    }

    [Fact]
    public void S1235_appearance_then_the_device_the_list_the_icons_and_the_keys_each_behind_a_divider()
    {
        var lines = Build(new MixerConfig());

        string?[] shape =
        [
            Str.WidgetsC_Volume_Settings_Device_Label.Text(),
            null,
            MixerStr.WidgetsB_Settings_Mixer_GroupByApp_Label.Text(),
            MixerStr.Mixer_Cfg_ActiveLabel.Text(),
            MixerStr.Mixer_Cfg_SystemLabel.Text(),
            null,
            MixerStr.Mixer_Cfg_IconsLabel.Text(),
            null,
            MixerStr.WidgetsB_Settings_Mixer_Note.Text(),
        ];
        Assert.Equal(shape, lines.Lines.Select(l => l.IsSeparator ? null : l.Text));
        Assert.NotNull(lines.Lines[0].Search);
        Assert.True(lines.Lines[^1].Note);
    }

    [Fact]
    public void S1235_the_device_line_shows_the_default_until_one_is_chosen()
    {
        Assert.Equal(Str.WidgetsC_Volume_Settings_DefaultOutput.Text(), Build(new MixerConfig()).Lines[0].Search!.Value);
        Assert.Equal("Headphones", Build(new MixerConfig { DeviceId = "{b}", DeviceName = "Headphones" }).Lines[0].Search!.Value);
    }

    [Fact]
    public void S1235_with_one_output_device_the_list_comes_first()
    {
        var one = new MenuLoaded<IReadOnlyList<AudioDevice>>([new("{a}", "Speakers")], false);

        Assert.Equal(MixerStr.WidgetsB_Settings_Mixer_GroupByApp_Label.Text(), Build(new MixerConfig(), one).Lines[0].Text);
    }

    [Fact]
    public void S1235_a_switch_turns_its_setting_the_other_way_and_saves_once()
    {
        var config = new MixerConfig { OnlyActive = false };
        Build(config).Lines.Single(l => l.Text == MixerStr.Mixer_Cfg_ActiveLabel.Text()).Action!();

        Assert.True(config.OnlyActive);
        Assert.Equal(1, _saves);
    }
}
