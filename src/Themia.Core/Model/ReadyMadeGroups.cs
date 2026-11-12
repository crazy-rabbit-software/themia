using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2470] Where a widget of a ready-made group stands, in DIPs from the group's top-left corner.</summary>
public readonly record struct ReadyMadeBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// [S-2470] One widget of a ready-made group: its kind (a widget id, as the first-run seed names them),
/// its box, and what differs from its kind's defaults: a config fragment in the state file's own shape
/// (merged over the default, property by property), a header placement and a title.
/// </summary>
public sealed record ReadyMadeWidget(string Kind, ReadyMadeBox Box, string? Config = null, HeaderPlacement? Header = null, string? Label = null);

/// <summary>
/// [S-2470] A ready-made group: a recipe that places several widgets, touching (so they draw as one
/// panel, [S-2240]), already grouped ([S-2450]) under the recipe's name. Pure data; its words go through
/// <see cref="Str"/>. <paramref name="Home"/> is the subject of the add menu it is about: a search result
/// shows it as the recipe's place ([S-2473]).
/// </summary>
public sealed record ReadyMadeGroup(string Id, Str Name, Str About, string Glyph, AddFolderId Home, IReadOnlyList<ReadyMadeWidget> Widgets)
{
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

    /// <summary>[S-2474] [S-11090] This PC: the four readings as a 2 × 2 grid, each over its history.</summary>
    public static ReadyMadeGroup ThisPc { get; } = new("thisPc", Str.Groups_Recipe_ThisPc, Str.Groups_Recipe_ThisPcAbout, Glyphs.Display, AddFolderId.System,
    [
        new("cpu", new(0, 0, 180, 84)),
        new("cpuHistory", new(0, 84, 180, 64)),
        new("memory", new(180, 0, 180, 84)),
        new("memoryHistory", new(180, 84, 180, 64)),
        new("network", new(0, 148, 180, 128)),
        new("networkHistory", new(0, 276, 180, 64)),
        new("disk", new(180, 148, 180, 84)),
        new("diskHistory", new(180, 232, 180, 108)),
    ]);

