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
using Themia.Widget.TopProcesses;

[assembly: ThemiaWidget("topProcesses", typeof(TopProcessesModule))]

namespace Themia.Widget.TopProcesses;

/// <summary>[S-2410] The busiest processes, ranked by processor or memory, one row each or one per program. SPEC.md beside this file.</summary>
public sealed class TopProcessesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("topProcesses"),
        ConfigType = typeof(TopProcessesConfig),
        CreateDefault = () => new TopProcessesConfig(),
        Json = TopProcessesJsonContext.Default,
        Keywords = ["processes", "task manager", "apps", "cpu hog", "memory usage", "running"],
        Label = TopProcessesStr.Details_Catalog_Processes_Label.Local(),
        Description = TopProcessesStr.Details_Catalog_Processes_Desc.Local(),
        Glyph = Glyphs.ListLayout,
        DefaultWidth = 300,
        DefaultHeight = 300,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => TopProcessesMenu.Lines(menu, (TopProcessesConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new ProcessesPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TopProcessesConfig))]
internal sealed partial class TopProcessesJsonContext : JsonSerializerContext;
