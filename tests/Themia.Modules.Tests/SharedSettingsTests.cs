using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11750]..[S-11757] The settings several kinds share, over every module of the build: who holds each,
/// each read and copied on a real config of every kind that holds it, each marked on its line, and the
/// selection page's model of a group.
/// </summary>
public sealed class SharedSettingsTests
{
    /// <summary>[S-11752] Which kinds carry each shared setting: the table the SPEC states, pinned.</summary>
    private static readonly Dictionary<string, string[]> Carriers = new(StringComparer.Ordinal)
    {
        ["place"] = ["airFacts", "airQuality", "airQualityGraph", "header", "humidityGraph", "moon", "moonTimes", "pressureGraph", "rainGraph", "rainNextHour", "sun", "sunTimes", "temperatureGraph", "uvGraph", "weatherCloudCover", "weatherFeelsLike", "weatherForecast", "weatherGusts", "weatherHighLow", "weatherHumidity", "weatherNow", "weatherPrecipitation", "weatherPressure", "weatherSunrise", "weatherSunset", "weatherUv", "weatherWind", "windGraph"],
        ["calendarAccounts"] = ["calendar", "nextMeeting", "upNext", "weekPlanner"],
        ["mailAccounts"] = ["email"],
        ["contactAccounts"] = ["contacts"],
        ["diskScope"] = ["disk", "diskHistory"],
        ["drive"] = ["drive", "driveFacts"],
        ["pingHost"] = ["ping", "pingFacts", "pingHistory"],
        ["gpuAdapter"] = ["gpu", "gpuEngines", "gpuFacts", "gpuHistory"],
        ["audioOutput"] = ["mixer", "volume"],
        ["exchangePair"] = ["exchangeHistory", "exchangeRate"],
        ["stockSymbol"] = ["stock", "stockHistory"],
        ["stockTimeframe"] = ["stock", "stockHistory"],
        ["screenTimePeriod"] = ["screenTime", "screenTimeApps", "screenTimeChart", "screenTimeDonut"],
        ["historySpan"] = ["batteryHistory", "cpuCores", "cpuHistory", "diskHistory", "gpuHistory", "memoryHistory", "networkHistory", "pingHistory"],
        ["hideWhenIdle"] = ["music", "musicCover", "musicProgress"],
        ["chartType"] = ["airQualityGraph", "batteryHistory", "cpuHistory", "diskHistory", "exchangeHistory", "gpuHistory", "humidityGraph", "memoryHistory", "networkHistory", "pingHistory", "pressureGraph", "rainGraph", "stockHistory", "temperatureGraph", "uvGraph", "windGraph"],
        ["chartDetail"] = ["airQualityGraph", "batteryHistory", "cpuHistory", "diskHistory", "exchangeHistory", "gpuHistory", "humidityGraph", "memoryHistory", "networkHistory", "pingHistory", "pressureGraph", "rainGraph", "stockHistory", "temperatureGraph", "uvGraph", "windGraph"],
    };

