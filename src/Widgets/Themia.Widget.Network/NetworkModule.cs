using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Network;

[assembly: ThemiaWidget("network", typeof(NetworkModule))]

namespace Themia.Widget.Network;

/// <summary>[S-1191] Network throughput: download as the reading over both directions' history, or both numbers ([S-6270]). SPEC.md beside this file.</summary>
public sealed class NetworkModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("network"),
        ConfigType = typeof(NetworkConfig),
        CreateDefault = () => new NetworkConfig(),
        Json = NetworkJsonContext.Default,
        Keywords = ["internet", "bandwidth", "wifi", "ethernet", "speed", "traffic", "up down"],
        Label = NetworkStr.WidgetsA_Catalog_Network_Label.Local(),
        Description = NetworkStr.WidgetsA_Catalog_Network_Description.Local(),
        Glyph = Glyphs.Network,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.NetworkReading, 0, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => NetworkSettings.Rows(rows.Rows, (NetworkConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new NetworkMeter());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkConfig))]
internal sealed partial class NetworkJsonContext : JsonSerializerContext;
