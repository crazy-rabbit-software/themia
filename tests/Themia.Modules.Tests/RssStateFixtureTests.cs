using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The Rss widget's settings as users have them, generated once with the build from before it
/// became a project (`912d89df`; Fixtures/state-rss-configured.json, ids w00.., made-up feeds): three
/// feeds (one on the local network) with every switch off and 35 articles, one feed at the least
/// articles, a refused address beside a blank one, and a fresh widget. The widget reads it from its
/// own module's JSON context now; this pins that the file still loads with no repair, reads the same
/// values and writes back unchanged.
/// </summary>
public sealed class RssStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-rss-configured.json"));

    [Fact]
    public void S44_the_rss_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the Rss widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_rss_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => Assert.IsType<RssConfig>(w.Config)).ToList();
        Assert.Equal(4, configs.Count);

        Assert.Equal(["https://news.example/rss", "https://blog.example/feed.xml", "http://192.168.1.20/feed"], configs[0].Feeds.Select(f => f.Url));
        Assert.Equal((35, false, false, false), (configs[0].MaxItems, configs[0].ShowSource, configs[0].ShowPreview, configs[0].ShowDate));
        Assert.Equal((5, true, true, true), (configs[1].MaxItems, configs[1].ShowSource, configs[1].ShowPreview, configs[1].ShowDate));
        Assert.Equal(["not a url", string.Empty], configs[2].Feeds.Select(f => f.Url));
        Assert.Empty(configs[2].FeedUrls);
        Assert.Empty(configs[3].Feeds);
        Assert.Equal(20, configs[3].MaxItems);
    }
}