    /// <summary>A value of each setting no kind starts with: written on one config, it differs from every default.</summary>
    private static readonly Dictionary<string, Action<WidgetConfig>> Other = new(StringComparer.Ordinal)
    {
        ["place"] = c =>
        {
            var placed = (IPlaced)c;
            (placed.Latitude, placed.Longitude, placed.PlaceName, placed.UseDeviceLocation) = (-33.87, 151.21, "Sydney", false);
        },
        ["calendarAccounts"] = c => ((CalendarConfig)c).AccountIds = ["account-1"],
        ["mailAccounts"] = c => ((IChoosesAccounts)c).ChosenAccounts = ["account-1"],
        ["contactAccounts"] = c => ((IChoosesAccounts)c).ChosenAccounts = ["account-1"],
        ["diskScope"] = c => ((IDiskScope)c).Scope = DiskScope.AllDrives,
        ["drive"] = c => ((IDriveChoice)c).Drive = "Q:\\",
        ["pingHost"] = c => ((IPingHost)c).Host = "example.org",
        ["gpuAdapter"] = c => ((IGpuAdapter)c).Adapter = 3,
        ["audioOutput"] = c => (((IAudioOutput)c).DeviceId, ((IAudioOutput)c).DeviceName) = ("device-1", "Headphones"),
        ["exchangePair"] = c => (((ExchangePairConfig)c).BaseCurrency, ((ExchangePairConfig)c).QuoteCurrency) = ("JPY", "GBP"),
        ["stockSymbol"] = c => (((StockQuoteConfig)c).Symbol, ((StockQuoteConfig)c).DisplayName) = ("MSFT", "Microsoft"),
        ["stockTimeframe"] = c => (((StockQuoteConfig)c).Range, ((StockQuoteConfig)c).ShowExtendedHours) = (QuoteRange.FiveYears, true),
        ["screenTimePeriod"] = c => ((IScreenTimePeriod)c).Period = ScreenTimePeriod.Month,
        ["historySpan"] = c => ((ISpansHistory)c).GraphSpanSeconds = 1800,
        ["listRows"] = c => ((IKitRows)c).Rows = 17,
        ["listLines"] = c => ((IKitRows)c).Lines = 3,
        ["listColumns"] = c => ((IKitColumns)c).Columns = 2,
        ["hideWhenIdle"] = c => ((IHidesWhenIdle)c).HideWhenIdle = MusicHideWhenIdle.NotPlaying,
        ["chartDetail"] = c => ((IChartLooks)c).Detail = ChartDetail.Detailed,
    };

    /// <summary>
    /// [S-6752] A chart type set apart from <paramref name="to"/>'s: one both kinds offer (a history draws no candles),
    /// which the kinds' own types (Area, Line, Bars) do not let one value be for every pair.
    /// </summary>
    private static void OtherChart(WidgetConfig from, WidgetConfig to)
    {
        var (a, b) = ((IChartLooks)from, (IChartLooks)to);
        a.Chart = a.ChartTypes.Intersect(b.ChartTypes).First(t => t != ChartLooks.Effective(b));
    }

    private static IEnumerable<(string Kind, WidgetConfig Config)> Defaults()
        => ModuleRegistry.Widgets.Select(m => (m.Descriptor.Id.Value, m.Descriptor.CreateDefault()));

    private static string[] HeldBy(SharedSetting setting)
        => [.. Defaults().Where(d => setting.Holds(d.Config)).Select(d => d.Kind).Order(StringComparer.Ordinal)];

    [Fact]
    public void S11752_each_shared_setting_is_carried_by_the_kinds_the_spec_names()
    {
        foreach (var setting in SharedSettings.All)
        {
            var kinds = HeldBy(setting);
            Assert.True(kinds.Length > 0, $"no kind of the build holds {setting}");
            if (Carriers.TryGetValue(setting.Key, out var expected))
            {
                Assert.Equal(expected, kinds);
            }
        }

        // The settings every list has are as many as those kinds are.
        Assert.True(HeldBy(SharedSettings.ListRows).Length >= 10);
        Assert.Equal(SharedSettings.All.Count, SharedSettings.All.Select(s => s.Key).Distinct().Count());
        Assert.All(SharedSettings.All, s => Assert.Same(s, SharedSettings.Find(s.Key)));
    }

    [Fact]
    public void S11752_each_shared_setting_reads_and_writes_on_a_real_config_of_every_kind_that_holds_it()
    {
        foreach (var setting in SharedSettings.All)
        {
            var holders = Defaults().Where(d => setting.Holds(d.Config)).ToList();
            foreach (var (kind, _) in holders)
            {
                foreach (var (otherKind, _) in holders)
                {
                    var from = ModuleCatalog.Of(new WidgetId(kind)).CreateDefault();
                    var to = ModuleCatalog.Of(new WidgetId(otherKind)).CreateDefault();
                    if (setting == SharedSettings.ChartType)
                    {
                        OtherChart(from, to);
                    }
                    else
                    {
                        Other[setting.Key](from);
                    }

                    Assert.True(SharedSettings.Read(setting, [from, to]) is { Holders: 2, Mixed: true }, $"{setting}: {kind} set apart still reads as {otherKind}'s");
                    Assert.True(setting.Copy(from, to), $"{setting}: {kind} onto {otherKind} changed nothing");
                    Assert.True(SharedSettings.Read(setting, [from, to]) is { Holders: 2, Mixed: false }, $"{setting}: {kind} onto {otherKind} still differs");
                    Assert.False(setting.Copy(from, to), $"{setting}: copying what {otherKind} holds again changed it");
                }
            }
        }
    }

