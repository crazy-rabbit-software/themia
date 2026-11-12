using System.Text.Json.Serialization;
using Themia.Sdk.Kit;
using Themia.Widget.RemovableDrives;

namespace Themia.Core.Model;

/// <summary>[S-4660] [S-11027] The removable devices: how many rows the list shows, its one setting.</summary>
public sealed class RemovableDrivesConfig : WidgetConfig, IKitRows
{
    /// <summary>[S-11027] How many rows the list shows (1..20); more devices scroll inside them.</summary>
    public int Rows { get; set; } = RemovableDrivesPlan.DefaultRows;

    /// <summary>Every row has two lines: the name and the space line.</summary>
    [JsonIgnore]
    public int Lines
    {
        get => RemovableDrivesPlan.Lines;
        set
        {
            // ignore: a device's row always has its two lines; the menu offers no choice.
        }
    }

    public override WidgetConfig Clone() => new RemovableDrivesConfig { Rows = Rows };

    public override IReadOnlyList<string> Repair()
    {
        var rows = KitSizing.ClampRows(Rows);
        if (rows == Rows)
        {
            return [];
        }

        var note = $"{Kind}: {Rows} rows is outside {KitSizing.MinRows}..{KitSizing.MaxRows}; using {rows}";
        Rows = rows;
        return [note];
    }
}
