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
using Themia.Widget.MemoryFacts;

[assembly: ThemiaWidget("memoryFacts", typeof(MemoryFactsModule))]

namespace Themia.Widget.MemoryFacts;

/// <summary>[S-2408] Memory in use as the hero, then available, committed, cached, the pools and the page file as the tile grows. SPEC.md beside this file.</summary>
public sealed class MemoryFactsModule : IWidgetModule
{
    /// <summary>[S-2013] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("memoryFacts"),
        ConfigType = typeof(MemoryFactsConfig),
        CreateDefault = () => new MemoryFactsConfig(),
        Json = MemoryFactsJsonContext.Default,
        Keywords = ["ram", "committed", "cached", "page file", "swap", "paged pool", "task manager"],
        Label = MemoryFactsStr.MemoryFacts_Label.Local(),
        Description = MemoryFactsStr.MemoryFacts_Desc.Local(),
        Glyph = Glyphs.Memory,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.MemoryReading, 0, 2),
    };

    /// <summary>[S-11017] Its line: which facts, in what order ([S-11007]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Facts(menu, (MemoryFactsConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new MemoryFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryFactsConfig))]
internal sealed partial class MemoryFactsJsonContext : JsonSerializerContext;
