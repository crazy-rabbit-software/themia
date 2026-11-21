using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-1479] Now playing's options on its page.</summary>
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
    public void S1479_appearance_then_hide_when_idle_and_nothing_about_what_it_shows()
    {
        var lines = Build(new MusicConfig());

        Assert.Empty(lines.StyleLines);
        Assert.Equal([MusicStr.Music_HideWhenIdle_Label.Text()], lines.Lines.Select(l => l.Text));
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

    [Fact]
    public void S1479_every_part_of_the_family_offers_when_it_hides_and_nothing_else()
    {
        IWidgetModule[] modules = [new MusicArtistModule(), new MusicAlbumModule(), new MusicPreviousModule(), new MusicPlayPauseModule(), new MusicNextModule()];
        foreach (var module in modules)
        {
            var lines = new WidgetMenuLines(() => _saves++);
            module.MenuLines(lines, module.Descriptor.CreateDefault());
            Assert.Equal([MusicStr.Music_HideWhenIdle_Label.Text()], lines.Lines.Select(l => l.Text));
        }
    }
}
