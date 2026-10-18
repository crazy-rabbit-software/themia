using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Bluetooth;

[assembly: ThemiaWidget("bluetooth", typeof(BluetoothModule))]

namespace Themia.Widget.Bluetooth;

/// <summary>[S-1254] Bluetooth: the paired devices, which are connected, their battery and signal. SPEC.md beside this file.</summary>
public sealed class BluetoothModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetooth"),
        ConfigType = typeof(BluetoothConfig),
        CreateDefault = () => new BluetoothConfig(),
        Json = BluetoothJsonContext.Default,
        Keywords = ["devices", "headphones", "wireless", "pairing"],
        Label = BluetoothStr.Widget_Bluetooth_Label.Local(),
        Description = BluetoothStr.WidgetsA_Catalog_Bluetooth_Description.Local(),
        Glyph = Glyphs.Bluetooth,
        DefaultWidth = 300,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 1, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => BluetoothSettings.Rows(rows.Rows, (BluetoothConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new BluetoothView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BluetoothConfig))]
internal sealed partial class BluetoothJsonContext : JsonSerializerContext;
