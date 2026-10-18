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
using Themia.Widget.ScreenOff;

[assembly: ThemiaWidget("screenOff", typeof(ScreenOffModule))]

namespace Themia.Widget.ScreenOff;

/// <summary>[S-8000] The ScreenOff power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class ScreenOffModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenOff"),
        ConfigType = typeof(ScreenOffConfig),
        CreateDefault = () => new ScreenOffConfig(),
        Json = ScreenOffJsonContext.Default,
        Keywords = ["power", "monitor off", "screen off", "blank screen", "display"],
        Label = ScreenOffStr.Power_Action_ScreenOff.Local(),
        Description = ScreenOffStr.WidgetsC_Power_Desc_ScreenOff.Local(),
        Glyph = Glyphs.Display,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 6),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenOffConfig))]
internal sealed partial class ScreenOffJsonContext : JsonSerializerContext;