    [Fact]
    public void S11751_a_setting_spread_from_one_value_reaches_every_holder_and_no_one_else()
    {
        var now = ModuleCatalog.Of(new WidgetId("weatherNow")).CreateDefault();
        var sun = ModuleCatalog.Of(new WidgetId("sun")).CreateDefault();
        var clock = ModuleCatalog.Of(new WidgetId("clock")).CreateDefault();
        WidgetConfig[] configs = [now, sun, clock];
        Assert.Equal([SharedSettings.Place], SharedSettings.Among(configs).Where(s => s == SharedSettings.Place));
        Assert.True(SharedSettings.Read(SharedSettings.Place, configs).Mixed); // the weather starts with no place, the sun at Zurich

        var scratch = now.Clone();
        Other["place"](scratch);
        Assert.Equal(2, SharedSettings.Spread(SharedSettings.Place, scratch, configs));
        Assert.False(SharedSettings.Read(SharedSettings.Place, configs).Mixed);
        Assert.Equal("Sydney", ((SkyPlaceConfig)sun).Place);
        Assert.Equal("Sydney", ((WeatherConfig)now).LocationName);
        Assert.Equal(0, SharedSettings.Spread(SharedSettings.Place, scratch, configs));
    }

    [Fact]
    public void S11753_every_kind_that_holds_a_shared_setting_marks_the_line_that_sets_it()
    {
        // The kinds whose lines read a list only the running host installs: their helpers are checked below.
        string[] hosted = ["brightness", "desktopItem", "disk", "diskHistory", "drive", "driveFacts", "gpu", "gpuEngines", "gpuFacts", "gpuHistory"];

        // A line offered only when it changes something (one output device), or only in a look (the calendar's
        // rows in its agenda, the cores' span in their graphs): the default look of these offers none.
        (string Kind, string Key)[] conditional =
        [
            ("mixer", "audioOutput"), ("volume", "audioOutput"), ("calendar", "listRows"), ("calendar", "listLines"), ("cpuCores", "historySpan"),
        ];

        var missing = new List<string>();
        foreach (var module in ModuleRegistry.Widgets)
        {
            var kind = module.Descriptor.Id.Value;
            if (hosted.Contains(kind))
            {
                continue;
            }

            var config = module.Descriptor.CreateDefault();
            var menu = new WidgetMenuLines(() => { }, _ => { }) { OpenSetting = _ => { } };
            module.MenuLines(menu, config);
            var marked = menu.StyleLines.Concat(menu.Lines).Select(l => l.Shares).OfType<string>().ToHashSet(StringComparer.Ordinal);
            foreach (var setting in SharedSettings.All.Where(s => s.Holds(config)))
            {
                if (!marked.Contains(setting.Key) && !conditional.Contains((kind, setting.Key)))
                {
                    missing.Add($"{kind}: {setting}");
                }
            }

            // A mark names a setting the config holds: never a line of something else.
            Assert.All(marked, key => Assert.True(SharedSettings.Find(key) is { } s && s.Holds(config), $"{kind} marks a line '{key}' its config does not hold"));
        }

        Assert.True(missing.Count == 0, "a shared setting's line is not marked: " + string.Join("; ", missing));

        var menuOf = new WidgetMenuLines(() => { }, _ => { });
        Assert.Equal(SharedSettings.DiskScope.Key, DiskMenuLines.Drive(menuOf, new DiskConfig(), []).Shares);
        Assert.Equal(SharedSettings.Drive.Key, DriveMenuLines.Drive(menuOf, string.Empty, _ => { }, []).Shares);
        Assert.Equal(SharedSettings.GpuAdapter.Key, GpuAdapterLine.Line(menuOf, 0, _ => { }, [new GpuAdapter(0, "One", "1", 0, 0), new GpuAdapter(1, "Two", "2", 0, 0)])?.Shares);
    }

