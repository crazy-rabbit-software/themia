using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-468] [S-469] [S-470] The camera's config under the root's rules: the refresh clamp, an unknown rotation, Paused through the state file (the config is its module's, so they run here). Moved from Core's VideoStreamRulesTests, unchanged in what they assert.</summary>
public sealed class VideoStreamHostTests
{
    [Theory]
    [InlineData(5, 5)]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    [InlineData(99999, 300)]
    public void S470_the_refresh_is_clamped_into_range(int raw, int expected)
        => Assert.Equal(expected, new VideoStreamConfig { RefreshSeconds = raw }.EffectiveRefreshSeconds);

    [Fact]
    public void S468_a_rotation_the_enum_does_not_name_is_read_as_none()
        => Assert.Equal(StreamRotation.None, new VideoStreamConfig { Rotation = (StreamRotation)45 }.EffectiveRotation);

    [Fact]
    public async Task S469_paused_survives_a_round_trip_through_the_state_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "state.json");

        try
        {
            var state = AppState.CreateDefault();
            var widget = WidgetInstance.Create(new WidgetId("videoStream"), new Themia.Core.Geometry.FracRect(0.1, 0.1, 0.2, 0.2));
            widget.Config = new VideoStreamConfig
            {
                Url = "http://cam/?action=stream",
                Paused = true,
                Rotation = StreamRotation.ThreeQuarter,
                Fit = StreamFit.Cover,
                RefreshSeconds = 10,
            };
            state.Views[0].Widgets.Add(widget);

            var store = new AppStateStore(path, TimeSpan.Zero);
            store.SaveSoon(state);
            await store.FlushAsync();
            await store.DisposeAsync();

            var loaded = new AppStateStore(path).Load().State;
            var back = Assert.IsType<VideoStreamConfig>(loaded.Views[0].Widgets.Single(w => w.Id == widget.Id).Config);
            AssertRoundTrip(back);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void AssertRoundTrip(VideoStreamConfig back)
    {
        Assert.True(back.Paused);
        Assert.Equal(StreamRotation.ThreeQuarter, back.Rotation);
        Assert.Equal(StreamFit.Cover, back.Fit);
        Assert.Equal(10, back.RefreshSeconds);
        Assert.Equal("http://cam/?action=stream", back.Url);
    }
}
