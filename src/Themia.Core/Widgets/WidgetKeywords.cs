using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-943] Curated search words for the widget picker, ported from the original's
/// <c>searchKeywords.ts</c>.
/// <para>
/// A widget's label and description are written for reading, not for searching: nobody reads
/// "Bytes per second across the active adapters" and types that. These are the words people reach
/// for instead: what the widget shows ("wifi", "temperature"), the vendor or protocol behind it
/// ("outlook", "imap"), the job it does ("timer", "launcher") and the other common spelling
/// ("todo" for tasks).
/// </para>
/// <para>
/// English only, on purpose: the localisation round indexes the translated label and description
/// next to the English ones, so this list keeps working in every language without being written
/// nine times.
/// </para>
/// </summary>
public static class WidgetKeywords
{
    private static readonly Dictionary<WidgetKind, string[]> Keywords = new()
    {
        [WidgetKind.Folder] = ["files", "explorer", "directory", "file browser", "documents", "downloads"],
        [WidgetKind.Email] = ["email", "inbox", "outlook", "gmail", "imap", "messages", "unread"],
        [WidgetKind.Contacts] = ["address book", "people", "phone numbers", "directory"],
        [WidgetKind.MonthCalendar] = ["calendar", "events", "meetings", "appointments", "schedule", "outlook", "google calendar", "birthdays"],
        [WidgetKind.WeekCalendar] = ["calendar", "events", "meetings", "appointments", "schedule", "outlook", "google calendar", "birthdays"],
        [WidgetKind.DayCalendar] = ["calendar", "events", "meetings", "appointments", "schedule", "today", "timeline", "outlook", "google calendar"],
        [WidgetKind.YearCalendar] = ["calendar", "events", "holidays", "overview", "outlook", "google calendar", "birthdays"],
        [WidgetKind.Agenda] = ["calendar", "events", "meetings", "appointments", "schedule", "upcoming", "next", "outlook", "google calendar", "birthdays"],
        [WidgetKind.Tasks] = ["todo", "to-do", "checklist", "reminders", "microsoft to do", "google tasks"],

        [WidgetKind.WeatherNow] = ["temperature", "forecast", "rain", "wind", "humidity", "climate", "degrees"],
        [WidgetKind.WeatherForecast] = ["weather", "temperature", "rain", "week", "outlook", "climate", "degrees"],
        [WidgetKind.Rss] = ["rss", "feed", "news", "headlines", "atom", "blog", "reader"],
        [WidgetKind.Stocks] = ["shares", "ticker", "market", "finance", "crypto", "bitcoin", "forex", "currency", "portfolio", "price"],
        [WidgetKind.VideoStream] = ["camera", "webcam", "cctv", "mjpeg", "live view", "printer"],
        [WidgetKind.AudioVisualizer] = ["spectrum", "equalizer", "equaliser", "visualiser", "bars", "audio", "sound", "music"],
        [WidgetKind.Music] = ["spotify", "player", "playback", "song", "track", "audio", "media controls", "now playing"],

        [WidgetKind.Cpu] = ["processor", "load", "usage", "performance", "cores", "task manager", "system monitor"],
        [WidgetKind.Memory] = ["ram", "usage", "performance", "task manager", "system monitor"],
        [WidgetKind.Gpu] = ["graphics", "vram", "nvidia", "amd", "video card", "system monitor"],
        [WidgetKind.Network] = ["internet", "bandwidth", "wifi", "ethernet", "speed", "traffic", "up down"],
        [WidgetKind.Disk] = ["storage", "ssd", "hdd", "drive", "space", "free space", "io"],
        [WidgetKind.Ping] = ["latency", "network", "internet", "connection", "packet loss", "ms"],
        [WidgetKind.Battery] = ["power", "charge", "laptop", "percent", "energy"],
        [WidgetKind.Bluetooth] = ["devices", "headphones", "wireless", "pairing"],
        [WidgetKind.ScreenTimeChart] = ["usage", "productivity", "time tracking", "screen time", "history", "week", "chart", "statistics"],
        [WidgetKind.ScreenTimeDonut] = ["usage", "productivity", "time tracking", "screen time", "share", "pie", "ring", "statistics"],
        [WidgetKind.ScreenTimeApps] = ["usage", "productivity", "time tracking", "screen time", "app usage", "list", "limits"],
        [WidgetKind.CpuDetails] = ["processor", "clock speed", "frequency", "ghz", "cores", "threads", "uptime", "temperature", "task manager"],
        [WidgetKind.MemoryDetails] = ["ram", "committed", "cached", "page file", "swap", "paged pool", "task manager"],
        [WidgetKind.TopProcesses] = ["processes", "task manager", "apps", "cpu hog", "memory usage", "running"],
        [WidgetKind.NetworkDetails] = ["internet", "adapters", "ip address", "public ip", "wifi", "ethernet", "data used"],
        [WidgetKind.DiskSpace] = ["storage", "free space", "drives", "ssd", "hdd", "capacity"],
        [WidgetKind.BatteryDetails] = ["power", "battery health", "wear", "cycles", "voltage", "laptop", "time remaining"],
        [WidgetKind.PingDetails] = ["latency", "jitter", "packet loss", "internet", "connection quality", "ms"],
        [WidgetKind.GpuDetails] = ["graphics", "vram", "video memory", "temperature", "fan", "nvidia", "amd"],
        [WidgetKind.Volume] = ["sound", "audio", "speaker", "mute", "loudness"],
        [WidgetKind.Mixer] = ["sound", "audio", "per app volume", "speaker", "levels", "faders"],
        [WidgetKind.Shutdown] = ["power", "shutdown", "power off", "turn off", "switch off"],
        [WidgetKind.Restart] = ["power", "reboot", "restart", "reset"],
        [WidgetKind.Sleep] = ["power", "standby", "suspend"],
        [WidgetKind.Hibernate] = ["power", "suspend to disk", "deep sleep"],
        [WidgetKind.Lock] = ["power", "lock screen", "away", "security"],
        [WidgetKind.SignOut] = ["power", "log off", "log out", "logout", "sign off"],
        [WidgetKind.ScreenOff] = ["power", "monitor off", "screen off", "blank screen", "display"],

        // [S-3900] The quick-setting toggles.
        [WidgetKind.WifiToggle] = ["quick settings", "toggle", "wifi", "wlan", "wireless", "radio", "internet"],
        [WidgetKind.BluetoothToggle] = ["quick settings", "toggle", "radio", "wireless", "headphones"],
        [WidgetKind.AirplaneModeToggle] = ["quick settings", "toggle", "flight mode", "flight", "radios off", "offline"],
        [WidgetKind.EnergySaverToggle] = ["quick settings", "toggle", "battery saver", "power saving", "battery", "eco"],
        [WidgetKind.NightLightToggle] = ["quick settings", "toggle", "blue light", "warm screen", "night mode", "eye strain"],
        [WidgetKind.RotationLockToggle] = ["quick settings", "toggle", "auto rotate", "screen rotation", "tablet", "orientation"],
        [WidgetKind.DoNotDisturbToggle] = ["quick settings", "toggle", "dnd", "focus assist", "quiet hours", "notifications", "silence"],
        [WidgetKind.SoundToggle] = ["quick settings", "toggle", "mute", "unmute", "speaker", "audio", "volume"],
        [WidgetKind.MicrophoneToggle] = ["quick settings", "toggle", "mic", "mute", "unmute", "calls", "recording"],
        [WidgetKind.SpatialSoundToggle] = ["quick settings", "toggle", "windows sonic", "surround", "3d audio", "headphones"],
        [WidgetKind.MonoAudioToggle] = ["quick settings", "toggle", "mono", "accessibility", "one ear", "audio"],
        [WidgetKind.ColorFiltersToggle] = ["quick settings", "toggle", "grayscale", "color blind", "invert", "accessibility"],
        [WidgetKind.GameModeToggle] = ["quick settings", "toggle", "gaming", "games", "performance", "xbox"],
        [WidgetKind.Clipboard] = ["copy", "paste", "history", "snippets"],

        [WidgetKind.Clock] = ["time", "timezone", "watch", "world clock", "date", "analog", "digital"],
        [WidgetKind.Shortcuts] = ["link", "launcher", "bookmark", "url", "open", "pin", "quick access"],
        [WidgetKind.Notes] = ["notepad", "text", "memo", "writing", "markdown", "sticky note"],
        [WidgetKind.Scratchpad] = ["draw", "sketch", "drawing", "canvas", "whiteboard", "doodle", "pen"],
        [WidgetKind.Gallery] = ["photos", "pictures", "images", "slideshow", "wallpaper", "bing"],
        [WidgetKind.Countdown] = ["timer", "days until", "deadline", "event", "anniversary", "birthday"],
        [WidgetKind.Pomodoro] = ["timer", "focus", "productivity", "work break", "tomato", "session"],
        [WidgetKind.HabitTracker] = ["streaks", "daily", "routine", "goals", "checklist", "tracker"],
        [WidgetKind.Quote] = ["inspiration", "daily quote", "saying", "motivation"],
        [WidgetKind.Sun] = ["sunrise", "sunset", "daylight", "golden hour", "dawn", "dusk", "solar noon", "astronomy"],
        [WidgetKind.Moon] = ["moon phase", "full moon", "new moon", "moonrise", "moonset", "lunar", "astronomy"],
    };

