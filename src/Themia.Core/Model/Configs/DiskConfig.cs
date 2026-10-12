using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-1822] Whose throughput the disk meter shows.</summary>
public enum DiskScope
{
    /// <summary>One drive: <see cref="DiskConfig.Drive"/> (empty = the system drive).</summary>
    OneDrive = 0,

    /// <summary>Every drive together.</summary>
    AllDrives = 1,
}

/// <summary>[S-1820] Disk throughput (reads and writes) of one drive or every drive, with its history.</summary>
public sealed class DiskConfig : GaugeConfig
{
    /// <summary>
    /// Five minutes of history. The history is on, as it was in the original.
    /// </summary>
    public DiskConfig()
        : base(graphSpanSeconds: 300)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Disk;

    /// <summary>[S-1822] One drive (<see cref="Drive"/>) or every drive together.</summary>
    public DiskScope Scope { get; set; } = DiskScope.OneDrive;

    /// <summary>[S-1160] The drive to report on, as a root (<c>C:\</c>). Empty means the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    public override WidgetConfig Clone()
    {
        var copy = new DiskConfig
        {
            Scope = Scope,
            Drive = Drive,
        };
        CopyTo(copy);
        return copy;
    }

    /// <summary>[S-1160] [S-1822] Keep the drive as a root; one that is not a drive letter, or an unknown scope, is repaired loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        Drive ??= string.Empty;

        if (Drive.Length > 0)
        {
            if (DiskRules.Root(Drive) is { } root)
            {
                Drive = root;
            }
            else
            {
                notes.Add($"{Kind}: '{Drive}' is not a drive letter; reporting on the system drive");
                Drive = string.Empty;
            }
        }

        if (!Enum.IsDefined(Scope))
        {
            notes.Add($"{Kind}: scope {(int)Scope} is unknown; reporting on one drive");
            Scope = DiskScope.OneDrive;
        }

        return notes;
    }
}
