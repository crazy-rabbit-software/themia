using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1130] Physical memory in use: the reading and its history, nothing else ([S-1762]).</summary>
public sealed class MemoryConfig : GaugeConfig
{
    public MemoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Memory;

    public override WidgetConfig Clone()
    {
        var copy = new MemoryConfig();
        CopyTo(copy);
        return copy;
    }
}
