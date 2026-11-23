using System.Text.Json;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>[S-2470] Where a widget of a ready-made group stands, in DIPs from the group's top-left corner.</summary>
public readonly record struct ReadyMadeBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-2470] One widget of a ready-made group: its kind (a widget id, or [S-16682] a kind's preset as <c>kind/preset</c>, as
/// the first-run seed names them),
/// its box, and what differs from its kind's defaults: a config fragment in the state file's own shape
/// (merged over the default, property by property) and a name.
/// </summary>
public sealed record ReadyMadeWidget(string Kind, ReadyMadeBox Box, string? Config = null)
{
    /// <summary>[S-16682] What it places: its kind, or the kind's preset; null for a name this build does not have.</summary>
    public WidgetPick? Pick => WidgetPick.TryParse(Kind, out var pick) ? pick : null;

    /// <summary>
    /// [S-12485] The group's Header widget ([S-9600]): its title, written in the language the group is
    /// placed in (the person's to change after), and its glyph. Null for every other widget.
    /// </summary>
    public (Str Title, string Glyph)? Heading { get; init; }

    /// <summary>
    /// The config fragment placing this widget merges over its kind's default: <see cref="Config"/>, or for
    /// the Header widget its title and glyph now ([S-12485]).
    /// </summary>
    public string? Fragment => Heading is var (title, glyph)
        ? $$"""{ "text": "{{JsonEncodedText.Encode(title.Text()).Value}}", "glyph": "{{JsonEncodedText.Encode(glyph).Value}}" }"""
        : Config;
}

/// <summary>
/// [S-2470] A ready-made group: a recipe that places several widgets, touching (so they draw as one
/// panel, [S-2240]); once placed they are loose widgets like any others ([S-2475]). Pure data; its words go through
/// <see cref="Str"/>. <paramref name="Home"/> is the subject of the add menu it is about: a search result
/// shows it as the recipe's place ([S-2473]).
/// </summary>
public sealed record ReadyMadeGroup(string Id, Str Name, Str About, string Glyph, AddFolderId Home, IReadOnlyList<ReadyMadeWidget> Widgets)
{
    /// <summary>
    /// [S-11703] Its place among the starters the add page's Start tab shows first (1 first), or null
    /// when it is not one: a person's first desktop is a few of these.
    /// </summary>
    public int? Starter { get; init; }

    /// <summary>The box round every widget, in DIPs from the group's corner.</summary>
    public ReadyMadeBox Bounds
    {
        get
        {
            var left = Widgets.Min(w => w.Box.X);
            var top = Widgets.Min(w => w.Box.Y);
            return new ReadyMadeBox(left, top, Widgets.Max(w => w.Box.Right) - left, Widgets.Max(w => w.Box.Bottom) - top);
        }
    }
}

/// <summary>
/// [S-2470]..[S-2475] The ready-made groups the add menu offers above the widgets, so a first desktop
/// is a few clicks. Adding one is adding an entry to <see cref="All"/> (and its two strings to the
/// <c>groups</c> locale area): nothing else names a recipe. <c>ReadyMadeGroupsTests</c> hold every entry
/// to the rules: each kind installed and its config read, no two boxes overlapping, every widget touching
/// another, and the whole fitting a 1280 × 720 work area.
/// </summary>
public static class ReadyMadeGroups
{
    private const string Scope = "ready-made";

    /// <summary>
    /// [S-2474] [S-11090] [S-14713] This PC: the four readings in one column of one width, each over its graph, the metric
    /// it is with its graph shown (owner, 2026-11-15: "the this pc group should be one column not multiple"); the width is
    /// network's, whose two readings stand on one line ([S-6264]).
    /// </summary>
    public static ReadyMadeGroup ThisPc { get; } = Headed(new("thisPc", Str.Groups_Recipe_ThisPc, Str.Groups_Recipe_ThisPcAbout, Glyphs.Display, AddFolderId.System,
    [
        new("cpu", new(0, 0, 344, 60)),
        new("cpuLoad", new(0, 60, 344, 64), Config: Graph),
        new("memory", new(0, 124, 344, 60)),
        new("memoryInUse", new(0, 184, 344, 64), Config: Graph),
        // The drive's activity's own span is five minutes; in this group the four graphs span one minute alike, so the
        // group's page reads one Chart span, not "Mixed" ([S-11751]).
        new("disk", new(0, 248, 344, 60)),
        new("driveActivity", new(0, 308, 344, 64), Config: """{ "trend": "Line", "span": 60 }"""),
        new("network", new(0, 372, 344, 60)),
        new("networkDownload", new(0, 432, 344, 64), Config: Graph),
    ]) { Starter = 2 });

    /// <summary>[S-2474] My day: the clock on top, the date joined under it ([S-12805]), the next meeting and the tasks under them.</summary>
    public static ReadyMadeGroup MyDay { get; } = Headed(new("myDay", Str.Groups_Recipe_MyDay, Str.Groups_Recipe_MyDayAbout, Glyphs.Calendar, AddFolderId.CalendarMail,
    [
        // [S-17100] The clock as tall as its face at the column's width (a taller one stood room over and under it).
        new("clock", new(0, 0, 320, 84)),
        new("date", new(0, 84, 320, 52)),
        new("meeting", new(0, 136, 320, 60)),
        new("meetings", new(0, 196, 320, 120), Config: AfterNext),
        new("tasks", new(0, 316, 320, 210)),
    ]) { Starter = 1 });

    /// <summary>
    /// [S-2474] [S-11172] [S-13900] Weather station: the reading, today's high and low, the wind, the days ahead and the rain
    /// in the next hour, stacked as one panel.
    /// </summary>
    public static ReadyMadeGroup WeatherStation { get; } = Headed(new("weatherStation", Str.Groups_Recipe_WeatherStation, Str.Groups_Recipe_WeatherStationAbout, Glyphs.Weather, AddFolderId.Weather,
    [
        new("weatherNow", new(0, 0, 300, 76)),
        .. Metrics(76, 300, "weatherHighLow", "weatherWind"),
        new("weatherForecast", new(0, 76 + (2 * Row), 300, 150)),
        new("rainNextHour", new(0, 226 + (2 * Row), 300, 148)),
    ]) { Starter = 3 }, Str.Groups_Recipe_Weather);

    /// <summary>
    /// [S-11209] My files: the Documents folder as a list on the left; beside it the files opened last,
    /// six rows. The two are as tall as one another, so they join into one panel.
    /// </summary>
    public static ReadyMadeGroup MyFiles { get; } = Headed(new("myFiles", Str.Groups_Recipe_MyFiles, Str.Groups_Recipe_MyFilesAbout, Glyphs.Folder, AddFolderId.Files,
    [
        new("folder", new(0, 0, 300, 280), Config: """{ "style": "list", "path": "%USERPROFILE%\\Documents" }"""),
        new("recentFiles", new(300, 0, 316, 280), Config: """{ "rows": 7 }"""),
    ]) { Starter = 5 });

    // ---- [S-11090] The system readings' groups: each old card's look, made of its atoms ----------------

    /// <summary>[S-11090] [S-14713] CPU: the reading over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Cpu { get; } = Headed(new("cpu", Str.Groups_Recipe_Cpu, Str.Groups_Recipe_CpuAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 200, 60)),
        new("cpuLoad", new(0, 60, 200, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] CPU cores: the reading over a bar per core.</summary>
    public static ReadyMadeGroup CpuCores { get; } = Headed(new("cpuCores", Str.Groups_Recipe_CpuCores, Str.Groups_Recipe_CpuCoresAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 160, 60)),
        new("cpuCores", new(0, 60, 160, 64)),
    ]), Str.Groups_Recipe_Cpu);

    /// <summary>[S-11090] [S-14607] CPU details: the reading over the processor's facts, a single metric each (the old card's four).</summary>
    public static ReadyMadeGroup CpuDetails { get; } = Headed(new("cpuDetails", Str.Groups_Recipe_CpuDetails, Str.Groups_Recipe_CpuDetailsAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 240, 60)),
        .. Metrics(60, 240, "cpuSpeed", "cpuBaseSpeed", "cpuCoreCount", "cpuLogical"),
    ]), Str.Groups_Recipe_Cpu);

    /// <summary>[S-11090] [S-14713] Memory: the reading over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Memory { get; } = Headed(new("memory", Str.Groups_Recipe_Memory, Str.Groups_Recipe_MemoryAbout, Glyphs.Memory, AddFolderId.System,
    [
        new("memory", new(0, 0, 200, 60)),
        new("memoryInUse", new(0, 60, 200, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] [S-14607] Memory details: the reading over its facts, a single metric each.</summary>
    public static ReadyMadeGroup MemoryDetails { get; } = Headed(new("memoryDetails", Str.Groups_Recipe_MemoryDetails, Str.Groups_Recipe_MemoryDetailsAbout, Glyphs.Memory, AddFolderId.System,
    [
        new("memory", new(0, 0, 260, 60)),
        .. Metrics(60, 260, "memoryInUse", "memoryAvailable", "memoryCommitted", "memoryCached"),
    ]), Str.Groups_Recipe_Memory);

    /// <summary>[S-11090] [S-14713] GPU: the reading over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Gpu { get; } = Headed(new("gpu", Str.Groups_Recipe_Gpu, Str.Groups_Recipe_GpuAbout, Glyphs.Gpu, AddFolderId.Graphics,
    [
        new("gpu", new(0, 0, 200, 60)),
        new("gpuLoad", new(0, 60, 200, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] [S-14607] GPU details: the reading over its facts, a single metric each.</summary>
    public static ReadyMadeGroup GpuDetails { get; } = Headed(new("gpuDetails", Str.Groups_Recipe_GpuDetails, Str.Groups_Recipe_GpuDetailsAbout, Glyphs.Gpu, AddFolderId.Graphics,
    [
        new("gpu", new(0, 0, 260, 60)),
        .. Metrics(60, 260, "gpuDedicatedMemory", "gpuSharedMemory", "gpuTemperature", "gpuFan"),
    ]), Str.Groups_Recipe_Gpu);

    /// <summary>[S-11090] [S-14713] Disk: the reading over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Disk { get; } = Headed(new("disk", Str.Groups_Recipe_Disk, Str.Groups_Recipe_DiskAbout, Glyphs.Disk, AddFolderId.System,
    [
        new("disk", new(0, 0, 200, 60)),
        new("driveActivity", new(0, 60, 200, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] [S-14713] Network: download and upload on one line, over the download's graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Network { get; } = Headed(new("network", Str.Groups_Recipe_Network, Str.Groups_Recipe_NetworkAbout, Glyphs.Network, AddFolderId.NetworkReading,
    [
        new("network", new(0, 0, 344, 60)),
        new("networkDownload", new(0, 60, 344, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] Network details: the reading over the connection's facts and its adapters.</summary>
    public static ReadyMadeGroup NetworkDetails { get; } = Headed(new("networkDetails", Str.Groups_Recipe_NetworkDetails, Str.Groups_Recipe_NetworkDetailsAbout, Glyphs.Network, AddFolderId.NetworkReading,
    [
        new("network", new(0, 0, 344, 60)),
        .. Metrics(60, 344, "networkLocalAddress", "networkReceived", "networkSent", "networkLinkSpeed"),
        new("networkAdapters", new(0, 60 + (4 * Row), 344, 80), Config: """{ "rows": 2 }"""),
    ]), Str.Groups_Recipe_Network);

    /// <summary>[S-11090] [S-14713] Ping: the latency over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Ping { get; } = Headed(new("ping", Str.Groups_Recipe_Ping, Str.Groups_Recipe_PingAbout, Glyphs.Network, AddFolderId.System,
    [
        new("ping", new(0, 0, 200, 60)),
        new("pingAverage", new(0, 60, 200, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] [S-14607] Ping details: the latency over its statistics, a single metric each.</summary>
    public static ReadyMadeGroup PingDetails { get; } = Headed(new("pingDetails", Str.Groups_Recipe_PingDetails, Str.Groups_Recipe_PingDetailsAbout, Glyphs.Network, AddFolderId.System,
    [
        new("ping", new(0, 0, 260, 60)),
        .. Metrics(60, 260, "pingAverage", "pingMinimum", "pingMaximum", "pingJitter"),
    ]), Str.Groups_Recipe_Ping);

    /// <summary>[S-11090] [S-14713] Battery: the charge over its graph (its metric with its graph shown).</summary>
    public static ReadyMadeGroup Battery { get; } = Headed(new("battery", Str.Groups_Recipe_Battery, Str.Groups_Recipe_BatteryAbout, Glyphs.Battery, AddFolderId.Devices,
    [
        new("battery", new(0, 0, 280, 60)),
        new("batteryCharge", new(0, 60, 280, 64), Config: Graph),
    ]));

    /// <summary>[S-11090] [S-14607] Battery details: the charge over the battery's facts, a single metric each.</summary>
    public static ReadyMadeGroup BatteryDetails { get; } = Headed(new("batteryDetails", Str.Groups_Recipe_BatteryDetails, Str.Groups_Recipe_BatteryDetailsAbout, Glyphs.Battery, AddFolderId.Devices,
    [
        new("battery", new(0, 0, 280, 60)),
        .. Metrics(60, 280, "batteryStatus", "batteryTime", "batteryHealth", "batteryFullCapacity"),
    ]), Str.Groups_Recipe_Battery);

    /// <summary>[S-11090] [S-14607] Drive: the used share over the drive's facts, a single metric each.</summary>
    public static ReadyMadeGroup Drive { get; } = Headed(new("drive", Str.Groups_Recipe_Drive, Str.Groups_Recipe_DriveAbout, Glyphs.Disk, AddFolderId.Devices,
    [
        new("drive", new(0, 0, 280, 60)),
        .. Metrics(60, 280, "driveUsed", "driveFree", "driveCapacity", "driveFileSystem"),
    ]));

    /// <summary>[S-11172] [S-13900] Weather: the reading over its facts, the old weather card as atoms (a widget a fact).</summary>
    public static ReadyMadeGroup Weather { get; } = Headed(new("weather", Str.Groups_Recipe_Weather, Str.Groups_Recipe_WeatherAbout, Glyphs.Weather, AddFolderId.Weather,
    [
        new("weatherNow", new(0, 0, 240, 76)),
        .. Metrics(76, 240, "weatherHighLow", "weatherFeelsLike", "weatherHumidity", "weatherWind"),
    ]));

    /// <summary>
    /// [S-14607] Weather facts: every one of the weather's readings where you are, a single metric each, in the facts'
    /// order (owner, 2026-11-20: "add premade groups for the air facts and weather facts").
    /// </summary>
    public static ReadyMadeGroup WeatherFacts { get; } = Headed(new("weatherFacts", Str.Groups_Recipe_WeatherFacts, Str.Groups_Recipe_WeatherFactsAbout, Glyphs.Thermometer, AddFolderId.WeatherFacts,
    [
        .. Metrics(0, 240, "weatherTemperature", "weatherHighLow", "weatherFeelsLike", "weatherHumidity", "weatherWind", "weatherPrecipitation", "weatherPressure",
            "weatherUv", "weatherCloudCover", "weatherGusts", "weatherSunrise", "weatherSunset"),
    ]));

    /// <summary>[S-11172] [S-14607] Sun: the sun's path across the sky and its times, a single metric each.</summary>
    public static ReadyMadeGroup Sun { get; } = Headed(new("sun", Str.Groups_Recipe_Sun, Str.Groups_Recipe_SunAbout, Glyphs.Sun, AddFolderId.Weather,
    [
        new("sun", new(0, 0, 280, 152)),
        .. Metrics(152, 280, "sunrise", "sunset", "daylight"),
    ]));

    /// <summary>[S-11172] [S-14607] Moon: how much of the moon is lit, its rise, set and next full moon, a single metric each.</summary>
    public static ReadyMadeGroup Moon { get; } = Headed(new("moon", Str.Groups_Recipe_Moon, Str.Groups_Recipe_MoonAbout, Glyphs.Moon, AddFolderId.Weather,
    [
        new("moon", new(0, 0, 240, 76)),
        .. Metrics(76, 240, "moonrise", "moonset", "nextFullMoon"),
    ]));

    /// <summary>
    /// [S-11172] [S-14607] Air quality: the air quality index over its readings, a single metric each: UV, fine and coarse
    /// dust, ozone and the worst pollen (owner, 2026-11-20: "add premade groups for the air facts and weather facts").
    /// </summary>
    public static ReadyMadeGroup AirQuality { get; } = Headed(new("airQuality", Str.Groups_Recipe_AirQuality, Str.Groups_Recipe_AirQualityAbout, Glyphs.EnergySaver, AddFolderId.Weather,
    [
        new("airQuality", new(0, 0, 240, 76)),
        .. Metrics(76, 240, "airUv", "airPm25", "airPm10", "airOzone", "airPollen"),
    ]));

    /// <summary>
    /// [S-12709] [S-14713] Weather graphs: the next day's temperature, rain and UV, hour by hour, stacked as one panel: each
    /// the weather metric with its graph shown (the temperature's area, the rain's and the UV's columns).
    /// </summary>
    public static ReadyMadeGroup WeatherGraphs { get; } = Headed(new("weatherGraphs", Str.Groups_Recipe_WeatherGraphs, Str.Groups_Recipe_WeatherGraphsAbout, Glyphs.Thermometer, AddFolderId.Weather,
    [
        new("weatherTemperature", new(0, 0, 320, 168), Config: Graph),
        new("weatherPrecipitation", new(0, 168, 320, 168), Config: Graph),
        new("weatherUv", new(0, 336, 320, 168), Config: Graph),
    ]));

    /// <summary>[S-11172] Exchange rates: a currency pair's rate today and its last 90 days.</summary>
    public static ReadyMadeGroup ExchangeRates { get; } = Headed(new("exchangeRates", Str.Groups_Recipe_ExchangeRates, Str.Groups_Recipe_ExchangeRatesAbout, Glyphs.Stocks, AddFolderId.Markets,
    [
        new("exchangeRate", new(0, 0, 280, 76)),
        new("exchangeHistory", new(0, 76, 280, 96)),
    ]));

    /// <summary>[S-11172] Stocks: a stock's or a coin's price and its chart.</summary>
    public static ReadyMadeGroup Stocks { get; } = Headed(new("stocks", Str.Groups_Recipe_Stocks, Str.Groups_Recipe_StocksAbout, Glyphs.Stocks, AddFolderId.Markets,
    [
        new("stock", new(0, 0, 280, 76)),
        new("stockHistory", new(0, 76, 280, 108)),
    ]));

    /// <summary>[S-11340] [S-16512] Now playing: the one widget in its side cover, every element shown, under its header.</summary>
    public static ReadyMadeGroup NowPlaying { get; } = Headed(new("nowPlaying", Str.Groups_Recipe_NowPlaying, Str.Groups_Recipe_NowPlayingAbout, Glyphs.Music, AddFolderId.Media,
    [
        new("music", new(0, 0, 400, 88)),
    ]) { Starter = 4 });

    /// <summary>[S-11340] [S-16512] Now playing, large: the one widget stacked, its cover over the track and its progress.</summary>
    public static ReadyMadeGroup NowPlayingLarge { get; } = Headed(new("nowPlayingLarge", Str.Groups_Recipe_NowPlayingLarge, Str.Groups_Recipe_NowPlayingLargeAbout, Glyphs.Music, AddFolderId.Media,
    [
        new("music", new(0, 0, 244, 320), Config: """{"style":"stacked"}"""),
    ]), Str.Groups_Recipe_NowPlaying);

    /// <summary>[S-11341] Sound: the volume over the mixer, one header (the volume's).</summary>
    public static ReadyMadeGroup Sound { get; } = Headed(new("sound", Str.Groups_Recipe_Sound, Str.Groups_Recipe_SoundAbout, Glyphs.Volume3, AddFolderId.Media,
    [
        new("volume", new(0, 0, 300, 112)),
        new("mixer", new(0, 112, 300, 120)),
    ]));

    /// <summary>[S-11342] Quick settings: Wi-Fi, Bluetooth, Do not disturb and Night light in a row.</summary>
    public static ReadyMadeGroup QuickSettings { get; } = Headed(new("quickSettings", Str.Groups_Recipe_QuickSettings, Str.Groups_Recipe_QuickSettingsAbout, Glyphs.Settings, AddFolderId.QuickSettings,
    [
        new("quickToggle/wifi", new(0, 0, 116, 116)),
        new("quickToggle/bluetooth", new(116, 0, 116, 116)),
        new("quickToggle/doNotDisturb", new(232, 0, 116, 116)),
        new("quickToggle/nightLight", new(348, 0, 116, 116)),
    ]));

    /// <summary>[S-11343] Power: Lock, Sleep, Restart and Shut down in a row.</summary>
    public static ReadyMadeGroup Power { get; } = Headed(new("power", Str.Groups_Recipe_Power, Str.Groups_Recipe_PowerAbout, Glyphs.Power, AddFolderId.PowerButtons,
    [
        new("lock", new(0, 0, 100, 100)),
        new("sleep", new(100, 0, 100, 100)),
        new("restart", new(200, 0, 100, 100)),
        new("shutdown", new(300, 0, 100, 100)),
    ]));

    /// <summary>[S-11409] The meeting block over the meetings after it: what the Next meeting card was.</summary>
    public static ReadyMadeGroup NextMeetings { get; } = Headed(new("nextMeetings", Str.Groups_Recipe_NextMeeting, Str.Groups_Recipe_NextMeetingAbout, Glyphs.Meeting, AddFolderId.CalendarMail,
    [
        new("meeting", new(0, 0, 320, 60)),
        new("meetings", new(0, 60, 320, 120), Config: AfterNext),
    ]));

    /// <summary>[S-11409] The inbox beside the task list.</summary>
    public static ReadyMadeGroup InboxAndTasks { get; } = Headed(new("inboxAndTasks", Str.Groups_Recipe_InboxAndTasks, Str.Groups_Recipe_InboxAndTasksAbout, Glyphs.Mail, AddFolderId.CalendarMail,
    [
        new("email", new(0, 0, 300, 240), Config: """{ "rows": 6 }"""),
        new("tasks", new(300, 0, 300, 240), Config: """{ "rows": 7 }"""),
    ]) { Starter = 7 });

    /// <summary>[S-11520] Screen time: the week's total over its bars over the apps, one panel.</summary>
    public static ReadyMadeGroup ScreenTime { get; } = Headed(new("screenTime", Str.Groups_Recipe_ScreenTime, Str.Groups_Recipe_ScreenTimeAbout, Glyphs.ScreenTime, AddFolderId.ScreenTime,
    [
        new("screenTime", new(0, 0, 300, 76)),
        new("screenTimeChart", new(0, 76, 300, 116)),
        new("screenTimeApps", new(0, 192, 300, 150), Config: """{ "period": "Week" }"""),
    ]) { Starter = 6 });

    /// <summary>[S-14600] A single metric's height in a group: its one row, whole grid cells.</summary>
    private static double Row => MetricRules.RowHeight;

    /// <summary>[S-14607] Single metrics stacked from <paramref name="top"/>, <paramref name="width"/> wide, one row each, touching.</summary>
    /// <summary>[S-14713] A metric's config with its graph shown: what stood in a group as a graph widget is the metric drawing Line.</summary>
    private const string Graph = """{ "trend": "Line" }""";

    /// <summary>[S-16964] A Meetings list under a Meeting: the next meeting left out (it stands over it), compact rows.</summary>
    private const string AfterNext = """{ "afterNext": true, "style": "columnList" }""";

    private static IEnumerable<ReadyMadeWidget> Metrics(double top, double width, params string[] kinds)
        => kinds.Select((kind, i) => new ReadyMadeWidget(kind, new(0, top + (i * Row), width, Row)));

    /// <summary>[S-12485] The Header widget's height over a group: the inset, its one line and the header's gap (as a card's header took).</summary>
    public const double HeaderHeight = FamilySpace.Inset + FamilySpace.HeaderLine + FamilySpace.HeaderGap;

    /// <summary>
    /// [S-12485] Every ready-made group stands under a Header widget of its own (owner, 2026-11-16: "the groups
    /// should already include preplaced matching headers"): across the group's top, its title (the recipe's
    /// name, or <paramref name="title"/> where that says it shorter: "Weather" over the weather station) and the
    /// recipe's glyph, joined to the members under it as one panel; the members stand under it as they were.
    /// </summary>
    private static ReadyMadeGroup Headed(ReadyMadeGroup group, Str? title = null)
    {
        var bounds = group.Bounds;
        var header = new ReadyMadeWidget("header", new(bounds.X, bounds.Y, bounds.Width, HeaderHeight)) { Heading = (title ?? group.Name, group.Glyph) };
        return group with { Widgets = [header, .. group.Widgets.Select(w => w with { Box = w.Box with { Y = w.Box.Y + HeaderHeight } })] };
    }

    /// <summary>[S-2470] Every ready-made group, in the add menu's order.</summary>
    public static IReadOnlyList<ReadyMadeGroup> All { get; } = [ThisPc, MyDay, WeatherStation, MyFiles, Cpu, CpuCores, CpuDetails, Memory, MemoryDetails, Gpu, GpuDetails, Disk, Network, NetworkDetails, Ping, PingDetails, Battery, BatteryDetails, Drive, Weather, WeatherFacts, Sun, Moon, AirQuality, WeatherGraphs, ExchangeRates, Stocks, NextMeetings, InboxAndTasks, NowPlaying, NowPlayingLarge, Sound, QuickSettings, Power, ScreenTime];

    /// <summary>The ready-made group named <paramref name="id"/>, or null.</summary>
    public static ReadyMadeGroup? Find(string? id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// [S-2472] The groups this build can place: every kind of each one installed. One whose widget a
    /// build lacks is left out of the menu whole, never placed in part.
    /// </summary>
    public static IReadOnlyList<ReadyMadeGroup> Offered()
        => [.. All.Where(r => r.Widgets.All(w => WidgetPick.TryParse(w.Kind, out _)))];

    /// <summary>[S-2471] A recipe box on <paramref name="screen"/>, its group's corner at (<paramref name="xDip"/>, <paramref name="yDip"/>) DIPs.</summary>
    public static FracRect Rect(ReadyMadeBox box, ScreenMetrics screen, double xDip = 0, double yDip = 0)
        => FracRect.FromDips((xDip + box.X) / screen.Width, (yDip + box.Y) / screen.Height, box.Width, box.Height, screen);

    /// <summary>
    /// [S-11809] The recipe's boxes on <paramref name="screen"/> (its corner at the origin) as the
    /// desktop places and paints them with <paramref name="sizes"/>: one block (<see cref="GridFit.Layout"/>)
    /// in which each box asks for its card's DIPs at the content scale, held to its kind's
    /// sizing there (on the grid in whole cells), every edge the recipe shares still shared.
    /// </summary>
    public static IReadOnlyList<FracRect> Laid(ReadyMadeGroup recipe, ScreenMetrics screen, PlacementSizes sizes)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var boxes = recipe.Widgets.Select(w => Rect(w.Box, screen)).ToList();
        if (!screen.IsUsable)
        {
            return boxes;
        }

        var scale = double.IsFinite(sizes.Scale) && sizes.Scale > 0 ? sizes.Scale : 1;
        IReadOnlyList<SizeLimits?> limits = Build(recipe, screen) is { } widgets
            ? [.. widgets.Select(w => SizeFit.Limits(w, screen, scale))]
            : [.. boxes.Select(_ => (SizeLimits?)null)];
        var asked = recipe.Widgets.Select(w => (w.Box.Width * scale / screen.Width, w.Box.Height * scale / screen.Height)).ToList();
        return GridFit.Layout(boxes, limits, screen, asked);
    }

    /// <summary>
    /// [S-2471] The widgets of <paramref name="recipe"/> on <paramref name="screen"/>, its corner at
    /// (<paramref name="xDip"/>, <paramref name="yDip"/>) DIPs, every id new, each the kind's default config
    /// with the fragment over it (as the first-run seed builds them): loose widgets, in no group of any
    /// kind ([S-2475]). Null, logged, when this build lacks one of its kinds.
    /// </summary>
    public static List<WidgetInstance>? Build(ReadyMadeGroup recipe, ScreenMetrics screen, double xDip = 0, double yDip = 0)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var widgets = new List<WidgetInstance>(recipe.Widgets.Count);
        foreach (var part in recipe.Widgets)
        {
            var rect = Rect(part.Box, screen, xDip, yDip);
            if (FirstRunDesktop.Build(new FirstRunDesktop.Seed(part.Kind, rect, Config: part.Fragment)) is not { } widget)
            {
                Log.Warn(Scope, $"'{recipe.Id}' names a widget this build does not have ('{part.Kind}'); it is not placed");
                return null;
            }

            widgets.Add(widget);
        }

        return widgets;
    }
}
