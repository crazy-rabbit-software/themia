using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-1479] [S-16501] Now playing's options on its page: what it shows, element by element, then when it hides.</summary>
public sealed class MusicMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(MusicConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        MusicMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S16501_under_the_style_every_element_in_three_cards_then_hide_when_idle()
    {
        var lines = Build(new MusicConfig());

        Assert.Equal([MusicStr.Music_Shows_Track.Text(), MusicStr.Music_Shows_Progress.Text(), MusicStr.Music_Shows_Controls.Text()],
            lines.StyleLines.Select(l => l.Text));
        Assert.Equal(MusicElements.All.Select(MusicMenu.Name), lines.StyleLines.SelectMany(l => l.Children!).Select(l => l.Text));
        Assert.Equal([MusicStr.Music_HideWhenIdle_Label.Text()], lines.Lines.Select(l => l.Text));
    }

    [Fact]
    public void S16506_the_strips_page_offers_no_cover()
    {
        var lines = Build(new MusicConfig { Style = MusicStyle.Strip });
        Assert.DoesNotContain(MusicStr.Music_Element_Cover.Text(), lines.StyleLines.SelectMany(l => l.Children!).Select(l => l.Text));
    }

    [Fact]
    public void S16501_an_element_toggled_off_is_hidden_and_saved_and_the_last_one_stays()
    {
        var config = new MusicConfig();
        var cover = Build(config).StyleLines[0].Children![0];
        cover.Action!();
        Assert.DoesNotContain(MusicElement.Cover, config.Shows);
        Assert.Equal(1, _saves);

        var alone = new MusicConfig { Shows = [MusicElement.Next] };
        var next = Build(alone).StyleLines[2].Children![2];
        next.Action!();
        Assert.Equal([MusicElement.Next], alone.Shows);
    }

    [Fact]
    public void S1479_hide_when_idle_is_a_choice_and_the_current_one_saves_nothing()
    {
        var config = new MusicConfig { HideWhenIdle = MusicHideWhenIdle.Never };
        var choice = Build(config).Lines[0].Children!;

        choice[0].Action!();
        Assert.Equal(0, _saves);

        choice[2].Action!();
        Assert.Equal(MusicHideWhenIdle.NotPlaying, config.HideWhenIdle);
        Assert.Equal(1, _saves);
    }
}
