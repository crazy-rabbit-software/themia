using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

/// <summary>Which scale temperatures are asked for and shown in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
public enum TemperatureUnit
{
    Celsius = 0,
    Fahrenheit = 1,
}

/// <summary>[S-564] Which day a week starts on: a closed set, never a stringly day name.</summary>
[JsonConverter(typeof(LenientEnumConverter<WeekStartDay>))]
public enum WeekStartDay
{
    Monday = 0,
    Sunday = 1,

    /// <summary>The Iranian and Afghan week (Persian), which Windows' fa-IR also starts on Saturday.</summary>
    Saturday = 2,
}
