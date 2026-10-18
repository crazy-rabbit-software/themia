using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-4442] Which displays the brightness slider sets.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrightnessScreens>))]
public enum BrightnessScreens
{
    /// <summary>Every display whose brightness can be set, together, to one level (Karpo's default).</summary>
    All = 0,

    /// <summary>Only the display the widget is on.</summary>
    ThisDisplay = 1,
}
