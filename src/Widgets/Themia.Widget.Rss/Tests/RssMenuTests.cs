using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Rss.Tests;

/// <summary>[S-7510] The feed widget's options on its properties menu ([S-3719]): the feeds as a list, the kit's switches and the count.</summary>
public sealed class RssMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(RssConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        RssMenu.Lines(lines, config);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    private static RssConfig Two() => new() { Feeds = [new() { Url = "https://a.example/feed.xml" }, new() { Url = "https://b.example/rss" }] };

    [Fact]
    public void S7510_the_first_level_is_appearance_the_feeds_the_three_fields_and_the_count()
    {
        var lines = Build(new RssConfig());

        Assert.Equal(
            [
                RssStr.WidgetsC_Rss_Settings_Feeds.Text(),
                RssStr.WidgetsC_Rss_Settings_Source_Label.Text(),
                RssStr.WidgetsC_Rss_Settings_Preview_Label.Text(),
                RssStr.WidgetsC_Rss_Settings_Date_Label.Text(),
                RssStr.WidgetsC_Rss_Settings_Articles.Text(),
                Str.EditBar_Link_UpdateInterval.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));

        // [S-3749] How often the feeds are asked is ⚙'s: a link, never a setting of the widget's.
        Assert.Equal(Str.SettingsMenu_OnlineUpdates.Text(), lines.Lines[^1].LinkTo);
        Assert.Empty(lines.StyleLines);
    }

    [Fact]
    public void S7510_with_no_feed_the_list_says_so_and_offers_add()
    {
        var feeds = Build(new RssConfig()).Lines[0].Children!;

        Assert.True(feeds[0].Note);
        Assert.Equal(RssStr.WidgetsC_Rss_Settings_Empty.Text(), feeds[0].Text);
        Assert.Contains(feeds, f => f.Text == Str.Menu_Add.Text());
    }

    [Fact]
    public void S7510_a_feed_is_its_address_without_the_scheme()
    {
        var feeds = Build(Two()).Lines[0].Children!;

        Assert.Equal(["a.example/feed.xml", "b.example/rss"], feeds.Where(f => f.Children is not null).Select(f => f.Text));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("file:///C:/feed.xml")]
    [InlineData("ftp://a.example/feed")]
    public void S7510_add_refuses_what_the_feed_gate_refuses_and_says_why(string typed)
    {
        Line(Build(new RssConfig()).Lines[0].Children!, Str.Menu_Add.Text()).Action!();

        Assert.NotNull(_asked!.Refuse(typed));
    }

    [Fact]
    public void S7510_add_takes_a_web_address_trimmed_and_saves_once()
    {
        var config = new RssConfig();
        Line(Build(config).Lines[0].Children!, Str.Menu_Add.Text()).Action!();

        Assert.Null(_asked!.Refuse("https://a.example/feed.xml"));
        _asked.Done("  https://a.example/feed.xml ");

        Assert.Equal("https://a.example/feed.xml", Assert.Single(config.Feeds).Url);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7510_at_twelve_feeds_add_is_greyed()
    {
        var config = new RssConfig { Feeds = [.. Enumerable.Range(0, RssConfig.MaxFeeds).Select(i => new RssFeed { Url = $"https://{i}.example/feed" })] };

        Assert.True(Line(Build(config).Lines[0].Children!, Str.Menu_Add.Text()).Disabled);
    }

    [Fact]
    public void S7510_change_address_is_checked_and_saves_only_a_change()
    {
        var config = Two();
        var feed = Build(config).Lines[0].Children![0];
        Line(feed.Children!, RssStr.WidgetsC_Rss_Menu_ChangeAddress.Text()).Action!();

        Assert.Equal("https://a.example/feed.xml", _asked!.Initial);
        Assert.NotNull(_asked.Refuse("javascript:alert(1)"));

        _asked.Done("https://a.example/feed.xml");
        Assert.Equal(0, _saves);

        _asked.Done("https://c.example/atom");
        Assert.Equal("https://c.example/atom", config.Feeds[0].Url);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7510_a_field_switch_turns_it_off_and_saves()
    {
        var config = new RssConfig();
        Line(Build(config).Lines, RssStr.WidgetsC_Rss_Settings_Preview_Label.Text()).Action!();

        Assert.False(config.ShowPreview);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7510_the_count_is_five_to_fifty_in_fives()
    {
        var config = new RssConfig();
        var count = Line(Build(config).Lines, RssStr.WidgetsC_Rss_Settings_Articles.Text());

        Assert.Equal(10, count.Children!.Count);
        Assert.Equal(LocaleFormat.Integer(20), count.Value);
        Line(count.Children!, LocaleFormat.Integer(35)).Action!();
        Assert.Equal(35, config.MaxItems);
    }
}
