using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2405] The processor's figures beside its load: speed, cores, processes, uptime.</summary>
public sealed class CpuDetailsConfig : DetailConfig, IReadsSystem
{
    /// <summary>[S-2513] The load, the counts and the processor's figures.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Cpu, SystemSource.Performance, SystemSource.Processor);

    public override WidgetConfig Clone()
    {
        var copy = new CpuDetailsConfig();
        return copy;
    }
}
