using System.Text.Json.Serialization;
using Themia.Sdk;
using Themia.Core.Model;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10000] [S-10004] What one Device batteries widget remembers: where a battery counts as low, and
/// whether a device on its charger is listed. Every property is stored; copy each in <see cref="Clone"/>.
/// </summary>
public sealed class DeviceBatteriesConfig : WidgetConfig, IKitRows
{
    /// <summary>[S-11028] [S-16665] How many devices a new card stands (its natural height), 1..20; a new list three.</summary>
    public int Rows { get; set; } = DeviceLayout.DefaultRows;

    /// <summary>[S-16665] A device item has its one face: the menu offers no lines.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set
        {
            // ignore: a device item has its one face.
        }
    }

    /// <summary>[S-16666] One grid cell between two devices, so each stands as a card of its own.</summary>
    public bool Gap { get; set; }

    /// <summary>[S-10003] The thresholds offered: the tray's 20 % in the middle.</summary>
    public static readonly IReadOnlyList<int> Thresholds = [10, 15, 20, 25, 30];

    /// <summary>[S-10003] The default threshold: 20 %, where Windows calls a battery low.</summary>
    public const int DefaultThreshold = 20;

    /// <summary>[S-10001] The most rows the list holds: a ceiling, not a setting.</summary>
    public const int MaxDevices = 30;

    /// <summary>[S-10003] At or below this percentage a battery is low (its number in the Critical role).</summary>
    public int LowThreshold { get; set; } = DefaultThreshold;

    /// <summary>[S-10004] List a device that is charging (on by default: it still has a level).</summary>
    public bool ShowCharging { get; set; } = true;

    /// <summary>[S-10003] The threshold in use: a hand-edited value off the offered steps is the default.</summary>
    [JsonIgnore]
    public int EffectiveThreshold => Thresholds.Contains(LowThreshold) ? LowThreshold : DefaultThreshold;

    /// <summary>[S-10003] A threshold off the offered steps is put back to the default, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        if (!Thresholds.Contains(LowThreshold))
        {
            notes.Add($"deviceBatteries: low threshold {LowThreshold} is not offered; {DefaultThreshold}");
            LowThreshold = DefaultThreshold;
        }

        var rows = KitSizing.ClampRows(Rows);
        if (rows != Rows)
        {
            notes.Add($"deviceBatteries: {Rows} rows is outside {KitSizing.MinRows}..{KitSizing.MaxRows}; using {rows}");
            Rows = rows;
        }

        return notes;
    }

    public override WidgetConfig Clone() => new DeviceBatteriesConfig
    {
        LowThreshold = LowThreshold,
        ShowCharging = ShowCharging,
        Gap = Gap,
        Rows = Rows,
    };
}
