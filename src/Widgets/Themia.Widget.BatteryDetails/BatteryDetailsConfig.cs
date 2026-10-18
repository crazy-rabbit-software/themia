using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2423] What the battery itself reports: status, time, health, cycles, voltage, rate.</summary>
public sealed class BatteryDetailsConfig : DetailConfig, IReadsSystem
{
    /// <summary>[S-2513] The charge and what the battery itself reports.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Battery, SystemSource.BatteryPack);

    public override WidgetConfig Clone()
    {
        var copy = new BatteryDetailsConfig();
        return copy;
    }
}