    /// <summary>
    /// The services a widget reads from, searched as their own field so "google" finds every
    /// widget that can show a Google account.
    /// </summary>
    private static readonly Dictionary<WidgetKind, string[]> ProviderNames = new()
    {
        [WidgetKind.Email] = ["Microsoft", "Google", "IMAP"],
        [WidgetKind.MonthCalendar] = ["Microsoft", "Google", "CalDAV"],
        [WidgetKind.WeekCalendar] = ["Microsoft", "Google", "CalDAV"],
        [WidgetKind.DayCalendar] = ["Microsoft", "Google", "CalDAV"],
        [WidgetKind.YearCalendar] = ["Microsoft", "Google", "CalDAV"],
        [WidgetKind.Agenda] = ["Microsoft", "Google", "CalDAV"],

        [WidgetKind.Contacts] = ["Microsoft", "Google", "CardDAV"],
        [WidgetKind.Tasks] = ["Microsoft", "Google"],
    };

    /// <summary>The curated synonyms for a kind; empty for one that has none.</summary>
    public static IReadOnlyList<string> For(WidgetKind kind)
        => Keywords.TryGetValue(kind, out var words) ? words : [];

    /// <summary>The account providers a kind reads from; empty for one that needs no account.</summary>
    public static IReadOnlyList<string> Providers(WidgetKind kind)
        => ProviderNames.TryGetValue(kind, out var names) ? names : [];
}
