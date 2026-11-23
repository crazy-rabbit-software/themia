using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Drives;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("drives", typeof(DrivesModule))]
[assembly: ThemiaWidget("usbSticks", typeof(UsbSticksModule))]
[assembly: ThemiaWidget("networkDrives", typeof(NetworkDrivesModule))]

namespace Themia.Widget.Drives;

/// <summary>
/// [S-16650] A drive list, whatever its kind: every drive of its kind as a device item ([S-16630]), the drives shown and
/// their order the user's, with the block gap between them where it is on. Each kind is its module over this one (owner,
/// 2026-11-23: "they should still try to share the same structure in the backen"). SPEC.md beside this file.
/// </summary>
public abstract class DriveListModule : IWidgetModule
{
    protected DriveListModule(DriveListKind kind, Func<DriveListConfig> create)
    {
        Kind = kind;
        Descriptor = new()
        {
            Id = new(kind.Id),
            ConfigType = create().GetType(),
            CreateDefault = create,
            Json = DrivesJsonContext.Default,
            Keywords = kind.Keywords,
            Label = kind.Label,
            Description = kind.Description,
            Glyph = kind.Glyph,
            Sizing = c => ItemLayout.Sizing((DriveListConfig)c, DeviceItems.Parts, reading: true),
            Traits = WidgetTraits.HasItems,
            Links = LinkUse.Of([LinkTopicId.HoveredDrive], [LinkTopicId.HoveredDrive]),
            Add = new(AddFolderId.Devices, 2, kind.Rank),
            Merges = kind.Merges,
        };
    }

    /// <summary>The kind it is.</summary>
    public DriveListKind Kind { get; }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-16655] Its options, all on its page ([S-3719]): the device list's (shown, order, gap, rows).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        var locale = Strings.Current;
        ItemMenuLines.List(menu, (DriveListConfig)config, DeviceItems.Parts, [.. DrivesRules.OfKind(DriveList.Now(), Kind).Select(d => new ItemChoice(d.Root, DriveRules.Title(locale, d), DriveRules.Glyph(d)))], name: ItemMenuLines.DevicePartName);
    }

    public FrameworkElement CreateView(IWidgetHost host) => new DrivesView(host, Kind);
}

/// <summary>[S-16650] The PC's drives: its disks and discs.</summary>
public sealed class DrivesModule() : DriveListModule(DriveListKinds.Drives, () => new DrivesConfig());

/// <summary>[S-16650] [S-16656] The USB sticks and external disks plugged in; a placed Removable drives widget becomes it.</summary>
public sealed class UsbSticksModule() : DriveListModule(DriveListKinds.UsbSticks, () => new UsbSticksConfig());

/// <summary>[S-16650] The mapped network drives.</summary>
public sealed class NetworkDrivesModule() : DriveListModule(DriveListKinds.NetworkDrives, () => new NetworkDrivesConfig());

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DrivesConfig))]
[JsonSerializable(typeof(UsbSticksConfig))]
[JsonSerializable(typeof(NetworkDrivesConfig))]
internal sealed partial class DrivesJsonContext : JsonSerializerContext;
