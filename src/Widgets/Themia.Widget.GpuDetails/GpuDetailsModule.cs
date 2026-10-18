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
using Themia.Widget.GpuDetails;

[assembly: ThemiaWidget("gpuDetails", typeof(GpuDetailsModule))]

namespace Themia.Widget.GpuDetails;

/// <summary>[S-2430] One adapter's memory and sensors: video memory in use as the hero, then shared memory, temperature, fan and power where the driver reports them. SPEC.md beside this file.</summary>
public sealed class GpuDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuDetails"),
        ConfigType = typeof(GpuDetailsConfig),
        CreateDefault = () => new GpuDetailsConfig(),
        Json = GpuDetailsJsonContext.Default,
        Keywords = ["graphics", "vram", "video memory", "temperature", "fan", "nvidia", "amd"],
        Label = GpuDetailsStr.Details_Catalog_Gpu_Label.Local(),
        Description = GpuDetailsStr.Details_Catalog_Gpu_Desc.Local(),
        Glyph = Glyphs.Gpu,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => GpuDetailsSettings.Rows(rows.Rows, (GpuDetailsConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new GpuDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuDetailsConfig))]
internal sealed partial class GpuDetailsJsonContext : JsonSerializerContext;
