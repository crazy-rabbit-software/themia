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
using Themia.Widget.Ping;

[assembly: ThemiaWidget("ping", typeof(PingModule))]

namespace Themia.Widget.Ping;

/// <summary>[S-1172] One host's latency: the last answer over its history, or signal bars ([S-6273]); the header's dot says online or offline ([S-1170]). SPEC.md beside this file.</summary>
public sealed class PingModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("ping"),
        ConfigType = typeof(PingConfig),
        CreateDefault = () => new PingConfig(),
        Json = PingJsonContext.Default,
        Keywords = ["latency", "network", "internet", "connection", "packet loss", "ms"],
        Label = PingStr.Widget_Ping_Label.Local(),
        Description = PingStr.WidgetsA_Catalog_Ping_Description.Local(),
        Glyph = Glyphs.Ping,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.PingReading, 0, 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => PingMenu.Lines(menu, (PingConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new PingMeter());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingConfig))]
internal sealed partial class PingJsonContext : JsonSerializerContext;
