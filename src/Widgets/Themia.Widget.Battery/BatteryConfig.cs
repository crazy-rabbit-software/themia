using Themia.Core.SystemData;
using Themia.Sdk.Items;

namespace Themia.Core.Model;

/// <summary>
/// [S-1180] [S-17182] This PC's battery as a device item: what its item shows ([S-16672]). Its history is the battery charge
/// metric's graph, its facts the battery facts' ([S-14660]).
/// </summary>
public sealed class BatteryConfig : WidgetConfig, IItemParts, IReadsSystem
{
    /// <summary>[S-16672] The parts its item leaves out.</summary>
    public List<ItemPart> HiddenParts { get; set; } = [];

    /// <summary>[S-16950] The parts' order (a face has no columns).</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    /// <summary>[S-2513] [S-17182] The charge and the state, and the pack while there is a battery (the time to full).</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Battery, SystemSource.BatteryPack);

    public override WidgetConfig Clone() => new BatteryConfig { HiddenParts = [.. HiddenParts], PartOrder = [.. PartOrder] };
}
