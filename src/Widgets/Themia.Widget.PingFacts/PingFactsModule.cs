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
using Themia.Widget.PingFacts;

[assembly: ThemiaWidget("pingFacts", typeof(PingFactsModule))]

namespace Themia.Widget.PingFacts;

/// <summary>[S-2426] Latency statistics over a window of probes: the latest as the hero, then average, range, jitter and loss. SPEC.md beside this file.</summary>
public sealed class PingFactsModule : IWidgetModule
{
    /// <summary>[S-2021] A click on the card opens Windows' network status page.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("network-status");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pingFacts"),
        ConfigType = typeof(PingFactsConfig),
        CreateDefault = () => new PingFactsConfig(),
        Json = PingFactsJsonContext.Default,
        Keywords = ["latency", "jitter", "packet loss", "internet", "connection quality", "ms"],
        Label = PingFactsStr.PingFacts_Label.Local(),
        Description = PingFactsStr.PingFacts_Desc.Local(),
        Glyph = Glyphs.Ping,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.PingReading, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => PingFactsMenu.Lines(menu, (PingFactsConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new PingFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingFactsConfig))]
internal sealed partial class PingFactsJsonContext : JsonSerializerContext;
