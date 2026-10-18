using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-468] Clockwise rotation for a camera mounted sideways or upside down. A closed set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StreamRotation>))]
public enum StreamRotation
{
    None = 0,
    Quarter = 90,
    Half = 180,
    ThreeQuarter = 270,
}
