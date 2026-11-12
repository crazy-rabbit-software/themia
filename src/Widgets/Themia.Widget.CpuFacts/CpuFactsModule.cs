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
using Themia.Widget.CpuFacts;

[assembly: ThemiaWidget("cpuFacts", typeof(CpuFactsModule))]

namespace Themia.Widget.CpuFacts;

/// <summary>[S-11012] The processor's facts as fact rows, which and in what order its own pick. SPEC.md beside this file.</summary>
public sealed class CpuFactsModule : IWidgetModule
{
    /// <summary>[S-2011] A click on the card opens Task Manager.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpuFacts"),
        ConfigType = typeof(CpuFactsConfig),
        CreateDefault = () => new CpuFactsConfig(),
        Json = CpuFactsJsonContext.Default,
        Keywords = ["processor", "clock speed", "frequency", "ghz", "cores", "threads", "uptime", "temperature", "details", "task manager"],
        Label = CpuFactsStr.CpuFacts_Label.Local(),
        Description = CpuFactsStr.CpuFacts_Desc.Local(),
        Glyph = Glyphs.Cpu,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 3),
    };

    /// <summary>[S-11012] Its one line: which facts, in what order ([S-11007]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Facts(menu, (CpuFactsConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new CpuFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuFactsConfig))]
internal sealed partial class CpuFactsJsonContext : JsonSerializerContext;
