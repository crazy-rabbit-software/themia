using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// The widget catalogue. A <b>closed set</b>, so it is an enum rather than a string: an unknown
/// value coming out of persisted JSON is a corrupt-data signal, not a plugin.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WidgetKind>))]
public enum WidgetKind
{
    Clock = 0,

    /// <summary>Processor load across all cores.</summary>
    Cpu = 1,

    /// <summary>Physical memory in use.</summary>
    Memory = 2,

    /// <summary>Space used on one drive.</summary>
    Disk = 3,

    /// <summary>Charge level and whether it is on mains.</summary>
    Battery = 4,

    /// <summary>Recent mail from the signed-in accounts.</summary>
    Email = 5,

    /// <summary>Bytes per second across the active adapters.</summary>
    Network = 7,

    /// <summary>Round-trip time to a host.</summary>
    Ping = 8,

    /// <summary>People from the signed-in address books.</summary>
    Contacts = 9,

    /// <summary>Open tasks from the signed-in accounts, or a list on this PC.</summary>
    Tasks = 11,

    /// <summary>A free-text note kept on this machine.</summary>
    Notes = 12,

    /// <summary>Time left until one or more dated targets.</summary>
    Countdown = 13,

    /// <summary>A focus/break timer.</summary>
    Pomodoro = 14,


    /// <summary>[S-4123] The contents of one folder on this machine, as a list, a table, tiles or icons (its styles).</summary>
    Folder = 17,

    /// <summary>Master output level and mute.</summary>
    Volume = 18,

    /// <summary>Paired devices and their battery.</summary>
    Bluetooth = 20,

    /// <summary>Whatever Windows is playing.</summary>
    Music = 21,

    /// <summary>Conditions where you are.</summary>
    WeatherNow = 22,

    /// <summary>The next few days.</summary>
    WeatherForecast = 23,

    /// <summary>Tickers and crypto, with a sparkline.</summary>
    Stocks = 24,

    /// <summary>Headlines from a feed you choose.</summary>
    Rss = 25,

    /// <summary>Links, files and folders you keep to hand.</summary>
    Shortcuts = 27,

    /// <summary>What you have copied recently.</summary>
    Clipboard = 29,

    /// <summary>A place to draw.</summary>
    Scratchpad = 30,

    /// <summary>A line worth rereading.</summary>
    Quote = 32,

    /// <summary>Pictures from a folder.</summary>
    Gallery = 33,

    /// <summary>Daily check-offs and streaks.</summary>
    HabitTracker = 34,

    /// <summary>A live camera picture: MJPEG, a polled snapshot, or a stream Windows can play.</summary>
    VideoStream = 42,

    /// <summary>[S-433] Graphics load of one adapter (one widget per adapter).</summary>
    Gpu = 39,

    /// <summary>Per-application volume for the default output device.</summary>
    Mixer = 40,

    /// <summary>[S-1910] One button: shut down. Each power action is its own widget.</summary>
    Shutdown = 53,

    /// <summary>[S-1910] One button: restart.</summary>
    Restart = 54,

    /// <summary>[S-1910] One button: sleep.</summary>
    Sleep = 55,

    /// <summary>[S-1910] One button: hibernate.</summary>
    Hibernate = 56,

    /// <summary>[S-1910] One button: lock.</summary>
    Lock = 57,

    /// <summary>[S-1910] One button: sign out.</summary>
    SignOut = 58,

    /// <summary>[S-1910] One button: turn off the display.</summary>
    ScreenOff = 59,

    /// <summary>[S-2405] The processor's speed, cores, processes and uptime.</summary>
    CpuDetails = 60,

    /// <summary>[S-2408] Memory in use, committed, cached, the pools and the page file.</summary>
    MemoryDetails = 61,

    /// <summary>[S-2410] The busiest processes, by processor or memory.</summary>
    TopProcesses = 62,

    /// <summary>[S-2415] Rates, session totals, addresses and every adapter.</summary>
    NetworkDetails = 63,

    /// <summary>[S-2423] The battery's status, time left, health and cycles.</summary>
    BatteryDetails = 65,

    /// <summary>[S-2426] Latency statistics: average, range, jitter and loss.</summary>
    PingDetails = 66,

    /// <summary>[S-2430] One adapter's video memory and sensors.</summary>
    GpuDetails = 67,

    /// <summary>[S-3100] The sun's path, sunrise, sunset and daylight, computed offline. Replaces Sun &amp; moon (16).</summary>
    Sun = 68,

    /// <summary>[S-3100] The moon's phase, rise and set, computed offline.</summary>
    Moon = 69,

    /// <summary>[S-3200] The spectrum (or the waveform) of whatever the PC plays, split out of Music, in six styles ([S-3216]).</summary>
    AudioVisualizer = 70,

    /// <summary>[S-3640] Events and birthdays as a month, a week, a day, a year or an agenda (its styles).</summary>
    Calendar = 71,

    /// <summary>[S-3320] Screen time over the last week or month, one bar (or calendar cell) per day. Replaces Screen time (41).</summary>
    ScreenTimeChart = 80,

    /// <summary>[S-3320] Each app's share of the day (or week, or month), as a ring.</summary>
    ScreenTimeDonut = 81,

    /// <summary>[S-3320] The apps used, busiest first, with their time and limits.</summary>
    ScreenTimeApps = 82,

    /// <summary>[S-3900] Quick setting: the Wi-Fi radio on or off.</summary>
    WifiToggle = 83,

    /// <summary>[S-3900] Quick setting: the Bluetooth radio on or off.</summary>
    BluetoothToggle = 84,

