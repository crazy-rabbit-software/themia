using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-6580] Where a widget's content sits across its card: the start edge, the middle or the end
/// edge. Left and Right are the reading direction's start and end, as Windows aligns text.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContentAlign>))]
public enum ContentAlign
{
    Left = 0,
    Center = 1,
    Right = 2,
}
