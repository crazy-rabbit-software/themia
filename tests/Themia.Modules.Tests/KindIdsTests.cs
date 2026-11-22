using Themia.Modules.Tests.Modules;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-6901] Every kind's id, pinned: folding projects into a family (the toggles, the power buttons, the screen-time
/// widgets) moves code, never an id, so a placed widget of any kind still loads as itself. A kind added or retired on
/// purpose edits this list in the same commit.
/// </summary>
public sealed class KindIdsTests
{
    private static readonly string[] Ids =
    [
        "airAlder", "airBirch", "airGrass", "airIndex", "airMugwort", "airOlive", "airOzone", "airPm10", "airPm25",
        "airPollen", "airQuality", "airRagweed", "airUv", "airplaneModeToggle", "alarm", "audioVisualizer", "battery",
        "batteryCharge", "batteryChemistry", "batteryCycles", "batteryDesignCapacity", "batteryFullCapacity",
        "batteryHealth", "batteryManufacturer", "batteryRate", "batteryStatus", "batteryTemperature", "batteryTime",
        "batteryVoltage", "bluetooth", "bluetoothToggle", "brightness", "calendar", "calendarHeader", "clipboard",
        "clock", "colorFiltersToggle", "contacts", "countdown", "cpu", "cpuBaseSpeed", "cpuCoreCount", "cpuCores",
        "cpuHandles", "cpuLoad", "cpuLogical", "cpuProcesses", "cpuSpeed", "cpuTemperature", "cpuThreads", "cpuUptime",
        "date", "dawn", "daylight", "desktopItem", "deviceBatteries", "disk", "doNotDisturbToggle", "drive",
        "driveActivity", "driveCapacity", "driveFileSystem", "driveFree", "driveType", "driveUsed", "dusk", "email",
        "energySaverToggle", "exchangeHistory", "exchangeRate", "fence", "focusSession", "folder", "gallery",
        "gameModeToggle", "gpu", "gpuClock", "gpuDedicatedMemory", "gpuEngines", "gpuFan", "gpuLoad", "gpuMemoryClock",
        "gpuSharedMemory", "gpuTemperature", "habitTracker", "header", "hibernate", "icon", "lock", "memory",
        "memoryAvailable", "memoryCached", "memoryCommitted", "memoryInUse", "memoryNonPagedPool", "memoryPageFile",
        "memoryPagedPool", "microphoneToggle", "mixer", "monoAudioToggle", "moon", "moonrise", "moonset", "music",
        "musicAlbum", "musicArtist", "musicCover", "musicDuration", "musicElapsed", "musicNext", "musicPlayPause",
        "musicPrevious", "musicProgress",
        "network", "networkAdapters", "networkDownload", "networkLinkSpeed", "networkLocalAddress",
        "networkPublicAddress", "networkReceived", "networkSent", "networkUpload", "nextFullMoon", "nextMeeting",
        "nextNewMoon", "nightLightToggle", "notes", "notifications", "ping", "pingAnswered", "pingAverage",
        "pingJitter", "pingLoss", "pingMaximum", "pingMinimum", "pomodoro", "quote", "rainNextHour", "rainOutlook",
        "rainTotal", "recentFiles", "recycleBin", "removableDrives", "restart", "rotationLockToggle", "rss",
        "scratchpad", "screenOff", "screenTime", "screenTimeApps", "screenTimeChart", "screenTimeDonut", "shutdown",
        "signOut", "sleep", "solarNoon", "soundToggle", "spatialSoundToggle", "stock", "stockHistory", "stopwatch",
        "sun", "sunrise", "sunset", "tasks", "topProcesses", "upNext", "videoStream", "volume", "weatherCloudCover",
        "weatherFeelsLike", "weatherForecast", "weatherGusts", "weatherHighLow", "weatherHumidity", "weatherNow",
        "weatherPrecipitation", "weatherPressure", "weatherSunrise", "weatherSunset",
        "weatherTemperature", "weatherUv", "weatherWind", "weekPlanner", "wifiToggle",
    ];

    [Fact]
    public void S6901_the_builds_kind_ids_are_the_pinned_ones()
        => Assert.Equal(Ids.Order(StringComparer.Ordinal), ModuleRegistry.WidgetIds.Order(StringComparer.Ordinal));
}
