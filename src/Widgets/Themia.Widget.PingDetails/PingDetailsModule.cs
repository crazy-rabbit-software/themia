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
using Themia.Widget.PingDetails;

[assembly: ThemiaWidget("pingDetails", typeof(PingDetailsModule))]

namespace Themia.Widget.PingDetails;

/// <summary>[S-2426] Latency statistics over a window of probes: the latest as the hero, then average, range, jitter and loss. SPEC.md beside this file.</summary>
public sealed class PingDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pingDetails"),
        ConfigType = typeof(PingDetailsConfig),
        CreateDefault = () => new PingDetailsConfig(),
        Json = PingDetailsJsonContext.Default,
        Keywords = ["latency", "jitter", "packet loss", "internet", "connection quality", "ms"],
        Label = PingDetailsStr.Details_Catalog_Ping_Label.Local(),
        Description = PingDetailsStr.Details_Catalog_Ping_Desc.Local(),
        Glyph = Glyphs.Ping,
        DefaultWidth = 260,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.PingReading, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => PingDetailsSettings.Rows(rows.Rows, (PingDetailsConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new PingDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingDetailsConfig))]
internal sealed partial class PingDetailsJsonContext : JsonSerializerContext;
