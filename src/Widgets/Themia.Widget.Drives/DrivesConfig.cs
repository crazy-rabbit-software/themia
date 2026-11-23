using System.Text.Json.Serialization;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-16650]..[S-16654] A drive list, whatever its kind: the device list's one config shape ([S-16636]: the drives hidden,
/// the user's order, the block gap, the rows). Each kind is its own config type (a config's type is how it knows its id).
/// Every property is stored; copy each in <see cref="Clone"/>.
/// </summary>
public abstract class DriveListConfig : WidgetConfig, IKitRows, IDeviceListConfig, IDeviceParts
{
    /// <inheritdoc />
    public List<string> Hidden { get; set; } = [];

    /// <inheritdoc />
    public List<string> Order { get; set; } = [];

    /// <inheritdoc />
    public int Gap { get; set; }

    /// <inheritdoc />
    public List<DevicePart> HiddenParts { get; set; } = [];

    /// <inheritdoc cref="IDeviceListConfig.Rows" />
    public int Rows { get; set; } = DeviceLayout.DefaultRows;

    /// <summary>An item has one face: no lines to choose.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set
        {
            // ignore: a device item has its one face; the menu offers no lines.
        }
    }

    /// <summary>Roots that are no drive or are there twice, rows out of range: repaired to the nearest, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        Hidden = Roots(Hidden, "hidden", notes);
        Order = Roots(Order, "order", notes);
        Gap = DeviceOrder.Repaired(Gap, Kind.Value, notes);
        HiddenParts = DeviceOrder.Repaired(HiddenParts, Kind.Value, notes);
        var rows = KitSizing.ClampRows(Rows);
        if (rows != Rows)
        {
            notes.Add($"{Kind}: {Rows} rows is outside {KitSizing.MinRows}..{KitSizing.MaxRows}; using {rows}");
            Rows = rows;
        }

        return notes;
    }

    /// <summary>The drive roots of <paramref name="roots"/>, once each; anything else dropped with a note.</summary>
    private List<string> Roots(List<string> roots, string what, List<string> notes)
    {
        var kept = new List<string>(roots.Count);
        foreach (var root in roots)
        {
            if (Themia.Core.Widgets.DiskRules.Root(root) is { } held && !kept.Contains(held, StringComparer.OrdinalIgnoreCase))
            {
                kept.Add(held);
            }
            else
            {
                notes.Add($"{Kind}: {what} '{root}' is not a drive or is there twice; dropped");
            }
        }

        return kept;
    }

    /// <summary>A copy of the kind's own type with this one's settings.</summary>
    protected T CopyTo<T>(T copy)
        where T : DriveListConfig
    {
        copy.Hidden = [.. Hidden];
        copy.Order = [.. Order];
        copy.Gap = Gap;
        copy.HiddenParts = [.. HiddenParts];
        copy.Rows = Rows;
        return copy;
    }
}

/// <summary>[S-16650] The PC's drives.</summary>
public sealed class DrivesConfig : DriveListConfig
{
    public override WidgetConfig Clone() => CopyTo(new DrivesConfig());
}

/// <summary>[S-16650] The USB sticks and external disks plugged in.</summary>
public sealed class UsbSticksConfig : DriveListConfig
{
    public override WidgetConfig Clone() => CopyTo(new UsbSticksConfig());
}

/// <summary>[S-16650] The mapped network drives.</summary>
public sealed class NetworkDrivesConfig : DriveListConfig
{
    public override WidgetConfig Clone() => CopyTo(new NetworkDrivesConfig());
}