    /// <summary>[S-3900] Quick setting: airplane mode.</summary>
    AirplaneModeToggle = 85,

    /// <summary>[S-3900] Quick setting: energy saver.</summary>
    EnergySaverToggle = 86,

    /// <summary>[S-3900] Quick setting: night light.</summary>
    NightLightToggle = 87,

    /// <summary>[S-3900] Quick setting: rotation lock.</summary>
    RotationLockToggle = 88,

    /// <summary>[S-3900] Quick setting: do not disturb.</summary>
    DoNotDisturbToggle = 89,

    /// <summary>[S-3900] Quick setting: the default output muted or not.</summary>
    SoundToggle = 90,

    /// <summary>[S-3900] Quick setting: the default microphone muted or not.</summary>
    MicrophoneToggle = 91,

    /// <summary>[S-3900] Quick setting: spatial sound (Windows Sonic) on the default output.</summary>
    SpatialSoundToggle = 92,

    /// <summary>[S-3900] Quick setting: mono audio.</summary>
    MonoAudioToggle = 93,

    /// <summary>[S-3900] Quick setting: color filters.</summary>
    ColorFiltersToggle = 94,

    /// <summary>[S-3900] Quick setting: game mode.</summary>
    GameModeToggle = 95,

    /// <summary>[S-4600] Start, pause, laps; kept running across a restart.</summary>
    Stopwatch = 117,

    /// <summary>[S-4620] Alarms at a time of day, once or on chosen days, rung by the app's alarm service.</summary>
    Alarm = 118,

    /// <summary>[S-4660] USB sticks, cards and external disks, each with Eject.</summary>
    RemovableDrives = 119,

    /// <summary>[S-4670] One drive's space, file system and, for a mapped share, whether it is connected.</summary>
    Drive = 120,

    /// <summary>[S-4400] Windows' notification list, grouped by app, with dismiss and clear all.</summary>
    Notifications = 121,

    /// <summary>[S-4420] A focus session of N minutes that holds Windows' do not disturb while it runs.</summary>
    FocusSession = 122,

    /// <summary>[S-4440] The display brightness, one slider for every display (or the one it is on).</summary>
    Brightness = 123,

    /// <summary>[S-4800] How much of the day, week, month and year has passed.</summary>
    YearProgress = 124,

    /// <summary>[S-4820] The next (or running) event from the synced calendars, with Join for an online meeting.</summary>
    NextMeeting = 125,

    /// <summary>[S-4840] The files opened last, as Windows' Recent Items lists them.</summary>
    RecentFiles = 126,

    /// <summary>[S-4860] The Recycle Bin's size and item count, with Empty.</summary>
    RecycleBin = 127,

    /// <summary>[S-5100] Air quality index, UV and pollen at a place, from open-meteo.</summary>
    AirQuality = 128,

    /// <summary>[S-5120] Rain in the next two hours, in 15-minute steps, from open-meteo.</summary>
    RainNextHour = 129,

    /// <summary>[S-5140] The ECB's reference exchange rates for a base currency, with the day's change.</summary>
    ExchangeRates = 130,

    /// <summary>[S-5320] The days ahead with their events, and today's tasks.</summary>
    WeekPlanner = 133,
}

/// <summary>How a countdown renders the time it has left. [C-11]</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CountdownFormat>))]
public enum CountdownFormat
{
    /// <summary>Pick the coarsest unit that still says something useful.</summary>
    Auto = 0,

    /// <summary>Whole days only.</summary>
    Days = 1,

    /// <summary>Days, hours and minutes.</summary>
    DaysHoursMinutes = 2,

    /// <summary>Days, hours, minutes and seconds — the only format that needs a one-second tick.</summary>
    DaysHoursMinutesSeconds = 3,
}

/// <summary>Where a widget draws its header.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeaderPlacement>))]
public enum HeaderPlacement
{
    Top = 0,
    Bottom = 1,

    /// <summary>No header. The widget is the content and nothing else.</summary>
    Hidden = 2,

    /// <summary>[S-404] A strip down the card's left edge, the title turned to read bottom to top.</summary>
    Left = 3,

    /// <summary>[S-404] A strip down the card's right edge, the title turned to read top to bottom.</summary>
    Right = 4,
}

/// <summary>
/// [S-6580] Where a widget's content sits across its card: the start edge, the middle or the end
/// edge. Left and Right are the reading direction's start and end, as Windows aligns text.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContentAlign>))]
public enum ContentAlign
{
    Left = 0,
    Center = 1,
    Right = 2,
}

/// <summary>Light/dark selection. <c>System</c> follows the Windows app theme.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>How a widget card's border is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WidgetBorderStyle>))]
public enum WidgetBorderStyle
{
    None = 0,
    Solid = 1,
    Dashed = 2,
    Dotted = 3,
}

/// <summary>
/// The one-shot animation widgets play when a view appears. A closed set, because each value picks
/// a different piece of code rather than a parameter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntroAnimation>))]
public enum IntroAnimation
{
    None = 0,
    Fade = 1,
    Scale = 2,
    Slide = 3,

    /// <summary>Widgets fly in from the nearer side edge.</summary>
    Sides = 4,

    /// <summary>Widgets fly in from the nearer top or bottom edge.</summary>
    TopBottom = 5,

    Flip = 6,

    Blur = 7,

    /// <summary>A star-field overlay the widgets drop out of.</summary>
    Hyperspace = 8,

    /// <summary>Each widget types itself in, terminal style.</summary>
    Hacker = 9,
}
