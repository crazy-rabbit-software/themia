using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-11544] How a month cell shows its day's events.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MonthEvents>))]
public enum MonthEvents
{
    /// <summary>Their titles as chips.</summary>
    Chips = 0,

    /// <summary>A dot each, in the calendar's colour.</summary>
    Dots = 1,
}
