using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1464] [S-3200] The music widget's config under the root's rules (hide when idle read leniently and never by default; no spectrum switch since the visualizer took it): the config is the music module's, so they run here. Moved from Core's MusicParityTests and AudioVisualizerTests, unchanged in what they assert.</summary>
public sealed class MusicHostTests
{

    [Fact]
    public void S1464_an_unknown_mode_reads_as_never_the_visible_state()
    {
        var config = Assert.IsType<MusicConfig>(ConfigJson.Read("Music", """{"kind":"music","hideWhenIdle":"sometimes"}"""));
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);
    }

    [Fact]
    public void S1464_defaults_never_hide()
        => Assert.Equal(MusicHideWhenIdle.Never, new MusicConfig().HideWhenIdle);

    [Fact]
    public void S3200_music_no_longer_has_a_spectrum_switch()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new MusicConfig(), StateJson.WidgetConfig);
        Assert.DoesNotContain("spectrum", json, StringComparison.OrdinalIgnoreCase);
    }
}
