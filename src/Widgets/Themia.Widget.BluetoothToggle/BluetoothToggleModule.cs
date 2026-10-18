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
using Themia.Widget.BluetoothToggle;

[assembly: ThemiaWidget("bluetoothToggle", typeof(BluetoothToggleModule))]

namespace Themia.Widget.BluetoothToggle;

/// <summary>[S-8300] The Bluetooth quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class BluetoothToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("bluetoothToggle"),
        ConfigType = typeof(BluetoothToggleConfig),
        CreateDefault = () => new BluetoothToggleConfig(),
        Json = BluetoothToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "radio", "wireless", "headphones"],
        Label = BluetoothToggleStr.Toggles_Bluetooth_Label.Local(),
        Description = BluetoothToggleStr.Toggles_Bluetooth_Desc.Local(),
        Glyph = Glyphs.Bluetooth,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BluetoothToggleConfig))]
internal sealed partial class BluetoothToggleJsonContext : JsonSerializerContext;
