using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// The language the app's own text is shown in. The nine the original shipped, twenty-one more
/// ([S-1112]), and following Windows. A closed set: each value picks a string table, not a parameter.
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
    Hindi = 10,
    Arabic = 11,
    Bengali = 12,
    Russian = 13,
    Indonesian = 14,
    Urdu = 15,
    Marathi = 16,
    Vietnamese = 17,
    Korean = 18,
    Dutch = 19,
    Turkish = 20,
    ChineseTraditional = 21,
    Telugu = 22,
    Hausa = 23,
    Punjabi = 24,
    Swahili = 25,
    Filipino = 26,
    Tamil = 27,
    Persian = 28,
    Thai = 29,
    Gujarati = 30,
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
    Saturday = 3,
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
