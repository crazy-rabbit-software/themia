using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;
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
        new WidgetId("countdown"), RssConfig.Descriptor.Id, new WidgetId("stocks"), new WidgetId("weatherNow"), new WidgetId("weatherForecast"), new WidgetId("airQuality"), new WidgetId("rainNextHour"),
        new WidgetId("videoStream"), new WidgetId("gallery"),
        EmailConfig.Descriptor.Id, CalendarWidgetConfig.Descriptor.Id, NextMeetingConfig.Descriptor.Id,
        WeekPlannerConfig.Descriptor.Id, ContactsConfig.Descriptor.Id, TasksConfig.Descriptor.Id,
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
        foreach (var kind in new[] { new WidgetId("quote"), ShortcutsConfig.Descriptor.Id, new WidgetId("sun"), new WidgetId("moon"), new WidgetId("shutdown"), FolderConfig.Descriptor.Id, new WidgetId("clock") })
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
}
