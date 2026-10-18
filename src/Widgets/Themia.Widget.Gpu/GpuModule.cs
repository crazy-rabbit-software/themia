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
using Themia.Widget.Gpu;

[assembly: ThemiaWidget("gpu", typeof(GpuModule))]

namespace Themia.Widget.Gpu;

/// <summary>[S-430] One graphics adapter's load: the reading over its history, or a row per engine type, or a gauge ([S-6320]). SPEC.md beside this file.</summary>
public sealed class GpuModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpu"),
        ConfigType = typeof(GpuConfig),
        CreateDefault = () => new GpuConfig(),
        Json = GpuJsonContext.Default,
        Keywords = ["graphics", "vram", "nvidia", "amd", "video card", "system monitor"],
        Label = GpuStr.WidgetsA_Catalog_Gpu_Label.Local(),
        Description = GpuStr.Shell_Meters_GpuDescription.Local(),
        Glyph = Glyphs.Gpu,
        DefaultWidth = 220,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => GpuSettings.Rows(rows.Rows, (GpuConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new GpuView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuConfig))]
internal sealed partial class GpuJsonContext : JsonSerializerContext;
