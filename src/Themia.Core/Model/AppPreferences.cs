using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// The language the app's own text is shown in. The nine the original shipped, plus following
/// Windows. A closed set: each value picks a string table, not a parameter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppLanguage>))]
public enum AppLanguage
{
    /// <summary>The Windows display language, or English when it is not one of these.</summary>
    System = 0,
    English = 1,
    German = 2,
    Spanish = 3,
    French = 4,
    Italian = 5,
    Japanese = 6,
    Polish = 7,
    Portuguese = 8,
    Chinese = 9,
}

/// <summary>
/// [S-702] How the app behaves, as opposed to how widgets look (<see cref="DesignState"/>).
/// <para>
/// Kept apart from the design so an appearance preset or a design reset can never switch off, say,
/// game mode by accident.
/// </para>
/// </summary>
public sealed class AppPreferences
{
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>Hide the widgets while a full-screen game or presentation has the screen.</summary>
    public bool GameModeSuspend { get; set; }

    /// <summary>Whether Explorer's desktop icons are shown under the widgets.</summary>
    public bool DesktopIconsVisible { get; set; } = true;

    /// <summary>[S-3770] How every widget writes times and dates and measures temperature.</summary>
    public RegionPreferences Region { get; set; } = new();

    /// <summary>[S-3780] How often the widgets that ask an online service ask it.</summary>
    public RefreshPreferences Refresh { get; set; } = new();

    public AppPreferences Clone() => new()
    {
        Language = Language,
        GameModeSuspend = GameModeSuspend,
        DesktopIconsVisible = DesktopIconsVisible,
        Region = Region.Clone(),
        Refresh = Refresh.Clone(),
    };
}

/// <summary>[S-3771] The clock the widgets tell time on: Windows' own choice, or a fixed one.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockHours>))]
public enum ClockHours
{
    System = 0,
    Twelve = 1,
    TwentyFour = 2,
}

/// <summary>[S-3771] The day calendars start their weeks on.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FirstWeekday>))]
public enum FirstWeekday
{
    System = 0,
    Monday = 1,
    Sunday = 2,
}

/// <summary>[S-3771] The scale weather is shown in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TemperatureScale>))]
public enum TemperatureScale
{
    System = 0,
    Celsius = 1,
    Fahrenheit = 2,
}

/// <summary>
/// [S-3770] The regional formats every widget shares. Each one follows Windows' regional settings
/// until the user picks a value; no widget carries its own.
/// </summary>
public sealed class RegionPreferences
{
    public ClockHours Clock { get; set; } = ClockHours.System;

    public FirstWeekday WeekStart { get; set; } = FirstWeekday.System;

    public TemperatureScale Temperature { get; set; } = TemperatureScale.System;

    public RegionPreferences Clone() => new() { Clock = Clock, WeekStart = WeekStart, Temperature = Temperature };

    /// <summary>[S-3772] A value outside its set follows Windows again, loudly.</summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Clock))
        {
            repairs.Add($"region: clock {(int)Clock} unknown; following Windows");
            Clock = ClockHours.System;
        }

        if (!Enum.IsDefined(WeekStart))
        {
            repairs.Add($"region: week start {(int)WeekStart} unknown; following Windows");
            WeekStart = FirstWeekday.System;
        }

        if (!Enum.IsDefined(Temperature))
        {
            repairs.Add($"region: temperature {(int)Temperature} unknown; following Windows");
            Temperature = TemperatureScale.System;
        }

        return repairs;
    }
}

/// <summary>
/// [S-3780] The four update cadences, in minutes, one per kind of online source. Each is snapped to
/// its offered choices on load (<see cref="Sync.UpdateCadence"/>), so a hand edit cannot ask a free
/// service every second.
/// </summary>
public sealed class RefreshPreferences
{
    /// <summary>Weather now and forecast (open-meteo publishes every 15 minutes).</summary>
    public int WeatherMinutes { get; set; } = 30;

    /// <summary>Stocks (Yahoo, CoinGecko).</summary>
    public int MarketsMinutes { get; set; } = 5;

    /// <summary>News feeds.</summary>
    public int FeedsMinutes { get; set; } = 30;

    /// <summary>Mail, calendars and task lists from the signed-in accounts.</summary>
    public int AccountsMinutes { get; set; } = 2;

    public RefreshPreferences Clone() => new()
    {
        WeatherMinutes = WeatherMinutes,
        MarketsMinutes = MarketsMinutes,
        FeedsMinutes = FeedsMinutes,
        AccountsMinutes = AccountsMinutes,
    };
}
