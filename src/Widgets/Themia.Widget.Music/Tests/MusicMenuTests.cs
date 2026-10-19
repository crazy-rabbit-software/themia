using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-1479] Now playing's options on its properties menu.</summary>
public sealed class MusicMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(MusicConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        MusicMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S1479_appearance_then_transport_progress_and_behind_a_divider_hide_when_idle()
    {
        var lines = Build(new MusicConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Empty(lines.StyleLines);
        string?[] shape = [MusicStr.WidgetsB_Settings_Music_Transport_Label.Text(), MusicStr.WidgetsB_Settings_Music_Progress_Label.Text(), null, MusicStr.Music_HideWhenIdle_Label.Text()];
        Assert.Equal(shape, lines.Lines.Select(l => l.IsSeparator ? null : l.Text));
    }

    [Fact]
    public void S1479_the_transport_switch_turns_it_off_and_saves_once()
    {
        var config = new MusicConfig { ShowTransport = true };
        Build(config).Lines[0].Action!();

        Assert.False(config.ShowTransport);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1479_hide_when_idle_is_a_choice_and_the_current_one_saves_nothing()
    {
        var config = new MusicConfig { HideWhenIdle = MusicHideWhenIdle.Never };
        var choice = Build(config).Lines[3].Children!;

        choice[0].Action!();
        Assert.Equal(0, _saves);

        choice[2].Action!();
        Assert.Equal(MusicHideWhenIdle.NotPlaying, config.HideWhenIdle);
        Assert.Equal(1, _saves);
    }
}
