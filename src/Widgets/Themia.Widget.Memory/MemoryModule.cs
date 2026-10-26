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
using Themia.Widget.Memory;

[assembly: ThemiaWidget("memory", typeof(MemoryModule))]

namespace Themia.Widget.Memory;

/// <summary>[S-1130] Physical memory in use: the reading and its history, or the used share against the free, or a gauge ([S-6315]). SPEC.md beside this file.</summary>
public sealed class MemoryModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("memory"),
        ConfigType = typeof(MemoryConfig),
        CreateDefault = () => new MemoryConfig(),
        Json = MemoryJsonContext.Default,
        Keywords = ["ram", "usage", "performance", "task manager", "system monitor"],
        Label = MemoryStr.WidgetsA_Catalog_Memory_Label.Local(),
        Description = MemoryStr.WidgetsA_Catalog_Memory_Description.Local(),
        Glyph = Glyphs.Memory,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.MemoryReading, 0, 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu (the chart span under Style, [S-1122]): the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        MeterMenuLines.Lines(menu, (MemoryConfig)config);
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new MemoryMeter());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryConfig))]
internal sealed partial class MemoryJsonContext : JsonSerializerContext;
