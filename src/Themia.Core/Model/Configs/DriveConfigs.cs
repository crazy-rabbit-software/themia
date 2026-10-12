using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-4670] One drive's information: which drive.</summary>
public sealed class DriveConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Drive;

    /// <summary>The drive's root (<c>C:\</c>); empty for the system drive.</summary>
    public string Drive { get; set; } = string.Empty;

    /// <summary>Anything that is not a drive letter (a folder, a typo) is the system drive again, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (string.IsNullOrEmpty(Drive))
        {
            return [];
        }

        if (DiskRules.Root(Drive) is not { } root)
        {
            var bad = Drive;
            Drive = string.Empty;
            return [$"Drive widget named '{bad}', which is not a drive; showing the system drive"];
        }

        Drive = root;
        return [];
    }

    public override WidgetConfig Clone() => new DriveConfig { Drive = Drive };
}

/// <summary>[S-4660] The removable devices: no options, so no settings page ([S-1750]).</summary>
public sealed class RemovableDrivesConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RemovableDrives;

    public override WidgetConfig Clone() => new RemovableDrivesConfig();
}
