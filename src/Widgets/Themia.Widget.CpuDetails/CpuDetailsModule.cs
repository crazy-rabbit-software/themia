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
using Themia.Widget.CpuDetails;

[assembly: ThemiaWidget("cpuDetails", typeof(CpuDetailsModule))]

namespace Themia.Widget.CpuDetails;

/// <summary>[S-2405] The processor's figures beside its load: the effective clock as the hero, then cores, processes, threads, handles, uptime and temperature as the tile grows. SPEC.md beside this file.</summary>
public sealed class CpuDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpuDetails"),
        ConfigType = typeof(CpuDetailsConfig),
        CreateDefault = () => new CpuDetailsConfig(),
        Json = CpuDetailsJsonContext.Default,
        Keywords = ["processor", "clock speed", "frequency", "ghz", "cores", "threads", "uptime", "temperature", "task manager"],
        Label = CpuDetailsStr.Details_Catalog_Cpu_Label.Local(),
        Description = CpuDetailsStr.Details_Catalog_Cpu_Desc.Local(),
        Glyph = Glyphs.Cpu,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new CpuDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuDetailsConfig))]
internal sealed partial class CpuDetailsJsonContext : JsonSerializerContext;