    /// <summary>[S-2474] My day: the clock on top, the next meeting and the tasks under it.</summary>
    public static ReadyMadeGroup MyDay { get; } = new("myDay", Str.Groups_Recipe_MyDay, Str.Groups_Recipe_MyDayAbout, Glyphs.Calendar, AddFolderId.CalendarMail,
    [
        new("clock", new(0, 0, 320, 138)),
        new("nextMeeting", new(0, 138, 320, 124)),
        new("upNext", new(0, 262, 320, 108), Header: HeaderPlacement.Hidden),
        new("tasks", new(0, 370, 320, 220), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-2474] [S-11172] Weather station: the reading, its facts, the days ahead and the rain in the next hour, stacked as one panel.</summary>
    public static ReadyMadeGroup WeatherStation { get; } = new("weatherStation", Str.Groups_Recipe_WeatherStation, Str.Groups_Recipe_WeatherStationAbout, Glyphs.Weather, AddFolderId.Weather,
    [
        new("weatherNow", new(0, 0, 300, 100)),
        new("weatherFacts", new(0, 100, 300, 136), Header: HeaderPlacement.Hidden),
        new("weatherForecast", new(0, 236, 300, 164), Header: HeaderPlacement.Hidden),
        new("rainNextHour", new(0, 400, 300, 148), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>
    /// [S-11209] My files: the Documents folder as a list on the left; beside it the shortcuts as a
    /// grid of four (the Recycle Bin, This PC, Downloads, Documents) over the files opened last, four
    /// rows. The three are as tall as one another's column, so they join into one panel.
    /// </summary>
    public static ReadyMadeGroup MyFiles { get; } = new("myFiles", Str.Groups_Recipe_MyFiles, Str.Groups_Recipe_MyFilesAbout, Glyphs.Folder, AddFolderId.Files,
    [
        new("folder", new(0, 0, 300, 308), Config: """{ "style": "list", "path": "%USERPROFILE%\\Documents" }"""),
        new("shortcuts", new(300, 0, 316, 100), Config: """
            { "style": "grid", "columns": 4, "items": [
                { "id": "bin", "target": "shell:RecycleBinFolder" },
                { "id": "pc", "target": "shell:MyComputerFolder" },
                { "id": "downloads", "target": "shell:Downloads" },
                { "id": "documents", "target": "shell:Personal" } ] }
            """),
        new("recentFiles", new(300, 100, 316, 208), Config: """{ "rows": 4 }"""),
    ]);

    // ---- [S-11090] The system readings' groups: each old card's look, made of its atoms ----------------

    /// <summary>[S-11090] CPU: the reading over its history.</summary>
    public static ReadyMadeGroup Cpu { get; } = new("cpu", Str.Groups_Recipe_Cpu, Str.Groups_Recipe_CpuAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 160, 84)),
        new("cpuHistory", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] CPU cores: the reading over a bar per core.</summary>
    public static ReadyMadeGroup CpuCores { get; } = new("cpuCores", Str.Groups_Recipe_CpuCores, Str.Groups_Recipe_CpuCoresAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 160, 84)),
        new("cpuCores", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] CPU details: the reading over the processor's facts.</summary>
    public static ReadyMadeGroup CpuDetails { get; } = new("cpuDetails", Str.Groups_Recipe_CpuDetails, Str.Groups_Recipe_CpuDetailsAbout, Glyphs.Cpu, AddFolderId.Processor,
    [
        new("cpu", new(0, 0, 240, 84)),
        new("cpuFacts", new(0, 84, 240, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11090] Memory: the reading over its history.</summary>
    public static ReadyMadeGroup Memory { get; } = new("memory", Str.Groups_Recipe_Memory, Str.Groups_Recipe_MemoryAbout, Glyphs.Memory, AddFolderId.MemoryReading,
    [
        new("memory", new(0, 0, 160, 84)),
        new("memoryHistory", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] Memory details: the reading over its facts.</summary>
    public static ReadyMadeGroup MemoryDetails { get; } = new("memoryDetails", Str.Groups_Recipe_MemoryDetails, Str.Groups_Recipe_MemoryDetailsAbout, Glyphs.Memory, AddFolderId.MemoryReading,
    [
        new("memory", new(0, 0, 260, 84)),
        new("memoryFacts", new(0, 84, 260, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11090] GPU: the reading over its history.</summary>
    public static ReadyMadeGroup Gpu { get; } = new("gpu", Str.Groups_Recipe_Gpu, Str.Groups_Recipe_GpuAbout, Glyphs.Gpu, AddFolderId.Graphics,
    [
        new("gpu", new(0, 0, 200, 84)),
        new("gpuHistory", new(0, 84, 200, 64)),
    ]);

    /// <summary>[S-11090] GPU details: the reading over its facts.</summary>
    public static ReadyMadeGroup GpuDetails { get; } = new("gpuDetails", Str.Groups_Recipe_GpuDetails, Str.Groups_Recipe_GpuDetailsAbout, Glyphs.Gpu, AddFolderId.Graphics,
    [
        new("gpu", new(0, 0, 260, 84)),
        new("gpuFacts", new(0, 84, 260, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11090] Disk: the reading over its history.</summary>
    public static ReadyMadeGroup Disk { get; } = new("disk", Str.Groups_Recipe_Disk, Str.Groups_Recipe_DiskAbout, Glyphs.Disk, AddFolderId.System,
    [
        new("disk", new(0, 0, 160, 84)),
        new("diskHistory", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] Network: download over upload, over their history.</summary>
    public static ReadyMadeGroup Network { get; } = new("network", Str.Groups_Recipe_Network, Str.Groups_Recipe_NetworkAbout, Glyphs.Network, AddFolderId.NetworkReading,
    [
        new("network", new(0, 0, 180, 128)),
        new("networkHistory", new(0, 128, 180, 64)),
    ]);

    /// <summary>[S-11090] Network details: the reading over the connection's facts and its adapters.</summary>
    public static ReadyMadeGroup NetworkDetails { get; } = new("networkDetails", Str.Groups_Recipe_NetworkDetails, Str.Groups_Recipe_NetworkDetailsAbout, Glyphs.Network, AddFolderId.NetworkReading,
    [
        new("network", new(0, 0, 300, 128)),
        new("networkFacts", new(0, 128, 300, 136), Header: HeaderPlacement.Hidden),
        new("networkAdapters", new(0, 264, 300, 112), Header: HeaderPlacement.Hidden, Config: """{ "rows": 2 }"""),
    ]);

    /// <summary>[S-11090] Ping: the latency over its history.</summary>
    public static ReadyMadeGroup Ping { get; } = new("ping", Str.Groups_Recipe_Ping, Str.Groups_Recipe_PingAbout, Glyphs.Network, AddFolderId.PingReading,
    [
        new("ping", new(0, 0, 160, 84)),
        new("pingHistory", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] Ping details: the latency over its statistics.</summary>
    public static ReadyMadeGroup PingDetails { get; } = new("pingDetails", Str.Groups_Recipe_PingDetails, Str.Groups_Recipe_PingDetailsAbout, Glyphs.Network, AddFolderId.PingReading,
    [
        new("ping", new(0, 0, 260, 84)),
        new("pingFacts", new(0, 84, 260, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11090] Battery: the charge over its history.</summary>
    public static ReadyMadeGroup Battery { get; } = new("battery", Str.Groups_Recipe_Battery, Str.Groups_Recipe_BatteryAbout, Glyphs.Battery, AddFolderId.BatteryReading,
    [
        new("battery", new(0, 0, 160, 84)),
        new("batteryHistory", new(0, 84, 160, 64)),
    ]);

    /// <summary>[S-11090] Battery details: the charge over the battery's facts.</summary>
    public static ReadyMadeGroup BatteryDetails { get; } = new("batteryDetails", Str.Groups_Recipe_BatteryDetails, Str.Groups_Recipe_BatteryDetailsAbout, Glyphs.Battery, AddFolderId.BatteryReading,
    [
        new("battery", new(0, 0, 260, 84)),
        new("batteryFacts", new(0, 84, 260, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11090] Drive: the used share over the drive's facts.</summary>
    public static ReadyMadeGroup Drive { get; } = new("drive", Str.Groups_Recipe_Drive, Str.Groups_Recipe_DriveAbout, Glyphs.Disk, AddFolderId.Devices,
    [
        new("drive", new(0, 0, 260, 112)),
        new("driveFacts", new(0, 112, 260, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Weather: the reading over its facts, the old weather card as two atoms.</summary>
    public static ReadyMadeGroup Weather { get; } = new("weather", Str.Groups_Recipe_Weather, Str.Groups_Recipe_WeatherAbout, Glyphs.Weather, AddFolderId.Weather,
    [
        new("weatherNow", new(0, 0, 240, 100)),
        new("weatherFacts", new(0, 100, 240, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Sun: the sun's path across the sky and its times.</summary>
    public static ReadyMadeGroup Sun { get; } = new("sun", Str.Groups_Recipe_Sun, Str.Groups_Recipe_SunAbout, Glyphs.Sun, AddFolderId.Weather,
    [
        new("sun", new(0, 0, 280, 176), Header: HeaderPlacement.Top),
        new("sunTimes", new(0, 176, 280, 108), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Moon: how much of the moon is lit, its rise, set and next full moon.</summary>
    public static ReadyMadeGroup Moon { get; } = new("moon", Str.Groups_Recipe_Moon, Str.Groups_Recipe_MoonAbout, Glyphs.Moon, AddFolderId.Weather,
    [
        new("moon", new(0, 0, 240, 100)),
        new("moonTimes", new(0, 100, 240, 108), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Air quality: the air quality index, UV, fine dust and pollen.</summary>
    public static ReadyMadeGroup AirQuality { get; } = new("airQuality", Str.Groups_Recipe_AirQuality, Str.Groups_Recipe_AirQualityAbout, Glyphs.EnergySaver, AddFolderId.Weather,
    [
        new("airQuality", new(0, 0, 240, 100)),
        new("airFacts", new(0, 100, 240, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Exchange rates: a currency pair's rate today and its last 90 days.</summary>
    public static ReadyMadeGroup ExchangeRates { get; } = new("exchangeRates", Str.Groups_Recipe_ExchangeRates, Str.Groups_Recipe_ExchangeRatesAbout, Glyphs.Stocks, AddFolderId.Markets,
    [
        new("exchangeRate", new(0, 0, 280, 100)),
        new("exchangeHistory", new(0, 100, 280, 96), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11172] Stocks: a stock's or a coin's price and its chart.</summary>
    public static ReadyMadeGroup Stocks { get; } = new("stocks", Str.Groups_Recipe_Stocks, Str.Groups_Recipe_StocksAbout, Glyphs.Stocks, AddFolderId.Markets,
    [
        new("stock", new(0, 0, 280, 100)),
        new("stockHistory", new(0, 100, 280, 108), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11340] Now playing: the cover at the side, the track beside it, the progress under both (the old card, tighter).</summary>
    public static ReadyMadeGroup NowPlaying { get; } = new("nowPlaying", Str.Groups_Recipe_NowPlaying, Str.Groups_Recipe_NowPlayingAbout, Glyphs.Music, AddFolderId.Media,
    [
        new("musicCover", new(0, 0, 96, 96), Config: """{ "size": "Small" }"""),
        new("music", new(96, 0, 244, 96)),
        new("musicProgress", new(0, 96, 340, 40)),
    ]);

    /// <summary>[S-11340] Now playing, large: the cover on top, the track and the progress under it.</summary>
    public static ReadyMadeGroup NowPlayingLarge { get; } = new("nowPlayingLarge", Str.Groups_Recipe_NowPlayingLarge, Str.Groups_Recipe_NowPlayingLargeAbout, Glyphs.Music, AddFolderId.Media,
    [
        new("musicCover", new(0, 0, 240, 240), Config: """{ "size": "Large" }"""),
        new("music", new(0, 240, 240, 96)),
        new("musicProgress", new(0, 336, 240, 40)),
    ]);

    /// <summary>[S-11341] Sound: the volume over the mixer, one header (the volume's).</summary>
    public static ReadyMadeGroup Sound { get; } = new("sound", Str.Groups_Recipe_Sound, Str.Groups_Recipe_SoundAbout, Glyphs.Volume3, AddFolderId.Media,
    [
        new("volume", new(0, 0, 300, 136)),
        new("mixer", new(0, 136, 300, 136), Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11342] Quick settings: Wi-Fi, Bluetooth, Do not disturb and Night light in a row.</summary>
    public static ReadyMadeGroup QuickSettings { get; } = new("quickSettings", Str.Groups_Recipe_QuickSettings, Str.Groups_Recipe_QuickSettingsAbout, Glyphs.Settings, AddFolderId.QuickSettings,
    [
        new("wifiToggle", new(0, 0, 120, 120)),
        new("bluetoothToggle", new(120, 0, 120, 120)),
        new("doNotDisturbToggle", new(240, 0, 120, 120)),
        new("nightLightToggle", new(360, 0, 120, 120)),
    ]);

    /// <summary>[S-11343] Power: Lock, Sleep, Restart and Shut down in a row.</summary>
    public static ReadyMadeGroup Power { get; } = new("power", Str.Groups_Recipe_Power, Str.Groups_Recipe_PowerAbout, Glyphs.Power, AddFolderId.PowerButtons,
    [
        new("lock", new(0, 0, 120, 120)),
        new("sleep", new(120, 0, 120, 120)),
        new("restart", new(240, 0, 120, 120)),
        new("shutdown", new(360, 0, 120, 120)),
    ]);

    /// <summary>[S-2470] Every ready-made group, in the add menu's order.</summary>
    /// <summary>[S-11409] The meeting block over the meetings after it: what the Next meeting card was.</summary>
    public static ReadyMadeGroup NextMeetings { get; } = new("nextMeetings", Str.Groups_Recipe_NextMeeting, Str.Groups_Recipe_NextMeetingAbout, Glyphs.Meeting, AddFolderId.CalendarMail,
    [
        new("nextMeeting", new(0, 0, 320, 124)),
        new("upNext", new(0, 124, 320, 132)),
    ]);

    /// <summary>[S-11409] The inbox beside the task list.</summary>
    public static ReadyMadeGroup InboxAndTasks { get; } = new("inboxAndTasks", Str.Groups_Recipe_InboxAndTasks, Str.Groups_Recipe_InboxAndTasksAbout, Glyphs.Mail, AddFolderId.CalendarMail,
    [
        new("email", new(0, 0, 300, 248)),
        new("tasks", new(300, 0, 300, 248), Config: """{ "rows": 7 }""", Header: HeaderPlacement.Hidden),
    ]);

    /// <summary>[S-11520] Screen time: the week's total over its bars over the apps, one panel.</summary>
    public static ReadyMadeGroup ScreenTime { get; } = new("screenTime", Str.Groups_Recipe_ScreenTime, Str.Groups_Recipe_ScreenTimeAbout, Glyphs.ScreenTime, AddFolderId.ScreenTime,
    [
        new("screenTime", new(0, 0, 300, 100)),
        new("screenTimeChart", new(0, 100, 300, 116)),
        new("screenTimeApps", new(0, 216, 300, 164), Config: """{ "period": "Week" }""", Header: HeaderPlacement.Hidden),
    ]);

    public static IReadOnlyList<ReadyMadeGroup> All { get; } = [ThisPc, MyDay, WeatherStation, MyFiles, Cpu, CpuCores, CpuDetails, Memory, MemoryDetails, Gpu, GpuDetails, Disk, Network, NetworkDetails, Ping, PingDetails, Battery, BatteryDetails, Drive, Weather, Sun, Moon, AirQuality, ExchangeRates, Stocks, NextMeetings, InboxAndTasks, NowPlaying, NowPlayingLarge, Sound, QuickSettings, Power, ScreenTime];

    /// <summary>The ready-made group named <paramref name="id"/>, or null.</summary>
    public static ReadyMadeGroup? Find(string? id) => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// [S-2472] The groups this build can place: every kind of each one installed. One whose widget a
    /// build lacks is left out of the menu whole, never placed in part.
    /// </summary>
    public static IReadOnlyList<ReadyMadeGroup> Offered()
        => [.. All.Where(r => r.Widgets.All(w => WidgetId.TryParse(w.Kind, out var id) && ModuleCatalog.TryGet(id, out _)))];

    /// <summary>[S-2471] A recipe box on <paramref name="screen"/>, its group's corner at (<paramref name="xPx"/>, <paramref name="yPx"/>) DIPs.</summary>
    public static FracRect Rect(ReadyMadeBox box, ScreenMetrics screen, double xPx = 0, double yPx = 0)
        => FracRect.FromPixels((xPx + box.X) / screen.Width, (yPx + box.Y) / screen.Height, box.Width, box.Height, screen);

    /// <summary>
    /// [S-2471] The widgets of <paramref name="recipe"/> on <paramref name="screen"/>, its corner at
    /// (<paramref name="xPx"/>, <paramref name="yPx"/>) DIPs, every id new, each the kind's default config
    /// with the fragment over it (as the first-run seed builds them), in one new group named after the
    /// recipe. Null, logged, when this build lacks one of its kinds.
    /// </summary>
    public static (List<WidgetInstance> Widgets, WidgetGroup Group)? Build(ReadyMadeGroup recipe, ScreenMetrics screen, double xPx = 0, double yPx = 0)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var group = WidgetGroup.Create(recipe.Name.Text());
        var widgets = new List<WidgetInstance>(recipe.Widgets.Count);
        foreach (var part in recipe.Widgets)
        {
            var rect = Rect(part.Box, screen, xPx, yPx);
            if (FirstRunDesktop.Build(new FirstRunDesktop.Seed(part.Kind, rect, Config: part.Config)) is not { } widget)
            {
                Log.Warn(Scope, $"'{recipe.Id}' names a widget this build does not have ('{part.Kind}'); it is not placed");
                return null;
            }

            widget.Header = part.Header is { } header && WidgetInstance.CanHaveHeader(widget.Kind) ? header : widget.Header;
            widget.Label = part.Label ?? widget.Label;
            widget.Group = group.Id;
            widgets.Add(widget);
        }

        return (widgets, group);
    }
}
