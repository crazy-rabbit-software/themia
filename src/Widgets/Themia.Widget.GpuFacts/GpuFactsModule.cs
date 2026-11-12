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
using Themia.Widget.GpuFacts;

[assembly: ThemiaWidget("gpuFacts", typeof(GpuFactsModule))]

namespace Themia.Widget.GpuFacts;

/// <summary>[S-2430] One adapter's memory and sensors: video memory in use as the hero, then shared memory, temperature, fan and power where the driver reports them. SPEC.md beside this file.</summary>
public sealed class GpuFactsModule : IWidgetModule
{
    /// <summary>[S-2015] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuFacts"),
        ConfigType = typeof(GpuFactsConfig),
        CreateDefault = () => new GpuFactsConfig(),
        Json = GpuFactsJsonContext.Default,
        Keywords = ["graphics", "vram", "video memory", "temperature", "fan", "nvidia", "amd"],
        Label = GpuFactsStr.GpuFacts_Label.Local(),
        Description = GpuFactsStr.GpuFacts_Desc.Local(),
        Glyph = Glyphs.Gpu,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 3),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => GpuFactsMenu.Lines(menu, (GpuFactsConfig)config, GpuAdapters.List());

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new GpuFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuFactsConfig))]
internal sealed partial class GpuFactsJsonContext : JsonSerializerContext;
