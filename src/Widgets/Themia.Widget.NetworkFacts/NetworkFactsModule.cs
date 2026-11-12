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
using Themia.Widget.NetworkFacts;

[assembly: ThemiaWidget("networkFacts", typeof(NetworkFactsModule))]

namespace Themia.Widget.NetworkFacts;

/// <summary>[S-2415] Down and up as Task Manager's network page has them: download as the hero, the session's totals, the addresses and every adapter. SPEC.md beside this file.</summary>
public sealed class NetworkFactsModule : IWidgetModule
{
    /// <summary>[S-2019] A click on the card opens Windows' network status page.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("network-status");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("networkFacts"),
        ConfigType = typeof(NetworkFactsConfig),
        CreateDefault = () => new NetworkFactsConfig(),
        Json = NetworkFactsJsonContext.Default,
        Keywords = ["internet", "adapters", "ip address", "public ip", "wifi", "ethernet", "data used"],
        Label = NetworkFactsStr.NetworkFacts_Label.Local(),
        Description = NetworkFactsStr.NetworkFacts_Desc.Local(),
        Glyph = Glyphs.Network,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.NetworkReading, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => NetworkFactsMenu.Lines(menu, (NetworkFactsConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new NetworkFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkFactsConfig))]
internal sealed partial class NetworkFactsJsonContext : JsonSerializerContext;
