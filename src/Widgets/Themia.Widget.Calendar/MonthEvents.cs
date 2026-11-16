using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-11544] How a month cell shows its day's events.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MonthEvents>))]
public enum MonthEvents
{
    /// <summary>[S-11547] Their titles: the config's lines of chips a day, a month view's.</summary>
    Chips = 0,

    /// <summary>A dot each, in the calendar's colour.</summary>
    Dots = 1,
}
