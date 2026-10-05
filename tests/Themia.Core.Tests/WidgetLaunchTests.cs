using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1920]..[S-1930] What a click on a widget opens, and the gate in front of it.</summary>
public sealed class WidgetLaunchTests
{
    private static readonly WidgetLaunchContext Empty = new();

    private static WidgetLaunchTarget? For(WidgetConfig config, WidgetLaunchContext? context = null)
        => WidgetLaunch.For(config.Kind, config, context ?? Empty);

    private static WidgetLaunchContext WithAccounts(params (string Id, AccountProvider Provider)[] accounts)
        => new() { Accounts = [.. accounts.Select(a => new LaunchAccount(a.Id, a.Provider))] };

    // ---- [S-1920] where a click opens ----------------------------------------------------------

    [Theory]
    [InlineData(WidgetKind.Cpu, LaunchArea.Body)]
    [InlineData(WidgetKind.Music, LaunchArea.Body)]
    [InlineData(WidgetKind.WeatherNow, LaunchArea.Body)]
    [InlineData(WidgetKind.Email, LaunchArea.Header)]
    [InlineData(WidgetKind.Tasks, LaunchArea.Header)]
    [InlineData(WidgetKind.Folder, LaunchArea.Header)]
    [InlineData(WidgetKind.Mixer, LaunchArea.Header)]
    [InlineData(WidgetKind.Notes, LaunchArea.None)]
    [InlineData(WidgetKind.Scratchpad, LaunchArea.None)]
    [InlineData(WidgetKind.Clipboard, LaunchArea.None)]
    [InlineData(WidgetKind.Shortcuts, LaunchArea.None)]
    [InlineData(WidgetKind.Quote, LaunchArea.None)]
    [InlineData(WidgetKind.SunMoon, LaunchArea.None)]
    [InlineData(WidgetKind.HabitTracker, LaunchArea.None)]
    [InlineData(WidgetKind.ScreenTime, LaunchArea.None)]
    [InlineData(WidgetKind.Countdown, LaunchArea.None)]
    [InlineData(WidgetKind.Pomodoro, LaunchArea.None)]
    public void S1920_each_kind_has_one_click_area(WidgetKind kind, LaunchArea area)
        => Assert.Equal(area, WidgetLaunch.AreaOf(kind));

    [Fact]
    public void S1920_a_kind_without_an_area_never_has_a_target()
    {
        foreach (var kind in Enum.GetValues<WidgetKind>().Where(k => WidgetLaunch.AreaOf(k) == LaunchArea.None))
        {
            WidgetConfig config;
            try
            {
                config = WidgetConfig.CreateDefault(kind);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue; // a kind with no config of its own (removed elsewhere) has nothing to open either
            }

            Assert.Null(WidgetLaunch.For(kind, config, Empty));
        }
    }

    // ---- [S-1925] the click ------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, LaunchArea.Body, false, false, true)]
    [InlineData(false, false, LaunchArea.Body, true, false, true)]
    [InlineData(false, false, LaunchArea.Body, false, true, false)]
    [InlineData(true, false, LaunchArea.Body, false, false, false)]
    [InlineData(false, true, LaunchArea.Body, false, false, false)]
    [InlineData(false, false, LaunchArea.Header, true, false, true)]
    [InlineData(false, false, LaunchArea.Header, false, false, false)]
    [InlineData(false, false, LaunchArea.Header, true, true, false)]
    [InlineData(false, false, LaunchArea.None, true, false, false)]
    public void S1925_a_click_opens_only_outside_edit_mode_in_the_area_and_off_the_controls(
        bool editMode, bool preview, LaunchArea area, bool inHeader, bool onControl, bool opens)
        => Assert.Equal(opens, WidgetLaunch.OpensOnClick(editMode, preview, area, inHeader, onControl));

    // ---- [S-1921] system widgets ---------------------------------------------------------------

    [Theory]
    [InlineData(WidgetKind.Cpu)]
    [InlineData(WidgetKind.Memory)]
    [InlineData(WidgetKind.Disk)]
    [InlineData(WidgetKind.Gpu)]
    public void S1921_meters_open_task_manager(WidgetKind kind)
    {
        var target = For(WidgetConfig.CreateDefault(kind));

        Assert.NotNull(target);
        Assert.Equal(WidgetLaunchMethod.Program, target.Method);
        Assert.Equal("Taskmgr.exe", target.Value);
        Assert.Equal("Open Task Manager", WidgetLaunch.Text(target, Locale.En));
    }

