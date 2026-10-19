using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.NetworkDetails;

[assembly: ThemiaWidget("networkDetails", typeof(NetworkDetailsModule))]

namespace Themia.Widget.NetworkDetails;

/// <summary>[S-2415] Down and up as Task Manager's network page has them: download as the hero, the session's totals, the addresses and every adapter. SPEC.md beside this file.</summary>
public sealed class NetworkDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("networkDetails"),
        ConfigType = typeof(NetworkDetailsConfig),
        CreateDefault = () => new NetworkDetailsConfig(),
        Json = NetworkDetailsJsonContext.Default,
        Keywords = ["internet", "adapters", "ip address", "public ip", "wifi", "ethernet", "data used"],
        Label = NetworkDetailsStr.Details_Catalog_Network_Label.Local(),
        Description = NetworkDetailsStr.Details_Catalog_Network_Desc.Local(),
        Glyph = Glyphs.Network,
        DefaultWidth = 300,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.NetworkReading, 0, 1),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => NetworkDetailsMenu.Lines(menu, (NetworkDetailsConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new NetworkDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkDetailsConfig))]
internal sealed partial class NetworkDetailsJsonContext : JsonSerializerContext;
