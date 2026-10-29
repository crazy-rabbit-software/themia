using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Moon;

[assembly: ThemiaWidget("moon", typeof(MoonModule))]

namespace Themia.Widget.Moon;

/// <summary>[S-3105] The moon where the observer is: its disc, phase, rise and set, or the month's phases. SPEC.md beside this file.</summary>
public sealed class MoonModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("moon"),
        ConfigType = typeof(MoonConfig),
        CreateDefault = () => new MoonConfig(),
        Json = MoonJsonContext.Default,
        Keywords = ["moon phase", "full moon", "new moon", "moonrise", "moonset", "lunar", "astronomy"],
        Label = MoonStr.WidgetsC_Moon_Label.Local(),
        Description = MoonStr.WidgetsC_Moon_Desc.Local(),
        Glyph = Glyphs.Moon,
        DefaultWidth = 240,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Weather, 1, 1),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => MoonMenu.Lines(menu, (MoonConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new MoonView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MoonConfig))]
internal sealed partial class MoonJsonContext : JsonSerializerContext;