    [Theory]
    [InlineData(WidgetKind.Network, "ms-settings:network-status")]
    [InlineData(WidgetKind.Ping, "ms-settings:network-status")]
    [InlineData(WidgetKind.Battery, "ms-settings:powersleep")]
    [InlineData(WidgetKind.Volume, "ms-settings:sound")]
    [InlineData(WidgetKind.Mixer, "ms-settings:apps-volume")]
    [InlineData(WidgetKind.Bluetooth, "ms-settings:bluetooth")]
    public void S1921_system_widgets_open_their_settings_page(WidgetKind kind, string page)
    {
        var target = For(WidgetConfig.CreateDefault(kind));

        Assert.NotNull(target);
        Assert.Equal(WidgetLaunchMethod.Settings, target.Method);
        Assert.Equal(page, target.Value);
    }

    [Fact]
    public void S1921_the_clock_opens_windows_clock()
    {
        var target = For(new ClockConfig());

        Assert.Equal(new WidgetLaunchTarget(WidgetLaunchMethod.Protocol, "ms-clock:", WidgetLaunchTitle.Clock), target);
    }

    // ---- [S-1926] music ----------------------------------------------------------------------

    [Fact]
    public void S1926_music_opens_nothing_while_nothing_plays()
        => Assert.Null(For(new MusicConfig()));

