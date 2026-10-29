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
using Themia.Widget.Sun;

[assembly: ThemiaWidget("sun", typeof(SunModule))]

namespace Themia.Widget.Sun;

/// <summary>[S-3102] The sun's day where the observer is: its path, rise, set and daylight. SPEC.md beside this file.</summary>
public sealed class SunModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sun"),
        ConfigType = typeof(SunConfig),
        CreateDefault = () => new SunConfig(),
        Json = SunJsonContext.Default,
        Keywords = ["sunrise", "sunset", "daylight", "golden hour", "dawn", "dusk", "solar noon", "astronomy"],
        Label = SunStr.WidgetsC_Sun_Label.Local(),
        Description = SunStr.WidgetsC_Sun_Desc.Local(),
        Glyph = Glyphs.Sun,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Weather, 1, 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => SunMenu.Lines(menu, (SunConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new SunView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SunConfig))]
internal sealed partial class SunJsonContext : JsonSerializerContext;
