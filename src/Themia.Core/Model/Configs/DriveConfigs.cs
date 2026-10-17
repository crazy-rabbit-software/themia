using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6274] The drive card's looks: the capacity bar, or a ring of the used share.</summary>
[JsonConverter(typeof(StyleJsonConverter<DriveStyle>))]
public enum DriveStyle
{
    /// <summary>[S-4653] The picture and name, the capacity bar and the space line.</summary>
    Bar = 0,

    /// <summary>[S-6274] A ring of the used share with the share inside; the name and the space line beside or under it.</summary>
    Ring = 1,
}

/// <summary>[S-4670] One drive's information: which drive.</summary>
public sealed class DriveConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] DriveStyles =
    [
        StyleOption.Of(DriveStyle.Bar, Str.Styles_Io_Bar, 0xEDA2),
        StyleOption.Of(DriveStyle.Ring, Str.Styles_Io_Ring, 0xEA3A),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Drive;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("drive"),
        Kind = WidgetKind.Drive,
        ConfigType = typeof(DriveConfig),
        CreateDefault = () => new DriveConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["disk", "storage", "free space", "capacity", "ssd", "hdd", "network drive", "mapped drive", "nas", "share"],
        LabelId = Str.Drives_Drive_Label,
        DescriptionId = Str.Drives_Drive_Desc,
        Glyph = Glyphs.Disk,
        DefaultWidth = 280,
        DefaultHeight = 160,
    };

    /// <summary>[S-6274] The card's look.</summary>
    public DriveStyle Style { get; set; } = DriveStyle.Bar;

    IReadOnlyList<StyleOption> IStyled.Styles => DriveStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (DriveStyle)value;
    }

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

    public override WidgetConfig Clone() => new DriveConfig { Drive = Drive, Style = Style };
}

/// <summary>[S-4660] The removable devices: no options, so no settings page ([S-1750]).</summary>
public sealed class RemovableDrivesConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RemovableDrives;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("removableDrives"),
        Kind = WidgetKind.RemovableDrives,
        ConfigType = typeof(RemovableDrivesConfig),
        CreateDefault = () => new RemovableDrivesConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usb", "usb stick", "flash drive", "sd card", "memory card", "external disk", "eject", "safely remove"],
        LabelId = Str.Drives_Removable_Label,
        DescriptionId = Str.Drives_Removable_Desc,
        Glyph = Glyphs.Usb,
        DefaultWidth = 300,
        DefaultHeight = 200,
    };

    public override WidgetConfig Clone() => new RemovableDrivesConfig();
}
