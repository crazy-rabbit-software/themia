using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Drive;
using Themia.Sdk.Kit;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("drive", typeof(DriveModule))]
[assembly: ThemiaWidget("usbStick", typeof(UsbStickModule))]
[assembly: ThemiaWidget("networkDrive", typeof(NetworkDriveModule))]

namespace Themia.Widget.Drive;

/// <summary>
/// [S-16646] One drive's widget as data (owner, 2026-11-23: "there should be individual widgets for the drive, bluetooth
/// device, usb stick, network drive ... they should still try to share the same structure in the backen"): its id, the
/// kind of drive it picks from (<see cref="Group"/>; null: any drive, the system drive by default), its words and glyph,
/// its place on the add page and why it shows no drive (none of its kind there).
/// </summary>
public sealed record OneDriveKind(
    string Id,
    DriveGroup? Group,
    LocalText Label,
    LocalText Description,
    string Glyph,
    IReadOnlyList<string> Keywords,
    int Rank,
    Func<Locale, string> Nothing);

/// <summary>[S-16646] The family's kinds: the Drive (any drive), the USB stick, the network drive.</summary>
public static class OneDriveKinds
{
    public static OneDriveKind Drive { get; } = new(
        "drive", null, DriveStr.Drives_Drive_Label.Local(), DriveStr.Drives_Drive_Desc.Local(), Glyphs.Disk,
        ["disk", "storage", "free space", "capacity", "ssd", "hdd", "network drive", "mapped drive", "nas", "share"], 0,
        locale => Strings.Get(locale, Str.Details_Disk_NotConnected));

    public static OneDriveKind UsbStick { get; } = new(
        "usbStick", DriveGroup.External, DriveStr.Drives_UsbStick_Label.Local(), DriveStr.Drives_UsbStick_Desc.Local(), Glyphs.Usb,
        ["usb", "usb stick", "flash drive", "sd card", "memory card", "external disk", "eject"], 2,
        locale => DriveStr.Drives_NothingPlugged.Text(locale));

    public static OneDriveKind NetworkDrive { get; } = new(
        "networkDrive", DriveGroup.Network, DriveStr.Drives_NetworkDrive_Label.Local(), DriveStr.Drives_NetworkDrive_Desc.Local(), Glyphs.NetworkDrive,
        ["network drive", "mapped drive", "nas", "share", "server"], 4,
        locale => DriveStr.Drives_NoShare.Text(locale));
}

/// <summary>
/// [S-4670] [S-16646] One drive's card, whatever its kind: its picture, the space line and the used share as a bar or a
/// ring (the device family's face, [S-16637]). Each kind is its module over this one. SPEC.md beside this file.
/// </summary>
public abstract class OneDriveModule : IWidgetModule
{
    protected OneDriveModule(OneDriveKind kind, Func<OneDriveConfig> create)
    {
        Kind = kind;
        Descriptor = new()
        {
            Id = new(kind.Id),
            ConfigType = create().GetType(),
            CreateDefault = create,
            Json = DriveJsonContext.Default,
            Keywords = kind.Keywords,
            Label = kind.Label,
            Description = kind.Description,
            Glyph = kind.Glyph,
            Sizing = c => DriveFace.Scaled(((OneDriveConfig)c).Style),
            Traits = WidgetTraits.HasItems,
            Links = LinkUse.Of([LinkTopicId.HoveredDrive], [LinkTopicId.HoveredDrive]),
            Add = new(AddFolderId.Devices, 2, kind.Rank),
        };
    }

    /// <summary>The kind it is.</summary>
    public OneDriveKind Kind { get; }

    /// <summary>[S-2032] A click on the card opens the drive's root in File Explorer: its own drive, else the system drive.</summary>
    internal static CardOpen? Opens(string drive, string? systemRoot)
        => CardOpen.Place(DiskRules.Root(drive) ?? DiskRules.Root(systemRoot));

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => DriveMenu.Lines(menu, Kind, (OneDriveConfig)config, DriveList.Now());

    public FrameworkElement CreateView(IWidgetHost host) => new DriveView(host, Kind);
}

/// <summary>[S-4670] One drive: any, the system drive by default.</summary>
public sealed class DriveModule() : OneDriveModule(OneDriveKinds.Drive, () => new DriveConfig());

/// <summary>[S-16646] One USB stick or external disk: the first plugged in by default.</summary>
public sealed class UsbStickModule() : OneDriveModule(OneDriveKinds.UsbStick, () => new UsbStickConfig());

/// <summary>[S-16646] One mapped network drive: the first there by default.</summary>
public sealed class NetworkDriveModule() : OneDriveModule(OneDriveKinds.NetworkDrive, () => new NetworkDriveConfig());

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DriveConfig))]
[JsonSerializable(typeof(UsbStickConfig))]
[JsonSerializable(typeof(NetworkDriveConfig))]
internal sealed partial class DriveJsonContext : JsonSerializerContext;
