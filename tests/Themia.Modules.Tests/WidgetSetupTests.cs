using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1860]..[S-1862] Which widgets show the "Set up" button instead of their body: the ones that
/// cannot show anything until they are configured, and only while that is so.
/// </summary>
public class WidgetSetupTests
{
    private static readonly FeatureSet Everything = FeatureSet.Of(HostFeatures.Mail, HostFeatures.Calendar, HostFeatures.Contacts, HostFeatures.Tasks);

    /// <summary>Every kind whose default config needs setting up, with nothing signed in.</summary>
    private static readonly WidgetId[] NeedSetupByDefault =
    [
        new WidgetId("countdown"), new WidgetId("rss"), new WidgetId("stocks"), new WidgetId("weatherNow"), new WidgetId("weatherForecast"), new WidgetId("airQuality"), new WidgetId("rainNextHour"),
        new WidgetId("videoStream"), new WidgetId("gallery"),
        new WidgetId("email"), new WidgetId("calendar"), new WidgetId("nextMeeting"),
        new WidgetId("weekPlanner"), new WidgetId("contacts"), new WidgetId("tasks"),
    ];

    [Fact]
    public void S1860_a_new_widget_asks_to_be_set_up_exactly_when_it_cannot_show_anything_yet()
    {
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            var needed = WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), FeatureSet.None);
            Assert.True(needed == NeedSetupByDefault.Contains(kind), $"{kind}: expected {NeedSetupByDefault.Contains(kind)}");
        }
    }

    [Fact]
    public void S1860_widgets_that_work_out_of_the_box_never_ask()
    {
        // Seeded quotes and shortcuts, a default place, a power button, the profile folder.
        foreach (var kind in new[] { new WidgetId("quote"), new WidgetId("shortcuts"), new WidgetId("sun"), new WidgetId("moon"), new WidgetId("shutdown"), new WidgetId("folder"), new WidgetId("clock") })
        {
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), FeatureSet.None), kind.ToString());
        }
    }

    [Fact]
    public void S1860_configuring_the_missing_piece_takes_the_button_away()
    {
        var countdown = new CountdownConfig();
        countdown.Retarget(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.False(WidgetSetup.Needed(countdown, FeatureSet.None));

        Assert.False(WidgetSetup.Needed(new RssConfig { Feeds = [new RssFeed { Url = "https://example.com/feed" }] }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new StocksConfig { Symbol = "MSFT" }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new WeatherNowConfig { Latitude = 47.4, Longitude = 8.5 }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new VideoStreamConfig { Url = "http://cam.local/stream" }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Path = @"C:\Pictures" }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Source = GallerySource.Bing }, FeatureSet.None));

        var shortcuts = new ShortcutsConfig();
        shortcuts.Items.Add(new ShortcutItem { Id = "a", Target = @"C:\Windows\notepad.exe" });
        Assert.False(WidgetSetup.Needed(shortcuts, FeatureSet.None));
    }

    [Fact]
    public void S1860_emptying_a_list_the_widget_came_with_asks_again()
    {
        Assert.True(WidgetSetup.Needed(new QuoteConfig { Quotes = [] }, FeatureSet.None));
        Assert.True(WidgetSetup.Needed(new QuoteConfig { Quotes = [new QuoteItem { Text = "  " }] }, FeatureSet.None));
        Assert.True(WidgetSetup.Needed(new ShortcutsConfig(), FeatureSet.None));
    }

    [Fact]
    public void S1860_set_up_but_wrong_keeps_the_widgets_own_sentence()
    {
        // These say what is wrong ("not a symbol", "not a feed") rather than asking to start.
        Assert.False(WidgetSetup.Needed(new StocksConfig { Symbol = "not a symbol!" }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new RssConfig { Feeds = [new RssFeed { Url = "not a url" }] }, FeatureSet.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Path = @"Z:\gone" }, FeatureSet.None));
    }

    [Theory]
    [InlineData("email", "mail")]
    [InlineData("calendar", "calendar")]
    [InlineData("contacts", "contacts")]
    [InlineData("tasks", "tasks")]
    public void S1861_an_account_widget_asks_until_an_account_offering_its_feature_is_signed_in(string kindId, string featureId)
    {
        var kind = new WidgetId(kindId);
        var feature = new FeatureId(featureId);
        var config = WidgetConfig.CreateDefault(kind);

        Assert.True(WidgetSetup.Needed(config, Everything.Without(feature)));
        Assert.False(WidgetSetup.Needed(config, feature));
    }

    [Fact]
    public void S1861_a_local_task_list_needs_no_account()
        => Assert.False(WidgetSetup.Needed(new TasksConfig { Source = TaskSource.Local }, FeatureSet.None));

    [Fact]
    public void S1861_features_are_the_union_of_the_signed_in_providers()
    {
        Assert.Equal(FeatureSet.None, WidgetSetup.Offered([]));
        Assert.Equal(FeatureSet.Of(HostFeatures.Mail, HostFeatures.Calendar), WidgetSetup.Offered([HostProviders.Imap, HostProviders.CalDav]));
        Assert.True(WidgetSetup.Offered([HostProviders.Microsoft]).Has(HostFeatures.Tasks));
        Assert.False(WidgetSetup.Offered([HostProviders.CardDav]).Has(HostFeatures.Tasks));
    }

    [Theory]
    [InlineData("email")]
    [InlineData("calendar")]
    [InlineData("nextMeeting")]
    [InlineData("weekPlanner")]
    [InlineData("contacts")]
    [InlineData("tasks")]
    public void S1862_set_up_goes_to_the_accounts_page_when_an_account_is_all_a_widget_lacks(string kindId)
        => Assert.Equal(SetupGoes.Accounts, WidgetSetup.Goes(WidgetConfig.CreateDefault(new WidgetId(kindId)), FeatureSet.None, Everything));

    [Theory]
    [InlineData("countdown")]
    [InlineData("rss")]
    [InlineData("stocks")]
    [InlineData("weatherNow")]
    [InlineData("weatherForecast")]
    [InlineData("airQuality")]
    [InlineData("rainNextHour")]
    [InlineData("videoStream")]
    [InlineData("gallery")]
    public void S1862_set_up_goes_to_the_menu_where_the_missing_piece_is_an_option(string kindId)
        => Assert.Equal(SetupGoes.Menu, WidgetSetup.Goes(WidgetConfig.CreateDefault(new WidgetId(kindId)), Everything, Everything));

    [Fact]
    public void S1862_set_up_goes_to_the_menu_when_more_than_an_account_is_missing()
    {
        // An account would not be enough: the widget lacks an option too, which its menu holds.
        Assert.Equal(SetupGoes.Menu, WidgetSetup.Goes(new QuoteConfig { Quotes = [] }, FeatureSet.None, Everything));
        Assert.Equal(SetupGoes.Menu, WidgetSetup.Goes(new ShortcutsConfig(), FeatureSet.None, Everything));
        Assert.Equal(SetupGoes.Menu, WidgetSetup.Goes(new RssConfig(), FeatureSet.None, Everything));
    }

    public static TheoryData<string> WaitingForAnOption() => ["countdown", "rss", "stocks", "weatherNow", "weatherForecast", "airQuality", "rainNextHour", "videoStream", "gallery"];

    [Theory]
    [MemberData(nameof(WaitingForAnOption))]
    public void S1862_a_widget_waiting_for_an_option_names_the_one_menu_line_set_up_opens(string kindId)
        => Assert.Single(SetupLines(WidgetConfig.CreateDefault(new WidgetId(kindId))));

    [Fact]
    public void S1862_an_emptied_list_names_its_line_too()
    {
        // The quotes' list, Add… in it (Shortcuts' Add: ShortcutsMenuTests).
        Assert.NotNull(Assert.Single(SetupLines(new QuoteConfig { Quotes = [] })).Children);
    }

    [Fact]
    public void S1862_a_picture_widget_names_the_line_of_its_source()
    {
        Assert.Single(SetupLines(new GalleryConfig { Source = GallerySource.Folder }));
        Assert.Single(SetupLines(new GalleryConfig { Source = GallerySource.File }));
    }

    /// <summary>Every line of the widget's menu marked as where Set up opens it, at any depth.</summary>
    private static List<ItemMenuEntry> SetupLines(WidgetConfig config)
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ModuleRegistry.Widgets.Single(m => m.Descriptor.Id == config.Kind).MenuLines(menu, config);

        var marked = new List<ItemMenuEntry>();
        void Walk(IEnumerable<ItemMenuEntry> lines)
        {
            foreach (var line in lines)
            {
                if (line.SetsUp)
                {
                    marked.Add(line);
                }

                Walk(line.Children ?? []);
            }
        }

        Walk(menu.Lines.Concat(menu.StyleLines));
        return marked;
    }

    [Fact]
    public void S1862_a_widget_that_waits_for_nothing_goes_to_its_menu()
        => Assert.Equal(SetupGoes.Menu, WidgetSetup.Goes(WidgetConfig.CreateDefault(new WidgetId("clock")), FeatureSet.None, Everything));
}
