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
using Themia.Widget.MemoryDetails;

[assembly: ThemiaWidget("memoryDetails", typeof(MemoryDetailsModule))]

namespace Themia.Widget.MemoryDetails;

/// <summary>[S-2408] Memory in use as the hero, then available, committed, cached, the pools and the page file as the tile grows. SPEC.md beside this file.</summary>
public sealed class MemoryDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("memoryDetails"),
        ConfigType = typeof(MemoryDetailsConfig),
        CreateDefault = () => new MemoryDetailsConfig(),
        Json = MemoryDetailsJsonContext.Default,
        Keywords = ["ram", "committed", "cached", "page file", "swap", "paged pool", "task manager"],
        Label = MemoryDetailsStr.Details_Catalog_Memory_Label.Local(),
        Description = MemoryDetailsStr.Details_Catalog_Memory_Desc.Local(),
        Glyph = Glyphs.Memory,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.MemoryReading, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new MemoryDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryDetailsConfig))]
internal sealed partial class MemoryDetailsJsonContext : JsonSerializerContext;
