using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1860]..[S-1862] Which widgets show the "Set up" button instead of their body: the ones that
/// cannot show anything until they are configured, and only while that is so.
/// </summary>
public class WidgetSetupTests
{
    private const AccountFeatures Everything = AccountFeatures.Mail | AccountFeatures.Calendar | AccountFeatures.Contacts
        | AccountFeatures.Tasks | AccountFeatures.Files | AccountFeatures.GitHub | AccountFeatures.ClaudeUsage;

    /// <summary>Every kind whose default config needs setting up, with nothing signed in.</summary>
    private static readonly WidgetKind[] NeedSetupByDefault =
    [
        WidgetKind.Countdown, WidgetKind.Rss, WidgetKind.Stocks, WidgetKind.WeatherNow, WidgetKind.WeatherForecast,
        WidgetKind.VideoStream, WidgetKind.Gallery, WidgetKind.GitHub,
        WidgetKind.Email, WidgetKind.Calendar, WidgetKind.Contacts, WidgetKind.Tasks, WidgetKind.OneDrive,
        WidgetKind.GitHubIssues, WidgetKind.GitHubNotifications, WidgetKind.GitHubReviews, WidgetKind.ClaudeUsage,
    ];

    [Fact]
    public void S1860_a_new_widget_asks_to_be_set_up_exactly_when_it_cannot_show_anything_yet()
    {
        foreach (var kind in Enum.GetValues<WidgetKind>())
        {
            var needed = WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), AccountFeatures.None);
            Assert.True(needed == NeedSetupByDefault.Contains(kind), $"{kind}: expected {NeedSetupByDefault.Contains(kind)}");
        }
    }

    [Fact]
    public void S1860_widgets_that_work_out_of_the_box_never_ask()
    {
        // Seeded quotes, colours and shortcuts, a default place, default power actions, the profile folder.
        foreach (var kind in new[] { WidgetKind.Quote, WidgetKind.ColorSwatch, WidgetKind.Shortcuts, WidgetKind.SunMoon, WidgetKind.Power, WidgetKind.Folder, WidgetKind.Clock })
        {
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), AccountFeatures.None), kind.ToString());
        }
    }

    [Fact]
    public void S1860_configuring_the_missing_piece_takes_the_button_away()
    {
        var countdown = new CountdownConfig();
        countdown.Timers.Add(CountdownTimer.Create("Launch", DateTimeOffset.UnixEpoch));
        Assert.False(WidgetSetup.Needed(countdown, AccountFeatures.None));

        Assert.False(WidgetSetup.Needed(new RssConfig { Feeds = [new RssFeed { Url = "https://example.com/feed" }] }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new StocksConfig { Symbol = "MSFT" }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new WeatherNowConfig { Latitude = 47.4, Longitude = 8.5 }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new VideoStreamConfig { Url = "http://cam.local/stream" }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Path = @"C:\Pictures" }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Source = GallerySource.Bing }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new GitHubConfig { Repo = "owner/name" }, AccountFeatures.None));

        var shortcuts = new ShortcutsConfig();
        shortcuts.Items.Add(new ShortcutItem { Id = "a", Target = @"C:\Windows\notepad.exe" });
        Assert.False(WidgetSetup.Needed(shortcuts, AccountFeatures.None));
    }

    [Fact]
    public void S1860_emptying_a_list_the_widget_came_with_asks_again()
    {
        Assert.True(WidgetSetup.Needed(new QuoteConfig { Quotes = [] }, AccountFeatures.None));
        Assert.True(WidgetSetup.Needed(new QuoteConfig { Quotes = [new QuoteItem { Text = "  " }] }, AccountFeatures.None));
        Assert.True(WidgetSetup.Needed(new PowerConfig { Actions = [] }, AccountFeatures.None));
        Assert.True(WidgetSetup.Needed(new ShortcutsConfig(), AccountFeatures.None));

        // A palette with no colours is nothing; a solid swatch still paints its colour.
        Assert.True(WidgetSetup.Needed(new ColorSwatchConfig { Mode = SwatchMode.Palette, Swatches = [] }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new ColorSwatchConfig { Mode = SwatchMode.Solid, Swatches = [] }, AccountFeatures.None));
    }

    [Fact]
    public void S1860_set_up_but_wrong_keeps_the_widgets_own_sentence()
    {
        // These say what is wrong ("not a symbol", "not a repository") rather than asking to start.
        Assert.False(WidgetSetup.Needed(new StocksConfig { Symbol = "not a symbol!" }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new GitHubConfig { Repo = "nonsense" }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new RssConfig { Feeds = [new RssFeed { Url = "not a url" }] }, AccountFeatures.None));
        Assert.False(WidgetSetup.Needed(new GalleryConfig { Path = @"Z:\gone" }, AccountFeatures.None));
    }

    [Theory]
    [InlineData(WidgetKind.Email, AccountFeatures.Mail)]
    [InlineData(WidgetKind.Calendar, AccountFeatures.Calendar)]
    [InlineData(WidgetKind.Contacts, AccountFeatures.Contacts)]
    [InlineData(WidgetKind.Tasks, AccountFeatures.Tasks)]
    [InlineData(WidgetKind.OneDrive, AccountFeatures.Files)]
    [InlineData(WidgetKind.GitHubIssues, AccountFeatures.GitHub)]
    [InlineData(WidgetKind.GitHubNotifications, AccountFeatures.GitHub)]
    [InlineData(WidgetKind.GitHubReviews, AccountFeatures.GitHub)]
    [InlineData(WidgetKind.ClaudeUsage, AccountFeatures.ClaudeUsage)]
    public void S1861_an_account_widget_asks_until_an_account_offering_its_feature_is_signed_in(WidgetKind kind, AccountFeatures feature)
    {
        var config = WidgetConfig.CreateDefault(kind);

        Assert.True(WidgetSetup.Needed(config, Everything & ~feature));
        Assert.False(WidgetSetup.Needed(config, feature));
    }

    [Fact]
    public void S1861_a_local_task_list_needs_no_account()
        => Assert.False(WidgetSetup.Needed(new TasksConfig { Source = TaskSource.Local }, AccountFeatures.None));

    [Fact]
    public void S1861_features_are_the_union_of_the_signed_in_providers()
    {
        Assert.Equal(AccountFeatures.None, WidgetSetup.Offered([]));
        Assert.Equal(AccountFeatures.Mail | AccountFeatures.GitHub, WidgetSetup.Offered([AccountProvider.Imap, AccountProvider.GitHub]));
        Assert.True(WidgetSetup.Offered([AccountProvider.Microsoft]).HasFlag(AccountFeatures.Files));
        Assert.False(WidgetSetup.Offered([AccountProvider.Google]).HasFlag(AccountFeatures.Files));
    }
}