    private static WidgetInstance Widget(string kind, string id)
    {
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        return widget;
    }

    /// <summary>A weather station placed: four loose widgets ([S-2475]) and a clock.</summary>
    private static (AppState State, ViewState View) Station()
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange([Widget("weatherNow", "now"), Widget("weatherHumidity", "facts"), Widget("weatherForecast", "days"), Widget("rainNextHour", "rain"), Widget("clock", "clock")]);
        return (new AppState { Views = [view] }, view);
    }

    private static WidgetSelection Selecting(ViewState view, params string[] ids)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ids);
        return selection;
    }

    [Fact]
    public void S11755_several_widgets_selected_are_their_shared_settings_and_their_look_never_one_alone()
    {
        var (state, view) = Station();
        var model = SelectionPage.Of(state, Selecting(view, "now", "facts", "days", "rain"), view.Id)!;

        Assert.Equal(SelectionKind.Several, model.Kind);
        Assert.Equal([SelectionSection.Shared, SelectionSection.Appearance], SelectionPage.Sections(model, ownLines: true, styleLines: true));

        var place = Assert.Single(model.Shared, r => r.Setting == SharedSettings.Place);
        Assert.Equal(["now", "facts", "days", "rain"], place.Holders);
        Assert.False(place.Mixed);
        Assert.Equal("4 widgets", SelectionPage.Words(model, k => ModuleCatalog.Of(k).Name));
        Assert.Equal(Themia.App.Shell.Glyphs.Apps, SelectionPage.Glyph(model));
    }

    [Fact]
    public void S11751_a_place_the_members_disagree_on_is_mixed_and_one_write_makes_them_agree()
    {
        var (state, view) = Station();
        Other["place"](view.Find("facts")!.Config);
        var ids = new[] { "now", "facts", "days", "rain" };

        Assert.True(SelectionPage.Of(state, Selecting(view, ids), view.Id)!.Shared.Single(r => r.Setting == SharedSettings.Place).Mixed);

        // The group's line writes a scratch copy; one write sets it on every member that holds it.
        var scratch = view.Find("now")!.Config.Clone();
        Other["place"](scratch);
        Assert.Equal(3, QuickSet.SetShared(view, ids, SharedSettings.Place, scratch));
        Assert.False(SelectionPage.Of(state, Selecting(view, ids), view.Id)!.Shared.Single(r => r.Setting == SharedSettings.Place).Mixed);
        Assert.All(ids, id => Assert.Equal(-33.87, ((IPlaced)view.Find(id)!.Config).Latitude));
        Assert.False(SharedSettings.Place.Holds(view.Find("clock")!.Config)); // the clock holds no place: nothing to reach
    }

    [Fact]
    public void S11751_a_shared_write_is_one_change_of_the_state_and_takes_no_undo_step_of_its_own()
    {
        // [S-886] A config is the widget's content, never the arrangement: a shared write, like any line of a widget, is one mutation and no step.
        var (state, view) = Station();
        var history = new LayoutHistory(state);
        var scratch = view.Find("now")!.Config.Clone();
        Other["place"](scratch);

        QuickSet.SetShared(view, ["now", "facts", "days", "rain"], SharedSettings.Place, scratch);

        Assert.False(history.Commit(state, UndoAction.Change));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void S11754_several_widgets_share_the_section_and_one_widget_is_its_own_page()
    {
        var (state, view) = Station();
        var a = Widget("sun", "sun");
        var b = Widget("moonTimes", "moon");
        view.Widgets.AddRange([a, b]);

        var several = SelectionPage.Of(state, Selecting(view, "sun", "moon"), view.Id)!;
        Assert.Equal(SelectionKind.Several, several.Kind);
        Assert.Contains(several.Shared, r => r.Setting == SharedSettings.Place && !r.Mixed);
        Assert.Equal(SelectionKind.Several, SelectionPage.Of(state, Selecting(view, "now", "facts", "days", "rain", "clock"), view.Id)!.Kind);

        var one = SelectionPage.Of(state, Selecting(view, "clock"), view.Id)!;
        Assert.Equal(SelectionKind.One, one.Kind);
        Assert.Empty(one.Shared);
    }
}
