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
using Themia.Widget.Cpu;

[assembly: ThemiaWidget("cpu", typeof(CpuModule))]

namespace Themia.Widget.Cpu;

/// <summary>[S-1140] Processor load across all cores: the reading and its history, or each processor (bars, tiles, their own graphs) or a gauge ([S-6311]). SPEC.md beside this file.</summary>
public sealed class CpuModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpu"),
        ConfigType = typeof(CpuConfig),
        CreateDefault = () => new CpuConfig(),
        Json = CpuJsonContext.Default,
        Keywords = ["processor", "load", "usage", "performance", "cores", "task manager", "system monitor"],
        Label = CpuStr.WidgetsA_Catalog_Cpu_Label.Local(),
        Description = CpuStr.WidgetsA_Catalog_Cpu_Description.Local(),
        Glyph = Glyphs.Cpu,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 0, Popular: 8),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => CpuSettings.Rows(rows.Rows, (CpuConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new CpuMeter());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuConfig))]
internal sealed partial class CpuJsonContext : JsonSerializerContext;
