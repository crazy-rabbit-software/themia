using System.Text.Json.Serialization;
using Themia.Sdk;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10000] [S-10004] What one Device batteries widget remembers: where a battery counts as low, and
/// whether a device on its charger is listed. Every property is stored; copy each in <see cref="Clone"/>.
/// </summary>
public sealed class DeviceBatteriesConfig : WidgetConfig, IKitConfig, IKitRows
{
    /// <summary>[S-11028] A new list's rows.</summary>
    public const int DefaultRows = 4;

    /// <summary>[S-11028] How many rows the list shows (1..20); more devices scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11028] Every row has its two lines (the device, its kind or charging under it); the menu offers no choice.</summary>
    [JsonIgnore]
    public int Lines
    {
        get => 2;
        set
        {
            // ignore: a device's row always has its second line.
        }
    }

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

    /// <summary>[S-7050] A local source: no account.</summary>
    [JsonIgnore]
    public FeatureId? Needs => null;

    /// <summary>[S-7050] Nothing to set up: Windows says what there is.</summary>
    [JsonIgnore]
    public bool Configured => true;

    [JsonIgnore]
    public IReadOnlyList<string> AccountIds => [];

    /// <summary>[S-10001] The icon, the value and the second line are always the row's: no switch for them.</summary>
    [JsonIgnore]
    public KitShow Shows => DeviceBatteriesSpec.Spec.Optional;

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
        Rows = Rows,
    };
}
