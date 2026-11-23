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
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Themia.Widget.Bluetooth;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("bluetooth", typeof(BluetoothModule))]
[assembly: ThemiaWidget("bluetoothDevice", typeof(BluetoothDeviceModule))]

namespace Themia.Widget.Bluetooth;

/// <summary>[S-1254] [S-16660] Bluetooth: the paired devices as device items, which are connected, their battery and signal. SPEC.md beside this file.</summary>
public sealed class BluetoothModule : IWidgetModule
{
    /// <summary>[S-2024] What a click on the card opens ([S-2000]).</summary>
    internal static CardOpen? Opens => CardOpen.Settings("bluetooth");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetooth"),
        ConfigType = typeof(BluetoothConfig),
        CreateDefault = () => new BluetoothConfig(),
        Json = BluetoothJsonContext.Default,
        Keywords = ["devices", "headphones", "wireless", "pairing"],
        Label = BluetoothStr.Bluetooth_List_Label.Local(),
        Description = BluetoothStr.WidgetsA_Catalog_Bluetooth_Description.Local(),
        Glyph = Glyphs.Bluetooth,
        Sizing = c => DeviceLayout.Sizing(((BluetoothConfig)c).Rows, ((BluetoothConfig)c).Gap),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.HoveredDevice], [LinkTopicId.HoveredDevice]),
        Add = new(AddFolderId.Devices, 1, 2),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => BluetoothMenu.Lines(menu, (BluetoothConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new BluetoothView(host);
}

/// <summary>[S-16662] One paired Bluetooth device on the device family's face. SPEC.md beside this file.</summary>
public sealed class BluetoothDeviceModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetoothDevice"),
        ConfigType = typeof(BluetoothDeviceConfig),
        CreateDefault = () => new BluetoothDeviceConfig(),
        Json = BluetoothJsonContext.Default,
        Keywords = ["bluetooth", "headphones", "mouse", "keyboard", "battery", "device", "wireless"],
        Label = BluetoothStr.Bluetooth_Device_Label.Local(),
        Description = BluetoothStr.Bluetooth_Device_Desc.Local(),
        Glyph = Glyphs.Bluetooth,
        Sizing = _ => DeviceLayout.Face(DeviceLayout.WidestLine(["Headphones · Connected · -88 dBm", "Couldn't read the Bluetooth devices"])),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.HoveredDevice], [LinkTopicId.HoveredDevice]),
        Add = new(AddFolderId.Devices, 1, 1),
    };

    /// <summary>[S-16662] Which device and what its face says: all on its page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => BluetoothMenu.Lines(menu, (BluetoothDeviceConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new BluetoothDeviceView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BluetoothConfig))]
[JsonSerializable(typeof(BluetoothDeviceConfig))]
internal sealed partial class BluetoothJsonContext : JsonSerializerContext;
