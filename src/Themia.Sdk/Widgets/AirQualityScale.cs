using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-5103] Which air quality index the widget reads out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirQualityScale>))]
public enum AirQualityScale
{
    /// <summary>The European index for a place in Europe, the US index everywhere else.</summary>
    Auto = 0,

    /// <summary>The European Environment Agency's index (EAQI), 0 to 100+.</summary>
    European = 1,

    /// <summary>The US EPA's index, 0 to 500.</summary>
    Us = 2,
}