    [Theory]
    [InlineData("Spotify.exe", "Open Spotify")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Open Media Player")]
    public void S1926_music_opens_the_playing_app(string appId, string text)
    {
        var name = text["Open ".Length..];
        var target = For(new MusicConfig(), new WidgetLaunchContext { Live = new WidgetLiveLaunch(MediaAppId: appId, MediaAppName: name) });

        Assert.NotNull(target);
        Assert.Equal(WidgetLaunchMethod.MediaApp, target.Method);
        Assert.Equal(appId, target.Value);
        Assert.Equal(text, WidgetLaunch.Text(target, Locale.En));
    }

    [Fact]
    public void S1926_an_unnamed_app_still_says_what_opens()
    {
        var target = For(new MusicConfig(), new WidgetLaunchContext { Live = new WidgetLiveLaunch(MediaAppId: "Chrome") });

        Assert.Equal("Open the app that is playing", WidgetLaunch.Text(target!, Locale.En));
    }

    [Theory]
    [InlineData("Spotify.exe", MediaAppKind.Desktop)]
    [InlineData("Chrome", MediaAppKind.Desktop)]
    [InlineData("308046B0AF4A39CB", MediaAppKind.Desktop)]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", MediaAppKind.Packaged)]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", MediaAppKind.Packaged)]
    [InlineData("", MediaAppKind.Invalid)]
    [InlineData("C:\\Windows\\System32\\cmd.exe", MediaAppKind.Invalid)]
    [InlineData("cmd.exe /c calc", MediaAppKind.Invalid)]
    [InlineData("..\\..\\evil", MediaAppKind.Invalid)]
    [InlineData("a!b", MediaAppKind.Invalid)]
    [InlineData("Pkg_hash!App!Other", MediaAppKind.Invalid)]
    [InlineData("Pkg_hash!", MediaAppKind.Invalid)]
    [InlineData("shell:AppsFolder", MediaAppKind.Invalid)]
    [InlineData("https://evil.example", MediaAppKind.Invalid)]
    [InlineData("Spotify\0.exe", MediaAppKind.Invalid)]
    public void S1926_only_a_plain_app_id_passes(string appId, MediaAppKind kind)
        => Assert.Equal(kind, MediaApps.KindOf(appId));

    [Fact]
    public void S1926_an_overlong_app_id_is_refused()
        => Assert.Equal(MediaAppKind.Invalid, MediaApps.KindOf(new string('a', MediaApps.MaxLength + 1)));

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("Chrome", "Chrome")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", null)]
    [InlineData("bad id", null)]
    public void S1926_a_desktop_app_is_found_by_its_process_name(string appId, string? process)
        => Assert.Equal(process, MediaApps.ProcessName(appId));

    [Fact]
    public void S1926_a_media_app_is_activated_through_its_start_menu_entry()
    {
        Assert.Equal(@"shell:AppsFolder\Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic",
            MediaApps.AppsFolderTarget("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic"));
        Assert.Null(MediaApps.AppsFolderTarget("calc & whoami"));
    }

    // ---- [S-1927] weather and stocks -----------------------------------------------------------

    [Fact]
    public void S1927_weather_without_a_place_opens_nothing()
        => Assert.Null(For(new WeatherNowConfig()));

    [Fact]
    public void S1927_weather_opens_msn_for_its_own_coordinates_and_unit()
    {
        var config = new WeatherForecastConfig
        {
            Place = "Zurich",
            LocationName = "Zürich, Zurich, Switzerland",
            Latitude = 47.3769,
            Longitude = 8.5417,
            Unit = TemperatureUnit.Celsius,
        };

        var target = WidgetLaunch.For(WidgetKind.WeatherForecast, config, new WidgetLaunchContext { Locale = Locale.De });

        Assert.NotNull(target);
        Assert.Equal(WidgetLaunchMethod.Web, target.Method);
        Assert.StartsWith("https://www.msn.com/de-de/weather/forecast/in-Z%C3%BCrich?loc=", target.Value, StringComparison.Ordinal);
        Assert.EndsWith("&weadegreetype=C", target.Value, StringComparison.Ordinal);

        var loc = Uri.UnescapeDataString(target.Value.Split("loc=")[1].Split('&')[0]);
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(loc));
        Assert.Contains("\"x\":\"8.5417\"", json, StringComparison.Ordinal);
        Assert.Contains("\"y\":\"47.3769\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S1927_a_place_name_cannot_break_out_of_its_path_segment()
    {
        var config = new WeatherNowConfig
        {
            LocationName = "../../evil?x=1#frag, Somewhere",
            Latitude = 1,
            Longitude = 2,
            Unit = TemperatureUnit.Fahrenheit,
        };

        var target = For(config);

        Assert.NotNull(target);
        var uri = new Uri(target.Value);
        Assert.Equal("www.msn.com", uri.Host);
        Assert.Equal("/en-us/weather/forecast/in-evil-x-1-frag", uri.AbsolutePath);
        Assert.EndsWith("&weadegreetype=F", target.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void S1927_a_listed_symbol_opens_its_yahoo_finance_page()
    {
        var target = For(new StocksConfig { Symbol = "msft" });

        Assert.Equal("https://finance.yahoo.com/quote/MSFT/", target?.Value);
    }

    [Fact]
    public void S1927_a_coin_opens_its_coingecko_page()
    {
        var target = For(new StocksConfig { Symbol = "Bitcoin", Market = QuoteMarket.Crypto });

        Assert.Equal("https://www.coingecko.com/en/coins/bitcoin", target?.Value);
    }

    [Fact]
    public void S1927_stocks_without_a_symbol_open_nothing()
        => Assert.Null(For(new StocksConfig()));

    // ---- web pages from the widget's own data -------------------------------------------------

    [Fact]
    public void S1924_the_stream_opens_its_https_page_only()
    {
        Assert.Equal("https://cam.example/live", For(new VideoStreamConfig { Url = "https://cam.example/live" })?.Value);
        Assert.Null(For(new VideoStreamConfig { Url = "http://192.168.1.20/video.mjpg" }));
        Assert.Null(For(new VideoStreamConfig { Url = "rtsp://cam.example/live" }));
    }

    [Fact]
    public void S1930_the_feed_widget_opens_its_feeds_site_over_https_only()
    {
        var config = new RssConfig { Feeds = [new RssFeed { Url = "https://news.example/feed.xml" }] };

        Assert.Null(For(config));
        Assert.Equal("https://news.example/", For(config, new WidgetLaunchContext { Live = new WidgetLiveLaunch(Site: "https://news.example/") })?.Value);
        Assert.Null(For(config, new WidgetLaunchContext { Live = new WidgetLiveLaunch(Site: "http://news.example/") }));
        Assert.Null(For(config, new WidgetLaunchContext { Live = new WidgetLiveLaunch(Site: "javascript:alert(1)") }));
        Assert.Null(For(config, new WidgetLaunchContext { Live = new WidgetLiveLaunch(Site: "https://user:pw@evil.example/") }));
    }

    [Fact]
    public void S1930_a_feed_names_its_site()
    {
        const string rss = """
            <rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom"><channel>
              <title>News</title>
              <atom:link href="https://news.example/feed.xml" rel="self" type="application/rss+xml"/>
              <link>https://news.example/</link>
              <item><title>One</title><link>https://news.example/1</link></item>
            </channel></rss>
            """;
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Blog</title>
              <link rel="self" href="https://blog.example/atom.xml"/>
              <link rel="alternate" href="https://blog.example/"/>
              <entry><title>Post</title><link href="https://blog.example/p"/></entry>
            </feed>
            """;
        const string selfOnly = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Blog</title>
              <link rel="self" href="https://blog.example/atom.xml"/>
              <entry><title>Post</title><link href="https://blog.example/p"/></entry>
            </feed>
            """;
        const string hostile = """
            <rss version="2.0"><channel><title>X</title><link>javascript:alert(1)</link>
              <item><title>One</title></item></channel></rss>
            """;

        Assert.Equal("https://news.example/", FeedParser.Parse(rss, 10).Document.Site);
        Assert.Equal("https://blog.example/", FeedParser.Parse(atom, 10).Document.Site);
        Assert.Equal(string.Empty, FeedParser.Parse(selfOnly, 10).Document.Site);
        Assert.Equal(string.Empty, FeedParser.Parse(hostile, 10).Document.Site);
    }

    // ---- [S-1928] account widgets ----------------------------------------------------------------

    [Fact]
    public void S1928_account_widgets_without_an_account_open_nothing()
    {
        Assert.Null(For(new EmailConfig()));
        Assert.Null(For(new CalendarConfig()));
        Assert.Null(For(new ContactsConfig()));
        Assert.Null(For(new TasksConfig()));
    }

    [Fact]
    public void S1928_mail_opens_the_web_inbox_of_its_provider()
    {
        Assert.Equal("https://outlook.office.com/mail/", For(new EmailConfig(), WithAccounts(("m", AccountProvider.Microsoft)))?.Value);
        Assert.Equal("https://mail.google.com/", For(new EmailConfig(), WithAccounts(("g", AccountProvider.Google)))?.Value);
        Assert.Null(For(new EmailConfig(), WithAccounts(("i", AccountProvider.Imap))));
    }

    [Fact]
    public void S1928_the_widgets_own_accounts_decide_not_every_account()
    {
        var context = WithAccounts(("m", AccountProvider.Microsoft), ("g", AccountProvider.Google));

        Assert.Equal("https://mail.google.com/", For(new EmailConfig { AccountIds = ["g"] }, context)?.Value);
        Assert.Equal("https://calendar.google.com/", For(new CalendarConfig { AccountIds = ["g"] }, context)?.Value);
        Assert.Equal("https://outlook.office.com/people/", For(new ContactsConfig(), context)?.Value);
    }

    [Fact]
    public void S1928_caldav_alone_opens_the_calendar_app_when_there_is_one()
    {
        var context = WithAccounts(("d", AccountProvider.CalDav));

        Assert.Null(For(new CalendarConfig(), context));
        Assert.Equal(
            new WidgetLaunchTarget(WidgetLaunchMethod.Protocol, "outlookcal:", WidgetLaunchTitle.Calendar),
            For(new CalendarConfig(), context with { HasProtocol = scheme => scheme == "outlookcal" }));
    }

    [Fact]
    public void S1928_tasks_open_to_do_or_google_tasks_and_a_local_list_nothing()
    {
        var microsoft = WithAccounts(("m", AccountProvider.Microsoft));

        Assert.Equal("https://to-do.office.com/tasks/", For(new TasksConfig(), microsoft)?.Value);
        Assert.Equal("ms-todo:", For(new TasksConfig(), microsoft with { HasProtocol = scheme => scheme == "ms-todo" })?.Value);
        Assert.Equal("https://tasks.google.com/", For(new TasksConfig { AccountId = "g" }, WithAccounts(("m", AccountProvider.Microsoft), ("g", AccountProvider.Google)))?.Value);
        Assert.Null(For(new TasksConfig { Source = TaskSource.Local }, microsoft));
    }

    // ---- [S-1929] folders ------------------------------------------------------------------------

    [Fact]
    public void S1929_the_folder_widget_opens_the_folder_it_shows()
    {
        var config = new FolderConfig { Path = @"C:\Users\Public" };

        Assert.Equal(@"C:\Users\Public", For(config)?.Value);
        Assert.Equal(@"C:\Users\Public\Music", For(config, new WidgetLaunchContext { Live = new WidgetLiveLaunch(Folder: @"C:\Users\Public\Music") })?.Value);
        Assert.Null(For(new FolderConfig { Path = @"relative\path" }));
        Assert.Null(For(new FolderConfig { Path = "https://evil.example/" }));
    }

    [Fact]
    public void S1929_a_gallery_opens_its_folder_and_nothing_for_bing()
    {
        Assert.Equal(@"C:\Pictures", For(new GalleryConfig { Source = GallerySource.Folder, Path = @"C:\Pictures" })?.Value);
        Assert.Null(For(new GalleryConfig { Source = GallerySource.Bing }));
    }

    // ---- [S-1924] the gate ---------------------------------------------------------------------

    [Theory]
    [InlineData(WidgetLaunchMethod.Program, "cmd.exe")]
    [InlineData(WidgetLaunchMethod.Program, @"C:\Windows\System32\Taskmgr.exe")]
    [InlineData(WidgetLaunchMethod.Settings, "ms-settings:anything")]
    [InlineData(WidgetLaunchMethod.Settings, "https://ms-settings.example")]
    [InlineData(WidgetLaunchMethod.Protocol, "ms-clock:?x=1")]
    [InlineData(WidgetLaunchMethod.Protocol, "search-ms:query=x")]
    [InlineData(WidgetLaunchMethod.Web, "http://plain.example/")]
    [InlineData(WidgetLaunchMethod.Web, "file:///C:/Windows/System32/calc.exe")]
    [InlineData(WidgetLaunchMethod.Web, "https://a.example/\nx")]
    [InlineData(WidgetLaunchMethod.Folder, "relative")]
    [InlineData(WidgetLaunchMethod.Folder, "https://a.example/")]
    [InlineData(WidgetLaunchMethod.MediaApp, "cmd.exe /c calc")]
    [InlineData(WidgetLaunchMethod.MediaApp, "")]
    public void S1924_the_gate_refuses_anything_not_built_here(WidgetLaunchMethod method, string value)
    {
        var target = new WidgetLaunchTarget(method, value, WidgetLaunchTitle.Website);
        var launcher = new FakeLauncher();

        Assert.False(WidgetLaunch.IsLaunchable(target));
        Assert.Equal(WidgetLaunchResult.Refused, WidgetLaunch.Open(target, launcher));
        Assert.Empty(launcher.Launched);
    }

    [Fact]
    public void S1924_open_hands_a_valid_target_to_the_launcher_once()
    {
        var launcher = new FakeLauncher();
        var target = For(new ClockConfig())!;

        Assert.Equal(WidgetLaunchResult.Opened, WidgetLaunch.Open(target, launcher));
        Assert.Equal([target], launcher.Launched);
        Assert.Equal(WidgetLaunchResult.Refused, WidgetLaunch.Open(null, launcher));
        Assert.Single(launcher.Launched);
    }

    [Fact]
    public void S1922_every_title_has_text_in_every_language()
    {
        foreach (var locale in LanguageResolver.All)
        {
            foreach (var title in Enum.GetValues<WidgetLaunchTitle>())
            {
                var text = WidgetLaunch.Text(new WidgetLaunchTarget(WidgetLaunchMethod.Web, "https://a.example/", title, "App"), locale);
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.DoesNotContain("shell.launch", text, StringComparison.Ordinal);
            }
        }
    }

    private sealed class FakeLauncher : IWidgetLauncher
    {
        public List<WidgetLaunchTarget> Launched { get; } = [];

        public WidgetLaunchResult Launch(WidgetLaunchTarget target)
        {
            Launched.Add(target);
            return WidgetLaunchResult.Opened;
        }
    }
}
