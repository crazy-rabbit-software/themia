using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
public sealed class MemoryDetailsConfig : DetailConfig, IReadsSystem
{
    /// <summary>[S-2513] The memory figures and the page files.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Performance);

    public override WidgetConfig Clone()
    {
        var copy = new MemoryDetailsConfig();
        return copy;
    }
}
