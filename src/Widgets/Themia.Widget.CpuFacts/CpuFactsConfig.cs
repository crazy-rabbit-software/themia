using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-11012] Which of the processor's facts the card shows, in its order: speed, base speed, cores and logical processors by default.</summary>
public sealed class CpuFactsConfig : FactsConfig, IReadsSystem
{
    public CpuFactsConfig()
        : base(CpuDetailRules.Facts)
    {
    }

    /// <summary>[S-2513] The load, the counts and the processor's figures.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Cpu, SystemSource.Performance, SystemSource.Processor);

    public override WidgetConfig Clone()
    {
        var copy = new CpuFactsConfig();
        CopyTo(copy);
        return copy;
    }
}
