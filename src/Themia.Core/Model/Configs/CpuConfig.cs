using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-1140] Processor load: the reading and its history, nothing else ([S-1762]).</summary>
public sealed class CpuConfig : GaugeConfig
{
    /// <summary>A minute of history, as Task Manager shows.</summary>
    public CpuConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Cpu;

    public override WidgetConfig Clone()
    {
        var copy = new CpuConfig();
        CopyTo(copy);
        return copy;
    }
}
