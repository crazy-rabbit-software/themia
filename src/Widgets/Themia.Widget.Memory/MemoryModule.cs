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
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.Memory;

[assembly: ThemiaWidget("memory", typeof(MemoryModule))]

namespace Themia.Widget.Memory;

/// <summary>[S-1130] Physical memory in use: the reading and its history, or the used share against the free, or a gauge ([S-6315]). SPEC.md beside this file.</summary>
public sealed class MemoryModule : IWidgetModule
{
    /// <summary>[S-2012] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

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
        Sizing = c => Sizing((MemoryConfig)c),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.MemoryReading, 0, 0),
    };

    /// <summary>
    /// [S-1130] [S-2438] Memory is converted to the sizing: 160..480 wide (a new one 220), the reading at
    /// 28 over its part (the Bar 240..480, a new one 280). The graph's history band and the gauge take 112..200 high (a new one 144: the
    /// band at least 32); the Bar is the reading over its one row, and the graph without its history
    /// the reading alone, each one height.
    /// </summary>
    internal static WidgetSizing Sizing(MemoryConfig config)
    {
        var reading = FamilySpace.ContentTop + FamilyType.Big28.LineHeight;
        double? fixedHeight = config.Style switch
        {
            MemoryStyle.Bar => reading + FamilySpace.S2 + FamilyShape.Row1 + FamilySpace.Inset,
            MemoryStyle.Graph when !config.DrawsHistory => reading + FamilySpace.Inset,
            _ => null,
        };

        // The Bar's row keeps its meter between its two figures only from 240 wide.
        var (least, start) = config.Style == MemoryStyle.Bar ? (240.0, 280.0) : (160.0, 220.0);
        return fixedHeight is { } h
            ? WidgetSizing.Range(new(least, h), new(start, h), new(480, h))
            : WidgetSizing.Range(new(160, 112), new(220, 144), new(480, 200));
    }

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
