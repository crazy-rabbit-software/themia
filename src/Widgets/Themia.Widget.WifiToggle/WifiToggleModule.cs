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
using Themia.Widget.WifiToggle;

[assembly: ThemiaWidget("wifiToggle", typeof(WifiToggleModule))]

namespace Themia.Widget.WifiToggle;

/// <summary>[S-8200] The Wi-Fi quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class WifiToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("wifiToggle"),
        ConfigType = typeof(WifiToggleConfig),
        CreateDefault = () => new WifiToggleConfig(),
        Json = WifiToggleJsonContext.Default,
        // [S-3900] The quick-setting toggles.
        Keywords = ["quick settings", "toggle", "wifi", "wlan", "wireless", "radio", "internet"],
        Label = WifiToggleStr.Toggles_Wifi_Label.Local(),
        Description = WifiToggleStr.Toggles_Wifi_Desc.Local(),
        Glyph = Glyphs.Wifi,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WifiToggleConfig))]
internal sealed partial class WifiToggleJsonContext : JsonSerializerContext;
